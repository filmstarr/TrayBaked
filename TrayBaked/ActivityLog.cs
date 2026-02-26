using System.IO;
using System.Text.Json;

namespace TrayBaked;

public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public string   Message   { get; set; } = "";

    public string TimestampDisplay => Timestamp.ToString("dd MMM HH:mm:ss");
}

static class ActivityLog
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrayBaked", "activity.json");

    private static List<ActivityLogEntry> _entries = Load();
    private static readonly object _lock = new();

    /// <summary>Fires on any change (Add or Clear) from whatever thread made the call.</summary>
    public static event EventHandler? Changed;

    public static void Add(string message)
    {
        lock (_lock)
        {
            _entries.Insert(0, new ActivityLogEntry { Timestamp = DateTime.Now, Message = message });
            if (_entries.Count > 500)
                _entries.RemoveRange(500, _entries.Count - 500);
            Save();
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Returns a snapshot copy; safe to read from any thread.</summary>
    public static List<ActivityLogEntry> GetEntries()
    {
        lock (_lock) return [.. _entries];
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            Save();
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static List<ActivityLogEntry> Load()
    {
        try
        {
            if (File.Exists(_filePath))
                return JsonSerializer.Deserialize<List<ActivityLogEntry>>(
                    File.ReadAllText(_filePath)) ?? [];
        }
        catch { }
        return [];
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_entries));
        }
        catch { }
    }
}
