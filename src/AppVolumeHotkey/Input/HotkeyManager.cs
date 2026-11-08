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

    public event Action<int>? Pressed;

    public HotkeyManager()
    {
        var p = new HwndSourceParameters("AppVolumeHotkey.Hotkeys")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            WindowStyle = 0,
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    public bool Register(int id, Hotkey hotkey)
    {
        Unregister(id);
        if (!Native.RegisterHotKey(_source.Handle, id, (uint)hotkey.Modifiers | Native.MOD_NOREPEAT, hotkey.VirtualKey))
            return false;
        _registered.Add(id);
        return true;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id))
            Native.UnregisterHotKey(_source.Handle, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.ToList())
            Unregister(id);
    }

    /// <summary>Prüft, ob die Kombination systemweit frei ist (kurz registrieren und wieder freigeben).</summary>
    public bool IsAvailable(Hotkey hotkey)
    {
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
                Pressed?.Invoke(id);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
