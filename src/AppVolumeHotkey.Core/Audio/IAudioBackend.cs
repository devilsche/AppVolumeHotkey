namespace AppVolumeHotkey.Core.Audio;

public interface IAudioBackend
{
    /// <summary>Aktuelle Lautstärke (0..1) der App oder null, wenn sie keine Audio-Session hat.</summary>
    float? GetVolume(string exe);

    /// <summary>Setzt alle Sessions der App auf den Wert (0..1). Fading ist Sache der Implementierung.</summary>
    void SetVolume(string exe, float volume01);
}
