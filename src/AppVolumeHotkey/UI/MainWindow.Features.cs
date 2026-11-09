using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AppVolumeHotkey.Core.Localization;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Input;
using AppVolumeHotkey.Overlay;

namespace AppVolumeHotkey.UI;

/// <summary>Hauptfenster: Seiten „Mikrofon“ und „Ducking“ (Änderungen werden sofort gespeichert).</summary>
public partial class MainWindow
{
    private bool _loadingFeatures;
    private DispatcherTimer? _saveTimer;
    private DispatcherTimer? _meterTimer;
    private readonly List<DuckingRuleCard> _ruleCards = new();

    private void InitFeatures()
    {
        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) =>
        {
            _saveTimer!.Stop();
            _host.SaveConfig();
        }, Dispatcher);
        _saveTimer.Stop();
        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => UpdateMeters(), Dispatcher);
        _meterTimer.Stop();

        _host.MicStateChanged += UpdateMicState;
        _host.Ducking.StateChanged += UpdateFeatureBadges;
        _host.ConfigChanged += UpdateFeatureBadges;
        L.Changed += LoadFeatures;

        MicHotkeyBox.HotkeyChanged += OnMicHotkeyChanged;
        DuckingHotkeyBox.HotkeyChanged += OnDuckingHotkeyChanged;
        // Beim Aufnehmen eines Hotkeys alle Hotkeys pausieren, damit die Taste im Feld ankommt
        foreach (var box in new[] { MicHotkeyBox, DuckingHotkeyBox })
        {
            box.GotKeyboardFocus += (_, _) => _host.SuspendHotkeys();
            box.LostKeyboardFocus += (_, _) => _host.ResumeHotkeys();
        }
        Closed += (_, _) =>
        {
            _host.MicStateChanged -= UpdateMicState;
            _host.Ducking.StateChanged -= UpdateFeatureBadges;
            _host.ConfigChanged -= UpdateFeatureBadges;
            L.Changed -= LoadFeatures;
            _meterTimer.Stop();
            if (_saveTimer.IsEnabled)
                _host.SaveConfig();
        };
        LoadFeatures();
    }

    private void FeatureList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FeatureList.SelectedItem is not null && GroupList.SelectedItem is not null)
            GroupList.SelectedIndex = -1;
        if (FeatureList.SelectedItem == DuckingFeatureItem)
        {
            BuildRuleCards(); // Gruppen könnten sich geändert haben
            _meterTimer?.Start();
        }
        else
        {
            _meterTimer?.Stop();
        }
        RefreshGroup();
    }

    private void ScheduleSave()
    {
        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    private void LoadFeatures()
    {
        _loadingFeatures = true;
        try
        {
            var mic = _host.Config.Mic;
            MicHotkeyBox.Hotkey = mic.Hotkey;
            MicPassThroughBox.IsChecked = mic.PassThrough;
            MicPassThroughBox.IsEnabled = mic.Hotkey?.IsMouse == true;
            (mic.Mode switch { MicMode.HoldToMute => MicModeHold, MicMode.PushToTalk => MicModePtt, _ => MicModeToggle }).IsChecked = true;
            (mic.Scope == MicScope.DefaultMicrophone ? MicScopeDefault : MicScopeAll).IsChecked = true;
            MicOverlayBox.IsChecked = mic.ShowOverlay;
            MicSoundBox.IsChecked = mic.PlaySound;
            MicSoundCombo.ItemsSource = SoundBank.Variants.Select(v => L.T(v.LabelKey)).ToList();
            MicSoundCombo.SelectedIndex = Math.Max(0, SoundBank.Variants.ToList().FindIndex(v => v.Variant == mic.SoundVariant));
            MicStatusBox.IsChecked = mic.ShowInStatus;
            MicUnmuteExitBox.IsChecked = mic.UnmuteOnExit;
            SetError(MicHotkeyError, MicHotkeyBox, _host.MicHotkeyFailed && mic.Hotkey is not null
                ? L.F("Profile_ErrHotkeyTaken", KeyNames.Format(mic.Hotkey)) : null);

            DuckingOnBox.IsChecked = _host.Config.Ducking.Enabled;
            DuckingHotkeyBox.Hotkey = _host.Config.Ducking.ToggleHotkey;
            SetError(DuckingHotkeyError, DuckingHotkeyBox, _host.DuckingHotkeyFailed && _host.Config.Ducking.ToggleHotkey is { } dk
                ? L.F("Profile_ErrHotkeyTaken", KeyNames.Format(dk)) : null);
        }
        finally
        {
            _loadingFeatures = false;
        }
        BuildRuleCards();
        UpdateMicState();
        UpdateFeatureBadges();
    }

    // ---------- Mikrofon ----------

    private void UpdateMicState()
    {
        var muted = _host.Mic.IsMuted;
        MicStateText.Text = muted switch
        {
            true => L.T("Mic_StatusMuted"),
            false => L.T("Mic_StatusLive"),
            _ => L.T("Mic_StatusNone"),
        };
        MicStateIcon.Text = muted == true ? "" : ""; // MicOff / Microphone
        MicStateIcon.Foreground = muted == true ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)) : (Brush)FindResource(SystemColors.WindowTextBrushKey);
        MicStateCard.BorderBrush = muted == true
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
            : (Brush)FindResource(SystemColors.ActiveBorderBrushKey);
        MicToggleText.Text = L.T(muted == true ? "Mic_TurnOn" : "Mic_TurnOff");
        MicToggleButton.IsEnabled = muted is not null;
        UpdateFeatureBadges();
    }

    private void UpdateFeatureBadges()
    {
        // Dezent: Symbol + kleiner Zustandstext (nicht nur Farbe)
        var muted = _host.Mic.IsMuted == true;
        MicFeatureIcon.Text = muted ? "\uEC54" : "\uE720";
        if (muted)
            MicFeatureIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        else
            MicFeatureIcon.ClearValue(TextBlock.ForegroundProperty);
        MicFeatureState.Text = muted ? L.T("Feature_Muted") : "";
        AutomationSetName(MicFeatureItem, L.T("Main_FeatureMic") + (muted ? ", " + L.T("Feature_Muted") : ""));

        var on = _host.Config.Ducking.Enabled;
        DuckingFeatureState.Text = on ? L.T("Feature_On") : "";
        AutomationSetName(DuckingFeatureItem, L.T("Main_FeatureDucking") + (on ? ", " + L.T("Feature_On") : ""));
        if (!_loadingFeatures && DuckingOnBox.IsChecked != on)
        {
            _loadingFeatures = true;
            DuckingOnBox.IsChecked = on;
            _loadingFeatures = false;
        }
    }

    private static void AutomationSetName(DependencyObject d, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(d, name);

    private void MicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_host.Mic.IsMuted is { } muted)
            _host.Mic.SetMuted(!muted);
    }

    private void MicOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingFeatures)
            return;
        var mic = _host.Config.Mic;
        mic.PassThrough = MicPassThroughBox.IsChecked == true;
        mic.Mode = MicModeHold.IsChecked == true ? MicMode.HoldToMute : MicModePtt.IsChecked == true ? MicMode.PushToTalk : MicMode.Toggle;
        mic.Scope = MicScopeDefault.IsChecked == true ? MicScope.DefaultMicrophone : MicScope.AllMicrophones;
        mic.ShowOverlay = MicOverlayBox.IsChecked == true;
        mic.PlaySound = MicSoundBox.IsChecked == true;
        mic.ShowInStatus = MicStatusBox.IsChecked == true;
        mic.UnmuteOnExit = MicUnmuteExitBox.IsChecked == true;
        _host.SaveConfig(); // sofort: z. B. Push-to-Talk schaltet direkt stumm
    }

    private void MicSound_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingFeatures || MicSoundCombo.SelectedIndex < 0)
            return;
        _host.Config.Mic.SoundVariant = SoundBank.Variants[MicSoundCombo.SelectedIndex].Variant;
        SoundBank.Play(_host.Config.Mic.SoundVariant, active: false);
        ScheduleSave();
    }

    private void OnMicHotkeyChanged()
    {
        MicPassThroughBox.IsEnabled = MicHotkeyBox.Hotkey?.IsMouse == true;
        if (_loadingFeatures)
            return;
        if (ApplyHotkey(MicHotkeyBox, MicHotkeyError, "mic"))
        {
            _host.Config.Mic.Hotkey = MicHotkeyBox.Hotkey;
            if (MicHotkeyBox.Hotkey?.IsMouse != true)
                _host.Config.Mic.PassThrough = false;
            ScheduleSave();
        }
    }

    /// <summary>Prüft einen neuen Hotkey auf Konflikte; zeigt Fehler an. true = übernehmen.</summary>
    private bool ApplyHotkey(HotkeyBox box, TextBlock error, object self)
    {
        string? message = null;
        if (box.Hotkey is { } hk)
        {
            message = _host.HotkeyConflict(hk, self);
            if (message is null && !_host.Hotkeys.IsAvailable(hk))
                message = L.F("Profile_ErrHotkeyTaken", KeyNames.Format(hk));
        }
        SetError(error, box, message);
        return message is null;
    }

    private static void SetError(TextBlock error, Control control, string? message)
    {
        error.Text = message is null ? "" : "⚠ " + message;
        error.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetHelpText(control, message ?? "");
    }

    // ---------- Ducking ----------

    private void DuckingOn_Click(object sender, RoutedEventArgs e)
    {
        if (_loadingFeatures)
            return;
        if ((DuckingOnBox.IsChecked == true) != _host.Config.Ducking.Enabled)
            _host.ToggleDucking();
    }

    private void OnDuckingHotkeyChanged()
    {
        if (_loadingFeatures)
            return;
        if (ApplyHotkey(DuckingHotkeyBox, DuckingHotkeyError, "ducking"))
        {
            _host.Config.Ducking.ToggleHotkey = DuckingHotkeyBox.Hotkey;
            ScheduleSave();
        }
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var groups = _host.Config.Groups;
        if (groups.Count == 0)
            return;
        var source = groups.FirstOrDefault(g => g.Name.Contains("voice", StringComparison.OrdinalIgnoreCase)) ?? groups[0];
        _host.Config.Ducking.Rules.Add(new DuckingRule { SourceGroupId = source.Id });
        _host.SaveConfig();
        BuildRuleCards();
        _ruleCards.LastOrDefault()?.FocusFirst();
    }

    private void BuildRuleCards()
    {
        RuleCards.Children.Clear();
        _ruleCards.Clear();
        var groups = _host.Config.Groups;
        var rules = _host.Config.Ducking.Rules;
        AddRuleButton.IsEnabled = groups.Count > 0;
        DuckingEmptyHint.Text = groups.Count == 0 ? L.T("Ducking_NeedGroup") : rules.Count == 0 ? L.T("Ducking_NoRules") : "";
        DuckingEmptyHint.Visibility = DuckingEmptyHint.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var card = new DuckingRuleCard(rule, i + 1, groups, withAccessKeys: i == 0);
            card.Changed += immediate => { if (immediate) _host.SaveConfig(); else ScheduleSave(); };
            card.RemoveRequested += () =>
            {
                _host.Config.Ducking.Rules.Remove(rule);
                _host.SaveConfig();
                BuildRuleCards();
            };
            _ruleCards.Add(card);
            RuleCards.Children.Add(card);
        }
    }

    private void UpdateMeters()
    {
        if (!IsVisible || DuckingPanel.Visibility != Visibility.Visible)
            return;
        foreach (var card in _ruleCards)
            card.SetLevel(_host.Ducking.SourcePeak(card.Rule));
    }
}

