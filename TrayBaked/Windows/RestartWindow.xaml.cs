using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TrayBaked.Models;

namespace TrayBaked.Windows;

public partial class RestartWindow : Window
{
    private readonly List<(WatchedApp App, bool Running)> _appStates;
    private readonly ObservableCollection<RestartStatusItem> _statusItems = new();
    private int _finalCount;

    public RestartWindow(List<(WatchedApp App, bool Running)> appStates)
    {
        _appStates = appStates;
        InitializeComponent();
        WindowHelper.ApplyTitleBarTheme(this);

        var icon = AppIconHelper.GetIconBitmapSource();
        Icon = icon;
        HeaderIcon.Source = icon;

        LoadPhase1();
    }

    private void LoadPhase1()
    {
        foreach (var (app, running) in _appStates)
        {
            AppCheckList.Items.Add(new AppCheckItem
            {
                App          = app,
                DisplayLabel = running ? app.Name : $"{app.Name}  (not running)",
                IsChecked    = running
            });
        }
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        var selected = AppCheckList.Items
            .OfType<AppCheckItem>()
            .Where(item => item.IsChecked)
            .Select(item => item.App)
            .ToList();

        if (selected.Count == 0) return;

        TransitionToPhase2(selected);
    }

    private void TransitionToPhase2(List<WatchedApp> apps)
    {
        Title = "TrayBaked — Restarting…";
        HeaderSubtitle.Text = "Restarting applications…";
        Topmost = false;

        Phase1Panel.Visibility   = Visibility.Collapsed;
        Phase1Buttons.Visibility = Visibility.Collapsed;
        Phase2List.Visibility    = Visibility.Visible;
        Phase2Buttons.Visibility = Visibility.Visible;

        foreach (var app in apps)
        {
            var item = new RestartStatusItem { AppName = app.Name };
            _statusItems.Add(item);
        }

        Phase2List.ItemsSource = _statusItems;

        var progress = new Progress<RestartStatus>(OnProgress);
        _ = AppLauncher.RestartAppsAsync(apps, progress);
    }

    private void OnProgress(RestartStatus status)
    {
        var item = _statusItems.FirstOrDefault(i => i.AppName == status.AppName);
        if (item is null) return;

        item.StatusText = status.StatusText;
        item.Kind       = status.Kind;

        if (status.Kind is StatusKind.Success or StatusKind.Error)
            _finalCount++;

        if (_finalCount >= _statusItems.Count)
        {
            Title = "TrayBaked — Done";
            HeaderSubtitle.Text = "All done";
            CloseBtn.IsEnabled  = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)   { DialogResult = false; Close(); }
    private void CloseBtn_Click(object sender, RoutedEventArgs e)  { Close(); }
}

// ─── View-models ─────────────────────────────────────────────────────────────

public class AppCheckItem
{
    public required WatchedApp App          { get; init; }
    public required string     DisplayLabel { get; init; }
    public          bool       IsChecked    { get; set;  }
}

public class RestartStatusItem : INotifyPropertyChanged
{
    public string AppName { get; init; } = "";

    private string _statusText = "Waiting…";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private StatusKind _kind = StatusKind.Waiting;
    public StatusKind Kind
    {
        get => _kind;
        set { _kind = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
