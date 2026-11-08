using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Persistence;

namespace AppVolumeHotkey.Core.Audio;

/// <summary>
/// Zustandsmaschine pro Gruppe: höchstens ein aktives Profil, Abfolge wird per Hotkey durchgeschaltet,
/// Ausgangswerte werden beim Aktivieren gemerkt und bei der Stufe „Ausgangswert“ wiederhergestellt.
/// Nicht threadsicher – nur vom UI-Thread aufrufen.
/// </summary>
public sealed class VolumeController(IAudioBackend audio, IBaselineStore store)
{
    /// <param name="Previous">Bei Halten-Profilen: Zustand vor dem Drücken, zu dem beim Loslassen zurückgekehrt wird.</param>
    private sealed record ActiveState(Guid ProfileId, int StepIndex, IReadOnlyList<Step> Steps, ValueMode Mode, TriggerMode Trigger,
        HashSet<string> Apps, ActiveState? Previous = null);

    private AppConfig _config = new();
    private readonly Dictionary<Guid, ActiveState> _active = new();
    private readonly Dictionary<Guid, Dictionary<string, float>> _baselines = new();
    private Dictionary<Guid, Dictionary<string, float>> _pendingRestore = new();

    public event Action? StateChanged;

    public (Guid ProfileId, int StepIndex)? GetActive(Guid groupId) =>
        _active.TryGetValue(groupId, out var s) ? (s.ProfileId, s.StepIndex) : null;

    public void SetConfig(AppConfig config)
    {
        _config = config;
        var changed = false;
        foreach (var (groupId, state) in _active.ToList())
        {
            var group = config.Groups.FirstOrDefault(g => g.Id == groupId);
            var profile = group?.Profiles.FirstOrDefault(p => p.Id == state.ProfileId);
            var stillValid = group is not null
                && profile is not null
                && profile.Steps.SequenceEqual(state.Steps)
                && profile.Mode == state.Mode
                && profile.Trigger == state.Trigger
                && state.Apps.SetEquals(group.Apps);
            if (!stillValid)
            {
                ResetGroupCore(groupId);
                changed = true;
            }
        }
        if (changed)
            Persist();
        StateChanged?.Invoke();
    }

    public HotkeyResult? OnHotkey(Guid profileId)
    {
        var (group, profile) = _config.AllProfiles().FirstOrDefault(x => x.Profile.Id == profileId);
        if (group is null || profile is null || profile.Steps.Count == 0)
            return null;
        if (profile.Trigger == TriggerMode.Hold)
            return OnHoldPressed(group, profile);

        int index;
        if (_active.TryGetValue(group.Id, out var state))
        {
            index = state.ProfileId == profile.Id ? (state.StepIndex + 1) % profile.Steps.Count : 0;
        }
        else
        {
            index = 0;
            CaptureBaselines(group);
        }

        var step = profile.Steps[index];
        bool isActive;
        if (step.Kind == StepKind.Baseline)
        {
            ResetGroupCore(group.Id);
            isActive = false;
        }
        else
        {
            var state2 = new ActiveState(profile.Id, index, profile.Steps.ToList(), profile.Mode, profile.Trigger,
                new HashSet<string>(group.Apps, StringComparer.OrdinalIgnoreCase));
            _active[group.Id] = state2;
            foreach (var exe in group.Apps)
                Apply(group.Id, state2, exe);
            isActive = true;
        }

        Persist();
        StateChanged?.Invoke();
        return new HotkeyResult(group, profile, step, isActive,
            ShowOverlay: isActive ? profile.OverlayOnActivate : profile.OverlayOnDeactivate,
            PlaySound: profile.Sound);
    }

    private HotkeyResult? OnHoldPressed(AppGroup group, Profile profile)
    {
        var index = profile.Steps.FindIndex(s => s.Kind == StepKind.Percent);
        if (index < 0)
            return null;

        _active.TryGetValue(group.Id, out var previous);
        if (previous?.ProfileId == profile.Id)
            return null; // Taste wird noch gehalten – nichts tun
        if (previous is null)
            CaptureBaselines(group);

        var state = new ActiveState(profile.Id, index, profile.Steps.ToList(), profile.Mode, profile.Trigger,
            new HashSet<string>(group.Apps, StringComparer.OrdinalIgnoreCase), previous);
        _active[group.Id] = state;
        foreach (var exe in group.Apps)
            Apply(group.Id, state, exe);

        Persist();
        StateChanged?.Invoke();
        return new HotkeyResult(group, profile, profile.Steps[index], true, profile.OverlayOnActivate, profile.Sound);
    }

