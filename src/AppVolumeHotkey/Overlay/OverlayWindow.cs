using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Interop;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.Overlay;

/// <summary>
/// Kompakte Overlay-Karte (dunkel, halbtransparent, Gold-Akzent): Icon, Profilname, Status-Plakette, Klartext, Pegelbalken.
/// Klick-durchlässig, nimmt nie den Fokus, erscheint nicht in Taskleiste/Alt-Tab.
/// </summary>
public sealed class OverlayWindow : Window
{
    private static readonly Color Gold = Color.FromRgb(0xE8, 0xB8, 0x4A);

    private readonly TextBlock _name = new() { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _detail = new() { Foreground = new SolidColorBrush(Color.FromArgb(0xD0, 0xFF, 0xFF, 0xFF)), FontSize = 12.5, Margin = new Thickness(0, 1, 0, 0) };
    private readonly Border _badge = new() { CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _badgeText = new() { FontSize = 11, FontWeight = FontWeights.Bold };
    private readonly ScaleTransform _barScale = new(1, 1);
    private readonly ScaleTransform _sizeScale = new(1, 1);
    private readonly Image _icon;
    private readonly Border _card;
    private readonly Border _track;
    private readonly Border _fill;
    private readonly DispatcherTimer _hideTimer;
    private readonly Dictionary<Guid, double> _lastFraction = new(); // letzter Balkenstand je Gruppe (1 = Ausgangswert)

    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        IsHitTestVisible = false;
        Title = "AppVolumeHotkey Overlay";

        _badge.Child = _badgeText;

        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_badge, Dock.Right);
        header.Children.Add(_badge);
        header.Children.Add(_name);

        var track = _track = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) };
        var fill = _fill = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), Background = new SolidColorBrush(Gold), RenderTransformOrigin = new Point(0, 0.5), RenderTransform = _barScale };
        var bar = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        bar.Children.Add(track);
        bar.Children.Add(fill);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(header);
        text.Children.Add(_detail);
        text.Children.Add(bar);

        var icon = _icon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app-96.png")),
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);

        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(text);

        Content = _card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xDE, 0x14, 0x14, 0x14)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, Gold.R, Gold.G, Gold.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 7, 12, 8),
            MinWidth = 210,
            MaxWidth = 320,
            Child = row,
            LayoutTransform = _sizeScale,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.45 },
            Margin = new Thickness(8), // Platz für den Schatten
        };

        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); HideOverlay(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var ex = (long)Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED | Native.WS_EX_TOPMOST;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
    }

    public void ShowResult(HotkeyResult r, AppSettings settings)
    {
        _name.Text = r.Profile.Name;
        if (r.IsActive)
        {
            _detail.Text = r.Profile.StepLabel(r.Step);
            _badgeText.Text = L.T("Overlay_Active");
            _badgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x16, 0x06));
            _badge.Background = new SolidColorBrush(Gold);
            _badge.BorderThickness = new Thickness(0);
        }
        else
        {
            _detail.Text = L.T("Overlay_Back");
            _badgeText.Text = L.T("Overlay_Off");
            _badgeText.Foreground = new SolidColorBrush(Color.FromArgb(0xD0, 0xFF, 0xFF, 0xFF));
            _badge.Background = Brushes.Transparent;
            _badge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
            _badge.BorderThickness = new Thickness(1);
        }
        AnimateBar(r, settings);

        ApplyStyle(settings.OverlayStyle);

        var scale = settings.OverlaySize switch
        {
            OverlaySize.Small => 0.9,
            OverlaySize.Large => 1.35,
            _ => 1.0,
        };
        _sizeScale.ScaleX = _sizeScale.ScaleY = scale;

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        if (!IsVisible)
        {
            Left = -32000; // außerhalb zeigen, messen, dann positionieren – kein Aufblitzen
            Top = -32000;
            Show();
        }
        UpdateLayout();
        Position(settings.OverlayCorner);

        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(300, settings.OverlayDurationMs));
        _hideTimer.Start();
    }

    /// <summary>Balken läuft synchron zum Lautstärke-Übergang vom vorherigen zum neuen Stand.</summary>
    private void AnimateBar(HotkeyResult r, AppSettings settings)
    {
        var target = r.IsActive ? Math.Clamp(r.Step.Value / 100.0, 0, 1) : 1.0;
        var from = _lastFraction.TryGetValue(r.Group.Id, out var last) ? last : 1.0;
        _lastFraction[r.Group.Id] = r.IsActive ? target : 1.0;

        _barScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        if (settings.FadeDurationMs <= 0 || !SystemParameters.ClientAreaAnimation)
        {
            _barScale.ScaleX = target;
            return;
        }
        _barScale.ScaleX = from;
        _barScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(settings.FadeDurationMs)) { FillBehavior = FillBehavior.HoldEnd });
    }

    /// <summary>Kompakt (Standard) oder Normal (größer, luftiger).</summary>
    private void ApplyStyle(OverlayStyle style)
    {
        var normal = style == OverlayStyle.Normal;
        _icon.Width = _icon.Height = normal ? 36 : 28;
        _icon.Margin = new Thickness(0, 0, normal ? 12 : 10, 0);
        _name.FontSize = normal ? 16 : 14;
        _detail.FontSize = normal ? 14 : 12.5;
        _badgeText.FontSize = normal ? 12 : 11;
        _track.Height = _fill.Height = normal ? 4 : 3;
        _card.Padding = normal ? new Thickness(14, 10, 14, 11) : new Thickness(10, 7, 12, 8);
        _card.CornerRadius = new CornerRadius(normal ? 12 : 10);
        _card.MinWidth = normal ? 260 : 210;
        _card.MaxWidth = normal ? 380 : 320;
    }

    private void Position(OverlayCorner corner)
    {
        const double margin = 8; // plus 8 px Schattenrand im Fenster
        var area = SystemParameters.WorkArea;
        Left = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft ? area.Left + margin : area.Right - ActualWidth - margin;
        Top = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight ? area.Top + margin : area.Bottom - ActualHeight - margin;
    }

    private void HideOverlay()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Hide();
            return;
        }
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => { if (Opacity == 0) Hide(); };
        BeginAnimation(OpacityProperty, fade);
    }
}
