namespace AppVolumeHotkey.Core.Model;

public enum StepKind
{
    Percent,
    Baseline,

    /// <summary>App im Windows-Mixer stummschalten (Lautstärke bleibt erhalten).</summary>
    Mute,
}

/// <summary>Eine Stufe einer Profil-Abfolge: fester Prozentwert (absolut) oder „Ausgangswert“.</summary>
public sealed record Step(StepKind Kind, int Value = 0)
{
    public static Step Percent(int value) => new(StepKind.Percent, Math.Clamp(value, 0, 100));

    public static Step Baseline { get; } = new(StepKind.Baseline);

    public static Step Mute { get; } = new(StepKind.Mute);

    [System.Text.Json.Serialization.JsonIgnore]
    public string Label => Kind switch
    {
        StepKind.Baseline => Localization.L.T("Step_Baseline"),
        StepKind.Mute => Localization.L.T("Step_Mute"),
        _ => Localization.L.F("Volume_Percent", Value),
    };
}
