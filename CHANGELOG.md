# Changelog

Jede Version auf Deutsch und Englisch. Der Abschnitt einer Version wird automatisch als Beschreibung des GitHub-Releases verwendet.
Every version in German and English. A version's section is used automatically as the GitHub release description.

## [0.15.0] – 2026-11-09

### Deutsch
- Neu: Stufe **„Stumm“** – schaltet die App im Windows-Mixer wirklich stumm (Lautstärke bleibt erhalten), auch mit „Halten“.
- Neu: **Lautstärke festhalten** pro Gruppe – setzt eine App (z. B. Discord) ihre Lautstärke selbst zurück, solange ein Profil aktiv ist, stellt AppVolumeHotkey den Profilwert wieder her.
- Neu: Einstellungen **exportieren und importieren** (Einstellungen → Allgemein) – als Backup oder für einen anderen PC.

### English
- New: **“Muted”** step – really mutes the app in the Windows mixer (volume is kept), also with “Hold”.
- New: **keep volume** per group – if an app (e.g. Discord) resets its own volume while a profile is active, AppVolumeHotkey restores the profile value.
- New: **export and import** settings (Settings → General) – as a backup or for another PC.

## [0.14.3] – 2026-11-09

### Deutsch
- „Laufende Apps“ wird auf den Seiten Mikrofon und Ducking ausgeblendet – dort hat die Liste keinen Zweck; die Mitte bekommt den Platz.

### English
- “Running apps” is hidden on the Microphone and Ducking pages, where it serves no purpose; the middle gets the space.

## [0.14.2] – 2026-11-09

### Deutsch
- Mikrofon-Zustand wird bei schmalem Fenster umgebrochen statt abgeschnitten.

### English
- Microphone state wraps instead of being cut off in narrow windows.

## [0.14.1] – 2026-11-09

### Deutsch
- Mikrofon-Modi heißen wie in Discord: „Stummschaltung umschalten“, „Push-to-Mute“, „Push-to-Talk“; Knopf wie in Windows: „Stummschalten“ / „Stummschaltung aufheben“.
- Hinweis, solange für das Mikrofon noch kein Hotkey festgelegt ist.
- „Funktionen“ sieht jetzt aus wie „Gruppen“; Zustand dezent als Symbol und kleiner Text statt großer Plakette.
- Listen mit abgerundeten Einträgen, Akzentstrich bei Auswahl und deutlichem Tastaturfokus.

### English
- Microphone modes use Discord's terms: “Toggle mute”, “Push to mute”, “Push to talk”; button as in Windows: “Mute” / “Unmute”.
- Hint while no microphone hotkey is set.
- “Features” now looks like “Groups”; state shown subtly as icon and small text instead of a large badge.
- Lists with rounded entries, accent bar for the selection and clear keyboard focus.

## [0.14.0] – 2026-11-09

### Deutsch
- Neu: **Mikrofon stumm** per Hotkey (auch Maustasten) – wirkt in allen Programmen. Drei Modi: Umschalten, Halten zum Stummschalten, Push-to-Talk. Wahlweise alle Mikrofone oder nur das Standard-Mikrofon; beim Beenden wird es wieder eingeschaltet (abschaltbar).
- Neu: **Ducking** – gibt eine Gruppe (z. B. „Voice“) Ton aus, werden alle anderen Apps oder ausgewählte Gruppen automatisch leiser und nach kurzer Stille wieder lauter. Empfindlichkeit mit Live-Pegel einstellbar, optional per Hotkey an/aus.
- Beides links unter „Funktionen“ im Hauptfenster; Zustand auch im Infobereich-Menü, im Overlay (Mikrofon rot), in der Statusanzeige („MIC STUMM“) und am Infobereich-Symbol (roter Punkt).
- Overlay: „Darstellung“ und „Größe“ zu einer Einstellung „Größe“ (Klein/Mittel/Groß) zusammengefasst.
- Hotkeys werden jetzt auch gegen Mikrofon- und Ducking-Hotkey auf doppelte Belegung geprüft.

