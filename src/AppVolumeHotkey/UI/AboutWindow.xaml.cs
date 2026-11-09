using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using AppVolumeHotkey.Platform;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.UI;

public partial class AboutWindow : Window
{
    private static AboutWindow? _open;

    private AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = L.F("About_Version", AppInfo.Version);
        CopyrightText.Text = AppInfo.Copyright;
        RepoLink.NavigateUri = new Uri(AppInfo.RepositoryUrl);
        RepoLinkText.Text = AppInfo.RepositoryUrl;
        NoticesBox.Text = AppInfo.ThirdPartyNotices();
        Closed += (_, _) => _open = null;
    }

    /// <summary>Nur für --ui-snapshots.</summary>
    internal static AboutWindow CreateForSnapshot() => new();

    public static void Show(Window? owner)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }
        _open = new AboutWindow();
        if (owner is { IsVisible: true })
        {
            _open.Owner = owner;
        }
        else
        {
            _open.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        _open.Show();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OpenDataDir_Click(object sender, RoutedEventArgs e)
    {
        global::System.IO.Directory.CreateDirectory(AppPaths.DataDir);
        Process.Start(new ProcessStartInfo(AppPaths.DataDir) { UseShellExecute = true });
    }
}
