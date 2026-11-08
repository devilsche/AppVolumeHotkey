using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Tests;

public sealed class VolumeControllerTests
{
    private readonly FakeAudioBackend _audio = new();
    private readonly InMemoryBaselineStore _store = new();
    private readonly AppConfig _cfg = new();
    private readonly AppGroup _voice;
    private readonly Profile _leise;
    private readonly VolumeController _vc;

    public VolumeControllerTests()
    {
        _audio.Running["Discord.exe"] = 0.7f;
        _audio.Running["ts3client_win64.exe"] = 1.0f;
        _leise = new Profile { Name = "Voice leise", Steps = { Step.Percent(20), Step.Baseline } };
        _voice = new AppGroup { Name = "Voice", Apps = { "discord.exe", "ts3client_win64.exe" }, Profiles = { _leise } };
        _cfg.Groups.Add(_voice);
        _vc = new VolumeController(_audio, _store);
        _vc.SetConfig(_cfg);
    }

    private float Discord => _audio.Running["Discord.exe"];
    private float Ts3 => _audio.Running["ts3client_win64.exe"];

    private Profile AddProfile(params Step[] steps)
    {
        var p = new Profile { Name = "P" + _voice.Profiles.Count, Steps = steps.ToList() };
        _voice.Profiles.Add(p);
        _vc.SetConfig(_cfg);
        return p;
    }

    [Fact]
    public void FirstPress_RemembersBaselineAndAppliesFirstStep()
    {
        var r = _vc.OnHotkey(_leise.Id);

        Assert.NotNull(r);
        Assert.True(r.IsActive);
        Assert.Equal(Step.Percent(20), r.Step);
        Assert.Equal(0.2f, Discord, 3);
        Assert.Equal(0.2f, Ts3, 3);
        Assert.Equal((_leise.Id, 0), _vc.GetActive(_voice.Id));
        Assert.Equal(0.7f, _store.Data[_voice.Id]["Discord.exe"], 3);
    }

