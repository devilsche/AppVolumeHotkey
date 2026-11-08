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

    public AppConfig Config { get; private set; }
    public CoreAudioBackend Audio { get; }
    public VolumeController Volume { get; }
    public HotkeyManager Hotkeys { get; }
    public TrayController Tray { get; }

    /// <summary>Profile, deren Hotkey sich nicht registrieren ließ (belegt).</summary>
    public HashSet<Guid> FailedHotkeys { get; } = new();

    public event Action? ConfigChanged;

    public AppHost()
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        Config = ConfigStore.Load(AppPaths.ConfigFile);

        Audio = new CoreAudioBackend { FadeDurationMs = Config.Settings.FadeDurationMs };
        Volume = new VolumeController(Audio, new JsonBaselineStore(AppPaths.StateFile));
        Volume.SetConfig(Config);
        Volume.RestorePersistedBaselines();
        Audio.SessionAdded += Volume.OnSessionAdded;

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
        UpdateStatusIndicator();
    }

    /// <summary>Kurzbeschreibung je aktiver Gruppe, z. B. „Voice 20 %“.</summary>
    public IReadOnlyList<string> ActiveStatusItems()
    {
        var list = new List<string>();
        foreach (var g in Config.Groups)
        {
            if (Volume.GetActive(g.Id) is not { } a)
                continue;
            var p = g.Profiles.FirstOrDefault(x => x.Id == a.ProfileId);
            if (p is not null)
                list.Add(L.F("Status_Item", g.Name, p.Steps[a.StepIndex].Label));
        }
        return list;
    }

    private void UpdateStatusIndicator()
    {
        if (_exiting)
            return;
        var items = ActiveStatusItems();
        if (items.Count == 0 || !Config.Settings.StatusIndicator)
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
        RegisterAllHotkeys();
        ConfigChanged?.Invoke();
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
        if (newFailures.Count > 0)
            Tray.ShowNotification(L.T("Msg_HotkeyTakenTitle"), L.F("Msg_HotkeyTaken", string.Join(", ", newFailures)));
    }

    private void OnHotkeyPressed(int id)
    {
        if (!_profileByHotkeyId.TryGetValue(id, out var profileId))
            return;
        if (Volume.OnHotkey(profileId) is { } result)
            _overlay.Handle(result, Config.Settings);
    }

    private void OnHotkeyReleased(int id)
    {
        if (!_profileByHotkeyId.TryGetValue(id, out var profileId))
            return;
        if (Volume.OnHotkeyReleased(profileId) is { } result)
            _overlay.Handle(result, Config.Settings);
    }

    public void Dispose()
    {
        Hotkeys.Dispose();
        Audio.Dispose();
        Tray.Dispose();
        _overlay.Close();
        _status?.Close();
    }
}
