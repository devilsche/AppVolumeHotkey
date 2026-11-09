using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Audio;

/// <summary>Pegel und laufende Apps für das Ducking.</summary>
public interface IPeakMeter
{
    /// <summary>Aktueller Spitzenpegel (0..1) der App, null wenn sie nicht läuft.</summary>
    float? GetPeak(string exe);

    /// <summary>Alle Apps, die gerade eine Audio-Session haben (ohne AppVolumeHotkey selbst).</summary>
    IReadOnlyCollection<string> RunningExes();
}

/// <summary>
/// Ducking: Gibt die Quell-Gruppe einer Regel Ton aus, werden die Ziel-Apps auf einen Anteil ihrer Lautstärke gesenkt;
/// nach einer Weile Stille wieder hochgesetzt. Hat der Nutzer die Lautstärke inzwischen selbst geändert, bleibt sie.
/// <see cref="Tick"/> regelmäßig (z. B. alle 50 ms) aufrufen.
/// </summary>
public sealed class DuckingController(IAudioBackend audio, IPeakMeter meter, Func<DateTime>? clock = null)
{
    private sealed class RuleState
    {
        public bool Ducked;
        public DateTime LastLoud;
        public readonly Dictionary<string, (float Original, float Applied)> Apps = new(StringComparer.OrdinalIgnoreCase);
    }

    private const float Tolerance = 0.02f;
    private readonly Func<DateTime> _now = clock ?? (() => DateTime.UtcNow);
    private readonly Dictionary<Guid, RuleState> _states = new();
    private AppConfig _config = new();

    public event Action? StateChanged;

    public bool Enabled => _config.Ducking.Enabled;

    public bool IsDucking => _states.Values.Any(s => s.Ducked);

    public void SetConfig(AppConfig config)
    {
        _config = config;
        // Zustände entfernter/geänderter Regeln aufräumen
        var ids = config.Ducking.Rules.Select(r => r.Id).ToHashSet();
        foreach (var id in _states.Keys.Where(id => !ids.Contains(id)).ToList())
            Restore(id);
        if (!config.Ducking.Enabled)
            RestoreAll();
        StateChanged?.Invoke();
    }

    /// <summary>Ducking an/aus (z. B. per Hotkey). Gibt den neuen Zustand zurück.</summary>
    public bool Toggle()
    {
        _config.Ducking.Enabled = !_config.Ducking.Enabled;
        if (!_config.Ducking.Enabled)
            RestoreAll();
        StateChanged?.Invoke();
        return _config.Ducking.Enabled;
    }

    /// <summary>Lautester Pegel der Quell-Gruppe einer Regel (für die Live-Anzeige).</summary>
    public float SourcePeak(DuckingRule rule)
    {
        var group = _config.Groups.FirstOrDefault(g => g.Id == rule.SourceGroupId);
        return group is null ? 0f : group.Apps.Select(exe => meter.GetPeak(exe) ?? 0f).DefaultIfEmpty(0f).Max();
    }

    public void Tick()
    {
        if (!_config.Ducking.Enabled)
            return;
        var now = _now();
        var changed = false;

        foreach (var rule in _config.Ducking.Rules)
        {
            var source = _config.Groups.FirstOrDefault(g => g.Id == rule.SourceGroupId);
            if (source is null)
                continue;
            if (!_states.TryGetValue(rule.Id, out var state))
                _states[rule.Id] = state = new RuleState();

            var loud = SourcePeak(rule) >= rule.Threshold;
            if (loud)
            {
                state.LastLoud = now;
                if (!state.Ducked)
                {
                    state.Ducked = true;
                    changed = true;
                }
                // Ziel-Apps senken (auch solche, die erst während des Duckings starten)
                foreach (var exe in Targets(rule, source))
                {
                    if (state.Apps.ContainsKey(exe) || audio.GetVolume(exe) is not { } current)
                        continue;
                    var applied = current * Math.Clamp(rule.Percent, 0, 100) / 100f;
                    audio.SetVolume(exe, applied);
                    state.Apps[exe] = (current, applied);
                }
            }
            else if (state.Ducked && (now - state.LastLoud).TotalMilliseconds >= rule.ReleaseMs)
            {
                Restore(rule.Id);
                changed = true;
            }
        }

        if (changed)
            StateChanged?.Invoke();
    }

    public void RestoreAll()
    {
        var any = _states.Values.Any(s => s.Ducked);
        foreach (var id in _states.Keys.ToList())
            Restore(id);
        if (any)
            StateChanged?.Invoke();
    }

    private void Restore(Guid ruleId)
    {
        if (!_states.Remove(ruleId, out var state))
            return;
        foreach (var (exe, (original, applied)) in state.Apps)
        {
            // Nur zurücksetzen, wenn der Nutzer die Lautstärke nicht inzwischen selbst geändert hat
            if (audio.GetVolume(exe) is { } now && Math.Abs(now - applied) <= Tolerance)
                audio.SetVolume(exe, original);
        }
    }

    private IEnumerable<string> Targets(DuckingRule rule, AppGroup source)
    {
        var sourceApps = new HashSet<string>(source.Apps, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> candidates = rule.TargetAllOthers
            ? meter.RunningExes()
            : _config.Groups.Where(g => rule.TargetGroupIds.Contains(g.Id)).SelectMany(g => g.Apps);
        return candidates.Where(e => !sourceApps.Contains(e)).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
