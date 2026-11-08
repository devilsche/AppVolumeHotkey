using System.Diagnostics;
using System.Security;
using System.Text;
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
        if (Packaging.IsPackaged)
        {
            ApplyPackaged(settings.StartWithWindows);
            return;
        }

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
            if (CreateElevatedTask())
                SetRunValue(null);
        }
        else
        {
            SetRunValue(command);
            if (elevated)
                RunSchtasks("/Delete", "/TN", Name, "/F");
        }
    }

    /// <summary>Store-Version: Autostart über die im Paket deklarierte StartupTask (Windows verwaltet sie unter „Autostart-Apps“).</summary>
    private static void ApplyPackaged(bool enable)
    {
        try
        {
            var task = global::Windows.ApplicationModel.StartupTask.GetAsync(Packaging.StartupTaskId).AsTask().GetAwaiter().GetResult();
            if (enable && task.State == global::Windows.ApplicationModel.StartupTaskState.Disabled)
                task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
            else if (!enable && task.State == global::Windows.ApplicationModel.StartupTaskState.Enabled)
                task.Disable();
        }
        catch (Exception e)
        {
            DebugLog.Write("Autostart (Store) fehlgeschlagen: " + e.Message);
        }
    }

    /// <summary>Store-Version: aktueller Zustand (der Nutzer kann ihn auch in den Windows-Einstellungen ändern).</summary>
    public static bool? PackagedState()
    {
        if (!Packaging.IsPackaged)
            return null;
        try
        {
            var task = global::Windows.ApplicationModel.StartupTask.GetAsync(Packaging.StartupTaskId).AsTask().GetAwaiter().GetResult();
            return task.State is global::Windows.ApplicationModel.StartupTaskState.Enabled or global::Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool CreateElevatedTask()
    {
        var user = global::System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>AppVolumeHotkey beim Anmelden mit Administratorrechten starten</Description></RegistrationInfo>
  <Triggers>
    <LogonTrigger><Enabled>true</Enabled><UserId>{SecurityElement.Escape(user)}</UserId></LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author""><UserId>{SecurityElement.Escape(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec><Command>{SecurityElement.Escape(AppPaths.ExePath)}</Command><Arguments>{AutostartArg}</Arguments></Exec>
  </Actions>
</Task>";
        var file = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), $"AppVolumeHotkey-task-{Guid.NewGuid():N}.xml");
        try
        {
            global::System.IO.File.WriteAllText(file, xml, Encoding.Unicode);
            return RunSchtasks("/Create", "/TN", Name, "/XML", file, "/F");
        }
        finally
        {
            try { global::System.IO.File.Delete(file); } catch (Exception) { }
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
