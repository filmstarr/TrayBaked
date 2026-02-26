using System.Diagnostics;

namespace TrayBaked;

class ExplorerMonitor : IDisposable
{
    private CancellationTokenSource? _cts;
    private DateTime _lastHandledTime;
    private DateTime _suppressUntil = DateTime.MinValue;
    private int _debounceSeconds;
    private bool _disposed;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public event EventHandler? ExplorerRestarted;

    public ExplorerMonitor(int debounceSeconds)
    {
        _debounceSeconds = debounceSeconds;

        // Seed with current explorer start time to avoid firing on first run
        _lastHandledTime = GetExplorerStartTime() ?? new DateTime(2000, 1, 1);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void UpdateDebounce(int debounceSeconds)
    {
        _debounceSeconds = debounceSeconds;
    }

    /// <summary>
    /// Suppresses ExplorerRestarted for the given duration.
    /// Call before restarting apps to prevent Store-app launches
    /// (which use explorer.exe as a shell relay) from re-triggering the event.
    /// </summary>
    public void Suppress(TimeSpan duration) => _suppressUntil = DateTime.Now + duration;

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var explorerStart = GetExplorerStartTime();
            if (explorerStart == null) continue;

            if (DateTime.Now < _suppressUntil)
            {
                // Silently acknowledge any new start times so they don't fire after suppress lifts.
                if (explorerStart.Value > _lastHandledTime)
                    _lastHandledTime = explorerStart.Value;
                continue;
            }

            if ((explorerStart.Value - _lastHandledTime).TotalSeconds >= _debounceSeconds)
            {
                _lastHandledTime = explorerStart.Value;
                ExplorerRestarted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private static DateTime? GetExplorerStartTime()
    {
        return Process.GetProcessesByName("explorer")
            .Select(p => TryGetStartTime(p))
            .Where(t => t != null)
            .OrderBy(t => t)
            .FirstOrDefault();
    }

    private static DateTime? TryGetStartTime(Process p)
    {
        try { return p.StartTime; }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
