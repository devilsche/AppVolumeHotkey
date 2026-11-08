# AppVolumeHotkey

Mit einem globalen Hotkey die Lautstärke einer oder mehrerer Apps umschalten. Gedacht fürs Zocken: z. B. Discord mit einem Tastendruck auf 20 % und mit dem nächsten Druck zurück auf den Wert von vorher, wie die Profilauswahl in TeamSpeak 3, aber für jede App.

## Download

Unter **Releases** liegt `AppVolumeHotkey-vX.Y.Z.exe`. Die laufende Version steht im Fenstertitel und unter „Über“. Einfach herunterladen und starten; eine Installation ist nicht nötig. Jeder Push baut die exe außerdem als Artefakt im Reiter **Actions**.

> Windows SmartScreen kann beim ersten Start warnen, weil die exe nicht signiert ist: „Weitere Informationen“ → „Trotzdem ausführen“.

## Bedienung

1. **Gruppe anlegen** (links), z. B. „Voice“.
2. **Apps hinzufügen** (in der Gruppe und rechts siehst du jeweils live die aktuelle Lautstärke im Windows-Mixer): aus „Laufende Apps“ (rechts) in die Gruppe ziehen, per Doppelklick oder über „Zur Gruppe hinzufügen“. Apps, die gerade nicht laufen, gehen über „App per .exe hinzufügen …“. Erkannt werden die Apps am Namen der .exe, also auch nach einem Neustart der App.
3. **Profil anlegen:** Name, Hotkey (ins Feld klicken und die Kombination drücken, z. B. `Shift+F9`, `Strg+Shift+A`, `F23`, oder eine Maustaste: `Maus 4`, `Shift+Maus 5`, `Mittelklick`) und die **Abfolge**:
   - `20 % → Ausgangswert`: Toggle wie in TS3
   - `20 % → 50 %`: zwischen zwei Werten hin- und herschalten
   - `20 % → 50 % → Ausgangswert`: drei Stufen
   - **Ausgangswert** ist die Lautstärke, die die Apps vor dem ersten Druck hatten.
   - **Bezug:** *relativ* (Standard bei neuen Profilen): 20 % heißt 20 % vom Ausgangswert jeder App. Steht Discord im Mixer auf 80 % und TS3 auf 100 %, landen sie bei 16 % und 20 %. *Feste Werte*: alle Apps genau auf 20 %.
   - Die Vorschau im Dialog zeigt in Klartext, was jeder Druck bewirkt.
4. Pro Gruppe ist immer höchstens ein Profil aktiv. Drückst du den Hotkey eines anderen Profils derselben Gruppe, wechselt die Gruppe direkt dorthin.

Das Fenster schließen minimiert die App in den Infobereich. Mit einem Rechtsklick auf das Symbol siehst du die aktiven Profile, kannst alle zurücksetzen, die Hotkeys pausieren oder die App beenden. Beim Beenden werden alle Lautstärken zurückgesetzt. Stürzt die App ab, holt sie die Ausgangswerte beim nächsten Start nach.

## Einstellungen

- Mit Windows starten, beim Autostart minimiert im Infobereich
- Mit Administratorrechten ausführen: nötig, wenn eine Anwendung mit Adminrechten im Vordergrund ist (z. B. manche Spiele oder Tools), sonst kommen die Hotkeys nicht an. Der Autostart läuft dann über die Aufgabenplanung.
- Overlay: Position, Größe, Anzeigedauer; Übergangsdauer (Standard 200 ms)

## Bekannte Grenzen

- In Spielen mit **exklusivem Vollbild** kann kein Overlay erscheinen. Abhilfe: „Rahmenloses Fenster“ im Spiel oder den Bestätigungston im Profil aktivieren.
- Tastatur-Hotkeys gehen nicht zusätzlich ans Spiel weiter. Bei Maustasten ist das pro Profil wählbar („zusätzlich weitergeben“).
- Linke und rechte Maustaste sind als Hotkey bewusst nicht erlaubt.

## Entwicklung

```bash
dotnet test tests/AppVolumeHotkey.Core.Tests
dotnet publish src/AppVolumeHotkey -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Die Kernlogik (`src/AppVolumeHotkey.Core`) ist plattformunabhängig und getestet. Die WPF-App (`src/AppVolumeHotkey`) lässt sich unter Linux bauen (`EnableWindowsTargeting`), aber nur unter Windows ausführen.

## Lizenz

MIT, siehe [LICENSE](LICENSE). Lizenzen der verwendeten Bibliotheken: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
