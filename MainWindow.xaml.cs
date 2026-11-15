using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using TagLibFile = TagLib.File;

namespace Klangkiste
{
    /// <summary>
        /// Interaction logic for MainWindow.xaml
        /// </summary>
        public partial class MainWindow : Window
        {
                private readonly Sammlung sammlung = new();
        private readonly KlangkisteKonfiguration konfiguration = KlangkisteKonfiguration.Laden();
                                private bool spaltenWerdenAufgebaut;
        private bool auswahlWirdSynchronisiert;
        private MedienOrdner? aktuellAngezeigterOrdner;
        private string? offenerSammlungPfad;

        // Merkt sich die Reihenfolge, in der Dateien markiert (angehakt/ausgewählt) wurden.
        // Damit werden Playlists in genau der Klick-Reihenfolge an den Player übergeben,
        // statt in Anzeige- oder Baum-Reihenfolge.
                private readonly List<AudioDatei> markierReihenfolge = new();
        private readonly HashSet<AudioDatei> markierMenge = new();

                // Mindestanzeigedauer des Fortschrittsfensters, damit es bei sehr schnellen
        // Vorgängen nicht nur kurz aufblitzt.
        private static readonly TimeSpan MindestFortschrittsdauer = TimeSpan.FromMilliseconds(300);

        // Wartet so lange, dass das Fortschrittsfenster insgesamt mindestens
        // MindestFortschrittsdauer sichtbar war. Vor dem Öffnen bitte Stopwatch.Start().
        private static async System.Threading.Tasks.Task WarteAufMindestdauerAsync(Stopwatch uhr)
        {
            TimeSpan rest = MindestFortschrittsdauer - uhr.Elapsed;
            if (rest > TimeSpan.Zero)
            {
                await System.Threading.Tasks.Task.Delay(rest);
            }
        }

        // Vermerkt eine Datei als zuletzt markiert (ans Ende der Reihenfolge), sofern sie
        // noch nicht enthalten ist. Verhindert doppelte Einträge und Umsortierung.
        private void MerkeMarkierung(AudioDatei datei)
        {
            if (markierMenge.Add(datei))
            {
                markierReihenfolge.Add(datei);
            }
        }

        // Entfernt eine Datei aus der Markier-Reihenfolge (z. B. beim Entmarkieren).
        private void VergissMarkierung(AudioDatei datei)
        {
            if (markierMenge.Remove(datei))
            {
                markierReihenfolge.Remove(datei);
            }
        }

        // Setzt die Markier-Reihenfolge komplett zurück (z. B. beim Ordnerwechsel).
        private void LeereMarkierReihenfolge()
        {
            markierReihenfolge.Clear();
            markierMenge.Clear();
        }

                                                                                                                                public MainWindow()
                {
                    InitializeComponent();
                    RaeumeAlteTemporaerePlaylistsAuf();
                    DataContext = sammlung;
            Closing += MainWindow_Closing;
            DesignAnwendung.SetzeKonfiguration(konfiguration);
            WendeDesignAn();
            WendeGitternetzAn();
                                                ErzeugeDateispalten();
                        WendeGespeicherteBreitenAn();
            WendeSeitentrennerZustandAn();
            LadeLetzteSammlung();
            AktualisiereNeueSammlungButton();

            // Fensterzustand erst anwenden, wenn das HWND (und damit die DPI-Quelle)
            // existiert, damit die Monitor-/DIP-Umrechnung korrekt ist – aber noch vor dem
            // ersten Anzeigen, damit das Fenster nicht sichtbar springt.
            SourceInitialized += (_, _) => WendeFensterzustandAn();
        }

        // Stellt Fensterposition/-größe und Maximiert-Zustand aus der Konfiguration wieder her.
        // Berücksichtigt Mehrfachmonitore über den gespeicherten Gerätenamen.
        private void WendeFensterzustandAn()
        {
            if (konfiguration.FensterPositionGespeichert
                && konfiguration.FensterBreite > 100 && konfiguration.FensterHoehe > 100)
            {
                Rect bereich;
                if (MonitorHelfer.TryFindeMonitor(konfiguration.MonitorGeraet, this, out Rect monitorArbeitsbereich))
                {
                    bereich = monitorArbeitsbereich;
                }
                else
                {
                    // Monitor nicht (mehr) vorhanden: Standard-Arbeitsbereich des Hauptbildschirms.
                    bereich = SystemParameters.WorkArea;
                }

                double breite = Math.Min(konfiguration.FensterBreite, bereich.Width);
                double hoehe = Math.Min(konfiguration.FensterHoehe, bereich.Height);

                // Gespeicherte Position nur übernehmen, wenn sie (weitgehend) im Zielbereich liegt;
                // sonst zentriert auf diesem Monitor platzieren (z. B. nach Monitorwechsel).
                double links = konfiguration.FensterLinks;
                double oben = konfiguration.FensterOben;
                if (links < bereich.Left - 50 || oben < bereich.Top - 50
                    || links + 100 > bereich.Right || oben + 50 > bereich.Bottom)
                {
                    links = bereich.Left + (bereich.Width - breite) / 2;
                    oben = bereich.Top + (bereich.Height - hoehe) / 2;
                }

                WindowStartupLocation = WindowStartupLocation.Manual;
                Width = breite;
                Height = hoehe;
                Left = links;
                Top = oben;
            }

            if (konfiguration.FensterMaximiert)
            {
                WindowState = WindowState.Maximized;
            }
        }

        // Speichert Fensterzustand (maximiert/normal), Normalgröße/-position und Monitor.
        private void SpeichereFensterzustand()
        {
            konfiguration.FensterMaximiert = WindowState == WindowState.Maximized;

            // Bei maximiertem Fenster liefert RestoreBounds die zuvor gültige Normalposition.
            Rect normal = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            if (normal.Width > 100 && normal.Height > 100)
            {
                konfiguration.FensterPositionGespeichert = true;
                konfiguration.FensterBreite = normal.Width;
                konfiguration.FensterHoehe = normal.Height;
                konfiguration.FensterLinks = normal.Left;
                konfiguration.FensterOben = normal.Top;
            }

            (Rect _, string geraet) = MonitorHelfer.MonitorVonFenster(this);
            konfiguration.MonitorGeraet = geraet;

            SpeichereKonfiguration();
        }

                                // Die Gitternetzlinien werden im Design über die Zellränder gezeichnet
                // (exakte Stärke). Die eingebauten DataGrid-GridLines müssen deshalb immer
                // aus sein, damit keine zusätzliche 1px-Linie doppelt erscheint.
                private void WendeGitternetzAn()
                {
                    Dateiliste.GridLinesVisibility = DataGridGridLinesVisibility.None;
                }

        // Stellt die zuletzt gespeicherte Breite der Ordnerbaum-Spalte (Position des
        // Grid-Splitters) wieder her. Ist noch nichts gespeichert (0), bleibt die
        // Standardbreite aus der XAML erhalten.
                private void WendeGespeicherteBreitenAn()
        {
            if (konfiguration.OrdnerbaumBreite > 0 && OrdnerbaumSpalte is not null)
            {
                OrdnerbaumSpalte.Width = new GridLength(konfiguration.OrdnerbaumBreite);
            }
        }

        // Fixiert den Seitentrenner (deaktiviert ihn), wenn in den Einstellungen
        // „Seitentrenner fixieren" aktiv ist. Beim Start und bei Änderungen aufrufen.
        private void WendeSeitentrennerZustandAn()
        {
            if (Seitentrenner is not null)
            {
                Seitentrenner.IsEnabled = !konfiguration.SeitentrennerFixiert;
            }
        }

                // Wird ausgelöst, wenn der Benutzer den Splitter loslässt: aktuelle Spaltenbreite
        // in die Konfiguration übernehmen und sofort speichern.
                private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (OrdnerbaumSpalte is null)
            {
                return;
            }

            // Der Splitter setzt die Spaltenbreite in Pixeln. Bevorzugt diesen exakten Wert;
            // sonst die gerenderte Breite, falls die Spalte (noch) nicht pixelbasiert ist.
            double breite = OrdnerbaumSpalte.Width.GridUnitType == GridUnitType.Pixel
                ? OrdnerbaumSpalte.Width.Value
                : OrdnerbaumSpalte.ActualWidth;

            if (double.IsNaN(breite) || breite <= 0)
            {
                return;
            }

            konfiguration.OrdnerbaumBreite = breite;
            SpeichereKonfiguration();
        }

        // Lädt beim Start automatisch die zuletzt geöffnete Sammlung (.mcd), sofern
        // vorhanden. Fehler werden still ignoriert, damit der Start nie blockiert.
        private void LadeLetzteSammlung()
        {
            string letzte = konfiguration.LetzteSammlung;
            if (string.IsNullOrWhiteSpace(letzte) || !File.Exists(letzte))
            {
                return;
            }

            try
            {
                                Sammlung? geladeneSammlung = LadeSammlungAusDatei(letzte);

                if (geladeneSammlung is null || geladeneSammlung.Version != "1.0")
                {
                    return;
                }

                SetzeImportpfade(geladeneSammlung, Path.GetDirectoryName(letzte) ?? AppContext.BaseDirectory);
                sammlung.Stammordner.Clear();
                foreach (MedienOrdner stammordner in geladeneSammlung.Stammordner)
                {
                    SetzeElternordner(stammordner, null);
                    sammlung.Stammordner.Add(stammordner);
                }

                                                                offenerSammlungPfad = letzte;
                MarkiereUnveraendert();
                BerechneAlleOrdnerdauern();
                StatusText.Text = $"Sammlung geladen: {sammlung.Stammordner.Count} Stammordner, "
                    + $"{sammlung.Stammordner.Sum(ZaehleDateien)} Dateien.";

                WaehleErstenStammordner();
            }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
            {
                                // Beim Start nicht mit Fehlerdialog stören – einfach leer starten.
                sammlung.Stammordner.Clear();
                offenerSammlungPfad = null;
                AktualisiereNeueSammlungButton();
            }
        }

