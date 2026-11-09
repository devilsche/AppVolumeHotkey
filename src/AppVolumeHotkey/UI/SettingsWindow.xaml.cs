using System.Windows;
using System.Windows.Automation;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Overlay;
using AppVolumeHotkey.Platform;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.UI;

public partial class SettingsWindow : Window
{
    private static readonly (OverlayCorner Value, string Label)[] Corners =
    {
        (OverlayCorner.TopLeft, "Settings_TopLeft"),
        (OverlayCorner.TopRight, "Settings_TopRight"),
        (OverlayCorner.BottomLeft, "Settings_BottomLeft"),
        (OverlayCorner.BottomRight, "Settings_BottomRight"),
    };

    private static readonly (OverlaySize Value, string Label)[] Sizes =
    {
        (OverlaySize.Small, "Settings_Small"),
        (OverlaySize.Medium, "Settings_Medium"),
        (OverlaySize.Large, "Settings_Large"),
    };

    private static readonly (OverlayStyle Value, string Label)[] Styles =
    {
        (OverlayStyle.Compact, "Settings_Compact"),
        (OverlayStyle.Normal, "Settings_Normal"),
    };

    private static readonly (string Value, string Label)[] Languages =
    {
        ("auto", "Settings_LangAuto"),
        ("de", "Settings_LangDe"),
        ("en", "Settings_LangEn"),
    };

    private static readonly (StatusStyle Value, string Label)[] StatusStyles =
    {
        (StatusStyle.MiniText, "Settings_StatusMini"),
        (StatusStyle.IconText, "Settings_StatusIconText"),
        (StatusStyle.Icon, "Settings_StatusIcon"),
    };

    private readonly AppHost _host;
    private readonly OverlayService _preview = new();

    private SettingsWindow(Window owner, AppHost host)
    {
        Owner = owner;
        _host = host;
        InitializeComponent();

        var s = host.Config.Settings;
        StartWithWindowsBox.IsChecked = Autostart.PackagedState() ?? s.StartWithWindows;
        if (Packaging.IsPackaged)
        {
            // In der Store-Version gibt es keinen Admin-Modus
            RunAsAdminBox.Visibility = Visibility.Collapsed;
            RunAsAdminHint.Visibility = Visibility.Collapsed;
        }
        StartMinimizedBox.IsChecked = s.StartMinimized;
        RunAsAdminBox.IsChecked = s.RunAsAdmin;
        MinimizeToTrayBox.IsChecked = s.MinimizeToTray;
        CornerBox.ItemsSource = Corners.Select(c => L.T(c.Label)).ToList();
        CornerBox.SelectedIndex = Array.FindIndex(Corners, c => c.Value == s.OverlayCorner);
        SizeBox.ItemsSource = Sizes.Select(c => L.T(c.Label)).ToList();
        SizeBox.SelectedIndex = Array.FindIndex(Sizes, c => c.Value == s.OverlaySize);
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        StatusOnBox.IsChecked = s.StatusIndicator;
        StatusStyleBox.ItemsSource = StatusStyles.Select(c => L.T(c.Label)).ToList();
        StatusStyleBox.SelectedIndex = Math.Max(0, Array.FindIndex(StatusStyles, c => c.Value == s.StatusStyle));
        StatusCornerBox.ItemsSource = Corners.Select(c => L.T(c.Label)).ToList();
        StatusCornerBox.SelectedIndex = Math.Max(0, Array.FindIndex(Corners, c => c.Value == s.StatusCorner));
        StatusOpacityBox.Text = s.StatusOpacity.ToString();
        TrayBadgeBox.IsChecked = s.TrayBadge;
        LanguageBox.ItemsSource = Languages.Select(c => L.T(c.Label)).ToList();
        LanguageBox.SelectedIndex = Math.Max(0, Array.FindIndex(Languages, c => c.Value == s.Language));
        StyleBox.ItemsSource = Styles.Select(c => L.T(c.Label)).ToList();
        StyleBox.SelectedIndex = Array.FindIndex(Styles, c => c.Value == s.OverlayStyle);
        DurationBox.Text = s.OverlayDurationMs.ToString();
        FadeBox.Text = s.FadeDurationMs.ToString();

        Closed += (_, _) => _preview.Close();
        Loaded += (_, _) => StartWithWindowsBox.Focus();
    }

