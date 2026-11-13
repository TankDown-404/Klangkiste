using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.Win32;

namespace Klangkiste
{
    public partial class EinstellungenWindow : Window
    {
                private readonly KlangkisteKonfiguration konfiguration;
        private bool wirdGeladen;

        public event EventHandler? EinstellungenGeaendert;

        // Wird beim Schließen des Fensters ausgelöst, damit das Hauptfenster z. B. die
        // Spalten einmalig neu aufbauen kann (Änderungen aus dem Spalten-Dialog).
        public event EventHandler? EinstellungenGeschlossen;

        public EinstellungenWindow(KlangkisteKonfiguration konfiguration)
        {
            this.konfiguration = konfiguration
                ?? throw new ArgumentNullException(nameof(konfiguration));
            wirdGeladen = true;

                        InitializeComponent();
            DesignAnwendung.WendeAn(this);
            LadePlayer();
            LadeAuswahl();
            LadeDateiliste();
            LadeDesign();
            wirdGeladen = false;

            // Egal wie geschlossen wird (Button, X, Esc): Spalten einmalig übernehmen.
            Closing += (_, _) => EinstellungenGeschlossen?.Invoke(this, EventArgs.Empty);
        }

                private static readonly string[] Uebergabearten =
            { "Playlist (M3U)", "Dateien direkt", "Einzeldatei" };

        private void LadePlayer()
        {
            wirdGeladen = true;
            string[] profile = { "Windows-Standardanwendung", "Benutzerdefiniert", "VLC", "Winamp", "foobar2000" };
            AudioProfilComboBox.ItemsSource = profile;
            VideoProfilComboBox.ItemsSource = profile;
            AudioUebergabeComboBox.ItemsSource = Uebergabearten;
            VideoUebergabeComboBox.ItemsSource = Uebergabearten;
            AudioProfilComboBox.SelectedIndex = (int)konfiguration.AudioPlayer.Profil;
            VideoProfilComboBox.SelectedIndex = (int)konfiguration.VideoPlayer.Profil;
            AudioExeTextBox.Text = konfiguration.AudioPlayer.ExePfad;
            VideoExeTextBox.Text = konfiguration.VideoPlayer.ExePfad;
            AudioUebergabeComboBox.SelectedIndex = (int)konfiguration.AudioPlayer.Uebergabe;
            VideoUebergabeComboBox.SelectedIndex = (int)konfiguration.VideoPlayer.Uebergabe;
            AudioArgumenteTextBox.Text = LadeVorlage(konfiguration.AudioPlayer);
            VideoArgumenteTextBox.Text = LadeVorlage(konfiguration.VideoPlayer);
            AktualisierePlayerFelder();
            wirdGeladen = false;
        }

        // Zeigt das tatsächlich wirksame Template (gesetzt oder Standardvorlage).
        private static string LadeVorlage(PlayerEinstellung player) => player.ErmittleVorlage();

        private void AudioUebergabe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (wirdGeladen || AudioUebergabeComboBox.SelectedIndex < 0)
            {
                return;
            }

            konfiguration.AudioPlayer.Uebergabe = (PlayerUebergabe)AudioUebergabeComboBox.SelectedIndex;
            AudioArgumenteTextBox.Text = PlayerEinstellung.StandardVorlage(
                konfiguration.AudioPlayer.Profil, konfiguration.AudioPlayer.Uebergabe);
            SpeichereAllgemeineKonfiguration();
        }

        private void VideoUebergabe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (wirdGeladen || VideoUebergabeComboBox.SelectedIndex < 0)
            {
                return;
            }

