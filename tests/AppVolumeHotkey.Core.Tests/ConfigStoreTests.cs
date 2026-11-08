using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Persistence;

namespace AppVolumeHotkey.Core.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avh-tests-" + Guid.NewGuid());
    private string ConfigPath => Path.Combine(_dir, "config.json");

    public ConfigStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var cfg = ConfigStore.Load(ConfigPath);
        Assert.Empty(cfg.Groups);
        Assert.Equal(200, cfg.Settings.FadeDurationMs);
        Assert.Equal(1500, cfg.Settings.OverlayDurationMs);
        Assert.True(cfg.Settings.StartMinimized);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var profile = new Profile
        {
            Name = "Voice leise",
            Hotkey = new Hotkey(HotkeyModifiers.Shift, 0x78),
            Steps = { Step.Percent(20), Step.Baseline },
            Sound = true,
        };
        var group = new AppGroup { Name = "Voice", Apps = { "Discord.exe", "ts3client_win64.exe" }, Profiles = { profile } };
        var cfg = new AppConfig { Groups = { group } };
        cfg.Settings.OverlayCorner = OverlayCorner.BottomLeft;

        ConfigStore.Save(ConfigPath, cfg);
        var loaded = ConfigStore.Load(ConfigPath);

        var g = Assert.Single(loaded.Groups);
        Assert.Equal(group.Id, g.Id);
        Assert.Equal(new[] { "Discord.exe", "ts3client_win64.exe" }, g.Apps);
        var p = Assert.Single(g.Profiles);
        Assert.Equal(profile.Id, p.Id);
        Assert.Equal(new Hotkey(HotkeyModifiers.Shift, 0x78), p.Hotkey);
        Assert.Equal(new[] { Step.Percent(20), Step.Baseline }, p.Steps);
        Assert.True(p.Sound);
        Assert.Equal(OverlayCorner.BottomLeft, loaded.Settings.OverlayCorner);
    }

    [Fact]
    public void Load_CorruptFile_BacksUpAndReturnsDefaults()
    {
        File.WriteAllText(ConfigPath, "{nicht json");
        var cfg = ConfigStore.Load(ConfigPath);
        Assert.Empty(cfg.Groups);
        Assert.Single(Directory.GetFiles(_dir, "config.json.broken-*"));
    }

    [Fact]
    public void StepLabels()
    {
        Assert.Equal("20 %", Step.Percent(20).Label);
        Assert.Equal("Ausgangswert", Step.Baseline.Label);
    }
}
