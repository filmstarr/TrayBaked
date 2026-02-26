using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TrayBaked.Models;
using CheckBox = System.Windows.Controls.CheckBox;

namespace TrayBaked.Windows;

public partial class RestartWindow : Window
{
    private readonly List<(WatchedApp App, bool Running)> _appStates;
    private readonly ObservableCollection<RestartStatusItem> _statusItems = new();
    private List<AppCheckItem> _checkItems = new();
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
        _checkItems = _appStates
            .OrderBy(s => s.App.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new AppCheckItem
            {
                App          = s.App,
                DisplayLabel = s.Running ? s.App.Name : $"{s.App.Name}  (not running)",
                IsChecked    = s.Running
            })
            .ToList();

        AppCheckList.ItemsSource = _checkItems;
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        var selected = _checkItems
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

        Phase1Panel.Visibility      = Visibility.Collapsed;
        Phase1Buttons.Visibility    = Visibility.Collapsed;
        Phase2ListBorder.Visibility = Visibility.Visible;
        Phase2Buttons.Visibility    = Visibility.Visible;

        foreach (var app in apps)
        {
            var item = new RestartStatusItem { AppName = app.Name };
            _statusItems.Add(item);
        }

        Phase2Grid.ItemsSource = _statusItems;

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
        {
            _finalCount++;
            ActivityLog.Add(status.Kind == StatusKind.Success
                ? $"{status.AppName} restarted"
                : $"{status.AppName}: {status.StatusText}");
        }

        if (_finalCount >= _statusItems.Count)
        {
            Title = "TrayBaked — Done";
            HeaderSubtitle.Text = "All done";
            CloseBtn.IsEnabled  = true;
        }
    }

    private void AppCheckList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Let the CheckBox handle its own clicks normally
        if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) != null) return;

        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.DataContext is AppCheckItem item)
            item.IsChecked = !item.IsChecked;
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

    private void Cancel_Click(object sender, RoutedEventArgs e)   { DialogResult = false; Close(); }
    private void CloseBtn_Click(object sender, RoutedEventArgs e)  { Close(); }
}

// ─── View-models ─────────────────────────────────────────────────────────────

public class AppCheckItem : INotifyPropertyChanged
{
    public required WatchedApp App          { get; init; }
    public required string     DisplayLabel { get; init; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
