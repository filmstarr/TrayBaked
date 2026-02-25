using TrayBaked.Models;

namespace TrayBaked.Forms;

class RestartForm : Form
{
    private readonly List<(WatchedApp App, bool Running)> _appStates;
    private readonly Dictionary<string, ListViewItem> _statusItems = new();
    private int _finalCount;

    // Shared
    private Label _headerLabel = null!;

    // Phase 1
    private CheckedListBox _checkList = null!;
    private Button _restartBtn = null!;
    private Button _cancelBtn = null!;

    // Phase 2
    private ListView _listView = null!;
    private Button _closeBtn = null!;

    private static readonly Rectangle ListBounds = new(12, 46, 400, 150);

    public RestartForm(List<(WatchedApp App, bool Running)> appStates)
    {
        _appStates = appStates;
        BuildUI();
    }

    private void BuildUI()
    {
        Text = "TrayBaked";
        ClientSize = new Size(424, 240);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        _headerLabel = new Label
        {
            Text = "Explorer restarted. Select apps to restart:",
            Location = new Point(12, 14),
            Size = new Size(400, 24),
            Font = new Font(Font.FontFamily, 9.5f)
        };

        // --- Phase 1: CheckedListBox ---
        _checkList = new CheckedListBox
        {
            Bounds = ListBounds,
            CheckOnClick = true,
            Font = new Font(Font.FontFamily, 9.5f)
        };

        foreach (var (app, running) in _appStates)
        {
            string label = running ? app.Name : $"{app.Name}  (not running)";
            _checkList.Items.Add(label, running);
        }

        _restartBtn = new Button
        {
            Text = "Restart",
            Size = new Size(80, 28),
            Location = new Point(232, 204),
            FlatStyle = FlatStyle.System
        };
        _restartBtn.Click += RestartBtn_Click;

        _cancelBtn = new Button
        {
            Text = "Cancel",
            Size = new Size(80, 28),
            Location = new Point(320, 204),
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.System
        };

        // --- Phase 2: ListView (same bounds, hidden initially) ---
        _listView = new ListView
        {
            Bounds = ListBounds,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font(Font.FontFamily, 9.5f),
            Visible = false
        };
        _listView.Columns.Add("Application", 210);
        _listView.Columns.Add("Status", 172);

        _closeBtn = new Button
        {
            Text = "Close",
            Size = new Size(80, 28),
            Location = new Point(320, 204),
            Enabled = false,
            FlatStyle = FlatStyle.System,
            Visible = false
        };
        _closeBtn.Click += (_, _) => Close();

        AcceptButton = _restartBtn;
        CancelButton = _cancelBtn;

        Controls.AddRange(new Control[]
        {
            _headerLabel, _checkList, _listView,
            _restartBtn, _cancelBtn, _closeBtn
        });
    }

    private void RestartBtn_Click(object? sender, EventArgs e)
    {
        var selectedApps = new List<WatchedApp>();
        for (int i = 0; i < _checkList.Items.Count; i++)
        {
            if (_checkList.GetItemChecked(i))
                selectedApps.Add(_appStates[i].App);
        }

        if (selectedApps.Count == 0) return;

        TransitionToProgress(selectedApps);
    }

    private void TransitionToProgress(List<WatchedApp> apps)
    {
        Text = "TrayBaked — Restarting…";
        _headerLabel.Visible = false;

        _checkList.Visible = false;
        _restartBtn.Visible = false;
        _cancelBtn.Visible = false;

        foreach (var app in apps)
        {
            var item = new ListViewItem(app.Name);
            item.SubItems.Add("Waiting…");
            item.ForeColor = SystemColors.GrayText;
            _listView.Items.Add(item);
            _statusItems[app.Name] = item;
        }

        _listView.Visible = true;
        _closeBtn.Visible = true;
        TopMost = false;

        AcceptButton = null;
        CancelButton = _closeBtn;

        var progress = new Progress<RestartStatus>(OnProgress);
        _ = AppLauncher.RestartAppsAsync(apps, progress);
    }

    private void OnProgress(RestartStatus status)
    {
        if (!_statusItems.TryGetValue(status.AppName, out var item)) return;

        item.SubItems[1].Text = status.StatusText;
        item.ForeColor = status.Kind switch
        {
            StatusKind.Waiting    => SystemColors.GrayText,
            StatusKind.InProgress => SystemColors.WindowText,
            StatusKind.Success    => Color.Green,
            StatusKind.Error      => Color.Firebrick,
            _                     => SystemColors.WindowText
        };

        if (status.Kind is StatusKind.Success or StatusKind.Error)
            _finalCount++;

        if (_finalCount >= _statusItems.Count)
        {
            _closeBtn.Enabled = true;
            Text = "TrayBaked — Done";
        }
    }
}
