using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Tests;

public sealed class HoldModeTests
{
    private readonly FakeAudioBackend _audio = new();
    private readonly InMemoryBaselineStore _store = new();
    private readonly AppConfig _cfg = new();
    private readonly AppGroup _voice;
    private readonly VolumeController _vc;

    public HoldModeTests()
    {
        _audio.Running["Discord.exe"] = 0.8f;
        _voice = new AppGroup { Name = "Voice", Apps = { "Discord.exe" } };
        _cfg.Groups.Add(_voice);
        _vc = new VolumeController(_audio, _store);
    }

    private float Discord => _audio.Running["Discord.exe"];

    private Profile Add(TriggerMode trigger, ValueMode mode, params Step[] steps)
    {
        var p = new Profile { Trigger = trigger, Mode = mode, Steps = steps.ToList() };
        _voice.Profiles.Add(p);
        _vc.SetConfig(_cfg);
        return p;
    }

    [Fact]
    public void Press_AppliesFirstPercentStep_ReleaseRestoresBaseline()
    {
        var hold = Add(TriggerMode.Hold, ValueMode.Absolute, Step.Percent(0), Step.Baseline);
        var down = _vc.OnHotkey(hold.Id);
        Assert.True(down!.IsActive);
        Assert.Equal(0f, Discord, 3);

        var up = _vc.OnHotkeyReleased(hold.Id);
        Assert.False(up!.IsActive);
        Assert.Equal(0.8f, Discord, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
        Assert.Empty(_store.Data);
    }

    [Fact]
    public void RepeatedPress_WhileHeld_DoesNotCycle()
    {
        var hold = Add(TriggerMode.Hold, ValueMode.Absolute, Step.Percent(20), Step.Percent(50));
        _vc.OnHotkey(hold.Id);
        Assert.Null(_vc.OnHotkey(hold.Id));
        Assert.Equal(0.2f, Discord, 3);
    }

    [Fact]
    public void FirstStepBaseline_UsesFirstPercentStep()
    {
        var hold = Add(TriggerMode.Hold, ValueMode.Absolute, Step.Baseline, Step.Percent(30));
        _vc.OnHotkey(hold.Id);
        Assert.Equal(0.3f, Discord, 3);
    }

    [Fact]
    public void Relative_ScalesBaseline()
    {
        var hold = Add(TriggerMode.Hold, ValueMode.Relative, Step.Percent(50));
        _vc.OnHotkey(hold.Id);
        Assert.Equal(0.4f, Discord, 3);
        _vc.OnHotkeyReleased(hold.Id);
        Assert.Equal(0.8f, Discord, 3);
    }

    [Fact]
    public void Release_ReturnsToPreviouslyActiveToggleProfile()
    {
        var toggle = Add(TriggerMode.Toggle, ValueMode.Absolute, Step.Percent(50), Step.Baseline);
        var hold = Add(TriggerMode.Hold, ValueMode.Absolute, Step.Percent(0));
        _vc.OnHotkey(toggle.Id);
        Assert.Equal(0.5f, Discord, 3);

        _vc.OnHotkey(hold.Id);
        Assert.Equal(0f, Discord, 3);

        var up = _vc.OnHotkeyReleased(hold.Id);
        Assert.True(up!.IsActive);
        Assert.Equal(toggle.Id, up.Profile.Id);
        Assert.Equal(0.5f, Discord, 3);
        Assert.Equal((toggle.Id, 0), _vc.GetActive(_voice.Id));

        // Toggle läuft normal weiter und stellt den ursprünglichen Ausgangswert her
        _vc.OnHotkey(toggle.Id);
        Assert.Equal(0.8f, Discord, 3);
    }

    [Fact]
    public void Release_WithoutPress_ReturnsNull()
    {
        var hold = Add(TriggerMode.Hold, ValueMode.Absolute, Step.Percent(0));
        Assert.Null(_vc.OnHotkeyReleased(hold.Id));
    }

    [Fact]
    public void Release_OfToggleProfile_IsIgnored()
    {
        var toggle = Add(TriggerMode.Toggle, ValueMode.Absolute, Step.Percent(50), Step.Baseline);
        _vc.OnHotkey(toggle.Id);
        Assert.Null(_vc.OnHotkeyReleased(toggle.Id));
        Assert.Equal(0.5f, Discord, 3);
    }

    [Fact]
    public void HoldWithoutPercentStep_ReturnsNull()
    {
        var hold = Add(TriggerMode.Hold, ValueMode.Absolute, Step.Baseline);
        Assert.Null(_vc.OnHotkey(hold.Id));
    }

    [Fact]
    public void TriggerChangedWhileActive_ResetsGroup()
    {
        var hold = Add(TriggerMode.Hold, ValueMode.Absolute, Step.Percent(0));
        _vc.OnHotkey(hold.Id);
        hold.Trigger = TriggerMode.Toggle;
        _vc.SetConfig(_cfg);
        Assert.Equal(0.8f, Discord, 3);
    }

    [Fact]
    public void Defaults_AndClone()
    {
        Assert.Equal(TriggerMode.Toggle, new Profile().Trigger);
        Assert.Equal(TriggerMode.Hold, new Profile { Trigger = TriggerMode.Hold }.Clone().Trigger);
    }
}
