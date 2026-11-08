# AppVolumeHotkey Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows-Tray-App, die per globalem Hotkey die Lautstärke von App-Gruppen über Profil-Abfolgen umschaltet.

**Architecture:** Plattformunabhängige Kernbibliothek (Modelle, Konfig-Persistenz, Zustandsmaschine `VolumeController`) hinter `IAudioBackend`/`IBaselineStore`, voll unit-getestet unter Linux. WPF-App (net10.0-windows) liefert Core-Audio-Backend (NAudio, mit Fading), Hotkeys (`RegisterHotKey`), Tray, Overlay und GUI. GitHub Actions baut auf Windows die Single-File-exe.

**Tech Stack:** .NET 10, C#, WPF (Fluent, `ThemeMode="System"`), NAudio.Wasapi 2.2.1, H.NotifyIcon.Wpf, CommunityToolkit.Mvvm, xUnit, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-08-app-volume-hotkey-design.md`

## Global Constraints

- UI-Texte Deutsch; Hell/Dunkel folgt Windows; alles per Tastatur bedienbar, sichtbarer Fokus, `AutomationProperties.Name` an Icon-Buttons; Status nie nur über Farbe.
- Stufenwerte absolut 0–100 %; Übergang Standard 200 ms (0–1000); Overlay-Anzeigedauer Standard 1500 ms.
- Apps über .exe-Namen, case-insensitive (`StringComparer.OrdinalIgnoreCase`), immer mit Endung `.exe`.
- Konfig: `%APPDATA%\AppVolumeHotkey\config.json`; Baselines: `%APPDATA%\AppVolumeHotkey\state.json`.
- Hotkeys nur Tastatur via `RegisterHotKey` + `MOD_NOREPEAT`.
- Kein Eingriff in fremde Prozesse außer `ISimpleAudioVolume`.
- Ausgabe: `AppVolumeHotkey.exe`, self-contained, single-file, win-x64.

## Review Focus

1. Hotkey gedrückt, während keine App der Gruppe läuft → kein Fehler, Profil schaltet trotzdem, Overlay zeigt Stufe (Test in Task 3).
2. App mit mehreren Prozessen/Sessions (Discord, Chrome) → alle Sessions bekommen den Wert (Backend setzt pro exe alle Sessions; Task 4, manuell).
3. Profil/Gruppe bearbeitet oder gelöscht, während aktiv → Gruppe wird vorher auf Ausgangswerte zurückgesetzt (Test in Task 3).
4. Kaputte `config.json` → Backup + Defaults, App startet (Test in Task 2).
5. Hotkey von anderem Programm belegt → Fehlermeldung im Profil-Dialog, kein stilles Versagen (Task 8, manuell).

---

### Task 1: Gerüst, SDK, CI, Repo

**Files:** Create `AppVolumeHotkey.sln`, `src/AppVolumeHotkey.Core/AppVolumeHotkey.Core.csproj` (net10.0), `src/AppVolumeHotkey/AppVolumeHotkey.csproj` (net10.0-windows, WPF, `EnableWindowsTargeting`, NoWarn `WPF0001`), `tests/AppVolumeHotkey.Core.Tests/…csproj` (xUnit), `.github/workflows/build.yml`, `.gitignore` (inkl. `.superpowers/`, `bin/`, `obj/`), `README.md`.

- [ ] .NET 10 SDK nach `~/.dotnet` via `dotnet-install.sh --channel 10.0` installieren.
- [ ] Projekte anlegen; `dotnet build` (Linux) grün.
- [ ] Workflow: `windows-latest`, `actions/setup-dotnet@v4` (10.0.x), `dotnet test`, `dotnet publish src/AppVolumeHotkey -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o out`; `actions/upload-artifact@v4` (`out/AppVolumeHotkey.exe`); bei Tag `v*` `softprops/action-gh-release@v2`.
- [ ] `git init`, Commit, `gh repo create devilsche/AppVolumeHotkey --private --source . --push`.

### Task 2: Modelle + ConfigStore

**Files:** `src/AppVolumeHotkey.Core/Model/*.cs`, `Persistence/ConfigStore.cs`, `Persistence/JsonBaselineStore.cs`; Tests `ConfigStoreTests.cs`.

