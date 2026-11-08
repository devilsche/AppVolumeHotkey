using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace AppVolumeHotkey.Platform;

public static class Elevation
{
    public static bool IsElevated
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>Startet die App mit Adminrechten neu. false, wenn der Nutzer die UAC-Abfrage abgelehnt hat.</summary>
    public static bool TryRelaunchElevated(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(AppPaths.ExePath)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        try
        {
            Process.Start(psi);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
