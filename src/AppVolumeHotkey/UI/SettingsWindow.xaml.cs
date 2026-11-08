using System.Windows;
using System.Windows.Automation;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Overlay;
using AppVolumeHotkey.Platform;

namespace AppVolumeHotkey.UI;

public partial class SettingsWindow : Window
{
    private static readonly (OverlayCorner Value, string Label)[] Corners =
    {
        (OverlayCorner.TopLeft, "Oben links"),
        (OverlayCorner.TopRight, "Oben rechts"),
        (OverlayCorner.BottomLeft, "Unten links"),
        (OverlayCorner.BottomRight, "Unten rechts"),
    };

    private static readonly (OverlaySize Value, string Label)[] Sizes =
    {
        (OverlaySize.Small, "Klein"),
        (OverlaySize.Medium, "Mittel"),
        (OverlaySize.Large, "Groß"),
    };

    private readonly AppHost _host;
    private readonly OverlayService _preview = new();

    private SettingsWindow(Window owner, AppHost host)
    {
        Owner = owner;
        _host = host;
        InitializeComponent();

        var s = host.Config.Settings;
        StartWithWindowsBox.IsChecked = s.StartWithWindows;
        StartMinimizedBox.IsChecked = s.StartMinimized;
        RunAsAdminBox.IsChecked = s.RunAsAdmin;
        CornerBox.ItemsSource = Corners.Select(c => c.Label).ToList();
        CornerBox.SelectedIndex = Array.FindIndex(Corners, c => c.Value == s.OverlayCorner);
        SizeBox.ItemsSource = Sizes.Select(c => c.Label).ToList();
        SizeBox.SelectedIndex = Array.FindIndex(Sizes, c => c.Value == s.OverlaySize);
        DurationBox.Text = s.OverlayDurationMs.ToString();
        FadeBox.Text = s.FadeDurationMs.ToString();

        Closed += (_, _) => _preview.Close();
        Loaded += (_, _) => StartWithWindowsBox.Focus();
    }

    public static void Open(Window owner, AppHost host) => new SettingsWindow(owner, host).ShowDialog();

    private bool TryRead(out AppSettings settings)
    {
        settings = new AppSettings
        {
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            StartMinimized = StartMinimizedBox.IsChecked == true,
            RunAsAdmin = RunAsAdminBox.IsChecked == true,
            OverlayCorner = Corners[Math.Max(0, CornerBox.SelectedIndex)].Value,
            OverlaySize = Sizes[Math.Max(0, SizeBox.SelectedIndex)].Value,
        };

        ClearError();
        if (!int.TryParse(DurationBox.Text.Trim(), out var duration) || duration is < 300 or > 10000)
            return ShowError(DurationBox, "Anzeigedauer: bitte eine Zahl von 300 bis 10000 eingeben.");
        if (!int.TryParse(FadeBox.Text.Trim(), out var fade) || fade is < 0 or > 1000)
            return ShowError(FadeBox, "Übergangsdauer: bitte eine Zahl von 0 bis 1000 eingeben.");
        settings.OverlayDurationMs = duration;
        settings.FadeDurationMs = fade;
        return true;
    }

    private bool ShowError(System.Windows.Controls.TextBox box, string message)
    {
        ErrorText.Text = "⚠ " + message;
        ErrorText.Visibility = Visibility.Visible;
        AutomationProperties.SetHelpText(box, message);
        box.Focus();
        box.SelectAll();
        return false;
    }

    private void ClearError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        DurationBox.ClearValue(AutomationProperties.HelpTextProperty);
        FadeBox.ClearValue(AutomationProperties.HelpTextProperty);
    }

    private void TestOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var s))
            return;
        var group = new AppGroup { Name = "Test" };
        var profile = new Profile { Name = "Beispiel", Steps = { Step.Percent(20) } };
        _preview.Handle(new HotkeyResult(group, profile, Step.Percent(20), true, true, false), s);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var s))
            return;
        var adminNewlyOn = s.RunAsAdmin && !_host.Config.Settings.RunAsAdmin;
        _host.Config.Settings = s;
        _host.SaveConfig();

        var elevated = Elevation.IsElevated;
        try
        {
            Autostart.Apply(s, elevated);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Autostart konnte nicht eingerichtet werden: " + ex.Message, "Autostart",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        DialogResult = true;

        if (adminNewlyOn && !elevated)
        {
            var answer = MessageBox.Show(Owner, "Jetzt mit Administratorrechten neu starten?", "Administratorrechte",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (answer == MessageBoxResult.Yes)
                _host.RestartElevated();
        }
    }
}
