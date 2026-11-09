using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Input;
using AppVolumeHotkey.Core.Localization;

namespace AppVolumeHotkey.UI;

public partial class ProfileDialog : Window
{
    private const string DragFormat = "AppVolumeHotkey.StepItem";
    private readonly AppHost _host;
    private readonly Profile _profile;
    private readonly ObservableCollection<StepItem> _steps = new();
    private bool _loaded;

    private ProfileDialog(Window owner, AppHost host, AppGroup group, Profile profile, bool isNew)
    {
        Owner = owner;
        _host = host;
        _profile = profile;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        Height = Math.Min(Height, MaxHeight);
        NavList.SelectedIndex = 0;

        Title = isNew ? L.T("Profile_TitleNew") : L.T("Profile_TitleEdit");
        Heading.Text = isNew ? L.F("Profile_HeadingNew", group.Name) : L.F("Profile_HeadingEdit", profile.Name, group.Name);
        NameBox.Text = profile.Name;
        HotkeyInput.Hotkey = profile.Hotkey;
        foreach (var s in profile.Steps)
            _steps.Add(Track(new StepItem(s)));
        StepList.ItemsSource = _steps;
        OverlayOnBox.IsChecked = profile.OverlayOnActivate;
        OverlayOffBox.IsChecked = profile.OverlayOnDeactivate;
        SoundBox.IsChecked = profile.Sound;
        SoundCombo.ItemsSource = Overlay.SoundBank.Variants.Select(v => L.T(v.LabelKey)).ToList();
        SoundCombo.SelectedIndex = Math.Max(0, Overlay.SoundBank.Variants.ToList().FindIndex(v => v.Variant == profile.SoundVariant));
        PassThroughBox.IsChecked = profile.PassThrough;
        (profile.Mode == ValueMode.Relative ? RelativeRadio : AbsoluteRadio).IsChecked = true;
        (profile.Trigger == TriggerMode.Hold ? HoldRadio : ToggleRadio).IsChecked = true;
        UpdatePassThrough();

        HotkeyInput.HotkeyChanged += () => { UpdatePassThrough(); if (_loaded) Validate(); };
        _steps.CollectionChanged += (_, _) => { Renumber(); if (_loaded) Validate(); };
        UpdatePreview();
        Renumber();

        Loaded += (_, _) =>
        {
            _loaded = true;
            Validate();
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    /// <summary>Zeigt den Dialog; gibt das bearbeitete Profil zurück oder null bei Abbruch.</summary>
    /// <summary>Nur für --ui-snapshots: Dialog ohne Modalität erzeugen.</summary>
    internal static ProfileDialog CreateForSnapshot(Window owner, AppHost host, AppGroup group, Profile profile) =>
        new(owner, host, group, profile, isNew: false);

    public static Profile? Edit(Window owner, AppHost host, AppGroup group, Profile profile, bool isNew)
    {
        host.SuspendHotkeys(); // sonst lösen bereits belegte Kombinationen beim Aufnehmen das Profil aus
        try
        {
            var d = new ProfileDialog(owner, host, group, profile, isNew);
            return d.ShowDialog() == true ? d._profile : null;
        }
        finally
        {
            host.ResumeHotkeys();
        }
    }

    private void UpdatePassThrough()
    {
        var isMouse = HotkeyInput.Hotkey?.IsMouse == true;
        PassThroughBox.IsEnabled = isMouse;
        PassThroughHint.Text = isMouse
            ? L.T("Profile_PassThroughOn")
            : L.T("Profile_PassThroughKeyboard");
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowPage(NavList.SelectedIndex);

    internal void ShowPage(int index)
    {
        if (NavList.SelectedIndex != index)
            NavList.SelectedIndex = index;
        GeneralPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        BehaviorPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        SequencePage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        FeedbackPage.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private StepItem Track(StepItem item)
    {
        item.PropertyChanged += (_, a) =>
        {
            if (_loaded && a.PropertyName == nameof(StepItem.PercentText))
                Validate();
        };
        return item;
    }

    private void Renumber()
    {
        for (var i = 0; i < _steps.Count; i++)
            _steps[i].Number = i + 1;
    }

    private void Any_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loaded)
            Validate();
    }

    private SoundVariant SelectedSound => Overlay.SoundBank.Variants[Math.Max(0, SoundCombo.SelectedIndex)].Variant;

    private void PreviewSoundOn_Click(object sender, RoutedEventArgs e) => Overlay.SoundBank.Play(SelectedSound, active: true);

    private void PreviewSoundOff_Click(object sender, RoutedEventArgs e) => Overlay.SoundBank.Play(SelectedSound, active: false);

    private TriggerMode SelectedTrigger => HoldRadio.IsChecked == true ? TriggerMode.Hold : TriggerMode.Toggle;

    private ValueMode SelectedMode => RelativeRadio.IsChecked == true ? ValueMode.Relative : ValueMode.Absolute;

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loaded)
            Validate();
    }

