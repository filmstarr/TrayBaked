using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using TrayBaked.Models;
using WinDeploy = Windows.Management.Deployment;

namespace TrayBaked;

/// <summary>
/// Extracts the real application icon for a WatchedApp.
/// Desktop apps: SHGetFileInfo on the exe path.
/// Store/packaged apps: PackageManager + AppxManifest logo file.
/// Results are cached by app name so repeated opens are instant.
/// </summary>
static class AppIconExtractor
{
    private static readonly Dictionary<string, BitmapSource?> _cache = new();

    public static BitmapSource? GetIcon(WatchedApp app)
    {
        if (_cache.TryGetValue(app.Name, out var cached))
            return cached;

        var icon = Extract(app);
        icon?.Freeze();
        _cache[app.Name] = icon;
        return icon;
    }

    /// <summary>Clears the icon cache (call after config changes).</summary>
    public static void ClearCache() => _cache.Clear();

    // ── Extraction logic ─────────────────────────────────────────────────────

    private static BitmapSource? Extract(WatchedApp app)
    {
        var procs = Process.GetProcessesByName(app.ProcessName);

        // Running processes: try AUMID first (Store apps), then exe path
        foreach (var p in Ordered(procs))
        {
            var aumid = AppLauncher.GetAumidNative(p.Id);
            if (!string.IsNullOrEmpty(aumid))
            {
                var icon = IconFromAumid(aumid);
                if (icon != null) return icon;
            }
        }

        foreach (var p in Ordered(procs))
        {
            var path = AppLauncher.GetExePathNative(p.Id);
            if (!string.IsNullOrEmpty(path))
            {
                var icon = IconFromExePath(path);
                if (icon != null) return icon;
            }
        }

        // Not running — fall back to StartCommand
        if (!string.IsNullOrWhiteSpace(app.StartCommand))
        {
            return app.StartCommand.Contains('!')
                ? IconFromAumid(app.StartCommand)
                : IconFromExePath(app.StartCommand);
        }

        return null;
    }

    private static BitmapSource? IconFromExePath(string path)
    {
        try
        {
            var sfi = new SHFILEINFO();
            var r = SHGetFileInfoStr(path, 0, ref sfi, (uint)Marshal.SizeOf(sfi),
                                    SHGFI_ICON | SHGFI_LARGEICON);
            if (r != IntPtr.Zero && sfi.hIcon != IntPtr.Zero)
            {
                try   { return HIconToBitmapSource(sfi.hIcon); }
                finally { DestroyIcon(sfi.hIcon); }
            }
        }
        catch { }
        return null;
    }

    private static BitmapSource? IconFromAumid(string aumid)
    {
        // First try: package manifest PNG (matches the colourful icons the system tray shows)
        var icon = IconFromPackageManifest(aumid);
        if (icon != null) return icon;

        // Second try: shell PIDL fallback
        return IconFromShellPidl(aumid);
    }

    private static BitmapSource? IconFromShellPidl(string aumid)
    {
        IntPtr pidl = IntPtr.Zero;
        try
        {
            int hr = SHParseDisplayName(aumid, IntPtr.Zero, out pidl, 0, out _);
            if (hr != 0 || pidl == IntPtr.Zero) return null;

            var sfi = new SHFILEINFO();
            var r = SHGetFileInfoPidl(pidl, 0, ref sfi, (uint)Marshal.SizeOf(sfi),
                                      SHGFI_ICON | SHGFI_LARGEICON | SHGFI_PIDL);
            if (r != IntPtr.Zero && sfi.hIcon != IntPtr.Zero)
            {
                try   { return HIconToBitmapSource(sfi.hIcon); }
                finally { DestroyIcon(sfi.hIcon); }
            }
        }
        catch { }
        finally
        {
            if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl);
        }
        return null;
    }

    private static BitmapSource? IconFromPackageManifest(string aumid)
    {
        try
        {
            var familyName = aumid.Split('!')[0];
            var pm = new WinDeploy.PackageManager();
            var package = pm.FindPackagesForUser("", familyName).FirstOrDefault();
            if (package == null) return null;

            var installDir = package.InstalledLocation.Path;
            var manifestPath = Path.Combine(installDir, "AppxManifest.xml");
            if (!File.Exists(manifestPath)) return null;

            var logoRelPath = ParseLogoFromManifest(manifestPath);
            if (logoRelPath == null) return null;

            var logoPath = ResolveLogoFile(installDir, logoRelPath);
            if (logoPath == null) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(logoPath, UriKind.Absolute);
            bmp.DecodePixelWidth = 40;
            bmp.CacheOption      = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }
        catch { return null; }
    }

    private static string? ParseLogoFromManifest(string manifestPath)
    {
        try
        {
            var doc = XDocument.Load(manifestPath);
            XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
            XNamespace ns  = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

            foreach (var ve in doc.Descendants(uap + "VisualElements"))
            {
                var logo = ve.Attribute("Square44x44Logo")?.Value
                        ?? ve.Attribute("Logo")?.Value;
                if (!string.IsNullOrEmpty(logo)) return logo;
            }

            return doc.Descendants(ns + "Logo").FirstOrDefault()?.Value;
        }
        catch { return null; }
    }

    private static string? ResolveLogoFile(string installDir, string relativePath)
    {
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(installDir, relativePath);
        if (File.Exists(fullPath)) return fullPath;

        var dir      = Path.GetDirectoryName(fullPath);
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var ext      = Path.GetExtension(fullPath);

        if (dir == null || !Directory.Exists(dir)) return null;

        return Directory.GetFiles(dir, $"{baseName}*{ext}")
            .OrderByDescending(f =>
            {
                var n = Path.GetFileNameWithoutExtension(f);
                if (n.Contains("targetsize-44")) return 5;
                if (n.Contains("targetsize-32")) return 4;
                if (n.Contains("scale-200"))     return 3;
                if (n.Contains("scale-150"))     return 2;
                if (n.Contains("scale-100"))     return 1;
                return 0;
            })
            .FirstOrDefault();
    }

    private static BitmapSource HIconToBitmapSource(IntPtr hIcon)
    {
        var src = Imaging.CreateBitmapSourceFromHIcon(
            hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }

    private static IEnumerable<Process> Ordered(Process[] procs) =>
        procs.OrderByDescending(p =>
        {
            try { return p.MainWindowHandle != IntPtr.Zero ? 1 : 0; }
            catch { return 0; }
        });

    // ── P/Invoke ─────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int    iIcon;
        public uint   dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]  public string szTypeName;
    }

    private const uint SHGFI_ICON      = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_PIDL      = 0x000000008;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
    private static extern IntPtr SHGetFileInfoStr(
        string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
    private static extern IntPtr SHGetFileInfoPidl(
        IntPtr pidl, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
