using System.Diagnostics;
using TrayBaked.Forms;
using TrayBaked.Models;

namespace TrayBaked;

class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ExplorerMonitor _monitor;
    private AppConfig _config;
    private readonly SynchronizationContext _uiContext;
    private bool _confirmationPending;

    public TrayAppContext()
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _config = ConfigManager.Load();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "TrayBaked — Watching for Explorer restarts",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _monitor = new ExplorerMonitor(_config.DebounceSeconds);
        _monitor.ExplorerRestarted += OnExplorerRestarted;
        _monitor.Start();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Restart Explorer", null, (_, _) => _ = RestartExplorerAsync());
        menu.Items.Add("Settings...", null, OpenSettings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private async Task RestartExplorerAsync()
    {
        var procs = Process.GetProcessesByName("explorer");
        foreach (var p in procs)
        {
            try { p.Kill(); }
            catch { }
        }

        // Windows 10/11 restarts explorer.exe as the shell automatically after it exits.
        // Poll for up to 5 seconds; only launch manually if it hasn't come back on its own.
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            if (Process.GetProcessesByName("explorer").Length > 0)
                return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    private void OpenSettings(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_config, SaveConfig);
        form.ShowDialog();
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

        // Wait 5 seconds for the taskbar to stabilize, then show the confirmation on the UI thread
        Task.Delay(5000).ContinueWith(_ =>
        {
            _uiContext.Post(_ => ShowConfirmation(), null);
        });
    }

    private void ShowConfirmation()
    {
        try
        {
            var appStates = _config.Apps
                .Select(app => (App: app, Running: Process.GetProcessesByName(app.ProcessName).Length > 0))
                .ToList();

            using var form = new RestartForm(appStates);
            form.ShowDialog();
        }
        finally
        {
            _confirmationPending = false;
        }
    }

    private void ExitApplication()
    {
        _monitor.Stop();
        _monitor.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitor.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
