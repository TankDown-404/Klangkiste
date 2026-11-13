using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Klangkiste
{
    public partial class OrdnerAuswahlDialog : Window
    {
        // Sentinel-Einträge in der Unterordner-Liste.
        private const string AufwaertsEintrag = "..";
        private static readonly string AufwaertsAnzeige = "..  (eine Ebene höher)";

        /// <summary>Die zum Einlesen vorgemerkten Ordner (mindestens einer nach „Einlesen").</summary>
        public List<string> Ordnerpfade { get; } = new();
        public DateitypAuswahl Dateityp { get; private set; } = DateitypAuswahl.AudioUndVideo;
        public string BenutzerdefinierterFilter { get; private set; } = "*.*";
        public bool UnterordnerEinbeziehen { get; private set; } = true;

        /// <summary>Vom Nutzer gewählter fester Startordner („Hauptlaufwerk"); leer = keiner.</summary>
        public string Hauptlaufwerk { get; private set; } = string.Empty;

        // Aktuell im Browser geöffneter Ordner.
        private string aktuellerOrdner = string.Empty;

                // Bereits in der Sammlung vorhandene Ordnerpfade (rekursiv). Beim Hinzufügen werden
        // sie übersprungen, sofern die Checkbox „Bereits eingelesene Ordner trotzdem zulassen"
        // nicht angehakt ist.
        private readonly HashSet<string> bereitsEingelesen;

        public OrdnerAuswahlDialog(string? hauptlaufwerk, IEnumerable<string>? bereitsEingeleseneOrdner = null)
        {
            InitializeComponent();
            DesignAnwendung.WendeAn(this);

            bereitsEingelesen = new HashSet<string>(
                (bereitsEingeleseneOrdner ?? Enumerable.Empty<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(NormalisierePfad),
                StringComparer.OrdinalIgnoreCase);

            Hauptlaufwerk = hauptlaufwerk ?? string.Empty;

            LadeLaufwerke();

            // Startordner bestimmen: gesetztes Hauptlaufwerk, sonst erstes Laufwerk.
            string start = Hauptlaufwerk;
            if (string.IsNullOrWhiteSpace(start) || !Directory.Exists(start))
            {
                start = LaufwerkComboBox.Items.Cast<LaufwerkEintrag>().FirstOrDefault()?.Pfad ?? string.Empty;
            }

            NavigiereZu(start);
        }

        // Vereinheitlicht einen Pfad für den Vergleich (ohne abschließenden Trenner).
        private static string NormalisierePfad(string pfad)
        {
            return pfad.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        // Füllt die Laufwerksauswahl mit allen bereiten Laufwerken (inkl. Name/Bezeichnung).
        private void LadeLaufwerke()
        {
            var eintraege = new List<LaufwerkEintrag>();
            foreach (DriveInfo laufwerk in DriveInfo.GetDrives())
            {
                try
                {
                    if (!laufwerk.IsReady)
                    {
                        continue;
                    }

                    string bezeichnung = string.IsNullOrWhiteSpace(laufwerk.VolumeLabel)
                        ? laufwerk.Name
                        : $"{laufwerk.Name}  ({laufwerk.VolumeLabel})";
                    eintraege.Add(new LaufwerkEintrag(laufwerk.Name, bezeichnung));
                }
                catch (Exception)
                {
                    // Nicht erreichbare Laufwerke überspringen.
                }
            }

            LaufwerkComboBox.ItemsSource = eintraege;
        }

        // Wechselt in den angegebenen Ordner, sofern er existiert. Aktualisiert Pfadfeld,
        // Laufwerksauswahl und die Liste der Unterordner.
        private void NavigiereZu(string pfad)
        {
            if (string.IsNullOrWhiteSpace(pfad) || !Directory.Exists(pfad))
            {
                return;
            }

            aktuellerOrdner = Path.GetFullPath(pfad).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Laufwerkswurzel nicht beschneiden (z. B. "D:\").
            if (aktuellerOrdner.Length == 2 && aktuellerOrdner[1] == ':')
            {
                aktuellerOrdner += Path.DirectorySeparatorChar;
            }

            PfadTextBox.Text = aktuellerOrdner;

            // Laufwerksauswahl auf das passende Laufwerk setzen (ohne erneute Navigation).
            LaufwerkEintrag? laufwerk = LaufwerkComboBox.Items.Cast<LaufwerkEintrag>()
                .FirstOrDefault(l => aktuellerOrdner.StartsWith(l.Pfad, StringComparison.OrdinalIgnoreCase));
            if (laufwerk is not null && !ReferenceEquals(LaufwerkComboBox.SelectedItem, laufwerk))
            {
                LaufwerkComboBox.SelectedItem = laufwerk;
            }

            LadeUnterordner();
        }

        // Listet die Unterordner des aktuellen Ordners auf (erster Eintrag = ".." zum Hochgehen,
        // sofern ein Elternordner existiert).
        private void LadeUnterordner()
        {
            var eintraege = new List<string>();

            string? eltern = Path.GetDirectoryName(
                aktuellerOrdner.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrEmpty(eltern))
            {
                eintraege.Add(AufwaertsAnzeige);
            }

            try
            {
                foreach (string ordner in Directory.GetDirectories(aktuellerOrdner)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    eintraege.Add(ordner);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nicht lesbare Ordner: einfach leer lassen.
            }

            UnterordnerListBox.ItemsSource = eintraege;
        }

        private void Dateityp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BenutzerdefinierterFilterTextBox is not null)
            {
                BenutzerdefinierterFilterTextBox.IsEnabled = DateitypComboBox.SelectedIndex == 4;
            }
        }

        private void Laufwerk_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaufwerkComboBox.SelectedItem is LaufwerkEintrag laufwerk
                && !string.Equals(aktuellerOrdner, laufwerk.Pfad, StringComparison.OrdinalIgnoreCase))
            {
                NavigiereZu(laufwerk.Pfad);
            }
        }

        private void Pfad_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigiereZu(PfadTextBox.Text.Trim());
                e.Handled = true;
            }
        }

        private void Unterordner_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UnterordnerListBox.SelectedItem is not string eintrag)
            {
                return;
            }

            if (eintrag == AufwaertsAnzeige)
            {
                GeheHoch();
                return;
            }

            NavigiereZu(eintrag);
        }

        private void Hoch_Click(object sender, RoutedEventArgs e)
        {
            GeheHoch();
        }

        private void GeheHoch()
        {
            string? eltern = Path.GetDirectoryName(
                aktuellerOrdner.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrEmpty(eltern))
            {
                NavigiereZu(eltern);
            }
        }

                // Übernimmt den aktuell geöffneten Ordner als zum Einlesen vorgemerkt.
        private void DiesenOrdnerUebernehmen_Click(object sender, RoutedEventArgs e)
        {
            int uebersprungen = UebernehmeOrdner(aktuellerOrdner);
            MeldeUebersprungene(uebersprungen);
        }

        // Übernimmt alle in der Unterordner-Liste markierten Ordner (ohne "..").
        private void MarkierteUebernehmen_Click(object sender, RoutedEventArgs e)
        {
            var markiert = UnterordnerListBox.SelectedItems.Cast<string>()
                .Where(p => p != AufwaertsAnzeige)
                .ToList();

            if (markiert.Count == 0)
            {
                MessageBox.Show(
                    "Bitte zuerst einen oder mehrere Unterordner markieren.",
                    "Keine Auswahl",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int uebersprungen = 0;
            foreach (string ordner in markiert)
            {
                uebersprungen += UebernehmeOrdner(ordner);
            }

            MeldeUebersprungene(uebersprungen);
        }

        // Fügt einen Ordner zur Liste der vorgemerkten Ordner hinzu (gegen Duplikate in der
        // Liste gesichert). Ist der Ordner bereits in der Sammlung und die Checkbox
        // „trotzdem zulassen" nicht angehakt, wird er übersprungen.
        // Rückgabe: 1, wenn wegen „bereits eingelesen" übersprungen wurde, sonst 0.
        private int UebernehmeOrdner(string ordner)
        {
            if (string.IsNullOrWhiteSpace(ordner) || !Directory.Exists(ordner))
            {
                return 0;
            }

            // Doppelte innerhalb der Liste ignorieren (unabhängig von der Checkbox).
            if (OrdnerListBox.Items.Cast<string>().Any(eintrag =>
                string.Equals(eintrag, ordner, StringComparison.OrdinalIgnoreCase)))
            {
                return 0;
            }

            bool doppelteZulassen = DoppelteZulassenCheckBox.IsChecked == true;
            if (!doppelteZulassen && bereitsEingelesen.Contains(NormalisierePfad(ordner)))
            {
                return 1;
            }

            OrdnerListBox.Items.Add(ordner);
            return 0;
        }

        // Meldet einmalig, wie viele Ordner übersprungen wurden (nur wenn > 0).
        private void MeldeUebersprungene(int anzahl)
        {
            if (anzahl <= 0)
            {
                return;
            }

            string text = anzahl == 1
                ? "Ein Ordner ist bereits eingelesen und wurde übersprungen."
                : $"{anzahl} Ordner sind bereits eingelesen und wurden übersprungen.";
            text += "\n\nZum trotzdem Hinzufügen die Option „Bereits eingelesene Ordner trotzdem zulassen“ anhaken.";

            MessageBox.Show(
                text,
                "Bereits eingelesen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OrdnerEntfernen_Click(object sender, RoutedEventArgs e)
        {
            var zuEntfernen = OrdnerListBox.SelectedItems.Cast<string>().ToList();
            foreach (string pfad in zuEntfernen)
            {
                OrdnerListBox.Items.Remove(pfad);
            }
        }

        // Übernimmt den aktuell geöffneten Ordner als festen Startordner (Hauptlaufwerk).
        private void HauptlaufwerkUebernehmen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(aktuellerOrdner) || !Directory.Exists(aktuellerOrdner))
            {
                MessageBox.Show(
                    "Bitte zuerst zu einem gültigen Ordner wechseln.",
                    "Kein Ordner",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Hauptlaufwerk = aktuellerOrdner;
            MessageBox.Show(
                $"„{Hauptlaufwerk}“ wird als Startordner verwendet.",
                "Hauptlaufwerk gesetzt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Liest die eingestellten Optionen aus und schließt den Dialog bei gültiger Auswahl.
        private void Einlesen_Click(object sender, RoutedEventArgs e)
        {
            Ordnerpfade.Clear();
            foreach (string pfad in OrdnerListBox.Items.Cast<string>())
            {
                if (Directory.Exists(pfad))
                {
                    Ordnerpfade.Add(pfad);
                }
            }

            if (Ordnerpfade.Count == 0)
            {
                MessageBox.Show(
                    "Bitte zuerst mindestens einen Ordner zur Liste hinzufügen.",
                    "Kein Ordner",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Dateityp = DateitypComboBox.SelectedIndex switch
            {
                0 => DateitypAuswahl.Audio,
                1 => DateitypAuswahl.Video,
                2 => DateitypAuswahl.AlleDateitypen,
                3 => DateitypAuswahl.AudioUndVideo,
                _ => DateitypAuswahl.Benutzerdefiniert
            };
            BenutzerdefinierterFilter = string.IsNullOrWhiteSpace(BenutzerdefinierterFilterTextBox.Text)
                ? "*.*"
                : BenutzerdefinierterFilterTextBox.Text.Trim();
            UnterordnerEinbeziehen = UnterordnerEinbeziehenCheckBox.IsChecked == true;

            DialogResult = true;
        }

        // Eintrag für die Laufwerksauswahl: Pfad (z. B. "D:\") + Anzeigename.
        private sealed record LaufwerkEintrag(string Pfad, string Anzeige)
        {
            public override string ToString() => Anzeige;
        }
    }
}
