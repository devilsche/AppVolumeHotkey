using System.Diagnostics;
using Microsoft.Win32;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Platform;

/// <summary>
/// Autostart ohne Admin über den Run-Schlüssel, mit Admin über eine Aufgabe der Aufgabenplanung
/// („mit höchsten Privilegien“), weil der Run-Schlüssel keine Erhöhung ohne UAC-Abfrage erlaubt.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "AppVolumeHotkey";
    public const string AutostartArg = "--autostart";

    public static void Apply(AppSettings settings, bool elevated)
    {
        var command = $"\"{AppPaths.ExePath}\" {AutostartArg}";

        if (!settings.StartWithWindows)
        {
            SetRunValue(null);
            if (elevated)
                RunSchtasks("/Delete", "/TN", Name, "/F");
            return;
        }

        if (settings.RunAsAdmin)
        {
            if (!elevated)
                return; // wird beim nächsten Start mit Adminrechten eingerichtet
            if (RunSchtasks("/Create", "/TN", Name, "/TR", command, "/SC", "ONLOGON", "/RL", "HIGHEST", "/F"))
                SetRunValue(null);
        }
        else
        {
            SetRunValue(command);
            if (elevated)
                RunSchtasks("/Delete", "/TN", Name, "/F");
        }
    }

    private static void SetRunValue(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (command is null)
            key.DeleteValue(Name, throwOnMissingValue: false);
        else
            key.SetValue(Name, command);
    }

    private static bool RunSchtasks(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.WaitForExit(10_000);
            return p.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
