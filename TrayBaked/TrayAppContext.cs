using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TrayBaked.Models;
using TrayBaked.Windows;

// Resolve ambiguity: both UseWpf and UseWindowsForms expose an 'Application' type
using Application = System.Windows.Application;

namespace TrayBaked;

/// <summary>
/// Manages the system-tray icon and application lifecycle.
/// Uses a styled WPF ContextMenu (not WinForms ContextMenuStrip) so the menu
/// inherits the application theme and Windows 11 visual style.
/// </summary>
class TrayAppContext : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private readonly ExplorerMonitor _monitor;
    private AppConfig _config;
    private bool _confirmationPending;
    private bool _disposed;

    // Lightweight hidden window that WPF requires as a placement target so that
    // the ContextMenu closes properly when the user clicks elsewhere.
    private Window? _menuOwner;

    // Only one Settings window may be open at a time.
    private SettingsWindow? _settingsWindow;

    public TrayAppContext()
    {
        _config = ConfigManager.Load();

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon    = AppIconHelper.GetTrayIcon(),
            Text    = "TrayBaked — Watching for Explorer restarts",
            Visible = true,
        };

        // Left-click → Settings; right-click → context menu
        _trayIcon.MouseUp += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                Application.Current.Dispatcher.BeginInvoke(OpenSettings);
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                Application.Current.Dispatcher.BeginInvoke(OpenTrayMenu);
        };

        _monitor = new ExplorerMonitor(_config.DebounceSeconds);
        _monitor.ExplorerRestarted += OnExplorerRestarted;
        _monitor.Start();
    }

    // ── Tray context menu ────────────────────────────────────────────────────

    private void OpenTrayMenu()
    {
        // Create a 1×1 invisible helper window the first time.
        // Show() + Activate() makes it the foreground window, so WPF knows to
        // dismiss the ContextMenu when focus moves away from it.
        if (_menuOwner == null)
        {
            _menuOwner = new Window
            {
                Width              = 1,
                Height             = 1,
                Opacity            = 0,
                WindowStyle        = WindowStyle.None,
                ShowInTaskbar      = false,
                AllowsTransparency = true,
                Background         = System.Windows.Media.Brushes.Transparent,
                ResizeMode         = ResizeMode.NoResize,
                Topmost            = true,
            };
        }

        var menu             = BuildTrayMenu();
        menu.Placement       = PlacementMode.MousePoint;
        menu.PlacementTarget = _menuOwner;
        menu.Closed         += (_, _) => _menuOwner.Hide();

        _menuOwner.Show();
        _menuOwner.Activate();
        menu.IsOpen = true;
    }

    /// <summary>
    /// Builds a fresh ContextMenu each time so any theme change since the last
    /// open is reflected immediately.
    /// </summary>
    private ContextMenu BuildTrayMenu()
    {
        var app       = Application.Current;
        var menuStyle = (Style?)app.TryFindResource("TrayMenuStyle");
        var itemStyle = (Style?)app.TryFindResource("TrayMenuItemStyle");
        var sepStyle  = (Style?)app.TryFindResource("TrayMenuSepStyle");

        MenuItem Item(string header, RoutedEventHandler onClick)
        {
            var mi = new MenuItem { Header = header, Style = itemStyle };
            mi.Click += onClick;
            return mi;
        }

        var menu = new ContextMenu { Style = menuStyle };
        menu.Items.Add(Item("Settings…",        (_, _) => OpenSettings()));
        menu.Items.Add(Item("Restart Explorer", (_, _) => _ = RestartExplorerAsync()));
        menu.Items.Add(new Separator            { Style = sepStyle });
        menu.Items.Add(Item("Exit",             (_, _) => ExitApplication()));
        return menu;
    }

    // ── Application actions ──────────────────────────────────────────────────

    private async Task RestartExplorerAsync()
    {
        foreach (var p in Process.GetProcessesByName("explorer"))
        {
            try { p.Kill(); } catch { }
        }

        // Windows restarts explorer.exe automatically; poll for 5 s before forcing it
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            if (Process.GetProcessesByName("explorer").Length > 0)
                return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            // Bring the existing window to the front rather than opening a second one.
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_config, SaveConfig);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.ShowDialog();
    }

    private void SaveConfig(AppConfig config)
    {
        _config = config;
        ConfigManager.Save(config);
        _monitor.UpdateDebounce(config.DebounceSeconds);
    }

    private void OnExplorerRestarted(object? sender, EventArgs e)
    {
        if (_confirmationPending) return;
        _confirmationPending = true;

        // Wait for the taskbar / shell to stabilise, then marshal onto the WPF dispatcher
        Task.Delay(5000).ContinueWith(_ =>
        {
            Application.Current.Dispatcher.BeginInvoke(ShowConfirmation);
        });
    }

    private void ShowConfirmation()
    {
        try
        {
            var appStates = _config.Apps
                .Select(app => (App: app,
                                Running: Process.GetProcessesByName(app.ProcessName).Length > 0))
                .ToList();

            var win = new RestartWindow(appStates);
            win.ShowDialog();
        }
        finally
        {
            _confirmationPending = false;
        }
    }

    private void ExitApplication()
    {
        _monitor.Stop();
        _trayIcon.Visible = false;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor.Dispose();
        _trayIcon.Dispose();
        _menuOwner?.Close();
    }
}
