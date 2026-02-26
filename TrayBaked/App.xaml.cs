using Microsoft.Win32;

namespace TrayBaked;

public partial class App : System.Windows.Application
{
    private TrayAppContext? _trayContext;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply the correct light/dark palette before any window opens
        ThemeManager.Apply();

        // Watch for the user toggling the Windows theme at runtime
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _trayContext = new TrayAppContext();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // UserPreferenceCategory.General covers the light/dark switch
        if (e.Category == UserPreferenceCategory.General)
            Dispatcher.BeginInvoke(() =>
            {
                ThemeManager.Apply();
                WindowHelper.RefreshOpenWindows();
            });
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _trayContext?.Dispose();
        base.OnExit(e);
    }
}
