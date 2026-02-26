using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

// Resolve ambiguity: both UseWpf and UseWindowsForms expose an 'Application' type
using Application = System.Windows.Application;

namespace TrayBaked;

/// <summary>
/// Helpers for applying Windows-native window attributes via DWM.
/// </summary>
static class WindowHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int value, int size);

    // Available on Windows 10 20H1 (build 19041) and later.
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// Registers <paramref name="window"/> to receive a dark or light title bar
    /// that matches the current Windows theme.  Call this in the window
    /// constructor after InitializeComponent().
    /// </summary>
    public static void ApplyTitleBarTheme(Window window)
    {
        // SourceInitialized fires once the HWND exists but before the window is shown.
        window.SourceInitialized += (_, _) => SetDark(window, !ThemeManager.IsLightMode);
    }

    /// <summary>
    /// Updates the title-bar colour for every currently open window.
    /// Call this from App after ThemeManager.Apply() when the system theme changes.
    /// </summary>
    public static void RefreshOpenWindows()
    {
        bool dark = !ThemeManager.IsLightMode;
        foreach (Window w in Application.Current.Windows)
            SetDark(w, dark);
    }

    private static void SetDark(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }
}
