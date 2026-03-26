using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TrayBaked.Models;

namespace TrayBaked.Windows;

public partial class PickProcessWindow : Window
{
    private List<ProcessItem> _allProcesses = new();
    private ListCollectionView? _view;

    public WatchedApp? CapturedApp { get; private set; }

    public PickProcessWindow()
    {
        InitializeComponent();
        WindowHelper.ApplyTitleBarTheme(this);
        Loaded += async (_, _) => await LoadProcessesAsync();
    }

    // Windows system / infrastructure processes that are never user-facing apps
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "csrss", "lsass", "winlogon", "wininit", "services", "smss",
        "dwm", "conhost", "condrv", "fontdrvhost", "spoolsv", "lsm",
        "RuntimeBroker", "SearchHost", "SearchIndexer", "SearchProtocolHost", "SearchFilterHost",
        "sihost", "taskhostw", "ctfmon", "dllhost", "WmiPrvSE", "WmiApSrv",
        "MsMpEng", "NisSrv", "SecurityHealthSystray", "SecurityHealthService",
        "SgrmBroker", "System", "Registry", "Idle", "MemCompression",
        "sppsvc", "SppExtComObj", "TrustedInstaller", "TiWorker",
        "audiodg", "dasHost", "DeviceCensus", "MusNotifyIcon",
        "TextInputHost", "ShellExperienceHost", "StartMenuExperienceHost",
        "explorer",
        "SystemSettings", "ApplicationFrameHost",
        "backgroundTaskHost", "WUDFHost", "WerFault", "WerSvc",
        "PerfHost", "msdtc", "vm3dservice", "vmacthlp",
        "uhssvc", "MoUsoCoreWorker", "wuauclt", "UsoClient",
        "UserOOBEBroker", "OfficeClickToRun",
        "TabTip", "InputHost",
    };

    private async Task LoadProcessesAsync()
    {
        var savedSorts = _view?.SortDescriptions.ToList();

        LoadingOverlay.Visibility = Visibility.Visible;
        RefreshButton.IsEnabled   = false;

        _allProcesses = await Task.Run(() =>
            Process.GetProcesses()
                .Where(p => !SystemProcessNames.Contains(p.ProcessName))
                .Select(p =>
                {
                    string windowTitle = "";
                    try { windowTitle = p.MainWindowTitle; } catch { }

                    string? exePath = AppLauncher.GetExePathNative(p.Id);
                    string? aumid   = AppLauncher.GetAumidNative(p.Id);

                    string displayName;
                    if (!string.IsNullOrWhiteSpace(windowTitle))
                    {
                        displayName = windowTitle;
                    }
                    else
                    {
                        // Use the exe's FileDescription (e.g. "Microsoft Outlook" for olk.exe)
                        // so the list shows a human-readable name rather than the raw process name.
                        string? versionName = null;
                        if (exePath != null)
                        {
                            try
                            {
                                var vi = FileVersionInfo.GetVersionInfo(exePath);
                                versionName = !string.IsNullOrWhiteSpace(vi.FileDescription) ? vi.FileDescription
                                            : !string.IsNullOrWhiteSpace(vi.ProductName)     ? vi.ProductName
                                            : null;
                            }
                            catch { }
                        }
                        displayName = versionName ?? p.ProcessName;
                    }

                    return new ProcessItem(displayName, p.ProcessName, p.Id, exePath, aumid);
                })
                .Where(p => !string.IsNullOrEmpty(p.ExePath) || !string.IsNullOrEmpty(p.Aumid))
                .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(p => !string.IsNullOrEmpty(p.Aumid) ? 1 : 0)
                    .ThenByDescending(p => p.DisplayName != p.ProcessName ? 1 : 0)
                    .First())
                .ToList()
        );

        // Wrap in a ListCollectionView so DataGrid's built-in column sorting and our
        // filter predicate both work without replacing ItemsSource on every keystroke.
        _view = new ListCollectionView(_allProcesses) { Filter = FilterPredicate };
        ProcessGrid.ItemsSource = _view;

        // Apply sorts AFTER assigning ItemsSource — the DataGrid clears SortDescriptions
        // on the view during binding, so any sorts added before the assignment are lost.
        _view.SortDescriptions.Clear();
        var sorts = savedSorts is { Count: > 0 }
            ? savedSorts
            : [new SortDescription(nameof(ProcessItem.DisplayName), ListSortDirection.Ascending)];
        foreach (var s in sorts)
            _view.SortDescriptions.Add(s);

        LoadingOverlay.Visibility = Visibility.Collapsed;
        RefreshButton.IsEnabled   = true;
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not ProcessItem p) return true;
        string filter = FilterBox.Text.Trim();
        if (string.IsNullOrEmpty(filter)) return true;
        return p.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void AcceptSelection()
    {
        if (ProcessGrid.SelectedItem is not ProcessItem p) return;
        CapturedApp = new WatchedApp
        {
            Name         = p.DisplayName,
            ProcessName  = p.ProcessName,
            StartCommand = p.StartCommand
        };
        DialogResult = true;
        Close();
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => _view?.Refresh();
    private async void Refresh_Click(object sender, RoutedEventArgs e)        => await LoadProcessesAsync();
    private void Add_Click(object sender, RoutedEventArgs e)                  => AcceptSelection();
    private void Cancel_Click(object sender, RoutedEventArgs e)               { DialogResult = false; Close(); }
}

// ─── Process view-model ──────────────────────────────────────────────────────

public record ProcessItem(
    string DisplayName,
    string ProcessName,
    int    Pid,
    string? ExePath,
    string? Aumid)
{
    public string StartCommand => Aumid ?? ExePath ?? "";
    public string TypeText     => Aumid != null ? "Store" : "Desktop";
    public bool   IsStore      => Aumid != null;
}