**Produces:**
- `enum StepKind { Percent, Baseline }`; `record Step(StepKind Kind, int Percent = 0)` mit `string Label` („20 %“ / „Ausgangswert“).
- `[Flags] enum HotkeyModifiers { None=0, Alt=1, Control=2, Shift=4, Win=8 }`; `record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)`.
- `class Profile { Guid Id; string Name; Hotkey? Hotkey; List<Step> Steps; bool OverlayOnActivate=true; bool OverlayOnDeactivate=true; bool Sound=false; }`
- `class AppGroup { Guid Id; string Name; List<string> Apps; List<Profile> Profiles; }`
- `enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }`, `enum OverlaySize { Small, Medium, Large }`
- `class AppSettings { bool StartWithWindows; bool StartMinimized=true; bool RunAsAdmin; OverlayCorner OverlayCorner=TopRight; OverlaySize OverlaySize=Medium; int OverlayDurationMs=1500; int FadeDurationMs=200; }`
- `class AppConfig { List<AppGroup> Groups; AppSettings Settings; }`
- `static class ConfigStore { AppConfig Load(string path); void Save(string path, AppConfig cfg); }` – atomar (tmp + Move); bei fehlender Datei Defaults; bei kaputter Datei Umbenennen nach `config.json.broken-yyyyMMddHHmmss` und Defaults.
- `interface IBaselineStore { Dictionary<Guid, Dictionary<string,float>> Load(); void Save(Dictionary<Guid, Dictionary<string,float>> b); }`, Implementierung `JsonBaselineStore(string path)`.

- [ ] Tests: `Load_MissingFile_ReturnsDefaults`, `SaveThenLoad_RoundTrips` (Gruppe mit 2 Apps, Profil mit Steps `20 %`,`Ausgangswert`, Hotkey Shift+F9 = (Shift, 0x78)), `Load_CorruptFile_BacksUpAndReturnsDefaults` (Datei `"{nicht json"` → Defaults, `.broken-*` existiert). Rot → implementieren → grün → Commit.

### Task 3: VolumeController (Zustandsmaschine)

**Files:** `src/AppVolumeHotkey.Core/Audio/IAudioBackend.cs`, `VolumeController.cs`, `HotkeyResult.cs`; Tests `VolumeControllerTests.cs` mit `FakeAudioBackend`, `InMemoryBaselineStore`.

**Produces:**
- `interface IAudioBackend { float? GetVolume(string exe); void SetVolume(string exe, float volume01); }` – `GetVolume` null wenn keine Session; `SetVolume` setzt alle Sessions der exe (Fading ist Sache des Backends).
- `record HotkeyResult(AppGroup Group, Profile Profile, Step Step, bool IsActive, bool ShowOverlay, bool PlaySound)`
- `class VolumeController(IAudioBackend audio, IBaselineStore store)`:
  - `void SetConfig(AppConfig cfg)` – setzt jede Gruppe zurück, deren aktives Profil gelöscht/geändert (Steps oder Apps) wurde oder deren Gruppe gelöscht wurde.
  - `HotkeyResult? OnHotkey(Guid profileId)`
  - `void OnSessionAdded(string exe)`
  - `void ResetGroup(Guid groupId)`, `void ResetAll()`
  - `void RestorePersistedBaselines()` (beim Start)
  - `(Guid ProfileId, int StepIndex)? GetActive(Guid groupId)`
  - `event Action? StateChanged`
- Regeln exakt nach Spec 5.3/5.4: Baseline pro Gruppe pro exe, gemerkt beim Aktivieren aus `GetVolume` (null → nicht merken); Wechsel auf anderes Profil behält Baseline; Stufe „Ausgangswert“ → restore, inaktiv, Baselines verwerfen; App neu: vorhandene Baseline behalten, sonst merken, dann aktuellen Stufenwert setzen; jede Baseline-Änderung → `store.Save`. Persistierte Baselines beim Start: sofort setzen, wo `GetVolume != null`, sonst pending bis `OnSessionAdded`; danach aus Store entfernen.
- `ShowOverlay = IsActive ? OverlayOnActivate : OverlayOnDeactivate`, `PlaySound = Profile.Sound`.

- [ ] Tests (Fake-Volumes: Discord 0.7, ts3 1.0):
  - `FirstPress_RemembersBaselineAndAppliesFirstStep` → Discord 0.2, IsActive.
  - `SecondPress_TogglesBackToBaseline` (`20 %→Ausgangswert`) → Discord 0.7, ts3 1.0, inaktiv.
  - `ThreeSteps_Cycle` (`20→50→Ausgangswert`).
  - `NoBaselineStep_CyclesForever` (`20→100`): 4 Drücke → 0.2,1.0,0.2,1.0, immer aktiv.
  - `OtherProfileSameGroup_KeepsOriginalBaseline`: P1 (0.2), Q (`50→Ausgangswert`) → 0.5 → 0.7.
  - `AfterReturn_NextPressRemembersNewBaseline`: nach Rückkehr manuell 0.4, erneut → Rückkehr 0.4.
  - `AppNotRunning_StillToggles`: Gruppe ohne laufende App → Result nicht null, kein Throw.
  - `SessionAddedWhileActive_GetsStepValue_AndKeepsOldBaseline`: Discord beendet (null), dann wieder mit 0.2 → OnSessionAdded → Baseline bleibt 0.7.
  - `SessionAddedWhileActive_NewApp_RemembersCurrent`.
  - `SetConfig_ActiveProfileDeleted_ResetsGroup`.
  - `RestorePersistedBaselines_AppliesAndClears` (+ pending via OnSessionAdded).
  - `ResetAll_RestoresEverything`.
