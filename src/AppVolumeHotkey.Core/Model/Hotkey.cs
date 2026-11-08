namespace AppVolumeHotkey.Core.Model;

/// <summary>Modifier-Flags; Werte entsprechen den Win32-MOD_*-Konstanten.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public sealed record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey);
