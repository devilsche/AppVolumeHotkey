using System.Windows;
using System.Windows.Controls;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.Tray;

public sealed class TrayController : IDisposable
{
    private readonly AppHost _host;
    private readonly NotifyIcon _icon;

    public TrayController(AppHost host)
    {
        _host = host;
        var ico = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _icon = new NotifyIcon(ico, "AppVolumeHotkey " + Platform.AppInfo.Version);
        _icon.SetActiveIcon(Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app-active.ico"))!.Stream);
        _icon.LeftClick += () => _host.ShowMainWindow();
        _icon.ContextMenu.Opened += (_, _) => Platform.DebugLog.Write("Tray: Menü geöffnet");
        Rebuild();
        L.Changed += Rebuild;
    }

    public void ShowNotification(string title, string message) => _icon.ShowBalloon(title, message);

    /// <summary>Menü neu aufbauen (bei jeder Zustands- oder Konfigurationsänderung).</summary>
    public void Rebuild()
    {
        var menu = _icon.ContextMenu;
        menu.Items.Clear();

        var active = _host.ActiveProfileDescriptions().ToList();
        if (active.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = L.T("Tray_NoneActive"), IsEnabled = false });
        }
        else
        {
            foreach (var line in active)
                menu.Items.Add(new MenuItem { Header = "● " + line, IsEnabled = false });
        }
        menu.Items.Add(new Separator());

        var reset = new MenuItem { Header = L.T("Tray_ResetAll"), IsEnabled = active.Count > 0 };
        reset.Click += (_, _) => _host.Volume.ResetAll();
        menu.Items.Add(reset);

        var pause = new MenuItem { Header = L.T("Tray_Pause"), IsCheckable = true, IsChecked = _host.HotkeysPaused };
        pause.Click += (_, _) => _host.HotkeysPaused = pause.IsChecked;
        menu.Items.Add(pause);

        menu.Items.Add(new Separator());
        var open = new MenuItem { Header = L.T("Tray_Open"), FontWeight = FontWeights.SemiBold };
        open.Click += (_, _) => _host.ShowMainWindow();
        menu.Items.Add(open);

        var about = new MenuItem { Header = L.T("Tray_About") };
        about.Click += (_, _) => UI.AboutWindow.Show(null);
        menu.Items.Add(about);

        var exit = new MenuItem { Header = L.T("Tray_Exit") };
        exit.Click += (_, _) => _host.Exit();
        menu.Items.Add(exit);

        _icon.SetActive(active.Count > 0 && _host.Config.Settings.TrayBadge);
        _icon.ToolTip = active.Count == 0 ? $"AppVolumeHotkey {Platform.AppInfo.Version}" : "AppVolumeHotkey – " + string.Join(", ", active);
    }

    public void Dispose() => _icon.Dispose();
}
