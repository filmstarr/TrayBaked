namespace TrayBaked.Models;

public class WatchedApp
{
    public string Name { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string? StartCommand { get; set; }
}

public class AppConfig
{
    public int DebounceSeconds { get; set; } = 10;
    public bool AutoRestart { get; set; } = false;
    public List<WatchedApp> Apps { get; set; } = [];
}
