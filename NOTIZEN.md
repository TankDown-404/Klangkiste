# Klangkiste – Projektnotizen


Kurzer Stand für den Fall, dass der Chat-Verlauf zurückgesetzt wird.
Bei einem Reset: Diese Datei zeigen, dann kann es nahtlos weitergehen.

> **WICHTIG – Pflege dieser Datei:**
> Der Chat-Verlauf darf jederzeit gelöscht werden („Clear chats"), um Tokens zu sparen.
> „Clear chats" setzt den Gesprächskontext komplett zurück – der Code bleibt erhalten,
> nur das Gesprächsgedächtnis ist weg. Damit trotzdem nahtlos weitergearbeitet werden
> kann, gilt: **Diese Datei wird am Ende jeder Arbeitssitzung auf den aktuellen Stand
> gebracht** (erledigte Punkte, neue Entscheidungen, nächste Schritte). Vor dem Löschen
> des Chats ist der Stand hier also immer aktuell.

## DAUERHAFT: Backup des Arbeitsordners (Sicherheitsnetz gegen Datenverlust)
- **Der komplette Arbeitsordner der Klangkiste ist als ZIP gesichert unter:**
  `E:\_DATEN_D\KlangkisteSiCo.zip`
- Zweck: zuverlässige Sicherung (u. a. der schon einmal abgeschnittenen
  `MainWindow.xaml.cs` / der dekompilierten Referenz `MainWindowSiCo.cs`), die uns bei
  einem erneuten Datenverlust „den Hintern rettet". Bei Bedarf dort entpacken/wiederherstellen.
- Diese Angabe gilt **dauerhaft** – nicht löschen/überschreiben, wenn der Chat geleert wird.

## Technik
- WPF, Ziel-Framework `net10.0-windows`, `ImplicitUsings`/`Nullable` aktiv.
- Hauptfenster `MainWindow` mit `DataGrid` (`Dateiliste`), `TreeView` (`Ordnerbaum`).
- Sammlung in `.mcd` – Klassen `Sammlung` / `MedienOrdner` / `AudioDatei`.
  **Seit der GZip-Einführung: `.mcd` ist GZIP-komprimiert (kein Klartext-XML mehr).**
  Der Loader liest beide Formate (siehe „Speicherformat `.mcd`").
- Konfiguration in `config.ini` (`KlangkisteKonfiguration`).
- Audio-Metadaten über TagLib.

## Speicherformat `.mcd` (Encoding, Abspecken, GZip)
- **Encoding:** Schreiben fest UTF-8 (ohne BOM) über `XmlWriterSettings.Encoding =
  new UTF8Encoding(false)`. Lesen über `XmlReader` mit UTF-8. Sonderzeichen im Pfad/Text
  (En-Dash `–`, typografisches Apostroph `’`, Emojis …) bleiben 1:1 erhalten.
- **Sonderzeichen-Sanitizer** `BereinigeXmlText` / `IstXmlIllegal` in `MainWindow`:
  entfernt NUR echte XML-Verbote (Steuerzeichen < 0x20 außer Tab/LF/CR, 0x7F,
  U+FFFE/U+FFFF, unpaarige Surrogate). Paarige Surrogate (Emojis) bleiben komplett.
  KEINE enge Whitelist mehr (frühere Version löschte fälschlich En-Dash/’ u. Ä. und damit
  Dateipfade → Dateien galten als „nicht vorhanden").
- **XML abspecken (A):** `ShouldSerialize<Eigenschaft>()`-Methoden lassen leere/ungesetzte
  Felder beim Schreiben weg. In `AudioDatei`: `Titel/Artist/Album/Jahr/Medientyp` (leer) und
  `Bitrate/DauerSekunden/GroesseBytes` (0) werden weggelassen. In `MedienOrdner`:
  `BenutzerdefinierterFilter` nur, wenn ≠ Default `"*.*"`. Abwärtskompatibel (fehlende Felder
  greifen beim Laden auf den Default zurück).
- **GZip-Komprimierung (B):** Speichern über `GZipStream(File.Create(tmp), CompressionLevel.
  SmallestSize)` in die `.tmp`, danach atomar Replace/Move → `.mcd`. Endung bleibt `.mcd`.
  `using System.IO.Compression;`. Effekt: ~107 MB → ~6,2 MB (Faktor ~17).
- **Laden:** Helfer `LadeSammlungAusDatei(pfad)` in `MainWindow` erkennt am Dateianfang via
  Magic-Bytes `0x1F 0x8B`, ob GZIP (entpacken) oder altes Klartext-XML (direkt) – beide
  werden gelesen. Wird von `LadeLetzteSammlung` UND `Importieren_Click` genutzt.
  Alte unkomprimierte `.mcd` bleiben ohne Neueinlesen lesbar; nächstes Speichern komprimiert.
- **Nebeneffekt (gewünscht):** `.mcd` ist durch GZip nicht mehr versehentlich im Editor
  editierbar → robuster gegen Beschädigung durch falsche Zeichen.

## Sortierung (Datei-Reihenfolge in der `.mcd`) – ERLEDIGT
- Reihenfolge beim Einlesen/Aktualisieren/Speichern ist jetzt **natürlich sortiert**
  (Sonderzeichen < Ziffern < Buchstaben), damit die `.mcd`-Reihenfolge der Anzeige entspricht.
- `NatuerlicherDateiComparer` (`IComparer<string>`) in `SortierHelfer.cs`.
- Sortier-/Einfügepunkte: `KopiereOrdner` (Speichern), `ScanneOrdner` (Einlesen),
  `AktualisiereOrdnerEbene` + Helfer `SortiereAudioDateienListe` (Aktualisieren).
- Fix zuvor: „SAMURAI - Chippin' In" landete immer am Listenende (Scan-Reihenfolge unsortiert).

## Wichtige Dateien (aktiv)
- `MainWindow.xaml` / `MainWindow.xaml.cs` – Hauptlogik: Scannen, Filter, Auswahl,
  Wiedergabe/Player-Start, Spaltenaufbau, Fensterzustand, Markier-Reihenfolge.
- `KlangkisteKonfiguration.cs` – `config.ini` lesen/schreiben; enthält `DesignModus`-Enum,
  `SpaltenEinstellung`, Player-Einstellungen, Fensterzustand, `LetzteSammlung`,
  `OrdnerbaumBreite`, `SeitentrennerFixiert`.
- `EinstellungenWindow.xaml` / `.xaml.cs` – Einstellungen (Player, Auswahlmodus, Gitternetz,
  Design-Modus, Schrift, Spalten).
- `DesignAnwendung.cs` – wendet Palette an (Hell / Dunkel / Automatisch). KEINE
  Benutzerdefiniert-Farben mehr.
- `MonitorHelfer.cs` – Win32-Helfer für Monitor-/DPI-fähige Fensterwiederherstellung.
- `HilfeWindow`, `HilfeInhalte`, `SpaltenAnpassenDialog`, `OrdnerAuswahlDialog`,
  `MedienformatDefinition`, `SortierHelfer.cs`, `DauerFormat.cs`.
- `FortschrittWindow.xaml` / `.xaml.cs` – kleines Modell-Fenster „Daten werden verarbeitet …"
  mit Indeterminate-ProgressBar; wird beim Einlesen/Aktualisieren gezeigt (siehe unten).
- `MainWindowSiCo.cs` – dekompilierte Referenz (Snapshot). Optional, kann gelöscht werden.

## Auswahlmodi (`Auswahlmodus`)
- `Normal` – nur Zeilenauswahl, keine Haken.
- `Kontrollkaestchen` – Linkklick markiert Zeile (kein Haken); Checkbox im Template
  (reagiert sofort) toggelt den Haken.
- `Hybrid` – Zeilenklick setzt den Haken. Abwahl dort nur über Checkbox abhaken oder
  „Auswahl aufheben" (kein Strg-Klick-Abwählen mehr – bewusst).

## Markier-Reihenfolge (Wiedergabe-Reihenfolge)
- Felder `markierReihenfolge` (List) + `markierMenge` (HashSet) in `MainWindow`.
- `MerkeMarkierung` / `VergissMarkierung` / `LeereMarkierReihenfolge`.
- Gemerkt wird beim Anhaken (Checkbox), beim Zeilen-Select (`SelectionChanged` AddedItems),
  beim Rechtsklick der einen Haken setzt.
- Vergessen wird beim Checkbox-Abhaken, bei „Auswahl aufheben", beim Ordnerwechsel.
- WICHTIG: `SelectionChanged` vergisst `RemovedItems` NICHT mehr automatisch. Grund: Ein
  Klick auf eine andere Zeile bzw. ein Rechtsklick (Clear+Add) löst RemovedItems für bereits
  markierte Dateien aus; ein automatisches Vergessen würde deren Haken und Position zerstören.
- `ErmittleAusgewaehlteDateien` baut die Reihenfolge DIREKT: zuerst die angehakten Dateien in
  `markierReihenfolge`-Reihenfolge (`IstAngehakt`-gefiltert, dedupliziert), danach die reine
  Zeilenauswahl aus `Dateiliste.SelectedItems`. Nicht gemerkte Dateien landen hinten.
- Beim Abspielen (`StarteWiedergabe`) wird stabil nach `Medientyp` (Audio/Video) gruppiert;
  innerhalb jeder Gruppe bleibt die Markier-Reihenfolge erhalten (so gewünscht).
- Nicht-Audio/Video-Dateien („Sonstige") werden NICHT an den Player gegeben, sondern einzeln
  über das Windows-Standardprogramm geöffnet (`OeffneMitWindowsStandardprogramm`; Fehler
  werden gesammelt einmal gemeldet).

## Sonstige umgesetzte Details
- Dark Mode Schrift `#AFAFB0`, Gitternetz `#737373` nur im Dunkelmodus.
- Icons als Emoji-Glyphen (🎵/🎬/📄/❌, 💽/📁) in der Dateiname-Spalte.
- Gitternetz: DataGrid-GridLines immer aus (Linien über Zellränder, exakte Stärke),
  kein Zeilenhöhen-Resize, Row-Header ausgeblendet.
- Fensterzustand in `config.ini` (maximiert + Position + Monitor, DPI-fähig).
- Beim Start wird der erste Stammordner der geladenen Sammlung automatisch im Baum
  ausgewählt (`WaehleErstenStammordner`, per Dispatcher/`Loaded`), damit die Dateiliste
  sofort gefüllt ist.

## Toolbar (Zeile 0)
- Die `ToolBar` wurde ERSETZT durch ein `Border` (`x:Name="HauptToolbar"`, untere
  Trennlinie `#DDDDDD`, `Padding="2"`) mit einem `DockPanel LastChildFill="True"`.
  Grund: Eine WPF-`ToolBar` gibt ihrem Inhalt nicht die volle Breite, dadurch ließ sich
  rechtsbündiges Ausrichten nicht zuverlässig umsetzen. Mit `Border`+`DockPanel` klappt
  es sauber und ohne Overflow-Menü.
- Layout: LINKER `StackPanel` (`DockPanel.Dock="Left"`): „Ordner einlesen", `Separator`,
  „Importieren", „Exportieren", `SammlungSpeichernButton` („Sammlung speichern").
  RECHTER `StackPanel` (`DockPanel.Dock="Right"`): „Einstellungen", dann „Hilfe"
  (Hilfe ist der ganz rechte Button – so gewünscht).
- `x:Name="HauptToolbar"` ist jetzt ein `Border` (vorher `ToolBar`); wird im Code-behind
  NICHT referenziert → Typwechsel unkritisch.

## Fortschrittsfenster (Einlesen / Aktualisieren)
- Neues Fenster `FortschrittWindow.xaml` / `.xaml.cs`: nur TextBlock „Daten werden
  verarbeitet …" + `ProgressBar` mit `IsIndeterminate="True"`. Standardschrift (nichts
  gesetzt), Design via `DesignAnwendung.WendeAn(this)` (wie `HilfeWindow`). `WindowStyle=
  "ToolWindow"` (kein X-Knopf), `ShowInTaskbar=false`, `Owner` = MainWindow, `CenterOwner`.
  Methode `SetzeMeldung(...)` (aktuell nicht genutzt).
- Wird in `OrdnerEinlesen_Click` UND `AktualisiereOrdner` angezeigt; beide Methoden sind
  jetzt `async void`.
- EINLESEN: `ScanneOrdner` läuft in `Task.Run` + `await` (neues, noch nicht gebundenes
  Objekt → thread-sicher, ProgressBar animiert).
- AKTUALISIEREN: `AktualisiereOrdnerIntern` läuft im UI-Thread (NICHT Task.Run!), weil es
  in die live gebundenen `ObservableCollection`s schreibt (sonst NotSupportedException).
  Vor dem Abgleich `Dispatcher.Yield(Background)`, damit das Fenster sichtbar wird.
- Mindestanzeigedauer 300 ms (`MindestFortschrittsdauer` + `WarteAufMindestdauerAsync`,
  per `Stopwatch`), damit das Fenster bei sehr schnellen Vorgängen nicht nur aufblitzt.
  Das `Close()` im `finally` wartet die Restzeit ab.
- KEIN Abbrechen-Button (bewusst), keine Ghost-Copy, keine Cancellation. Beim Aktualisieren
  schreibt der Abgleich direkt in die bestehenden Ordner (kein Verwerfen bei Abbruch mehr,
  weil es keinen Abbruch gibt).

## GEPLANT: Zweisprachigkeit Deutsch (Standard) + Englisch – noch NICHT umgesetzt
- Ziel: Umschaltbar in den Einstellungen; Deutsch = Standard, Englisch zusätzlich **fest
  eingebaut** (keine externen Language-Packs).
- Sprachwechsel erfordert **Neustart**. Hinweisdialog zweisprachig, z. B.:
  „Diese Einstellung erfordert einen Neustart des Programms." /
  „This setting requires restarting the application."
- Arbeitsteilung (besprochen):
  1. Grundgerüst Lokalisierung (Mechanik, Sprachwahl in `config.ini`, Neustart-Hinweis).
  2. Alle Nicht-Hilfe-Texte auslagern (Buttons, Menüs, Status, Filter, Spalten,
     Kontextmenüs, `MessageBox`en, Fehlermeldungen) + Englisch.
  3. Hilfe: **Nutzer liefert** Übersetzung als Kopie **`HilfeInhalteE.cs`** (gleiche
     Struktur/Reihenfolge/Themenanzahl wie `HilfeInhalte.cs`); wir hängen sie sprachabhängig ein.
  4. Layout-/Funktionstest in beiden Sprachen.
- Offene Fragen bei Umsetzung: Format-Metadatenhinweise (`MedienformatDefinition.
  MetadatenHinweis`/`Kategorie`) auch englisch? Themen-Titel („1. Erste Schritte"…) übersetzen?
- Aufwand grob: Grundgerüst klein–mittel; Texte auslagern mittel; Hilfe-Übersetzung groß
  (liegt beim Nutzer); Test mittel.
- STATUS: vom Nutzer vorerst **verschoben**, hier nur vorgemerkt.

## Build
- `dotnet build` zuletzt (Nutzer): **0 Fehler, 0 Warnungen**.
- ACHTUNG bei TagLib: `using TagLibFile = TagLib.File;` (Alias) verwenden, NICHT
  `using TagLib;` (sonst CS0104: `File` mehrdeutig vs. `System.IO.File`). `Tag` als
  `TagLib.Tag` qualifizieren.

## Publish (selbstenthaltend, NICHT single-file) – vom Nutzer gewünscht
- Befehl für einen Release-Publish MIT integrierter .NET-Runtime, Dateien bleiben einzeln:
  ```
  dotnet publish E:\VSS\ARBEITEN\Klangkiste\Klangkiste.csproj -c Release -r win-x64 --self-contained true
  ```
  Output: `bin\Release\net10.0-windows\win-x64\publish\`
- Wichtig: `--self-contained true` (Runtime dabei), **OHNE** `PublishSingleFile` (nicht zu
  einer EXE pressen – so gewünscht, „professioneller" Look mit Einzeldateien).
- Optionale Sauber-Variante (ohne .pdb, eigenes Ziel):
  ```
  dotnet publish ... --self-contained true -p:DebugType=None -p:DebugSymbols=false -o E:\VSS\ARBEITEN\Klangkiste\Publish
  ```
- Optional für schnelleren Start: `-p:ReadyToRun=true`.
- Einzeiler, den der Nutzer zuletzt wollte (self-contained, KEIN single-file, KEIN Löschen):
  ```
  dotnet publish Klangkiste.csproj -c Release -r win-x64 --self-contained true -o .\publish
  ```
  `.\publish` ist relativ zum aktuellen Arbeitsverzeichnis (i. d. R. Projektordner).
- **Satelliten-Ressourcen-Ordner** im Publish (`cs, de, es, fr, it, ja, ko, pl, pt-br, ru,
  tr, zh-hans, zh-hant`): sind lokalisierte Framework-/Bibliotheks-Texte. Löschbar; danach
  erscheinen .NET-interne Meldungen auf Englisch. `de` nur behalten, wenn deutsche
  Framework-Meldungen gewünscht. Eigene Klangkiste-Texte sind NICHT betroffen. Werden bei
  jedem Publish neu erzeugt (dauerhaft weglassen nur via `.csproj`-Property
  `SatelliteResourceLanguages`). Der Nutzer wollte sie nur wissen, nicht löschen.

## Wie wir arbeiten
- Der Nutzer baut selbst (`dotnet build`). Terminal/Shell in der Agent-Umgebung funktioniert.
- Erst besprechen, dann umsetzen. Keinen Code schreiben, wenn der Nutzer das ausdrücklich
  (noch) nicht möchte.
- Knapp und zielgerichtet arbeiten. Keine unnötigen Änderungen an unbetroffenen Stellen.
- **Am Ende jeder Sitzung: `NOTIZEN.md` aktualisieren**, damit der Chat gefahrlos gelöscht
  werden kann (Token sparen). Beim Wiederaufnehmen zuerst `NOTIZEN.md` lesen.
