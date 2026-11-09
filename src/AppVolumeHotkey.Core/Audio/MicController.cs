using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Audio;

/// <summary>Mikrofon stummschalten – als ganzes Gerät oder nur für die Aufnahme bestimmter Apps.</summary>
public interface IMicBackend
{
    /// <summary>true = stumm, null = kein Mikrofon gefunden.</summary>
    bool? IsDeviceMuted(MicScope scope);

    void SetDeviceMuted(MicScope scope, bool muted);

    /// <summary>Aufnahme-Sessions dieser Apps stumm/an (wirkt nur, solange sie aufnehmen).</summary>
    void SetAppsMuted(IEnumerable<string> exes, bool muted);
}

public sealed record MicResult(MicRule Rule, bool Muted, bool ShowOverlay, bool PlaySound, SoundVariant Sound);

/// <summary>
/// Logik für beliebig viele Mikrofon-Hotkeys: Umschalten, Halten zum Stummschalten oder Push-to-Talk,
/// jeweils für alle Mikrofone, das Standard-Mikrofon oder nur die Apps einer Gruppe.
/// </summary>
public sealed class MicController(IMicBackend mic)
{
    private AppConfig _config = new();
    private readonly Dictionary<Guid, MicMode> _knownModes = new();
    private readonly Dictionary<Guid, bool> _groupMuted = new();     // gewünschter Zustand je Gruppen-Regel
    private readonly Dictionary<Guid, bool> _mutedBeforeHold = new();
    private readonly HashSet<Guid> _held = new();

    public event Action? StateChanged;

    public IReadOnlyList<MicRule> Rules => _config.Mic.Rules;

    public void SetConfig(AppConfig config)
    {
        _config = config;
        var rules = config.Mic.Rules;

        // Entfernte Gruppen-Regeln: deren Apps wieder einschalten
        foreach (var id in _groupMuted.Keys.Where(id => rules.All(r => r.Id != id || r.Scope != MicScope.Group)).ToList())
        {
            if (_groupMuted[id])
                ReleaseGroupRuleApps(id);
            _groupMuted.Remove(id);
        }
        foreach (var id in _knownModes.Keys.Where(id => rules.All(r => r.Id != id)).ToList())
            _knownModes.Remove(id);

        foreach (var rule in rules)
        {
            var wasPtt = _knownModes.TryGetValue(rule.Id, out var oldMode) && oldMode == MicMode.PushToTalk;
            var isPtt = rule.Mode == MicMode.PushToTalk;
            if (isPtt && !wasPtt)
                Set(rule, true);   // Push-to-Talk: standardmäßig stumm
            else if (wasPtt && !isPtt)
                Set(rule, false);  // Push-to-Talk verlassen: wieder an
            _knownModes[rule.Id] = rule.Mode;
        }
        EnforceApps();
        StateChanged?.Invoke();
    }

    private readonly Dictionary<Guid, List<string>> _lastAppliedApps = new();

    public bool? IsMuted(MicRule rule) => rule.Scope == MicScope.Group
        ? _groupMuted.TryGetValue(rule.Id, out var m) && m
        : mic.IsDeviceMuted(rule.Scope);

    /// <summary>Push-to-Talk/Halten: wird die Taste gerade gehalten?</summary>
    public bool IsHeld(MicRule rule) => _held.Contains(rule.Id);

    /// <summary>Ist irgendein Mikrofon (Gerät oder Gruppe) gerade stumm geschaltet?</summary>
    public bool AnyMuted => _config.Mic.Rules.Any(r => IsMuted(r) == true);

    public MicResult? OnPressed(Guid ruleId)
    {
        var rule = Find(ruleId);
        if (rule is null || _held.Contains(rule.Id))
            return null;
        switch (rule.Mode)
        {
            case MicMode.Toggle:
                return Result(rule, Set(rule, !(IsMuted(rule) ?? false)));
            case MicMode.HoldToMute:
                _held.Add(rule.Id);
                _mutedBeforeHold[rule.Id] = IsMuted(rule) ?? false;
                return Result(rule, Set(rule, true));
            default: // PushToTalk
                _held.Add(rule.Id);
                return Result(rule, Set(rule, false));
        }
    }

    public MicResult? OnReleased(Guid ruleId)
    {
        var rule = Find(ruleId);
        if (rule is null || !_held.Remove(rule.Id))
            return null;
        return rule.Mode switch
        {
            MicMode.HoldToMute => Result(rule, Set(rule, _mutedBeforeHold.GetValueOrDefault(rule.Id))),
            MicMode.PushToTalk => Result(rule, Set(rule, true)),
            _ => null,
        };
    }

    /// <summary>Direkt aus der Oberfläche oder dem Infobereich-Menü.</summary>
    public void SetMuted(MicRule rule, bool muted) => Set(rule, muted);

    /// <summary>Gewünschten Zustand der Gruppen-Regeln erneut anwenden (neue Aufnahme-Sessions, geänderte Gruppen).</summary>
    public void EnforceApps()
    {
        foreach (var rule in _config.Mic.Rules.Where(r => r.Scope == MicScope.Group))
        {
            var apps = GroupApps(rule);
            // Apps, die aus der Gruppe entfernt wurden, freigeben
            if (_lastAppliedApps.TryGetValue(rule.Id, out var before))
            {
                var removed = before.Where(a => !apps.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
                if (removed.Count > 0)
                    mic.SetAppsMuted(removed, false);
            }
            _lastAppliedApps[rule.Id] = apps;
            if (_groupMuted.TryGetValue(rule.Id, out var muted) && muted)
                mic.SetAppsMuted(apps, true);
        }
    }

    public void OnExit()
    {
        if (!_config.Mic.UnmuteOnExit)
            return;
        foreach (var rule in _config.Mic.Rules)
        {
            if (rule.Scope == MicScope.Group)
                mic.SetAppsMuted(GroupApps(rule), false);
            else if (mic.IsDeviceMuted(rule.Scope) == true)
                mic.SetDeviceMuted(rule.Scope, false);
        }
    }

    private MicRule? Find(Guid id) => _config.Mic.Rules.FirstOrDefault(r => r.Id == id);

    private List<string> GroupApps(MicRule rule) =>
        _config.Groups.FirstOrDefault(g => g.Id == rule.GroupId)?.Apps.ToList() ?? new List<string>();

    private void ReleaseGroupRuleApps(Guid ruleId)
    {
        if (_lastAppliedApps.TryGetValue(ruleId, out var apps))
            mic.SetAppsMuted(apps, false);
    }

    private bool Set(MicRule rule, bool muted)
    {
        if (rule.Scope == MicScope.Group)
        {
            _groupMuted[rule.Id] = muted;
            var apps = GroupApps(rule);
            _lastAppliedApps[rule.Id] = apps;
            mic.SetAppsMuted(apps, muted);
        }
        else
        {
            mic.SetDeviceMuted(rule.Scope, muted);
        }
        StateChanged?.Invoke();
        return muted;
    }

    private static MicResult Result(MicRule rule, bool muted) => new(rule, muted, rule.ShowOverlay, rule.PlaySound, rule.SoundVariant);
}