            konfiguration.VideoPlayer.Uebergabe = (PlayerUebergabe)VideoUebergabeComboBox.SelectedIndex;
            VideoArgumenteTextBox.Text = PlayerEinstellung.StandardVorlage(
                konfiguration.VideoPlayer.Profil, konfiguration.VideoPlayer.Uebergabe);
            SpeichereAllgemeineKonfiguration();
        }

        // Aktiviert/deaktiviert die Argument-Eingabe je nach Profil (Windows-Standard = Sonderfall).
        private void AktualisierePlayerFelder()
        {
            bool audioSonderfall = konfiguration.AudioPlayer.Profil == PlayerProfil.WindowsStandard;
            AudioUebergabeComboBox.IsEnabled = !audioSonderfall;
            AudioArgumenteTextBox.IsEnabled = !audioSonderfall;

            bool videoSonderfall = konfiguration.VideoPlayer.Profil == PlayerProfil.WindowsStandard;
            VideoUebergabeComboBox.IsEnabled = !videoSonderfall;
            VideoArgumenteTextBox.IsEnabled = !videoSonderfall;
        }

        private void LadeAuswahl()
        {
            wirdGeladen = true;
            NormalModusRadioButton.IsChecked = konfiguration.Auswahlmodus == Auswahlmodus.Normal;
            KontrollkaestchenModusRadioButton.IsChecked = konfiguration.Auswahlmodus == Auswahlmodus.Kontrollkaestchen;
            HybridModusRadioButton.IsChecked = konfiguration.Auswahlmodus == Auswahlmodus.Hybrid;
            OrdneruebergreifendCheckBox.IsChecked = konfiguration.OrdneruebergreifendeAuswahl;
            OrdneruebergreifendCheckBox.IsEnabled = konfiguration.Auswahlmodus != Auswahlmodus.Normal;
            wirdGeladen = false;
        }

                                private void LadeDateiliste()
        {
            wirdGeladen = true;
                        GitternetzCheckBox.IsChecked = konfiguration.GitternetzAnzeigen;
            GitternetzstaerkeSlider.Value = konfiguration.GitternetzStaerke;
            GitternetzstaerkeWertText.Text = $"{(int)Math.Round(konfiguration.GitternetzStaerke)} px";
            SeitentrennerFixierenCheckBox.IsChecked = konfiguration.SeitentrennerFixiert;
            wirdGeladen = false;
        }

        private void LadeDesign()
        {
            wirdGeladen = true;
            // Nur noch Hell / Dunkel / Automatisch – Benutzerdefiniert gibt es nicht mehr.
            DesignModusComboBox.ItemsSource = new[] { "Automatisch", "Heller Modus", "Dunkler Modus" };
            DesignModusComboBox.SelectedIndex = konfiguration.DesignModus switch
            {
                DesignModus.HellerModus => 1,
                DesignModus.DunklerModus => 2,
                _ => 0
            };

            SchriftartComboBox.ItemsSource = Fonts.SystemFontFamilies.OrderBy(Font => Font.Source).ToList();
            SchriftartComboBox.DisplayMemberPath = nameof(FontFamily.Source);
            SchriftartComboBox.SelectedItem = Fonts.SystemFontFamilies.FirstOrDefault(Font => Font.Source == konfiguration.Schriftart)
                ?? Fonts.SystemFontFamilies.FirstOrDefault();

            SchriftgroesseComboBox.ItemsSource = Enumerable.Range(8, 25).Select(Wert => (double)Wert).ToList();
            SchriftgroesseComboBox.SelectedItem = konfiguration.Schriftgroesse;
            wirdGeladen = false;
        }

                private void AudioProfil_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!wirdGeladen && AudioProfilComboBox.SelectedIndex >= 0)
            {
                konfiguration.AudioPlayer.Profil = (PlayerProfil)AudioProfilComboBox.SelectedIndex;
                // Passendes Standard-Template für das neue Profil anzeigen.
                AudioArgumenteTextBox.Text = PlayerEinstellung.StandardVorlage(
                    konfiguration.AudioPlayer.Profil, konfiguration.AudioPlayer.Uebergabe);
                AktualisierePlayerFelder();
                SpeichereAllgemeineKonfiguration();
            }
        }

        private void VideoProfil_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!wirdGeladen && VideoProfilComboBox.SelectedIndex >= 0)
            {
                konfiguration.VideoPlayer.Profil = (PlayerProfil)VideoProfilComboBox.SelectedIndex;
                VideoArgumenteTextBox.Text = PlayerEinstellung.StandardVorlage(
                    konfiguration.VideoPlayer.Profil, konfiguration.VideoPlayer.Uebergabe);
                AktualisierePlayerFelder();
                SpeichereAllgemeineKonfiguration();
            }
        }

        private void AudioArgumente_LostFocus(object sender, RoutedEventArgs e)
        {
            if (wirdGeladen)
            {
                return;
            }

            konfiguration.AudioPlayer.Argumente = AudioArgumenteTextBox.Text.Trim();
            SpeichereAllgemeineKonfiguration();
        }

        private void VideoArgumente_LostFocus(object sender, RoutedEventArgs e)
        {
            if (wirdGeladen)
            {
                return;
            }

            konfiguration.VideoPlayer.Argumente = VideoArgumenteTextBox.Text.Trim();
            SpeichereAllgemeineKonfiguration();
        }

        private void AudioExeDurchsuchen_Click(object sender, RoutedEventArgs e) => WaehleExe(konfiguration.AudioPlayer, AudioExeTextBox);
        private void VideoExeDurchsuchen_Click(object sender, RoutedEventArgs e) => WaehleExe(konfiguration.VideoPlayer, VideoExeTextBox);

        private void WaehleExe(PlayerEinstellung player, TextBox textBox)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Ausführbare Dateien (*.exe)|*.exe",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                player.ExePfad = dialog.FileName;
                textBox.Text = dialog.FileName;
                SpeichereAllgemeineKonfiguration();
            }
        }

                private void AudioPlayerTesten_Click(object sender, RoutedEventArgs e) => TestePlayer(konfiguration.AudioPlayer);
        private void VideoPlayerTesten_Click(object sender, RoutedEventArgs e) => TestePlayer(konfiguration.VideoPlayer);

        private void TestePlayer(PlayerEinstellung player)
        {
            if (player.Profil == PlayerProfil.WindowsStandard)
            {
                MessageBox.Show("Die Windows-Standardanwendung benötigt keine eigene Programmdatei.", "Player testen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!File.Exists(player.ExePfad))
            {
                MessageBox.Show("Die ausgewählte EXE-Datei wurde nicht gefunden.", "Player testen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Optionale Testdatei: Wenn der Benutzer eine auswählt, wird sie über das
            // Template (statt nur der EXE) an den Player übergeben.
            var dialog = new OpenFileDialog
            {
                Title = $"Testdatei für {player.Profil} wählen (optional – Abbrechen startet nur die EXE)",
                Filter = "Medien (*.mp3;*.flac;*.wav;*.ogg;*.m4a;*.mp4;*.mkv;*.avi;*.webm)|*.mp3;*.flac;*.wav;*.ogg;*.m4a;*.mp4;*.mkv;*.avi;*.webm|Alle Dateien (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                // Kein Testmedium: nur die EXE starten.
                Process.Start(new ProcessStartInfo { FileName = player.ExePfad, UseShellExecute = true });
                return;
            }

                        string testdatei = dialog.FileName;
            try
            {
                // Eine temporäre Playlist (.m3u, ANSI) anlegen, die nur die Testdatei enthält –
                // gleiche Konvention wie in MainWindow (Winamp liest .m3u8 sonst leer).
                string playlist = Path.Combine(
                    Path.GetTempPath(),
                    $"Klangkiste_Test_{Guid.NewGuid():N}.m3u");
                File.WriteAllLines(playlist, new[] { "#EXTM3U", testdatei }, MainWindow.ErmittleAnsiKodierung());

                // Zentrale Startlogik nutzen (ArgumentList, UseShellExecute = false).
                Process.Start(MainWindow.ErzeugePlayerStartInfo(player, playlist, new[] { testdatei }));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                MessageBox.Show($"Der Testplayer konnte nicht gestartet werden: {ex.Message}", "Player testen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Auswahlmodus_Checked(object sender, RoutedEventArgs e)
        {
            if (wirdGeladen)
            {
                return;
            }

            Auswahlmodus neuerModus = NormalModusRadioButton.IsChecked == true
                ? Auswahlmodus.Normal
                : KontrollkaestchenModusRadioButton.IsChecked == true
                    ? Auswahlmodus.Kontrollkaestchen
                    : Auswahlmodus.Hybrid;
            konfiguration.Auswahlmodus = neuerModus;
            OrdneruebergreifendCheckBox.IsEnabled = neuerModus != Auswahlmodus.Normal;
            SpeichereAllgemeineKonfiguration();
            EinstellungenGeaendert?.Invoke(this, EventArgs.Empty);
        }

        private void Ordneruebergreifend_Click(object sender, RoutedEventArgs e)
        {
            if (wirdGeladen) return;
            konfiguration.OrdneruebergreifendeAuswahl = OrdneruebergreifendCheckBox.IsChecked == true;
            SpeichereAllgemeineKonfiguration();
        }

        private void GitternetzAnzeigen_Click(object sender, RoutedEventArgs e)
        {
            if (wirdGeladen) return;
            konfiguration.GitternetzAnzeigen = GitternetzCheckBox.IsChecked == true;
            SpeichereAllgemeineKonfiguration();
            EinstellungenGeaendert?.Invoke(this, EventArgs.Empty);
        }

                private void Gitternetzstaerke_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (wirdGeladen || konfiguration is null) return;
            // Feste Stufen 1..5 px.
            konfiguration.GitternetzStaerke = Math.Round(e.NewValue);
            if (GitternetzstaerkeWertText is not null)
            {
                GitternetzstaerkeWertText.Text = $"{(int)konfiguration.GitternetzStaerke} px";
            }
            SpeichereAllgemeineKonfiguration();
            // Sofort im Hauptfenster anwenden (live beim Ziehen).
            EinstellungenGeaendert?.Invoke(this, EventArgs.Empty);
        }

                private void SpaltenAnpassen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SpaltenAnpassenDialog(konfiguration) { Owner = this };
            dialog.ShowDialog();
            // Der Dialog verändert die Spaltenobjekte direkt. Jetzt persistieren; das
            // Anwenden auf die Dateiliste erfolgt beim Schließen der Einstellungen
            // (EinstellungenGeschlossen), damit nur einmal neu aufgebaut wird.
            SpeichereAllgemeineKonfiguration();
        }

                private void SeitentrennerFixieren_Click(object sender, RoutedEventArgs e)
        {
            if (wirdGeladen) return;
            konfiguration.SeitentrennerFixiert = SeitentrennerFixierenCheckBox.IsChecked == true;
            SpeichereAllgemeineKonfiguration();
            // Sofort im Hauptfenster anwenden.
            EinstellungenGeaendert?.Invoke(this, EventArgs.Empty);
        }

                // Modus wechseln -> sofort speichern und neu anwenden.
        private void DesignModus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (wirdGeladen || DesignModusComboBox.SelectedIndex < 0)
            {
                return;
            }

            konfiguration.DesignModus = DesignModusComboBox.SelectedIndex switch
            {
                1 => DesignModus.HellerModus,
                2 => DesignModus.DunklerModus,
                _ => DesignModus.Automatisch
            };
            SpeichereAllgemeineKonfiguration();
            DesignAnwendung.WendeAn(this);
            EinstellungenGeaendert?.Invoke(this, EventArgs.Empty);
        }

        private void Schriftart_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (wirdGeladen || SchriftartComboBox.SelectedItem is not FontFamily schriftart)
            {
                return;
            }

            konfiguration.Schriftart = schriftart.Source;
            SpeichereAllgemeineKonfiguration();
            DesignAnwendung.WendeAn(this);
            EinstellungenGeaendert?.Invoke(this, EventArgs.Empty);
        }

        private void Schriftgroesse_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (wirdGeladen || SchriftgroesseComboBox.SelectedItem is not double groesse)
            {
                return;
            }

            konfiguration.Schriftgroesse = groesse;
            SpeichereAllgemeineKonfiguration();
            DesignAnwendung.WendeAn(this);
            EinstellungenGeaendert?.Invoke(this, EventArgs.Empty);
        }

        private void SpeichereAllgemeineKonfiguration()
        {
            try { konfiguration.Speichern(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}