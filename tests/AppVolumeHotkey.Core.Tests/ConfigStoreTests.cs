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
        cfg.Settings.OverlayStyle = OverlayStyle.Normal;
        cfg.Settings.MinimizeToTray = false;
        cfg.Settings.StatusIndicator = false;
        cfg.Settings.StatusCorner = OverlayCorner.BottomRight;
        cfg.Settings.StatusStyle = StatusStyle.Icon;
        cfg.Settings.StatusOpacity = 60;
        cfg.Settings.TrayBadge = false;
        cfg.Settings.MainWindow = new WindowPlacement(100, 50, 1300, 800, true) { GroupsWidth = 260, RunningWidth = 340 };

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
        Assert.Equal(OverlayStyle.Normal, loaded.Settings.OverlayStyle);
        Assert.Equal(OverlayStyle.Compact, new AppSettings().OverlayStyle);
        Assert.Equal(new WindowPlacement(100, 50, 1300, 800, true) { GroupsWidth = 260, RunningWidth = 340 }, loaded.Settings.MainWindow);
        Assert.Null(new AppSettings().MainWindow);
        Assert.True(new AppSettings().MinimizeToTray);
        Assert.False(loaded.Settings.MinimizeToTray);
        Assert.False(loaded.Settings.StatusIndicator);
        Assert.Equal(OverlayCorner.BottomRight, loaded.Settings.StatusCorner);
        Assert.Equal(StatusStyle.Icon, loaded.Settings.StatusStyle);
        Assert.Equal(60, loaded.Settings.StatusOpacity);
        Assert.False(loaded.Settings.TrayBadge);
        var d = new AppSettings();
        Assert.True(d.StatusIndicator);
        Assert.Equal(OverlayCorner.TopLeft, d.StatusCorner);
        Assert.Equal(StatusStyle.MiniText, d.StatusStyle);
        Assert.Equal(85, d.StatusOpacity);
        Assert.True(d.TrayBadge);
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

    [Theory]
    [InlineData("{\"Groups\":[null]}")]
    [InlineData("{\"Groups\":[{\"Name\":\"V\",\"Apps\":[null,\"a.exe\"],\"Profiles\":[null]}]}")]
    [InlineData("{\"Groups\":[{\"Name\":\"V\",\"Profiles\":[{\"Name\":\"P\",\"Steps\":[null,{\"Kind\":\"Percent\",\"Value\":20}]}]}]}")]
    [InlineData("{\"Settings\":null,\"Groups\":null}")]
    public void Load_NullEntries_AreDropped(string json)
    {
        File.WriteAllText(ConfigPath, json);
        var cfg = ConfigStore.Load(ConfigPath);
        Assert.NotNull(cfg.Settings);
        Assert.All(cfg.Groups, g =>
        {
            Assert.NotNull(g);
            Assert.All(g.Apps, Assert.NotNull);
            Assert.All(g.Profiles, p => Assert.All(p.Steps, Assert.NotNull));
        });
    }

    [Fact]
    public void Load_UnreadablePath_ReturnsDefaults()
    {
        Directory.CreateDirectory(ConfigPath); // Pfad ist ein Ordner -> IOException/UnauthorizedAccess beim Lesen
        var cfg = ConfigStore.Load(ConfigPath);
        Assert.Empty(cfg.Groups);
    }

    [Fact]
    public void BaselineStore_SaveToUnwritablePath_DoesNotThrow()
    {
        var path = Path.Combine(_dir, "state.json");
        Directory.CreateDirectory(path);
        var store = new JsonBaselineStore(path);
        store.Save(new() { [Guid.NewGuid()] = new() { ["a.exe"] = 0.5f } });
        store.Save(new());
    }
}
