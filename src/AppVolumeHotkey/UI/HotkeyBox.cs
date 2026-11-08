using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Input;

namespace AppVolumeHotkey.UI;

/// <summary>
/// Eingabefeld für Hotkeys: hineinklicken bzw. per Tab fokussieren und die Kombination drücken.
/// Esc = Feld unverändert verlassen, Entf/Rücktaste = Hotkey löschen, Tab = normal weiter.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    private const string Prompt = "Taste drücken …";
    private Hotkey? _hotkey;

    public event Action? HotkeyChanged;

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        AutomationProperties.SetHelpText(this, "Fokussieren und gewünschte Tastenkombination oder Maustaste (Mittelklick, Maus 4, Maus 5) drücken. Entf löscht den Hotkey.");
        UpdateText();
    }

    public Hotkey? Hotkey
    {
        get => _hotkey;
        set
        {
            _hotkey = value;
            UpdateText();
            HotkeyChanged?.Invoke();
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = Prompt;
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };
        var mods = CurrentModifiers();

        if (key == Key.Tab && mods == HotkeyModifiers.Shift)
            return; // Shift+Tab = rückwärts navigieren

        if (mods == HotkeyModifiers.None)
        {
            switch (key)
            {
                case Key.Tab:
                    return; // normale Fokus-Navigation
                case Key.Escape:
                    e.Handled = true;
                    UpdateText();
                    MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    return;
                case Key.Delete:
                case Key.Back:
                    e.Handled = true;
                    Hotkey = null;
                    Text = Prompt;
                    return;
            }
        }

        e.Handled = true;
        if (IsModifierKey(key))
        {
            Text = mods == HotkeyModifiers.None ? Prompt : KeyNames.FormatModifiers(mods) + "+ …";
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
            return;
        Hotkey = new Hotkey(mods, vk);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        // Erst per Linksklick fokussieren, dann Mittelklick oder Maus 4/5 drücken
        if (IsKeyboardFocusWithin)
        {
            uint? vk = e.ChangedButton switch
            {
                MouseButton.Middle => MouseButtons.Middle,
                MouseButton.XButton1 => MouseButtons.X1,
                MouseButton.XButton2 => MouseButtons.X2,
                _ => null,
            };
            if (vk is { } v)
            {
                e.Handled = true;
                Hotkey = new Hotkey(CurrentModifiers(), v);
                return;
            }
        }
        base.OnPreviewMouseDown(e);
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        // Maus 4/5 sonst als „Zurück/Vor“-Befehl weiterreichen
        if (e.ChangedButton is MouseButton.Middle or MouseButton.XButton1 or MouseButton.XButton2)
            e.Handled = true;
        base.OnPreviewMouseUp(e);
    }

    private static HotkeyModifiers CurrentModifiers()
    {
        var m = HotkeyModifiers.None;
        var k = Keyboard.Modifiers;
        if (k.HasFlag(ModifierKeys.Alt)) m |= HotkeyModifiers.Alt;
        if (k.HasFlag(ModifierKeys.Control)) m |= HotkeyModifiers.Control;
        if (k.HasFlag(ModifierKeys.Shift)) m |= HotkeyModifiers.Shift;
        if (k.HasFlag(ModifierKeys.Windows) || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) m |= HotkeyModifiers.Win;
        return m;
    }

    private static bool IsModifierKey(Key k) => k is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    private void UpdateText() => Text = _hotkey is null ? "kein Hotkey" : KeyNames.Format(_hotkey);
}
