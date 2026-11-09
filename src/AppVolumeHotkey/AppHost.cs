using System.IO;
using System.Windows;
using AppVolumeHotkey.Audio;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Persistence;
using AppVolumeHotkey.Input;
using AppVolumeHotkey.Overlay;
using AppVolumeHotkey.Platform;
using AppVolumeHotkey.Tray;
using AppVolumeHotkey.UI;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey;

/// <summary>Ein Eintrag der Statusanzeige; <see cref="Alert"/> = rot (z. B. Mikrofon stumm).</summary>
public sealed record StatusItem(string Text, bool Alert);

/// <summary>Verdrahtet Konfiguration, Audio, Hotkeys, Overlay, Tray und Fenster.</summary>
public sealed class AppHost : IDisposable
{
    private readonly Dictionary<int, Guid> _profileByHotkeyId = new();
    private readonly HashSet<Guid> _reportedFailures = new();
    private readonly OverlayService _overlay = new();
    private StatusIndicatorWindow? _status;
    private bool _hotkeysPaused;
    private MainWindow? _main;
    private bool _exiting;

    private const int MicHotkeyId = 9001;
    private const int DuckingHotkeyId = 9002;
    private readonly System.Windows.Threading.DispatcherTimer _duckTimer;
    private readonly System.Windows.Threading.DispatcherTimer _micPoll;
    private bool? _lastMicMuted;

    public AppConfig Config { get; private set; }
    public CoreAudioBackend Audio { get; }
    public MicBackend MicAudio { get; }
    public MicController Mic { get; }
    public DuckingController Ducking { get; }

    /// <summary>Mikrofon-/Ducking-Hotkey ließ sich nicht registrieren (belegt).</summary>
    public bool MicHotkeyFailed { get; private set; }
    public bool DuckingHotkeyFailed { get; private set; }

    /// <summary>Mikrofon stumm/an hat sich geändert (auch von außen, z. B. in Windows).</summary>
    public event Action? MicStateChanged;
    public VolumeController Volume { get; }
    public HotkeyManager Hotkeys { get; }
    public TrayController Tray { get; }

    /// <summary>Profile, deren Hotkey sich nicht registrieren ließ (belegt).</summary>
    public HashSet<Guid> FailedHotkeys { get; } = new();

    public event Action? ConfigChanged;

    /// <summary>Komplette Konfiguration ersetzt (Import) – Fenster müssen alles neu laden.</summary>
    public event Action? ConfigReplaced;

