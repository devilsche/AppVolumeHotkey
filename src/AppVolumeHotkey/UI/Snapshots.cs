using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AppVolumeHotkey.Audio;
using AppVolumeHotkey.Core.Audio;
using AppVolumeHotkey.Core.Localization;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Overlay;
using AppVolumeHotkey.Platform;

namespace AppVolumeHotkey.UI;

/// <summary>
/// <c>AppVolumeHotkey.exe --ui-snapshots &lt;Ordner&gt;</c>: zeichnet alle Fenster mit Beispieldaten als PNG
/// (Deutsch/Englisch, hell/dunkel). Läuft im GitHub-Build auf Windows; ändert keine echten Einstellungen.
/// </summary>
internal static class Snapshots
{
    public static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        AppPaths.DataDir = Path.Combine(Path.GetTempPath(), "AppVolumeHotkey-snapshots-" + Guid.NewGuid().ToString("N"));

        var (config, voice, leise) = SampleConfig();
        var host = new AppHost(config, SampleApps());
        host.Volume.OnHotkey(leise.Id); // ein aktives Profil für die Ansicht
        host.Mic.SetMuted(true);        // Mikrofon stumm für die Ansicht

        foreach (var (theme, themeName) in new[] { (ThemeMode.Light, "light"), (ThemeMode.Dark, "dark") })
        {
            Application.Current.ThemeMode = theme;
            foreach (var lang in new[] { "de", "en" })
            {
                L.SetLanguage(lang);
                var prefix = Path.Combine(outDir, $"{lang}-{themeName}-");
                Pump();

                var area = SystemParameters.WorkArea;
                var main = new MainWindow(host)
                {
                    Left = 0, Top = 0, ShowActivated = false,
                    Width = Math.Min(1320, area.Width), Height = Math.Min(760, area.Height),
                };
                main.Show();
                Pump();
                SelectFirstGroup(main);
                Pump();
                Save(main, prefix + "01-main.png", theme, width: 1920, height: 1080);

                if (main.FindName("FeatureList") is ListBox features)
                {
                    features.SelectedIndex = 0;
                    Pump();
                    Save(main, prefix + "01b-mic.png", theme, width: 1920, minHeight: 1080);
                    features.SelectedIndex = 1;
                    Pump();
                    Save(main, prefix + "01c-ducking.png", theme, width: 1920, minHeight: 1080);
                    features.SelectedIndex = -1;
                    SelectFirstGroup(main);
                    Pump();
                }

                host.SuspendHotkeys(); // wie beim echten Öffnen des Dialogs, sonst gilt F13 als „belegt“
                var profile = ProfileDialog.CreateForSnapshot(main, host, voice, leise.Clone());
                profile.Show();
                Pump();
                Save(profile, prefix + "02-profile.png", theme, width: 680);
                profile.Close();
                host.ResumeHotkeys();

                foreach (var (page, name) in new[] { (0, "general"), (1, "overlay"), (2, "status") })
                {
                    var settings = SettingsWindow.CreateForSnapshot(main, host, page);
                    settings.Show();
                    Pump();
                    Save(settings, prefix + $"03-settings-{name}.png", theme, width: 840);
                    settings.Close();
                }

                var about = AboutWindow.CreateForSnapshot();
                about.Show();
                Pump();
                Save(about, prefix + "04-about.png", theme, width: 600);
                about.Close();

                if (themeName == "dark") // Overlay/Statusanzeige sind themenunabhängig
                {
                    var result = new HotkeyResult(voice, leise, leise.Steps[0], true, true, false);
                    foreach (var size in new[] { OverlaySize.Small, OverlaySize.Medium, OverlaySize.Large })
                    {
                        var overlay = new OverlayWindow();
                        overlay.ShowResult(result, new AppSettings { OverlaySize = size, FadeDurationMs = 0 });
                        Pump();
                        Save(overlay, prefix + $"05-overlay-{size.ToString().ToLowerInvariant()}.png", theme, gameScene: true);
                        overlay.Close();
                    }
                    var micOverlay = new OverlayWindow();
                    micOverlay.ShowMic(true, MicScope.AllMicrophones, new AppSettings());
                    Pump();
                    Save(micOverlay, prefix + "05-overlay-mic.png", theme, gameScene: true);
                    micOverlay.Close();
                    foreach (var style in new[] { StatusStyle.MiniText, StatusStyle.IconText, StatusStyle.Icon })
                    {
                        var status = new StatusIndicatorWindow();
                        status.Update(host.ActiveStatusItems(), new AppSettings { StatusStyle = style });
                        Pump();
                        Save(status, prefix + $"06-status-{style.ToString().ToLowerInvariant()}.png", theme, gameScene: true);
                        status.Close();
                    }
                }

                main.ForceClose();
                Pump();
            }
        }

