using System.IO;
using Microsoft.Win32;

namespace TrayBaked;

/// <summary>
/// Reads the Windows AppsUseLightTheme registry value and swaps the active
/// colour resource dictionary between LightTheme.xaml and DarkTheme.xaml.
/// Call Apply() once at startup and again whenever the preference changes.
/// </summary>
static class ThemeManager
{
    private const string RegPath  = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegValue = "AppsUseLightTheme";

    /// <summary>True when Windows is in light mode (or the key is absent).</summary>
    public static bool IsLightMode
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath);
            return key?.GetValue(RegValue) is not int i || i != 0;
        }
    }

    /// <summary>
    /// Swaps the theme resource dictionary in Application.Current.Resources to
    /// match the current Windows theme.  Must be called on the UI thread.
    /// </summary>
    public static void Apply()
    {
        var uri = IsLightMode
            ? new Uri("Resources/LightTheme.xaml", UriKind.Relative)
            : new Uri("Resources/DarkTheme.xaml",  UriKind.Relative);

        var merged  = System.Windows.Application.Current.Resources.MergedDictionaries;

        // Remove whichever theme dictionary is currently loaded, if any
        var existing = merged.FirstOrDefault(d =>
        {
            var src = d.Source?.OriginalString ?? "";
            return src.Contains("LightTheme") || src.Contains("DarkTheme");
        });
        if (existing != null)
            merged.Remove(existing);

        merged.Add(new System.Windows.ResourceDictionary { Source = uri });
    }
}