- [ ] Rot → implementieren → grün → Commit, Push.

### Task 4: Windows-Audio-Backend + Hotkeys

**Files:** `src/AppVolumeHotkey/Audio/CoreAudioBackend.cs`, `Input/HotkeyManager.cs`, `Input/KeyNames.cs`.

**Produces:**
- `sealed class CoreAudioBackend : IAudioBackend, IDisposable` – `int FadeDurationMs { get; set; }`; `IReadOnlyList<RunningApp> GetRunningApps()` (`record RunningApp(string Exe, string DisplayName, ImageSource? Icon)`); `event Action<string>? SessionAdded`. Polling per `DispatcherTimer` 1 s über alle aktiven Render-Geräte (`MMDeviceEnumerator`, `AudioSessionManager.RefreshSessions()`), Session-Identität = `GetSessionInstanceIdentifier`; Prozessname via `Process.GetProcessById(pid).ProcessName + ".exe"`, PID 0 (Systemsounds) ignorieren. `SetVolume`: lineares Fading pro exe mit 15-ms-`DispatcherTimer`, laufendes Fading derselben exe wird ersetzt (Start = aktueller Ist-Wert); bei 0 ms sofort.
- `sealed class HotkeyManager : IDisposable` – eigenes `HwndSource`-Message-Window; `bool Register(int id, Hotkey hk)`; `void Unregister(int id)`; `void UnregisterAll()`; `bool IsAvailable(Hotkey hk)` (Probe-Register/Unregister); `event Action<int>? Pressed`; `MOD_NOREPEAT=0x4000`, `WM_HOTKEY=0x0312`.
- `static class KeyNames { string Format(Hotkey hk); }` → z. B. „Shift+F9“, „F23“ (über `KeyInterop.KeyFromVirtualKey`).

- [ ] `dotnet build` grün (Linux). Commit, Push, CI grün.

### Task 5: App-Shell (Start, Einzelinstanz, Tray, Admin, Autostart)

**Files:** `App.xaml(.cs)`, `AppHost.cs` (verdrahtet alles), `System/SingleInstance.cs`, `System/Autostart.cs`, `System/Elevation.cs`, `Tray/TrayController.cs`, `Assets/app.ico`.

- [ ] `App.ThemeMode = ThemeMode.System`; `ShutdownMode=OnExplicitShutdown`.
- [ ] Einzelinstanz: `Mutex "Local\\AppVolumeHotkey"`; zweite Instanz signalisiert `EventWaitHandle "Local\\AppVolumeHotkey.Show"` und beendet sich.
- [ ] `RunAsAdmin && !IsElevated` → Neustart mit `Verb="runas"` (+ gleiche Argumente), dann Exit; UAC abgelehnt → normal weiterlaufen + Tray-Hinweis.
- [ ] `Autostart.Apply(AppSettings s, bool elevated)`: ohne Admin `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` Wert `AppVolumeHotkey` = `"<exe>" --autostart`; mit Admin `schtasks /Create /TN AppVolumeHotkey /TR "\"<exe>\" --autostart" /SC ONLOGON /RL HIGHEST /F` und Run-Wert entfernen; aus → beides entfernen (schtasks-Fehler ignorieren).
- [ ] Start: Config laden, `VolumeController.RestorePersistedBaselines()`, Hotkeys aller Profile registrieren (Fehlschlag → Tray-Hinweis „Hotkey X belegt“), Hauptfenster nur zeigen wenn nicht (`--autostart` oder StartMinimized).
- [ ] Tray (H.NotifyIcon): Linksklick öffnet Hauptfenster; Menü: aktive Profile (deaktiviert, nur Anzeige „Voice: Voice leise · 20 %“), „Alle zurücksetzen“, „Hotkeys pausieren“ (Checkbox), „Öffnen“, „Beenden“. Beenden → `ResetAll()` → Exit.
- [ ] Build grün, Commit, Push.

### Task 6: Overlay + Ton

**Files:** `Overlay/OverlayWindow.xaml(.cs)`, `Overlay/OverlayService.cs`, `System/WindowsTheme.cs`.

