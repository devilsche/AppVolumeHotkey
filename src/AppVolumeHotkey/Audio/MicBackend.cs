using System.IO;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Interop;

namespace AppVolumeHotkey.Audio;

/// <summary>
/// Mikrofone in Windows stummschalten: ganzes Gerät (wirkt für alle Programme) oder nur die
/// Aufnahme-Sessions einzelner Apps (z. B. nur Edge für Meet).
/// </summary>
public sealed class MicBackend : IMicBackend
{
    private static Guid _noContext = Guid.Empty;
    private readonly IMMDeviceEnumerator? _enumerator;
    private readonly bool _fake;
    private readonly Dictionary<MicScope, bool> _fakeMuted = new();

    public MicBackend(bool fake = false)
    {
        _fake = fake;
        if (!fake)
        {
            try { _enumerator = CoreAudio.CreateEnumerator(); }
            catch (Exception e) { Platform.DebugLog.Write("Mikrofon: Geräteliste nicht verfügbar: " + e.Message); }
        }
    }

    public bool? IsDeviceMuted(MicScope scope)
    {
        if (_fake)
            return _fakeMuted.GetValueOrDefault(scope);
        var states = Endpoints(scope).Select(v => v.GetMute(out var m) == CoreAudio.S_OK ? m : (bool?)null).Where(m => m is not null).ToList();
        if (states.Count == 0)
            return null;
        return states.All(m => m == true); // „stumm“ erst, wenn alle betroffenen Mikrofone stumm sind
    }

    public void SetDeviceMuted(MicScope scope, bool muted)
    {
        if (_fake)
        {
            _fakeMuted[scope] = muted;
            return;
        }
        foreach (var v in Endpoints(scope))
        {
            try { v.SetMute(muted, ref _noContext); }
            catch (Exception) { }
        }
    }

    public void SetAppsMuted(IEnumerable<string> exes, bool muted)
    {
        if (_fake || _enumerator is null)
            return;
        var wanted = new HashSet<string>(exes, StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
            return;
        try
        {
            if (_enumerator.EnumAudioEndpoints(EDataFlow.eCapture, CoreAudio.DEVICE_STATE_ACTIVE, out var coll) != CoreAudio.S_OK || coll is null)
                return;
            coll.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                if (coll.Item(i, out var dev) != CoreAudio.S_OK || dev is null)
                    continue;
                var iid = CoreAudio.IID_IAudioSessionManager2;
                if (dev.Activate(ref iid, CoreAudio.CLSCTX_ALL, IntPtr.Zero, out var obj) != CoreAudio.S_OK || obj is not IAudioSessionManager2 manager)
                    continue;
                if (manager.GetSessionEnumerator(out var sessions) != CoreAudio.S_OK || sessions is null)
                    continue;
                sessions.GetCount(out var n);
                for (var s = 0; s < n; s++)
                {
                    try
                    {
                        if (sessions.GetSession(s, out var control) != CoreAudio.S_OK || control is null)
                            continue;
                        if (control.GetProcessId(out var pid) < 0 || pid == 0)
                            continue;
                        var path = Native.GetProcessImagePath(pid);
                        if (path is null || !wanted.Contains(Path.GetFileName(path)))
                            continue;
                        if (control is ISimpleAudioVolume volume)
                        {
                            volume.GetMute(out var current);
                            if (current != muted)
                                volume.SetMute(muted, ref _noContext);
                        }
                    }
                    catch (Exception) { }
                }
            }
        }
        catch (Exception e)
        {
            Platform.DebugLog.Write("Mikrofon (Apps): " + e.Message);
        }
    }

    private List<IAudioEndpointVolume> Endpoints(MicScope scope)
    {
        var list = new List<IAudioEndpointVolume>();
        if (_enumerator is null)
            return list;
        try
        {
            if (scope == MicScope.DefaultMicrophone)
            {
                if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, CoreAudio.eConsole, out var dev) == CoreAudio.S_OK && dev is not null
                    && Activate(dev) is { } v)
                    list.Add(v);
                return list;
            }
            if (_enumerator.EnumAudioEndpoints(EDataFlow.eCapture, CoreAudio.DEVICE_STATE_ACTIVE, out var coll) != CoreAudio.S_OK || coll is null)
                return list;
            coll.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                if (coll.Item(i, out var dev) == CoreAudio.S_OK && dev is not null && Activate(dev) is { } v)
                    list.Add(v);
            }
        }
        catch (Exception e)
        {
            Platform.DebugLog.Write("Mikrofon: Abfrage fehlgeschlagen: " + e.Message);
        }
        return list;
    }

    private static IAudioEndpointVolume? Activate(IMMDevice device)
    {
        var iid = CoreAudio.IID_IAudioEndpointVolume;
        return device.Activate(ref iid, CoreAudio.CLSCTX_ALL, IntPtr.Zero, out var obj) == CoreAudio.S_OK ? obj as IAudioEndpointVolume : null;
    }
}
