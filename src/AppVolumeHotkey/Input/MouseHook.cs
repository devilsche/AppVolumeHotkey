using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Input;

/// <summary>
/// Globale Maus-Hotkeys (Mittelklick, Maus 4/5) über einen Low-Level-Hook (WH_MOUSE_LL).
/// Der Hook läuft auf einem eigenen Thread mit Nachrichtenschleife, damit ein beschäftigter UI-Thread
/// ihn nie ausbremst (Windows entfernt Hooks, die zu langsam antworten). Treffer werden an den
/// UI-Dispatcher weitergereicht. Der Hook ist nur installiert, solange Maus-Hotkeys registriert sind.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C;
    private const uint WM_QUIT = 0x0012;
    private const uint LLMHF_INJECTED = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int X, Y;
        public uint MouseData, Flags, Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam, LParam;
        public uint Time;
        public int PtX, PtY;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private readonly Dictionary<int, (Hotkey Hotkey, bool PassThrough)> _hotkeys = new();
    private readonly HashSet<uint> _swallowedDown = new(); // Taste, deren „Down“ verschluckt wurde → „Up“ auch verschlucken
    private readonly Dictionary<uint, int> _pressedIdByButton = new(); // für „Loslassen“-Meldung
    private HookProc? _proc; // Referenz halten, sonst sammelt der GC den Delegate ein
    private Thread? _thread;
    private uint _threadId;

    public event Action<int>? Pressed;

    public event Action<int>? Released;

    public MouseHook(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public void Register(int id, Hotkey hotkey, bool passThrough)
    {
        lock (_lock)
            _hotkeys[id] = (hotkey, passThrough);
        EnsureRunning();
    }

    public void UnregisterAll()
    {
        lock (_lock)
        {
            _hotkeys.Clear();
            _swallowedDown.Clear();
            _pressedIdByButton.Clear();
        }
        Stop();
    }

    private void EnsureRunning()
    {
        if (_thread is not null)
            return;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "AppVolumeHotkey.MouseHook" };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    private void Run(ManualResetEventSlim ready)
    {
        _threadId = GetCurrentThreadId();
        _proc = Callback;
        var hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        ready.Set();
        if (hook == IntPtr.Zero)
            return;
        try
        {
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
        }
        finally
        {
            UnhookWindowsHookEx(hook);
        }
    }

    private void Stop()
    {
        if (_thread is null)
            return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((data.Flags & LLMHF_INJECTED) == 0 && ButtonOf(msg, data.MouseData) is { } vk)
            {
                var isDown = msg is WM_MBUTTONDOWN or WM_XBUTTONDOWN;
                if (isDown ? HandleDown(vk) : HandleUp(vk))
                    return new IntPtr(1); // verschlucken
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private bool HandleDown(uint vk)
    {
        int? id;
        bool passThrough;
        lock (_lock)
        {
            id = HotkeyMatch.Find(_hotkeys.Select(kv => (kv.Key, kv.Value.Hotkey)), CurrentModifiers(), vk);
            if (id is null)
                return false;
            passThrough = _hotkeys[id.Value].PassThrough;
            if (!passThrough)
                _swallowedDown.Add(vk);
            _pressedIdByButton[vk] = id.Value;
        }
        var hit = id.Value;
        _dispatcher.BeginInvoke(() => Pressed?.Invoke(hit));
        return !passThrough;
    }

    private bool HandleUp(uint vk)
    {
        int? releasedId = null;
        bool swallow;
        lock (_lock)
        {
            swallow = _swallowedDown.Remove(vk);
            if (_pressedIdByButton.Remove(vk, out var id))
                releasedId = id;
        }
        if (releasedId is { } r)
            _dispatcher.BeginInvoke(() => Released?.Invoke(r));
        return swallow;
    }

    private static uint? ButtonOf(int msg, uint mouseData) => msg switch
    {
        WM_MBUTTONDOWN or WM_MBUTTONUP => MouseButtons.Middle,
        WM_XBUTTONDOWN or WM_XBUTTONUP => (mouseData >> 16) switch
        {
            1 => MouseButtons.X1,
            2 => MouseButtons.X2,
            _ => null,
        },
        _ => null,
    };

    private static HotkeyModifiers CurrentModifiers()
    {
        static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
        var m = HotkeyModifiers.None;
        if (Down(0x10)) m |= HotkeyModifiers.Shift;   // VK_SHIFT
        if (Down(0x11)) m |= HotkeyModifiers.Control; // VK_CONTROL
        if (Down(0x12)) m |= HotkeyModifiers.Alt;     // VK_MENU
        if (Down(0x5B) || Down(0x5C)) m |= HotkeyModifiers.Win;
        return m;
    }

    public void Dispose() => UnregisterAll();
}
