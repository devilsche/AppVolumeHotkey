# AppVolumeHotkey – Design

Stand: 2026-11-08 · Status: zur Freigabe

## 1. Zweck und Hintergrund

Beim Zocken will der Nutzer per globalem Hotkey die Lautstärke von Voice-Apps (vor allem Discord) schnell umschalten – „leise / wieder laut“. TeamSpeak 3 hat so etwas eingebaut (Profilauswahl per Hotkey), Discord nicht, und genau diese Funktion fehlt. Ziel ist ein **generelles** Windows-Tool, das das für beliebige Apps kann.

Bestehende Tools reichen nicht: EarTrumpet ist ein Mixer ohne diese Hotkey-Logik; NirSoft SoundVolumeView kann Toggles nur umständlich über Batch-Dateien.

**Erfolg:** Im Spiel eine Taste drücken → Discord (und alle anderen Apps der Gruppe) gehen sanft auf z. B. 20 %, Overlay bestätigt es; nochmal drücken → zurück auf den Wert von vorher. Ohne Alt-Tab, ohne Batch-Dateien, einmal eingerichtet und vergessen.

## 2. Ziele

- Apps per Drag & Drop zu **Gruppen** zusammenfassen.
- Pro Gruppe mehrere **Profile**; jedes Profil hat einen frei wählbaren globalen Hotkey und eine **Abfolge von Stufen**, die per Hotkey durchgeschaltet wird.
- Sanfter Übergang (~200 ms).
- Optionales Overlay („Voice leise · 20 % · AKTIV“), optionaler Bestätigungston.
- Läuft unauffällig im Infobereich, Autostart mit Windows, minimierter Start.
- Sinnvolle, vollständig per Tastatur bedienbare GUI, folgt Hell-/Dunkelmodus von Windows.

## 3. Nicht-Ziele (vorerst)

- Schrittweises Durchklicken (+X % pro Druck) – evtl. später.
- Dynamische Ziele („alle außer …“, „Vordergrund-App“).
- Relative Werte („20 % des aktuellen Werts“) – alle Stufenwerte sind **absolut**.
- Stream-Deck/MIDI/CLI-Steuerung, automatisches Ducking bei Calls.
- Pro-Ausgabegerät-Konfiguration.

## 4. Anti-Ziele

- Darf das Spiel nicht stören: kein Fokusklau, Overlay nimmt keine Klicks/Eingaben an, kein Eingriff in den Spielprozess (Anti-Cheat-sicher, nur offizielle Windows-Audio-API).
- Kein Gefrickel: keine Batch-Dateien, keine manuelle Konfigurationsdatei nötig.
- Hotkeys dürfen nicht „verschluckt“ werden, ohne dass der Nutzer erfährt warum (z. B. Konflikt).

## 5. Fachliches Modell

### 5.1 Gruppe
- Name.
- Liste von Apps, identifiziert über den **Namen der .exe** (z. B. `Discord.exe`, case-insensitive), nicht über PID. Gilt für alle Prozesse/Audio-Sessions mit diesem Namen, auch für Apps, die erst später starten oder neu gestartet werden.
- Eine App darf in mehreren Gruppen sein *(Builder-Entscheidung, siehe 5.4)*.

### 5.2 Profil
- Gehört zu genau einer Gruppe.
- Name, Hotkey (beliebige Taste inkl. F13–F24, mit optionalen Modifikatoren Shift/Ctrl/Alt/Win).
- **Abfolge**: geordnete Liste von Stufen (mind. 1). Eine Stufe ist entweder
  - ein fester Wert in Prozent (0–100, absolut), oder
  - die besondere Stufe **„Ausgangswert“**.
- Overlay beim Aktivieren an/aus, Overlay beim Deaktivieren an/aus, Bestätigungston an/aus.

