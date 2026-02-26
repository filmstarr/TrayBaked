using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Resolve ambiguities that arise from having both UseWpf and UseWindowsForms enabled
using Color  = System.Windows.Media.Color;
using Pen    = System.Windows.Media.Pen;
using Point  = System.Windows.Point;
using Rect   = System.Windows.Rect;

namespace TrayBaked;

/// <summary>
/// Renders the TrayBaked application icon: a pair of binoculars sitting inside an oven.
/// The icon is drawn procedurally from WPF geometry so it scales to any size cleanly.
/// </summary>
static class AppIconHelper
{
    private static BitmapSource? _cached64;
    private static string?       _cachedPngPath;

    /// <summary>Returns a 64-px frozen BitmapSource suitable for Window.Icon.</summary>
    public static BitmapSource GetIconBitmapSource()
        => _cached64 ??= Render(64);

    /// <summary>
    /// Saves a 64-px PNG to %LOCALAPPDATA%\TrayBaked\icon.png and returns its path.
    /// Used as the image source for toast notifications.
    /// </summary>
    public static string GetOrSaveIconPng()
    {
        if (_cachedPngPath != null) return _cachedPngPath;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrayBaked");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "icon.png");

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(Render(128)));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        enc.Save(fs);

        _cachedPngPath = path;
        return path;
    }

    /// <summary>Creates a GDI icon for the system tray NotifyIcon.</summary>
    public static System.Drawing.Icon GetTrayIcon()
    {
        var bmp = Render(32);
        using var ms = new MemoryStream();
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        enc.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);
        using var gdi = new System.Drawing.Bitmap(ms);
        return System.Drawing.Icon.FromHandle(gdi.GetHicon());
    }

    private static BitmapSource Render(int px)
    {
        double s = px;
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            // ── Oven body ──────────────────────────────────────────────────────────
            // Warm brushed-metal gradient for the outer case
            var bodyBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromRgb(0xDA, 0xD8, 0xD2), 0.0),
                    new(Color.FromRgb(0xAA, 0xA8, 0xA0), 1.0),
                },
                new Point(0, 0), new Point(0, 1));

            var bodyPen = new Pen(new SolidColorBrush(Color.FromRgb(0x88, 0x86, 0x7E)), s * 0.03);
            bodyPen.Freeze();

            dc.DrawRoundedRectangle(
                bodyBrush, bodyPen,
                new Rect(s * 0.05, s * 0.05, s * 0.90, s * 0.90),
                s * 0.14, s * 0.14);

            // ── Control panel (top strip) ──────────────────────────────────────────
            var panelBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromRgb(0x28, 0x28, 0x28), 0.0),
                    new(Color.FromRgb(0x3A, 0x3A, 0x3A), 1.0),
                },
                new Point(0, 0), new Point(0, 1));

            // Rounded top, square bottom — draw as rounded rect then cover bottom corners
            dc.DrawRoundedRectangle(
                panelBrush, null,
                new Rect(s * 0.05, s * 0.05, s * 0.90, s * 0.30),
                s * 0.14, s * 0.14);
            dc.DrawRectangle(
                panelBrush, null,
                new Rect(s * 0.05, s * 0.22, s * 0.90, s * 0.13));

            // ── Oven dials ─────────────────────────────────────────────────────────
            double dialR = s * 0.075;
            var dialBrush = new RadialGradientBrush(
                Color.FromRgb(0xFF, 0xAA, 0x20),
                Color.FromRgb(0xC0, 0x60, 0x00))
            { GradientOrigin = new Point(0.35, 0.30) };

            dc.DrawEllipse(dialBrush, null, new Point(s * 0.26, s * 0.195), dialR, dialR);
            dc.DrawEllipse(dialBrush, null, new Point(s * 0.74, s * 0.195), dialR, dialR);

            // Tiny indicator marks on each dial
            var markPen = new Pen(new SolidColorBrush(Color.FromRgb(0x30, 0x18, 0x00)), s * 0.025);
            markPen.StartLineCap = PenLineCap.Round;
            markPen.EndLineCap = PenLineCap.Round;
            markPen.Freeze();

            dc.DrawLine(markPen,
                new Point(s * 0.26, s * 0.195 - dialR * 0.30),
                new Point(s * 0.26, s * 0.195 - dialR * 0.75));
            dc.DrawLine(markPen,
                new Point(s * 0.74, s * 0.195 - dialR * 0.30),
                new Point(s * 0.74, s * 0.195 - dialR * 0.75));

            // ── Door glass ────────────────────────────────────────────────────────
            var doorBg = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromRgb(0x14, 0x14, 0x26), 0.0),
                    new(Color.FromRgb(0x0C, 0x0C, 0x18), 1.0),
                },
                new Point(0, 0), new Point(0, 1));

            var doorPen = new Pen(new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x78)), s * 0.025);
            doorPen.Freeze();

            var doorRect = new Rect(s * 0.12, s * 0.35, s * 0.76, s * 0.55);
            dc.DrawRoundedRectangle(doorBg, doorPen, doorRect, s * 0.07, s * 0.07);

            // Subtle glass reflection (top-left highlight)
            var reflectBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF), 0.0),
                    new(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0),
                },
                new Point(0, 0), new Point(0, 1));
            dc.DrawRoundedRectangle(
                reflectBrush, null,
                new Rect(s * 0.15, s * 0.37, s * 0.70, s * 0.18),
                s * 0.05, s * 0.05);

            // ── Binoculars ────────────────────────────────────────────────────────
            double cy     = s * 0.645;   // vertical centre of binoculars
            double bR     = s * 0.140;   // outer barrel radius
            double cx1    = s * 0.330;   // left barrel centre
            double cx2    = s * 0.670;   // right barrel centre

            // Barrel bodies — warm amber radial gradient
            var barrelBrush = new RadialGradientBrush(
                Color.FromRgb(0xE8, 0x98, 0x18),
                Color.FromRgb(0x88, 0x48, 0x04))
            { GradientOrigin = new Point(0.35, 0.30) };

            dc.DrawEllipse(barrelBrush, null, new Point(cx1, cy), bR, bR * 0.88);
            dc.DrawEllipse(barrelBrush, null, new Point(cx2, cy), bR, bR * 0.88);

            // Bridge connecting the two barrels
            var bridgeBrush = new SolidColorBrush(Color.FromRgb(0xA0, 0x58, 0x08));
            double bLeft  = cx1 + bR * 0.70;
            double bRight = cx2 - bR * 0.70;
            dc.DrawRoundedRectangle(
                bridgeBrush, null,
                new Rect(bLeft, cy - bR * 0.28, bRight - bLeft, bR * 0.56),
                s * 0.025, s * 0.025);

            // Inner lenses — blue glass with radial gradient
            var lensBrush = new RadialGradientBrush(
                Color.FromRgb(0x90, 0xD4, 0xFF),
                Color.FromRgb(0x18, 0x58, 0xA8))
            { GradientOrigin = new Point(0.38, 0.34) };

            dc.DrawEllipse(lensBrush, null, new Point(cx1, cy), bR * 0.63, bR * 0.56);
            dc.DrawEllipse(lensBrush, null, new Point(cx2, cy), bR * 0.63, bR * 0.56);

            // Lens specular highlight (white gleam, top-left of each lens)
            var gleamBrush = new SolidColorBrush(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF));
            dc.DrawEllipse(gleamBrush, null,
                new Point(cx1 - bR * 0.20, cy - bR * 0.30), bR * 0.18, bR * 0.13);
            dc.DrawEllipse(gleamBrush, null,
                new Point(cx2 - bR * 0.20, cy - bR * 0.30), bR * 0.18, bR * 0.13);
        }

        var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
