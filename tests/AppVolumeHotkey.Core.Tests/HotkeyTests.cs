using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Persistence;

namespace AppVolumeHotkey.Core.Tests;

public sealed class HotkeyTests
{
    [Theory]
    [InlineData(MouseButtons.Middle, true)]
    [InlineData(MouseButtons.X1, true)]
    [InlineData(MouseButtons.X2, true)]
    [InlineData(0x78u, false)] // F9
    [InlineData(0x01u, false)] // linke Maustaste ist nicht erlaubt
    [InlineData(0x02u, false)] // rechte Maustaste ist nicht erlaubt
    public void IsMouse(uint vk, bool expected) =>
        Assert.Equal(expected, new Hotkey(HotkeyModifiers.None, vk).IsMouse);

    [Fact]
    public void Match_RequiresExactModifiers()
    {
        var list = new[]
        {
            (1, new Hotkey(HotkeyModifiers.None, MouseButtons.X1)),
            (2, new Hotkey(HotkeyModifiers.Shift, MouseButtons.X1)),
            (3, new Hotkey(HotkeyModifiers.None, MouseButtons.X2)),
        };
        Assert.Equal(1, HotkeyMatch.Find(list, HotkeyModifiers.None, MouseButtons.X1));
        Assert.Equal(2, HotkeyMatch.Find(list, HotkeyModifiers.Shift, MouseButtons.X1));
        Assert.Null(HotkeyMatch.Find(list, HotkeyModifiers.Control, MouseButtons.X1));
        Assert.Null(HotkeyMatch.Find(list, HotkeyModifiers.None, MouseButtons.Middle));
    }

    [Fact]
    public void PassThrough_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "avh-" + Guid.NewGuid());
        var path = Path.Combine(dir, "config.json");
        try
        {
            var p = new Profile { Hotkey = new Hotkey(HotkeyModifiers.None, MouseButtons.X2), PassThrough = true, Steps = { Step.Baseline } };
            ConfigStore.Save(path, new AppConfig { Groups = { new AppGroup { Profiles = { p } } } });
            var loaded = ConfigStore.Load(path).Groups[0].Profiles[0];
            Assert.True(loaded.PassThrough);
            Assert.True(loaded.Hotkey!.IsMouse);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SoundVariant_RoundTripsAndClones()
    {
        var dir = Path.Combine(Path.GetTempPath(), "avh-" + Guid.NewGuid());
        var path = Path.Combine(dir, "config.json");
        try
        {
            var p = new Profile { Sound = true, SoundVariant = SoundVariant.Bell, Steps = { Step.Baseline } };
            Assert.Equal(SoundVariant.Bell, p.Clone().SoundVariant);
            ConfigStore.Save(path, new AppConfig { Groups = { new AppGroup { Profiles = { p } } } });
            Assert.Equal(SoundVariant.Bell, ConfigStore.Load(path).Groups[0].Profiles[0].SoundVariant);
            Assert.Equal(SoundVariant.TwoTones, new Profile().SoundVariant);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
