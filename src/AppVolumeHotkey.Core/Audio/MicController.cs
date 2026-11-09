using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Audio;

/// <summary>Stummschalten des Mikrofons in Windows (wirkt in allen Programmen).</summary>
public interface IMicBackend
{
    /// <summary>true = stumm, null = kein Mikrofon gefunden.</summary>
    bool? IsMuted();

    void SetMuted(bool muted);
}

public sealed record MicResult(bool Muted, bool ShowOverlay, bool PlaySound, SoundVariant Sound);

/// <summary>Logik für den Mikrofon-Hotkey: Umschalten, Halten zum Stummschalten oder Push-to-Talk.</summary>
public sealed class MicController(IMicBackend mic)
{
    private MicSettings _settings = new();
    private bool _mutedBeforeHold;
    private bool _held;

    public event Action? StateChanged;

    public MicSettings Settings => _settings;

    public bool? IsMuted => mic.IsMuted();

    public void SetSettings(MicSettings settings)
    {
        var wasPtt = _settings.Mode == MicMode.PushToTalk;
        _settings = settings;
        var isPtt = settings.Mode == MicMode.PushToTalk;
        if (isPtt && !wasPtt)
            Set(true);   // Push-to-Talk: standardmäßig stumm
        else if (wasPtt && !isPtt)
            Set(false);  // Push-to-Talk verlassen: Mikrofon wieder an
        StateChanged?.Invoke();
    }

    public MicResult? OnPressed()
    {
        if (_held)
            return null;
        _held = true;
        switch (_settings.Mode)
        {
            case MicMode.Toggle:
                _held = false; // Loslassen spielt keine Rolle
                return Result(Set(!(mic.IsMuted() ?? false)));
            case MicMode.HoldToMute:
                _mutedBeforeHold = mic.IsMuted() ?? false;
                return Result(Set(true));
            default: // PushToTalk
                return Result(Set(false));
        }
    }

    public MicResult? OnReleased()
    {
        if (!_held)
            return null;
        _held = false;
        return _settings.Mode switch
        {
            MicMode.HoldToMute => Result(Set(_mutedBeforeHold)),
            MicMode.PushToTalk => Result(Set(true)),
            _ => null,
        };
    }

    /// <summary>Direkt aus der Oberfläche (Knopf „Jetzt einschalten“ o. Ä.).</summary>
    public void SetMuted(bool muted)
    {
        Set(muted);
    }

    public void OnExit()
    {
        if (_settings.UnmuteOnExit && mic.IsMuted() == true)
            mic.SetMuted(false);
    }

    private bool Set(bool muted)
    {
        mic.SetMuted(muted);
        StateChanged?.Invoke();
        return muted;
    }

    private MicResult Result(bool muted) => new(muted, _settings.ShowOverlay, _settings.PlaySound, _settings.SoundVariant);
}
