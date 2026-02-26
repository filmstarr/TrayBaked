using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TrayBaked.Models;

namespace TrayBaked;

public enum StatusKind { Waiting, InProgress, Success, Error }
public record RestartStatus(string AppName, string StatusText, StatusKind Kind);

static class AppLauncher
{
    // Represents how to launch an app: either a direct exe path or a Store AUMID.
    private sealed record LaunchTarget(string? ExePath, string? Aumid)
    {
        public bool IsValid => ExePath != null || Aumid != null;
    }

    public static async Task RestartAppsAsync(
        IEnumerable<WatchedApp> apps,
        IProgress<RestartStatus>? progress = null)
    {
        foreach (var app in apps)
        {
            var procs = Process.GetProcessesByName(app.ProcessName);
            var target = FindLaunchTarget(procs, app.StartCommand);

            if (!target.IsValid)
            {
                progress?.Report(new RestartStatus(app.Name,
                    "Error: could not locate executable", StatusKind.Error));
                continue;
            }

            if (procs.Length > 0)
            {
                progress?.Report(new RestartStatus(app.Name, "Stopping…", StatusKind.InProgress));

                foreach (var p in procs)
                {
                    try { p.CloseMainWindow(); }
                    catch { }
                }

                await Task.Delay(500);

                foreach (var p in procs)
                {
                    try { if (!p.HasExited) p.Kill(); }
                    catch { }
                }

                // Give all processes time to fully exit before relaunching
                await Task.Delay(1000);
            }

            progress?.Report(new RestartStatus(app.Name, "Starting…", StatusKind.InProgress));

            try
            {
                await LaunchAndMinimizeAsync(target);
                progress?.Report(new RestartStatus(app.Name, "Started", StatusKind.Success));
            }
            catch (Exception ex)
            {
                progress?.Report(new RestartStatus(app.Name,
                    $"Error: {ex.Message}", StatusKind.Error));
            }
        }
    }

    /// <summary>
    /// Determines how to launch the app by inspecting running processes.
    /// Tries AUMID first (handles Store/packaged apps like the Microsoft Store version of Spotify).
    /// Falls back to the exe path for regular desktop apps.
    /// If no processes are running, uses StartCommand — treating it as an AUMID if it contains '!'.
    /// </summary>
    private static LaunchTarget FindLaunchTarget(Process[] procs, string? startCommand)
    {
        var ordered = OrderByLikelihood(procs);

        // Packaged/Store apps have an AUMID; prefer that over the protected WindowsApps exe path
        foreach (var p in ordered)
        {
            var aumid = GetAumidNative(p.Id);
            if (!string.IsNullOrEmpty(aumid))
                return new LaunchTarget(null, aumid);
        }

        // Regular desktop app — use the exe path directly
        foreach (var p in ordered)
        {
            var path = GetExePathNative(p.Id);
            if (!string.IsNullOrEmpty(path))
                return new LaunchTarget(path, null);
        }

        // No running processes — fall back to StartCommand
        if (!string.IsNullOrWhiteSpace(startCommand))
        {
            return startCommand.Contains('!')
                ? new LaunchTarget(null, startCommand)   // stored AUMID
                : new LaunchTarget(startCommand, null);  // stored exe path
        }

        return new LaunchTarget(null, null);
    }

    private static async Task LaunchAndMinimizeAsync(LaunchTarget target)
    {
        if (target.Aumid != null)
        {
            var existingPids = GetRunningPidsForAumid(target.Aumid);

            // Store/packaged apps must be launched via the Windows shell AppsFolder protocol.
            // Direct Process.Start on a WindowsApps exe is denied even for the owning user.
            Process.Start(new ProcessStartInfo($"shell:AppsFolder\\{target.Aumid}")
            {
                UseShellExecute = true
            });

            var launched = await WaitForNewAumidProcessAsync(target.Aumid, existingPids, TimeSpan.FromSeconds(10));
            if (launched != null)
                await MinimizeMainWindowAsync(launched, TimeSpan.FromSeconds(10));
        }
        else if (target.ExePath != null)
        {
            var p = Process.Start(new ProcessStartInfo(target.ExePath)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            });

            if (p != null)
                await MinimizeMainWindowAsync(p, TimeSpan.FromSeconds(10));
        }
    }

    private static async Task MinimizeMainWindowAsync(Process p, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                p.Refresh();
                if (p.HasExited)
                    return;

                var hWnd = p.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindowAsync(hWnd, SW_MINIMIZE);
                    return;
                }
            }
            catch
            {
                // Best-effort: some processes may deny queries briefly during startup.
            }

            await Task.Delay(150);
        }
    }

    private static HashSet<int> GetRunningPidsForAumid(string aumid)
    {
        var set = new HashSet<int>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (string.Equals(GetAumidNative(p.Id), aumid, StringComparison.OrdinalIgnoreCase))
                    set.Add(p.Id);
            }
            catch { }
        }
        return set;
    }

    private static async Task<Process?> WaitForNewAumidProcessAsync(
        string aumid,
        HashSet<int> existingPids,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (existingPids.Contains(p.Id))
                        continue;

                    if (string.Equals(GetAumidNative(p.Id), aumid, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
                catch { }
            }

            await Task.Delay(250);
        }

        return null;
    }

    private static IOrderedEnumerable<Process> OrderByLikelihood(Process[] procs) =>
        procs.OrderByDescending(p =>
        {
            try { return p.MainWindowHandle != IntPtr.Zero ? 1 : 0; }
            catch { return 0; }
        }).ThenByDescending(p =>
        {
            try { return p.WorkingSet64; }
            catch { return 0L; }
        });

    internal static string? GetAumidNative(int pid)
    {
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            uint length = 512;
            var sb = new StringBuilder((int)length);
            // Returns 0 (ERROR_SUCCESS) for packaged apps; non-zero (e.g. 15703 = not packaged) otherwise
            return GetApplicationUserModelId(handle, ref length, sb) == 0 ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    internal static string? GetExePathNative(int pid)
    {
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(
        IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(
        IntPtr hProcess, ref uint applicationUserModelIdLength, StringBuilder applicationUserModelId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int SW_MINIMIZE = 6;

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
