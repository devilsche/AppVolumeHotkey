namespace AppVolumeHotkey.Core.Model;

public enum MicMode
{
    /// <summary>Jeder Druck: stumm ↔ an.</summary>
    Toggle,

    /// <summary>Stumm, solange die Taste gedrückt ist (z. B. zum Husten).</summary>
    HoldToMute,

    /// <summary>Immer stumm, nur beim Halten an.</summary>
    PushToTalk,
}

public enum MicScope
{
    AllMicrophones,
    DefaultMicrophone,
}

public sealed class MicSettings
{
    public Hotkey? Hotkey { get; set; }
    public MicMode Mode { get; set; } = MicMode.Toggle;
    public MicScope Scope { get; set; } = MicScope.AllMicrophones;
    public bool ShowOverlay { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public SoundVariant SoundVariant { get; set; } = SoundVariant.Soft;

    /// <summary>Solange stumm, in der Statusanzeige zeigen.</summary>
    public bool ShowInStatus { get; set; } = true;

    /// <summary>Beim Beenden der App das Mikrofon wieder einschalten.</summary>
    public bool UnmuteOnExit { get; set; } = true;

    public bool PassThrough { get; set; }
}