        host.Dispose();
        try { Directory.Delete(AppPaths.DataDir, recursive: true); } catch (Exception) { }
    }

    private static (AppConfig Config, AppGroup Voice, Profile Leise) SampleConfig()
    {
        var leise = new Profile
        {
            Name = "Voice leise",
            Hotkey = new Hotkey(HotkeyModifiers.None, 0x7C), // F13
            Mode = ValueMode.Relative,
            Steps = { Step.Percent(20), Step.Baseline },
            Sound = true,
        };
        var stumm = new Profile
        {
            Name = "Voice stumm",
            Hotkey = new Hotkey(HotkeyModifiers.None, MouseButtons.X2),
            Trigger = TriggerMode.Hold,
            Mode = ValueMode.Absolute,
            Steps = { Step.Percent(0) },
        };
        var voice = new AppGroup { Name = "Voice", Apps = { "Discord.exe", "ts3client_win64.exe" }, Profiles = { leise, stumm } };
        var spiel = new AppGroup
        {
            Name = "Spiel",
            Apps = { "cs2.exe" },
            Profiles =
            {
                new Profile
                {
                    Name = "Spiel 50/100",
                    Hotkey = new Hotkey(HotkeyModifiers.Shift, 0x78), // Shift+F9
                    Steps = { Step.Percent(50), Step.Percent(100) },
                },
            },
        };
        var musik = new AppGroup { Name = "Musik", Apps = { "Spotify.exe" } };
        var cfg = new AppConfig { Groups = { voice, spiel, musik } };
        cfg.Mic.Hotkey = new Hotkey(HotkeyModifiers.None, 0x7D); // F14
        cfg.Ducking.Enabled = true;
        cfg.Ducking.ToggleHotkey = new Hotkey(HotkeyModifiers.Control, 0x79); // Strg+F10
        cfg.Ducking.Rules.Add(new DuckingRule { SourceGroupId = voice.Id, Percent = 35 });
        return (cfg, voice, leise);
    }

    private static IEnumerable<(RunningApp, float)> SampleApps() => new[]
    {
        (new RunningApp("Discord.exe", "Discord", null), 0.8f),
        (new RunningApp("ts3client_win64.exe", "TeamSpeak 3 Client", null), 1.0f),
        (new RunningApp("cs2.exe", "Counter-Strike 2", null), 0.65f),
        (new RunningApp("Spotify.exe", "Spotify", null), 0.4f),
        (new RunningApp("firefox.exe", "Firefox", null), 1.0f),
    };

    private static void SelectFirstGroup(MainWindow main)
    {
        if (main.FindName("GroupList") is ListBox list && list.Items.Count > 0)
            list.SelectedIndex = 0;
    }

    /// <summary>Layout, Bindungen und Rendern abarbeiten lassen.</summary>
    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(60);
        }
    }

    /// <param name="width">Feste Breite fürs Bild (unabhängig vom kleinen Bildschirm des Build-Rechners).</param>
    /// <param name="height">Feste Höhe; null = ganze Inhaltshöhe ohne Scrollen.</param>
    private static void Save(Window window, string path, ThemeMode theme, bool gameScene = false, double? width = null, double? height = null,
        double minHeight = 0)
    {
        window.UpdateLayout();
        if (window.Content is not FrameworkElement content)
            return;
        if (width is { } w0)
        {
            // Fenster auf Wunschbreite bringen (der Build-Rechner läuft dafür in 1920×1080) und den Inhalt
            // mit unbegrenzter Höhe anordnen – Scrollbereiche zeigen so ihren ganzen Inhalt.
            window.Width = w0 + 16;
            window.UpdateLayout();
            content.Measure(new Size(content.ActualWidth, height ?? double.PositiveInfinity));
            var h0 = height ?? Math.Max(minHeight, Math.Ceiling(content.DesiredSize.Height));
            content.Arrange(new Rect(0, 0, content.ActualWidth, h0));
        }
        if (content.ActualWidth < 1 || content.ActualHeight < 1)
            return;
        Render(content, window, path, theme, gameScene);
    }

    private static void Render(FrameworkElement content, Window window, string path, ThemeMode theme, bool gameScene)
    {

        var dpi = VisualTreeHelper.GetDpi(content);
        double w = content.ActualWidth, h = content.ActualHeight;
        var pad = gameScene ? 40.0 : 0.0;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var rect = new Rect(0, 0, w + 2 * pad, h + 2 * pad);
            Brush background = gameScene
                ? new LinearGradientBrush(Color.FromRgb(0x2B, 0x3A, 0x2F), Color.FromRgb(0x7B, 0x6B, 0x4A), 30)
                : window.Background is SolidColorBrush { Color.A: > 0 } solid
                    ? solid
                    : new SolidColorBrush(theme == ThemeMode.Dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3));
            dc.DrawRectangle(background, null, rect);
            // Auch Schatten/Ränder außerhalb der Elementgrenzen erfassen (sonst rechts abgeschnitten)
            var bounds = VisualTreeHelper.GetDescendantBounds(content);
            bounds.Union(new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds,
                    AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, new Rect(pad + bounds.X, pad + bounds.Y, bounds.Width, bounds.Height));
        }

        var bmp = new RenderTargetBitmap(
            (int)Math.Ceiling((w + 2 * pad) * dpi.DpiScaleX), (int)Math.Ceiling((h + 2 * pad) * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
