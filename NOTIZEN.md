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

## !!! Ordner-Einlesen: Mehrfachauswahl + „Hauptlaufwerk" (ERLEDIGT)
- **Problem des Nutzers:** Nach dem Einlesen eines Ordners sprang der Windows-Ordnerdialog
  beim nächsten Öffnen IN den zuletzt gewählten Ordner → man musste jedes Mal eine Ebene
  hochklicken und wusste nicht mehr, was schon eingelesen war.
- **Entscheidung:** KEIN eigener Ordnerbaum (verworfen – zu großer Aufwand/Neubau).
  Stattdessen **Mehrfachauswahl** (Einzelauswahl wird damit obsolet) + **fester Startordner
  („Hauptlaufwerk")**.
- **WICHTIG – Windows-Dialog-Grenze (vom Nutzer bestätigt ok):** Innerhalb einer Sitzung
  bleibt normales Windows-Verhalten (Rein-Doppelklicken in einen Ordner = „drin"). Der feste
  Startordner sorgt nur dafür, dass der Dialog beim NÄCHSTEN Öffnen wieder auf der
  gewünschten Ebene beginnt.
- **UMSETZUNG (v3 – eigener Ordner-Browser, ersetzt den OpenFileDialog-Hack):**
  - Grund für v3: Der `OpenFileDialog`-Trick (Ordner markieren + „Öffnen") sprang in den
    Ordner hinein statt ihn zu übergeben → unbrauchbar.
  - `OrdnerAuswahlDialog` jetzt ein kleiner **Ordner-Browser** (kein Baum/Gedächtnis):
    - Oben: `LaufwerkComboBox` (alle bereiten Laufwerke mit Volume-Label) + `PfadTextBox`
      (editierbar, Enter = springen) + Button „Als Hauptlaufwerk".
    - Mitte: `UnterordnerListBox` (Doppelklick = rein; erster Eintrag „..  (eine Ebene
      höher)"), Buttons „Hoch" / „Diese Ebene →" / „Markierte →".
    - Unten: `OrdnerListBox` (vorgemerkte Ordner, SelectionMode=Extended) + „Entfernen" +
      Dateityp/Filter/„Unterordner einbeziehen" (unverändert) + Einlesen/Abbrechen.
    - Kerne: `LadeLaufwerke`, `NavigiereZu(pfad)` (setzt Pfad + passendes Laufwerk +
      Unterordner), `LadeUnterordner` (`Directory.GetDirectories` pro Ebene, mit `..` wenn
      Eltern existiert), `UebernehmeOrdner` (gegen Duplikate), `GeheHoch`.
    - Laufwerkswurzel bleibt Wurzel (kein Trimmen zu „D:").
    - Hauptlaufwerk ist ein BELIEBIGER Ordner (nicht nur Laufwerk) – über „Als Hauptlaufwerk"
      setzbar; Start = `Hauptlaufwerk`, sonst erstes Laufwerk.
  - **Dopplungsfilter (v4):** neue CheckBox `DoppelteZulassenCheckBox` („Bereits eingelesene
    Ordner trotzdem zulassen") unter „Unterordner einbeziehen". `OrdnerAuswahlDialog` bekommt
    im Konstruktor optional `IEnumerable<string>? bereitsEingeleseneOrdner` (HashSet,
    case-insensitive, normalisiert). `UebernehmeOrdner` gibt 1 zurück, wenn wegen „bereits
    eingelesen" übersprungen (nur wenn Checkbox NICHT angehakt); die Click-Handler summieren
    und `MeldeUebersprungene` zeigt einmalig eine Info. `MainWindow` liefert die Pfade über
    `ErmittleEingeleseneOrdnerpfade()` (rekursiv alle `MedienOrdner.Pfad`).
    Buttons umbenannt: „Ebene hoch" / „Ebene hinzufügen" / „Markierte hinzufügen" (Breite 150).
  - `MainWindow.OrdnerEinlesen_Click` unverändert (nutzt `dialog.Ordnerpfade` +
    `dialog.Hauptlaufwerk`, merkt Hauptlaufwerk in config).
- **NÄCHSTER SCHRITT:** Nutzer hat v4 (Browser + Dopplungsfilter + Variante mit Checkbox) gebaut
  und **voll als funktionierend bestätigt** („alles funktioniert tadellos"). Feature damit
  abgeschlossen.

## !!! XML-SPEICHERFEHLER: illegales Zeichen – GELÖST (Sanitizing + atomares Speichern)
- **Symptom war:** `InvalidOperationException` → innere
  `ArgumentException: '', hexadecimal value 0x07, is an invalid character` (U+0007, BEL).
  Ein `AudioDatei`-Feld (konkret `Titel`) enthielt ein Steuerzeichen aus einem defekten
  ID3-Tag.
- **Übeltäter (per Log gefunden):** `Jefferson Airplane - 1973 - Thirty Seconds Over
  Winterland - 03 - Crown of Creation.MP3`, Feld `Titel` = `"EXÐ"` + U+0007, Pfad unter
  `F:\_MP3 Vatern\JeffersonAirplane_\…`. Die MP3 selbst ist in Ordnung, nur ihr Tag ist defekt.
- **Zusatzbefund Datenverlust:** `SpeichereInDatei` nutzte `File.Create(zielpfad)` → die
  .mcd wurde SOFORT geleert. Warf `Serialize` danach, blieb eine leere/kaputte .mcd zurück.
  Genau so war die alte `E:\test.mcd` „hinüber" (Fehler `There is an error in XML document
  (366011, 22)` beim Laden – Datei physisch beschädigt, NICHT heilbar; per Neu-Export ersetzt).
- **Isolierungstest des Nutzers:** Ordner einlesen → exportieren → wieder importieren →
  Sammlung speichern: alles fehlerfrei. Bewies: Code ok, nur die alte .mcd war defekt.
- **UMSETZUNG (fertig, Verhalten wie abgestimmt):**
  1. **Atomares Speichern** in `SpeichereInDatei`: erst in `zielpfad + ".tmp"` schreiben;
     danach `File.Replace(tempPfad, zielpfad, null)` (atomar, wenn Ziel existiert) bzw.
     `File.Move` (Ziel existiert noch nicht, Erst-Speichern). `finally` löscht Reste der
     Temp-Datei (best effort). Bei FEHLER bleibt die alte .mcd unangetastet → kein
     Datenverlust mehr.
  2. **Sanitizing beim Schreiben:** neue Helfer `BereinigeXmlText(string?)` +
     `IstXmlIllegal(char)`; illegal = `< 0x20` außer Tab/LF/CR, `U+FFFE`, `U+FFFF`,
     Surrogate. Illegale Zeichen werden **ENTFERNT** (nicht ersetzt). Angewendet in
     `ErzeugePortableKopie` (Version, ExportDatum) und `KopieOrdner` (Name, Pfad,
     BenutzerdefinierterFilter, Dateiname, Medientyp, Titel, Artist, Album, Jahr).
     Die MP3 selbst wird NICHT angefasst.
  3. **Genauere Fehlermeldung:** neue Helfer `BaueSpeicherfehlerMeldung(ex, zielpfad)` →
     kurze Kategorie (Rechte / Sperre-Platte-voll / XML-Zeichen / sonstiger Fehler) +
     `InnerException.Message` (falls vorhanden) + Zielpfad + Hinweis „alte Datei bleibt
     erhalten". MessageBox-Titel bleibt „Speicherfehler".
  4. **Diagnose ENTFERNT:** `SchreibeSpeicherDiagnose`, `DiagnostiziereOrdner`,
     `PruefeXmlText` und der Aufruf im `SpeichereInDatei`-catch sind raus. `IstXmlIllegal`
     bleibt (wird von `BereinigeXmlText` fürs Sanitizing gebraucht). Test durch Nutzer:
     Speichern UND Laden der .mcd erfolgreich (voll bestätigt).
- **Log.txt-Feature: VERWORFEN** (Nutzer-Entscheidung) – wird es nicht geben.

## ERLEDIGT (neu): „Sonstige" Dateien mit Windows-Standardprogramm öffnen
- **Problem:** Playlists (und andere Nicht-Audio/Video-Dateien) ließen sich nicht ausführen –
  es kam nur „ist mit nix verknüpft". Gewünscht: Alles, was NICHT im Player-Filter
  (Audio/Video) ist, wird wie ein Doppelklick im Explorer mit der jeweiligen
  **Windows-Verknüpfung / dem Standardprogramm** geöffnet.
- **Umsetzung (nur in `MainWindow.xaml.cs`):**
  - `StarteWiedergabe(...)` teilt die Auswahl jetzt (nach `File.Exists`-Filter und `Distinct`)
    in zwei Töpfe:
    - `IstAbspielbar` (Audio/Video) → wie bisher an den Player (Gruppierung/M3U unverändert).
    - **alles andere** → neue Methode `OeffneMitWindowsStandardprogramm(...)`.
  - Neue Helfer-Methode `OeffneMitWindowsStandardprogramm(IReadOnlyList<AudioDatei>)`:
    pro Datei `Process.Start(new ProcessStartInfo { FileName = datei.Pfad, UseShellExecute = true })`.
    Fehlende/nicht startbare Dateien (`Win32Exception`/`InvalidOperationException`/`IOException`)
    werden übersprungen und am Ende **einmal gesammelt** in einer MessageBox („Öffnen", Warning)
    gemeldet. Rückgabe = Anzahl erfolgreich geöffneter Dateien.
  - Die „Es wurden keine abspielbaren Dateien ausgewählt"-Info kommt nur noch, wenn WEDER ein
    Player NOCH das Standardprogramm etwas geöffnet hat.
- **Entscheidung Mehrfachauswahl:** Bleibt unangetastet. N markierte sonstige Dateien = N
  `Process.Start`-Aufrufe. Kein Sicherheitsnetz/keine Rückfrage (bewusst, „Nutzer hat's so
  gewollt" – gleiches Verhalten wie heute schon bei mehreren Audio/Video).
- **Vom Nutzer bestätigt:** funktioniert („Perfektion in seiner reinsten Form"); Build 0 Fehler.
  Keine Änderung an `IstAbspielbar`/`IconGlyph`, an Auswahlmodi oder Markier-Reihenfolge.

## !!! DATENVERLUST-VORFALL + REKONSTRUKTION – ABGESCHLOSSEN (Dekompilat-Abgleich)
- **Was passiert ist:** `MainWindow.xaml.cs` wurde beim Bearbeiten mit einem Edit-Tool
  abgeschnitten (String `Filter = "Klang` mitten im `OpenFileDialog` in `Importieren_Click`).
  Die Datei brach am Ende ab. Rekonstruktion war nötig.
- **Vorgehen (final):** Der Nutzer hat eine **dekompilierte Referenz** `MainWindowSiCo.cs`
  geliefert. Damit wurde der rekonstruierte Bereich **echt abgeglichen und korrigiert**,
  statt zu raten. Ergebnis jetzt vollständig aus dem Dekompilat übernommen.
- **Korrigierte Fehlannahmen der ersten (geratenen) Rekonstruktion → jetzt = Original:**
  - Eine `SuchTextBox` → es gibt **vier** Filterfelder: `FilterDateinameTextBox`,
    `FilterTitelTextBox`, `FilterArtistTextBox`, `FilterAlbumTextBox`.
  - `AlleDateienDerSammlung(ordner)` mit Parameter → **parameterlos**, arbeitet über
    `sammlung.Stammordner`.
  - `PasstZurSuche` (Contains) → `ErzeugeFilterPraedikat` + `ErzeugeFilterMuster`
    (Wildcard `*`/`?` + Regex).
  - `WendeFilterAn` mit simpler Contains-Logik → Gesamtsuche via `ICollectionView.Filter`
    (aktueller Ordner) bzw. Sammlungsliste (Ganze-Sammlung-Suche).
  - `FindParent` nur VisualTree → VisualTree **mit LogicalTree-Fallback**.
- **Neu hinzugekommen (waren komplett weg):** Sortierung (`Dateiliste_Sorting`,
  `AudioDateiSortComparer`), Import/Export inkl. portabler Pfade (`ErzeugePortableKopie`,
  `KopieOrdner`, `MacheRelativ`/`MacheAbsolut`), Filter-UI-Handler
  (`GanzeSammlung_Changed`, `Filter_TextChanged`, `FilterLoeschen_Click`),
  `ErzeugeFilterMuster`, `Passt(Regex, string?)`.
- **Bewusst behalten (Nutzer-Verbesserung, NICHT das Original):** der breite
  **Whitelist-XML-Filter** (`BereinigeXmlText` / `IstXmlIllegal`) – robuster als der
  (verbuggte) Original-Filter, entfernt Emojis/Surrogat-Paare sauber.
- **Portabilität bestätigt:** `HilfeInhalte.cs` (Thema 10) sagt „Beim Export werden Pfade
  relativ zum Speicherort der .mcd gespeichert" → `MacheRelativ`/`MacheAbsolut` ist gewollt
  (Export relativ, Import wieder absolut).
- **Namenskonflikte & Fixes (wichtig, sonst tritt es beim Neubau wieder auf):**
  - `Tag`/`TagLib` nicht gefunden → `using TagLibFile = TagLib.File;` (Alias!) und `TagLib.Tag`
    voll qualifiziert in `LeseMetadaten`.
  - Grund für den Alias: `using TagLib;` verursacht CS0104 („File" mehrdeutig zwischen
    `TagLib.File` und `System.IO.File`). **NICHT** wieder `using TagLib;` einführen.
- **Snapshot-Datei `MainWindowSiCo.cs`:** kann gelöscht werden, wird nicht mehr gebraucht
  (nur Referenz). Build läuft auch mit ihr (eigene Klasse), aber „sauber" = löschen.
- **Build:** Vom Nutzer gebaut: **0 Fehler, 0 Warnungen** nach dem Abgleich (bestätigt).
- **Hinweis Speicher-Diagnose:** `SchreibeSpeicherDiagnose` (schreibt
  `E:\Klangkiste_start.log`) wurde entfernt, nachdem der Speicherfehler behoben war.

## Technik
- WPF, Ziel-Framework `net10.0-windows`.
- Hauptfenster `MainWindow` mit `DataGrid` (`Dateiliste`), `TreeView` (`Ordnerbaum`).
- Sammlung in `.mcd` (XML, Klasse `Sammlung` / `MedienOrdner` / `AudioDatei`).
- Konfiguration in `config.ini` (`KlangkisteKonfiguration`).
- Audio-Metadaten über TagLib.

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
  `UmbenennKonfliktDialog`, `MedienformatDefinition`.
- `FortschrittWindow.xaml` / `.xaml.cs` – kleines Modell-Fenster „Daten werden verarbeitet …"
  mit Indeterminate-ProgressBar; wird beim Einlesen/Aktualisieren gezeigt (siehe unten).
- GELÖSCHT (bewusst): `DesignKonfiguration.cs`, `FarbpaletteWindow.cs`,
  `DesignPaketverwaltung.cs` (toter Code).

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
  (Es gibt KEINE separate `SortiereNachMarkierReihenfolge`-Methode mehr im Code.)
- Beim Abspielen (`StarteWiedergabe`) wird stabil nach `Medientyp` (Audio/Video) gruppiert;
  innerhalb jeder Gruppe bleibt die Markier-Reihenfolge erhalten (so gewünscht).
- Nicht-Audio/Video-Dateien ("Sonstige") werden dabei NICHT an den Player gegeben, sondern
  einzeln über das Windows-Standardprogramm geöffnet (siehe „Sonstige Dateien ... ERLEDIGT").

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

## Als Nächstes geplant (noch NICHT umgesetzt)
- (keine offenen Punkte)

## VERWORFEN: Datensatz-Erzeugung (wieder komplett zurückgebaut)
- Zwischenzeitlich war geplant/gebaut: „Löschen“ (beide Varianten) sollte statt zu löschen
  Datensätze aus den Dateien erzeugen (Kopien im Sammlungsordner mit Muster-Namen, via
  `UebernehmeNeueDateienAusMedienAsync` in `MainWindow`, `MedienDatensatzErzeuger.cs` etc.).
- **Diese Funktion wurde auf Wunsch des Nutzers wieder VOLLSTÄNDIG entfernt.** Der Code
  entspricht jetzt wieder dem Git-Original (`git diff` leer).
- Wiederhergestellter Ist-Zustand der beiden Menüpunkte:
  - `AusSammlungLoeschen_Click`: Bestätigung → `EntferneAusSammlung(datei)` je Datei →
    `BerechneAlleOrdnerdauern`, `MarkiereGeaendert`, `SelectedItems.Clear`, `WendeFilterAn`.
    Die Datei bleibt physisch erhalten (nur aus Sammlung entfernt).
  - `DateiVomMediumLoeschen_Click`: Dialog (Papierkorb/endgültig/Abbrechen) → je Datei
    `FileSystem.DeleteFile(... SendToRecycleBin)` bzw. `File.Delete(...)` → danach
    `EntferneAusSammlung(datei)` → Aufräumen. Fehlerfälle: `UnauthorizedAccessException`
    → `ZeigeFehlendeDateirechte(...)`, `IOException` → Meldung.
    - Beide Handler sind wieder synchron (kein `async void`, kein `await`).
- **Reste der verworfenen Aktion sind vollständig entfernt:**
  - `MedienDatensatzErzeuger.cs` wurde gelöscht.
  - `TrackNummer`/`CdNummer` wurden aus `AudioDatei.cs` entfernt; das Auslesen in
    `LeseMetadaten` (`MainWindow.xaml.cs`) wurde ebenfalls entfernt.
  - Keine Verweise mehr auf `MedienDatensatzErzeuger`, `DatensatzNamensMuster`,
    `DatensatzErzeugungsOptionen`, `TrackNummer`, `CdNummer` im Produktivcode.

## ERLEDIGT (neu): Kontextmenü „Anpassen" am Spaltenkopf wieder da
- **Symptom:** Rechtsklick auf die Spaltenkopfzeile (`DataGridColumnHeader`) zeigte kein
  Kontextmenü („Anpassen") mehr.
- **Ursache:** `DesignAnwendung.WendeDataGridAn` hat `datagrid.ColumnHeaderStyle = headerStil`
  gesetzt und damit den in `MainWindow.xaml` definierten `SpaltenHeaderStyle` (der den
  `ContextMenu`-Setter = `SpaltenContextMenu` enthält) komplett ERSETZT. Nichts gelöscht –
  nur überschrieben.
- **Fix:** Neuer Header-Style erbt per `BasedOn` vom XAML-Style:
  `if (datagrid.TryFindResource("SpaltenHeaderStyle") is Style basis) headerStil.BasedOn = basis;`
  Dann die Farb-/Padding-/Hover-Setter wie bisher. **KEIN Template-Setter** → Sortieren per
  Linksklick bleibt unverändert. `SpaltenContextMenu` + `SpaltenAnpassen_Click` waren die
  ganze Zeit intakt.
- **Vom Nutzer bestätigt:** Rechtsklick-Menü UND Sortieren funktionieren; Compiler 0 Fehler.
- **Hinweis:** `ErzeugeStil` ist jetzt evtl. ungenutzt, wird aber bewusst drin gelassen
  (Nutzer-Entscheidung; harmlos, keine Warnung).

### ERLEDIGT: Spalten-Änderungen wirken sofort (nicht mehr erst nach Neustart)
- **Ursache:** `SpaltenAnpassenDialog` verändert die `SpaltenEinstellung`-Objekte aus
  `konfiguration.Spalten` direkt (ObservableCollection), speicherte aber nicht und niemand
  baute die Dateiliste-Spalten neu auf → erst nach Neustart sichtbar.
- **Lösung (umgesetzt):**
  - `EinstellungenWindow.SpaltenAnpassen_Click`: nach dem Dialog `SpeichereAllgemeineKonfiguration()`.
  - Neues Event `EinstellungenWindow.EinstellungenGeschlossen` wird im Konstruktor an das
    `Closing` des Fensters gehängt (greift bei Button, X und Esc!). Der frühere Button-Handler
    `Schliessen_Click` wurde wieder entfernt (Schließen-Button ist wieder nur `IsCancel="True"`).
  - `MainWindow.Einstellungen_Click`: abonniert `EinstellungenGeschlossen` → `ErzeugeDateispalten()`
    + `AktualisiereStatus()`. Dadurch werden Spalten (Reihenfolge/Sichtbarkeit) beim Schließen
    der Einstellungen einmalig übernommen.
- **Vom Nutzer bestätigt:** funktioniert wie vorgestellt; Compiler 0 Fehler.

## ERLEDIGT (neu): Seitentrenner fixierbar (Einstellungen > Dateiliste)
- **Name:** Der verschiebbare Balken zwischen Ordner- und Dateiliste heißt **Seitentrenner**
  (Control: WPF `GridSplitter`, jetzt `x:Name="Seitentrenner"` in `MainWindow.xaml`).
  Doppelklick-Verhalten bewusst NICHT angefasst (WPF-Standard: tut nichts).
- **Config:** `KlangkisteKonfiguration.SeitentrennerFixiert` (bool, Default **false** = nicht
  fixiert). Gespeichert/gelesen in Sektion `[Darstellung]` als
  `SeitentrennerFixiert=true/false` (lowercase).
- **Einstellungen > Dateiliste:** neue CheckBox `SeitentrennerFixierenCheckBox`,
  Beschriftung **„Seitentrenner fixieren"**, direkt unter „Gitternetz anzeigen".
  `SeitentrennerFixieren_Click` schreibt die Config, speichert und ruft `EinstellungenGeaendert`
  (sofortige Wirkung).
- **MainWindow:** `WendeSeitentrennerZustandAn()` setzt `Seitentrenner.IsEnabled = !SeitentrennerFixiert`
  (fixiert = deaktiviert/ausgegraut). Aufgerufen im Konstruktor (Start-Zustand sofort) und in
  `EinstellungenAnwenden()` (live bei Änderung). `EinstellungenAnwenden` ruft jetzt auch
  `WendeSeitentrennerZustandAn()` vor `ErzeugeDateispalten()`.
- Zwischenzeitlich existierte ein Button-Handler `Schliessen_Click` am Schließen-Button;
  wieder entfernt zugunsten des `Closing`-Events (siehe oben).
- **Vom Nutzer bestätigt:** funktioniert wie vorgestellt; Compiler 0 Fehler.

## ERLEDIGT (neu): Gesamtdauer in der Statusleiste (Ordner + Sammlung)
- Variante B: KEIN Speichern in der .mcd; Summen werden beim Laden/Einlesen/Import und nach
  Strukturänderungen einmalig berechnet und im Speicher gehalten (`[XmlIgnore]`).
- Neue Datei `DauerFormat.cs`: `DauerFormat.Formatiere(long sekunden)` → „Tage, Std., Min.,
  Sek." mit NUR den relevanten führenden Einheiten; 0 ⇒ „-". Beispiel 25 h ⇒ „1 Tag, 1 Std.,
  0 Min., 0 Sek.".
- `MedienOrdner`: neues Feld `GesamtdauerSekunden` (`[XmlIgnore]`) = Summe der `DauerSekunden`
  NUR der eigenen `AudioDateien` (ohne Unterordner – so gewünscht für „ausgewählter Ordner").
- `MainWindow.xaml`: Statusleiste hat rechts zwei TextBlocks `OrdnerDauerText` („Ordner: …")
  und `SammlungDauerText` („Sammlung: …").
- `MainWindow.xaml.cs`:
  - `AktualisiereDaueranzeige()`: Ordner = `aktuellAngezeigterOrdner.GesamtdauerSekunden`
    (flach), Sammlung = rekursive Summe über alle Stammordner (`SummiereDauerRekursiv`).
  - `BerechneAlleOrdnerdauern()` / `BerechneOrdnerdauerRekursiv` / `AktualisiereOrdnerDauer`:
    füllen die Caches (rekursiv, ein Durchlauf).
  - Aufrufe: `LadeLetzteSammlung`, `OrdnerEinlesen_Click`, `Importieren_Click`,
    `AktualisiereOrdner`, alle Lösch-Aktionen (Datei aus Sammlung/Medium, Ordner aus Sammlung/
    Medium), `NeueSammlung_Click`.
  - `AktualisiereStatus()` ruft am Ende `AktualisiereDaueranzeige()`,
    `Ordnerbaum_SelectedItemChanged` ruft es direkt (auch im Gesamtsuche-Pfad).

## ERLEDIGT (neu): Hilfetexte an aktuellen Stand angepasst + Trennstrich
- `MainWindow.xaml`: `Separator` zwischen „Sammlung speichern" und „Ordner sortieren" ergänzt.
- `HilfeInhalte.cs` überarbeitet (Nummerierung 1-16 stabil gehalten, wegen Verweis „Hilfe → 16"):
  - Thema 7: „Umbenennen und Konflikte" → „Sortierung der Ordnerliste" (Ordner sortieren
    rekursiv, Sonderzeichen<Zahlen<Buchstaben, Dateilisten-Sortierung per Spaltenklick).
  - Thema 10: „Neue Sammlung" ergänzt (.mcd-Datei bleibt erhalten, nur Verweis entfällt).
  - Thema 11/12/14: Designs realistisch beschrieben (hell/dunkel/automatisch, KEIN .kkd,
    KEINE Benutzerdefiniert-Farben; Schriftart/-größe im Einstellungsfenster). Thema 14 heißt
    jetzt „Toolbar und Statusleiste" mit aktueller Buttonliste + Aktivierungsregeln.
  - Thema 16: nur noch Löschen vom Medium (Umbenennen entfernt).
  - Thema 2/15: an aktuelles Verhalten angepasst (Einlesen=dirty, Ganze-Sammlung-Suche).

## ERLEDIGT (neu): Button „Ordner sortieren"
- Neuer Button `x:Name="OrdnerSortierenButton"` rechts neben „Sammlung speichern"
  (Platzhalter-Text „Ordner sortieren").
- `IsEnabled`: aktiv, sobald `sammlung.Stammordner.Count > 0` – UNABHÄNGIG von .mcd.
  Zentrale Methode `AktualisiereOrdnerSortierenButton()`, aufgerufen aus
  `AktualisiereNeueSammlungButton()`, `AktualisiereStatus()`, `OrdnerEinlesen_Click`,
  `Importieren_Click`, `NeueSammlung_Click`, `OrdnerSortieren_Click` (und via Ketten beim
  Laden/Speichern/Fehler).
- `OrdnerSortieren_Click`: Bestätigung „Sammlung sortieren?" (Ja/Nein). Bei Ja:
  `SortiereOrdnerListe(sammlung.Stammordner)` (REKURSIV), dann IMMER `MarkiereGeaendert()`
  (auch ohne faktische Änderung), Button-Refresh, Statusmeldung.
- `SortiereOrdnerListe`: sortiert je Ebene `ObservableCollection<MedienOrdner>` in-place
  (per `.Move`) nach Name; zuerst rekursiv die Unterordner, dann die aktuelle Ebene.
  Vergleich über `MedienOrdnerNameComparer` → `NatuerlicheTextReihenfolge.Vergleiche`
  (Sonderzeichen < Ziffern < Buchstaben). `OrderBy` ist stabil.
- Neues `using System.Collections.ObjectModel;` in `MainWindow.xaml.cs`.

## ERLEDIGT (neu): Button „Neue Sammlung"
- Neuer Button `x:Name="NeueSammlungButton"` in der Toolbar, VOR „Importieren":
  `[Ordner einlesen] | [Neue Sammlung] [Importieren] [Exportieren] [Sammlung speichern]`.
- `IsEnabled` nur abhängig von `offenerSammlungPfad` (`AktualisiereNeueSammlungButton()`),
  aufgerufen in: `AktualisiereSpeichernButton()` (deckt MarkiereGeaendert/Unveraendert ab),
  am Ende von `LadeLetzteSammlung`-Catch, im Konstruktor, nach dem Leeren.
- `NeueSammlung_Click`:
  1. Abfrage 1: „Soll wirklich eine neue Sammlung erstellt werden?" (Ja/Nein, Default Nein).
  2. Nur wenn `IstGeaendert`: `BestaetigeUngespeicherteAenderungen()` (Speichern? Ja/Nein/
     Abbrechen). Abbrechen ⇒ KEINE neue Sammlung.
  3. Leeren: `Stammordner.Clear()`, `offenerSammlungPfad = null`,
     `konfiguration.LetzteSammlung = ""` + `SpeichereKonfiguration()` (NUR die Referenz –
     die .mcd-DATEI bleibt unangetastet!), Dateiliste/ItemsSource null, Haken + Markier-
     Reihenfolge leeren, `MarkiereUnveraendert()`, Status „Bereit. 0 Ordner, 0 Dateien geladen.".

## ERLEDIGT (neu): Umbenennen RAUS + Sortierung + Dirty-Flag
### 1. Umbenennen komplett entfernt (Datei UND Ordner) – radikal
- Kontextmenü `OrdnerContextMenu`: Item „Umbenennen" entfernt.
- Kontextmenü `DateiContextMenu`: Item „Umbenennen" entfernt; `Opened="DateiContextMenu_Opened"`
  entfernt (Handler existierte nur für das Umbenennen-Item).
- Code-behind entfernt: `DateiUmbenennen_Click`, `OrdnerUmbenennen_Click`,
  `DateiContextMenu_Opened`. Ebenso die nur dafür genutzten Helfer:
  `ErmittleFreienDateinamen`, `ErmittleFreienOrdnernamen`, `DateiNameBelegt`,
  `OrdnerNameBelegt`, `BenenneDateiUm`, `BenenneOrdnerUm`.
- `UmbenennKonfliktDialog.xaml` / `.xaml.cs` werden NICHT mehr referenziert. Der Nutzer
  MUSS diese zwei Dateien noch MANUELL löschen (Agent-Umgebung hat kein Terminal/Datei-
  löschen). Build läuft auch mit den verwaisten Dateien (eigenständiges Fenster), aber
  „radikal raus" heißt: löschen.
- ACHTUNG: Der `Dateiliste_MouseDoubleClick` musste beim Umbau neu eingefügt werden
  (war versehentlich mit rausgeflogen) – jetzt wieder vorhanden und korrekt.

### 2. Sortierung: Sonderzeichen < Ziffern < Buchstaben (per Klick auf Spaltenkopf)
- Neue Datei `SortierHelfer.cs`:
  - `NatuerlicheTextReihenfolge.Vergleiche(string?, string?)`: Zeichengruppen-Rang
    0 = Sonderzeichen (_, (, ), [, ], {, }, -, ., Leerraum), 1 = Ziffern, 2 = Buchstaben.
    Innerhalb Gruppe alphabetisch, case-insensitive; kürzerer String zuerst; Tiebraker
    culture-invariant.
  - `AudioDateiSortComparer : IComparer` (Adapter für `ListCollectionView.CustomSort`
    mit Richtung).
- `MainWindow.xaml`: `CanUserSortColumns="True"` + `Sorting="Dateiliste_Sorting"`.
- `ErzeugeDateispalten`: `SortMemberPath` gesetzt (Dateiname-Template = "Dateiname",
alle Text-Spalten = `spalte.Id`).
- Code-behind neu: `Dateiliste_Sorting` (eigene Sortierung, `e.Handled = true`,
  Richtung toggeln, Sortierpfeil setzen), `SortiereAktuelleAnsicht`,
  `ErzeugeVergleich` (Map Spalten-Id → Vergleich; Textspalten nutzen
  `NatuerlicheTextReihenfolge`, Bitrate/Groesse/Dauer/Aenderungsdatum typgerecht).
- Sortierung bleibt beim Filtern/Refresh erhalten (CustomSort sitzt auf der View).
  Ordnerwechsel erzeugt eine neue View → Sortierung startet neutral (akzeptiert).

### 3. Dirty-Flag beim Einlesen und Aktualisieren – jetzt IMMER gesetzt
- `OrdnerEinlesen_Click`: `MarkiereGeaendert()` jetzt UNBEDINGT (vorher nur wenn keine
  .mcd offen). Neue Ordner ⇒ Sammlung gilt immer als geändert.
- `AktualisiereOrdner`: `MarkiereGeaendert()` jetzt UNBEDINGT (vorher nur bei
  `geaendert == true`). Die Variable `geaendert` im äußeren Scope wurde entfernt
  (Rückgabewert von `AktualisiereOrdnerIntern` wird nicht mehr benötigt; intern weiter
  genutzt).

## Build-Stand nach diesen Änderungen
- Zuletzt vom Nutzer gebaut: **0 Fehler** (nach Rückbau der Datensatz-Erzeugung und
  Wiederherstellung der beiden Löschfunktionen). `git diff` ist leer → Datei wieder im
  Originalzustand.

## ERLEDIGT (neu): Splitter-Position (Ordnerbaum ↔ Dateiliste) wird gespeichert
- Ziel: Die per GridSplitter eingestellte Breite der linken Ordnerbaum-Spalte bleibt
  über Neustarts erhalten (in `config.ini`).
- `KlangkisteKonfiguration`: neue Eigenschaft `OrdnerbaumBreite` (double, 0 = nie gesetzt).
  Wird in Sektion `[Fenster]` als `OrdnerbaumBreite=` (InvariantCulture) geschrieben und
  beim Lesen (Fallback `Monitor`) geparst.
- `MainWindow.xaml`:
  - Erste `ColumnDefinition` heißt jetzt `x:Name="OrdnerbaumSpalte"` (Width="250" MinWidth="150").
  - `GridSplitter`: neuer Handler `DragCompleted="Splitter_DragCompleted"`.
- `MainWindow.xaml.cs`:
  - `WendeGespeicherteBreitenAn()` (im Konstruktor nach `ErzeugeDateispalten()` aufgerufen):
    setzt `OrdnerbaumSpalte.Width` aus `konfiguration.OrdnerbaumBreite`, wenn > 0.
  - `Splitter_DragCompleted(...)`: liest die neue Breite (bevorzugt `Width.Value` bei
    Pixel-Spalte, sonst `ActualWidth`) und ruft `SpeichereKonfiguration()`.
- Es gibt auch `SpaltenbreiteGeaendert`/`SpeichereKonfiguration` (gleiches Muster) –
  konsistent.

## ERLEDIGT: Rechte-Problem bei Umbenennen / Vom-Datenträger-Löschen (A+B+C umgesetzt)
> HINWEIS: Das Umbenennen ist inzwischen KOMPLETT entfernt (siehe „ERLEDIGT (neu)").
> Die Rechte-Meldung betrifft jetzt nur noch „Vom Datenträger löschen".
> Der Text „Umbenennen" in `ZeigeFehlendeDateirechte`-Aufrufen etc. ist damit obsolet,
> schadet aber nicht. `Hilfe → 16.` bleibt gültig.
### Umsetzung (WICHTIG – so ist es jetzt im Code)
- (A) Neuer Helfer `ZeigeFehlendeDateirechte(titel, kontext, ordner)` in `MainWindow.xaml.cs`
  (platziert direkt vor `EntferneAusSammlung`). Zeigt eine `MessageBox` (Warning) mit:
  Ursache = fehlende Windows-Dateirechte (nicht die App), nicht umgehbar, kurzer
  Windows-Lösungsweg (Explorer→Eigenschaften→Sicherheit→Erweitert→Besitzer) + Hinweis
  auf „Hilfe → 16.".
- `DateiVomMediumLoeschen_Click`: `UnauthorizedAccessException` separat →
  `ZeigeFehlendeDateirechte(...)`; `IOException` wie bisher.
  (Das frühere Umbenennen ist komplett entfernt, daher gibt es keinen
  `DateiUmbenennen_Click` mehr, der diese Meldung nutzt.)
- (B) Neues Hilfe-Thema „16. Dateirechte / Zugriff verweigert" in `HilfeInhalte.cs`
  (ausführlicher Text + Schritt-für-Schritt). TextBlock im HilfeWindow hat
  `TextWrapping="Wrap"` und zeigt `\n` korrekt als Zeilenumbruch.
- (C) Diagnose-Code entfernt: `LogDirty(...)`-Methode komplett gelöscht, alle Aufrufe
  entfernt (auch in `MarkiereGeaendert`/`MarkiereUnveraendert`), `UMBENENNEN-PROBE`-Block
  in `DateiUmbenennen_Click` gelöscht. Keine `Debug.WriteLine`/`LogDirty`-Reste mehr
  (per grep verifiziert).
- Dir gewünscht-offen gelassen: „Als Administrator neu starten" (UAC) ist NICHT gebaut.
- Hinweis: `LogStart` / `LogWinampRegistryDiagnose` (schreiben nach E:\Klangkiste_start.log)
  sind ALTE Player-Diagnosen, NICHT Teil dieses Aufräumens – bewusst stehen gelassen.

## Historie: ursprüngliches Rechte-Problem (zur Erinnerung)
### Befund (durch Log + PowerShell-Diagnose belegt)
- Beim Umbenennen erscheint „Access denied" (`UnauthorizedAccessException`), die
  Dirty-Flag (`MarkiereGeaendert`) wird dann NICHT gesetzt – das ist logisch korrekt
  (es wurde nichts geändert), KEIN Bug im Dirty-Code.
- Ursache ist NICHT ein Datei-Lock (resmon zeigt keinen Prozess, kein Player offen) und
  NICHT read-only (`Attributes: Archive`). Ursache sind die **Windows-Dateirechte (ACL)**:
  Auf den betroffenen MP3s (z. B. `F:\_0_\01 Titel 1.mp3`, `F:\MP3\...)` steht für die
  Gruppe **„Benutzer" nur `ReadAndExecute`** – kein Schreiben/Umbenennen/Löschen.
  „Administratoren: FullControl" greift nur, wenn der **Prozess elevated** läuft (UAC):
  Admin-Anmeldung ≠ elevated Prozess. Klangkiste (normal gestartet) läuft als „Benutzer".
- Das ist Alt-Bestand-typisch bei Sammlern (Dateien von anderen Systemen/Platten/Backups
  kopiert, ACLs mitgeschleppt) – also NICHT auf den Testrechner beschränkt, sondern generell.
- `config.ini` schreiben klappt, weil sie von Klangkiste selbst angelegt wird und damit die
  normalen Benutzerrechte erbt.
- „Vom Datenträger löschen" hat dasselbe Problem (fehlendes `Delete`-Recht bei ReadAndExecute).

### Technische Wahrheit (wichtig, nicht wieder vergessen)
- Ein Programm kann sich NICHT selbst Schreibrechte „geben", die das OS verweigert. Auch ein
  elevated Prozess ändert fremde Dateien nur, wenn die ACL es erlaubt (Admin-FullControl).
- Selbst-Neu-Start „als Administrator" (UAC) hilft NUR, wenn das Konto es darf und die ACL
  den Admins FullControl gibt – das ist „hilft oft, aber nicht garantiert".
- Ein Standardbenutzer ohne Admin kann die Rechte GAR NICHT ändern. Dann hilft nur ein
  einmaliger Rechte-Fix durch jemanden mit Rechten (Admin/IT/Eigentümer), außerhalb der App.

### Entscheidung / geplanter Umfang (mit Nutzer abgestimmt)
- Umbenennen + Vom-Datenträger-Löschen NICHT entfernen (Mehrheit der User/Daten funktioniert).
- KEINE dauerhafte Admin-Checkbox in den Einstellungen (verspricht Standard-Usern etwas,
  das sie nicht aktivieren können; Sicherheits-Downgrade).
- Stattdessen bauen:
  (A) Aussagekräftige Fehlermeldung bei `UnauthorizedAccessException` für Umbenennen UND
      Vom-Datenträger-Löschen: klar benennen, dass Windows-Dateirechte fehlen (nicht
      Klangkiste) und dass es durch die App NICHT umgehbar ist.
  (B) Hilfe-Hinweis, wie man in Windows die Dateirechte/Besitz übernimmt (Explorer →
      Eigenschaften → Sicherheit → Erweitert → Besitzer/Permissions). Aus der Meldung
      erreichbar/verlinkt oder als Text.
  (C) Diagnose-Code wieder entfernen (`LogDirty`, `UMBENENNEN-PROBE`, `GC`-Probe,
      `Debug.WriteLine`) – war nur zur Fehlersuche.
- Optional/offen (Nutzer entscheidet später): „Als Administrator neu starten" (UAC) als
  *Angebot* – nur sinnvoll, wenn das Konto es darf. NICHT automatisch.

### Diagnose-Artefakte, die beim Aufräumen entfernt werden müssen
- `LogDirty(...)` (schreibt nach `E:\Klangkiste_start.log`, Präfix `[DIRTY]`).
- `UMBENENNEN-PROBE`-Block (exklusives Öffnen + GC-Gegenprobe) in `DateiUmbenennen_Click`.
- `Debug.WriteLine` in `MarkiereGeaendert` (bereits durch LogDirty ersetzt).
- Erweiterte Fehlermeldung mit alt/neu-Pfaden im Umbenennen-catch (kann bleiben oder
  auf die neue Rechte-Meldung umgestellt werden).
- Zusätzlich: `catch (Exception)`-Sicherheitsnetze in `OrdnerEinlesen_Click` /
  `AktualisiereOrdner` (verhindern stilles Verschlucken von async-void-Exceptions).
  Diese sind sinnvoll und können bleiben.

## Build
- `dotnet build` zuletzt (Nutzer): **0 Fehler, 0 Warnungen** – nach dem vollständigen
  Abgleich von `MainWindow.xaml.cs` mit dem Dekompilat (`MainWindowSiCo.cs`).
- In der Agent-Umgebung funktioniert ein Terminal/Shell (Nutzer hat es bestätigt).
- ACHTUNG bei TagLib: `using TagLibFile = TagLib.File;` (Alias) verwenden, NICHT
  `using TagLib;` (sonst CS0104: `File` mehrdeutig vs. `System.IO.File`). `Tag` als
  `TagLib.Tag` qualifizieren.

## Publish (selbstenthaltend, NICHT single-file) – vom Nutzer gewünscht
- **Verwendeter Befehl (zuletzt genutzt, Output in eigenen Ordner `publish\`):**
  ```
  dotnet publish E:\VSS\ARBEITEN\Klangkiste\Klangkiste.csproj -c Release -r win-x64 --self-contained true -o "E:\VSS\ARBEITEN\Klangkiste\publish"
  ```
  Output: `E:\VSS\ARBEITEN\Klangkiste\publish\`
  (Achtung: NICHT `bin\...\publish\` – der Nutzer publisht in den eigenen Ordner via `-o`.)
- Wichtig: `--self-contained true` (Runtime dabei), **OHNE** `PublishSingleFile` (nicht zu
  einer EXE pressen – so gewünscht, „professioneller" Look mit Einzeldateien).
- **Update-Strategie des Nutzers:** Er **ersetzt einfach den kompletten `publish\`-Inhalt**
  über die Ziel-Installation (bewusst „alles ersetzen", um sicher zu sein – keine Einzeldatei-
  Auswahl). `config.ini` + `.mcd` ggf. vorher sichern, falls sie im selben Ordner liegen.
- Vor dem Publish: `MainWindowSiCo.cs` (Dekomilat-Referenz) aus dem Projektordner löschen –
  sie wird sonst mitkompiliert (harmlos, aber unsauber).
- Optional für schnelleren Start: `-p:ReadyToRun=true`; optional ohne Debug-Dateien:
  `-p:DebugType=None -p:DebugSymbols=false`.

## STATUS (Stand jetzt): PROJEKT ABGESCHLOSSEN / „FERTIG"
- Build sauber: **0 Fehler, 0 Warnungen**. `MainWindowSiCo.cs` wird vom Nutzer gelöscht.
- Publish (self-contained, Ordner `publish\`) gemacht – Nutzer ist durch.
- Sicherheitsnetz: ZIP-Backup unter `E:\_DATEN_D\KlangkisteSiCo.zip` (siehe ganz oben).
- Restrisiko (nur falls beim Benutzen auffällt, dann gezielt fixen): die aus dem Dekompilat
  rekonstruierten Pfade einzeln durchklicken – Sortierung per Spaltenklick,
  Import/Export-Rundreise, Rechtsklick-Kontextmenü, Filterfelder (die VIER Boxen) + Gesamtsuche.
- **Keine offenen Baustellen.** Neue Wünsche/Probleme → einfach melden, dann geht's weiter.

## Wie wir arbeiten
- Der Nutzer baut selbst (`dotnet build`). Terminal/Shell in der Agent-Umgebung funktioniert
  inzwischen (bestätigt); Datei-Löschen war früher nicht möglich – bei Bedarf prüfen.
- Erst besprechen, dann umsetzen. Keinen Code schreiben, wenn der Nutzer das ausdrücklich
  (noch) nicht möchte.
- Knapp und zielgerichtet arbeiten. Keine unnötigen Änderungen an unbetroffenen Stellen.
- **Am Ende jeder Sitzung: `NOTIZEN.md` aktualisieren**, damit der Chat gefahrlos gelöscht
  werden kann (Token sparen). Beim Wiederaufnehmen zuerst `NOTIZEN.md` lesen.
