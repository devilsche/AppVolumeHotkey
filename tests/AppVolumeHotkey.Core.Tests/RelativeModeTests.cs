using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Tests;

public sealed class RelativeModeTests
{
    private readonly FakeAudioBackend _audio = new();
    private readonly InMemoryBaselineStore _store = new();
    private readonly AppConfig _cfg = new();
    private readonly AppGroup _voice;
    private readonly VolumeController _vc;

    public RelativeModeTests()
    {
        _audio.Running["Discord.exe"] = 0.8f;
        _audio.Running["ts3client_win64.exe"] = 1.0f;
        _voice = new AppGroup { Name = "Voice", Apps = { "Discord.exe", "ts3client_win64.exe" } };
        _cfg.Groups.Add(_voice);
        _vc = new VolumeController(_audio, _store);
    }

    private Profile Add(params Step[] steps)
    {
        var p = new Profile { Mode = ValueMode.Relative, Steps = steps.ToList() };
        _voice.Profiles.Add(p);
        _vc.SetConfig(_cfg);
        return p;
    }

    private float Discord => _audio.Running["Discord.exe"];
    private float Ts3 => _audio.Running["ts3client_win64.exe"];

    [Fact]
    public void FirstStep_ScalesEachAppsBaseline()
    {
        var p = Add(Step.Percent(20), Step.Baseline);
        _vc.OnHotkey(p.Id);
        Assert.Equal(0.16f, Discord, 3);
        Assert.Equal(0.20f, Ts3, 3);
        _vc.OnHotkey(p.Id);
        Assert.Equal(0.8f, Discord, 3);
        Assert.Equal(1.0f, Ts3, 3);
    }

    [Fact]
    public void Cycle_20_50_StaysRelativeToOriginal()
    {
        var p = Add(Step.Percent(20), Step.Percent(50));
        _vc.OnHotkey(p.Id);
        _vc.OnHotkey(p.Id);
        Assert.Equal(0.40f, Discord, 3);
        Assert.Equal(0.50f, Ts3, 3);
        _vc.OnHotkey(p.Id);
        Assert.Equal(0.16f, Discord, 3);
    }

    [Fact]
    public void SwitchProfile_UsesOriginalBaseline()
    {
        var p = Add(Step.Percent(20), Step.Baseline);
        var q = Add(Step.Percent(50), Step.Baseline);
        _vc.OnHotkey(p.Id);
        _vc.OnHotkey(q.Id);
        Assert.Equal(0.40f, Discord, 3);
    }

    [Fact]
    public void SessionAdded_UsesItsOwnBaseline()
    {
        _audio.Running.Remove("ts3client_win64.exe");
        var p = Add(Step.Percent(20), Step.Baseline);
        _vc.OnHotkey(p.Id);
        _audio.Running["ts3client_win64.exe"] = 0.9f;
        _vc.OnSessionAdded("ts3client_win64.exe");
        Assert.Equal(0.18f, Ts3, 3);
    }

    [Fact]
    public void ModeChangedWhileActive_ResetsGroup()
    {
        var p = Add(Step.Percent(20), Step.Baseline);
        _vc.OnHotkey(p.Id);
        p.Mode = ValueMode.Absolute;
        _vc.SetConfig(_cfg);
        Assert.Equal(0.8f, Discord, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
    }

    [Fact]
    public void DefaultMode_IsAbsolute_ForBackwardCompatibility() =>
        Assert.Equal(ValueMode.Absolute, new Profile().Mode);

    [Fact]
    public void Describe()
    {
        var p = new Profile { Mode = ValueMode.Relative, Steps = { Step.Percent(20), Step.Baseline } };
        Assert.Equal("20 % vom Ausgangswert → Ausgangswert", p.StepsSummary);
        p.Mode = ValueMode.Absolute;
        Assert.Equal("20 % → Ausgangswert", p.StepsSummary);
    }
}
