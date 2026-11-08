using System.ComponentModel;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Input;
using AppVolumeHotkey.Core.Localization;

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

    public string StatusText => IsActive ? L.F("Row_Active", Profile.StepLabel(Profile.Steps[_active!.Value.StepIndex])) : "";

    public bool HasWarning => _hotkeyFailed;
    public string Warning => _hotkeyFailed ? L.T("Row_HotkeyTaken") : "";

    public string AccessibleName =>
        L.F("Row_Accessible", Name, HotkeyText, Summary) + (IsActive ? L.T("Row_AccActive") : "") + (_hotkeyFailed ? L.T("Row_AccTaken") : "");

    public string EditName => L.F("Row_EditName", Name);
    public string DeleteName => L.F("Row_DeleteName", Name);

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
