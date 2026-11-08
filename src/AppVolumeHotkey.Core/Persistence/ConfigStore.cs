using System.Text.Json;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Persistence;

public static class ConfigStore
{
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            return new AppConfig();

        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Json.Options);
            return Normalize(cfg ?? new AppConfig());
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException)
        {
            var backup = $"{path}.broken-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(path, backup, overwrite: true);
            return new AppConfig();
        }
    }

    public static void Save(string path, AppConfig cfg) =>
        Json.WriteAtomic(path, JsonSerializer.Serialize(cfg, Json.Options));

    private static AppConfig Normalize(AppConfig cfg)
    {
        cfg.Groups ??= new();
        cfg.Settings ??= new();
        foreach (var g in cfg.Groups)
        {
            g.Apps ??= new();
            g.Profiles ??= new();
            foreach (var p in g.Profiles)
                p.Steps ??= new();
        }
        return cfg;
    }
}
