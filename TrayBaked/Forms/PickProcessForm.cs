using System.Diagnostics;
using TrayBaked.Models;

namespace TrayBaked.Forms;

class PickProcessForm : Form
{
    private TextBox _filterBox = null!;
    private ListView _listView = null!;

    public WatchedApp? CapturedApp { get; private set; }

    private List<ProcessInfo> _allProcesses = new();

    // ExePath is the raw exe path; Aumid is set for Store/packaged apps.
    // StartCommand is whichever is more useful for restarting: Aumid takes priority.
    private record ProcessInfo(
        string DisplayName,
        string ProcessName,
        int Pid,
        string? ExePath,
        string? Aumid)
    {
        public string StartCommand => Aumid ?? ExePath ?? "";
    }

    public PickProcessForm()
    {
        BuildUI();
        LoadProcesses();
    }

    private void BuildUI()
    {
        Text = "Add from Running Process";
        Size = new Size(720, 460);
        MinimumSize = new Size(600, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;

        var filterLabel = new Label
        {
            Text = "Filter:",
            AutoSize = true,
            Location = new Point(12, 14),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _filterBox = new TextBox
        {
            Location = new Point(50, 11),
            Width = 400,
            PlaceholderText = "Type to filter by name or process…"
        };
        _filterBox.TextChanged += (_, _) => ApplyFilter();

        var refreshBtn = new Button
        {
            Text = "Refresh",
            Size = new Size(72, 24),
            Location = new Point(458, 10),
            FlatStyle = FlatStyle.System
        };
        refreshBtn.Click += (_, _) => LoadProcesses();

        _listView = new ListView
        {
            Location = new Point(12, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = true,
            Font = new Font(Font.FontFamily, 9f),
            HideSelection = false
        };

        _listView.Columns.Add("Display Name", 180);
        _listView.Columns.Add("Process Name", 120);
        _listView.Columns.Add("PID", 50, HorizontalAlignment.Right);
        _listView.Columns.Add("Type", 60);
        _listView.Columns.Add("Path / AUMID", 270);

        _listView.DoubleClick += (_, _) => AcceptSelection();

        SizeChanged += (_, _) =>
        {
            _listView.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 88);
            _filterBox.Width = ClientSize.Width - 160;
            refreshBtn.Location = new Point(_filterBox.Right + 8, refreshBtn.Location.Y);
        };

        var okBtn = new Button
        {
            Text = "Add",
            Size = new Size(80, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            FlatStyle = FlatStyle.System
        };
        okBtn.Click += (_, _) => AcceptSelection();

        var cancelBtn = new Button
        {
            Text = "Cancel",
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            FlatStyle = FlatStyle.System
        };

        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(4)
        };
        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Controls.Add(okBtn);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        _listView.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 88);

        Controls.Add(filterLabel);
        Controls.Add(_filterBox);
        Controls.Add(refreshBtn);
        Controls.Add(_listView);
        Controls.Add(btnPanel);
    }

    // Windows system/infrastructure processes that are never user-facing apps
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
        "explorer",  // explorer itself is handled separately by TrayBaked
        "SystemSettings", "ApplicationFrameHost",
        "backgroundTaskHost", "WUDFHost", "WerFault", "WerSvc",
        "PerfHost", "msdtc", "vm3dservice", "vmacthlp",
        "uhssvc", "MoUsoCoreWorker", "wuauclt", "UsoClient",
        "UserOOBEBroker", "OfficeClickToRun",
        "TabTip", "InputHost",
    };

    private void LoadProcesses()
    {
        _allProcesses = Process.GetProcesses()
            .Where(p => !SystemProcessNames.Contains(p.ProcessName))
            .Select(p =>
            {
                string windowTitle = "";
                try { windowTitle = p.MainWindowTitle; } catch { }

                string displayName = !string.IsNullOrWhiteSpace(windowTitle)
                    ? windowTitle
                    : p.ProcessName;

                // Use the reliable native API instead of MainModule (which fails for Store apps)
                string? exePath = AppLauncher.GetExePathNative(p.Id);
                string? aumid  = AppLauncher.GetAumidNative(p.Id);

                return new ProcessInfo(displayName, p.ProcessName, p.Id, exePath, aumid);
            })
            .Where(p => !string.IsNullOrEmpty(p.ExePath) || !string.IsNullOrEmpty(p.Aumid))
            // Deduplicate by process name: prefer instances with a window title, then highest working set
            .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(p => !string.IsNullOrEmpty(p.Aumid) ? 1 : 0)
                .ThenByDescending(p => p.DisplayName != p.ProcessName ? 1 : 0)
                .First())
            .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string filter = _filterBox.Text.Trim();

        var visible = string.IsNullOrEmpty(filter)
            ? _allProcesses
            : _allProcesses.Where(p =>
                p.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase));

        _listView.BeginUpdate();
        _listView.Items.Clear();
        foreach (var p in visible)
        {
            var item = new ListViewItem(p.DisplayName);
            item.SubItems.Add(p.ProcessName);
            item.SubItems.Add(p.Pid.ToString());
            item.SubItems.Add(p.Aumid != null ? "Store" : "Desktop");
            item.SubItems.Add(p.StartCommand);
            item.Tag = p;

            // Tint Store app rows slightly to make them easy to spot
            if (p.Aumid != null)
                item.ForeColor = SystemColors.HotTrack;

            _listView.Items.Add(item);
        }
        _listView.EndUpdate();
    }

    private void AcceptSelection()
    {
        if (_listView.SelectedItems.Count == 0) return;

        var p = (ProcessInfo)_listView.SelectedItems[0].Tag!;

        CapturedApp = new WatchedApp
        {
            Name         = p.DisplayName,
            ProcessName  = p.ProcessName,
            StartCommand = p.StartCommand
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
