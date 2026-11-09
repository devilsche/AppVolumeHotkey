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
            try
            {
                var backup = $"{path}.broken-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(path, backup, overwrite: true);
            }
            catch (Exception io) when (io is IOException or UnauthorizedAccessException) { }
            return new AppConfig();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new AppConfig(); // Datei gerade gesperrt o. Ä. – nicht überschreiben, nur Defaults verwenden
        }
    }

    /// <summary>Alles außer Fensterposition als Datei speichern (Backup / anderer PC).</summary>
    public static void Export(string path, AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, Json.Options);
        var copy = JsonSerializer.Deserialize<AppConfig>(json, Json.Options)!;
        copy.Settings.MainWindow = null;
        Json.WriteAtomic(path, JsonSerializer.Serialize(copy, Json.Options));
    }

    /// <summary>Exportierte Datei lesen. Die Datei wird nie verändert; bei Fehlern false und eine Meldung.</summary>
    public static bool TryImport(string path, out AppConfig? config, out string? error)
    {
        config = null;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !(doc.RootElement.TryGetProperty("Groups", out _) || doc.RootElement.TryGetProperty("Settings", out _)))
            {
                error = "Keine AppVolumeHotkey-Einstellungen.";
                return false;
            }
            var cfg = JsonSerializer.Deserialize<AppConfig>(doc.RootElement.GetRawText(), Json.Options);
            if (cfg is null)
            {
                error = "Leere Datei.";
                return false;
            }
            config = Normalize(cfg);
            config.Settings.MainWindow = null;
            return true;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            error = e.Message;
            return false;
        }
    }

    public static void Save(string path, AppConfig cfg) =>
        Json.WriteAtomic(path, JsonSerializer.Serialize(cfg, Json.Options));

    private static AppConfig Normalize(AppConfig cfg)
    {
        cfg.Groups = (cfg.Groups ?? new()).Where(g => g is not null).ToList();
        cfg.Settings ??= new();
        cfg.Mic ??= new();
        cfg.Ducking ??= new();
        cfg.Ducking.Rules = (cfg.Ducking.Rules ?? new()).Where(r => r is not null).ToList();
        foreach (var r in cfg.Ducking.Rules)
            r.TargetGroupIds ??= new();
        foreach (var g in cfg.Groups)
        {
            g.Name ??= "Gruppe";
            g.Apps = (g.Apps ?? new()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            g.Profiles = (g.Profiles ?? new()).Where(p => p is not null).ToList();
            foreach (var p in g.Profiles)
            {
                p.Name ??= "Profil";
                p.Steps = (p.Steps ?? new()).Where(s => s is not null).ToList();
            }
        }
        return cfg;
    }
}