### English
- New: **mute microphone** by hotkey (also mouse buttons) – works in every program. Three modes: toggle, hold to mute, push-to-talk. Either all microphones or only the default one; it's turned back on when the app exits (optional).
- New: **ducking** – when a group (e.g. “Voice”) plays audio, all other apps or selected groups get quieter automatically and louder again after a short silence. Sensitivity adjustable with a live level meter, optional on/off hotkey.
- Both under “Features” on the left of the main window; state also shown in the tray menu, the overlay (microphone in red), the status indicator (“MIC MUTED”) and the tray icon (red dot).
- Overlay: “Style” and “Size” merged into one “Size” setting (small/medium/large).
- Hotkeys are now also checked against the microphone and ducking hotkeys for duplicates.

## [0.13.0] – 2026-11-09

### Deutsch
- Aufgeräumte Oberfläche nach UX-Prüfung: mehr Luft, einheitliche Abstände, Schriftgrößen und Windows-Symbole.
- Einstellungen in Bereiche aufgeteilt: Allgemein, Overlay, Statusanzeige.
- Profil-Dialog in übersichtlichen Abschnitten; Speichern/Abbrechen bleiben immer sichtbar, auch auf kleinen Bildschirmen.
- Aktive Profile mit goldener Plakette; Bearbeiten/Löschen als Symbolknöpfe, nichts wird mehr abgeschnitten.
- Hinweise bei leeren Listen, kürzere und einheitlichere Texte („Infobereich“, „Ausgangswert“, „Lautstärke-Übergang“).
- Barrierefreiheit: eindeutige Alt-Tastenkürzel, bessere Screenreader-Namen, besser lesbare ausgegraute Einträge, größere Overlay-Schrift.

### English
- Cleaned-up interface after a UX review: more breathing room, consistent spacing, font sizes and Windows icons.
- Settings split into sections: General, Overlay, Status indicator.
- Profile dialog in clear sections; Save/Cancel always stay visible, even on small screens.
- Active profiles get a gold badge; Edit/Delete are icon buttons, nothing gets cut off anymore.
- Hints for empty lists, shorter and more consistent wording (“tray”, “original volume”, “volume fade”).
- Accessibility: unique access keys, better screen reader names, more readable greyed-out entries, larger overlay text.

## [0.12.2] – 2026-11-08

### Deutsch
- Beendete Apps verschwinden sofort aus „Laufende Apps“, auch wenn Windows ihre Audio-Sitzung noch eine Weile behält.

### English
- Closed apps disappear from “Running apps” right away, even if Windows keeps their audio session around for a while.

## [0.12.1] – 2026-11-08

### Deutsch
- Lange Beschriftungen von Häkchen und Optionsfeldern werden umgebrochen statt abgeschnitten.

### English
- Long checkbox and radio button labels now wrap instead of being cut off.

## [0.12.0] – 2026-11-08

### Deutsch
- Neu: Auslösen pro Profil – **Umschalten** (jeder Druck eine Stufe weiter) oder **Halten** (nur solange die Taste gedrückt ist; beim Loslassen zurück zum vorherigen Zustand, auch zu einem anderen aktiven Profil).
- Halten funktioniert auch mit Maustasten.

### English
- New: trigger mode per profile – **Toggle** (each press moves to the next step) or **Hold** (only while the key is held; on release it returns to the previous state, including another active profile).
- Hold also works with mouse buttons.

## [0.11.0] – 2026-11-08

### Deutsch
- Neue Apps werden sofort erkannt: Startet eine App, während ihre Gruppe aktiv ist, wird sie direkt leiser geschaltet – meist bevor der erste Ton zu hören ist.

### English
- New apps are detected instantly: if an app starts while its group is active, it is turned down right away – usually before the first sound.

## [0.10.0] – 2026-11-08

### Deutsch
- Neu: dauerhafte Statusanzeige, solange ein Profil aktiv ist – als Minitext (wie eine FPS-Anzeige), Symbol + Text oder nur Symbol; Ecke und Deckkraft einstellbar.
- Neu: grüner Punkt am Tray-Symbol, solange ein Profil aktiv ist.
- Beides in den Einstellungen abschaltbar.

