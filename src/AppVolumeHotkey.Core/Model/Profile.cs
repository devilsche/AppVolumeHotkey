namespace AppVolumeHotkey.Core.Model;

public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Neues Profil";
    public Hotkey? Hotkey { get; set; }
    public List<Step> Steps { get; set; } = new();
    public bool OverlayOnActivate { get; set; } = true;
    public bool OverlayOnDeactivate { get; set; } = true;
    public bool Sound { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string StepsSummary => Steps.Count == 0 ? "(keine Stufen)" : string.Join(" → ", Steps.Select(s => s.Label));

    public Profile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Hotkey = Hotkey,
        Steps = new List<Step>(Steps),
        OverlayOnActivate = OverlayOnActivate,
        OverlayOnDeactivate = OverlayOnDeactivate,
        Sound = Sound,
    };
}
