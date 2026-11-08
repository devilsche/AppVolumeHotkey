using System.ComponentModel;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Input;

namespace AppVolumeHotkey.UI;

/// <summary>Zeile der Profilliste; Status wird in place aktualisiert (kein Neuaufbau → kein Flackern).</summary>
public sealed class ProfileRow : INotifyPropertyChanged
{
    private (Guid ProfileId, int StepIndex)? _active;
    private bool _hotkeyFailed;

    public ProfileRow(Profile profile, (Guid ProfileId, int StepIndex)? active, bool hotkeyFailed)
    {
        Profile = profile;
        _active = active;
        _hotkeyFailed = hotkeyFailed;
    }

    public Profile Profile { get; }
    public string Name => Profile.Name;
    public string HotkeyText => KeyNames.Format(Profile.Hotkey);
    public string Summary => Profile.StepsSummary;

    public bool IsActive => _active?.ProfileId == Profile.Id;

    public string StatusText => IsActive ? $"● AKTIV · {Profile.StepLabel(Profile.Steps[_active!.Value.StepIndex])}" : "";

    public bool HasWarning => _hotkeyFailed;
    public string Warning => _hotkeyFailed ? "⚠ Hotkey ist von einem anderen Programm belegt und funktioniert gerade nicht." : "";

    public string AccessibleName =>
        $"{Name}, Hotkey {HotkeyText}, Abfolge {Summary}" + (IsActive ? ", aktiv" : "") + (_hotkeyFailed ? ", Hotkey belegt" : "");

    public void Update((Guid ProfileId, int StepIndex)? active, bool hotkeyFailed)
    {
        if (_active == active && _hotkeyFailed == hotkeyFailed)
            return;
        _active = active;
        _hotkeyFailed = hotkeyFailed;
        foreach (var name in new[] { nameof(IsActive), nameof(StatusText), nameof(HasWarning), nameof(Warning), nameof(AccessibleName) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