### English
- New: permanent status indicator while a profile is active – as mini text (like an FPS counter), icon + text or icon only; corner and opacity adjustable.
- New: green dot on the tray icon while a profile is active.
- Both can be turned off in the settings.

## [0.9.0] – 2026-11-08

### Deutsch
- Neu: Sprachumschalter Deutsch/English (Standard: wie Windows), Wechsel ohne Neustart.

### English
- New: language switch German/English (default: same as Windows), changes without restart.

## [0.8.0] – 2026-11-08

### Deutsch
- Microsoft-Store-Paket (MSIX). In der Store-Version läuft der Autostart über Windows; der Admin-Modus entfällt dort.

### English
- Microsoft Store package (MSIX). In the Store version, autostart is managed by Windows; admin mode is not available there.

## [0.7.0] – 2026-11-08

### Deutsch
- Breiteres Standardfenster; Größe, Position und Spaltenbreiten werden gespeichert.
- Bereiche per Trenner in der Breite veränderbar.
- Neu: beim Minimieren in den Infobereich verschieben (abschaltbar).

### English
- Wider default window; size, position and column widths are remembered.
- Panels can be resized with splitters.
- New: minimize to the system tray (can be turned off).

## [0.6.0] – 2026-11-08

### Deutsch
- Keine Bibliotheken Dritter mehr: Audio-Zugriff und Tray-Symbol direkt gegen Windows programmiert.
- 0.6.1: Tray-Menü schloss sich sofort wieder – behoben.

### English
- No more third-party libraries: audio access and tray icon programmed directly against Windows.
- 0.6.1: tray menu closed immediately – fixed.

## [0.5.0] – 2026-11-08

### Deutsch
- Neues Overlay: Karte mit Icon, Status und goldenem Pegelbalken, kompakt oder normal; Balken läuft synchron zum Übergang.
- Eigene Bestätigungstöne in 5 Varianten.
- Über-Fenster mit Version, Datenschutz und Lizenzen; MIT-Lizenz.

### English
- New overlay: card with icon, status and gold level bar, compact or normal; the bar moves in sync with the transition.
- Custom confirmation sounds in 5 variants.
- About window with version, privacy and licenses; MIT license.

## [0.4.0] – 2026-11-08

### Deutsch
- Relativer Modus: Prozentwerte beziehen sich auf die Lautstärke jeder App (80 % → bei 20 % = 16 %).
- Vorlagen und Klartext-Vorschau im Profil-Dialog.
- Live-Lautstärke bei laufenden Apps und in der Gruppe; Apps in der Gruppe werden ausgegraut.
- 0.4.2–0.4.5: Version im Titel und Dateinamen, Diagnose-Log, flüssigere Balken, kein Flackern.

### English
- Relative mode: percentages refer to each app's own volume (80 % → at 20 % = 16 %).
- Templates and plain-language preview in the profile dialog.
- Live volume for running apps and in the group; apps already in the group are greyed out.
- 0.4.2–0.4.5: version in title and file name, diagnostic log, smoother bars, no flicker.

## [0.3.0] – 2026-11-08

### Deutsch
- Neues Icon in Gold.

### English
- New gold icon.

## [0.2.0] – 2026-11-08

### Deutsch
- Maustasten als Hotkey (Mittelklick, Maus 4/5, auch mit Shift/Strg/Alt/Win), optional an andere Programme weitergeben.

### English
- Mouse buttons as hotkeys (middle click, mouse 4/5, also with Shift/Ctrl/Alt/Win), optionally passed on to other programs.

## [0.1.0] – 2026-11-08

### Deutsch
- Erste Version: Gruppen, Profile mit Abfolgen, globale Hotkeys, sanfter Übergang, Overlay, Tray, Autostart.

### English
- First version: groups, profiles with step sequences, global hotkeys, smooth transitions, overlay, tray, autostart.