Beispiele:
- TS3-Style-Toggle: `20 % → Ausgangswert`
- Drei Stufen: `20 % → 50 % → Ausgangswert`
- Ohne Rückkehr: `20 % → 100 %`

### 5.3 Verhalten (Zustandsmaschine pro Gruppe)

- Pro Gruppe ist höchstens **ein** Profil aktiv.
- **Gruppe inaktiv, Hotkey von Profil P gedrückt:** Das Tool merkt sich für jede App der Gruppe deren aktuelle Lautstärke als **Ausgangswert**. P wird aktiv, erste Stufe wird angewendet.
- **P aktiv, Hotkey von P gedrückt:** nächste Stufe der Abfolge; nach der letzten wieder die erste.
- **Stufe „Ausgangswert“ erreicht:** gemerkte Werte werden wiederhergestellt, P gilt als **inaktiv**, gemerkte Werte werden verworfen. Der nächste Druck merkt sich die Ausgangswerte neu.
- **P aktiv, Hotkey eines anderen Profils Q derselben Gruppe gedrückt:** Q wird aktiv und beginnt bei seiner ersten Stufe. Die gemerkten Ausgangswerte bleiben die vom ursprünglichen Aktivieren. Erreicht Q „Ausgangswert“, werden diese wiederhergestellt.
- Abfolge ohne „Ausgangswert“ (z. B. `20 % → 100 %`): das Profil bleibt dauerhaft aktiv und schaltet zyklisch. *(Builder-Entscheidung: inaktiv wird es erst, wenn ein anderes Profil der Gruppe übernimmt und dieses „Ausgangswert“ erreicht, oder über das Tray-Menü „Zurücksetzen“.)*
- Manuelle Änderungen im Windows-Mixer während ein Profil aktiv ist, werden ignoriert; bei Rückkehr wird der gemerkte Ausgangswert gesetzt.
- **App startet/öffnet neue Audio-Session, während ein Profil aktiv ist:** Gibt es für sie bereits einen gemerkten Ausgangswert (sie lief schon beim Aktivieren), bleibt dieser erhalten – Windows hat ihr inzwischen evtl. selbst den abgesenkten Wert zurückgegeben. Nur wenn noch keiner gemerkt ist, wird ihr aktueller Wert gemerkt. Dann bekommt sie sofort den aktuellen Stufenwert. *(Builder-Entscheidung)*
- **App beendet sich:** ihr gemerkter Wert bleibt bis zur Rückkehr erhalten (falls sie wiederkommt, wird er beim Zurückschalten gesetzt).
- Gilt für Sessions auf **allen aktiven Ausgabegeräten** *(Builder-Entscheidung)*.
- Bei regulärem Beenden des Tools: aktive Profile werden zurückgesetzt (Ausgangswerte wiederhergestellt).
- **Absturz/Kill/harter Shutdown:** Windows merkt sich App-Lautstärken selbst, Discord bliebe also bei 20 %. Deshalb werden gemerkte Ausgangswerte bei jeder Änderung in `%APPDATA%\AppVolumeHotkey\state.json` geschrieben. Beim Start werden vorhandene Werte wiederhergestellt (sobald die jeweilige App eine Session hat) und danach gelöscht. Der Aktiv-Zustand selbst wird nicht fortgesetzt – nach dem Start ist alles inaktiv. *(Builder-Entscheidung)*

### 5.4 App in mehreren Gruppen *(Builder-Entscheidung)*
Zuletzt angewendete Stufe gewinnt. Ausgangswerte werden pro Gruppe gemerkt; beim Zurücksetzen einer Gruppe wird deren gemerkter Wert gesetzt, auch wenn eine andere Gruppe die App gerade steuert. Einfach und vorhersagbar; in der Praxis selten.

### 5.5 Übergang
- Lineares Ein-/Ausblenden über die eingestellte Dauer (Standard 200 ms, einstellbar 0–1000 ms).
- Neuer Hotkey-Druck während eines laufenden Übergangs: startet vom aktuellen Ist-Wert zum neuen Ziel.

