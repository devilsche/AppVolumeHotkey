using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Interop;

namespace AppVolumeHotkey.Audio;

/// <summary>Mikrofone (Aufnahmegeräte) in Windows stumm/an schalten – wirkt für alle Programme.</summary>
public sealed class MicBackend : IMicBackend
{
    private static Guid _noContext = Guid.Empty;
    private readonly IMMDeviceEnumerator? _enumerator;

    public MicScope Scope { get; set; } = MicScope.AllMicrophones;

    private readonly bool _fake;

    public MicBackend(bool fake = false)
    {
        _fake = fake;
        if (!fake)
        {
            try { _enumerator = CoreAudio.CreateEnumerator(); }
            catch (Exception e) { Platform.DebugLog.Write("Mikrofon: Geräteliste nicht verfügbar: " + e.Message); }
        }
    }

    // Nur für Screenshots
    private bool _fakeMuted;

    public bool? IsMuted()
    {
        if (_fake)
            return _fakeMuted;
        if (_enumerator is null)
            return null;
        var states = Endpoints().Select(v => v.GetMute(out var m) == CoreAudio.S_OK ? m : (bool?)null).Where(m => m is not null).ToList();
        if (states.Count == 0)
            return null;
        return states.All(m => m == true); // „stumm“ erst, wenn alle betroffenen Mikrofone stumm sind
    }

    public void SetMuted(bool muted)
    {
        if (_fake)
        {
            _fakeMuted = muted;
            return;
        }
        foreach (var v in Endpoints())
        {
            try { v.SetMute(muted, ref _noContext); }
            catch (Exception) { }
        }
    }

    private List<IAudioEndpointVolume> Endpoints()
    {
        var list = new List<IAudioEndpointVolume>();
        if (_enumerator is null)
            return list;
        try
        {
            if (Scope == MicScope.DefaultMicrophone)
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
