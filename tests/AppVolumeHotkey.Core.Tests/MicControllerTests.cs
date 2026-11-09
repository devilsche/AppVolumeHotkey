using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Persistence;

namespace AppVolumeHotkey.Core.Tests;

internal sealed class FakeMic : IMicBackend
{
    public bool DeviceMuted { get; set; }
    public Dictionary<string, bool> AppMuted { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool? IsDeviceMuted(MicScope scope) => DeviceMuted;
    public void SetDeviceMuted(MicScope scope, bool muted) => DeviceMuted = muted;
    public void SetAppsMuted(IEnumerable<string> exes, bool muted) { foreach (var e in exes) AppMuted[e] = muted; }
}

public sealed class MicControllerTests
{
    private readonly FakeMic _mic = new();
    private readonly AppGroup _meet = new() { Name = "Meet", Apps = { "msedge.exe" } };
    private readonly AppConfig _cfg = new();
    private readonly MicController _c;
    private readonly MicRule _all = new() { Mode = MicMode.Toggle, Scope = MicScope.AllMicrophones, Hotkey = new Hotkey(HotkeyModifiers.None, 0x7D) };

    public MicControllerTests()
    {
        _cfg.Groups.Add(_meet);
        _cfg.Mic.Rules.Add(_all);
        _c = new MicController(_mic);
        _c.SetConfig(_cfg);
    }

    private MicRule AddGroupRule(MicMode mode)
    {
        var r = new MicRule { Mode = mode, Scope = MicScope.Group, GroupId = _meet.Id, ShowOverlay = false };
        _cfg.Mic.Rules.Add(r);
        _c.SetConfig(_cfg);
        return r;
    }

    [Fact]
    public void DeviceToggle_PressSwitches_ReleaseIgnored()
    {
        Assert.True(_c.OnPressed(_all.Id)!.Muted);
        Assert.Null(_c.OnReleased(_all.Id));
        Assert.True(_mic.DeviceMuted);
        Assert.False(_c.OnPressed(_all.Id)!.Muted);
        Assert.False(_mic.DeviceMuted);
    }

    [Fact]
    public void DeviceHoldToMute_RestoresPrevious()
    {
        _all.Mode = MicMode.HoldToMute;
        _c.SetConfig(_cfg);
        _c.OnPressed(_all.Id);
        Assert.True(_mic.DeviceMuted);
        _c.OnReleased(_all.Id);
        Assert.False(_mic.DeviceMuted);

        _mic.DeviceMuted = true;
        _c.OnPressed(_all.Id);
        _c.OnReleased(_all.Id);
        Assert.True(_mic.DeviceMuted);
    }

    [Fact]
    public void DevicePushToTalk_MutedByDefault()
    {
        _all.Mode = MicMode.PushToTalk;
        _c.SetConfig(_cfg);
        Assert.True(_mic.DeviceMuted);
        Assert.False(_c.OnPressed(_all.Id)!.Muted);
        Assert.True(_c.OnReleased(_all.Id)!.Muted);
    }

    [Fact]
    public void LeavingPushToTalk_Unmutes()
    {
        _all.Mode = MicMode.PushToTalk;
        _c.SetConfig(_cfg);
        _all.Mode = MicMode.Toggle;
        _c.SetConfig(_cfg);
        Assert.False(_mic.DeviceMuted);
    }

    [Fact]
    public void GroupPushToTalk_OnlyMutesGroupApps()
    {
        var ptt = AddGroupRule(MicMode.PushToTalk);
        Assert.True(_mic.AppMuted["msedge.exe"]);
        Assert.False(_mic.DeviceMuted);
        Assert.True(_c.IsMuted(ptt));

        var down = _c.OnPressed(ptt.Id)!;
        Assert.False(down.Muted);
        Assert.False(_mic.AppMuted["msedge.exe"]);
        Assert.True(_c.IsHeld(ptt));

        _c.OnReleased(ptt.Id);
        Assert.True(_mic.AppMuted["msedge.exe"]);
        Assert.False(_c.IsHeld(ptt));
    }

    [Fact]
    public void GroupToggle_Independent_FromDeviceToggle()
    {
        var g = AddGroupRule(MicMode.Toggle);
        _c.OnPressed(g.Id);
        Assert.True(_mic.AppMuted["msedge.exe"]);
        Assert.False(_mic.DeviceMuted);
        _c.OnPressed(_all.Id);
        Assert.True(_mic.DeviceMuted);
        Assert.True(_mic.AppMuted["msedge.exe"]);
    }

    [Fact]
    public void EnforceApps_ReappliesDesiredState_ForNewSessions()
    {
        AddGroupRule(MicMode.PushToTalk);
        _mic.AppMuted.Clear(); // Edge startet die Aufnahme neu (neue Session)
        _c.EnforceApps();
        Assert.True(_mic.AppMuted["msedge.exe"]);
    }

    [Fact]
    public void RemovingGroupRule_UnmutesApps()
    {
        var g = AddGroupRule(MicMode.PushToTalk);
        _cfg.Mic.Rules.Remove(g);
        _c.SetConfig(_cfg);
        Assert.False(_mic.AppMuted["msedge.exe"]);
    }

    [Fact]
    public void OnExit_UnmutesEverything_IfConfigured()
    {
        AddGroupRule(MicMode.PushToTalk);
        _c.OnPressed(_all.Id);
        _c.OnExit();
        Assert.False(_mic.DeviceMuted);
        Assert.False(_mic.AppMuted["msedge.exe"]);
    }

    [Fact]
    public void OnExit_KeepsMuted_IfNotConfigured()
    {
        _cfg.Mic.UnmuteOnExit = false;
        _c.SetConfig(_cfg);
        _c.OnPressed(_all.Id);
        _c.OnExit();
        Assert.True(_mic.DeviceMuted);
    }

    [Fact]
    public void SetMuted_FromUi_RaisesStateChanged()
    {
        var n = 0;
        _c.StateChanged += () => n++;
        _c.SetMuted(_all, true);
        Assert.True(_mic.DeviceMuted);
        Assert.True(n > 0);
    }

    [Fact]
    public void ResultFlags_FollowRule()
    {
        _all.ShowOverlay = false;
        _all.PlaySound = true;
        var r = _c.OnPressed(_all.Id)!;
        Assert.False(r.ShowOverlay);
        Assert.True(r.PlaySound);
        Assert.Same(_all, r.Rule);
    }

    [Fact]
    public void UnknownRule_ReturnsNull() => Assert.Null(_c.OnPressed(Guid.NewGuid()));

    [Fact]
    public void LegacySingleHotkey_IsMigratedToRule()
    {
        var dir = Path.Combine(Path.GetTempPath(), "avh-mic-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"Mic\":{\"Hotkey\":{\"Modifiers\":\"None\",\"VirtualKey\":125},\"Mode\":\"PushToTalk\",\"Scope\":\"DefaultMicrophone\",\"ShowOverlay\":false}}");
            var cfg = ConfigStore.Load(path);
            var rule = Assert.Single(cfg.Mic.Rules);
            Assert.Equal(new Hotkey(HotkeyModifiers.None, 125), rule.Hotkey);
            Assert.Equal(MicMode.PushToTalk, rule.Mode);
            Assert.Equal(MicScope.DefaultMicrophone, rule.Scope);
            Assert.False(rule.ShowOverlay);
            Assert.Null(cfg.Mic.Hotkey);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