## 6. Oberfläche

Alle Texte auf Deutsch. Hell/Dunkel folgt Windows (inkl. Overlay). Vollständig per Tastatur bedienbar, sichtbarer Fokus, Screenreader-taugliche Beschriftungen (Accessibility-Anforderungen des Nutzers: WCAG-2.2-AA-Grundsätze sinngemäß für Desktop).

### 6.1 Hauptfenster (Layout „A“, vom Nutzer gewählt)
Drei Spalten:
- **Links – Gruppen:** Liste, „+ Neue Gruppe“, Umbenennen/Löschen (Kontextmenü + Tastenkürzel F2/Entf).
- **Mitte – gewählte Gruppe:**
  - Apps der Gruppe als Chips mit ✕; Ablagefläche „App hierher ziehen“; „App per .exe hinzufügen …“ (Dateidialog bzw. Namenseingabe) für Apps, die gerade nicht laufen.
  - Profile der Gruppe: je Zeile Hotkey, Name, Abfolge-Kurzform, Status „● AKTIV“ (nicht nur Farbe, auch Text); Bearbeiten-Knopf (✎), Löschen; „+ Profil“.
- **Rechts – Laufende Apps:** alle Apps, die aktuell eine Audio-Session haben (Icon + Name), live aktualisiert. Drag & Drop in die Gruppe; Tastatur-Alternative „Zur Gruppe hinzufügen“.
- Zahnrad → Einstellungen.
- Fenster schließen = in den Infobereich minimieren. Beenden nur über Tray-Menü.

### 6.2 Profil-Dialog (Variante „B“, vom Nutzer gewählt)
Modales Fenster mit:
- Name.
- Hotkey: ein Eingabefeld – hineinklicken (oder per Tab fokussieren) und die gewünschte Kombination drücken; sie wird sofort übernommen und angezeigt. Beliebige Kombination aus Shift/Strg/Alt/Win + Taste (z. B. Shift+Strg+A, F23, Shift+F9). Esc verlässt das Feld ohne Änderung, Entf/Rücktaste löscht den Hotkey.
- Abfolge: Stufen als Chips (Prozentwert editierbar; „Ausgangswert“ optisch hervorgehoben + Text), „+ Stufe“ (Prozent oder Ausgangswert), Umsortieren per Drag & Drop **und** per Tastatur (z. B. Alt+←/→ oder Knöpfe), ✕ zum Entfernen.
- Overlay beim Aktivieren ☐, beim Deaktivieren ☐, Bestätigungston ☐.
- Abbrechen / Speichern. Änderungen gelten erst nach Speichern.
- Validierung: Hotkey bereits von anderem Profil vergeben oder von Windows/anderem Programm belegt (`RegisterHotKey` schlägt fehl) → Fehlermeldung am Feld, Speichern nicht möglich. Leere Abfolge → Fehler.

### 6.3 Einstellungen
- Mit Windows starten ☐
- Minimiert (im Infobereich) starten ☐
- Mit Administratorrechten ausführen ☐ (nötig, damit Hotkeys ankommen, wenn ein als Admin laufendes Spiel im Vordergrund ist)
- Overlay: Position (Ecke oben links/rechts, unten links/rechts, ggf. Monitor), Größe (klein/mittel/groß), Anzeigedauer (Standard 1,5 s)
- Übergangsdauer (Standard 200 ms)

### 6.4 Infobereich (Tray)
- Linksklick: Hauptfenster öffnen.
- Rechtsklick-Menü: aktive Profile anzeigen, „Alle zurücksetzen“, „Hotkeys pausieren“ *(Builder-Entscheidung, nützlich beim Chatten)*, Öffnen, Beenden.