    private void SetSteps(params Step[] steps)
    {
        _steps.Clear();
        foreach (var s in steps)
            _steps.Add(Track(new StepItem(s)));
        StepList.SelectedIndex = 0;
    }

    private void PresetToggle_Click(object sender, RoutedEventArgs e) => SetSteps(Step.Percent(20), Step.Baseline);

    private void PresetTwoValues_Click(object sender, RoutedEventArgs e) => SetSteps(Step.Percent(20), Step.Percent(50));

    private void PresetThree_Click(object sender, RoutedEventArgs e) => SetSteps(Step.Percent(20), Step.Percent(50), Step.Baseline);

    /// <summary>Klartext-Vorschau: was passiert bei Druck 1, 2, …</summary>
    private void UpdatePreview()
    {
        if (PreviewText is null)
            return;
        var steps = new List<Step>();
        foreach (var item in _steps)
        {
            if (!item.TryGetStep(out var s))
            {
                PreviewText.Text = "–";
                return;
            }
            steps.Add(s);
        }
        if (steps.Count == 0)
        {
            PreviewText.Text = L.T("Profile_PreviewNoSteps");
            return;
        }
        var probe = new Profile { Mode = SelectedMode, Steps = steps };
        if (SelectedTrigger == TriggerMode.Hold)
        {
            var first = steps.FirstOrDefault(s => s.Kind != StepKind.Baseline);
            PreviewText.Text = first is null
                ? L.T("Profile_ErrHoldNeedsPercent")
                : L.F("Profile_PreviewHoldDown", probe.StepLabel(first)) + "\n" + L.T("Profile_PreviewHoldUp");
            return;
        }
        var lines = steps.Select((s, i) => L.F("Profile_PreviewPress", i + 1, s.Kind == StepKind.Baseline
            ? L.T("Profile_PreviewBack")
            : s.Kind == StepKind.Mute ? L.T("Profile_PreviewMute")
            : probe.StepLabel(s) + (probe.Mode == ValueMode.Relative ? L.F("Profile_PreviewExample", Math.Round(80 * s.Value / 100.0)) : "")));
        var tail = steps.Any(s => s.Kind == StepKind.Baseline)
            ? L.T("Profile_PreviewAgain")
            : L.T("Profile_PreviewStays");
        PreviewText.Text = string.Join("\n", lines) + "\n" + tail;
    }

    private bool Validate()
    {
        UpdatePreview();
        var ok = true;

        ok &= SetError(NameBox, NameError, string.IsNullOrWhiteSpace(NameBox.Text) ? L.T("Profile_ErrName") : null);

        string? hotkeyError = null;
        if (HotkeyInput.Hotkey is { } hk)
        {
            // Kollision mit anderen Profilen, dem Mikrofon- oder dem Ducking-Hotkey
            hotkeyError = _host.HotkeyConflict(hk, _profile.Id);
            if (hotkeyError is null && !_host.Hotkeys.IsAvailable(hk))
                hotkeyError = L.F("Profile_ErrHotkeyTaken", KeyNames.Format(hk));
        }
        ok &= SetError(HotkeyInput, HotkeyError, hotkeyError);

        string? stepsError = null;
        if (_steps.Count == 0)
            stepsError = L.T("Profile_ErrNoSteps");
        else if (_steps.FirstOrDefault(s => !s.TryGetStep(out _)) is { } bad)
            stepsError = L.F("Profile_ErrStep", bad.Number);
        else if (HoldRadio is not null && HoldRadio.IsChecked == true && _steps.All(s => s.IsBaseline))
            stepsError = L.T("Profile_ErrHoldNeedsPercent");
        ok &= SetError(StepList, StepsError, stepsError);

        return ok;
    }