- [ ] `OverlayService.Show(HotkeyResult r, AppSettings s)`: Text `"{Profil} · {Step.Label} · AKTIV"` bzw. `"{Profil} · Ausgangswert · AUS"`; Ecke/Größe (Schrift 14/18/24 pt) aus Settings, 16 px Abstand zur `SystemParameters.WorkArea`; Ausblenden nach `OverlayDurationMs`, 150-ms-Fade nur wenn `SystemParameters.ClientAreaAnimation`.
- [ ] Fenster: `WindowStyle=None`, `AllowsTransparency`, `Topmost`, `ShowActivated=false`, `ShowInTaskbar=false`; ExStyles `WS_EX_TRANSPARENT|WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW|WS_EX_LAYERED`.
- [ ] Farben aus `WindowsTheme.IsDark()` (Registry `AppsUseLightTheme`): dunkel `#E6202020`/weiß, hell `#F2F3F3F3`/schwarz.
- [ ] Ton: aktiv `SystemSounds.Asterisk`, inaktiv `SystemSounds.Beep`.
- [ ] Build grün, Commit, Push.

### Task 7: Hauptfenster (Layout A)

**Files:** `UI/MainWindow.xaml(.cs)`, `UI/MainViewModel.cs`, `UI/GroupViewModel.cs`, `UI/ProfileRowViewModel.cs`.

- [ ] Drei Spalten (Grid 1*:2*:1*), mind. 900×560. Links Gruppenliste + „Neue Gruppe“, Umbenennen (F2, Inline-TextBox), Löschen (Entf, mit Bestätigung).
- [ ] Mitte: Apps als Chips mit ✕-Button (`AutomationProperties.Name="Discord.exe entfernen"`), Drop-Fläche „App hierher ziehen“ (`AllowDrop`, Datenformat `DataFormats.Text` = exe), „App per .exe hinzufügen …“ (`OpenFileDialog` *.exe → `Path.GetFileName`). Profile-Liste: Hotkey (`KeyNames.Format` / „kein Hotkey“), Name, Abfolge („20 % → Ausgangswert“), Status-Text „● AKTIV · 20 %“, Buttons „Bearbeiten“, „Löschen“; „+ Profil“.
- [ ] Rechts: laufende Apps (Icon + Name) live aus `CoreAudioBackend.GetRunningApps()`, Drag-Quelle; Button „Zur Gruppe hinzufügen“ für Tastatur.
- [ ] Zahnrad-Button „Einstellungen“. Schließen → `Hide()` statt Exit.
- [ ] Jede Änderung → `ConfigStore.Save` + `VolumeController.SetConfig` + Hotkeys neu registrieren.
- [ ] Build grün, Commit, Push.

### Task 8: Profil-Dialog + Einstellungen

**Files:** `UI/ProfileDialog.xaml(.cs)`, `UI/ProfileDialogViewModel.cs`, `UI/SettingsWindow.xaml(.cs)`.

- [ ] Profil-Dialog modal: Name; Hotkey-Feld `HotkeyBox` (eigenes Control, fokussierbar, Anzeige „Taste drücken …“ im Fokus): `PreviewKeyDown` übernimmt jede Nicht-Modifier-Taste inkl. `e.SystemKey` mit Modifiern aus `Keyboard.Modifiers` + LWin/RWin-Status, `e.Handled=true`; Esc = unverändert verlassen, Entf/Rücktaste ohne Modifier = Hotkey löschen; Tab ohne Modifier = normale Fokus-Navigation; Stufenliste: Prozent-Stufen als NumericTextBox 0–100, „Ausgangswert“-Stufe mit Akzent-Rahmen + Text; „+ Prozent-Stufe“, „+ Ausgangswert“, je Stufe „nach oben“/„nach unten“/✕ (Alt+↑/↓ als Shortcut) und Drag-Umsortieren; Checkboxen Overlay aktivieren/deaktivieren, Ton; Abbrechen/Speichern.
- [ ] Validierung (Fehlertext unter Feld, `AutomationProperties.HelpText`, Speichern deaktiviert): Name leer; Abfolge leer; Prozent außerhalb 0–100; Hotkey bei anderem Profil vergeben („Bereits von ‚X‘ verwendet“); Hotkey systemweit belegt (`HotkeyManager.IsAvailable`, außer es ist der eigene aktuelle) → „Von einem anderen Programm belegt“.
- [ ] Einstellungen: Checkboxen Mit Windows starten / Minimiert starten / Mit Administratorrechten ausführen (Hinweis: „Nötig, wenn Spiele als Administrator laufen“), Overlay-Ecke (ComboBox), Größe, Anzeigedauer (ms), Übergangsdauer (ms, 0–1000). Speichern → `Autostart.Apply`; RunAsAdmin neu an und nicht elevated → Neustart als Admin anbieten.
- [ ] Build grün, Commit, Push, CI grün.

### Task 9: Release

- [ ] README (Download, Bedienung, bekannte Grenzen: exklusiver Vollbildmodus, Admin-Spiele, SmartScreen-Warnung bei unsignierter exe).
- [ ] Tag `v0.1.0` pushen; CI-Release mit `AppVolumeHotkey.exe` prüfen.
