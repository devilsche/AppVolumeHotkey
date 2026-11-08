using System.Windows.Interop;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Interop;

namespace AppVolumeHotkey.Input;

/// <summary>Globale Tastatur-Hotkeys über RegisterHotKey auf einem unsichtbaren Message-Window.</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int ProbeId = 0xBFFF;
    private readonly HwndSource _source;
    private readonly HashSet<int> _registered = new();
    private readonly MouseHook _mouse;

    public event Action<int>? Pressed;

    /// <summary>Hotkey losgelassen (für „Halten“-Profile).</summary>
    public event Action<int>? Released;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);

    private readonly Dictionary<int, uint> _vkById = new();
    private readonly Dictionary<int, uint> _held = new();
    private readonly System.Windows.Threading.DispatcherTimer _releaseTimer;

    public HotkeyManager()
    {
        var p = new HwndSourceParameters("AppVolumeHotkey.Hotkeys")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            WindowStyle = 0,
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
        _mouse = new MouseHook(System.Windows.Threading.Dispatcher.CurrentDispatcher);
        _mouse.Pressed += id => Pressed?.Invoke(id);
        _mouse.Released += id => Released?.Invoke(id);
        _releaseTimer = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(25),
            System.Windows.Threading.DispatcherPriority.Input, (_, _) => CheckReleased(), System.Windows.Threading.Dispatcher.CurrentDispatcher);
        _releaseTimer.Stop();
    }

    /// <param name="passThrough">Nur für Maustasten: Klick zusätzlich an andere Programme weitergeben.</param>
    public bool Register(int id, Hotkey hotkey, bool passThrough = false)
    {
        if (hotkey.IsMouse)
        {
            _mouse.Register(id, hotkey, passThrough);
            return true;
        }
        Unregister(id);
        if (!Native.RegisterHotKey(_source.Handle, id, (uint)hotkey.Modifiers | Native.MOD_NOREPEAT, hotkey.VirtualKey))
            return false;
        _registered.Add(id);
        _vkById[id] = hotkey.VirtualKey;
        return true;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id))
            Native.UnregisterHotKey(_source.Handle, id);
        _vkById.Remove(id);
        _held.Remove(id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.ToList())
            Unregister(id);
        _mouse.UnregisterAll();
    }

    /// <summary>Prüft, ob die Kombination systemweit frei ist (kurz registrieren und wieder freigeben).</summary>
    public bool IsAvailable(Hotkey hotkey)
    {
        if (hotkey.IsMouse)
            return true; // Maustasten kann man nicht exklusiv belegen
        if (!Native.RegisterHotKey(_source.Handle, ProbeId, (uint)hotkey.Modifiers | Native.MOD_NOREPEAT, hotkey.VirtualKey))
            return false;
        Native.UnregisterHotKey(_source.Handle, ProbeId);
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id != ProbeId)
            {
                if (_vkById.TryGetValue(id, out var vk))
                {
                    _held[id] = vk;  // Loslassen per Tastenzustand erkennen (RegisterHotKey meldet nur das Drücken)
                    _releaseTimer.Start();
                }
                Pressed?.Invoke(id);
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void CheckReleased()
    {
        foreach (var (id, vk) in _held.ToList())
        {
            if ((GetAsyncKeyState((int)vk) & 0x8000) == 0)
            {
                _held.Remove(id);
                Released?.Invoke(id);
            }
        }
        if (_held.Count == 0)
            _releaseTimer.Stop();
    }

    public void Dispose()
    {
        _releaseTimer.Stop();
        UnregisterAll();
        _mouse.Dispose();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
