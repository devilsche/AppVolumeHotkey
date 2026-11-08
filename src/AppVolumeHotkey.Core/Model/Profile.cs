namespace AppVolumeHotkey.Core.Model;

/// <summary>Worauf sich die Prozentwerte der Stufen beziehen.</summary>
public enum ValueMode
{
    /// <summary>20 % = 20 % im Windows-Mixer.</summary>
    Absolute,

    /// <summary>20 % = 20 % des Ausgangswerts der jeweiligen App (80 % → 16 %).</summary>
    Relative,
}

/// <summary>Eigene Bestätigungstöne (je ein Ton für „aktiv“ und „aus“).</summary>
public enum SoundVariant
{
    TwoTones,
    Click,
    Bell,
    Blip,
    Soft,
}

public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Neues Profil";
    public Hotkey? Hotkey { get; set; }
    public List<Step> Steps { get; set; } = new();
    public bool OverlayOnActivate { get; set; } = true;
    public bool OverlayOnDeactivate { get; set; } = true;
    public bool Sound { get; set; }

    public SoundVariant SoundVariant { get; set; } = SoundVariant.TwoTones;

    /// <summary>Fehlt in alten Konfigurationen → absolut (bisheriges Verhalten).</summary>
    public ValueMode Mode { get; set; } = ValueMode.Absolute;

    /// <summary>Nur für Maus-Hotkeys: Klick zusätzlich an Spiel/andere Programme weitergeben.</summary>
    public bool PassThrough { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string StepsSummary => Steps.Count == 0 ? "(keine Stufen)" : string.Join(" → ", Steps.Select(StepLabel));

    /// <summary>Beschriftung einer Stufe dieses Profils, z. B. „20 % vom Ausgangswert“.</summary>
    public string StepLabel(Step step) =>
        step.Kind == StepKind.Percent && Mode == ValueMode.Relative ? $"{step.Value} % vom Ausgangswert" : step.Label;

    public Profile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Hotkey = Hotkey,
        Steps = new List<Step>(Steps),
        OverlayOnActivate = OverlayOnActivate,
        OverlayOnDeactivate = OverlayOnDeactivate,
        Sound = Sound,
        SoundVariant = SoundVariant,
        PassThrough = PassThrough,
        Mode = Mode,
    };
}