    /// <summary>Taste eines Halten-Profils losgelassen: zurück zum vorherigen Zustand (anderes Profil oder Ausgangswert).</summary>
    public HotkeyResult? OnHotkeyReleased(Guid profileId)
    {
        var (group, profile) = _config.AllProfiles().FirstOrDefault(x => x.Profile.Id == profileId);
        if (group is null || profile is null || profile.Trigger != TriggerMode.Hold)
            return null;
        if (!_active.TryGetValue(group.Id, out var state) || state.ProfileId != profile.Id)
            return null;

        HotkeyResult result;
        var previousProfile = state.Previous is { } prev ? group.Profiles.FirstOrDefault(p => p.Id == prev.ProfileId) : null;
        if (state.Previous is { } before && previousProfile is not null)
        {
            _active[group.Id] = before;
            foreach (var exe in group.Apps)
                Apply(group.Id, before, exe);
            result = new HotkeyResult(group, previousProfile, before.Steps[before.StepIndex], true, profile.OverlayOnDeactivate, profile.Sound);
        }
        else
        {
            ResetGroupCore(group.Id);
            result = new HotkeyResult(group, profile, Step.Baseline, false, profile.OverlayOnDeactivate, profile.Sound);
        }

        Persist();
        StateChanged?.Invoke();
        return result;
    }

    public void OnSessionAdded(string exe)
    {
        var changed = false;

        foreach (var (groupId, values) in _pendingRestore.ToList())
        {
            if (values.Remove(exe, out var v))
            {
                audio.SetVolume(exe, v);
                changed = true;
            }
            if (values.Count == 0)
                _pendingRestore.Remove(groupId);
        }

        foreach (var (groupId, state) in _active)
        {
            if (!state.Apps.Contains(exe))
                continue;
            var baselines = _baselines[groupId];
            if (!baselines.ContainsKey(exe) && audio.GetVolume(exe) is { } current)
            {
                baselines[exe] = current;
                changed = true;
            }
            Apply(groupId, state, exe);
        }

        if (changed)
            Persist();
    }

    public void ResetGroup(Guid groupId)
    {
        ResetGroupCore(groupId);
        Persist();
        StateChanged?.Invoke();
    }

    public void ResetAll()
    {
        foreach (var groupId in _active.Keys.ToList())
            ResetGroupCore(groupId);
        Persist();
        StateChanged?.Invoke();
    }

    /// <summary>Beim Start: Ausgangswerte aus einem früheren (abgestürzten) Lauf wiederherstellen.</summary>
    public void RestorePersistedBaselines()
    {
        _pendingRestore = new();
        foreach (var (groupId, values) in store.Load())
        {
            foreach (var (exe, v) in values)
            {
                if (audio.GetVolume(exe) is not null)
                    audio.SetVolume(exe, v);
                else
                    Pending(groupId)[exe] = v;
            }
        }
        Persist();
    }

    /// <summary>Setzt die App auf den Wert der aktuellen Stufe (absolut oder relativ zu ihrem Ausgangswert).</summary>
    private void Apply(Guid groupId, ActiveState state, string exe)
    {
        var fraction = state.Steps[state.StepIndex].Value / 100f;
        if (state.Mode == ValueMode.Absolute)
        {
            audio.SetVolume(exe, fraction);
        }
        else if (_baselines.TryGetValue(groupId, out var b) && b.TryGetValue(exe, out var baseline))
        {
            audio.SetVolume(exe, baseline * fraction);
        }
        // relativ ohne Ausgangswert (App läuft nicht): nichts tun, OnSessionAdded holt es nach
    }

    private Dictionary<string, float> Pending(Guid groupId)
    {
        if (!_pendingRestore.TryGetValue(groupId, out var d))
            _pendingRestore[groupId] = d = new(StringComparer.OrdinalIgnoreCase);
        return d;
    }

    private void CaptureBaselines(AppGroup group)
    {
        var d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var exe in group.Apps)
        {
            if (audio.GetVolume(exe) is { } v)
                d[exe] = v;
        }
        _baselines[group.Id] = d;
    }

    private void ResetGroupCore(Guid groupId)
    {
        if (_baselines.Remove(groupId, out var values))
        {
            foreach (var (exe, v) in values)
            {
                if (audio.GetVolume(exe) is not null)
                    audio.SetVolume(exe, v);
                else
                    Pending(groupId)[exe] = v; // App läuft gerade nicht – beim nächsten Start der App zurücksetzen
            }
        }
        _active.Remove(groupId);
    }

    private void Persist()
    {
        var all = new Dictionary<Guid, Dictionary<string, float>>();
        foreach (var source in new[] { _pendingRestore, _baselines })
        {
            foreach (var (groupId, values) in source)
            {
                if (values.Count == 0)
                    continue;
                if (!all.TryGetValue(groupId, out var target))
                    all[groupId] = target = new(StringComparer.OrdinalIgnoreCase);
                foreach (var (exe, v) in values)
                    target[exe] = v;
            }
        }
        store.Save(all);
    }
}
