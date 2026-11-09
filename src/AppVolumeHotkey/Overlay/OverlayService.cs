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

    public void HandleMic(MicResult r, MicSettings mic, AppSettings settings)
    {
        if (r.ShowOverlay)
        {
            _window ??= new OverlayWindow();
            _window.ShowMic(r.Muted, mic.Scope, settings);
        }
        if (r.PlaySound)
            SoundBank.Play(r.Sound, active: !r.Muted);
    }

    public void HandleDucking(bool on, AppSettings settings)
    {
        _window ??= new OverlayWindow();
        _window.ShowDucking(on, settings);
    }

    public void Close() => _window?.Close();
}
