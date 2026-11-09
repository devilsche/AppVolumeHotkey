namespace AppVolumeHotkey.Core.Model;

public sealed class DuckingSettings
{
    public bool Enabled { get; set; }

    /// <summary>Optionaler Hotkey zum An-/Ausschalten.</summary>
    public Hotkey? ToggleHotkey { get; set; }

    public List<DuckingRule> Rules { get; set; } = new();
}

/// <summary>„Wenn <see cref="SourceGroupId"/> Ton ausgibt, Ziel-Apps auf <see cref="Percent"/> % ihrer Lautstärke senken.“</summary>
public sealed class DuckingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceGroupId { get; set; }

    /// <summary>true: alle anderen laufenden Apps; false: nur Apps der <see cref="TargetGroupIds"/>.</summary>
    public bool TargetAllOthers { get; set; } = true;

    public List<Guid> TargetGroupIds { get; set; } = new();

    /// <summary>Prozent der aktuellen Lautstärke der Ziel-Apps (relativ).</summary>
    public int Percent { get; set; } = 35;

    /// <summary>Pegel (0..1), ab dem die Quelle als „spricht“ gilt.</summary>
    public float Threshold { get; set; } = 0.05f;

    /// <summary>Nach so vielen ms Stille wird wieder lauter.</summary>
    public int ReleaseMs { get; set; } = 1500;

    public DuckingRule Clone() => new()
    {
        Id = Id,
        SourceGroupId = SourceGroupId,
        TargetAllOthers = TargetAllOthers,
        TargetGroupIds = new List<Guid>(TargetGroupIds),
        Percent = Percent,
        Threshold = Threshold,
        ReleaseMs = ReleaseMs,
    };
}