/// <summary>Karte für eine Ducking-Regel (im Code aufgebaut, Änderungen gehen direkt in die Regel).</summary>
internal sealed class DuckingRuleCard : Border
{
    private const double MeterMax = 0.3; // Sprachpegel liegen meist darunter
    private readonly ScaleTransform _level = new(0, 1);
    private readonly TextBox _percent;
    private readonly TextBox _release;
    private readonly TextBlock _percentError;
    private readonly TextBlock _releaseError;
    private readonly ComboBox _source;

    public DuckingRule Rule { get; }

    /// <summary>true = sofort speichern, false = verzögert (Texteingabe, Regler).</summary>
    public event Action<bool>? Changed;

    public event Action? RemoveRequested;

    public DuckingRuleCard(DuckingRule rule, int number, IReadOnlyList<AppGroup> groups, bool withAccessKeys)
    {
        Rule = rule;
        string Key(string k) => withAccessKeys ? L.T(k) : L.T(k).Replace("_", "");

        BorderThickness = new Thickness(1);
        SetResourceReference(BorderBrushProperty, SystemColors.ActiveBorderBrushKey);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(16);
        Margin = new Thickness(0, 8, 0, 8);

        var root = new StackPanel();
        Child = root;

        // Kopf: „Regel 1“ + Entfernen
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
        var remove = new Button { Width = 36, Height = 32, MinWidth = 32, MinHeight = 32, Padding = new Thickness(0), ToolTip = L.F("Ducking_RemoveRule", number) };
        remove.Content = new TextBlock { Text = "", FontFamily = (FontFamily)Application.Current.FindResource("IconFont"), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(remove, L.F("Ducking_RemoveRule", number));
        remove.Click += (_, _) => RemoveRequested?.Invoke();
        DockPanel.SetDock(remove, Dock.Right);
        head.Children.Add(remove);
        head.Children.Add(new TextBlock { Text = L.F("Ducking_RuleHeading", number), FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(head);

        // Quelle
        _source = new ComboBox { MinWidth = 240, MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
        foreach (var g in groups)
            _source.Items.Add(new ComboBoxItem { Content = g.Name, Tag = g.Id });
        _source.SelectedIndex = Math.Max(0, groups.ToList().FindIndex(g => g.Id == rule.SourceGroupId));
        if (groups.Count > 0 && rule.SourceGroupId != groups[_source.SelectedIndex].Id)
            rule.SourceGroupId = groups[_source.SelectedIndex].Id;
        System.Windows.Automation.AutomationProperties.SetName(_source, L.T("Ducking_SourceA11y"));
        root.Children.Add(new Label { Content = Key("Ducking_Source"), Target = _source, Padding = new Thickness(0, 0, 0, 4) });
        root.Children.Add(_source);

        // Ziele
        root.Children.Add(new TextBlock { Text = L.T("Ducking_Targets"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        var groupName = "targets-" + rule.Id.ToString("N");
        var all = new RadioButton { Content = Key("Ducking_TargetAll"), GroupName = groupName, IsChecked = rule.TargetAllOthers, Margin = new Thickness(0, 0, 0, 8) };
        var some = new RadioButton { Content = Key("Ducking_TargetGroups"), GroupName = groupName, IsChecked = !rule.TargetAllOthers, Margin = new Thickness(0, 0, 0, 4) };
        root.Children.Add(all);
        root.Children.Add(some);
        var targets = new WrapPanel { Margin = new Thickness(28, 0, 0, 16) };
        root.Children.Add(targets);

        void FillTargets()
        {
            targets.Children.Clear();
            foreach (var g in groups.Where(g => g.Id != rule.SourceGroupId))
            {
                var cb = new CheckBox { Content = g.Name.Replace("_", "__"), IsChecked = rule.TargetGroupIds.Contains(g.Id), Margin = new Thickness(0, 0, 16, 4), IsEnabled = !rule.TargetAllOthers };
                var id = g.Id;
                cb.Click += (_, _) =>
                {
                    if (cb.IsChecked == true) { if (!rule.TargetGroupIds.Contains(id)) rule.TargetGroupIds.Add(id); }
                    else rule.TargetGroupIds.Remove(id);
                    Changed?.Invoke(true);
                };
                targets.Children.Add(cb);
            }
        }
        FillTargets();
        all.Click += (_, _) => { rule.TargetAllOthers = true; FillTargets(); Changed?.Invoke(true); };
        some.Click += (_, _) => { rule.TargetAllOthers = false; FillTargets(); Changed?.Invoke(true); };
        _source.SelectionChanged += (_, _) =>
        {
            if (_source.SelectedItem is ComboBoxItem { Tag: Guid id })
            {
                rule.SourceGroupId = id;
                rule.TargetGroupIds.Remove(id);
                FillTargets();
                Changed?.Invoke(true);
            }
        };

        // Prozent
        _percent = new TextBox { Text = rule.Percent.ToString(), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        _percentError = Error();
        root.Children.Add(new Label { Content = Key("Ducking_Percent"), Target = _percent, Padding = new Thickness(0, 0, 0, 4) });
        root.Children.Add(_percent);
        root.Children.Add(Caption(L.T("Ducking_PercentHint")));
        root.Children.Add(_percentError);
        _percent.TextChanged += (_, _) =>
        {
            var ok = int.TryParse(_percent.Text.Trim().TrimEnd('%').Trim(), out var v) && v is >= 0 and <= 100;
            ShowError(_percentError, _percent, ok ? null : L.T("Ducking_ErrPercent"));
            if (ok) { rule.Percent = v; Changed?.Invoke(false); }
        };

        // Empfindlichkeit: Live-Pegel + Regler (gleiche Skala)
        var slider = new Slider
        {
            Minimum = 0.005, Maximum = MeterMax, Value = Math.Clamp(rule.Threshold, 0.005, MeterMax),
            SmallChange = 0.005, LargeChange = 0.02, MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        System.Windows.Automation.AutomationProperties.SetName(slider, L.T("Ducking_Threshold").Replace("_", ""));
        var meter = new Grid { Height = 8, MaxWidth = 360, Margin = new Thickness(0, 4, 0, 4), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        var track = new Border { CornerRadius = new CornerRadius(4), Opacity = 0.5 };
        track.SetResourceReference(BackgroundProperty, SystemColors.ActiveBorderBrushKey);
        meter.Children.Add(track);
        meter.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
            RenderTransformOrigin = new Point(0, 0.5),
            RenderTransform = _level,
        });
        var meterBox = new StackPanel { MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 300, Margin = new Thickness(0, 16, 0, 0) };
        meterBox.Children.Add(new Label { Content = Key("Ducking_Threshold"), Target = slider, Padding = new Thickness(0, 0, 0, 4) });
        meterBox.Children.Add(meter);
        meterBox.Children.Add(slider);
        meterBox.Children.Add(Caption(L.T("Ducking_ThresholdHint")));
        root.Children.Add(meterBox);
        slider.ValueChanged += (_, e) => { rule.Threshold = (float)e.NewValue; Changed?.Invoke(false); };

        // Abklingzeit
        _release = new TextBox { Text = rule.ReleaseMs.ToString(), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        _releaseError = Error();
        root.Children.Add(new Label { Content = Key("Ducking_Release"), Target = _release, Padding = new Thickness(0, 16, 0, 4) });
        root.Children.Add(_release);
        root.Children.Add(_releaseError);
        _release.TextChanged += (_, _) =>
        {
            var ok = int.TryParse(_release.Text.Trim(), out var v) && v is >= 200 and <= 10000;
            ShowError(_releaseError, _release, ok ? null : L.T("Ducking_ErrRelease"));
            if (ok) { rule.ReleaseMs = v; Changed?.Invoke(false); }
        };
    }

    public void FocusFirst() => _source.Focus();

    public void SetLevel(float peak) => _level.ScaleX = Math.Clamp(peak / MeterMax, 0, 1);

    private static TextBlock Caption(string text)
    {
        var tb = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return tb;
    }

    private static TextBlock Error()
    {
        var tb = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        return tb;
    }

    private static void ShowError(TextBlock error, Control control, string? message)
    {
        error.Text = message is null ? "" : "⚠ " + message;
        error.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetHelpText(control, message ?? "");
    }
}
