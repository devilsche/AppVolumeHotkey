using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Overlay;

public sealed class OverlayService
{
    private OverlayWindow? _window;

    public void Handle(HotkeyResult r, AppSettings settings)
    {
        if (r.ShowOverlay)
        {
            _window ??= new OverlayWindow();
            _window.ShowResult(r, settings);
        }
        if (r.PlaySound)
            SoundBank.Play(r.Profile.SoundVariant, r.IsActive);
    }

    public void Close() => _window?.Close();
}
