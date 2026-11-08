using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Persistence;

namespace AppVolumeHotkey.Core.Tests;

internal sealed class FakeAudioBackend : IAudioBackend
{
    public Dictionary<string, float> Running { get; } = new(StringComparer.OrdinalIgnoreCase);

    public float? GetVolume(string exe) => Running.TryGetValue(exe, out var v) ? v : null;

    public void SetVolume(string exe, float volume01)
    {
        if (Running.ContainsKey(exe))
            Running[exe] = volume01;
    }
}

internal sealed class InMemoryBaselineStore : IBaselineStore
{
    public Dictionary<Guid, Dictionary<string, float>> Data { get; set; } = new();

    public Dictionary<Guid, Dictionary<string, float>> Load() =>
        Data.ToDictionary(kv => kv.Key, kv => new Dictionary<string, float>(kv.Value, StringComparer.OrdinalIgnoreCase));

    public void Save(Dictionary<Guid, Dictionary<string, float>> baselines) =>
        Data = baselines.ToDictionary(kv => kv.Key, kv => new Dictionary<string, float>(kv.Value, StringComparer.OrdinalIgnoreCase));
}