    /// <summary>Nur für --ui-snapshots.</summary>
    internal static SettingsWindow CreateForSnapshot(Window owner, AppHost host) => new(owner, host) { MaxHeight = double.PositiveInfinity };

    public static void Open(Window owner, AppHost host) => new SettingsWindow(owner, host).ShowDialog();

    private bool TryRead(out AppSettings settings)
    {
        settings = new AppSettings
        {
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            StartMinimized = StartMinimizedBox.IsChecked == true,
            RunAsAdmin = !Packaging.IsPackaged && RunAsAdminBox.IsChecked == true,
            MinimizeToTray = MinimizeToTrayBox.IsChecked == true,
            OverlayCorner = Corners[Math.Max(0, CornerBox.SelectedIndex)].Value,
            OverlaySize = Sizes[Math.Max(0, SizeBox.SelectedIndex)].Value,
            OverlayStyle = Styles[Math.Max(0, StyleBox.SelectedIndex)].Value,
            MainWindow = _host.Config.Settings.MainWindow,
            Language = Languages[Math.Max(0, LanguageBox.SelectedIndex)].Value,
            StatusIndicator = StatusOnBox.IsChecked == true,
            StatusStyle = StatusStyles[Math.Max(0, StatusStyleBox.SelectedIndex)].Value,
            StatusCorner = Corners[Math.Max(0, StatusCornerBox.SelectedIndex)].Value,
            TrayBadge = TrayBadgeBox.IsChecked == true,
        };

        ClearError();
        if (!int.TryParse(DurationBox.Text.Trim(), out var duration) || duration is < 300 or > 10000)
            return ShowError(DurationBox, L.T("Settings_ErrDuration"));
        if (!int.TryParse(FadeBox.Text.Trim(), out var fade) || fade is < 0 or > 1000)
            return ShowError(FadeBox, L.T("Settings_ErrFade"));
        if (!int.TryParse(StatusOpacityBox.Text.Trim(), out var opacity) || opacity is < 30 or > 100)
            return ShowError(StatusOpacityBox, L.T("Settings_ErrOpacity"));
        settings.StatusOpacity = opacity;
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
        StatusOpacityBox.ClearValue(AutomationProperties.HelpTextProperty);
        FadeBox.ClearValue(AutomationProperties.HelpTextProperty);
    }

    private void TestOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var s))
            return;
        var group = new AppGroup { Name = "Test" };
        var profile = new Profile { Name = L.T("Settings_SampleProfile"), Mode = ValueMode.Relative, Steps = { Step.Percent(20) } };
        _preview.Handle(new HotkeyResult(group, profile, Step.Percent(20), true, true, false), s);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var s))
            return;
        var adminNewlyOn = s.RunAsAdmin && !_host.Config.Settings.RunAsAdmin;
        _host.Config.Settings = s;
        _host.SaveConfig();
        var lang = L.Resolve(s.Language);
        if (lang != L.Language)
            L.SetLanguage(lang); // alle Fenster, Tray und Overlay wechseln sofort

        var elevated = Elevation.IsElevated;
        try
        {
            Autostart.Apply(s, elevated);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, L.F("Settings_AutostartFailed", ex.Message), "Autostart",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        DialogResult = true;

        if (adminNewlyOn && !elevated)
        {
            var answer = MessageBox.Show(Owner, L.T("Settings_RestartAdmin"), L.T("Settings_AdminTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (answer == MessageBoxResult.Yes)
                _host.RestartElevated();
        }
    }
}
