using System.Text.Json;

namespace AppVolumeHotkey.Core.Persistence;

/// <summary>Gemerkte Ausgangswerte: Gruppe → (exe → Lautstärke 0..1).</summary>
public interface IBaselineStore
{
    Dictionary<Guid, Dictionary<string, float>> Load();
    void Save(Dictionary<Guid, Dictionary<string, float>> baselines);
}

public sealed class JsonBaselineStore(string path) : IBaselineStore
{
    public Dictionary<Guid, Dictionary<string, float>> Load()
    {
        try
        {
            if (!File.Exists(path))
                return new();
            var raw = JsonSerializer.Deserialize<Dictionary<Guid, Dictionary<string, float>>>(File.ReadAllText(path), Json.Options);
            return raw?.ToDictionary(kv => kv.Key, kv => new Dictionary<string, float>(kv.Value, StringComparer.OrdinalIgnoreCase)) ?? new();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return new();
        }
    }

    public void Save(Dictionary<Guid, Dictionary<string, float>> baselines)
    {
        if (baselines.Count == 0)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }
        Json.WriteAtomic(path, JsonSerializer.Serialize(baselines, Json.Options));
    }
}
