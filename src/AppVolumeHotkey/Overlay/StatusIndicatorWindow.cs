using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Interop;

namespace AppVolumeHotkey.Overlay;

/// <summary>
/// Dauerhafte, kleine Statusanzeige (klick-durchlässig, nie fokussiert), solange mindestens ein Profil aktiv ist.
/// Drei Darstellungen: Minitext (wie eine FPS-Anzeige), Symbol + Text, nur Symbol.
/// </summary>
public sealed class StatusIndicatorWindow : Window
{
    private static readonly Color Gold = Color.FromRgb(0xF2, 0xC9, 0x4C);
    private static readonly Color Green = Color.FromRgb(0x16, 0xA3, 0x4A);

    public StatusIndicatorWindow()
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
        Title = "AppVolumeHotkey Status";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var ex = (long)Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED | Native.WS_EX_TOPMOST;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
    }

    /// <summary>Anzeige aktualisieren; leere Liste oder abgeschaltet → ausblenden.</summary>
    public void Update(IReadOnlyList<string> activeItems, AppSettings settings)
    {
        if (!settings.StatusIndicator || activeItems.Count == 0)
        {
            Hide();
            return;
        }

        Content = settings.StatusStyle switch
        {
            StatusStyle.Icon => BuildIcon(),
            StatusStyle.IconText => BuildPill(string.Join("  ·  ", activeItems)),
            _ => BuildMiniText(string.Join("  ·  ", activeItems)),
        };
        Opacity = Math.Clamp(settings.StatusOpacity, 30, 100) / 100.0;

        if (!IsVisible)
        {
            Left = -32000;
            Top = -32000;
            Show();
        }
        UpdateLayout();
        Position(settings.StatusCorner);
        Topmost = false; // erneut nach oben holen, falls ein anderes Topmost-Fenster davorliegt
        Topmost = true;
    }

    private static FrameworkElement BuildMiniText(string text) => new TextBlock
    {
        Text = text,
        Foreground = new SolidColorBrush(Gold),
        FontSize = 13,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(4),
        Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Opacity = 0.9, Color = Colors.Black },
    };

    private static FrameworkElement BuildIcon()
    {
        var grid = new Grid { Width = 26, Height = 26, Margin = new Thickness(4) };
        grid.Children.Add(AppIcon(26));
        grid.Children.Add(Dot(10, HorizontalAlignment.Right, VerticalAlignment.Bottom));
        return grid;
    }

    private static FrameworkElement BuildPill(string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(AppIcon(18));
        var dot = Dot(8, HorizontalAlignment.Left, VerticalAlignment.Center);
        dot.Margin = new Thickness(6, 0, 6, 0);
        row.Children.Add(dot);
        row.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x14, 0x14, 0x14)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xE8, 0xB8, 0x4A)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(5, 3, 10, 3),
            Margin = new Thickness(4),
            Child = row,
        };
    }

    private static Image AppIcon(double size)
    {
        var img = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app-96.png")), Width = size, Height = size };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        return img;
    }

    private static FrameworkElement Dot(double size, HorizontalAlignment h, VerticalAlignment v) => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = new SolidColorBrush(Green),
        BorderBrush = Brushes.White,
        BorderThickness = new Thickness(1.5),
        HorizontalAlignment = h,
        VerticalAlignment = v,
    };

    private void Position(OverlayCorner corner)
    {
        const double margin = 4;
        var area = SystemParameters.WorkArea;
        Left = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft ? area.Left + margin : area.Right - ActualWidth - margin;
        Top = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight ? area.Top + margin : area.Bottom - ActualHeight - margin;
    }
}