        // Wählt beim Start den ersten Stammordner der geladenen Sammlung im Baum aus,
        // damit dessen Dateien sofort in der Dateiliste erscheinen. Der Baum ist beim
        // Laden noch nicht gerendert, daher wird die Auswahl über den Dispatcher um einen
        // UI-Takt aufgeschoben (Loaded-Priorität), wenn die TreeViewItem-Container existieren.
        private void WaehleErstenStammordner()
        {
            if (sammlung.Stammordner.Count == 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sammlung.Stammordner.Count == 0)
                {
                    return;
                }

                Ordnerbaum.UpdateLayout();
                if (Ordnerbaum.ItemContainerGenerator
                        .ContainerFromItem(sammlung.Stammordner[0]) is TreeViewItem erstes)
                {
                    erstes.IsSelected = true;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

                private void Einstellungen_Click(object sender, RoutedEventArgs e)
        {
            var fenster = new EinstellungenWindow(konfiguration)
            {
                Owner = this
            };
            fenster.EinstellungenGeaendert += (_, _) => EinstellungenAnwenden();
            // Beim Schließen der Einstellungen die Spalten einmalig neu aufbauen
            // (Änderungen aus dem Spalten-Dialog wirken so sofort, nicht erst nach Neustart).
            fenster.EinstellungenGeschlossen += (_, _) =>
            {
                ErzeugeDateispalten();
                AktualisiereStatus();
            };
            fenster.ShowDialog();
        }

                private void Hilfe_Click(object sender, RoutedEventArgs e)
        {
            var fenster = new HilfeWindow
            {
                Owner = this
            };
            fenster.Show();
        }

                                private void MarkiereGeaendert()
                {
                    sammlung.IstGeaendert = true;
                    AktualisiereSpeichernButton();
                }

                        private void MarkiereUnveraendert()
                {
                    sammlung.IstGeaendert = false;
                    AktualisiereSpeichernButton();
                }

        // Fragt vor einer Aktion (z. B. Import), die die aktuelle Sammlung ersetzt,
        // ob ungespeicherte Änderungen gesichert werden sollen.
        // Rückgabe false = Aktion abbrechen.
        private bool BestaetigeUngespeicherteAenderungen()
        {
            if (!sammlung.IstGeaendert)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(offenerSammlungPfad))
            {
                MessageBoxResult ergebnis = MessageBox.Show(
                    "Die Sammlung enthält ungespeicherte Änderungen. Sollen sie in die geöffnete Datei gespeichert werden?",
                    "Ungespeicherte Änderungen",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (ergebnis == MessageBoxResult.Cancel)
                {
                    return false;
                }

                if (ergebnis == MessageBoxResult.Yes)
                {
                    SpeichereInDatei(offenerSammlungPfad);
                    return !sammlung.IstGeaendert;
                }

                return true;
            }

            // Keine offene .mcd: Export anbieten.
            MessageBoxResult exportErgebnis = MessageBox.Show(
                "Die Sammlung enthält ungespeicherte Änderungen. Sollen sie vorher exportiert werden?",
                "Ungespeicherte Änderungen",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (exportErgebnis == MessageBoxResult.Cancel)
            {
                return false;
            }

            if (exportErgebnis == MessageBoxResult.Yes)
            {
                Exportieren_Click(this, new RoutedEventArgs());
                // Nur weiter, wenn der Export tatsächlich (durch Abbrechen im Dialog) nicht
                // erzwungen wurde; wenn weiterhin dirty, abbrechen.
                return !sammlung.IstGeaendert;
            }

            return true;
        }

                private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Fensterzustand unabhängig vom Speichern-Dialog immer sichern.
            SpeichereFensterzustand();

            if (!sammlung.IstGeaendert)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(offenerSammlungPfad))
            {
                MessageBoxResult ergebnis = MessageBox.Show(
                    "Sollen die Änderungen an der Sammlung gespeichert werden?",
                    "Sammlung geändert",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (ergebnis == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (ergebnis == MessageBoxResult.Yes)
                {
                    SpeichereInDatei(offenerSammlungPfad);
                    if (sammlung.IstGeaendert)
                    {
                        // Speichern fehlgeschlagen -> nicht schließen.
                        e.Cancel = true;
                    }
                }

                return;
            }

            // Keine offene .mcd: Export anbieten.
            MessageBoxResult exportErgebnis = MessageBox.Show(
                "Die Sammlung enthält ungespeicherte Änderungen. Sollen sie exportiert werden?",
                "Sammlung geändert",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (exportErgebnis == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (exportErgebnis == MessageBoxResult.Yes)
            {
                Exportieren_Click(this, new RoutedEventArgs());
                if (sammlung.IstGeaendert)
                {
                    e.Cancel = true;
                }
            }
        }

                private void AktualisiereSpeichernButton()
                {
                    if (SammlungSpeichernButton is null)
                    {
                        // Kann während der Initialisierung vorkommen, bevor das Control existiert.
                        return;
                    }

                    SammlungSpeichernButton.IsEnabled =
                        sammlung.IstGeaendert && !string.IsNullOrWhiteSpace(offenerSammlungPfad);
                    AktualisiereNeueSammlungButton();
                }

                // Aktiviert den "Neue Sammlung"-Button nur, wenn eine .mcd geladen ist.
                private void AktualisiereNeueSammlungButton()
                {
                    if (NeueSammlungButton is null)
                    {
                        return;
                    }

                    NeueSammlungButton.IsEnabled = !string.IsNullOrWhiteSpace(offenerSammlungPfad);
                    AktualisiereOrdnerSortierenButton();
                }

                // Aktiviert "Ordner sortieren", sobald sich mindestens ein Ordner in der Sammlung befindet –
                // unabhängig davon, ob eine .mcd geladen wurde.
                private void AktualisiereOrdnerSortierenButton()
                {
                    if (OrdnerSortierenButton is null)
                    {
                        return;
                    }

                    OrdnerSortierenButton.IsEnabled = sammlung.Stammordner.Count > 0;
                }

                // Sortiert die gesamte Ordnerstruktur rekursiv neu: erst Sonderzeichen, dann Zahlen,
                // dann Buchstaben (gleiche Reihenfolge wie in der Dateiliste). Nach Bestätigung wird in
                // jedem Fall die Dirty-Flag gesetzt, auch wenn faktisch nichts umsortiert werden musste.
                private void OrdnerSortieren_Click(object sender, RoutedEventArgs e)
                {
                    MessageBoxResult bestaetigung = MessageBox.Show(
                        "Sammlung sortieren?",
                        "Ordner sortieren",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);
                    if (bestaetigung != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    SortiereOrdnerListe(sammlung.Stammordner);
                    MarkiereGeaendert();
                    AktualisiereOrdnerSortierenButton();
                    StatusText.Text = $"Ordner sortiert: {sammlung.Stammordner.Count} Stammordner, "
                        + $"{sammlung.Stammordner.Sum(ZaehleDateien)} Dateien geladen.";
                }

                // Sortiert eine ObservableCollection<MedienOrdner> in-place nach Name (Sonderzeichen <
                // Ziffern < Buchstaben) und sortiert jeden Unterordner rekursiv mit.
                private static void SortiereOrdnerListe(ObservableCollection<MedienOrdner> ordnerListe)
                {
                    // Rekursiv zuerst die Unterordner jeder Ebene sortieren.
                    foreach (MedienOrdner ordner in ordnerListe)
                    {
                        SortiereOrdnerListe(ordner.Unterordner);
                    }

                    // Diese Ebene in-place umsortieren (stabile Reihenfolge über den Vergleich).
                    List<MedienOrdner> sortiert = ordnerListe
                        .OrderBy(Ordner => Ordner, MedienOrdnerNameComparer.Instanz)
                        .ToList();

                    for (int i = 0; i < sortiert.Count; i++)
                    {
                        MedienOrdner ordner = sortiert[i];
                        int aktuellerIndex = ordnerListe.IndexOf(ordner);
                        if (aktuellerIndex != i)
                        {
                            ordnerListe.Move(aktuellerIndex, i);
                        }
                    }
                }

                // Vergleicht MedienOrdner anhand ihres Namens mit der gemeinsamen Zeichenreihenfolge
                // (Sonderzeichen < Ziffern < Buchstaben).
                private sealed class MedienOrdnerNameComparer : IComparer<MedienOrdner>
                {
                    public static readonly MedienOrdnerNameComparer Instanz = new();

                    public int Compare(MedienOrdner? x, MedienOrdner? y)
                    {
                        return NatuerlicheTextReihenfolge.Vergleiche(x?.Name, y?.Name);
                    }
                }

                // Legt eine neue, leere Sammlung an – wie beim allerersten Start.
                // Die zuletzt geladene .mcd wird nur ENTLADEN (Referenz in config.ini geleert),
                // die Datei selbst bleibt unangetastet auf dem Datenträger.
                private void NeueSammlung_Click(object sender, RoutedEventArgs e)
                {
                    // Abfrage 1: Bestätigung, dass wirklich eine neue Sammlung erstellt werden soll.
                    MessageBoxResult bestaetigung = MessageBox.Show(
                        "Soll wirklich eine neue Sammlung erstellt werden?\n\nDie aktuelle Sammlung wird geschlossen.",
                        "Neue Sammlung",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);
                    if (bestaetigung != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    // Abfrage 2: Nur bei ungespeicherten Änderungen nach dem Speichern fragen.
                    // Bei Abbruch wird KEINE neue Sammlung erstellt.
                    if (sammlung.IstGeaendert && !BestaetigeUngespeicherteAenderungen())
                    {
                        return;
                    }

                    // Sammlung leeren (wie beim allerersten Start).
                    sammlung.Stammordner.Clear();
                    offenerSammlungPfad = null;

                    // Nur die Referenz auf die letzte Sammlung entfernen – die .mcd-DATEI bleibt bestehen.
                    konfiguration.LetzteSammlung = string.Empty;
                    SpeichereKonfiguration();

                    // Anzeige zurücksetzen.
                    aktuellAngezeigterOrdner = null;
                    Dateiliste.ItemsSource = null;
                    SetzeAlleHaken(false);
                    LeereMarkierReihenfolge();

                    MarkiereUnveraendert();
                    AktualisiereNeueSammlungButton();
                    AktualisiereOrdnerSortierenButton();
                    AktualisiereDaueranzeige();
                    StatusText.Text = "Bereit. 0 Ordner, 0 Dateien geladen.";
                }

        private void SammlungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(offenerSammlungPfad))
            {
                // Keine offene .mcd vorhanden: auf Export ausweichen.
                Exportieren_Click(sender, e);
                return;
            }

            SpeichereInDatei(offenerSammlungPfad);
        }

                // Liest eine .mcd-Datei und erkennt dabei automatisch GZIP-komprimierte
        // (neue) und unkomprimierte Klartext-XML (alte) Dateien. Die Erkennung erfolgt
        // an den GZIP-Magic-Bytes (0x1F 0x8B) am Dateianfang. Dadurch bleiben alte
        // Sammlungen ohne Neueinlesen lesbar; neu gespeicherte werden komprimiert.
        private static Sammlung? LadeSammlungAusDatei(string pfad)
        {
            var serializer = new XmlSerializer(typeof(Sammlung));
            var einstellungen = new XmlReaderSettings { CheckCharacters = false };

            using var basis = File.OpenRead(pfad);

            // GZIP? -> erste zwei Bytes prüfen, danach wieder an den Anfang zurücksetzen.
            bool istGzip = basis.Length >= 2;
            if (istGzip)
            {
                int b0 = basis.ReadByte();
                int b1 = basis.ReadByte();
                istGzip = b0 == 0x1F && b1 == 0x8B;
                basis.Position = 0;
            }

            if (istGzip)
            {
                using var gzip = new GZipStream(basis, CompressionMode.Decompress);
                using var leser = XmlReader.Create(gzip, einstellungen);
                return serializer.Deserialize(leser) as Sammlung;
            }

            // Altes Format: unkomprimierte UTF-8-XML.
            using (var leser = XmlReader.Create(basis, einstellungen))
            {
                return serializer.Deserialize(leser) as Sammlung;
            }
        }

        private void SpeichereInDatei(string zielpfad)
        {
            try
            {
                sammlung.ExportDatum = DateTime.Now.ToString("O");
                var serializer = new XmlSerializer(typeof(Sammlung));
                var namespaces = new XmlSerializerNamespaces();
                namespaces.Add(string.Empty, string.Empty);

                                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    NewLineChars = Environment.NewLine,
                    // Fest auf UTF-8 (ohne BOM): Sonderzeichen/Symbole im Pfad/Text (z. B.
                    // En-Dash, typografisches Apostroph, Emojis) werden so 1:1 geschrieben
                    // und beim Laden wieder identisch gelesen.
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                };

                                Sammlung exportSammlung = ErzeugePortableKopie(sammlung, Path.GetDirectoryName(zielpfad) ?? AppContext.BaseDirectory);

                // ATOMARES SPEICHERN: zuerst in eine Temp-Datei neben der Ziel-.mcd schreiben.
                // Erst wenn das vollständig und fehlerfrei geklappt hat, wird die Temp-Datei
                // die echte Zieldatei (ersetzt/umbenannt). Bei einem Fehler bleibt die alte
                // .mcd dadurch unangetastet – kein Datenverlust mehr durch vorzeitiges Leeren.
                                string tempPfad = zielpfad + ".tmp";
                try
                {
                    // GZIP-KOMPRIMIERUNG: Die XML wird gepackt in die .mcd geschrieben
                    // (Endung bleibt .mcd). Dadurch schrumpft die Datei deutlich. Der Loader
                    // erkennt GZIP automatisch an den Magic-Bytes (1F 8B) und liest auch
                    // alte, unkomprimierte .mcd weiter.
                    using (var datei = File.Create(tempPfad))
                    using (var gzip = new GZipStream(datei, CompressionLevel.SmallestSize))
                    using (var xml = XmlWriter.Create(gzip, settings))
                    {
                        serializer.Serialize(xml, exportSammlung, namespaces);
                    }

                    // Temp -> Ziel "befördern". File.Replace ist atomar, benötigt aber eine
                    // bereits existierende Zieldatei; beim allerersten Speichern gibt es sie
                    // noch nicht, dann genügt ein Move (Umbenennen).
                    if (File.Exists(zielpfad))
                    {
                        File.Replace(tempPfad, zielpfad, null);
                    }
                    else
                    {
                        File.Move(tempPfad, zielpfad);
                    }
                }
                finally
                {
                    // Sicherheitsnetz: falls die Temp-Datei noch existiert (Fehlerfall oder
                    // abgebrochenes Replace), best effort aufräumen. Bei Erfolg ist sie
                    // bereits zur Zieldatei geworden und dieses Delete greift ins Leere.
                    try
                    {
                        if (File.Exists(tempPfad))
                        {
                            File.Delete(tempPfad);
                        }
                    }
                    catch (Exception)
                    {
                        // Aufräumen darf den Speichervorgang nicht stören.
                    }
                }

                                offenerSammlungPfad = zielpfad;
                konfiguration.LetzteSammlung = zielpfad;
                SpeichereKonfiguration();
                MarkiereUnveraendert();
                StatusText.Text = $"Sammlung gespeichert: {sammlung.Stammordner.Count} Stammordner, "
                    + $"{sammlung.Stammordner.Sum(ZaehleDateien)} Dateien.";
            }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                        {
                                            MessageBox.Show(
                                BaueSpeicherfehlerMeldung(ex, zielpfad),
                    "Speicherfehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Baut eine verständliche Fehlermeldung für den Speicherfehler: eine kurze, klar
        // benannte Kategorie (Rechte/Sperre/XML/Platte), der Zielpfad und – falls vorhanden –
        // die innere Meldung (z. B. das konkrete ungültige Zeichen). Bewusst knapp gehalten.
        private static string BaueSpeicherfehlerMeldung(Exception fehler, string zielpfad)
        {
            string kategorie;
            if (fehler is UnauthorizedAccessException)
            {
                kategorie = "Kein Schreibzugriff auf die Datei bzw. den Ordner (Windows-Dateirechte).";
            }
            else if (fehler is IOException)
            {
                kategorie = "Die Datei konnte nicht geschrieben werden "
                    + "(z. B. gesperrt, schreibgeschützt oder Datenträger voll).";
            }
            else if (fehler.InnerException is ArgumentException)
            {
                kategorie = "Die Sammlung enthält Zeichen, die nicht in eine XML-Datei passen.";
            }
            else
            {
                kategorie = "Beim Erzeugen der Sammlungsdatei ist ein Fehler aufgetreten.";
            }

            var text = new StringBuilder();
            text.Append("Die Sammlung konnte nicht gespeichert werden.\n\n");
            text.Append(kategorie);

            if (fehler.InnerException is { Message: { Length: > 0 } inner })
            {
                text.Append("\n\nDetails: ").Append(inner);
            }

            text.Append("\n\nZiel:\n").Append(zielpfad);
            text.Append("\n\nDie zuvor gespeicherte Datei bleibt unverändert erhalten.");
            return text.ToString();
        }

                                // Entfernt aus einem Text NUR die Zeichen, die XML tatsächlich verbietet.
                // Alles andere – Sonderzeichen, Symbole, En-Dash, typografisches Apostroph,
                // Emojis, Akzente usw. – bleibt 1:1 erhalten (nichts wird ersetzt/umgeschrieben).
                // Entfernt werden nur:
                //   • Steuerzeichen < 0x20 außer Tab/LF/CR (0x09/0x0A/0x0D)
                //   • U+FFFE und U+FFFF (von XML nicht erlaubt)
                //   • einzelne (unpaarige) Surrogat-Hälften – die würden beim Kodieren sonst
                //     als Ersatzzeichen enden. Paarige Surrogate (z. B. Emojis) bleiben komplett.
                private static string BereinigeXmlText(string? wert)
                {
                    if (string.IsNullOrEmpty(wert))
                    {
                        return wert ?? string.Empty;
                    }

                    // Schnelldurchlauf: nichts zu tun? Dann Original zurückgeben (keine Allokation).
                    int problemIndex = FindeErstesIllegalesZeichen(wert);
                    if (problemIndex < 0)
                    {
                        return wert;
                    }

                    var bereinigt = new StringBuilder(wert.Length).Append(wert, 0, problemIndex);
                    for (int i = problemIndex; i < wert.Length; i++)
                    {
                        char c = wert[i];

                        // Paariges Surrogat (gültiges Zeichen außerhalb der BMP, z. B. Emoji):
                        // beide Hälften übernehmen.
                        if (char.IsHighSurrogate(c) && i + 1 < wert.Length && char.IsLowSurrogate(wert[i + 1]))
                        {
                            bereinigt.Append(c).Append(wert[i + 1]);
                            i++;
                            continue;
                        }

                        if (!IstXmlIllegal(c))
                        {
                            bereinigt.Append(c);
                        }
                    }

                    return bereinigt.ToString();
                }

                private static int FindeErstesIllegalesZeichen(string wert)
                {
                    for (int i = 0; i < wert.Length; i++)
                    {
                        char c = wert[i];
                        if (char.IsHighSurrogate(c) && i + 1 < wert.Length && char.IsLowSurrogate(wert[i + 1]))
                        {
                            i++; // gültiges Surrogat-Paar überspringen
                            continue;
                        }

                        if (IstXmlIllegal(c))
                        {
                            return i;
                        }
                    }

                    return -1;
                }

                // True, wenn das Zeichen in XML nicht vorkommen darf und entfernt werden muss.
                // Bewusst NUR die echten XML-Verbote (keine engere Whitelist mehr), damit
                // gültige Unicode-Zeichen im Pfad/Text unverändert bleiben.
                private static bool IstXmlIllegal(char c)
                {
                    // Erlaubte Steuerzeichen: Tab, Zeilenumbruch, Wagenrücklauf.
                    if (c == '\t' || c == '\n' || c == '\r')
                    {
                        return false;
                    }

                    // Alle übrigen Steuerzeichen (inkl. DEL 0x7F) sind in XML nicht erlaubt.
                    if (c < 0x20 || c == 0x7F)
                    {
                        return true;
                    }

                    // U+FFFE und U+FFFF sind nicht erlaubt.
                    if (c == '\uFFFE' || c == '\uFFFF')
                    {
                        return true;
                    }

                    // Einzelne Surrogat-Hälften (unpaarig) sind ungültig. Paarige Surrogate
                    // werden vom Aufrufer bereits zusammengefasst und hier gar nicht geprüft.
                    if (char.IsSurrogate(c))
                    {
                        return true;
                    }

                    // Alles andere (Buchstaben, Akzente, Symbole, En-Dash, typografisches
                    // Apostroph, Emojis, …) bleibt erhalten.
                    return false;
                }

        // Wendet geänderte Einstellungen (Design, Schrift, Gitternetz, Spalten) an.
                private void EinstellungenAnwenden()
        {
            WendeDesignAn();
            WendeGitternetzAn();
            WendeSeitentrennerZustandAn();
            ErzeugeDateispalten();
            AktualisiereStatus();
        }

        private void WendeDesignAn()
        {
            DesignAnwendung.WendeAn(this);
        }

                                        // Sammelt alle aktuell in der Sammlung enthaltenen Ordnerpfade (rekursiv). Wird an den
        // Ordner-Auswahldialog übergeben, damit er bereits eingelesene Ordner überspringen kann.
        private IEnumerable<string> ErmittleEingeleseneOrdnerpfade()
        {
            var pfade = new List<string>();
            foreach (MedienOrdner stammordner in sammlung.Stammordner)
            {
                SammleOrdnerpfade(stammordner, pfade);
            }

            return pfade;
        }

        private static void SammleOrdnerpfade(MedienOrdner ordner, List<string> ziel)
        {
            if (!string.IsNullOrWhiteSpace(ordner.Pfad))
            {
                ziel.Add(ordner.Pfad);
            }

            foreach (MedienOrdner unterordner in ordner.Unterordner)
            {
                SammleOrdnerpfade(unterordner, ziel);
            }
        }

                                private async void OrdnerEinlesen_Click(object sender, RoutedEventArgs e)
        {
                        var dialog = new OrdnerAuswahlDialog(konfiguration.Hauptlaufwerk, ErmittleEingeleseneOrdnerpfade())
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            // Gewähltes Hauptlaufwerk (Startordner) dauerhaft merken.
            if (!string.IsNullOrWhiteSpace(dialog.Hauptlaufwerk)
                && !string.Equals(dialog.Hauptlaufwerk, konfiguration.Hauptlaufwerk, StringComparison.OrdinalIgnoreCase))
            {
                konfiguration.Hauptlaufwerk = dialog.Hauptlaufwerk;
                SpeichereKonfiguration();
            }

            // Fortschrittsfenster einblenden und das (zeitaufwändige) Scannen im
            // Hintergrund ausführen, damit die Fortschrittsleiste animiert bleibt.
            var fortschritt = new FortschrittWindow { Owner = this };
            var uhr = Stopwatch.StartNew();
            var stammordnerListe = new List<MedienOrdner>();

            try
            {
                fortschritt.Show();

                // Alle gewählten Ordner nacheinander scannen (gleiche Einstellungen für alle).
                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (string ordnerpfad in dialog.Ordnerpfade)
                    {
                        stammordnerListe.Add(ScanneOrdner(
                            ordnerpfad,
                            null,
                            dialog.Dateityp,
                            dialog.UnterordnerEinbeziehen,
                            dialog.BenutzerdefinierterFilter));
                    }
                });
            }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            MessageBox.Show(
                                $"Der Ordner konnte nicht eingelesen werden: {ex.Message}",
                                "Fehler beim Einlesen",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return;
                        }
                        catch (Exception ex)
                        {
                            // Sicherheitsnetz: In async void würde eine unbehandelte Exception sonst
                            // still verschluckt und der restliche Code (inkl. MarkiereGeaendert) nie
                            // erreicht. Hier sichtbar machen.
                            MessageBox.Show(
                                $"Unerwarteter Fehler beim Einlesen: {ex.Message}",
                                "Fehler beim Einlesen",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return;
                        }
                        finally
                        {
                            await WarteAufMindestdauerAsync(uhr);
                            fortschritt.Close();
                        }

            if (stammordnerListe.Count == 0)
            {
                return;
            }

                        foreach (MedienOrdner stammordner in stammordnerListe)
            {
                sammlung.Stammordner.Add(stammordner);
            }
            // Neue Ordner wurden eingelesen: Die Sammlung gilt in JEDEM Fall als geändert,
            // unabhängig davon, ob eine .mcd geöffnet ist. So erscheint beim Beenden zuverlässig
            // die Speichern-/Export-Abfrage.
            BerechneAlleOrdnerdauern();
            MarkiereGeaendert();
            AktualisiereOrdnerSortierenButton();
            AktualisiereDaueranzeige();
            StatusText.Text = $"Bereit. {sammlung.Stammordner.Count} Ordner, "
                + $"{sammlung.Stammordner.Sum(ZaehleDateien)} Dateien geladen.";
        }

                private void AusSammlungLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<AudioDatei> dateien = ErmittleAusgewaehlteDateien().ToList();
            if (dateien.Count == 0)
            {
                return;
            }

            MessageBoxResult bestaetigung = MessageBox.Show(
                $"Sollen {dateien.Count} Datei(en) nur aus der Sammlung entfernt werden?",
                "Aus Sammlung löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (bestaetigung != MessageBoxResult.Yes)
            {
                return;
            }

                                                            foreach (AudioDatei datei in dateien)
            {
                EntferneAusSammlung(datei);
            }

            BerechneAlleOrdnerdauern();
            MarkiereGeaendert();
            Dateiliste.SelectedItems.Clear();
            WendeFilterAn();
        }

        private void DateiVomMediumLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<AudioDatei> dateien = ErmittleAusgewaehlteDateien().ToList();
            if (dateien.Count == 0)
            {
                return;
            }

            MessageBoxResult loeschart = MessageBox.Show(
                $"Sollen {dateien.Count} Datei(en) vom Medium gelöscht werden?\n\n"
                + "Ja = Papierkorb\nNein = endgültig löschen\nAbbrechen = keine Aktion",
                "Von Medium löschen",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel);
            if (loeschart == MessageBoxResult.Cancel)
            {
                return;
            }

                        try
            {
                foreach (AudioDatei datei in dateien)
                {
                    if (File.Exists(datei.Pfad))
                    {
                        if (loeschart == MessageBoxResult.Yes)
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                datei.Pfad,
                                UIOption.OnlyErrorDialogs,
                                RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            File.Delete(datei.Pfad);
                        }
                    }

                                        EntferneAusSammlung(datei);
                }

                BerechneAlleOrdnerdauern();
                MarkiereGeaendert();
                Dateiliste.SelectedItems.Clear();
                WendeFilterAn();
            }
            catch (UnauthorizedAccessException)
            {
                ZeigeFehlendeDateirechte(
                    "Löschen nicht möglich",
                    "Mindestens eine Datei konnte nicht vom Medium gelöscht werden.",
                    Path.GetDirectoryName(dateien[0].Pfad));
            }
            catch (Exception ex) when (ex is IOException)
            {
                MessageBox.Show(
                    $"Mindestens eine Datei konnte nicht gelöscht werden: {ex.Message}",
                    "Löschen fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

                        private void DateiEigenschaften_Click(object sender, RoutedEventArgs e)
        {
            List<AudioDatei> dateien = ErmittleAusgewaehlteDateien().ToList();
            if (dateien.Count == 0)
            {
                return;
            }

            long groesse = dateien.Sum(Datei => Datei.GroesseBytes);
            string medientyp = GemeinsamerWert(dateien.Select(Datei => Datei.Medientyp));
            string artist = GemeinsamerWert(dateien.Select(Datei => Datei.Artist));
            string album = GemeinsamerWert(dateien.Select(Datei => Datei.Album));
            string details = $"Dateien: {dateien.Count:N0}\n"
                + $"Gesamtgröße: {FormatiereGroesse(groesse)}\n"
                + $"Medientyp: {medientyp}\n"
                + $"Artist: {artist}\n"
                + $"Album: {album}";

            if (dateien.Count == 1)
            {
                details += $"\nPfad: {dateien[0].Pfad}\n"
                    + $"Dauer: {dateien[0].DauerAnzeige}\n"
                    + $"Bitrate: {dateien[0].Bitrate} kbps";
            }

            MessageBox.Show(
                details,
                dateien.Count == 1 ? $"Eigenschaften: {dateien[0].Dateiname}" : "Eigenschaften der Auswahl",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Zeigt eine aussagekräftige Meldung, wenn Windows das Ändern/Löschen einer Datei
        // wegen fehlender Dateirechte (ACL) verweigert. Klar stellen, dass es NICHT an der
        // Klangkiste liegt und durch die App nicht umgehbar ist – inkl. kurzem Lösungsweg.
        // Der Ordnerpfad (falls vorhanden) wird zum Nachschlagen angezeigt.
        private void ZeigeFehlendeDateirechte(string titel, string kontext, string? ordner)
        {
            string text = kontext
                + "\n\nUrsache sind fehlende Windows-Dateirechte, nicht die Klangkiste.\n"
                + "Das Konto/die Gruppe hat für diese Datei(en) kein Schreib-/Löschrecht "
                + "(z. B. nur „Lesen und Ausführen“). Durch die App ist das nicht umgehbar.\n\n"
                + "So beheben Sie es in Windows:\n"
                + "  • Explorer → Rechtsklick auf die Datei/den Ordner → Eigenschaften\n"
                + "  • Reiter „Sicherheit“ → „Erweitert“ → Besitzer übernehmen\n"
                + "  • Anschließend unter „Berechtigungen“ Vollzugriff für Ihr Konto setzen\n\n"
                + "Mehr dazu unter „Hilfe → 16. Dateirechte / Zugriff verweigert“.";

            if (!string.IsNullOrWhiteSpace(ordner))
            {
                text += $"\n\nBetroffener Ordner:\n{ordner}";
            }

                        MessageBox.Show(text, titel, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void EntferneAusSammlung(AudioDatei datei)
        {
            MedienOrdner? ordner = FindeOrdner(datei, sammlung.Stammordner);
            ordner?.AudioDateien.Remove(datei);
            datei.IstAngehakt = false;
        }

        private static MedienOrdner? FindeOrdner(AudioDatei datei, IEnumerable<MedienOrdner> ordner)
        {
            foreach (MedienOrdner aktuellerOrdner in ordner)
            {
                if (aktuellerOrdner.AudioDateien.Contains(datei))
                {
                    return aktuellerOrdner;
                }

                MedienOrdner? gefunden = FindeOrdner(datei, aktuellerOrdner.Unterordner);
                if (gefunden is not null)
                {
                    return gefunden;
                }
            }

            return null;
        }

        private static string GemeinsamerWert(IEnumerable<string> werte)
        {
            string[] nichtLeereWerte = werte.Where(Wert => !string.IsNullOrWhiteSpace(Wert)).Distinct().ToArray();
            return nichtLeereWerte.Length == 1 ? nichtLeereWerte[0] : "—";
        }

                        private void Dateiliste_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.DataContext is AudioDatei datei)
            {
                StarteWiedergabe(new[] { datei });
                e.Handled = true;
            }
        }

        private void DateiAbspielen_Click(object sender, RoutedEventArgs e)
        {
            StarteWiedergabe(ErmittleAusgewaehlteDateien());
        }

                private void AuswahlHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            StarteWiedergabe(ErmittleAusgewaehlteDateien(), anhaengen: true);
        }

                private void AuswahlAufheben_Click(object sender, RoutedEventArgs e)
        {
            foreach (AudioDatei datei in ErmittleAusgewaehlteDateien().ToList())
            {
                datei.IstAngehakt = false;
                VergissMarkierung(datei);
                Dateiliste.SelectedItems.Remove(datei);
            }
        }

                private void StarteWiedergabe(IEnumerable<AudioDatei> auswahl, bool anhaengen = false)
                {
                    // Nur vorhandene Dateien betrachten.
                    var vorhanden = auswahl
                        .Where(Datei => File.Exists(Datei.Pfad))
                        .Distinct()
                        .ToList();

                    // Audio/Video -> Player. Alles andere (z. B. Playlists, PDFs) -> Windows-Standardprogramm.
                    List<AudioDatei> dateien = vorhanden
                        .Where(Datei => Datei.IstAbspielbar)
                        .ToList();

                    List<AudioDatei> sonstige = vorhanden
                        .Where(Datei => !Datei.IstAbspielbar)
                        .ToList();

                    int wiedergabelisten = 0;
                    foreach (IGrouping<string, AudioDatei> gruppe in dateien.GroupBy(Datei => Datei.Medientyp))
                    {
                        List<AudioDatei> gruppenDateien = gruppe.ToList();
                        string playlist = ErstelleTemporäreM3U(gruppe.Key, gruppenDateien);
                        PlayerEinstellung player = string.Equals(gruppe.Key, "Video", StringComparison.OrdinalIgnoreCase)
                            ? konfiguration.VideoPlayer
                            : konfiguration.AudioPlayer;
                                        StartePlaylist(playlist, gruppenDateien.Select(Datei => Datei.Pfad).ToList(), player, anhaengen);
                        wiedergabelisten++;
                    }

                    // Nicht abspielbare Dateien mit ihrem Windows-Standardprogramm öffnen
                    // (wie ein Doppelklick im Explorer).
                    int geoeffnet = OeffneMitWindowsStandardprogramm(sonstige);

                    if (wiedergabelisten == 0 && geoeffnet == 0)
                    {
                        MessageBox.Show(
                            "Es wurden keine abspielbaren Dateien ausgewählt.",
                            "Keine Wiedergabe möglich",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }

                // Öffnet die übergebenen Dateien so, wie es ein Doppelklick im Explorer täte
                // (Shell-Verknüpfung / Standardprogramm). Fehlende oder nicht startbare Dateien
                // werden übersprungen und am Ende gesammelt gemeldet.
                // Rückgabe: Anzahl der tatsächlich geöffneten Dateien.
                private static int OeffneMitWindowsStandardprogramm(IReadOnlyList<AudioDatei> dateien)
                {
                    int geoeffnet = 0;
                    var fehler = new List<string>();

                    foreach (AudioDatei datei in dateien)
                    {
                        if (!File.Exists(datei.Pfad))
                        {
                            fehler.Add($"{datei.Dateiname} (Datei nicht gefunden)");
                            continue;
                        }

                        try
                        {
                            Process.Start(new ProcessStartInfo { FileName = datei.Pfad, UseShellExecute = true });
                            geoeffnet++;
                        }
                        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
                        {
                            fehler.Add($"{datei.Dateiname} ({ex.Message})");
                        }
                    }

                    if (fehler.Count > 0)
                    {
                        MessageBox.Show(
                            "Folgende Datei(en) konnten nicht geöffnet werden:\n\n"
                            + string.Join("\n", fehler),
                            "Öffnen",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    return geoeffnet;
                }

                                                                private static void StartePlaylist(string playlist, IReadOnlyList<string> dateien, PlayerEinstellung player, bool anhaengen = false)
        {
            if (player.Profil == PlayerProfil.WindowsStandard)
            {
                // Windows-Standard: die Playlist an die zugeordnete Anwendung übergeben.
                Process.Start(new ProcessStartInfo { FileName = playlist, UseShellExecute = true });
                return;
            }

            if (string.IsNullOrWhiteSpace(player.ExePfad) || !File.Exists(player.ExePfad))
            {
                MessageBox.Show(
                    "Für das ausgewählte Playerprofil ist keine gültige EXE-Datei hinterlegt.",
                    "Wiedergabe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                                if (anhaengen && player.Profil == PlayerProfil.Winamp)
                {
                    StarteWinampEinreihen(player, dateien);
                }
                else if (anhaengen && player.Profil == PlayerProfil.VLC)
                {
                    StarteVlcEinreihen(player, dateien);
                }
                else if (!anhaengen && player.Profil == PlayerProfil.VLC)
                {
                    // VLC spielt die Dateien direkt (nicht über eine .m3u) ab: Bei laufender
                    // Instanz legt VLC eine .m3u sonst nur als EIN Element in die Playlist,
                    // statt sie in Einzeltitel aufzulösen. Mit den Einzeldateien + --started-from-file
                    // baut VLC selbst die richtige (ersetzende) Playlist in der laufenden Instanz.
                    StarteVlcAbspielen(player, dateien);
                }
                else
                {
                    ProcessStartInfo startInfo = ErzeugePlayerStartInfo(player, playlist, dateien, anhaengen);
                    LogStart(startInfo);
                    Process.Start(startInfo);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                MessageBox.Show(
                    $"Der Player konnte nicht gestartet werden: {ex.Message}",
                    "Wiedergabe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

                                // Reiht Dateien in eine bereits laufende Winamp-Instanz ein – analog zum
        // Explorer-Kontextmenü "Enqueue in Winamp". Pro Datei wird ein eigener Aufruf
        // gestartet, weil Winamp bei einem gemeinsamen Aufruf die aktuelle Playlist leert.
                private static void StarteWinampEinreihen(PlayerEinstellung player, IReadOnlyList<string> dateien)
                {
                    // Winamp 2.x (z. B. 2.91) kennt kein /ENQUEUE – dieser Schalter kam erst mit
                    // Winamp 5. In 2.x hängt /ADD die Dateien an die laufende Playlist an, ohne
                    // die Wiedergabe zu starten (genau der "Enqueue in Winamp"-Kontextmenübefehl).
                    // Pro Datei ein eigener Aufruf, weil Winamp bei mehreren Argumenten in einem
                    // Aufruf die bestehende Playlist ersetzt statt sie zu ergänzen.
                    foreach (string datei in dateien)
                    {
                        var anhaengeInfo = new ProcessStartInfo
                        {
                            FileName = player.ExePfad,
                            Arguments = $"/ADD \"{datei}\"",
                            UseShellExecute = false,
                            WorkingDirectory = Path.GetDirectoryName(player.ExePfad) ?? string.Empty
                        };
                        LogStart(anhaengeInfo);
                        Process.Start(anhaengeInfo);
                    }
                }

        // Spielt die Auswahl in VLC ab und ERSETZT dabei die laufende Playlist.
        // Entscheidend: die Dateien werden OHNE --playlist-enqueue übergeben. VLC
        // interpretiert einen Aufruf ohne Enqueue-Schalter als "neue Playlist laden und
        // abspielen" – das entspricht genau dem gewünschten "Abspielen". Mit
        // --started-from-file dockt das an die laufende Instanz an (kein zweites Fenster).
        // Alle Dateien in EINEM Aufruf, damit VLC daraus genau eine (ersetzende) Playlist baut.
        private static void StarteVlcAbspielen(PlayerEinstellung player, IReadOnlyList<string> dateien)
        {
            var abspielInfo = new ProcessStartInfo
            {
                FileName = player.ExePfad,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(player.ExePfad) ?? string.Empty
            };

            abspielInfo.ArgumentList.Add("--started-from-file");
            foreach (string datei in dateien)
            {
                abspielInfo.ArgumentList.Add(datei);
            }

            LogStart(abspielInfo);
            Process.Start(abspielInfo);
        }

        // Reiht Dateien in eine laufende VLC-Instanz ein (hängt an, ohne zu ersetzen).
        // Wichtig ist der Schalter --started-from-file: er löst VLCs Einzelinstanz-/IPC-Logik
        // aus, sodass die laufende Instanz die Dateien übernimmt, statt ein zweites Fenster
        // zu öffnen. Pro Datei ein eigener Aufruf, damit nur angehängt wird.
        private static void StarteVlcEinreihen(PlayerEinstellung player, IReadOnlyList<string> dateien)
        {
            foreach (string datei in dateien)
            {
                var anhaengeInfo = new ProcessStartInfo
                {
                    FileName = player.ExePfad,
                    Arguments = $"--started-from-file --playlist-enqueue \"{datei}\"",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(player.ExePfad) ?? string.Empty
                };
                LogStart(anhaengeInfo);
                Process.Start(anhaengeInfo);
            }
        }

        // Schreibt die für das Winamp-"Enqueue" relevanten Registry-Einträge ins Log,
        // damit nachvollziehbar ist, welches Verb die installierte Winamp-Version
        // bereitstellt und ob .mp3 überhaupt mit Winamp verknüpft ist.
        internal static void LogWinampRegistryDiagnose()
        {
            try
            {
                string logPfad = @"E:\Klangkiste_start.log";
                var zeilen = new List<string> { $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] --- Registry-Diagnose Winamp ---" };

                // 1) .mp3-Verknüpfung: ProgId + darunter registrierte Verben.
                using (RegistryKey? mp3 = Registry.ClassesRoot.OpenSubKey(".mp3"))
                {
                    string progId = mp3?.GetValue(null)?.ToString() ?? "(nicht gesetzt)";
                    zeilen.Add($"  .mp3 -> ProgId: {progId}");

                    if (!progId.StartsWith(".", StringComparison.Ordinal))
                    {
                        using RegistryKey? verbKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell");
                        if (verbKey is not null)
                        {
                            zeilen.Add("  Verben unter " + progId + @"\shell: "
                                + string.Join(", ", verbKey.GetSubKeyNames()));

                            foreach (string verb in verbKey.GetSubKeyNames())
                            {
                                using RegistryKey? command = Registry.ClassesRoot
                                    .OpenSubKey($@"{progId}\shell\{verb}\command");
                                zeilen.Add($"    [{verb}] {(command?.GetValue(null)?.ToString() ?? "(kein command)")}");
                            }
                        }
                    }
                }

                // 2) Winamp-spezifische Klassen: Winamp.MP3, Winamp.File etc.
                foreach (string klassenName in new[] { "Winamp.MP3", "Winamp.File", "Winamp.Playlist" })
                {
                    using RegistryKey? shell = Registry.ClassesRoot.OpenSubKey($@"{klassenName}\shell");
                    if (shell is not null)
                    {
                        zeilen.Add($"  {klassenName}\\shell: " + string.Join(", ", shell.GetSubKeyNames()));
                        foreach (string verb in shell.GetSubKeyNames())
                        {
                            using RegistryKey? command = Registry.ClassesRoot
                                .OpenSubKey($@"{klassenName}\shell\{verb}\command");
                            zeilen.Add($"    [{verb}] {(command?.GetValue(null)?.ToString() ?? "(kein command)")}");
                        }
                    }
                }

                zeilen.Add(string.Empty);
                File.AppendAllLines(logPfad, zeilen);
            }
            catch (Exception)
            {
                // Diagnose darf nie stören.
            }
        }

        internal static void LogStart(ProcessStartInfo startInfo)
        {
            try
            {
                                string logPfad = @"E:\Klangkiste_start.log";
                var zeilen = new List<string>
                {
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FileName: {startInfo.FileName}",
                    "  Verb: " + (string.IsNullOrEmpty(startInfo.Verb) ? "(kein)" : startInfo.Verb),
                    "  UseShellExecute: " + startInfo.UseShellExecute,
                    "  Arguments: " + startInfo.Arguments
                };
                for (int i = 0; i < startInfo.ArgumentList.Count; i++)
                {
                    zeilen.Add($"  Arg[{i}]: {startInfo.ArgumentList[i]}");
                }

                zeilen.Add(string.Empty);
                File.AppendAllLines(logPfad, zeilen);
            }
            catch (Exception)
            {
                // Logging darf den Start nie verhindern.
            }
        }

        // Baut die ProcessStartInfo für einen externen Player.
        // WICHTIG: UseShellExecute = false, damit die Argumente exakt übergeben werden
        // (mit UseShellExecute = true ignoriert .NET die Argumente; dann startet z. B.
        // Winamp ohne Datei und zeigt seinen eigenen "Öffnen"-Dialog).
        // Die Argumente werden über ArgumentList gesetzt, das Anführungszeichen korrekt
        // behandelt – dadurch werden Pfade mit Leerzeichen zuverlässig als ein Argument
        // übergeben.
                internal static ProcessStartInfo ErzeugePlayerStartInfo(
            PlayerEinstellung player, string playlist, IReadOnlyList<string> dateien, bool anhaengen = false)
        {
            string vorlage = player.ErmittleVorlage(anhaengen);
            var startInfo = new ProcessStartInfo
            {
                FileName = player.ExePfad,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(player.ExePfad) ?? string.Empty
            };

            foreach (string argument in ZerlegeArgumente(vorlage, playlist, dateien))
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }

        // Zerlegt das Argument-Template in einzelne Argumente, wobei Platzhalter
        // durch die konkreten Werte ersetzt werden. Platzhalter werden IMMER als ein
        // einziges Argument behandelt (also nicht an Leerzeichen im Pfad zerlegt).
        internal static IEnumerable<string> ZerlegeArgumente(
            string vorlage, string playlist, IReadOnlyList<string> dateien)
        {
            var ergebnis = new List<string>();
            int i = 0;
            while (i < vorlage.Length)
            {
                char c = vorlage[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // Platzhalter als komplettes Argument?
                if (VersuchePlatzhalter(vorlage, ref i, playlist, dateien, ergebnis))
                {
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    // In Anführungszeichen gesetztes Argument einlesen (ohne die Quotes).
                    char quote = c;
                    i++;
                    var sb = new StringBuilder();
                    while (i < vorlage.Length && vorlage[i] != quote)
                    {
                        // Platzhalter auch innerhalb von Anführungszeichen ersetzen.
                        if (VersuchePlatzhalter(vorlage, ref i, playlist, dateien, ergebnis, inQuotes: sb))
                        {
                            continue;
                        }

                        sb.Append(vorlage[i]);
                        i++;
                    }

                    i++; // schließendes Quote
                    ergebnis.Add(sb.ToString());
                }
                else
                {
                    // Unquoted-Argument bis zum nächsten Whitespace einlesen.
                    var sb = new StringBuilder();
                    while (i < vorlage.Length && !char.IsWhiteSpace(vorlage[i]))
                    {
                        if (VersuchePlatzhalter(vorlage, ref i, playlist, dateien, ergebnis, inQuotes: sb))
                        {
                            continue;
                        }

                        sb.Append(vorlage[i]);
                        i++;
                    }

                    if (sb.Length > 0)
                    {
                        ergebnis.Add(sb.ToString());
                    }
                }
            }

            return ergebnis;
        }

        // Prüft, ob an Position "i" ein Platzhalter beginnt, und fügt ggf. seinen Wert
        // entweder an inQuotes an oder als eigenes Argument.
        private static bool VersuchePlatzhalter(
            string vorlage,
            ref int i,
            string playlist,
            IReadOnlyList<string> dateien,
            List<string> ergebnis,
            StringBuilder? inQuotes = null)
        {
            if (vorlage[i] != '{')
            {
                return false;
            }

            if (vorlage.AsSpan(i).StartsWith("{playlist}"))
            {
                Emittiere(playlist, inQuotes, ergebnis);
                i += "{playlist}".Length;
                return true;
            }

            if (vorlage.AsSpan(i).StartsWith("{file}"))
            {
                // {file} steht für ALLE Dateien: unquoted = mehrere Argumente,
                // inQuotes = durch Leerzeichen getrennter Text (Sonderfall, selten).
                if (inQuotes is not null)
                {
                    inQuotes.Append(string.Join(" ", dateien));
                }
                else
                {
                    ergebnis.AddRange(dateien);
                }

                i += "{file}".Length;
                return true;
            }

            if (vorlage.AsSpan(i).StartsWith("{first}"))
            {
                Emittiere(dateien.Count > 0 ? dateien[0] : string.Empty, inQuotes, ergebnis);
                i += "{first}".Length;
                return true;
            }

            return false;
        }

        private static void Emittiere(string wert, StringBuilder? inQuotes, List<string> ergebnis)
        {
            if (inQuotes is not null)
            {
                inQuotes.Append(wert);
            }
            else
            {
                ergebnis.Add(wert);
            }
        }

        

        private void SpaltenAnpassen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SpaltenAnpassenDialog(konfiguration)
            {
                Owner = this
            };
            dialog.SpaltenGeaendert += (_, _) =>
            {
                ErzeugeDateispalten();
                SpeichereKonfiguration();
            };
            dialog.ShowDialog();
        }

        private void ErzeugeDateispalten()
        {
            spaltenWerdenAufgebaut = true;
            try
            {
                Dateiliste.Columns.Clear();
                                if (konfiguration.Auswahlmodus != Auswahlmodus.Normal)
                {
                    // Eigene Template-Spalte mit CheckBox statt DataGridCheckBoxColumn:
                    // Letztere benötigt zum Umschalten erst den Zell-Edit-Modus (zwei Klicks).
                    // Eine CheckBox im Template reagiert sofort.
                    Dateiliste.Columns.Add(new DataGridTemplateColumn
                    {
                        Header = string.Empty,
                        CellTemplate = ErzeugeHakenTemplate(),
                        Width = new DataGridLength(36),
                        IsReadOnly = true,
                        CanUserResize = false,
                        CanUserSort = false
                    });
                }

                                foreach (SpaltenEinstellung spalte in konfiguration.Spalten
                                    .Where(Spalte => Spalte.Sichtbar)
                                    .OrderBy(Spalte => Spalte.Reihenfolge))
                                {
                                    if (spalte.Id == "Dateiname")
                                    {
                                        // Dateiname-Spalte mit Datei-Icon (Shell-Icon je nach Endung),
                                        // linksbündig.
                                                                                var namensspalte = new DataGridTemplateColumn
                                        {
                                            Header = spalte.Ueberschrift,
                                            Width = new DataGridLength(spalte.Breite),
                                            IsReadOnly = true,
                                            SortMemberPath = "Dateiname",
                                            CellTemplate = ErzeugeDateinameTemplate()
                                        };
                                        Dateiliste.Columns.Add(namensspalte);

                                        DependencyPropertyDescriptor? namensBreite =
                                            DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                                        namensBreite?.AddValueChanged(
                                            namensspalte,
                                            (_, _) => SpaltenbreiteGeaendert(spalte, namensspalte));
                                        continue;
                                    }

                                                                        var datenspalte = new DataGridTextColumn
                                    {
                                        Header = spalte.Ueberschrift,
                                        Binding = ErzeugeSpaltenbindung(spalte.Id),
                                        Width = new DataGridLength(spalte.Breite),
                                        IsReadOnly = true,
                                        SortMemberPath = spalte.Id
                                    };

                                    // Alle übrigen Spalten rechtsbündig.
                                    var rechtsStil = new Style(typeof(TextBlock));
                                    rechtsStil.Setters.Add(new Setter(
                                        System.Windows.Controls.TextBlock.HorizontalAlignmentProperty,
                                        HorizontalAlignment.Right));
                                    datenspalte.ElementStyle = rechtsStil;

                                    Dateiliste.Columns.Add(datenspalte);
                    DependencyPropertyDescriptor? breiteDescriptor =
                        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                    breiteDescriptor?.AddValueChanged(
                        datenspalte,
                        (_, _) => SpaltenbreiteGeaendert(spalte, datenspalte));
                }
            }
            finally
            {
                spaltenWerdenAufgebaut = false;
            }
        }

                                private static Binding ErzeugeSpaltenbindung(string spaltenId)
        {
            return spaltenId switch
            {
                "Dateiname" => new Binding(nameof(AudioDatei.Dateiname)),
                "Titel" => new Binding(nameof(AudioDatei.Titel)),
                "Artist" => new Binding(nameof(AudioDatei.Artist)),
                "Album" => new Binding(nameof(AudioDatei.Album)),
                "Bitrate" => new Binding(nameof(AudioDatei.Bitrate)) { StringFormat = "{0} kbps" },
                "Groesse" => new Binding(nameof(AudioDatei.GroesseAnzeige)),
                "Dauer" => new Binding(nameof(AudioDatei.DauerAnzeige)),
                "Pfad" => new Binding(nameof(AudioDatei.Pfad)),
                "Aenderungsdatum" => new Binding(nameof(AudioDatei.Aenderungsdatum))
                {
                    StringFormat = "dd.MM.yyyy HH:mm"
                },
                "Medientyp" => new Binding(nameof(AudioDatei.Medientyp)),
                _ => new Binding(nameof(AudioDatei.Dateiname))
            };
        }

        // CellTemplate für die Dateiname-Spalte: Datei-Icon + Name, linksbündig.
        private static DataTemplate ErzeugeDateinameTemplate()
        {
            var template = new DataTemplate();
            var panel = new FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel));
            panel.SetValue(System.Windows.Controls.StackPanel.OrientationProperty,
                System.Windows.Controls.Orientation.Horizontal);

                        var bild = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            bild.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                new Binding(nameof(AudioDatei.IconGlyph)));
            bild.SetValue(System.Windows.Controls.TextBlock.FontFamilyProperty,
                new FontFamily("Segoe UI Emoji"));
            bild.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 14.0);
            bild.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 5, 0));
            bild.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            panel.AppendChild(bild);

            var text = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            text.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                new Binding(nameof(AudioDatei.Dateiname)));
            text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            panel.AppendChild(text);

            template.VisualTree = panel;
            return template;
        }

        // CellTemplate für die Haken-Spalte: eine CheckBox, die sofort umschaltet.
        private static DataTemplate ErzeugeHakenTemplate()
        {
            var template = new DataTemplate();
            var checkBox = new FrameworkElementFactory(typeof(System.Windows.Controls.CheckBox));
            checkBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                new Binding(nameof(AudioDatei.IstAngehakt))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            checkBox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            template.VisualTree = checkBox;
            return template;
        }

        private void SpaltenbreiteGeaendert(SpaltenEinstellung spalte, DataGridColumn datenspalte)
        {
            if (spaltenWerdenAufgebaut || double.IsNaN(datenspalte.Width.DisplayValue))
            {
                return;
            }

            spalte.Breite = Math.Max(20, datenspalte.Width.DisplayValue);
            SpeichereKonfiguration();
        }

        private void SpeichereKonfiguration()
        {
            try
            {
                konfiguration.Speichern();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void WiedergabeOrdner_Click(object sender, RoutedEventArgs e)
        {
            WiedergabeOrdner(sender, false);
        }

        private void WiedergabeMitUnterordnern_Click(object sender, RoutedEventArgs e)
        {
            WiedergabeOrdner(sender, true);
        }

        private void WiedergabeOrdner(object sender, bool mitUnterordnern)
        {
            MedienOrdner? ordner = ErmittleKontextOrdner(sender);
            if (ordner is null)
            {
                return;
            }

            IEnumerable<AudioDatei> dateien = mitUnterordnern
                ? AlleDateien(ordner)
                : ordner.AudioDateien;

            var gruppen = dateien
                .Where(Datei => Datei.Medientyp is "Audio" or "Video")
                .GroupBy(Datei => Datei.Medientyp);

                        int wiedergabelisten = 0;
            foreach (IGrouping<string, AudioDatei> gruppe in gruppen)
            {
                List<AudioDatei> gruppenDateien = gruppe.ToList();
                string playlist = ErstelleTemporäreM3U(gruppe.Key, gruppenDateien);
                PlayerEinstellung player = string.Equals(gruppe.Key, "Video", StringComparison.OrdinalIgnoreCase)
                    ? konfiguration.VideoPlayer
                    : konfiguration.AudioPlayer;
                StartePlaylist(playlist, gruppenDateien.Select(Datei => Datei.Pfad).ToList(), player);
                wiedergabelisten++;
            }

            if (wiedergabelisten == 0)
            {
                MessageBox.Show(
                    "Im ausgewählten Ordner wurden keine abspielbaren Medien gefunden.",
                    "Keine Medien gefunden",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

                private static string ErstelleTemporäreM3U(string medientyp, IEnumerable<AudioDatei> dateien)
        {
            // Endung bewusst .m3u (nicht .m3u8): Winamp und viele Player lesen nur .m3u
            // zuverlässig. .m3u8 wird von Winamp oft als leere Playlist interpretiert.
            string playlist = Path.Combine(
                Path.GetTempPath(),
                $"Klangkiste_{medientyp}_{Guid.NewGuid():N}.m3u");

                        var zeilen = new List<string> { "#EXTM3U" };
            zeilen.AddRange(dateien.Select(Datei => Datei.Pfad));

            // WICHTIG: Seit .NET 5 liefert Encoding.Default immer UTF-8 (ohne BOM).
            // Winamp liest .m3u aber als ANSI (System-Codepage). Deshalb hier explizit
            // die Windows-ANSI-Codepage des Systems verwenden, damit Pfade mit Umlauten
            // (ä, ö, ü, ß) korrekt übergeben werden.
            File.WriteAllLines(playlist, zeilen, ErmittleAnsiKodierung());
            return playlist;
        }

        // Liefert die Windows-ANSI-Codepage (z. B. 1252 in Westeuropa). Fällt bei
        // fehlendem Provider auf Latin-1 (ISO-8859-1) zurück, das für Pfade genügt.
                internal static Encoding ErmittleAnsiKodierung()
        {
            try
            {
                // Bevorzugt die echte System-ANSI-Codepage (z. B. 1252).
                // Der CodePages-Provider ist für GetEncoding(codepage) nötig.
                Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                int codepage = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                if (codepage > 0)
                {
                    return Encoding.GetEncoding(codepage);
                }
            }
            catch (Exception)
            {
                // Provider/Codepage nicht verfügbar -> Fallback unten.
            }

            // Fallback: Latin-1 deckt westeuropäische Umlaute (ä, ö, ü, ß) ab und ist
            // immer verfügbar (kein Provider nötig).
            return Encoding.Latin1;
        }

        // Entfernt beim Start veraltete temporäre Playlists (älter als ein Tag).
        // Aktuelle Playlists werden nicht angerührt, da ein Player sie noch lesen könnte.
        private static void RaeumeAlteTemporaerePlaylistsAuf()
        {
            try
            {
                                DateTime grenze = DateTime.Now.AddDays(-1);
                foreach (string datei in Directory.EnumerateFiles(
                    Path.GetTempPath(), "Klangkiste_*.m3u*"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(datei) < grenze)
                        {
                            File.Delete(datei);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static IEnumerable<AudioDatei> AlleDateien(MedienOrdner ordner)
        {
            foreach (AudioDatei datei in ordner.AudioDateien)
            {
                yield return datei;
            }

            foreach (MedienOrdner unterordner in ordner.Unterordner)
            {
                foreach (AudioDatei datei in AlleDateien(unterordner))
                {
                    yield return datei;
                }
            }
        }

        private static MedienOrdner? ErmittleKontextOrdner(object sender)
        {
            DependencyObject? aktuellesElement = sender as DependencyObject;
            while (aktuellesElement is not null)
            {
                if (aktuellesElement is ContextMenu contextMenu
                    && contextMenu.PlacementTarget is TreeViewItem treeViewItem)
                {
                    return treeViewItem.DataContext as MedienOrdner;
                }

                aktuellesElement = (aktuellesElement as FrameworkElement)?.Parent;
            }

            return null;
        }

                private void OrdnerAktualisierenGesamt_Click(object sender, RoutedEventArgs e)
        {
            AktualisiereOrdner(sender, rekursiv: true);
        }

        private void OrdnerAktualisierenDieser_Click(object sender, RoutedEventArgs e)
        {
            AktualisiereOrdner(sender, rekursiv: false);
        }

                private async void AktualisiereOrdner(object sender, bool rekursiv)
        {
            MedienOrdner? ordner = ErmittleKontextOrdner(sender);
            if (ordner is null || !Directory.Exists(ordner.Pfad))
            {
                MessageBox.Show(
                    "Der Ordner ist auf dem Datenträger nicht mehr vorhanden.",
                    "Aktualisieren nicht möglich",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string umfang = rekursiv
                ? $"den Ordner „{ordner.Name}“ einschließlich seiner Unterordner"
                : $"nur den Ordner „{ordner.Name}“";
            MessageBoxResult bestaetigung = MessageBox.Show(
                $"Soll {umfang} neu eingelesen werden?",
                "Ordner aktualisieren",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (bestaetigung != MessageBoxResult.Yes)
            {
                return;
            }

                        // Fortschrittsfenster einblenden. Der Abgleich selbst läuft im UI-Thread,
            // weil er direkt in die (live an die Oberfläche gebundenen) ObservableCollections
            // der vorhandenen Ordner schreibt – eine Änderung aus einem Fremd-Thread würde
            // in WPF eine NotSupportedException auslösen. Vor dem Start wird dem Dispatcher
            // ein Render-Durchlauf gegönnt, damit das Fenster sichtbar wird.
                        var fortschritt = new FortschrittWindow { Owner = this };
                        var uhr = Stopwatch.StartNew();

                        try
                        {
                            fortschritt.Show();
                            await System.Windows.Threading.Dispatcher.Yield(
                                System.Windows.Threading.DispatcherPriority.Background);
                            AktualisiereOrdnerIntern(ordner, rekursiv);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(
                    $"Der Ordner konnte nicht aktualisiert werden: {ex.Message}",
                    "Aktualisieren fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unerwarteter Fehler beim Aktualisieren: {ex.Message}",
                    "Aktualisieren fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            finally
            {
                await WarteAufMindestdauerAsync(uhr);
                fortschritt.Close();
            }

                        // Ansicht neu aufbauen (Treffer/Filter berücksichtigen), damit entfernte/
            // hinzugefügte Dateien sichtbar werden – auch bei aktiver Gesamtsuche.
            WendeFilterAn();

                        // Ein Aktualisieren gilt IMMER als Änderung der Sammlung (auch wenn der Abgleich
            // selbst nichts geändert hat), damit beim Beenden zuverlässig die Speichern-/
            // Export-Abfrage erscheint.
            BerechneAlleOrdnerdauern();
            MarkiereGeaendert();
            AktualisiereStatus();
        }

        // Gleicht den Ordner (optional rekursiv) mit dem Datenträger ab.
        // Rückgabe: true, wenn sich dadurch etwas geändert hat.
        private bool AktualisiereOrdnerIntern(MedienOrdner ordner, bool rekursiv)
        {
            bool geaendert = AktualisiereOrdnerEbene(ordner);

            if (!rekursiv)
            {
                return geaendert;
            }

            // Physisch vorhandene Unterordner ermitteln.
            var vorhandeneUnterordner = Directory.EnumerateDirectories(ordner.Pfad)
                .OrderBy(Pfad => Pfad, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var vorhandenePfade = vorhandeneUnterordner
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Unterordner, die physisch nicht mehr existieren, aus dem Baum entfernen.
            for (int i = ordner.Unterordner.Count - 1; i >= 0; i--)
            {
                MedienOrdner unterordner = ordner.Unterordner[i];
                if (!vorhandenePfade.Contains(unterordner.Pfad))
                {
                    ordner.Unterordner.RemoveAt(i);
                    geaendert = true;
                }
            }

            foreach (string unterordnerPfad in vorhandeneUnterordner)
            {
                MedienOrdner? vorhandener = ordner.Unterordner.FirstOrDefault(
                    Unterordner => string.Equals(Unterordner.Pfad, unterordnerPfad, StringComparison.OrdinalIgnoreCase));
                if (vorhandener is null)
                {
                    MedienOrdner neuerUnterordner = ScanneOrdner(
                        unterordnerPfad,
                        ordner,
                        ordner.Dateityp,
                        ordner.UnterordnerEinbeziehen,
                        ordner.BenutzerdefinierterFilter);
                    ordner.Unterordner.Add(neuerUnterordner);
                    geaendert = true;
                }
                else if (AktualisiereOrdnerIntern(vorhandener, rekursiv: true))
                {
                    geaendert = true;
                }
            }

            return geaendert;
        }

        // Gleicht die Dateien einer einzelnen Ordnerebene ab (neu lesen, entfernen, hinzufügen).
        private bool AktualisiereOrdnerEbene(MedienOrdner ordner)
        {
            bool geaendert = false;

            // Aktuelle Dateien des Ordners nach Name (case-insensitive) indizieren.
            var vorhandeneDateien = new Dictionary<string, AudioDatei>(StringComparer.OrdinalIgnoreCase);
            foreach (AudioDatei datei in ordner.AudioDateien)
            {
                vorhandeneDateien[datei.Dateiname] = datei;
            }

            var gefundeneDateinamen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dateipfad in Directory.EnumerateFiles(ordner.Pfad))
            {
                string endung = Path.GetExtension(dateipfad);
                if (!SollDateiEingelesenWerden(endung, ordner.Dateityp, ordner.BenutzerdefinierterFilter))
                {
                    continue;
                }

                string dateiname = Path.GetFileName(dateipfad);
                gefundeneDateinamen.Add(dateiname);

                try
                {
                    FileInfo info = new(dateipfad);

                    if (vorhandeneDateien.TryGetValue(dateiname, out AudioDatei? bestehende))
                    {
                        // Datei existiert weiterhin: ggf. geänderte Basisinfos und Metadaten neu lesen.
                        if (bestehende.GroesseBytes != info.Length
                            || bestehende.Aenderungsdatum != info.LastWriteTime)
                        {
                            bestehende.GroesseBytes = info.Length;
                            bestehende.Aenderungsdatum = info.LastWriteTime;
                            geaendert = true;
                        }

                        bestehende.Pfad = info.FullName;
                        LeseMetadaten(bestehende);
                    }
                    else
                    {
                        var neueDatei = new AudioDatei
                        {
                            Dateiname = info.Name,
                            Pfad = info.FullName,
                            GroesseBytes = info.Length,
                            Aenderungsdatum = info.LastWriteTime,
                            Medientyp = IstVideo(info.Extension)
                                ? "Video"
                                : IstAudio(info.Extension) ? "Audio" : "Sonstige"
                        };
                        LeseMetadaten(neueDatei);
                        ordner.AudioDateien.Add(neueDatei);
                        geaendert = true;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

                        // Dateien, die physisch nicht mehr vorhanden sind, aus dem Ordner entfernen.
            for (int i = ordner.AudioDateien.Count - 1; i >= 0; i--)
            {
                AudioDatei datei = ordner.AudioDateien[i];
                if (!gefundeneDateinamen.Contains(datei.Dateiname))
                {
                    ordner.AudioDateien.RemoveAt(i);
                    geaendert = true;
                }
            }

            // Nach dem Abgleich in natürlicher Reihenfolge umsortieren, damit neu hinzugefügte
            // Dateien nicht hinten angehängt, sondern alphabetisch einsortiert werden – und diese
            // Reihenfolge so in die .mcd gespeichert wird.
            if (SortiereAudioDateienListe(ordner.AudioDateien))
            {
                geaendert = true;
            }

            return geaendert;
        }

        // Sortiert eine ObservableCollection<AudioDatei> in-place nach Dateiname (natürliche
        // Reihenfolge, Sonderzeichen < Ziffern < Buchstaben). Rückgabe true, wenn dabei etwas
        // verschoben wurde.
        private static bool SortiereAudioDateienListe(ObservableCollection<AudioDatei> liste)
        {
            List<AudioDatei> sortiert = liste
                .OrderBy(Datei => Datei.Dateiname, NatuerlicherDateiComparer.Instanz)
                .ToList();

            bool verschoben = false;
            for (int i = 0; i < sortiert.Count; i++)
            {
                AudioDatei datei = sortiert[i];
                int aktuellerIndex = liste.IndexOf(datei);
                if (aktuellerIndex != i)
                {
                    liste.Move(aktuellerIndex, i);
                    verschoben = true;
                }
            }

            return verschoben;
        }

        private void EintragLoeschen_Click(object sender, RoutedEventArgs e)
        {
            MedienOrdner? ordner = ErmittleKontextOrdner(sender);
            if (ordner is null)
            {
                return;
            }

            MessageBoxResult bestaetigung = MessageBox.Show(
                $"Soll „{ordner.Name}“ mit allen Unterordnern nur aus der Sammlung entfernt werden?",
                "Eintrag löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (bestaetigung != MessageBoxResult.Yes)
            {
                return;
            }

                                                EntferneOrdner(ordner);
            BerechneAlleOrdnerdauern();
            MarkiereGeaendert();
            Dateiliste.ItemsSource = null;
            AktualisiereStatus();
        }

                private void EntferneOrdner(MedienOrdner ordner)
        {
            if (ordner.Parent is MedienOrdner parent)
            {
                parent.Unterordner.Remove(ordner);
            }
            else
            {
                sammlung.Stammordner.Remove(ordner);
            }
        }

        private void VonMediumLoeschen_Click(object sender, RoutedEventArgs e)
        {
            MedienOrdner? ordner = ErmittleKontextOrdner(sender);
            if (ordner is null)
            {
                return;
            }

            MessageBoxResult loeschart = MessageBox.Show(
                $"Soll „{ordner.Name}“ mit allen Unterordnern und Dateien vom Medium gelöscht werden?\n\n"
                + "Ja = Papierkorb\nNein = endgültig löschen\nAbbrechen = keine Aktion",
                "Vom Medium löschen",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel);

            if (loeschart == MessageBoxResult.Cancel)
            {
                return;
            }

            try
            {
                if (Directory.Exists(ordner.Pfad))
                {
                    if (loeschart == MessageBoxResult.Yes)
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            ordner.Pfad,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        Directory.Delete(ordner.Pfad, true);
                    }
                }

                                EntferneOrdner(ordner);
            BerechneAlleOrdnerdauern();
            MarkiereGeaendert();
            Dateiliste.ItemsSource = null;
            AktualisiereStatus();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(
                    $"Der Ordner konnte nicht gelöscht werden: {ex.Message}",
                    "Löschen fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

                private void OrdnerEigenschaften_Click(object sender, RoutedEventArgs e)
        {
            MedienOrdner? ordner = ErmittleKontextOrdner(sender);
            if (ordner is null)
            {
                return;
            }

            List<AudioDatei> dateien = AlleDateien(ordner).ToList();
            long groesse = dateien.Sum(Datei => Datei.GroesseBytes);
            int ordneranzahl = ZaehleOrdner(ordner);

            MessageBox.Show(
                $"Pfad: {ordner.Pfad}\n"
                + $"Dateien: {dateien.Count:N0}\n"
                + $"Unterordner: {ordneranzahl:N0}\n"
                + $"Größe: {FormatiereGroesse(groesse)}",
                $"Eigenschaften: {ordner.Name}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static void AktualisierePfade(MedienOrdner ordner, string alterPfad, string neuerPfad)
        {
            ordner.Pfad = neuerPfad;
            foreach (AudioDatei datei in ordner.AudioDateien)
            {
                datei.Pfad = Path.Combine(neuerPfad, datei.Dateiname);
            }

            foreach (MedienOrdner unterordner in ordner.Unterordner)
            {
                string unterordnerPfad = Path.Combine(neuerPfad, unterordner.Name);
                AktualisierePfade(unterordner, Path.Combine(alterPfad, unterordner.Name), unterordnerPfad);
            }
        }

        private static int ZaehleOrdner(MedienOrdner ordner) => ordner.Unterordner.Count
            + ordner.Unterordner.Sum(ZaehleOrdner);

        private static string FormatiereGroesse(long bytes)
        {
            string[] einheiten = { "Bytes", "KB", "MB", "GB", "TB" };
            double groesse = bytes;
            int einheit = 0;
            while (groesse >= 1024 && einheit < einheiten.Length - 1)
            {
                groesse /= 1024;
                einheit++;
            }

            return $"{groesse:0.##} {einheiten[einheit]}";
        }

                                private void AktualisiereStatus(int? trefferAnzahl = null, bool sammlungsSuche = false)
        {
            string basis = $"Bereit. {sammlung.Stammordner.Count} Stammordner, "
                + $"{sammlung.Stammordner.Sum(ZaehleDateien)} Dateien geladen.";

            if (sammlungsSuche)
            {
                basis += $" Gesamtsuche aktiv: {trefferAnzahl ?? 0} Treffer.";
            }
            else if (IstFilterAktiv() && Dateiliste.ItemsSource is not null)
            {
                var view = CollectionViewSource.GetDefaultView(Dateiliste.ItemsSource);
                if (view is not null)
                {
                    int sichtbar = view.Cast<object>().Count();
                    int gesamt = aktuellAngezeigterOrdner?.AudioDateien.Count ?? sichtbar;
                    basis += $" Filter aktiv: {sichtbar} von {gesamt} angezeigt.";
                }
            }

                        StatusText.Text = basis;
            AktualisiereOrdnerSortierenButton();
            AktualisiereDaueranzeige();
        }

        // Zeigt in der Statusleiste die Gesamtdauer des ausgewählten Ordners (nur dessen
        // eigene Dateien, ohne Unterordner) und der ganzen Sammlung (rekursiv).
        // Greift auf den zwischengespeicherten Wert des Ordners bzw. eine einmalige Summe zu.
        private void AktualisiereDaueranzeige()
        {
            if (OrdnerDauerText is null || SammlungDauerText is null)
            {
                return;
            }

            long ordnerSekunden = aktuellAngezeigterOrdner?.GesamtdauerSekunden ?? 0;
            OrdnerDauerText.Text = "Ordner: " + DauerFormat.Formatiere(ordnerSekunden);

            long sammlungSekunden = 0;
            foreach (MedienOrdner stammordner in sammlung.Stammordner)
            {
                sammlungSekunden += SummiereDauerRekursiv(stammordner);
            }

            SammlungDauerText.Text = "Sammlung: " + DauerFormat.Formatiere(sammlungSekunden);
        }

                // Summe der Dauer aller Dateien im Ordner inkl. Unterordner. Nutzt die gecachten
                // Ordnerdauern, sodass nur addiert wird (kein Metadaten-Zugriff).
                private static long SummiereDauerRekursiv(MedienOrdner ordner)
                {
                    long summe = ordner.GesamtdauerSekunden;
                    foreach (MedienOrdner unterordner in ordner.Unterordner)
                    {
                        summe += SummiereDauerRekursiv(unterordner);
                    }

                    return summe;
                }

                // Summiert die Dauer NUR der eigenen Dateien eines Ordners (ohne Unterordner) und
                // speichert sie in GesamtdauerSekunden ([XmlIgnore], nicht in der .mcd).
                private static void AktualisiereOrdnerDauer(MedienOrdner ordner)
                {
                    long summe = 0;
                    foreach (AudioDatei datei in ordner.AudioDateien)
                    {
                        summe += datei.DauerSekunden;
                    }

                    ordner.GesamtdauerSekunden = summe;
                }

                // Berechnet die Gesamtdauer jedes Ordners (nur dessen eigene Dateien) und legt sie
                // im Cache des Ordners ab. Rekursiv über alle Stammordner. Einmalig nach Laden/
                // Einlesen/Import und nach Strukturänderungen aufrufen.
                private void BerechneAlleOrdnerdauern()
                {
                    foreach (MedienOrdner stammordner in sammlung.Stammordner)
                    {
                        BerechneOrdnerdauerRekursiv(stammordner);
                    }
                }

                private static void BerechneOrdnerdauerRekursiv(MedienOrdner ordner)
                {
                    AktualisiereOrdnerDauer(ordner);
                    foreach (MedienOrdner unterordner in ordner.Unterordner)
                    {
                        BerechneOrdnerdauerRekursiv(unterordner);
                    }
                }

                private void GanzeSammlung_Changed(object sender, RoutedEventArgs e)
                {
                    WendeFilterAn();
                }

                private void Filter_TextChanged(object sender, TextChangedEventArgs e)
                {
                    WendeFilterAn();
                }

                private void FilterLoeschen_Click(object sender, RoutedEventArgs e)
                {
                    FilterDateinameTextBox.Text = string.Empty;
                    FilterTitelTextBox.Text = string.Empty;
                    FilterArtistTextBox.Text = string.Empty;
                    FilterAlbumTextBox.Text = string.Empty;
                }

                // True, wenn in mindestens einem der vier Filterfelder etwas steht.
                private bool IstFilterAktiv()
                {
                    return !string.IsNullOrWhiteSpace(FilterDateinameTextBox?.Text)
                        || !string.IsNullOrWhiteSpace(FilterTitelTextBox?.Text)
                        || !string.IsNullOrWhiteSpace(FilterArtistTextBox?.Text)
                        || !string.IsNullOrWhiteSpace(FilterAlbumTextBox?.Text);
                }

                // Baut aus den vier Filterfeldern ein Prädikat. null = kein Filter aktiv.
                // Leere Felder werden ignoriert; gefüllte Felder werden UND-verknüpft.
                private Func<AudioDatei, bool>? ErzeugeFilterPraedikat()
                {
                    if (!IstFilterAktiv())
                    {
                        return null;
                    }

                    Regex? dateiname = ErzeugeFilterMuster(FilterDateinameTextBox.Text);
                    Regex? titel = ErzeugeFilterMuster(FilterTitelTextBox.Text);
                    Regex? artist = ErzeugeFilterMuster(FilterArtistTextBox.Text);
                    Regex? album = ErzeugeFilterMuster(FilterAlbumTextBox.Text);

                    return datei =>
                        (dateiname is null || Passt(dateiname, datei.Dateiname))
                        && (titel is null || Passt(titel, datei.Titel))
                        && (artist is null || Passt(artist, datei.Artist))
                        && (album is null || Passt(album, datei.Album));
                }

                // Baut die angezeigten Dateien neu auf und wendet dabei Filter/Gesamtsuche an.
                // Gesamtsuche: über alle Dateien der Sammlung, sonst nur der aktuelle Ordner
                // (dort via ICollectionView-Filter, damit Markierungen/Zustand erhalten bleiben).
                private void WendeFilterAn()
                {
                    if (GanzeSammlungCheckBox?.IsChecked == true && IstFilterAktiv())
                    {
                        Func<AudioDatei, bool>? filter = ErzeugeFilterPraedikat();
                        List<AudioDatei> liste = filter is null
                            ? new List<AudioDatei>()
                            : AlleDateienDerSammlung().Where(filter).ToList();
                        Dateiliste.ItemsSource = liste;
                        AktualisiereStatus(liste.Count, sammlungsSuche: true);
                        return;
                    }

                    Dateiliste.ItemsSource = aktuellAngezeigterOrdner?.AudioDateien;
                    if (Dateiliste.ItemsSource is null)
                    {
                        return;
                    }

                    if (CollectionViewSource.GetDefaultView(Dateiliste.ItemsSource) is ICollectionView view)
                    {
                        Func<AudioDatei, bool>? ordnerFilter = ErzeugeFilterPraedikat();
                        view.Filter = ordnerFilter is null
                            ? null
                            : obj => obj is AudioDatei datei && ordnerFilter(datei);
                        view.Refresh();
                        AktualisiereStatus();
                    }
                }

                private static bool Passt(Regex muster, string? wert)
                {
                    return muster.IsMatch(wert ?? string.Empty);
                }

                // Sortierung der Dateiliste: Klick auf Spaltenkopf sortiert nach der jeweiligen
                // Spalte in natürlicher Reihenfolge (Sonderzeichen < Ziffern < Buchstaben).
                private void Dateiliste_Sorting(object sender, DataGridSortingEventArgs e)
                {
                    e.Handled = true;
                    string spaltenId = e.Column.SortMemberPath;
                    if (string.IsNullOrEmpty(spaltenId))
                    {
                        spaltenId = e.Column.Header as string ?? string.Empty;
                    }

                    ListSortDirection richtung = e.Column.SortDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;

                    foreach (DataGridColumn spalte in Dateiliste.Columns)
                    {
                        spalte.SortDirection = null;
                    }

                    e.Column.SortDirection = richtung;
                    SortiereAktuelleAnsicht(spaltenId, richtung);
                }

                private void SortiereAktuelleAnsicht(string spaltenId, ListSortDirection richtung)
                {
                    if (Dateiliste.ItemsSource is null)
                    {
                        return;
                    }

                    if (CollectionViewSource.GetDefaultView(Dateiliste.ItemsSource) is ListCollectionView view)
                    {
                        Comparison<AudioDatei>? vergleich = ErzeugeVergleich(spaltenId);
                        if (vergleich is not null)
                        {
                            view.CustomSort = new AudioDateiSortComparer(vergleich, richtung);
                        }
                    }
                }

                private static Comparison<AudioDatei>? ErzeugeVergleich(string spaltenId)
                {
                    return spaltenId switch
                    {
                        "Dateiname" => (a, b) => NatuerlicheTextReihenfolge.Vergleiche(a.Dateiname, b.Dateiname),
                        "Titel" => (a, b) => NatuerlicheTextReihenfolge.Vergleiche(a.Titel, b.Titel),
                        "Artist" => (a, b) => NatuerlicheTextReihenfolge.Vergleiche(a.Artist, b.Artist),
                        "Album" => (a, b) => NatuerlicheTextReihenfolge.Vergleiche(a.Album, b.Album),
                        "Pfad" => (a, b) => NatuerlicheTextReihenfolge.Vergleiche(a.Pfad, b.Pfad),
                        "Medientyp" => (a, b) => NatuerlicheTextReihenfolge.Vergleiche(a.Medientyp, b.Medientyp),
                        "Bitrate" => (a, b) => a.Bitrate.CompareTo(b.Bitrate),
                        "Groesse" => (a, b) => a.GroesseBytes.CompareTo(b.GroesseBytes),
                        "Dauer" => (a, b) => a.DauerSekunden.CompareTo(b.DauerSekunden),
                        "Aenderungsdatum" => (a, b) => a.Aenderungsdatum.CompareTo(b.Aenderungsdatum),
                        _ => null
                    };
                }

                // Wandelt die Filtereingabe in ein Regex-Muster um. ohne * / ? = Teilstring-Suche,
                // mit * / ? = Platzhalter-Muster (jeweils komplett, case-insensitive).
                internal static Regex? ErzeugeFilterMuster(string? eingabe)
                {
                    if (string.IsNullOrWhiteSpace(eingabe))
                    {
                        return null;
                    }

                    string text = eingabe.Trim();
                    if (text.IndexOfAny(new[] { '*', '?' }) < 0)
                    {
                        return new Regex(Regex.Escape(text), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    }

                    var muster = new StringBuilder("^");
                    foreach (char c in text)
                    {
                        switch (c)
                        {
                            case '*':
                                muster.Append(".*");
                                break;
                            case '?':
                                muster.Append('.');
                                break;
                            default:
                                muster.Append(Regex.Escape(c.ToString()));
                                break;
                        }
                    }

                    muster.Append('$');
                    return new Regex(muster.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }

                private void Importieren_Click(object sender, RoutedEventArgs e)
                {
                    if (!BestaetigeUngespeicherteAenderungen())
                    {
                        return;
                    }

                    var dialog = new OpenFileDialog
                    {
                        Title = "Klangkiste-Sammlung importieren",
                        Filter = "Klangkiste-Sammlung (*.mcd)|*.mcd"
                    };

                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    try
                    {
                                                Sammlung? geladeneSammlung = LadeSammlungAusDatei(dialog.FileName);

                        if (geladeneSammlung is null || geladeneSammlung.Version != "1.0")
                        {
                            MessageBox.Show(
                                "Die Datei ist keine gültige Klangkiste-Sammlung.",
                                "Import fehlgeschlagen",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }

                        SetzeImportpfade(geladeneSammlung, Path.GetDirectoryName(dialog.FileName) ?? AppContext.BaseDirectory);
                        sammlung.Stammordner.Clear();
                        foreach (MedienOrdner stammordner in geladeneSammlung.Stammordner)
                        {
                            SetzeElternordner(stammordner, null);
                            sammlung.Stammordner.Add(stammordner);
                        }

                        offenerSammlungPfad = dialog.FileName;
                        konfiguration.LetzteSammlung = dialog.FileName;
                        SpeichereKonfiguration();
                        MarkiereUnveraendert();
                        BerechneAlleOrdnerdauern();
                        AktualisiereStatus();
                        StatusText.Text = $"Sammlung importiert: {sammlung.Stammordner.Count} Stammordner, "
                            + $"{sammlung.Stammordner.Sum(ZaehleDateien)} Dateien.";
                        WaehleErstenStammordner();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
                    {
                        MessageBox.Show(
                            $"Die Sammlung konnte nicht importiert werden: {ex.Message}",
                            "Import fehlgeschlagen",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }

                private void Exportieren_Click(object sender, RoutedEventArgs e)
                {
                    var dialog = new SaveFileDialog
                    {
                        Title = "Klangkiste-Sammlung exportieren",
                        Filter = "Klangkiste-Sammlung (*.mcd)|*.mcd",
                        DefaultExt = ".mcd",
                        FileName = "Sammlung.mcd"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        SpeichereInDatei(dialog.FileName);
                    }
                }

                // Baut eine in die XML-Datei serialisierbare Kopie der Sammlung, in der alle
                // Pfade relativ zum Ordner der .mcd gespeichert werden (portabel) und alle Texte
                // vom XML-Filter bereinigt sind. Das Originalobjekt bleibt unangetastet.
                private static Sammlung ErzeugePortableKopie(Sammlung quelle, string basisOrdner)
                {
                    var kopie = new Sammlung
                    {
                        Version = BereinigeXmlText(quelle.Version),
                        ExportDatum = BereinigeXmlText(quelle.ExportDatum),
                        IstGeaendert = quelle.IstGeaendert
                    };

                    foreach (MedienOrdner stammordner in quelle.Stammordner)
                    {
                        kopie.Stammordner.Add(KopiereOrdner(stammordner, basisOrdner));
                    }

                    return kopie;
                }

                private static MedienOrdner KopiereOrdner(MedienOrdner quelle, string basisOrdner)
                {
                    var ziel = new MedienOrdner
                    {
                        Name = BereinigeXmlText(quelle.Name),
                        Pfad = BereinigeXmlText(MacheRelativ(quelle.Pfad, basisOrdner)),
                        Dateityp = quelle.Dateityp,
                        BenutzerdefinierterFilter = BereinigeXmlText(quelle.BenutzerdefinierterFilter),
                        UnterordnerEinbeziehen = quelle.UnterordnerEinbeziehen
                    };

                                        // Dateien in natürlicher Reihenfolge (Sonderzeichen < Ziffern < Buchstaben,
                    // wie in der Dateiliste) in die portable Kopie übernehmen, damit die in der
                    // .mcd gespeicherte Reihenfolge der Anzeige-Reihenfolge entspricht.
                                        foreach (AudioDatei datei in quelle.AudioDateien
                        .OrderBy(Datei => Datei.Dateiname, NatuerlicherDateiComparer.Instanz))
                    {
                        ziel.AudioDateien.Add(new AudioDatei
                        {
                            Dateiname = BereinigeXmlText(datei.Dateiname),
                            Pfad = BereinigeXmlText(MacheRelativ(datei.Pfad, basisOrdner)),
                            GroesseBytes = datei.GroesseBytes,
                            Aenderungsdatum = datei.Aenderungsdatum,
                            Medientyp = BereinigeXmlText(datei.Medientyp),
                            DauerSekunden = datei.DauerSekunden,
                            Titel = BereinigeXmlText(datei.Titel),
                            Artist = BereinigeXmlText(datei.Artist),
                            Album = BereinigeXmlText(datei.Album),
                            Jahr = BereinigeXmlText(datei.Jahr),
                            Bitrate = datei.Bitrate
                        });
                    }

                    foreach (MedienOrdner unterordner in quelle.Unterordner)
                    {
                        ziel.Unterordner.Add(KopiereOrdner(unterordner, basisOrdner));
                    }

                    return ziel;
                }

                // Macht einen absoluten Pfad relativ zum basisOrdner (mit Vorwärts-Schrägstrichen).
                // Liegt der Pfad nicht unterhalb des basisOrdners, bleibt er unverändert.
                private static string MacheRelativ(string pfad, string basisOrdner)
                {
                    if (string.IsNullOrWhiteSpace(pfad) || string.IsNullOrWhiteSpace(basisOrdner))
                    {
                        return pfad;
                    }

                    try
                    {
                        string voll = Path.GetFullPath(pfad);
                        string basis = Path.GetFullPath(basisOrdner);
                        if (!basis.EndsWith(Path.DirectorySeparatorChar))
                        {
                            basis += Path.DirectorySeparatorChar;
                        }

                        if (voll.StartsWith(basis, StringComparison.OrdinalIgnoreCase))
                        {
                            return voll.Substring(basis.Length).Replace('\\', '/');
                        }
                    }
                    catch (Exception)
                    {
                    }

                    return pfad;
                }

                // Setzt nach dem Laden/Import alle relativen Pfade wieder auf absolute Pfade um.
                private static void SetzeImportpfade(Sammlung geladene, string basisOrdner)
                {
                    foreach (MedienOrdner stammordner in geladene.Stammordner)
                    {
                        SetzeImportpfadeOrdner(stammordner, basisOrdner);
                    }
                }

                private static void SetzeImportpfadeOrdner(MedienOrdner ordner, string basisOrdner)
                {
                    ordner.Pfad = MacheAbsolut(ordner.Pfad, basisOrdner);
                    foreach (AudioDatei datei in ordner.AudioDateien)
                    {
                        datei.Pfad = MacheAbsolut(datei.Pfad, basisOrdner);
                    }

                    foreach (MedienOrdner unterordner in ordner.Unterordner)
                    {
                        SetzeImportpfadeOrdner(unterordner, basisOrdner);
                    }
                }

                // Macht einen (möglicherweise relativen) Pfad absolut, bezogen auf basisOrdner.
                private static string MacheAbsolut(string pfad, string basisOrdner)
                {
                    if (string.IsNullOrWhiteSpace(pfad))
                    {
                        return pfad;
                    }

                    if (Path.IsPathRooted(pfad))
                    {
                        return pfad;
                    }

                    string normalisiert = pfad.Replace('/', Path.DirectorySeparatorChar);
                    return Path.GetFullPath(Path.Combine(basisOrdner, normalisiert));
                }

                // Setzt die Eltern-Rückverweise der Ordner (rekursiv). Nötig, weil [XmlIgnore]
                // nicht serialisiert wird und die Referenz nach dem Laden fehlt.
                private static void SetzeElternordner(MedienOrdner ordner, MedienOrdner? parent)
                {
                    ordner.Parent = parent;
                    foreach (MedienOrdner unterordner in ordner.Unterordner)
                    {
                        SetzeElternordner(unterordner, ordner);
                    }
                }

                // Liest einen Ordner rekursiv ein und baut daraus einen MedienOrdner-Baum.
                private static MedienOrdner ScanneOrdner(
                    string pfad,
                    MedienOrdner? parent,
                    DateitypAuswahl dateityp,
                    bool unterordnerEinbeziehen,
                    string benutzerdefinierterFilter)
                {
                    var ordner = new MedienOrdner
                    {
                        Name = Path.GetFileName(pfad.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                        Pfad = pfad,
                        Dateityp = dateityp,
                        UnterordnerEinbeziehen = unterordnerEinbeziehen,
                        BenutzerdefinierterFilter = benutzerdefinierterFilter,
                        Parent = parent
                    };

                    if (string.IsNullOrEmpty(ordner.Name))
                    {
                        ordner.Name = pfad;
                    }

                                        // Dateien in natürlicher Reihenfolge einlesen (Sonderzeichen < Ziffern <
                    // Buchstaben), damit schon die Scan-Reihenfolge der Anzeige entspricht und in
                    // der .mcd sortiert landet – nicht in der unsortierten Datenträger-Reihenfolge.
                    foreach (string dateipfad in Directory.EnumerateFiles(pfad)
                        .OrderBy(Dateipfad => Path.GetFileName(Dateipfad), NatuerlicherDateiComparer.Instanz))
                    {
                        if (!SollDateiEingelesenWerden(Path.GetExtension(dateipfad), dateityp, benutzerdefinierterFilter))
                        {
                            continue;
                        }

                        try
                        {
                            FileInfo info = new(dateipfad);
                            var datei = new AudioDatei
                            {
                                Dateiname = info.Name,
                                Pfad = info.FullName,
                                GroesseBytes = info.Length,
                                Aenderungsdatum = info.LastWriteTime,
                                Medientyp = IstVideo(info.Extension)
                                    ? "Video"
                                    : IstAudio(info.Extension) ? "Audio" : "Sonstige"
                            };
                            LeseMetadaten(datei);
                            ordner.AudioDateien.Add(datei);
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }

                    if (unterordnerEinbeziehen)
                    {
                        foreach (string unterordnerPfad in Directory.EnumerateDirectories(pfad)
                            .OrderBy(Pfad => Pfad, StringComparer.OrdinalIgnoreCase))
                        {
                            ordner.Unterordner.Add(ScanneOrdner(
                                unterordnerPfad, ordner, dateityp, unterordnerEinbeziehen, benutzerdefinierterFilter));
                        }
                    }

                    return ordner;
                }

                private static bool SollDateiEingelesenWerden(string endung, DateitypAuswahl dateityp, string benutzerdefinierterFilter)
                {
                    return dateityp switch
                    {
                        DateitypAuswahl.Audio => MedienformatDefinition.Audioendungen.Contains(endung),
                        DateitypAuswahl.Video => MedienformatDefinition.Videoendungen.Contains(endung),
                        DateitypAuswahl.AudioUndVideo => MedienformatDefinition.Audioendungen.Contains(endung)
                            || MedienformatDefinition.Videoendungen.Contains(endung),
                        DateitypAuswahl.AlleDateitypen => true,
                        DateitypAuswahl.Benutzerdefiniert => PasstZumBenutzerdefiniertenFilter(Path.GetFileName(endung), benutzerdefinierterFilter)
                            || PasstZumBenutzerdefiniertenFilter("datei" + endung, benutzerdefinierterFilter),
                        _ => true
                    };
                }

                // Prüft, ob ein Dateiname zu einem benutzerdefinierten Filter (z. B. "*.mp3;*.flac")
                // passt. Unterstützt mehrere durch ; oder , getrennte Muster sowie * und ?.
                private static bool PasstZumBenutzerdefiniertenFilter(string dateiname, string filter)
                {
                    if (string.IsNullOrWhiteSpace(filter) || filter == "*.*" || filter == "*")
                    {
                        return true;
                    }

                    string[] muster = filter.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string teil in muster)
                    {
                        string text = teil.Trim();
                        if (text.Length == 0)
                        {
                            continue;
                        }

                        var regex = new StringBuilder("^");
                        foreach (char c in text)
                        {
                            switch (c)
                            {
                                case '*':
                                    regex.Append(".*");
                                    break;
                                case '?':
                                    regex.Append('.');
                                    break;
                                default:
                                    regex.Append(Regex.Escape(c.ToString()));
                                    break;
                            }
                        }

                        regex.Append('$');
                        if (Regex.IsMatch(dateiname, regex.ToString(), RegexOptions.IgnoreCase))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                private static bool IstAudio(string endung) => MedienformatDefinition.Audioendungen.Contains(endung);

                private static bool IstVideo(string endung) => MedienformatDefinition.Videoendungen.Contains(endung);

                // Liest Titel/Artist/Album/Jahr/Dauer/Bitrate aus den Tags der Datei (TagLib).
                // Fehler werden still ignoriert – fehlende Tags dürfen das Einlesen nicht stoppen.
                private static void LeseMetadaten(AudioDatei datei)
                {
                                        try
                    {
                        using TagLibFile dateiInfo = TagLibFile.Create(datei.Pfad);
                        TagLib.Tag tag = dateiInfo.Tag;
                        if (!string.IsNullOrWhiteSpace(tag.Title))
                        {
                            datei.Titel = tag.Title;
                        }

                        if (!string.IsNullOrWhiteSpace(tag.FirstPerformer))
                        {
                            datei.Artist = tag.FirstPerformer;
                        }

                        if (!string.IsNullOrWhiteSpace(tag.Album))
                        {
                            datei.Album = tag.Album;
                        }

                        if (tag.Year != 0)
                        {
                            datei.Jahr = tag.Year.ToString();
                        }

                        if (dateiInfo.Properties is not null)
                        {
                            datei.DauerSekunden = (long)Math.Round(dateiInfo.Properties.Duration.TotalSeconds);
                            if (dateiInfo.Properties.AudioBitrate > 0)
                            {
                                datei.Bitrate = dateiInfo.Properties.AudioBitrate;
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                // Ermittelt die auszuwählenden Dateien in Wiedergabe-Reihenfolge: zuerst die in der
                // Markier-Reihenfolge gemerkten (angehakten) Dateien, danach die reine Zeilenauswahl.
                private IEnumerable<AudioDatei> ErmittleAusgewaehlteDateien()
                {
                    var ergebnis = new List<AudioDatei>();
                    var gesehen = new HashSet<AudioDatei>();

                    foreach (AudioDatei datei in markierReihenfolge)
                    {
                        if (datei.IstAngehakt && gesehen.Add(datei))
                        {
                            ergebnis.Add(datei);
                        }
                    }

                    foreach (object eintrag in Dateiliste.SelectedItems)
                    {
                        if (eintrag is AudioDatei datei && gesehen.Add(datei))
                        {
                            ergebnis.Add(datei);
                        }
                    }

                    return ergebnis;
                }

                // Setzt alle Haken der aktuell angezeigten Dateien auf den gewünschten Zustand.
                private void SetzeAlleHaken(bool angehakt)
                {
                    if (Dateiliste.ItemsSource is null)
                    {
                        return;
                    }

                    foreach (object eintrag in Dateiliste.ItemsSource)
                    {
                        if (eintrag is AudioDatei datei)
                        {
                            datei.IstAngehakt = angehakt;
                        }
                    }
                }

                // Alle Dateien der ganzen Sammlung (rekursiv) – Basis für die Gesamtsuche.
                private IEnumerable<AudioDatei> AlleDateienDerSammlung()
                {
                    foreach (MedienOrdner stammordner in sammlung.Stammordner)
                    {
                        foreach (AudioDatei datei in AlleDateien(stammordner))
                        {
                            yield return datei;
                        }
                    }
                }

                private static int ZaehleDateien(MedienOrdner ordner) =>
                    ordner.AudioDateien.Count + ordner.Unterordner.Sum(ZaehleDateien);

                private void Ordnerbaum_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
                {
                    if (e.NewValue is not MedienOrdner ordner)
                    {
                        return;
                    }

                    aktuellAngezeigterOrdner = ordner;
                    LeereMarkierReihenfolge();
                    auswahlWirdSynchronisiert = true;
                    try
                    {
                        Dateiliste.SelectedItems.Clear();
                        if (GanzeSammlungCheckBox?.IsChecked == true && IstFilterAktiv())
                        {
                            WendeFilterAn();
                        }
                        else
                        {
                            Dateiliste.ItemsSource = ordner.AudioDateien;
                            WendeFilterAn();
                        }
                    }
                    finally
                    {
                        auswahlWirdSynchronisiert = false;
                    }

                    AktualisiereStatus();
                }

                                private void Dateiliste_SelectionChanged(object sender, SelectionChangedEventArgs e)
                {
                    if (auswahlWirdSynchronisiert)
                    {
                        return;
                    }

                    foreach (object hinzugefuegt in e.AddedItems)
                    {
                        if (hinzugefuegt is AudioDatei { IstAngehakt: not false } datei)
                        {
                            MerkeMarkierung(datei);
                        }
                    }
                }

                private void Dateiliste_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
                {
                    // Im Hybrid-Modus schaltet ein Linksklick auf die Zeile direkt den Haken um.
                    if (konfiguration.Auswahlmodus == Auswahlmodus.Hybrid
                        && FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.DataContext is AudioDatei datei)
                    {
                        datei.IstAngehakt = !datei.IstAngehakt;
                        if (datei.IstAngehakt)
                        {
                            MerkeMarkierung(datei);
                        }
                        else
                        {
                            VergissMarkierung(datei);
                        }
                    }
                }

                private void Dateiliste_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
                {
                    // Rechtsklick auf eine (noch) nicht ausgewählte Zeile: diese Zeile auswählen,
                    // damit das Kontextmenü auf der richtigen Datei arbeitet.
                    DataGridRow? zeile = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
                    if (zeile is not null && !zeile.IsSelected)
                    {
                        auswahlWirdSynchronisiert = true;
                        try
                        {
                            Dateiliste.SelectedItems.Clear();
                            zeile.IsSelected = true;
                        }
                        finally
                        {
                            auswahlWirdSynchronisiert = false;
                        }
                    }
                }

                private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
                {
                    DependencyObject? aktuell = element;
                    while (aktuell is not null)
                    {
                        if (aktuell is T treffer)
                        {
                            return treffer;
                        }

                        aktuell = aktuell is Visual or System.Windows.Media.Media3D.Visual3D
                            ? VisualTreeHelper.GetParent(aktuell)
                            : LogicalTreeHelper.GetParent(aktuell);
                    }

                    return null;
                }
        }
}
 