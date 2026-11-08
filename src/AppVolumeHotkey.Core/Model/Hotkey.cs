using System.Text.Json.Serialization;

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

/// <summary>Virtual-Key-Codes der erlaubten Maustasten (linke/rechte Taste bewusst ausgeschlossen).</summary>
public static class MouseButtons
{
    public const uint Middle = 0x04; // VK_MBUTTON
    public const uint X1 = 0x05;     // VK_XBUTTON1 („Maus 4“, meist Zurück)
    public const uint X2 = 0x06;     // VK_XBUTTON2 („Maus 5“, meist Vor)
}

/// <summary>Tastatur- oder Maus-Hotkey. Maustasten nutzen dieselben Virtual-Key-Codes wie Windows.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    [JsonIgnore]
    public bool IsMouse => VirtualKey is MouseButtons.Middle or MouseButtons.X1 or MouseButtons.X2;
}

public static class HotkeyMatch
{
    /// <summary>Id des Eintrags mit genau diesen Modifiern und dieser Taste, sonst null.</summary>
    public static int? Find(IEnumerable<(int Id, Hotkey Hotkey)> registered, HotkeyModifiers modifiers, uint virtualKey)
    {
        foreach (var (id, hk) in registered)
        {
            if (hk.VirtualKey == virtualKey && hk.Modifiers == modifiers)
                return id;
        }
        return null;
    }
}
