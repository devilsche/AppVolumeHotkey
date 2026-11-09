using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Tests;

internal sealed class FakeMeterAudio : IAudioBackend, IPeakMeter
{
    public Dictionary<string, float> Volume { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> Peak { get; } = new(StringComparer.OrdinalIgnoreCase);
    public float? GetVolume(string exe) => Volume.TryGetValue(exe, out var v) ? v : null;
    public void SetVolume(string exe, float v) { if (Volume.ContainsKey(exe)) Volume[exe] = v; }
    public float? GetPeak(string exe) => Peak.TryGetValue(exe, out var v) ? v : null;
    public IReadOnlyCollection<string> RunningExes() => Volume.Keys.ToList();
}

public sealed class DuckingControllerTests
{
    private readonly FakeMeterAudio _audio = new();
    private DateTime _now = new(2026, 1, 1);
    private readonly AppConfig _cfg = new();
    private readonly AppGroup _voice = new() { Name = "Voice", Apps = { "Discord.exe" } };
    private readonly AppGroup _musik = new() { Name = "Musik", Apps = { "Spotify.exe" } };
    private readonly DuckingController _dc;

    public DuckingControllerTests()
    {
        _audio.Volume["Discord.exe"] = 0.8f;
        _audio.Volume["Spotify.exe"] = 0.6f;
        _audio.Volume["cs2.exe"] = 1.0f;
        _cfg.Groups.Add(_voice);
        _cfg.Groups.Add(_musik);
        _cfg.Ducking.Enabled = true;
        _cfg.Ducking.Rules.Add(new DuckingRule { SourceGroupId = _voice.Id, Percent = 50, Threshold = 0.1f, ReleaseMs = 1000 });
        _dc = new DuckingController(_audio, _audio, () => _now);
        _dc.SetConfig(_cfg);
    }

    private void Speak(float peak) { _audio.Peak["Discord.exe"] = peak; _dc.Tick(); }
    private void Wait(int ms) { _now = _now.AddMilliseconds(ms); _dc.Tick(); }

    [Fact]
    public void Loud_DucksAllOthers_NotSource()
    {
        Speak(0.5f);
        Assert.True(_dc.IsDucking);
        Assert.Equal(0.3f, _audio.Volume["Spotify.exe"], 3);
        Assert.Equal(0.5f, _audio.Volume["cs2.exe"], 3);
        Assert.Equal(0.8f, _audio.Volume["Discord.exe"], 3);
    }

    [Fact]
    public void Quiet_BelowThreshold_DoesNothing()
    {
        Speak(0.05f);
        Assert.False(_dc.IsDucking);
        Assert.Equal(0.6f, _audio.Volume["Spotify.exe"], 3);
    }

    [Fact]
    public void Silence_ForReleaseTime_Restores()
    {
        Speak(0.5f);
        Speak(0f);
        Wait(500);
        Assert.True(_dc.IsDucking);
        Wait(600);
        Assert.False(_dc.IsDucking);
        Assert.Equal(0.6f, _audio.Volume["Spotify.exe"], 3);
        Assert.Equal(1.0f, _audio.Volume["cs2.exe"], 3);
    }

    [Fact]
    public void ContinuedSpeech_ExtendsDucking_NoDoubleDucking()
    {
        Speak(0.5f);
        Wait(800);
        Speak(0.5f);
        Wait(800);
        Assert.True(_dc.IsDucking);
        Assert.Equal(0.3f, _audio.Volume["Spotify.exe"], 3);
    }

    [Fact]
    public void UserChangedVolumeWhileDucked_IsNotOverwritten()
    {
        Speak(0.5f);
        _audio.Volume["Spotify.exe"] = 0.9f; // manuell im Mixer geändert
        Speak(0f);
        Wait(1100);
        Assert.Equal(0.9f, _audio.Volume["Spotify.exe"], 3);
    }

    [Fact]
    public void TargetGroups_OnlyThoseAreDucked()
    {
        _cfg.Ducking.Rules[0].TargetAllOthers = false;
        _cfg.Ducking.Rules[0].TargetGroupIds.Add(_musik.Id);
        _dc.SetConfig(_cfg);
        Speak(0.5f);
        Assert.Equal(0.3f, _audio.Volume["Spotify.exe"], 3);
        Assert.Equal(1.0f, _audio.Volume["cs2.exe"], 3);
    }

    [Fact]
    public void NewTargetAppWhileDucked_IsDuckedToo()
    {
        Speak(0.5f);
        _audio.Volume["firefox.exe"] = 0.4f;
        Speak(0.5f);
        Assert.Equal(0.2f, _audio.Volume["firefox.exe"], 3);
    }

    [Fact]
    public void Disabling_RestoresImmediately()
    {
        Speak(0.5f);
        _cfg.Ducking.Enabled = false;
        _dc.SetConfig(_cfg);
        Assert.False(_dc.IsDucking);
        Assert.Equal(0.6f, _audio.Volume["Spotify.exe"], 3);
    }

    [Fact]
    public void Toggle_SwitchesEnabled()
    {
        Assert.False(_dc.Toggle());
        Assert.False(_cfg.Ducking.Enabled);
        Assert.True(_dc.Toggle());
    }

    [Fact]
    public void RestoreAll_OnExit()
    {
        Speak(0.5f);
        _dc.RestoreAll();
        Assert.Equal(0.6f, _audio.Volume["Spotify.exe"], 3);
    }

    [Fact]
    public void RuleWithMissingSourceGroup_IsIgnored()
    {
        _cfg.Ducking.Rules[0].SourceGroupId = Guid.NewGuid();
        _dc.SetConfig(_cfg);
        Speak(0.9f);
        Assert.False(_dc.IsDucking);
    }
}
