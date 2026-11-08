using System.IO;

namespace AppVolumeHotkey.Platform;

public static class ErrorLog
{
    public static string Path => global::System.IO.Path.Combine(AppPaths.DataDir, "error.log");

    public static void Write(Exception e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.AppendAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e}\n\n");
        }
        catch (Exception) { }
    }
}
