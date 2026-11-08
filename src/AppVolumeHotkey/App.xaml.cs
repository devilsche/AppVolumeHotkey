using System.Windows;
using AppVolumeHotkey.Core.Persistence;
using AppVolumeHotkey.Platform;

namespace AppVolumeHotkey;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeMode = ThemeMode.System;

        DispatcherUnhandledException += (_, args) =>
        {
            ErrorLog.Write(args.Exception);
            args.Handled = true; // Tray-App weiterlaufen lassen
            _host?.EmergencyReset();
            MessageBox.Show("Ein unerwarteter Fehler ist aufgetreten. Die Lautstärken wurden zurückgesetzt.\n\n" + args.Exception.Message
                + "\n\nDetails: " + ErrorLog.Path, "AppVolumeHotkey", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("AppVolumeHotkey läuft bereits mit Administratorrechten. Öffne es über das Symbol im Infobereich.",
                    "AppVolumeHotkey", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                _instance.SignalFirstInstance();
            Shutdown();
            return;
        }

        var isAutostart = e.Args.Contains(Autostart.AutostartArg, StringComparer.OrdinalIgnoreCase);
        var settings = ConfigStore.Load(AppPaths.ConfigFile).Settings;
        var elevated = Elevation.IsElevated;
        var elevationDeclined = false;

        if (settings.RunAsAdmin && !elevated)
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

        try { Autostart.Apply(settings, elevated); }
        catch (Exception) { /* Autostart ist nicht kritisch */ }

        DebugLog.Write($"Start (elevated={elevated}, autostart={isAutostart})");
        DebugLog.StartUiWatchdog(Dispatcher);
        _host = new AppHost();
        _instance.ListenForShowRequests(() => Dispatcher.BeginInvoke(() => _host?.ShowMainWindow()));

        if (elevationDeclined)
            _host.Tray.ShowNotification("Ohne Adminrechte gestartet",
                "Hotkeys kommen nicht an, solange eine Anwendung mit Administratorrechten im Vordergrund ist.");

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
