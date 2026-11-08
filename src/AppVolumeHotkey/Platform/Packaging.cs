using System.Runtime.InteropServices;
using System.Text;

namespace AppVolumeHotkey.Platform;

/// <summary>Erkennt, ob die App als MSIX-Paket (Microsoft Store) läuft.</summary>
public static class Packaging
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;
    public const string StartupTaskId = "AppVolumeHotkeyStartup";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, StringBuilder? name);

    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            var len = 0;
            return GetCurrentPackageFullName(ref len, null) != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Wurde die Store-App durch die Autostart-Aufgabe von Windows gestartet?</summary>
    public static bool LaunchedByStartupTask()
    {
        if (!IsPackaged)
            return false;
        try
        {
            var args = global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            return args?.Kind == global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
