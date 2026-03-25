namespace TrayBaked.Models;

public class WatchedApp
{
    public string Name { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string? StartCommand { get; set; }
    /// <summary>
    /// When true the app is never pre-selected for restart in the UI or
    /// automatically restarted; the user can still check it manually.
    /// </summary>
    public bool ExcludeFromAutoRestart { get; set; } = false;
}

public class AppConfig
{
    public int DebounceSeconds { get; set; } = 10;
    public bool AutoRestart { get; set; } = false;
    public List<WatchedApp> Apps { get; set; } = [];
}
