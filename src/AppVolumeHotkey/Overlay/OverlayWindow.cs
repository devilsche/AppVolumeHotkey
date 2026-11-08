using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Interop;
using AppVolumeHotkey.Platform;

namespace AppVolumeHotkey.Overlay;

/// <summary>Kleines, klick-durchlässiges Fenster, das nie den Fokus nimmt und nicht in Taskleiste/Alt-Tab erscheint.</summary>
public sealed class OverlayWindow : Window
{
    private readonly TextBlock _text;
    private readonly Border _border;
    private readonly DispatcherTimer _hideTimer;

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

        _text = new TextBlock { FontWeight = FontWeights.SemiBold };
        _border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8, 14, 8),
            BorderThickness = new Thickness(1),
            Child = _text,
        };
        Content = _border;

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

    public void ShowMessage(string message, AppSettings settings)
    {
        var dark = WindowsTheme.IsDark();
        _border.Background = new SolidColorBrush(dark ? Color.FromArgb(0xE6, 0x20, 0x20, 0x20) : Color.FromArgb(0xF2, 0xF3, 0xF3, 0xF3));
        _border.BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x40, 0x00, 0x00, 0x00));
        _text.Foreground = dark ? Brushes.White : Brushes.Black;
        _text.FontSize = settings.OverlaySize switch
        {
            OverlaySize.Small => 14,
            OverlaySize.Large => 24,
            _ => 18,
        };
        _text.Text = message;

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        if (!IsVisible)
        {
            Left = -32000; // außerhalb des Bildschirms zeigen, messen, dann positionieren – kein Aufblitzen
            Top = -32000;
            Show();
        }
        UpdateLayout();
        Position(settings.OverlayCorner);

        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(300, settings.OverlayDurationMs));
        _hideTimer.Start();
    }

    private void Position(OverlayCorner corner)
    {
        const double margin = 16;
        var area = SystemParameters.WorkArea;
        var w = ActualWidth;
        var h = ActualHeight;
        Left = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft ? area.Left + margin : area.Right - w - margin;
        Top = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight ? area.Top + margin : area.Bottom - h - margin;
    }

    private void HideOverlay()
    {
        // „Animationen in Windows anzeigen“ aus → ohne Ausblenden
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