    [Fact]
    public void SecondPress_TogglesBackToBaseline()
    {
        _vc.OnHotkey(_leise.Id);
        var r = _vc.OnHotkey(_leise.Id);

        Assert.NotNull(r);
        Assert.False(r.IsActive);
        Assert.Equal(Step.Baseline, r.Step);
        Assert.Equal(0.7f, Discord, 3);
        Assert.Equal(1.0f, Ts3, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
        Assert.Empty(_store.Data);
    }

    [Fact]
    public void ThreeSteps_Cycle()
    {
        var p = AddProfile(Step.Percent(20), Step.Percent(50), Step.Baseline);
        _vc.OnHotkey(p.Id);
        Assert.Equal(0.2f, Discord, 3);
        _vc.OnHotkey(p.Id);
        Assert.Equal(0.5f, Discord, 3);
        _vc.OnHotkey(p.Id);
        Assert.Equal(0.7f, Discord, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
    }

    [Fact]
    public void NoBaselineStep_CyclesForever()
    {
        var p = AddProfile(Step.Percent(20), Step.Percent(100));
        var seen = new List<float>();
        for (var i = 0; i < 4; i++)
        {
            var r = _vc.OnHotkey(p.Id);
            Assert.True(r!.IsActive);
            seen.Add(Discord);
        }
        Assert.Equal(new[] { 0.2f, 1.0f, 0.2f, 1.0f }, seen);
    }

    [Fact]
    public void OtherProfileSameGroup_KeepsOriginalBaseline()
    {
        var q = AddProfile(Step.Percent(50), Step.Baseline);
        _vc.OnHotkey(_leise.Id);
        Assert.Equal(0.2f, Discord, 3);

        var r = _vc.OnHotkey(q.Id);
        Assert.Equal(0.5f, Discord, 3);
        Assert.Equal((q.Id, 0), _vc.GetActive(_voice.Id));
        Assert.True(r!.IsActive);

        _vc.OnHotkey(q.Id);
        Assert.Equal(0.7f, Discord, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
    }

    [Fact]
    public void AfterReturn_NextPressRemembersNewBaseline()
    {
        _vc.OnHotkey(_leise.Id);
        _vc.OnHotkey(_leise.Id);
        _audio.Running["Discord.exe"] = 0.4f; // manuell im Mixer geändert

        _vc.OnHotkey(_leise.Id);
        _vc.OnHotkey(_leise.Id);
        Assert.Equal(0.4f, Discord, 3);
    }

    [Fact]
    public void AppNotRunning_StillToggles()
    {
        _audio.Running.Clear();
        var r1 = _vc.OnHotkey(_leise.Id);
        Assert.NotNull(r1);
        Assert.True(r1.IsActive);
        var r2 = _vc.OnHotkey(_leise.Id);
        Assert.False(r2!.IsActive);
    }

    [Fact]
    public void ProfileWithoutSteps_ReturnsNull()
    {
        var p = AddProfile();
        Assert.Null(_vc.OnHotkey(p.Id));
        Assert.Null(_vc.OnHotkey(Guid.NewGuid()));
    }

    [Fact]
    public void SessionAddedWhileActive_GetsStepValue_AndKeepsOldBaseline()
    {
        _vc.OnHotkey(_leise.Id);
        _audio.Running.Remove("Discord.exe");     // Discord beendet
        _audio.Running["Discord.exe"] = 0.2f;     // Windows gibt beim Neustart den abgesenkten Wert zurück
        _audio.Running["Discord.exe"] = 0.9f;     // ... oder irgendeinen anderen
        _vc.OnSessionAdded("Discord.exe");
        Assert.Equal(0.2f, Discord, 3);

        _vc.OnHotkey(_leise.Id);
        Assert.Equal(0.7f, Discord, 3);
    }

    [Fact]
    public void SessionAddedWhileActive_NewApp_RemembersCurrent()
    {
        _audio.Running.Remove("ts3client_win64.exe");
        _vc.OnHotkey(_leise.Id);
        _audio.Running["ts3client_win64.exe"] = 0.8f;
        _vc.OnSessionAdded("TS3CLIENT_WIN64.EXE");
        Assert.Equal(0.2f, Ts3, 3);

        _vc.OnHotkey(_leise.Id);
        Assert.Equal(0.8f, Ts3, 3);
    }

    [Fact]
    public void SessionAdded_UnrelatedApp_Untouched()
    {
        _audio.Running["Spotify.exe"] = 0.6f;
        _vc.OnHotkey(_leise.Id);
        _vc.OnSessionAdded("Spotify.exe");
        Assert.Equal(0.6f, _audio.Running["Spotify.exe"], 3);
    }

    [Fact]
    public void SetConfig_ActiveProfileDeleted_ResetsGroup()
    {
        _vc.OnHotkey(_leise.Id);
        _voice.Profiles.Clear();
        _vc.SetConfig(_cfg);
        Assert.Equal(0.7f, Discord, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
    }

    [Fact]
    public void SetConfig_ActiveProfileStepsChanged_ResetsGroup()
    {
        _vc.OnHotkey(_leise.Id);
        _leise.Steps[0] = Step.Percent(30);
        _vc.SetConfig(_cfg);
        Assert.Equal(0.7f, Discord, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
    }

    [Fact]
    public void SetConfig_GroupDeleted_ResetsGroup()
    {
        _vc.OnHotkey(_leise.Id);
        _cfg.Groups.Clear();
        _vc.SetConfig(_cfg);
        Assert.Equal(0.7f, Discord, 3);
        Assert.Empty(_store.Data);
    }

    [Fact]
    public void SetConfig_OnlyRenamed_StaysActive()
    {
        _vc.OnHotkey(_leise.Id);
        _leise.Name = "Anders";
        _voice.Name = "Sprache";
        _vc.SetConfig(_cfg);
        Assert.Equal(0.2f, Discord, 3);
        Assert.NotNull(_vc.GetActive(_voice.Id));
    }

    [Fact]
    public void RestorePersistedBaselines_AppliesAndClears()
    {
        _audio.Running["Discord.exe"] = 0.2f; // Tool ist abgestürzt, Discord steht noch auf 20 %
        _store.Data = new() { [_voice.Id] = new() { ["Discord.exe"] = 0.7f, ["ts3client_win64.exe"] = 0.9f } };
        _audio.Running.Remove("ts3client_win64.exe");

        var vc = new VolumeController(_audio, _store);
        vc.SetConfig(_cfg);
        vc.RestorePersistedBaselines();

        Assert.Equal(0.7f, Discord, 3);
        Assert.Equal(0.9f, _store.Data[_voice.Id]["ts3client_win64.exe"], 3); // noch ausstehend

        _audio.Running["ts3client_win64.exe"] = 0.2f;
        vc.OnSessionAdded("ts3client_win64.exe");
        Assert.Equal(0.9f, Ts3, 3);
        Assert.Empty(_store.Data);
    }

    [Fact]
    public void ResetAll_RestoresEverything()
    {
        _vc.OnHotkey(_leise.Id);
        _vc.ResetAll();
        Assert.Equal(0.7f, Discord, 3);
        Assert.Equal(1.0f, Ts3, 3);
        Assert.Null(_vc.GetActive(_voice.Id));
    }

    [Fact]
    public void OverlayAndSoundFlags()
    {
        _leise.OverlayOnActivate = false;
        _leise.OverlayOnDeactivate = true;
        _leise.Sound = true;
        var on = _vc.OnHotkey(_leise.Id)!;
        var off = _vc.OnHotkey(_leise.Id)!;
        Assert.False(on.ShowOverlay);
        Assert.True(off.ShowOverlay);
        Assert.True(on.PlaySound);
    }

    [Fact]
    public void StateChanged_Raised()
    {
        var n = 0;
        _vc.StateChanged += () => n++;
        _vc.OnHotkey(_leise.Id);
        Assert.True(n > 0);
    }
}
