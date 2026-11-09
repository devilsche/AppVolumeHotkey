using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Tests;

internal sealed class FakeMic : IMicBackend
{
    public bool Muted { get; set; }
    public int Sets { get; private set; }
    public bool? IsMuted() => Muted;
    public void SetMuted(bool muted) { Muted = muted; Sets++; }
}

public sealed class MicControllerTests
{
    private readonly FakeMic _mic = new();

    private MicController Create(MicMode mode)
    {
        var c = new MicController(_mic);
        c.SetSettings(new MicSettings { Mode = mode, Hotkey = new Hotkey(HotkeyModifiers.None, 0x7C) });
        return c;
    }

    [Fact]
    public void Toggle_PressSwitches_ReleaseIgnored()
    {
        var c = Create(MicMode.Toggle);
        Assert.True(c.OnPressed()!.Muted);
        Assert.Null(c.OnReleased());
        Assert.True(_mic.Muted);
        Assert.False(c.OnPressed()!.Muted);
        Assert.False(_mic.Muted);
    }

    [Fact]
    public void HoldToMute_MutesWhileHeld_RestoresPrevious()
    {
        var c = Create(MicMode.HoldToMute);
        c.OnPressed();
        Assert.True(_mic.Muted);
        Assert.False(c.OnReleased()!.Muted);
        Assert.False(_mic.Muted);

        _mic.Muted = true; // war schon stumm → bleibt stumm
        c.OnPressed();
        c.OnReleased();
        Assert.True(_mic.Muted);
    }

    [Fact]
    public void PushToTalk_MutedByDefault_OnWhileHeld()
    {
        var c = Create(MicMode.PushToTalk);
        Assert.True(_mic.Muted); // Wechsel in PTT schaltet stumm
        Assert.False(c.OnPressed()!.Muted);
        Assert.True(c.OnReleased()!.Muted);
    }

    [Fact]
    public void LeavingPushToTalk_Unmutes()
    {
        var c = Create(MicMode.PushToTalk);
        c.SetSettings(new MicSettings { Mode = MicMode.Toggle });
        Assert.False(_mic.Muted);
    }

    [Fact]
    public void OnExit_UnmutesIfConfigured()
    {
        var c = Create(MicMode.Toggle);
        c.OnPressed();
        c.OnExit();
        Assert.False(_mic.Muted);

        c.SetSettings(new MicSettings { UnmuteOnExit = false });
        c.OnPressed();
        c.OnExit();
        Assert.True(_mic.Muted);
    }

    [Fact]
    public void SetMuted_FromUi_RaisesStateChanged()
    {
        var c = Create(MicMode.Toggle);
        var n = 0;
        c.StateChanged += () => n++;
        c.SetMuted(true);
        Assert.True(_mic.Muted);
        Assert.True(n > 0);
    }

    [Fact]
    public void ResultFlags_FollowSettings()
    {
        var c = new MicController(_mic);
        c.SetSettings(new MicSettings { ShowOverlay = false, PlaySound = true });
        var r = c.OnPressed()!;
        Assert.False(r.ShowOverlay);
        Assert.True(r.PlaySound);
    }
}
