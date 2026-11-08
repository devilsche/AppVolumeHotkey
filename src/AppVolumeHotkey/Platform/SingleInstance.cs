using System.Threading;

namespace AppVolumeHotkey.Platform;

/// <summary>Einzelinstanz per Mutex; eine zweite Instanz bittet die erste, ihr Fenster zu zeigen.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\AppVolumeHotkey";
    private const string EventName = @"Local\AppVolumeHotkey.Show";

    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _showEvent;
    private bool _owned;
    private RegisteredWaitHandle? _wait;

    public SingleInstance()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: false, MutexName);
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        }
        catch (UnauthorizedAccessException)
        {
            // Die erste Instanz läuft mit Adminrechten; wir dürfen ihre Objekte nicht öffnen
            OtherInstanceIsElevated = true;
            return;
        }
        try
        {
            _owned = _mutex.WaitOne(TimeSpan.FromSeconds(3));
        }
        catch (AbandonedMutexException)
        {
            _owned = true;
        }
    }

    public bool IsFirst => _owned;

    /// <summary>Eine andere Instanz läuft mit Adminrechten und kann nicht angesprochen werden.</summary>
    public bool OtherInstanceIsElevated { get; }

    public void SignalFirstInstance() => _showEvent?.Set();

    public void ListenForShowRequests(Action onShow)
    {
        if (_showEvent is not null)
            _wait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Gibt den Mutex frei (z. B. vor einem Neustart mit Adminrechten).</summary>
    public void Release()
    {
        if (_owned && _mutex is not null)
        {
            _mutex.ReleaseMutex();
            _owned = false;
        }
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        Release();
        _mutex?.Dispose();
        _showEvent?.Dispose();
    }
}
