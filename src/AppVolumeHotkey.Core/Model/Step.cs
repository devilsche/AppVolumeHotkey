namespace AppVolumeHotkey.Core.Model;

public enum StepKind
{
    Percent,
    Baseline,
}

/// <summary>Eine Stufe einer Profil-Abfolge: fester Prozentwert (absolut) oder „Ausgangswert“.</summary>
public sealed record Step(StepKind Kind, int Value = 0)
{
    public static Step Percent(int value) => new(StepKind.Percent, Math.Clamp(value, 0, 100));

    public static Step Baseline { get; } = new(StepKind.Baseline);

    [System.Text.Json.Serialization.JsonIgnore]
    public string Label => Kind == StepKind.Baseline ? "Ausgangswert" : $"{Value} %";
}
