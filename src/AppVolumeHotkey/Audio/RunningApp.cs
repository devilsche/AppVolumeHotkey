using System.Windows.Media;

namespace AppVolumeHotkey.Audio;

public sealed record RunningApp(string Exe, string DisplayName, ImageSource? Icon);
