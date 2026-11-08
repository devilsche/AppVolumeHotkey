using System.Threading;

namespace AppVolumeHotkey.Platform;

/// <summary>Einzelinstanz per Mutex; eine zweite Instanz bittet die erste, ihr Fenster zu zeigen.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\AppVolumeHotkey";
    private const string EventName = @"Local\AppVolumeHotkey.Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private bool _owned;
    private RegisteredWaitHandle? _wait;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
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

    public void SignalFirstInstance() => _showEvent.Set();

    public void ListenForShowRequests(Action onShow) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false);

    /// <summary>Gibt den Mutex frei (z. B. vor einem Neustart mit Adminrechten).</summary>
    public void Release()
    {
        if (_owned)
        {
            _mutex.ReleaseMutex();
            _owned = false;
        }
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        Release();
        _mutex.Dispose();
        _showEvent.Dispose();
    }
}
