using System.Windows;

namespace TrayBaked.Windows;

public partial class ActivityLogWindow : Window
{
    public ActivityLogWindow()
    {
        InitializeComponent();
        WindowHelper.ApplyTitleBarTheme(this);

        var icon = AppIconHelper.GetIconBitmapSource();
        Icon = icon;
        HeaderIcon.Source = icon;

        Refresh();

        ActivityLog.Changed += OnLogChanged;
        Closed += (_, _) => ActivityLog.Changed -= OnLogChanged;
    }

    private void Refresh() => LogGrid.ItemsSource = ActivityLog.GetEntries();

    private void OnLogChanged(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(Refresh);

    private void Clear_Click(object sender, RoutedEventArgs e) => ActivityLog.Clear();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
