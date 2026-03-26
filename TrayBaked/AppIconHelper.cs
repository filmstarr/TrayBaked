using System.IO;
using System.Windows.Media.Imaging;

namespace TrayBaked;

static class AppIconHelper
{
    private static Stream OpenIcoStream()
    {
        var uri = new Uri("pack://application:,,,/TrayBaked;component/Assets/TrayBaked.ico");
        return System.Windows.Application.GetResourceStream(uri).Stream;
    }

    /// <summary>
    /// Saves a PNG to %LOCALAPPDATA%\TrayBaked\icon.png and returns its path.
    /// Used as the image source for toast notifications.
    /// </summary>
    public static string GetOrSaveIconPng()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrayBaked");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "icon.png");

        using var stream = OpenIcoStream();
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);

        return path;
    }

    /// <summary>Creates a GDI icon for the system tray NotifyIcon.</summary>
    public static Icon GetTrayIcon()
    {
        using var stream = OpenIcoStream();
        return new Icon(stream, 32, 32);
    }
}
