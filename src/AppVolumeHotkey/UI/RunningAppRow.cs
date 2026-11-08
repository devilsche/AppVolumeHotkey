using System.ComponentModel;
using System.Windows.Media;
using AppVolumeHotkey.Audio;

namespace AppVolumeHotkey.UI;

/// <summary>Zeile in „Laufende Apps“; die Lautstärke wird bei jedem Abfragezyklus aktualisiert, ohne die Liste neu aufzubauen.</summary>
public sealed class RunningAppRow(RunningApp app) : INotifyPropertyChanged
{
    private int _volumePercent = -1;
    private bool _isInGroup;

    /// <summary>App ist bereits in der gerade gewählten Gruppe.</summary>
    public bool IsInGroup
    {
        get => _isInGroup;
        set
        {
            if (_isInGroup == value)
                return;
            _isInGroup = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInGroup)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGroupSuffix)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibleName)));
        }
    }

    public string Exe => app.Exe;
    public string DisplayName => app.DisplayName;
    public ImageSource? Icon => app.Icon;

    public int VolumePercent
    {
        get => _volumePercent;
        set
        {
            if (_volumePercent == value)
                return;
            _volumePercent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VolumePercent)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VolumeText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibleName)));
        }
    }

    public string InGroupSuffix => IsInGroup ? " · ✓ in Gruppe" : "";

    public string VolumeText => _volumePercent < 0 ? "–" : $"{_volumePercent} %";

    public string AccessibleName => $"{DisplayName}, Lautstärke {VolumeText}" + (IsInGroup ? ", bereits in der Gruppe" : "");

    public event PropertyChangedEventHandler? PropertyChanged;
}