    /// <param name="snapshotConfig">Nur für --ui-snapshots: feste Beispielkonfiguration und Beispiel-Apps.</param>
    public AppHost(AppConfig? snapshotConfig = null, IEnumerable<(RunningApp App, float Volume)>? fakeApps = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        Config = snapshotConfig ?? ConfigStore.Load(AppPaths.ConfigFile);

        Audio = fakeApps is not null ? new CoreAudioBackend(fakeApps) : new CoreAudioBackend();
        Audio.FadeDurationMs = Config.Settings.FadeDurationMs;
        Volume = new VolumeController(Audio, new JsonBaselineStore(AppPaths.StateFile));
        Volume.SetConfig(Config);
        Volume.RestorePersistedBaselines();
        Audio.SessionAdded += Volume.OnSessionAdded;
        // „Lautstärke festhalten“: nach jeder Abfrage prüfen, Apps im Ducking auslassen
        Audio.Polled += () => Volume.Enforce(exe => Ducking?.IsDucked(exe) == true);

        MicAudio = new MicBackend(fake: snapshotConfig is not null) { Scope = Config.Mic.Scope };
        Mic = new MicController(MicAudio);
        Mic.SetSettings(Config.Mic);
        Ducking = new DuckingController(Audio, Audio);
        Ducking.SetConfig(Config);

        Hotkeys = new HotkeyManager();
        Hotkeys.Pressed += OnHotkeyPressed;
        Hotkeys.Released += OnHotkeyReleased;

        Tray = new TrayController(this);
        Volume.StateChanged += Tray.Rebuild;
        ConfigChanged += Tray.Rebuild;

        RegisterAllHotkeys();

        Volume.StateChanged += UpdateStatusIndicator;
        ConfigChanged += UpdateStatusIndicator;
        L.Changed += UpdateStatusIndicator;
        Mic.StateChanged += OnMicChanged;
        Ducking.StateChanged += () => { UpdateStatusIndicator(); Tray.Rebuild(); };

        // Ducking: Pegel regelmäßig prüfen (nur wenn eingeschaltet und Regeln vorhanden)
        _duckTimer = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(50), System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => { if (Config.Ducking.Enabled && Config.Ducking.Rules.Count > 0) Ducking.Tick(); },
            System.Windows.Threading.Dispatcher.CurrentDispatcher);
        // Mikrofon-Zustand auch bei Änderungen außerhalb der App mitbekommen
        _micPoll = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(1), System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => OnMicChanged(), System.Windows.Threading.Dispatcher.CurrentDispatcher);
        if (snapshotConfig is not null)
        {
            _duckTimer.Stop();
            _micPoll.Stop();
        }
        OnMicChanged();
        UpdateStatusIndicator();
    }

    private void OnMicChanged()
    {
        var muted = Mic.IsMuted;
        if (muted == _lastMicMuted)
            return;
        _lastMicMuted = muted;
        UpdateStatusIndicator();
        Tray.Rebuild();
        MicStateChanged?.Invoke();
    }

    /// <summary>
    /// Ist der Hotkey schon vergeben? Liefert die Fehlermeldung oder null.
    /// <paramref name="self"/>: das gerade bearbeitete Element (Profil-Id, "mic" oder "ducking"), das sich nicht selbst blockiert.
    /// </summary>
    public string? HotkeyConflict(Hotkey hotkey, object? self)
    {
        foreach (var (g, p) in Config.AllProfiles())
        {
            if (p.Hotkey == hotkey && !Equals(self, p.Id))
                return L.F("Profile_ErrHotkeyUsed", KeyNames.Format(hotkey), p.Name, g.Name);
        }
        if (Config.Mic.Hotkey == hotkey && !Equals(self, "mic"))
            return L.F("Hotkey_UsedByMic", KeyNames.Format(hotkey));
        if (Config.Ducking.ToggleHotkey == hotkey && !Equals(self, "ducking"))
            return L.F("Hotkey_UsedByDucking", KeyNames.Format(hotkey));
        return null;
    }

    /// <summary>Kurzbeschreibung je aktiver Gruppe, z. B. „Voice 20 %“.</summary>
    public IReadOnlyList<StatusItem> ActiveStatusItems()
    {
        var list = new List<StatusItem>();
        if (Config.Mic.ShowInStatus && _lastMicMuted == true)
            list.Add(new StatusItem(L.T("Status_MicMuted"), Alert: true));
        if (!Config.Settings.StatusIndicator)
            return list;
        foreach (var g in Config.Groups)
        {
            if (Volume.GetActive(g.Id) is not { } a)
                continue;
            var p = g.Profiles.FirstOrDefault(x => x.Id == a.ProfileId);
            if (p is not null)
                list.Add(new StatusItem(L.F("Status_Item", g.Name, p.Steps[a.StepIndex].Label), Alert: false));
        }
        if (Ducking.IsDucking)
            list.Add(new StatusItem(L.T("Status_Ducking"), Alert: false));
        return list;
    }

    private void UpdateStatusIndicator()
    {
        if (_exiting)
            return;
        var items = ActiveStatusItems();
        if (items.Count == 0)
        {
            _status?.Hide();
            return;
        }
        _status ??= new StatusIndicatorWindow();
        _status.Update(items, Config.Settings);
    }

    public bool HotkeysPaused
    {
        get => _hotkeysPaused;
        set
        {
            _hotkeysPaused = value;
            RegisterAllHotkeys();
            Tray.Rebuild();
        }
    }

    /// <summary>Alle Gruppen sofort (ohne Übergang) zurücksetzen – beim Beenden und im Fehlerfall.</summary>
    public void EmergencyReset()
    {
        try
        {
            Audio.FadeDurationMs = 0;
            Audio.CompleteFades();
            Ducking.RestoreAll();
            Volume.ResetAll();
        }
        catch (Exception) { }
        finally
        {
            Audio.FadeDurationMs = Config.Settings.FadeDurationMs;
            Audio.CompleteFades();
        }
    }

    private int _suspendCount;

    /// <summary>Hotkeys vorübergehend abmelden (z. B. während ein Hotkey aufgenommen wird).</summary>
    public void SuspendHotkeys()
    {
        _suspendCount++;
        Hotkeys.UnregisterAll();
    }

    public void ResumeHotkeys()
    {
        if (_suspendCount > 0 && --_suspendCount == 0)
            RegisterAllHotkeys();
    }

    /// <summary>Nur speichern (z. B. Fenstergröße) – ohne Hotkeys neu zu registrieren oder Zustände zurückzusetzen.</summary>
    public void SaveSettingsOnly()
    {
        try { ConfigStore.Save(AppPaths.ConfigFile, Config); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public void SaveConfig()
    {
        try
        {
            ConfigStore.Save(AppPaths.ConfigFile, Config);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(L.F("Msg_SaveFailed", e.Message),
                L.T("Msg_SaveFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Audio.FadeDurationMs = Config.Settings.FadeDurationMs;
        Volume.SetConfig(Config);
        MicAudio.Scope = Config.Mic.Scope;
        Mic.SetSettings(Config.Mic);
        Ducking.SetConfig(Config);
        RegisterAllHotkeys();
        ConfigChanged?.Invoke();
        OnMicChanged();
    }

    public IEnumerable<string> ActiveProfileDescriptions()
    {
        foreach (var g in Config.Groups)
        {
            if (Volume.GetActive(g.Id) is not { } a)
                continue;
            var p = g.Profiles.FirstOrDefault(x => x.Id == a.ProfileId);
            if (p is null)
                continue;
            yield return L.F("Tray_ActiveLine", g.Name, p.Name, p.StepLabel(p.Steps[a.StepIndex]));
        }
    }

    public void ShowMainWindow()
    {
        if (_exiting)
            return;
        _main ??= new MainWindow(this);
        if (!_main.IsVisible)
            _main.Show();
        if (_main.WindowState == WindowState.Minimized)
            _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void Exit()
    {
        if (_exiting)
            return;
        _exiting = true;
        EmergencyReset();
        try { Mic.OnExit(); } catch (Exception) { }
        _main?.SavePlacementPublic();
        _main?.ForceClose();
        Dispose();
        Application.Current.Shutdown();
    }

    /// <summary>Beendet diese Instanz und startet mit Adminrechten neu (wenn der Nutzer die UAC-Abfrage bestätigt).</summary>
    public void RestartElevated()
    {
        ((App)Application.Current).ReleaseSingleInstance();
        if (Elevation.TryRelaunchElevated(Array.Empty<string>()))
            Exit();
        else
            ((App)Application.Current).ReacquireSingleInstance();
    }

    private void RegisterAllHotkeys()
    {
        Hotkeys.UnregisterAll();
        _profileByHotkeyId.Clear();
        FailedHotkeys.Clear();
        MicHotkeyFailed = DuckingHotkeyFailed = false;
        if (_hotkeysPaused || _suspendCount > 0)
            return;

        var nextId = 1;
        var newFailures = new List<string>();
        foreach (var (_, profile) in Config.AllProfiles())
        {
            if (profile.Hotkey is null)
                continue;
            var id = nextId++;
            if (Hotkeys.Register(id, profile.Hotkey, profile.PassThrough))
            {
                _profileByHotkeyId[id] = profile.Id;
                _reportedFailures.Remove(profile.Id);
            }
            else
            {
                FailedHotkeys.Add(profile.Id);
                if (_reportedFailures.Add(profile.Id))
                    newFailures.Add($"{KeyNames.Format(profile.Hotkey)} ({profile.Name})");
            }
        }
        if (Config.Mic.Hotkey is { } micKey && !Hotkeys.Register(MicHotkeyId, micKey, Config.Mic.PassThrough))
        {
            MicHotkeyFailed = true;
            newFailures.Add($"{KeyNames.Format(micKey)} ({L.T("Mic_Title")})");
        }
        if (Config.Ducking.ToggleHotkey is { } duckKey && !Hotkeys.Register(DuckingHotkeyId, duckKey))
        {
            DuckingHotkeyFailed = true;
            newFailures.Add($"{KeyNames.Format(duckKey)} (Ducking)");
        }
        if (newFailures.Count > 0)
            Tray.ShowNotification(L.T("Msg_HotkeyTakenTitle"), L.F("Msg_HotkeyTaken", string.Join(", ", newFailures)));
    }

    /// <summary>Exportierte Einstellungen übernehmen: alles zurücksetzen, ersetzen, speichern.</summary>
    public void ImportConfig(AppConfig imported)
    {
        EmergencyReset();
        imported.Settings.MainWindow = Config.Settings.MainWindow; // Fensterposition dieses PCs behalten
        Config = imported;
        SaveConfig();
        ConfigReplaced?.Invoke();
    }

    /// <summary>Ducking an/aus (Hotkey, Infobereich-Menü oder Oberfläche).</summary>
    public void ToggleDucking()
    {
        var on = Ducking.Toggle();
        SaveSettingsOnly();
        ConfigChanged?.Invoke();
        _overlay.HandleDucking(on, Config.Settings);
    }

    private void OnHotkeyPressed(int id)
    {
        if (id == MicHotkeyId)
        {
            if (Mic.OnPressed() is { } mic)
                _overlay.HandleMic(mic, Config.Mic, Config.Settings);
            return;
        }
        if (id == DuckingHotkeyId)
        {
            ToggleDucking();
            return;
        }
        if (!_profileByHotkeyId.TryGetValue(id, out var profileId))
            return;
        if (Volume.OnHotkey(profileId) is { } result)
            _overlay.Handle(result, Config.Settings);
    }

    private void OnHotkeyReleased(int id)
    {
        if (id == MicHotkeyId)
        {
            if (Mic.OnReleased() is { } mic)
                _overlay.HandleMic(mic, Config.Mic, Config.Settings);
            return;
        }
        if (!_profileByHotkeyId.TryGetValue(id, out var profileId))
            return;
        if (Volume.OnHotkeyReleased(profileId) is { } result)
            _overlay.Handle(result, Config.Settings);
    }

    public void Dispose()
    {
        _duckTimer.Stop();
        _micPoll.Stop();
        Hotkeys.Dispose();
        Audio.Dispose();
        Tray.Dispose();
        _overlay.Close();
        _status?.Close();
    }
}
