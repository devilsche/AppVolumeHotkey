namespace AppVolumeHotkey.Core.Model;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public enum OverlayStyle
{
    Compact,
    Normal,
}

public enum OverlaySize
{
    Small,
    Medium,
    Large,
}

/// <summary>Gespeicherte Fensterposition/-größe (geräteunabhängige Pixel).</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized)
{
    /// <summary>Breite der Spalte „Gruppen“ (null = Standard).</summary>
    public double? GroupsWidth { get; init; }

    /// <summary>Breite der Spalte „Laufende Apps“ (null = Standard).</summary>
    public double? RunningWidth { get; init; }
}

public sealed class AppSettings
{
    /// <summary>„auto“ (wie Windows), „de“ oder „en“.</summary>
    public string Language { get; set; } = "auto";

    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; } = true;
    public bool RunAsAdmin { get; set; }

    /// <summary>Minimieren versteckt das Fenster im Infobereich statt in der Taskleiste.</summary>
    public bool MinimizeToTray { get; set; } = true;
    public OverlayCorner OverlayCorner { get; set; } = OverlayCorner.TopRight;
    public OverlaySize OverlaySize { get; set; } = OverlaySize.Medium;
    public OverlayStyle OverlayStyle { get; set; } = OverlayStyle.Compact;
    public int OverlayDurationMs { get; set; } = 1500;
    public int FadeDurationMs { get; set; } = 200;

    /// <summary>Zuletzt verwendete Größe/Position des Hauptfensters; null = Standard.</summary>
    public WindowPlacement? MainWindow { get; set; }
}
