using System.ComponentModel;
using AppVolumeHotkey.Core.Localization;

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

    public string VolumeText => _volumePercent < 0 ? L.T("Chip_NotRunning") : L.F("Volume_Percent", _volumePercent);

    public string AccessibleName => $"{Exe}, {VolumeText}";

    public string RemoveName => L.F("Chip_Remove", Exe);

    public event PropertyChangedEventHandler? PropertyChanged;
}
