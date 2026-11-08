using System.ComponentModel;

namespace AppVolumeHotkey.UI;

/// <summary>App-Chip in der Gruppe mit Live-Lautstärke aus dem Windows-Mixer.</summary>
public sealed class AppChipRow(string exe) : INotifyPropertyChanged
{
    private int _volumePercent = -1;

    public string Exe { get; } = exe;

    public int VolumePercent
    {
        get => _volumePercent;
        set
        {
            if (_volumePercent == value)
                return;
            _volumePercent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VolumeText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibleName)));
        }
    }

    public string VolumeText => _volumePercent < 0 ? "läuft nicht" : $"{_volumePercent} %";

    public string AccessibleName => $"{Exe}, {VolumeText}";

    public event PropertyChangedEventHandler? PropertyChanged;
}
