using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Interop;

namespace AppVolumeHotkey.Audio;

/// <summary>
/// Windows-Core-Audio über eigene COM-Anbindung (siehe Interop/CoreAudio.cs). Fragt jede Sekunde alle Sessions auf allen aktiven Ausgabegeräten ab
/// (robuster als COM-Benachrichtigungen) und blendet Lautstärkeänderungen linear über <see cref="FadeDurationMs"/> ein.
/// Nur auf dem UI-Thread verwenden.
/// </summary>
public sealed class CoreAudioBackend : IAudioBackend, IDisposable
{
    private sealed record SessionEntry(string Id, string Exe, uint Pid, ISimpleAudioVolume Volume);

    private sealed record Device(string Id, IAudioSessionManager2 Manager);

    private static Guid _noContext = Guid.Empty;

    private sealed class Fade
    {
        public required Dictionary<string, float> StartById { get; init; }
        public required float Target { get; init; }
        public required Stopwatch Clock { get; init; }
    }

    private readonly IMMDeviceEnumerator _enumerator = null!;

    // Nur für den Snapshot-Modus (--ui-snapshots): feste Beispiel-Apps statt echter Audio-Sessions
    private readonly Dictionary<string, (RunningApp App, float Volume)>? _fake;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _fadeTimer;
    private readonly Dictionary<string, Fade> _fades = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string?> _exeByPid = new();
    private readonly Dictionary<string, RunningApp> _appInfoCache = new(StringComparer.OrdinalIgnoreCase);
    private List<Device> _devices = new();

    // Sofort-Erkennung: Windows meldet neue Sessions → kurz danach gezielt neu abfragen (Polling bleibt als Absicherung)
    private readonly SessionCreatedNotifier _notifier;
    private readonly Dictionary<string, (IAudioSessionManager2 Manager, IntPtr Callback)> _registered = new();
    private readonly DispatcherTimer _quickRefresh;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private Dictionary<string, SessionEntry> _sessions = new();

    public int FadeDurationMs { get; set; } = 200;

    /// <summary>Neue Audio-Session einer App (Parameter: exe-Name).</summary>
    public event Action<string>? SessionAdded;

    /// <summary>Liste laufender Apps hat sich geändert.</summary>
    public event Action? SessionsChanged;

    /// <summary>Nach jedem Abfragezyklus (ca. 1 s), z. B. für die Live-Anzeige der Lautstärken.</summary>
    public event Action? Polled;

    /// <summary>Beispiel-Backend für Screenshots: keine Windows-Audio-Abfragen.</summary>
    public CoreAudioBackend(IEnumerable<(RunningApp App, float Volume)> fakeApps)
    {
        _fake = fakeApps.ToDictionary(a => a.App.Exe, a => a, StringComparer.OrdinalIgnoreCase);
        _pollTimer = new DispatcherTimer();
        _fadeTimer = new DispatcherTimer();
        _quickRefresh = new DispatcherTimer();
        _notifier = new SessionCreatedNotifier(() => { });
    }

