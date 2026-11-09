using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Persistence;

namespace AppVolumeHotkey.Core.Tests;

internal sealed class MuteAudio : IAudioBackend
{
    public Dictionary<string, float> Volume { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Muted { get; } = new(StringComparer.OrdinalIgnoreCase);
    public float? GetVolume(string exe) => Volume.TryGetValue(exe, out var v) ? v : null;
    public void SetVolume(string exe, float v) { if (Volume.ContainsKey(exe)) Volume[exe] = v; }
    public bool? GetMuted(string exe) => Volume.ContainsKey(exe) ? Muted.Contains(exe) : null;
    public void SetMuted(string exe, bool muted) { if (!Volume.ContainsKey(exe)) return; if (muted) Muted.Add(exe); else Muted.Remove(exe); }
}

public sealed class MuteStepTests
{
    private readonly MuteAudio _audio = new();
    private readonly AppConfig _cfg = new();
    private readonly AppGroup _voice = new() { Name = "Voice", Apps = { "Discord.exe" } };
    private readonly VolumeController _vc;

    public MuteStepTests()
    {
        _audio.Volume["Discord.exe"] = 0.8f;
        _cfg.Groups.Add(_voice);
        _vc = new VolumeController(_audio, new InMemoryBaselineStore());
    }

    private Profile Add(TriggerMode trigger, params Step[] steps)
    {
        var p = new Profile { Trigger = trigger, Steps = steps.ToList() };
        _voice.Profiles.Add(p);
        _vc.SetConfig(_cfg);
        return p;
    }

    [Fact]
    public void MuteStep_MutesWithoutChangingVolume_BackUnmutes()
    {
        var p = Add(TriggerMode.Toggle, Step.Mute, Step.Baseline);
        var r = _vc.OnHotkey(p.Id)!;
        Assert.True(r.IsActive);
        Assert.Contains("Discord.exe", _audio.Muted);
        Assert.Equal(0.8f, _audio.Volume["Discord.exe"], 3);

        _vc.OnHotkey(p.Id);
        Assert.DoesNotContain("Discord.exe", _audio.Muted);
    }

    [Fact]
    public void FromMuteToPercent_Unmutes()
    {
        var p = Add(TriggerMode.Toggle, Step.Mute, Step.Percent(50));
        _vc.OnHotkey(p.Id);
        _vc.OnHotkey(p.Id);
        Assert.DoesNotContain("Discord.exe", _audio.Muted);
        Assert.Equal(0.5f, _audio.Volume["Discord.exe"], 3);
    }

    [Fact]
    public void AlreadyMutedByUser_StaysMutedAfterReset()
    {
        _audio.Muted.Add("Discord.exe");
        var p = Add(TriggerMode.Toggle, Step.Mute, Step.Baseline);
        _vc.OnHotkey(p.Id);
        _vc.OnHotkey(p.Id);
        Assert.Contains("Discord.exe", _audio.Muted); // wir haben es nicht stummgeschaltet → nicht aufheben
    }

    [Fact]
    public void HoldMute_MutesWhileHeld()
    {
        var p = Add(TriggerMode.Hold, Step.Mute);
        _vc.OnHotkey(p.Id);
        Assert.Contains("Discord.exe", _audio.Muted);
        _vc.OnHotkeyReleased(p.Id);
        Assert.DoesNotContain("Discord.exe", _audio.Muted);
    }

    [Fact]
    public void MuteLabel() => Assert.Equal("Stumm", Step.Mute.Label);
}

public sealed class LockVolumeTests
{
    private readonly MuteAudio _audio = new();
    private readonly AppConfig _cfg = new();
    private readonly AppGroup _voice = new() { Name = "Voice", Apps = { "Discord.exe" }, LockVolume = true };
    private readonly VolumeController _vc;
    private readonly Profile _p;

    public LockVolumeTests()
    {
        _audio.Volume["Discord.exe"] = 0.8f;
        _p = new Profile { Mode = ValueMode.Relative, Steps = { Step.Percent(20), Step.Baseline } };
        _voice.Profiles.Add(_p);
        _cfg.Groups.Add(_voice);
        _vc = new VolumeController(_audio, new InMemoryBaselineStore());
        _vc.SetConfig(_cfg);
    }

    [Fact]
    public void Enforce_RestoresStepValue_WhenAppResetsItself()
    {
        _vc.OnHotkey(_p.Id);
        _audio.Volume["Discord.exe"] = 1.0f; // Discord setzt sich selbst zurück
        _vc.Enforce();
        Assert.Equal(0.16f, _audio.Volume["Discord.exe"], 3);
    }

    [Fact]
    public void Enforce_DoesNothing_WhenNoProfileActive()
    {
        _audio.Volume["Discord.exe"] = 1.0f;
        _vc.Enforce();
        Assert.Equal(1.0f, _audio.Volume["Discord.exe"], 3);
    }

    [Fact]
    public void Enforce_DoesNothing_WhenLockOff()
    {
        _voice.LockVolume = false;
        _vc.SetConfig(_cfg);
        _vc.OnHotkey(_p.Id);
        _audio.Volume["Discord.exe"] = 1.0f;
        _vc.Enforce();
        Assert.Equal(1.0f, _audio.Volume["Discord.exe"], 3);
    }

    [Fact]
    public void Enforce_SkipsExemptApps()
    {
        _vc.OnHotkey(_p.Id);
        _audio.Volume["Discord.exe"] = 0.05f; // z. B. gerade vom Ducking gesenkt
        _vc.Enforce(exe => true);
        Assert.Equal(0.05f, _audio.Volume["Discord.exe"], 3);
    }

    [Fact]
    public void Enforce_ReMutes_MuteStep()
    {
        _p.Steps[0] = Step.Mute;
        _vc.SetConfig(_cfg);
        _vc.OnHotkey(_p.Id);
        _audio.Muted.Clear(); // App hebt Stummschaltung selbst auf
        _vc.Enforce();
        Assert.Contains("Discord.exe", _audio.Muted);
    }
}

public sealed class ImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avh-import-" + Guid.NewGuid());
    public ImportTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void ExportThenImport_RoundTrips_WithoutWindowPlacement()
    {
        var cfg = new AppConfig { Groups = { new AppGroup { Name = "Voice", LockVolume = true } } };
        cfg.Mic.Mode = MicMode.PushToTalk;
        cfg.Ducking.Rules.Add(new DuckingRule { Percent = 20 });
        cfg.Settings.MainWindow = new WindowPlacement(1, 2, 1000, 700, false);
        var file = Path.Combine(_dir, "export.json");

        ConfigStore.Export(file, cfg);
        Assert.True(ConfigStore.TryImport(file, out var imported, out var error), error);
        Assert.Equal("Voice", imported!.Groups[0].Name);
        Assert.True(imported.Groups[0].LockVolume);
        Assert.Equal(MicMode.PushToTalk, imported.Mic.Mode);
        Assert.Equal(20, imported.Ducking.Rules[0].Percent);
        Assert.Null(imported.Settings.MainWindow);
    }

    [Theory]
    [InlineData("{nicht json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"foo\":1}")]
    public void Import_InvalidFile_Fails_AndLeavesFileAlone(string content)
    {
        var file = Path.Combine(_dir, "bad.json");
        File.WriteAllText(file, content);
        Assert.False(ConfigStore.TryImport(file, out var cfg, out var error));
        Assert.Null(cfg);
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Equal(content, File.ReadAllText(file));
    }

    [Fact]
    public void Import_MissingFile_Fails()
    {
        Assert.False(ConfigStore.TryImport(Path.Combine(_dir, "nope.json"), out _, out _));
    }
}
