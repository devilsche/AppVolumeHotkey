using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppVolumeHotkey.Core.Model;
using AppVolumeHotkey.Input;

namespace AppVolumeHotkey.UI;

public partial class ProfileDialog : Window
{
    private const string DragFormat = "AppVolumeHotkey.StepItem";
    private readonly AppHost _host;
    private readonly Profile _profile;
    private readonly Hotkey? _originalHotkey;
    private readonly ObservableCollection<StepItem> _steps = new();
    private bool _loaded;

    private ProfileDialog(Window owner, AppHost host, AppGroup group, Profile profile, bool isNew)
    {
        Owner = owner;
        _host = host;
        _profile = profile;
        _originalHotkey = isNew ? null : profile.Hotkey;
        InitializeComponent();

        Title = isNew ? "Neues Profil" : "Profil bearbeiten";
        Heading.Text = isNew ? $"Neues Profil in „{group.Name}“" : $"Profil „{profile.Name}“ in „{group.Name}“";
        NameBox.Text = profile.Name;
        HotkeyInput.Hotkey = profile.Hotkey;
        foreach (var s in profile.Steps)
            _steps.Add(Track(new StepItem(s)));
        StepList.ItemsSource = _steps;
        OverlayOnBox.IsChecked = profile.OverlayOnActivate;
        OverlayOffBox.IsChecked = profile.OverlayOnDeactivate;
        SoundBox.IsChecked = profile.Sound;

        HotkeyInput.HotkeyChanged += () => { if (_loaded) Validate(); };
        _steps.CollectionChanged += (_, _) => { Renumber(); if (_loaded) Validate(); };
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
    public static Profile? Edit(Window owner, AppHost host, AppGroup group, Profile profile, bool isNew)
    {
        var d = new ProfileDialog(owner, host, group, profile, isNew);
        return d.ShowDialog() == true ? d._profile : null;
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

    private bool Validate()
    {
        var ok = true;

        ok &= SetError(NameBox, NameError, string.IsNullOrWhiteSpace(NameBox.Text) ? "Bitte einen Namen eingeben." : null);

        string? hotkeyError = null;
        if (HotkeyInput.Hotkey is { } hk)
        {
            var other = _host.Config.AllProfiles().FirstOrDefault(x => x.Profile.Id != _profile.Id && x.Profile.Hotkey == hk);
            if (other.Profile is not null)
                hotkeyError = $"{KeyNames.Format(hk)} wird bereits von „{other.Profile.Name}“ (Gruppe „{other.Group.Name}“) verwendet.";
            else if (hk != _originalHotkey && !_host.HotkeysPaused && !_host.Hotkeys.IsAvailable(hk))
                hotkeyError = $"{KeyNames.Format(hk)} ist von Windows oder einem anderen Programm belegt. Bitte eine andere Kombination wählen.";
        }
        ok &= SetError(HotkeyInput, HotkeyError, hotkeyError);

        string? stepsError = null;
        if (_steps.Count == 0)
            stepsError = "Die Abfolge braucht mindestens eine Stufe.";
        else if (_steps.FirstOrDefault(s => !s.TryGetStep(out _)) is { } bad)
            stepsError = $"Stufe {bad.Number}: Bitte eine ganze Zahl von 0 bis 100 eingeben.";
        ok &= SetError(StepList, StepsError, stepsError);

        SaveButton.IsEnabled = ok;
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
            return;
        _profile.Name = NameBox.Text.Trim();
        _profile.Hotkey = HotkeyInput.Hotkey;
        _profile.Steps = _steps.Select(s => { s.TryGetStep(out var step); return step; }).ToList();
        _profile.OverlayOnActivate = OverlayOnBox.IsChecked == true;
        _profile.OverlayOnDeactivate = OverlayOffBox.IsChecked == true;
        _profile.Sound = SoundBox.IsChecked == true;
        DialogResult = true;
    }
}
