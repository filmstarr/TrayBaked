using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TrayBaked.Models;

// Resolve ambiguity: both UseWpf and UseWindowsForms expose a 'Button' type
using Button = System.Windows.Controls.Button;

namespace TrayBaked.Windows;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly Action<AppConfig> _onSave;
    private readonly ObservableCollection<WatchedAppRow> _rows = new();

    private const string AutoStartKeyName = "TrayBaked";
    private const string AutoStartKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public SettingsWindow(AppConfig config, Action<AppConfig> onSave)
    {
        _config = config;
        _onSave = onSave;
        InitializeComponent();
        WindowHelper.ApplyTitleBarTheme(this);

        var icon = AppIconHelper.GetIconBitmapSource();
        Icon = icon;
        HeaderIcon.Source = icon;

        AppsGrid.ItemsSource = _rows;
        LoadData();

        Loaded += SettingsWindow_Loaded;
    }

    private void SettingsWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0) return;

        var newRow = _rows[^1]; // trailing blank "add" row
        AppsGrid.SelectedItem = newRow;
        AppsGrid.ScrollIntoView(newRow);

        if (AppsGrid.Columns.Count == 0) return;

        AppsGrid.CurrentCell = new DataGridCellInfo(newRow, AppsGrid.Columns[0]);
        AppsGrid.Dispatcher.BeginInvoke(new Action(() =>
        {
            AppsGrid.BeginEdit();
        }), DispatcherPriority.Background);
    }

    private void LoadData()
    {
        _rows.Clear();
        foreach (var app in _config.Apps)
        {
            var row = new WatchedAppRow
            {
                Name         = app.Name,
                ProcessName  = app.ProcessName,
                StartCommand = app.StartCommand ?? ""
            };
            AttachRowListener(row);
            _rows.Add(row);
        }
        EnsureTrailingBlank();

        using var key = Registry.CurrentUser.OpenSubKey(AutoStartKeyPath, false);
        AutoStartCheckbox.IsChecked = key?.GetValue(AutoStartKeyName) != null;

        AutoRestartCheckbox.IsChecked = _config.AutoRestart;
    }

    private void AttachRowListener(WatchedAppRow row)
    {
        row.PropertyChanged += (_, _) =>
        {
            if (!IsBlank(row)) EnsureTrailingBlank();
        };
    }

    private void EnsureTrailingBlank()
    {
        if (_rows.Count == 0 || !IsBlank(_rows[^1]))
        {
            var blank = NewBlankRow();
            AttachRowListener(blank);
            _rows.Add(blank);
        }
    }

    private static WatchedAppRow NewBlankRow() => new();

    private static bool IsBlank(WatchedAppRow r) =>
        string.IsNullOrWhiteSpace(r.Name) &&
        string.IsNullOrWhiteSpace(r.ProcessName) &&
        string.IsNullOrWhiteSpace(r.StartCommand);

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is WatchedAppRow row)
        {
            _rows.Remove(row);
            EnsureTrailingBlank();
        }
    }

    private void CaptureProcess_Click(object sender, RoutedEventArgs e)
    {
        var picker = new PickProcessWindow { Owner = this };
        if (picker.ShowDialog() == true && picker.CapturedApp is { } app)
        {
            var row = new WatchedAppRow
            {
                Name         = app.Name,
                ProcessName  = app.ProcessName,
                StartCommand = app.StartCommand ?? ""
            };
            AttachRowListener(row);

            // Insert before the trailing blank row
            int insertAt = _rows.Count > 0 && IsBlank(_rows[^1])
                ? _rows.Count - 1
                : _rows.Count;
            _rows.Insert(insertAt, row);

            AppsGrid.SelectedItem = row;
            AppsGrid.ScrollIntoView(row);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        AppsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        _config.AutoRestart = AutoRestartCheckbox.IsChecked ?? false;

        _config.Apps.Clear();
        foreach (var row in _rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.ProcessName))
                continue;
            _config.Apps.Add(new WatchedApp
            {
                Name         = row.Name.Trim(),
                ProcessName  = row.ProcessName.Trim(),
                StartCommand = string.IsNullOrWhiteSpace(row.StartCommand)
                                   ? null
                                   : row.StartCommand.Trim()
            });
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartKeyPath, writable: true);
            if (AutoStartCheckbox.IsChecked == true)
                key?.SetValue(AutoStartKeyName, $"\"{Environment.ProcessPath}\"");
            else
                key?.DeleteValue(AutoStartKeyName, throwOnMissingValue: false);
        }
        catch { /* registry access may fail in some environments */ }

        _onSave(_config);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // Enter edit mode on the first click instead of requiring a second click
    private void AppsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't intercept clicks on buttons (e.g. the inline delete button)
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null) return;

        var cell = FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell == null || cell.IsEditing || cell.IsReadOnly) return;

        cell.Focus();
        AppsGrid.BeginEdit();
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }
}

// ─── Row model ──────────────────────────────────────────────────────────────

public class WatchedAppRow : INotifyPropertyChanged
{
    private string _name = "";
    private string _processName = "";
    private string _startCommand = "";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string ProcessName
    {
        get => _processName;
        set { _processName = value; OnPropertyChanged(); }
    }

    public string StartCommand
    {
        get => _startCommand;
        set { _startCommand = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
