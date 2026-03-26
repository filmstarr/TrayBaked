using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TrayBaked.Models;
using Application = System.Windows.Application;
using Button      = System.Windows.Controls.Button;
using Image       = System.Windows.Controls.Image;
using ToolTip     = System.Windows.Controls.ToolTip;

namespace TrayBaked.Windows;

/// <summary>
/// Small icon-grid popup that appears above the system tray on left-click.
/// Each icon represents a watched app; clicking one restarts it.
/// Closes automatically when it loses focus.
/// </summary>
partial class QuickLaunchWindow : Window
{
    private readonly AppConfig            _config;
    private readonly System.Drawing.Point _trayIconPos;
    private bool _closing;
    private bool _contextMenuOpen;
    private CancellationTokenSource? _autoCloseCts;

    // Layout constants (WPF device-independent pixels)
    private const double IconSize      = 16;  // image inside button
    private const double ButtonWidth = 40;  // outer button hit-area width
    private const double ButtonHeight = 40;  // outer button hit-area height
    private const double CardPadding   = 6;   // inner card padding each side
    private const double ShadowPad     = 10;  // transparent outer border for shadow
    private const int    IconsPerRow   = 5;

    public QuickLaunchWindow(AppConfig config, System.Drawing.Point trayIconPos)
    {
        _config      = config;
        _trayIconPos = trayIconPos;
        InitializeComponent();

        BuildButtons();

        Deactivated += (_, _) => RequestClose();
        MouseLeave  += (_, _) => StartAutoClose();
        MouseEnter  += (_, _) => CancelAutoClose();
        Loaded      += (_, _) =>
        {
            SizeToContent = SizeToContent.Manual;   // take over sizing explicitly
            ApplySizeAndPosition();
            _ = LoadIconsAsync();
        };
    }

    private void RequestClose()
    {
        if (_closing || _contextMenuOpen) return;
        _closing = true;
        Close();
    }

    private void StartAutoClose()
    {
        CancelAutoClose();
        _autoCloseCts = new CancellationTokenSource();
        var token = _autoCloseCts.Token;
        Task.Delay(2000, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                Dispatcher.BeginInvoke(RequestClose);
        }, token);
    }

    private void CancelAutoClose()
    {
        _autoCloseCts?.Cancel();
        _autoCloseCts = null;
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void BuildButtons()
    {
        var btnStyle = (Style?)Application.Current.TryFindResource("TrayIconButton");

        foreach (var app in _config.Apps)
        {
            var isRunning = Process.GetProcessesByName(app.ProcessName).Length > 0;

            var img = new Image { Width = IconSize, Height = IconSize, Opacity = isRunning ? 1.0 : 0.35 };

            var btn = new Button { Style = btnStyle, Content = img, Tag = app };

            // Tooltip: PlacementMode.Custom lets us centre on the mouse X while keeping
            // the same vertical position (bottom of tooltip = 10 px above button top).
            // ToolTipService Placement/VerticalOffset are ignored for ToolTip objects,
            // so everything is handled in the callback.
            var capturedBtn = btn;
            var tt = new ToolTip
            {
                Content   = new TextBlock { Text = isRunning ? app.Name : $"{app.Name} - not running", FontSize = 12 },
                Padding   = new Thickness(8, 4, 8, 4),
                Placement = System.Windows.Controls.Primitives.PlacementMode.Custom,
                CustomPopupPlacementCallback = (popupSize, _, _) =>
                {
                    var mouse = System.Windows.Input.Mouse.GetPosition(capturedBtn);
                    double x = mouse.X - popupSize.Width  / 2;
                    double y = -popupSize.Height - 16;   // 10 px above button top
                    return [new System.Windows.Controls.Primitives.CustomPopupPlacement(
                        new System.Windows.Point(x, y),
                        System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)];
                },
            };
            btn.ToolTip = tt;
            // InitialShowDelay IS read from the owner element even for ToolTip objects.
            ToolTipService.SetInitialShowDelay(btn, 100);
            ToolTipService.SetBetweenShowDelay(btn, 0);

            var cm = new ContextMenu
            {
                Style            = (Style?)Application.Current.TryFindResource("TrayMenuStyle"),
                HorizontalOffset = -5,
                VerticalOffset   = -5,
            };
            var exitItem = new MenuItem
            {
                Header = "Exit",
                Style  = (Style?)Application.Current.TryFindResource("TrayMenuItemStyle"),
                Tag    = btn
            };
            exitItem.Click += OnExitAppClick;
            cm.Items.Add(exitItem);

            cm.Opened += (_, _) => { _contextMenuOpen = true;  CancelAutoClose(); };
            cm.Closed  += (_, _) => { _contextMenuOpen = false; if (!IsMouseOver) StartAutoClose(); };

            btn.ContextMenuOpening += (_, e) =>
            {
                if (Process.GetProcessesByName(app.ProcessName).Length == 0)
                    e.Handled = true;
            };
            btn.ContextMenu = cm;
            btn.Click += OnIconClick;
            IconsPanel.Children.Add(btn);
        }
    }

    // ── Icon loading ─────────────────────────────────────────────────────────

    private async Task LoadIconsAsync()
    {
        for (int i = 0; i < _config.Apps.Count; i++)
        {
            if (!IsLoaded) return;

            var app  = _config.Apps[i];
            var icon = await Task.Run(() => AppIconExtractor.GetIcon(app));

            if (!IsLoaded) return;

            if (icon != null && IconsPanel.Children[i] is Button btn
                             && btn.Content is Image img)
            {
                img.Source = icon;
            }
        }
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    private async void OnIconClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not WatchedApp app) return;
        RequestClose();
        await AppLauncher.RestartAppsAsync([app], minimize: false);
    }

    private async void OnExitAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not Button btn || btn.Tag is not WatchedApp app) return;

        var procs = Process.GetProcessesByName(app.ProcessName);

        foreach (var p in procs)
        {
            try { p.CloseMainWindow(); } catch { }
        }

        await Task.Delay(500);

        foreach (var p in procs)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        if (btn.Content is Image img)
            img.Opacity = 0.35;
    }

    // ── Sizing & positioning ─────────────────────────────────────────────────

    private void ApplySizeAndPosition()
    {
        int   count   = _config.Apps.Count;
        int   cols    = Math.Min(count, IconsPerRow);
        int   rows    = (count + IconsPerRow - 1) / IconsPerRow;
        double chrome = 2 * ShadowPad + 2 * CardPadding + 2; // outer + inner padding + border px

        Width  = cols * ButtonWidth + chrome;
        Height = rows * ButtonHeight + chrome;
        IconsPanel.MaxWidth = IconsPerRow * ButtonWidth;

        // Convert physical tray-icon coordinates to WPF DIPs
        var src    = PresentationSource.FromVisual(this);
        double scX = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double scY = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double iconX = _trayIconPos.X * scX;
        double iconY = _trayIconPos.Y * scY;

        // Centre the popup on the tray icon, just above the taskbar
        double left = iconX - Width  / 2;
        double top  = iconY - Height - 4;

        // Clamp within the work area
        var wa = SystemParameters.WorkArea;
        Left = Math.Clamp(left, wa.Left, wa.Right  - Width);
        Top  = Math.Clamp(top,  wa.Top,  wa.Bottom - Height);
    }
}