### 6.5 Overlay
- Kleines, halbtransparentes Feld, z. B. „🔉 Voice leise · 20 % · AKTIV“ bzw. „… · Ausgangswert · AUS“.
- Immer im Vordergrund, nimmt keine Klicks/keinen Fokus an, erscheint nicht in Taskleiste/Alt-Tab.
- Blendet nach der Anzeigedauer aus; respektiert „Animationen reduzieren“ von Windows (dann ohne Fade).
- **Bekannte Grenze:** In Spielen mit exklusivem Vollbild ist kein Overlay möglich – dafür gibt es den Bestätigungston. Bei „Rahmenloses Fenster“ funktioniert es.

## 7. Technik (Entscheidung des Builders – Nutzer hat Sprache freigestellt)

- **C# / .NET 10 (LTS), WPF**, Fluent-Theme mit `ThemeMode="System"` für Hell/Dunkel.
- Audio: Windows Core Audio API (`IMMDeviceEnumerator`, `IAudioSessionManager2`, `IAudioSessionNotification`, `ISimpleAudioVolume`) – via **NAudio** oder direkte COM-Interop.
- Hotkeys: `RegisterHotKey` mit `MOD_NOREPEAT` auf einem Hidden-Window/HwndSource.
- Tray: z. B. `H.NotifyIcon.Wpf`.
- Overlay: eigenes WPF-Fenster mit `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`.
- Autostart: ohne Admin über `HKCU\…\Run`; mit Admin über eine Aufgabe in der Aufgabenplanung („mit höchsten Privilegien“), da Run-Key keine UAC-Erhöhung ohne Prompt erlaubt.
- Einzelinstanz (Mutex); zweiter Start bringt das vorhandene Fenster nach vorn.
- Konfiguration: JSON in `%APPDATA%\AppVolumeHotkey\config.json`, automatisch gespeichert; robust gegenüber fehlender/kaputter Datei (Backup + Neustart mit Defaults).
- Auslieferung: eine selbst-enthaltene .exe (`PublishSingleFile`) für Windows 10/11 x64.
- Architektur: Kernlogik (Gruppen, Profile, Zustandsmaschine, Ausgangswerte, Übergang) ohne UI- und Windows-Abhängigkeit hinter einer Audio-Schnittstelle, damit sie per Unit-Tests prüfbar ist.

- **Hotkeys nur Tastatur** über `RegisterHotKey` *(Builder-Entscheidung, Nutzer: „mach einfach“)*. Die Taste wird dabei „geschluckt“ (Spiel/Discord sehen sie nicht) – für F13–F24 und Kombinationen wie Shift+F9 unkritisch. Maustasten (Maus4/5) und durchgereichte Tasten bräuchten Low-Level-Hooks und sind ein mögliches späteres Feature, nicht Teil von v1.

## 8. Rahmenbedingungen

- Entwicklungsrechner ist Linux. .NET 10 SDK wird lokal im Benutzerordner installiert (`dotnet-install.sh`, ohne Root). Unit-Tests der Kernlogik laufen dort; Bauen der WPF-App unter Linux mit `EnableWindowsTargeting`. **Ausführen und manuelles Testen nur auf Windows** durch den Nutzer.
- **Auslieferung:** Git-Repo auf GitHub (privat, Konto `devilsche`, Name `AppVolumeHotkey`). GitHub Actions baut auf `windows-latest`, führt die Tests aus und erzeugt die selbst-enthaltene `AppVolumeHotkey.exe`:
  - bei jedem Push als Build-Artefakt,
  - bei einem Tag `v*` als GitHub Release mit der .exe zum Herunterladen.
  Der Nutzer lädt nur die .exe herunter – kein Installer nötig; Autostart richtet die App selbst ein.

## 9. Dem Builder überlassen

Projektstruktur, MVVM-Framework (oder keins), konkrete Bibliotheken für Tray/Audio, genaues Visual-Design innerhalb Fluent, Icon, Sound für Bestätigungston, Fehlermeldungstexte, Logging.
