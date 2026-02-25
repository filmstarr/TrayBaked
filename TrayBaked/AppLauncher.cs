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
                Launch(target);
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

    private static void Launch(LaunchTarget target)
    {
        if (target.Aumid != null)
        {
            // Store/packaged apps must be launched via the Windows shell AppsFolder protocol.
            // Direct Process.Start on a WindowsApps exe is denied even for the owning user.
            Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{target.Aumid}")
            {
                UseShellExecute = true
            });
        }
        else if (target.ExePath != null)
        {
            Process.Start(new ProcessStartInfo(target.ExePath)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            });
        }
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
}
