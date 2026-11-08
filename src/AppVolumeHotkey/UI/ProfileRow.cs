using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Input;

namespace AppVolumeHotkey.UI;

public sealed class ProfileRow(Profile profile, (Guid ProfileId, int StepIndex)? active, bool hotkeyFailed)
{
    public Profile Profile { get; } = profile;
    public string Name => Profile.Name;
    public string HotkeyText => KeyNames.Format(Profile.Hotkey);
    public string Summary => Profile.StepsSummary;

    public bool IsActive => active?.ProfileId == Profile.Id;

    public string StatusText => IsActive ? $"● AKTIV · {Profile.StepLabel(Profile.Steps[active!.Value.StepIndex])}" : "";

    public bool HasWarning => hotkeyFailed;
    public string Warning => hotkeyFailed ? "⚠ Hotkey ist von einem anderen Programm belegt und funktioniert gerade nicht." : "";

    public string AccessibleName =>
        $"{Name}, Hotkey {HotkeyText}, Abfolge {Summary}" + (IsActive ? ", aktiv" : "") + (hotkeyFailed ? ", Hotkey belegt" : "");
}
