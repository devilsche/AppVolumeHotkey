using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace AppVolumeHotkey.Tray;

public sealed class TrayController : IDisposable
{
    private readonly AppHost _host;
    private readonly TaskbarIcon _icon;

    public TrayController(AppHost host)
    {
        _host = host;
        _icon = new TaskbarIcon
        {
            ToolTipText = "AppVolumeHotkey " + Platform.AppInfo.Version,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            NoLeftClickDelay = true,
            ContextMenu = new ContextMenu(),
        };
        _icon.TrayLeftMouseUp += (_, _) => _host.ShowMainWindow();
        _icon.ForceCreate(enablesEfficiencyMode: false);
        Rebuild();
    }

    public void ShowNotification(string title, string message) => _icon.ShowNotification(title, message);

    /// <summary>Menü neu aufbauen (bei jeder Zustands- oder Konfigurationsänderung).</summary>
    public void Rebuild()
    {
        var menu = _icon.ContextMenu!;
        menu.Items.Clear();

        var active = _host.ActiveProfileDescriptions().ToList();
        if (active.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "Kein Profil aktiv", IsEnabled = false });
        }
        else
        {
            foreach (var line in active)
                menu.Items.Add(new MenuItem { Header = "● " + line, IsEnabled = false });
        }
        menu.Items.Add(new Separator());

        var reset = new MenuItem { Header = "Alle _zurücksetzen", IsEnabled = active.Count > 0 };
        reset.Click += (_, _) => _host.Volume.ResetAll();
        menu.Items.Add(reset);

        var pause = new MenuItem { Header = "Hotkeys _pausieren", IsCheckable = true, IsChecked = _host.HotkeysPaused };
        pause.Click += (_, _) => _host.HotkeysPaused = pause.IsChecked;
        menu.Items.Add(pause);

        menu.Items.Add(new Separator());
        var open = new MenuItem { Header = "_Öffnen", FontWeight = FontWeights.SemiBold };
        open.Click += (_, _) => _host.ShowMainWindow();
        menu.Items.Add(open);

        var about = new MenuItem { Header = "Ü_ber" };
        about.Click += (_, _) => UI.AboutWindow.Show(null);
        menu.Items.Add(about);

        var exit = new MenuItem { Header = "_Beenden" };
        exit.Click += (_, _) => _host.Exit();
        menu.Items.Add(exit);

        var tip = active.Count == 0 ? $"AppVolumeHotkey {Platform.AppInfo.Version}" : "AppVolumeHotkey – " + string.Join(", ", active);
        _icon.ToolTipText = tip.Length > 120 ? tip[..117] + "…" : tip;
    }

    public void Dispose() => _icon.Dispose();
}
