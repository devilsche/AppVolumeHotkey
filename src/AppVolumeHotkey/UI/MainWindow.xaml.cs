using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppVolumeHotkey.Audio;
using AppVolumeHotkey.Core.Model;
using Microsoft.Win32;

namespace AppVolumeHotkey.UI;

public partial class MainWindow : Window
{
    private const string DragFormat = "AppVolumeHotkey.Exe";
    private readonly AppHost _host;
    private readonly System.Collections.ObjectModel.ObservableCollection<RunningAppRow> _running = new();
    private List<AppChipRow> _chips = new();
    private readonly System.Windows.Threading.DispatcherTimer _liveTimer;
    private Point _dragStart;
    private bool _forceClose;

    public MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        Title = $"AppVolumeHotkey {Platform.AppInfo.Version}";
        RestorePlacement();
        VersionLabel.Text = "v" + Platform.AppInfo.Version;

        _host.ConfigChanged += Refresh;
        _host.Volume.StateChanged += RefreshProfiles;
        _host.Audio.SessionsChanged += RefreshRunning;
        _host.Audio.Polled += UpdateRunningVolumes;
        RunningList.ItemsSource = _running;

        // Live-Anzeige: Lautstärken alle 250 ms lesen, solange das Fenster sichtbar ist (billig, ohne Neu-Abfrage der Sessions)
        _liveTimer = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(250),
            System.Windows.Threading.DispatcherPriority.Normal, (_, _) => UpdateRunningVolumes(), Dispatcher);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { _liveTimer.Start(); UpdateRunningVolumes(); }
            else _liveTimer.Stop();
        };

        Refresh();
        RefreshRunning();
        if (_host.Config.Groups.Count > 0)
            GroupList.SelectedIndex = 0;
    }

    private AppGroup? SelectedGroup => GroupList.SelectedItem as AppGroup;

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    /// <summary>Gespeicherte Größe/Position übernehmen, sofern sie noch auf einen Bildschirm passt; sonst Standard (an Arbeitsfläche angepasst).</summary>
    private void RestorePlacement()
    {
        var area = SystemParameters.WorkArea;
        var virt = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (_host.Config.Settings.MainWindow is { } p
            && p.Width >= MinWidth && p.Height >= MinHeight
            && virt.IntersectsWith(new Rect(p.Left, p.Top, p.Width, p.Height))
            && virt.Contains(new Point(p.Left + 40, p.Top + 20))) // Titelleiste erreichbar
        {
            if (p.GroupsWidth is >= 170 and <= 800)
                GroupsColumn.Width = new GridLength(p.GroupsWidth.Value);
            if (p.RunningWidth is >= 200 and <= 900)
                RunningColumn.Width = new GridLength(p.RunningWidth.Value);
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = p.Left;
            Top = p.Top;
            Width = p.Width;
            Height = p.Height;
            if (p.Maximized)
                Loaded += (_, _) => WindowState = WindowState.Maximized;
            return;
        }
        Width = Math.Min(Width, area.Width - 40);
        Height = Math.Min(Height, area.Height - 40);
    }

    internal void SavePlacementPublic()
    {
        if (IsVisible)
            SavePlacement();
    }

    private void SavePlacement()
    {
        var r = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (r.IsEmpty || r.Width < 100)
            return;
        var p = new WindowPlacement(r.Left, r.Top, r.Width, r.Height, WindowState == WindowState.Maximized)
        {
            GroupsWidth = GroupsColumn.ActualWidth,
            RunningWidth = RunningColumn.ActualWidth,
        };
        if (p == _host.Config.Settings.MainWindow)
            return;
        _host.Config.Settings.MainWindow = p;
        _host.SaveSettingsOnly();
    }

    private WindowState _lastNonMinimized = WindowState.Normal;

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState != WindowState.Minimized)
        {
            _lastNonMinimized = WindowState;
            return;
        }
        if (_host.Config.Settings.MinimizeToTray)
        {
            SavePlacement();
            Hide();                           // nur noch im Infobereich
            WindowState = _lastNonMinimized;  // beim nächsten Öffnen wie vorher (normal/maximiert)
        }
    }

    private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) => SavePlacement();

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        // Schließen = in den Infobereich minimieren; Beenden nur über das Tray-Menü
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _host.ConfigChanged -= Refresh;
        _host.Volume.StateChanged -= RefreshProfiles;
        _host.Audio.SessionsChanged -= RefreshRunning;
        _host.Audio.Polled -= UpdateRunningVolumes;
        _liveTimer.Stop();
        base.OnClosed(e);
    }

    // ---------- Anzeige ----------

    private void Refresh()
    {
        var selectedId = SelectedGroup?.Id;
        GroupList.ItemsSource = null;
        GroupList.ItemsSource = _host.Config.Groups;
        if (selectedId is not null)
            GroupList.SelectedItem = _host.Config.Groups.FirstOrDefault(g => g.Id == selectedId);
        PausedHint.Visibility = _host.HotkeysPaused ? Visibility.Visible : Visibility.Collapsed;
        RefreshGroup();
    }

    private void RefreshGroup()
    {
        var g = SelectedGroup;
        var hasGroup = g is not null;
        GroupPanel.Visibility = hasGroup ? Visibility.Visible : Visibility.Collapsed;
        NoGroupHint.Visibility = hasGroup ? Visibility.Collapsed : Visibility.Visible;
        RenameGroupButton.IsEnabled = hasGroup;
        DeleteGroupButton.IsEnabled = hasGroup;
        UpdateInGroupMarks();
        if (g is null)
            return;
        GroupTitle.Text = $"Gruppe „{g.Name}“";
        _chips = g.Apps.Select(a => new AppChipRow(a)).ToList();
        AppChips.ItemsSource = _chips;
        UpdateRunningVolumes();
        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        var g = SelectedGroup;
        if (g is null)
        {
            ProfileList.ItemsSource = null;
            return;
        }
        var active = _host.Volume.GetActive(g.Id);

        // Gleiche Profile (gleiche Objekte, gleiche Reihenfolge)? Dann nur den Status aktualisieren – kein Flackern.
        if (ProfileList.ItemsSource is List<ProfileRow> current
            && current.Count == g.Profiles.Count
            && current.Select(r => r.Profile).SequenceEqual(g.Profiles))
        {
            foreach (var row in current)
                row.Update(active, _host.FailedHotkeys.Contains(row.Profile.Id));
            return;
        }

        var selectedId = (ProfileList.SelectedItem as ProfileRow)?.Profile.Id;
        var rows = g.Profiles.Select(p => new ProfileRow(p, active, _host.FailedHotkeys.Contains(p.Id))).ToList();
        ProfileList.ItemsSource = rows;
        if (selectedId is not null)
            ProfileList.SelectedItem = rows.FirstOrDefault(r => r.Profile.Id == selectedId);
    }

    /// <summary>Liste in place abgleichen, damit Auswahl, Fokus und Scrollposition erhalten bleiben.</summary>
    private void RefreshRunning()
    {
        var apps = _host.Audio.GetRunningApps();
        var exes = apps.Select(a => a.Exe).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = _running.Count - 1; i >= 0; i--)
        {
            if (!exes.Contains(_running[i].Exe))
                _running.RemoveAt(i);
        }
        for (var i = 0; i < apps.Count; i++)
        {
            var existing = _running.FirstOrDefault(r => string.Equals(r.Exe, apps[i].Exe, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                _running.Insert(Math.Min(i, _running.Count), new RunningAppRow(apps[i]));
        }
        UpdateRunningVolumes();
    }

    private void UpdateRunningVolumes()
    {
        if (!IsVisible)
            return;
        foreach (var row in _running)
            row.VolumePercent = _host.Audio.GetCurrentVolume(row.Exe) is { } v ? (int)Math.Round(v * 100) : -1;
        UpdateInGroupMarks();
        foreach (var chip in _chips)
            chip.VolumePercent = _host.Audio.GetCurrentVolume(chip.Exe) is { } v ? (int)Math.Round(v * 100) : -1;
    }

    private void UpdateInGroupMarks()
    {
        var g = SelectedGroup;
        foreach (var row in _running)
        {
            row.IsInGroup = g?.ContainsApp(row.Exe) == true;
            var others = _host.Config.Groups.Where(x => x != g && x.ContainsApp(row.Exe)).Select(x => $"„{x.Name}“").ToList();
            var parts = new List<string>();
            if (row.IsInGroup)
                parts.Add($"✓ in „{g!.Name}“");
            if (others.Count > 0)
                parts.Add((row.IsInGroup ? "auch in " : "in ") + string.Join(", ", others));
            row.InGroupSuffix = parts.Count == 0 ? "" : " · " + string.Join(", ", parts);
        }
        UpdateAddRunningButton();
    }

    private void UpdateAddRunningButton() =>
        AddRunningButton.IsEnabled = SelectedGroup is not null && RunningList.SelectedItem is RunningAppRow { IsInGroup: false };

    private void RunningList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAddRunningButton();

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        UpdateRunningVolumes();
    }

    // ---------- Gruppen ----------

    private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshGroup();

    private void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        var name = InputDialog.Ask(this, "Neue Gruppe", "Name der Gruppe", "Voice");
        if (name is null)
            return;
        var g = new AppGroup { Name = name };
        _host.Config.Groups.Add(g);
        _host.SaveConfig();
        GroupList.SelectedItem = g;
        GroupList.Focus();
    }

    private void RenameGroup_Click(object sender, RoutedEventArgs e) => RenameGroup();

    private void RenameGroup()
    {
        if (SelectedGroup is not { } g)
            return;
        var name = InputDialog.Ask(this, "Gruppe umbenennen", "Name der Gruppe", g.Name);
        if (name is null)
            return;
        g.Name = name;
        _host.SaveConfig();
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e) => DeleteGroup();

    private void DeleteGroup()
    {
        if (SelectedGroup is not { } g)
            return;
        var answer = MessageBox.Show(this, $"Gruppe „{g.Name}“ mit allen Profilen löschen?", "Gruppe löschen",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;
        _host.Config.Groups.Remove(g);
        _host.SaveConfig();
        if (_host.Config.Groups.Count > 0)
            GroupList.SelectedIndex = 0;
    }

    private void GroupList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2) { RenameGroup(); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteGroup(); e.Handled = true; }
    }

    // ---------- Apps ----------

    private void AddAppToGroup(string exe)
    {
        if (SelectedGroup is not { } g || string.IsNullOrWhiteSpace(exe))
            return;
        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exe += ".exe";
        if (g.ContainsApp(exe))
            return;
        g.Apps.Add(exe);
        _host.SaveConfig();
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup is not { } g || (sender as Button)?.Tag is not string exe)
            return;
        g.Apps.RemoveAll(a => string.Equals(a, exe, StringComparison.OrdinalIgnoreCase));
        _host.SaveConfig();
        AddExeButton.Focus();
    }

    private void AddExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "App auswählen",
            Filter = "Programme (*.exe)|*.exe",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) == true)
            AddAppToGroup(Path.GetFileName(dlg.FileName));
    }

    private void AddRunning_Click(object sender, RoutedEventArgs e)
    {
        if (RunningList.SelectedItem is RunningAppRow { IsInGroup: false } app)
            AddAppToGroup(app.Exe);
    }

    private void RunningList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RunningList.SelectedItem is RunningAppRow { IsInGroup: false } app)
            AddAppToGroup(app.Exe);
    }

    private void RunningList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(null);

    private void RunningList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        var diff = _dragStart - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is not RunningAppRow app || app.IsInGroup)
            return;
        DragDrop.DoDragDrop(RunningList, new DataObject(DragFormat, app.Exe), DragDropEffects.Copy);
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DragFormat) || e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.BorderThickness = new Thickness(ok ? 3 : 2);
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e) => DropZone.BorderThickness = new Thickness(2);

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.BorderThickness = new Thickness(2);
        if (e.Data.GetData(DragFormat) is string exe)
        {
            AddAppToGroup(exe);
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            // .exe-Dateien oder Verknüpfungen aus dem Explorer
            foreach (var f in files.Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                AddAppToGroup(Path.GetFileName(f));
        }
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as T;
    }

    // ---------- Profile ----------

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup is not { } g)
            return;
        var p = new Profile { Name = "Leise", Mode = ValueMode.Relative, Steps = { Step.Percent(20), Step.Baseline } };
        if (ProfileDialog.Edit(this, _host, g, p, isNew: true) is { } result)
        {
            g.Profiles.Add(result);
            _host.SaveConfig();
        }
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ProfileRow row)
            EditProfile(row.Profile);
    }

    private void ProfileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProfileList.SelectedItem is ProfileRow row)
            EditProfile(row.Profile);
    }

    private void ProfileList_KeyDown(object sender, KeyEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileRow row)
            return;
        if (e.Key == Key.Enter) { EditProfile(row.Profile); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteProfile(row.Profile); e.Handled = true; }
    }

    private void EditProfile(Profile profile)
    {
        if (SelectedGroup is not { } g)
            return;
        if (ProfileDialog.Edit(this, _host, g, profile.Clone(), isNew: false) is not { } result)
            return;
        var index = g.Profiles.FindIndex(p => p.Id == profile.Id);
        if (index < 0)
            return;
        g.Profiles[index] = result;
        _host.SaveConfig();
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ProfileRow row)
            DeleteProfile(row.Profile);
    }

    private void DeleteProfile(Profile profile)
    {
        if (SelectedGroup is not { } g)
            return;
        var answer = MessageBox.Show(this, $"Profil „{profile.Name}“ löschen?", "Profil löschen",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;
        g.Profiles.RemoveAll(p => p.Id == profile.Id);
        _host.SaveConfig();
    }

    // ---------- Einstellungen ----------

    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsWindow.Open(this, _host);

    private void About_Click(object sender, RoutedEventArgs e) => AboutWindow.Show(this);
}
