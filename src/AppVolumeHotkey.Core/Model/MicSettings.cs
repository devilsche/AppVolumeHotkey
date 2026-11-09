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
    /// <summary>Alle Mikrofone (Gerät) – wirkt in jedem Programm.</summary>
    AllMicrophones,

    /// <summary>Nur das Standard-Mikrofon (Gerät).</summary>
    DefaultMicrophone,

    /// <summary>Nur die Aufnahme der Apps einer Gruppe (z. B. Edge für Meet).</summary>
    Group,
}

/// <summary>Ein Mikrofon-Hotkey mit eigenem Modus und Bereich.</summary>
public sealed class MicRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Hotkey? Hotkey { get; set; }
    public bool PassThrough { get; set; }
    public MicMode Mode { get; set; } = MicMode.Toggle;
    public MicScope Scope { get; set; } = MicScope.AllMicrophones;

    /// <summary>Gruppe bei <see cref="MicScope.Group"/>.</summary>
    public Guid GroupId { get; set; }

    public bool ShowOverlay { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public SoundVariant SoundVariant { get; set; } = SoundVariant.Soft;

    /// <summary>Zustand in der Statusanzeige zeigen (stumm / PTT / LIVE).</summary>
    public bool ShowInStatus { get; set; } = true;
}

public sealed class MicSettings
{
    public List<MicRule> Rules { get; set; } = new();

    /// <summary>Beim Beenden der App alles wieder einschalten.</summary>
    public bool UnmuteOnExit { get; set; } = true;

    // ---- Altformat (bis 0.16): ein einzelner Mikrofon-Hotkey. Wird beim Laden in Rules übernommen. ----
    public Hotkey? Hotkey { get; set; }
    public MicMode Mode { get; set; } = MicMode.Toggle;
    public MicScope Scope { get; set; } = MicScope.AllMicrophones;
    public bool ShowOverlay { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public SoundVariant SoundVariant { get; set; } = SoundVariant.Soft;
    public bool ShowInStatus { get; set; } = true;
    public bool PassThrough { get; set; }

    /// <summary>Altformat in eine Regel übernehmen (einmalig beim Laden).</summary>
    public void MigrateLegacy()
    {
        if (Rules.Count == 0 && Hotkey is not null)
        {
            Rules.Add(new MicRule
            {
                Hotkey = Hotkey, PassThrough = PassThrough, Mode = Mode, Scope = Scope == MicScope.Group ? MicScope.AllMicrophones : Scope,
                ShowOverlay = ShowOverlay, PlaySound = PlaySound, SoundVariant = SoundVariant, ShowInStatus = ShowInStatus,
            });
        }
        Hotkey = null;
    }
}
