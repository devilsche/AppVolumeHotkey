namespace AppVolumeHotkey.Core.Model;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public enum OverlaySize
{
    Small,
    Medium,
    Large,
}

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; } = true;
    public bool RunAsAdmin { get; set; }
    public OverlayCorner OverlayCorner { get; set; } = OverlayCorner.TopRight;
    public OverlaySize OverlaySize { get; set; } = OverlaySize.Medium;
    public int OverlayDurationMs { get; set; } = 1500;
    public int FadeDurationMs { get; set; } = 200;
}
