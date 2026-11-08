using System.Windows.Input;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Input;

public static class KeyNames
{
    public static string Format(Hotkey? hotkey)
    {
        if (hotkey is null)
            return "kein Hotkey";
        var key = KeyName(KeyInterop.KeyFromVirtualKey((int)hotkey.VirtualKey));
        return hotkey.Modifiers == HotkeyModifiers.None ? key : FormatModifiers(hotkey.Modifiers) + "+" + key;
    }

    public static string FormatModifiers(HotkeyModifiers m)
    {
        var parts = new List<string>();
        if (m.HasFlag(HotkeyModifiers.Control)) parts.Add("Strg");
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
        Key.Space => "Leertaste",
        Key.Return => "Enter",
        Key.Back => "Rücktaste",
        Key.Delete => "Entf",
        Key.Insert => "Einfg",
        Key.Home => "Pos1",
        Key.End => "Ende",
        Key.PageUp => "Bild↑",
        Key.PageDown => "Bild↓",
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Left => "←",
        Key.Right => "→",
        Key.Escape => "Esc",
        Key.Pause => "Pause",
        Key.Scroll => "Rollen",
        Key.PrintScreen => "Druck",
        Key.OemPlus => "+",
        Key.OemMinus => "-",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.MediaPlayPause => "Play/Pause",
        Key.MediaNextTrack => "Nächster Titel",
        Key.MediaPreviousTrack => "Vorheriger Titel",
        Key.MediaStop => "Stopp",
        Key.VolumeMute => "Stumm",
        Key.VolumeUp => "Lauter",
        Key.VolumeDown => "Leiser",
        _ => key.ToString(),
    };
}
