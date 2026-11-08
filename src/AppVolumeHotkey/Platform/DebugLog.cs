using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace AppVolumeHotkey.Platform;

/// <summary>
/// Kleines Diagnose-Log in %APPDATA%\AppVolumeHotkey\debug.log (max. ~1 MB, danach neu begonnen).
/// Enthält nur technische Ereignisse, keine Eingaben.
/// </summary>
public static class DebugLog
{
    private static readonly object Lock = new();

    public static string Path => global::System.IO.Path.Combine(AppPaths.DataDir, "debug.log");

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(AppPaths.DataDir);
                var info = new FileInfo(Path);
                if (info.Exists && info.Length > 1_000_000)
                    info.Delete();
                File.AppendAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] v{AppInfo.Version} {message}\n");
            }
        }
        catch (Exception) { }
    }

    /// <summary>Hintergrund-Wächter: meldet, wenn der UI-Thread länger als 500 ms nicht reagiert.</summary>
    public static void StartUiWatchdog(Dispatcher dispatcher)
    {
        var thread = new Thread(() =>
        {
            while (!dispatcher.HasShutdownStarted)
            {
                var started = DateTime.Now;
                var op = dispatcher.BeginInvoke(DispatcherPriority.Send, () => { });
                if (op.Wait(TimeSpan.FromMilliseconds(500)) != DispatcherOperationStatus.Completed)
                {
                    op.Wait(TimeSpan.FromSeconds(30));
                    Write($"UI hing ca. {(DateTime.Now - started).TotalMilliseconds:F0} ms");
                }
                Thread.Sleep(1000);
            }
        })
        { IsBackground = true, Name = "AppVolumeHotkey.Watchdog", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }
}
