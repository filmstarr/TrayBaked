using Microsoft.Win32;
using TrayBaked.Models;

namespace TrayBaked.Forms;

class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Action<AppConfig> _onSave;

    private DataGridView _grid = null!;
    private NumericUpDown _debounceSpinner = null!;
    private CheckBox _autoStartCheckbox = null!;

    private const string AutoStartKeyName = "TrayBaked";
    private const string AutoStartKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public SettingsForm(AppConfig config, Action<AppConfig> onSave)
    {
        _config = config;
        _onSave = onSave;
        BuildUI();
        LoadData();
    }

    private void BuildUI()
    {
        Text = "TrayBaked — Settings";
        Size = new Size(660, 490);
        MinimumSize = new Size(560, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        Padding = new Padding(12);

        var appsLabel = new Label
        {
            Text = "Watched Applications",
            AutoSize = true,
            Dock = DockStyle.None,
            Location = new Point(12, 12),
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
        };

        // --- DataGridView ---
        _grid = new DataGridView
        {
            Location = new Point(12, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            EditMode = DataGridViewEditMode.EditOnEnter,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font(Font.FontFamily, 9f)
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Display Name",
            FillWeight = 22
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ProcessName",
            HeaderText = "Process Name",
            FillWeight = 20
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "StartCommand",
            HeaderText = "Start Command (optional fallback path)",
            FillWeight = 38
        });

        // --- Row buttons ---
        var addBtn = new Button
        {
            Text = "Add Row",
            Size = new Size(80, 26),
            FlatStyle = FlatStyle.System
        };
        addBtn.Click += AddRow_Click;

        var captureBtn = new Button
        {
            Text = "From Process…",
            Size = new Size(110, 26),
            FlatStyle = FlatStyle.System
        };
        captureBtn.Click += CaptureProcess_Click;

        var removeBtn = new Button
        {
            Text = "Remove",
            Size = new Size(80, 26),
            FlatStyle = FlatStyle.System
        };
        removeBtn.Click += RemoveRow_Click;

        var rowBtnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        rowBtnPanel.Controls.Add(addBtn);
        rowBtnPanel.Controls.Add(captureBtn);
        rowBtnPanel.Controls.Add(removeBtn);

        // --- Debounce ---
        var debounceLabel = new Label
        {
            Text = "Debounce seconds:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _debounceSpinner = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 120,
            Width = 55
        };

        var debouncePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        debouncePanel.Controls.Add(debounceLabel);
        debouncePanel.Controls.Add(_debounceSpinner);

        // --- Auto-start ---
        _autoStartCheckbox = new CheckBox
        {
            Text = "Start TrayBaked automatically with Windows",
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        // --- Save / Cancel ---
        var saveBtn = new Button
        {
            Text = "Save",
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.System
        };
        saveBtn.Click += Save_Click;

        var cancelBtn = new Button
        {
            Text = "Cancel",
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.System
        };

        var saveCancelPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        saveCancelPanel.Controls.Add(cancelBtn);
        saveCancelPanel.Controls.Add(saveBtn);

        AcceptButton = saveBtn;
        CancelButton = cancelBtn;

        // --- Bottom panel layout ---
        var bottomPanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Bottom,
            Height = 108,
            Padding = new Padding(8, 4, 8, 8)
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        bottomPanel.Controls.Add(rowBtnPanel, 0, 0);
        bottomPanel.Controls.Add(saveCancelPanel, 1, 0);
        bottomPanel.SetRowSpan(saveCancelPanel, 3);

        bottomPanel.Controls.Add(debouncePanel, 0, 1);
        bottomPanel.Controls.Add(_autoStartCheckbox, 0, 2);

        // Resize grid to leave room for bottom panel
        _grid.Size = new Size(ClientSize.Width - 24, ClientSize.Height - bottomPanel.Height - 46);

        Controls.Add(appsLabel);
        Controls.Add(_grid);
        Controls.Add(bottomPanel);

        // Keep grid sized correctly on resize
        Resize += (_, _) =>
        {
            _grid.Size = new Size(ClientSize.Width - 24, ClientSize.Height - bottomPanel.Height - 46);
        };
    }

    private void LoadData()
    {
        _grid.Rows.Clear();
        foreach (var app in _config.Apps)
        {
            _grid.Rows.Add(app.Name, app.ProcessName, app.StartCommand ?? "");
        }

        _debounceSpinner.Value = Math.Max(1, Math.Min(120, _config.DebounceSeconds));

        using var key = Registry.CurrentUser.OpenSubKey(AutoStartKeyPath, false);
        _autoStartCheckbox.Checked = key?.GetValue(AutoStartKeyName) != null;
    }

    private void AddRow_Click(object? sender, EventArgs e)
    {
        int idx = _grid.Rows.Add("New App", "processname", "");
        _grid.ClearSelection();
        _grid.Rows[idx].Selected = true;
        _grid.CurrentCell = _grid.Rows[idx].Cells["Name"];
        _grid.BeginEdit(true);
    }

    private void RemoveRow_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count > 0)
            _grid.Rows.Remove(_grid.SelectedRows[0]);
    }

    private void CaptureProcess_Click(object? sender, EventArgs e)
    {
        using var picker = new PickProcessForm();
        if (picker.ShowDialog(this) != DialogResult.OK || picker.CapturedApp is not { } app)
            return;

        int idx = _grid.Rows.Add(app.Name, app.ProcessName, app.StartCommand ?? "");
        _grid.ClearSelection();
        _grid.Rows[idx].Selected = true;
        _grid.CurrentCell = _grid.Rows[idx].Cells["Name"];
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        _grid.EndEdit();
        _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

        _config.Apps.Clear();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var name = row.Cells["Name"].Value?.ToString()?.Trim() ?? "";
            var proc = row.Cells["ProcessName"].Value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(proc)) continue;

            var startCmd = row.Cells["StartCommand"].Value?.ToString()?.Trim();

            _config.Apps.Add(new WatchedApp
            {
                Name = name,
                ProcessName = proc,
                StartCommand = string.IsNullOrEmpty(startCmd) ? null : startCmd
            });
        }

        _config.DebounceSeconds = (int)_debounceSpinner.Value;

        // Auto-start registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartKeyPath, writable: true);
            if (_autoStartCheckbox.Checked)
                key?.SetValue(AutoStartKeyName, $"\"{Application.ExecutablePath}\"");
            else
                key?.DeleteValue(AutoStartKeyName, throwOnMissingValue: false);
        }
        catch { /* registry access may fail in some environments */ }

        _onSave(_config);
        DialogResult = DialogResult.OK;
        Close();
    }
}