    public CoreAudioBackend()
    {
        _enumerator = CoreAudio.CreateEnumerator();
        _pollTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher.CurrentDispatcher);
        _fadeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) => FadeTick(), Dispatcher.CurrentDispatcher);
        _fadeTimer.Stop();
        _quickRefresh = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, (_, _) =>
        {
            _quickRefresh!.Stop();
            Refresh();
        }, Dispatcher.CurrentDispatcher);
        _quickRefresh.Stop();
        _notifier = new SessionCreatedNotifier(() => _dispatcher.BeginInvoke(() =>
        {
            Platform.DebugLog.Write("Audio: neue Session gemeldet");
            _quickRefresh.Stop(); // kurz entprellen, falls mehrere Sessions gleichzeitig entstehen
            _quickRefresh.Start();
        }));
        Refresh(raiseEvents: false);
        _pollTimer.Start();
    }

    /// <summary>Tatsächliche aktuelle Lautstärke der ersten Session (ohne Rücksicht auf laufende Übergänge), für die Anzeige.</summary>
    public float? GetCurrentVolume(string exe)
    {
        if (_fake is not null)
            return _fake.TryGetValue(exe, out var f) ? f.Volume : null;
        foreach (var s in SessionsOf(exe))
        {
            if (TryGet(s) is { } v)
                return v;
        }
        return null;
    }

    public float? GetVolume(string exe)
    {
        if (_fake is not null)
            return _fake.TryGetValue(exe, out var f) ? f.Volume : null;
        // Läuft gerade ein Übergang, zählt sein Ziel – sonst würde ein halber Zwischenwert als Ausgangswert gemerkt
        if (_fades.TryGetValue(exe, out var fade) && SessionsOf(exe).Any())
            return fade.Target;
        foreach (var s in SessionsOf(exe))
        {
            if (TryGet(s) is { } v)
                return v;
        }
        return null;
    }

    public void SetVolume(string exe, float volume01)
    {
        if (_fake is not null)
        {
            if (_fake.TryGetValue(exe, out var f))
                _fake[exe] = (f.App, Math.Clamp(volume01, 0f, 1f));
            return;
        }
        var target = Math.Clamp(volume01, 0f, 1f);
        var sessions = SessionsOf(exe).ToList();
        if (sessions.Count == 0)
        {
            _fades.Remove(exe);
            return;
        }

        if (FadeDurationMs <= 0)
        {
            _fades.Remove(exe);
            foreach (var s in sessions)
                TrySet(s, target);
            return;
        }

        var starts = new Dictionary<string, float>();
        foreach (var s in sessions)
        {
            if (TryGet(s) is { } v)
                starts[s.Id] = v;
        }
        _fades[exe] = new Fade { StartById = starts, Target = target, Clock = Stopwatch.StartNew() };
        _fadeTimer.Start();
    }

    public IReadOnlyList<RunningApp> GetRunningApps() => _fake is not null
        ? _fake.Values.Select(f => f.App).OrderBy(a => a.DisplayName).ToList()
        : _sessions.Values
            .GroupBy(s => s.Exe, StringComparer.OrdinalIgnoreCase)
            .Select(g => GetAppInfo(g.Key, g.First().Pid))
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private IEnumerable<SessionEntry> SessionsOf(string exe) =>
        _sessions.Values.Where(s => string.Equals(s.Exe, exe, StringComparison.OrdinalIgnoreCase));

    private static float? TryGet(SessionEntry s)
    {
        try { return s.Volume.GetMasterVolume(out var v) == CoreAudio.S_OK ? v : null; }
        catch (Exception) { return null; } // Session gerade verschwunden
    }

    private static void TrySet(SessionEntry s, float v)
    {
        try { s.Volume.SetMasterVolume(v, ref _noContext); }
        catch (Exception) { /* Session gerade verschwunden */ }
    }

    private void FadeTick()
    {
        var duration = Math.Max(1, FadeDurationMs);
        foreach (var (exe, fade) in _fades.ToList())
        {
            var t = Math.Min(1.0, fade.Clock.Elapsed.TotalMilliseconds / duration);
            foreach (var (id, start) in fade.StartById)
            {
                if (_sessions.TryGetValue(id, out var s))
                    TrySet(s, (float)(start + (fade.Target - start) * t));
            }
            if (t >= 1.0)
                _fades.Remove(exe);
        }
        if (_fades.Count == 0)
            _fadeTimer.Stop();
    }

    private readonly Stopwatch _sinceDeviceScan = new();

    private void Refresh(bool raiseEvents = true)
    {
        var timer = Stopwatch.StartNew();
        var newSessions = new Dictionary<string, SessionEntry>();

        // Geräteliste nur alle 10 s neu holen (teuer); Sessions der bekannten Geräte jede Sekunde
        if (_devices.Count == 0 || !_sinceDeviceScan.IsRunning || _sinceDeviceScan.Elapsed > TimeSpan.FromSeconds(10))
        {
            try
            {
                _devices = EnumerateDevices();
                _sinceDeviceScan.Restart();
                SyncNotifications();
            }
            catch (Exception)
            {
                return; // Audio-Subsystem gerade nicht verfügbar – beim nächsten Tick erneut versuchen
            }
        }

        try
        {
            foreach (var device in _devices)
            {
                try
                {
                    if (device.Manager.GetSessionEnumerator(out var sessions) != CoreAudio.S_OK || sessions is null)
                        continue;
                    if (sessions.GetCount(out var count) != CoreAudio.S_OK)
                        continue;
                    for (var i = 0; i < count; i++)
                    {
                        try
                        {
                            if (sessions.GetSession(i, out var control) != CoreAudio.S_OK || control is null)
                                continue;
                            if (control.IsSystemSoundsSession() == CoreAudio.S_OK)
                                continue;
                            if (control.GetState(out var state) != CoreAudio.S_OK || state == CoreAudio.AudioSessionStateExpired)
                                continue;
                            // Erfolgscodes wie AUDCLNT_S_NO_CURRENT_PROCESS (Mehrprozess-Session) liefern trotzdem eine PID
                            if (control.GetProcessId(out var pid) < 0)
                                continue;
                            if (!Native.IsProcessAlive(pid))
                                continue; // App wurde beendet, Windows hat die Session nur noch nicht entfernt
                            var exe = ExeForPid(pid);
                            if (exe is null)
                                continue;
                            if (control.GetSessionInstanceIdentifier(out var instanceId) != CoreAudio.S_OK)
                                continue;
                            var id = device.Id + "|" + instanceId;
                            // ISimpleAudioVolume per QueryInterface vom selben Session-Objekt
                            newSessions[id] = new SessionEntry(id, exe, pid, (ISimpleAudioVolume)control);
                        }
                        catch (Exception) { }
                    }
                }
                catch (Exception) { }
            }
        }
        catch (Exception)
        {
            _sinceDeviceScan.Reset(); // beim nächsten Tick Geräte neu holen
            return;
        }

        var added = newSessions.Values.Where(s => !_sessions.ContainsKey(s.Id)).Select(s => s.Exe)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var removedAny = _sessions.Keys.Any(k => !newSessions.ContainsKey(k));

        // Alte COM-Wrapper werden vom GC freigegeben (kein ReleaseComObject: Windows liefert oft dieselben Objekte
        // erneut, ein vorzeitiges Freigeben würde die neuen Wrapper mit zerstören).
        _sessions = newSessions;

        // PID-Cache klein halten
        var livePids = newSessions.Values.Select(s => s.Pid).ToHashSet();
        foreach (var pid in _exeByPid.Keys.Where(p => !livePids.Contains(p)).ToList())
            _exeByPid.Remove(pid);

        if (timer.ElapsedMilliseconds > 200)
            Platform.DebugLog.Write($"Audio-Abfrage dauerte {timer.ElapsedMilliseconds} ms ({_devices.Count} Geräte, {newSessions.Count} Sessions)");

        if (!raiseEvents)
            return;
        foreach (var exe in added)
            SessionAdded?.Invoke(exe);
        if (added.Count > 0 || removedAny)
            SessionsChanged?.Invoke();
        Polled?.Invoke();
    }

    /// <summary>Für jedes aktive Gerät genau einmal Benachrichtigungen anmelden, verschwundene Geräte abmelden.</summary>
    private void SyncNotifications()
    {
        var ids = _devices.Select(d => d.Id).ToHashSet();
        foreach (var (id, reg) in _registered.Where(r => !ids.Contains(r.Key)).ToList())
        {
            try { reg.Manager.UnregisterSessionNotification(reg.Callback); } catch (Exception) { }
            Marshal.Release(reg.Callback);
            _registered.Remove(id);
        }
        foreach (var device in _devices.Where(d => !_registered.ContainsKey(d.Id)))
        {
            var ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.GetComInterfaceForObject<SessionCreatedNotifier, IAudioSessionNotification>(_notifier);
                if (device.Manager.RegisterSessionNotification(ptr) == CoreAudio.S_OK)
                {
                    _registered[device.Id] = (device.Manager, ptr);
                    ptr = IntPtr.Zero;
                }
            }
            catch (Exception e)
            {
                Platform.DebugLog.Write("Audio: Benachrichtigung nicht angemeldet: " + e.Message);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.Release(ptr);
            }
        }
    }

    private void UnregisterNotifications()
    {
        foreach (var (_, reg) in _registered)
        {
            try { reg.Manager.UnregisterSessionNotification(reg.Callback); } catch (Exception) { }
            Marshal.Release(reg.Callback);
        }
        _registered.Clear();
    }

    private List<Device> EnumerateDevices()
    {
        var result = new List<Device>();
        if (_enumerator.EnumAudioEndpoints(EDataFlow.eRender, CoreAudio.DEVICE_STATE_ACTIVE, out var collection) != CoreAudio.S_OK || collection is null)
            throw new InvalidOperationException("Audiogeräte konnten nicht abgefragt werden.");
        collection.GetCount(out var count);
        for (uint i = 0; i < count; i++)
        {
            try
            {
                if (collection.Item(i, out var device) != CoreAudio.S_OK || device is null)
                    continue;
                device.GetId(out var id);
                var iid = CoreAudio.IID_IAudioSessionManager2;
                if (device.Activate(ref iid, CoreAudio.CLSCTX_ALL, IntPtr.Zero, out var obj) != CoreAudio.S_OK || obj is not IAudioSessionManager2 manager)
                    continue;
                result.Add(new Device(id ?? i.ToString(), manager));
            }
            catch (Exception) { }
        }
        return result;
    }

    private string? ExeForPid(uint pid)
    {
        if (pid == 0)
            return null;
        if (_exeByPid.TryGetValue(pid, out var cached))
            return cached;
        string? exe = null;
        try
        {
            var path = Native.GetProcessImagePath(pid);
            exe = path is not null ? Path.GetFileName(path) : Process.GetProcessById((int)pid).ProcessName + ".exe";
        }
        catch (Exception) { }
        _exeByPid[pid] = exe;
        return exe;
    }

    private RunningApp GetAppInfo(string exe, uint pid)
    {
        if (_appInfoCache.TryGetValue(exe, out var info))
            return info;

        var display = Path.GetFileNameWithoutExtension(exe);
        ImageSource? icon = null;
        var path = Native.GetProcessImagePath(pid);
        if (path is not null)
        {
            try
            {
                var desc = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(desc))
                    display = desc.Trim();
            }
            catch (Exception) { }
            icon = LoadIcon(path);
        }
        info = new RunningApp(exe, display, icon);
        _appInfoCache[exe] = info;
        return info;
    }

    private static ImageSource? LoadIcon(string path)
    {
        var sfi = new Native.SHFILEINFO();
        var res = Native.SHGetFileInfo(path, 0, ref sfi, (uint)System.Runtime.InteropServices.Marshal.SizeOf(sfi), Native.SHGFI_ICON | Native.SHGFI_SMALLICON);
        if (res == IntPtr.Zero || sfi.hIcon == IntPtr.Zero)
            return null;
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(sfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Native.DestroyIcon(sfi.hIcon);
        }
    }

    /// <summary>Alle laufenden Übergänge sofort auf ihr Ziel setzen.</summary>
    public void CompleteFades()
    {
        foreach (var (_, fade) in _fades)
        {
            foreach (var id in fade.StartById.Keys)
            {
                if (_sessions.TryGetValue(id, out var s))
                    TrySet(s, fade.Target);
            }
        }
        _fades.Clear();
        _fadeTimer.Stop();
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _quickRefresh.Stop();
        UnregisterNotifications();
        CompleteFades();
        _sessions = new();
        _devices = new();
    }
}
