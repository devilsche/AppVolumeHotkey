using System.Media;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Overlay;

public sealed class OverlayService
{
    private OverlayWindow? _window;

    public static string FormatMessage(HotkeyResult r) => r.IsActive
        ? $"🔉 {r.Profile.Name} · {r.Profile.StepLabel(r.Step)} · AKTIV"
        : $"🔊 {r.Profile.Name} · Ausgangswert · AUS";

    public void Handle(HotkeyResult r, AppSettings settings)
    {
        if (r.ShowOverlay)
        {
            _window ??= new OverlayWindow();
            _window.ShowMessage(FormatMessage(r), settings);
        }
        if (r.PlaySound)
        {
            if (r.IsActive)
                SystemSounds.Asterisk.Play();
            else
                SystemSounds.Beep.Play();
        }
    }

    public void Close() => _window?.Close();
}
