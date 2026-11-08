using System.IO;

namespace AppVolumeHotkey.Platform;

public static class AppPaths
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppVolumeHotkey");

    public static string ConfigFile => Path.Combine(DataDir, "config.json");
    public static string StateFile => Path.Combine(DataDir, "state.json");

    public static string ExePath => Environment.ProcessPath ?? throw new InvalidOperationException("Pfad der exe unbekannt.");
}