    private static bool SetError(Control control, TextBlock errorBlock, string? message)
    {
        if (message is null)
        {
            errorBlock.Visibility = Visibility.Collapsed;
            errorBlock.Text = "";
            AutomationProperties.SetHelpText(errorBlock, "");
            control.ClearValue(AutomationProperties.HelpTextProperty);
            return true;
        }
        errorBlock.Text = "⚠ " + message;
        errorBlock.Visibility = Visibility.Visible;
        AutomationProperties.SetHelpText(control, message);
        return false;
    }

    // ---------- Stufen ----------

    private void AddPercent_Click(object sender, RoutedEventArgs e)
    {
        // Vor einem abschließenden „Ausgangswert“ einfügen, damit die Rückkehr am Ende bleibt
        var index = _steps.Count > 0 && _steps[^1].IsBaseline ? _steps.Count - 1 : _steps.Count;
        var item = Track(new StepItem(Step.Percent(50)));
        _steps.Insert(index, item);
        StepList.SelectedItem = item;
    }

    private void AddMute_Click(object sender, RoutedEventArgs e)
    {
        // Wie Prozent-Stufen vor einem abschließenden „Ausgangswert“ einfügen
        var index = _steps.Count > 0 && _steps[^1].IsBaseline ? _steps.Count - 1 : _steps.Count;
        var item = Track(new StepItem(Step.Mute));
        _steps.Insert(index, item);
        StepList.SelectedItem = item;
    }

    private void AddBaseline_Click(object sender, RoutedEventArgs e)
    {
        var item = Track(new StepItem(Step.Baseline));
        _steps.Add(item);
        StepList.SelectedItem = item;
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is StepItem item)
            _steps.Remove(item);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move((sender as Button)?.Tag as StepItem, -1);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move((sender as Button)?.Tag as StepItem, +1);

    private void Move(StepItem? item, int delta)
    {
        if (item is null)
            return;
        var from = _steps.IndexOf(item);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= _steps.Count)
            return;
        _steps.Move(from, to);
        StepList.SelectedItem = item;
    }

    private void StepList_KeyDown(object sender, KeyEventArgs e)
    {
        if (StepList.SelectedItem is not StepItem item)
            return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
        {
            Move(item, key == Key.Up ? -1 : +1);
            (StepList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (key == Key.Delete && e.OriginalSource is ListBoxItem)
        {
            _steps.Remove(item);
            e.Handled = true;
        }
    }

    private void Grip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is StepItem item)
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DragFormat, item), DragDropEffects.Move);
    }

    private void StepList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void StepList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DragFormat) is not StepItem item)
            return;
        var target = FindItem(e.OriginalSource as DependencyObject);
        var from = _steps.IndexOf(item);
        var to = target is null ? _steps.Count - 1 : _steps.IndexOf(target);
        if (from >= 0 && to >= 0 && from != to)
            _steps.Move(from, to);
    }

    private static StepItem? FindItem(DependencyObject? d)
    {
        while (d is not null)
        {
            if (d is FrameworkElement { DataContext: StepItem s })
                return s;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    // ---------- Speichern ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Validate())
        {
            // Speichern bleibt klickbar; bei Fehlern zum ersten fehlerhaften Feld springen
            Control first = NameError.Visibility == Visibility.Visible ? NameBox
                : HotkeyError.Visibility == Visibility.Visible ? HotkeyInput
                : StepList;
            NavList.SelectedIndex = first == StepList ? 2 : 0; // Bereich mit dem Fehler zeigen
            first.Focus();
            first.BringIntoView();
            return;
        }
        _profile.Name = NameBox.Text.Trim();
        _profile.Hotkey = HotkeyInput.Hotkey;
        _profile.Steps = _steps.Select(s => { s.TryGetStep(out var step); return step; }).ToList();
        _profile.OverlayOnActivate = OverlayOnBox.IsChecked == true;
        _profile.OverlayOnDeactivate = OverlayOffBox.IsChecked == true;
        _profile.Sound = SoundBox.IsChecked == true;
        _profile.SoundVariant = SelectedSound;
        _profile.Mode = SelectedMode;
        _profile.Trigger = SelectedTrigger;
        _profile.PassThrough = HotkeyInput.Hotkey?.IsMouse == true && PassThroughBox.IsChecked == true;
        DialogResult = true;
    }
}
