using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace AppVolumeHotkey.Tray;

/// <summary>
/// Eigenes Symbol im Infobereich über Shell_NotifyIcon (ohne Fremdbibliothek).
/// Linksklick → <see cref="LeftClick"/>, Rechtsklick → WPF-Kontextmenü, Sprechblasen über <see cref="ShowBalloon"/>.
/// Wird Explorer neu gestartet, legt sich das Symbol automatisch wieder an.
/// </summary>
internal sealed class NotifyIcon : IDisposable
{
    private const int WM_APP_TRAY = 0x8000 + 1; // WM_APP + 1
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;

    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10, NIF_SHOWTIP = 0x80;
    private const uint NIIF_INFO = 0x1, NIIF_WARNING = 0x2, NIIF_LARGE_ICON = 0x20;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconFromResourceEx(byte[] data, uint size, bool icon, uint version, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private const int SM_CXSMICON = 49;

    private readonly HwndSource _window;
    private readonly uint _taskbarCreated;
    private readonly IntPtr _iconNormal;
    private IntPtr _iconActive;
    private IntPtr _icon;
    private string _tip;
    private bool _added;

    public event Action? LeftClick;

    public ContextMenu ContextMenu { get; } = new();

    public NotifyIcon(Stream icoStream, string tooltip)
    {
        _tip = tooltip;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _window = new HwndSource(new HwndSourceParameters("AppVolumeHotkey.Tray") { WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(WndProc);
        _iconNormal = _icon = LoadSmallIcon(icoStream);
        ContextMenu.Placement = PlacementMode.MousePoint;
        Add();
    }

    public string ToolTip
    {
        get => _tip;
        set
        {
            _tip = value.Length > 127 ? value[..126] + "…" : value;
            var d = Data(NIF_TIP | NIF_SHOWTIP);
            Shell_NotifyIcon(NIM_MODIFY, ref d);
        }
    }

    /// <summary>Optionales zweites Symbol (z. B. mit grünem Punkt) für den Zustand „aktiv“.</summary>
    public void SetActiveIcon(Stream ico) => _iconActive = LoadSmallIcon(ico);

    public void SetActive(bool active)
    {
        var target = active && _iconActive != IntPtr.Zero ? _iconActive : _iconNormal;
        if (target == _icon)
            return;
        _icon = target;
        var d = Data(NIF_ICON);
        Shell_NotifyIcon(NIM_MODIFY, ref d);
    }

    public void ShowBalloon(string title, string text, bool warning = false)
    {
        var d = Data(NIF_INFO);
        d.szInfoTitle = title.Length > 63 ? title[..63] : title;
        d.szInfo = text.Length > 255 ? text[..255] : text;
        d.dwInfoFlags = (warning ? NIIF_WARNING : NIIF_INFO) | NIIF_LARGE_ICON;
        Shell_NotifyIcon(NIM_MODIFY, ref d);
    }

    private NOTIFYICONDATA Data(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = _tip,
        szInfo = "",
        szInfoTitle = "",
    };

    private void Add()
    {
        var d = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref d);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_APP_TRAY)
        {
            switch (lParam.ToInt32() & 0xFFFF)
            {
                case WM_LBUTTONUP:
                    LeftClick?.Invoke();
                    break;
                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    ShowMenu();
                    break;
            }
            handled = true;
        }
        else if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
        {
            Add(); // Explorer wurde neu gestartet
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        Platform.DebugLog.Write("Tray: Rechtsklick empfangen");
        ContextMenu.IsOpen = true;
        // Direkt danach das Popup-Fenster des Menüs in den Vordergrund holen (nicht vorher ein anderes Fenster –
        // ein Fokuswechsel nach dem Öffnen schließt WPF-Popups sofort wieder). So schließt ein Klick daneben das Menü.
        if (PresentationSource.FromVisual(ContextMenu) is HwndSource src)
        {
            var ok = SetForegroundWindow(src.Handle);
            Platform.DebugLog.Write($"Tray: Menü-Fenster in Vordergrund: {ok}");
        }
    }

    /// <summary>Wählt aus der .ico-Datei den passenden Eintrag für die kleine Symbolgröße (DPI-abhängig).</summary>
    private static IntPtr LoadSmallIcon(Stream ico)
    {
        using var ms = new MemoryStream();
        ico.CopyTo(ms);
        var bytes = ms.ToArray();
        var want = Math.Max(16, GetSystemMetrics(SM_CXSMICON));
        int count = BitConverter.ToUInt16(bytes, 4);
        int bestOffset = -1, bestSize = 0, bestDim = int.MaxValue;
        for (var i = 0; i < count; i++)
        {
            var e = 6 + i * 16;
            int w = bytes[e] == 0 ? 256 : bytes[e];
            var size = BitConverter.ToInt32(bytes, e + 8);
            var offset = BitConverter.ToInt32(bytes, e + 12);
            // kleinster Eintrag >= gewünschter Größe, sonst der größte
            var score = w >= want ? w - want : 1000 - w;
            if (score < bestDim)
            {
                bestDim = score;
                bestOffset = offset;
                bestSize = size;
            }
        }
        if (bestOffset < 0)
            return IntPtr.Zero;
        var entry = new byte[bestSize];
        Array.Copy(bytes, bestOffset, entry, 0, bestSize);
        return CreateIconFromResourceEx(entry, (uint)bestSize, true, 0x00030000, want, want, 0);
    }

    public void Dispose()
    {
        if (_added)
        {
            var d = Data(0);
            Shell_NotifyIcon(NIM_DELETE, ref d);
            _added = false;
        }
        if (_iconNormal != IntPtr.Zero)
            DestroyIcon(_iconNormal);
        if (_iconActive != IntPtr.Zero)
            DestroyIcon(_iconActive);
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }
}
