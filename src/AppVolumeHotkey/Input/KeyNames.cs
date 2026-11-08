using System.Windows.Input;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.Input;

public static class KeyNames
{
    public static string Format(Hotkey? hotkey)
    {
        if (hotkey is null)
            return L.T("Key_None");
        var key = hotkey.VirtualKey switch
        {
            MouseButtons.Middle => L.T("Key_Middle"),
            MouseButtons.X1 => L.T("Key_Mouse4"),
            MouseButtons.X2 => L.T("Key_Mouse5"),
            _ => KeyName(KeyInterop.KeyFromVirtualKey((int)hotkey.VirtualKey)),
        };
        return hotkey.Modifiers == HotkeyModifiers.None ? key : FormatModifiers(hotkey.Modifiers) + "+" + key;
    }

    public static string FormatModifiers(HotkeyModifiers m)
    {
        var parts = new List<string>();
        if (m.HasFlag(HotkeyModifiers.Control)) parts.Add(L.T("Key_Ctrl"));
        if (m.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (m.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (m.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        return string.Join("+", parts);
    }

    public static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (int)(key - Key.NumPad0),
        Key.Multiply => "Num *",
        Key.Add => "Num +",
        Key.Subtract => "Num -",
        Key.Divide => "Num /",
        Key.Decimal => "Num ,",
        Key.Space => L.T("Key_Space"),
        Key.Return => "Enter",
        Key.Back => L.T("Key_Back"),
        Key.Delete => L.T("Key_Delete"),
        Key.Insert => L.T("Key_Insert"),
        Key.Home => L.T("Key_Home"),
        Key.End => L.T("Key_End"),
        Key.PageUp => L.T("Key_PageUp"),
        Key.PageDown => L.T("Key_PageDown"),
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Left => "←",
        Key.Right => "→",
        Key.Escape => "Esc",
        Key.Pause => "Pause",
        Key.Scroll => L.T("Key_Scroll"),
        Key.PrintScreen => L.T("Key_Print"),
        Key.OemPlus => "+",
        Key.OemMinus => "-",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.MediaPlayPause => "Play/Pause",
        Key.MediaNextTrack => L.T("Key_Next"),
        Key.MediaPreviousTrack => L.T("Key_Prev"),
        Key.MediaStop => L.T("Key_Stop"),
        Key.VolumeMute => L.T("Key_Mute"),
        Key.VolumeUp => L.T("Key_VolUp"),
        Key.VolumeDown => L.T("Key_VolDown"),
        _ => key.ToString(),
    };
}
