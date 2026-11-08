using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Interop;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace AppVolumeHotkey.Audio;

/// <summary>
/// Windows-Core-Audio über NAudio. Fragt jede Sekunde alle Sessions auf allen aktiven Ausgabegeräten ab
/// (robuster als COM-Benachrichtigungen) und blendet Lautstärkeänderungen linear über <see cref="FadeDurationMs"/> ein.
/// Nur auf dem UI-Thread verwenden.
/// </summary>
public sealed class CoreAudioBackend : IAudioBackend, IDisposable
{
    private sealed record SessionEntry(string Id, string Exe, uint Pid, AudioSessionControl Control);

    private sealed class Fade
    {
        public required Dictionary<string, float> StartById { get; init; }
        public required float Target { get; init; }
        public required Stopwatch Clock { get; init; }
    }

    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _fadeTimer;
    private readonly Dictionary<string, Fade> _fades = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string?> _exeByPid = new();
    private readonly Dictionary<string, RunningApp> _appInfoCache = new(StringComparer.OrdinalIgnoreCase);
    private List<MMDevice> _devices = new();
    private Dictionary<string, SessionEntry> _sessions = new();

    public int FadeDurationMs { get; set; } = 200;

    /// <summary>Neue Audio-Session einer App (Parameter: exe-Name).</summary>
    public event Action<string>? SessionAdded;

    /// <summary>Liste laufender Apps hat sich geändert.</summary>
    public event Action? SessionsChanged;

    public CoreAudioBackend()
    {
        _pollTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher.CurrentDispatcher);
        _fadeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) => FadeTick(), Dispatcher.CurrentDispatcher);
        _fadeTimer.Stop();
        Refresh(raiseEvents: false);
        _pollTimer.Start();
    }

    public float? GetVolume(string exe)
    {
        foreach (var s in SessionsOf(exe))
        {
            try { return s.Control.SimpleAudioVolume.Volume; }
            catch (Exception) { /* Session gerade verschwunden */ }
        }
        return null;
    }

    public void SetVolume(string exe, float volume01)
    {
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
            try { starts[s.Id] = s.Control.SimpleAudioVolume.Volume; }
            catch (Exception) { }
        }
        _fades[exe] = new Fade { StartById = starts, Target = target, Clock = Stopwatch.StartNew() };
        _fadeTimer.Start();
    }

    public IReadOnlyList<RunningApp> GetRunningApps() =>
        _sessions.Values
            .GroupBy(s => s.Exe, StringComparer.OrdinalIgnoreCase)
            .Select(g => GetAppInfo(g.Key, g.First().Pid))
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private IEnumerable<SessionEntry> SessionsOf(string exe) =>
        _sessions.Values.Where(s => string.Equals(s.Exe, exe, StringComparison.OrdinalIgnoreCase));

    private static void TrySet(SessionEntry s, float v)
    {
        try { s.Control.SimpleAudioVolume.Volume = v; }
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

    private void Refresh(bool raiseEvents = true)
    {
        var newDevices = new List<MMDevice>();
        var newSessions = new Dictionary<string, SessionEntry>();
        try
        {
            foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                newDevices.Add(device);
                try
                {
                    var manager = device.AudioSessionManager;
                    manager.RefreshSessions();
                    var sessions = manager.Sessions;
                    for (var i = 0; i < sessions.Count; i++)
                    {
                        var control = sessions[i];
                        try
                        {
                            if (control.IsSystemSoundsSession || control.State == AudioSessionState.AudioSessionStateExpired)
                                continue;
                            var pid = control.GetProcessID;
                            var exe = ExeForPid(pid);
                            if (exe is null)
                                continue;
                            var id = device.ID + "|" + control.GetSessionInstanceIdentifier;
                            newSessions[id] = new SessionEntry(id, exe, pid, control);
                        }
                        catch (Exception) { }
                    }
                }
                catch (Exception) { }
            }
        }
        catch (Exception)
        {
            return; // Audio-Subsystem gerade nicht verfügbar – beim nächsten Tick erneut versuchen
        }

        var added = newSessions.Values.Where(s => !_sessions.ContainsKey(s.Id)).Select(s => s.Exe)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var removedAny = _sessions.Keys.Any(k => !newSessions.ContainsKey(k));

        var oldDevices = _devices;
        var oldSessions = _sessions;
        _devices = newDevices;
        _sessions = newSessions;
        foreach (var s in oldSessions.Values)
        {
            try { s.Control.Dispose(); } catch (Exception) { }
        }
        foreach (var d in oldDevices)
        {
            try { d.Dispose(); } catch (Exception) { }
        }

        // PID-Cache klein halten
        var livePids = newSessions.Values.Select(s => s.Pid).ToHashSet();
        foreach (var pid in _exeByPid.Keys.Where(p => !livePids.Contains(p)).ToList())
            _exeByPid.Remove(pid);

        if (!raiseEvents)
            return;
        foreach (var exe in added)
            SessionAdded?.Invoke(exe);
        if (added.Count > 0 || removedAny)
            SessionsChanged?.Invoke();
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

    public void Dispose()
    {
        _pollTimer.Stop();
        _fadeTimer.Stop();
        foreach (var s in _sessions.Values)
        {
            try { s.Control.Dispose(); } catch (Exception) { }
        }
        foreach (var d in _devices)
        {
            try { d.Dispose(); } catch (Exception) { }
        }
        _enumerator.Dispose();
    }
}
