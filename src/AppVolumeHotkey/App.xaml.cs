using System.Windows;
using AppVolumeHotkey.Core.Persistence;
using AppVolumeHotkey.Platform;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeMode = ThemeMode.System;
        L.SetLanguage(L.Resolve("auto"));

        // Nur für den Build: Bilder aller Fenster erzeugen und beenden
        var snapIndex = Array.IndexOf(e.Args, "--ui-snapshots");
        if (snapIndex >= 0)
        {
            var dir = snapIndex + 1 < e.Args.Length ? e.Args[snapIndex + 1] : "snapshots";
            try
            {
                UI.Snapshots.Run(dir);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                Console.Error.WriteLine(ex);
                Shutdown(1);
                return;
            }
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            ErrorLog.Write(args.Exception);
            args.Handled = true; // Tray-App weiterlaufen lassen
            _host?.EmergencyReset();
            MessageBox.Show(L.F("Msg_Error", args.Exception.Message, ErrorLog.Path), "AppVolumeHotkey", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                ErrorLog.Write(ex);
            try { Dispatcher.Invoke(() => _host?.EmergencyReset()); } catch (Exception) { }
        };

        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            if (_instance.OtherInstanceIsElevated)
                MessageBox.Show(L.T("Msg_AlreadyElevated"),
                    "AppVolumeHotkey", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                _instance.SignalFirstInstance();
            Shutdown();
            return;
        }

        var isAutostart = e.Args.Contains(Autostart.AutostartArg, StringComparer.OrdinalIgnoreCase) || Packaging.LaunchedByStartupTask();
        var settings = ConfigStore.Load(AppPaths.ConfigFile).Settings;
        L.SetLanguage(L.Resolve(settings.Language));
        var elevated = Elevation.IsElevated;
        var elevationDeclined = false;

        // Store-Version: Admin-Neustart wird nicht angeboten (Paket-Apps laufen ohne Erhöhung)
        if (settings.RunAsAdmin && !elevated && !Packaging.IsPackaged)
        {
            _instance.Release();
            if (Elevation.TryRelaunchElevated(e.Args))
            {
                Shutdown();
                return;
            }
            elevationDeclined = true;
            _instance.Dispose();
            _instance = new SingleInstance();
        }

        // Store-Version: Zustand nicht beim Start erzwingen – der Nutzer kann ihn in Windows ändern
        if (!Packaging.IsPackaged)
        {
            try { Autostart.Apply(settings, elevated); }
            catch (Exception) { /* Autostart ist nicht kritisch */ }
        }

        DebugLog.Write($"Start (elevated={elevated}, autostart={isAutostart})");
        DebugLog.StartUiWatchdog(Dispatcher);
        _host = new AppHost();
        _instance.ListenForShowRequests(() => Dispatcher.BeginInvoke(() => _host?.ShowMainWindow()));

        if (elevationDeclined)
            _host.Tray.ShowNotification(L.T("Msg_NoAdminTitle"), L.T("Msg_NoAdmin"));

        if (!(isAutostart && settings.StartMinimized))
            _host.ShowMainWindow();
    }

    internal void ReleaseSingleInstance() => _instance?.Release();

    internal void ReacquireSingleInstance()
    {
        _instance?.Dispose();
        _instance = new SingleInstance();
        _instance.ListenForShowRequests(() => Dispatcher.BeginInvoke(() => _host?.ShowMainWindow()));
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Abmelden/Herunterfahren: Lautstärken zurücksetzen
        _host?.Exit();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
