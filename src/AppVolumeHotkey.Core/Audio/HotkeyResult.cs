using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Core.Audio;

public sealed record HotkeyResult(AppGroup Group, Profile Profile, Step Step, bool IsActive, bool ShowOverlay, bool PlaySound);
