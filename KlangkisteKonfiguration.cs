using System.Globalization;
using System.IO;
using System.Text;

namespace Klangkiste
{
    public enum DesignModus
    {
        Automatisch,
        HellerModus,
        DunklerModus
    }

    public class SpaltenEinstellung
    {
        public string Id { get; set; } = string.Empty;
        public string Ueberschrift { get; set; } = string.Empty;
        public bool Sichtbar { get; set; }
        public double Breite { get; set; }
        public int Reihenfolge { get; set; }
    }

    public sealed class KlangkisteKonfiguration
    {
        private const string Konfigurationsdatei = "config.ini";
        private readonly string dateipfad;

        public List<SpaltenEinstellung> Spalten { get; } = ErzeugeStandardeinstellungen();
        public Auswahlmodus Auswahlmodus { get; set; } = Auswahlmodus.Normal;
        public bool OrdneruebergreifendeAuswahl { get; set; }
                public PlayerEinstellung AudioPlayer { get; } = new();
        public PlayerEinstellung VideoPlayer { get; } = new();
                public bool GitternetzAnzeigen { get; set; } = true;
        public double GitternetzStaerke { get; set; } = 1;

        // Seitentrenner (GridSplitter zwischen Ordner- und Dateiliste) fixiert?
        // false = verschiebbar (Standard), true = deaktiviert/fixiert.
        public bool SeitentrennerFixiert { get; set; }

        // Design (ehemals designer.ini): nur noch Modus + Schrift.
        public DesignModus DesignModus { get; set; } = DesignModus.HellerModus;
        public string Schriftart { get; set; } = "Segoe UI";
        public double Schriftgroesse { get; set; } = 12;

                // Zuletzt geöffnete Sammlung – wird beim Start automatisch geladen.
                public string LetzteSammlung { get; set; } = string.Empty;

                // Fester Startordner für den Ordner-Auswahldialog („Hauptlaufwerk"). Der Dialog öffnet
                // immer dort, damit man nach dem Einlesen nicht in den zuletzt gewählten Ordner
                // hineinspringt, sondern auf der gewünschten Ebene bleibt.
                public string Hauptlaufwerk { get; set; } = string.Empty;

        // Fensterzustand (zuletzt maximiert?) und Position (Mehrmonitor-fähig).
        public bool FensterMaximiert { get; set; }
        public bool FensterPositionGespeichert { get; set; }
        public double FensterBreite { get; set; }
        public double FensterHoehe { get; set; }
        public double FensterLinks { get; set; }
        public double FensterOben { get; set; }
                // Gerätename des Monitors (z. B. "\\.\DISPLAY2"), um beim Start denselben Monitor zu finden.
        public string MonitorGeraet { get; set; } = string.Empty;

        // Breite der linken Ordnerbaum-Spalte (Position des Grid-Splitters zwischen
        // Ordnerliste und Dateiliste). 0 = noch nie gespeichert → Standardbreite nutzen.
        public double OrdnerbaumBreite { get; set; }

        private KlangkisteKonfiguration(string dateipfad)
        {
            this.dateipfad = dateipfad;
        }

        public static KlangkisteKonfiguration Laden()
        {
            string pfad = ErmittleDateipfad();
            var konfiguration = new KlangkisteKonfiguration(pfad);
            konfiguration.LeseDatei();
            return konfiguration;
        }

        public void Speichern()
        {
            var zeilen = new List<string>
            {
                "[Auswahl]",
                $"Modus={Auswahlmodus}",
                $"Ordneruebergreifend={OrdneruebergreifendeAuswahl.ToString().ToLowerInvariant()}",
                                $"AudioProfil={AudioPlayer.Profil}",
                $"AudioExe={AudioPlayer.ExePfad}",
                $"AudioUebergabe={AudioPlayer.Uebergabe}",
                $"AudioArgumente={AudioPlayer.Argumente}",
                $"VideoProfil={VideoPlayer.Profil}",
                $"VideoExe={VideoPlayer.ExePfad}",
                $"VideoUebergabe={VideoPlayer.Uebergabe}",
                $"VideoArgumente={VideoPlayer.Argumente}",
                string.Empty,
                                "[Darstellung]",
                                $"GitternetzAnzeigen={GitternetzAnzeigen.ToString().ToLowerInvariant()}",
                $"GitternetzStaerke={GitternetzStaerke.ToString(CultureInfo.InvariantCulture)}",
                $"SeitentrennerFixiert={SeitentrennerFixiert.ToString().ToLowerInvariant()}",
                $"DesignModus={DesignModus}",
                $"Schriftart={Schriftart}",
                $"Schriftgroesse={Schriftgroesse.ToString(CultureInfo.InvariantCulture)}",
                string.Empty,
                                "[Allgemein]",
                                $"LetzteSammlung={LetzteSammlung}",
                                $"Hauptlaufwerk={Hauptlaufwerk}",
                                string.Empty,
                "[Fenster]",
                $"Maximiert={FensterMaximiert.ToString().ToLowerInvariant()}",
                $"PositionGespeichert={FensterPositionGespeichert.ToString().ToLowerInvariant()}",
                $"Breite={FensterBreite.ToString(CultureInfo.InvariantCulture)}",
                $"Hoehe={FensterHoehe.ToString(CultureInfo.InvariantCulture)}",
                $"Links={FensterLinks.ToString(CultureInfo.InvariantCulture)}",
                                $"Oben={FensterOben.ToString(CultureInfo.InvariantCulture)}",
                $"Monitor={MonitorGeraet}",
                $"OrdnerbaumBreite={OrdnerbaumBreite.ToString(CultureInfo.InvariantCulture)}",
                string.Empty,
                "[Spalten]"
            };

            foreach (SpaltenEinstellung spalte in Spalten.OrderBy(Spalte => Spalte.Reihenfolge))
            {
                zeilen.Add($"{spalte.Id}.Sichtbar={spalte.Sichtbar.ToString().ToLowerInvariant()}");
                zeilen.Add($"{spalte.Id}.Breite={spalte.Breite.ToString(CultureInfo.InvariantCulture)}");
                zeilen.Add($"{spalte.Id}.Reihenfolge={spalte.Reihenfolge}");
            }

            File.WriteAllLines(dateipfad, zeilen, new UTF8Encoding(false));
        }

        private void LeseDatei()
        {
            if (!File.Exists(dateipfad))
            {
                return;
            }

            foreach (string zeile in File.ReadLines(dateipfad))
            {
                string[] teile = zeile.Split('=', 2);
                if (teile.Length != 2 || !teile[0].Contains('.'))
                {
                    if (teile.Length == 2)
                    {
                        if (string.Equals(teile[0], "Modus", StringComparison.OrdinalIgnoreCase)
                            && Enum.TryParse(teile[1], true, out Auswahlmodus modus))
                        {
                            Auswahlmodus = modus;
                        }
                        else if (string.Equals(teile[0], "Ordneruebergreifend", StringComparison.OrdinalIgnoreCase)
                            && bool.TryParse(teile[1], out bool ordneruebergreifend))
                        {
                            OrdneruebergreifendeAuswahl = ordneruebergreifend;
                        }
                                                else if (string.Equals(teile[0], "GitternetzAnzeigen", StringComparison.OrdinalIgnoreCase)
                                                    && bool.TryParse(teile[1], out bool gitternetzAnzeigen))
                                                {
                                                    GitternetzAnzeigen = gitternetzAnzeigen;
                                                }
                                                else if (string.Equals(teile[0], "SeitentrennerFixiert", StringComparison.OrdinalIgnoreCase)
                                                    && bool.TryParse(teile[1], out bool seitentrennerFixiert))
                                                {
                                                    SeitentrennerFixiert = seitentrennerFixiert;
                                                }
                        else if (string.Equals(teile[0], "DesignModus", StringComparison.OrdinalIgnoreCase)
                            && Enum.TryParse(teile[1], true, out DesignModus designModus))
                        {
                            DesignModus = designModus;
                        }
                        else if (string.Equals(teile[0], "Schriftart", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(teile[1]))
                        {
                            Schriftart = teile[1];
                        }
                        else if (string.Equals(teile[0], "Schriftgroesse", StringComparison.OrdinalIgnoreCase)
                            && double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double schriftgroesse))
                        {
                            Schriftgroesse = Math.Clamp(schriftgroesse, 6, 72);
                        }
                                                                                                else if (string.Equals(teile[0], "LetzteSammlung", StringComparison.OrdinalIgnoreCase))
                        {
                            LetzteSammlung = teile[1];
                        }
                        else if (string.Equals(teile[0], "Hauptlaufwerk", StringComparison.OrdinalIgnoreCase))
                        {
                            Hauptlaufwerk = teile[1];
                        }
                        else if (string.Equals(teile[0], "Maximiert", StringComparison.OrdinalIgnoreCase)
                            && bool.TryParse(teile[1], out bool maximiert))
                        {
                            FensterMaximiert = maximiert;
                        }
                        else if (string.Equals(teile[0], "PositionGespeichert", StringComparison.OrdinalIgnoreCase)
                            && bool.TryParse(teile[1], out bool positionGespeichert))
                        {
                            FensterPositionGespeichert = positionGespeichert;
                        }
                        else if (string.Equals(teile[0], "Breite", StringComparison.OrdinalIgnoreCase)
                            && double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double fensterBreite))
                        {
                            FensterBreite = fensterBreite;
                        }
                        else if (string.Equals(teile[0], "Hoehe", StringComparison.OrdinalIgnoreCase)
                            && double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double fensterHoehe))
                        {
                            FensterHoehe = fensterHoehe;
                        }
                        else if (string.Equals(teile[0], "Links", StringComparison.OrdinalIgnoreCase)
                            && double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double fensterLinks))
                        {
                            FensterLinks = fensterLinks;
                        }
                        else if (string.Equals(teile[0], "Oben", StringComparison.OrdinalIgnoreCase)
                            && double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double fensterOben))
                        {
                            FensterOben = fensterOben;
                        }
                                                else if (string.Equals(teile[0], "Monitor", StringComparison.OrdinalIgnoreCase))
                        {
                            MonitorGeraet = teile[1];
                        }
                        else if (string.Equals(teile[0], "OrdnerbaumBreite", StringComparison.OrdinalIgnoreCase)
                            && double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ordnerbaumBreite))
                        {
                            OrdnerbaumBreite = ordnerbaumBreite;
                        }
                        else if (string.Equals(teile[0], "GitternetzStaerke", StringComparison.OrdinalIgnoreCase)
                            && double.TryParse(
                                teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double gitternetzStaerke))
                        {
                            GitternetzStaerke = Math.Max(0.5, gitternetzStaerke);
                        }
                        else if (string.Equals(teile[0], "AudioProfil", StringComparison.OrdinalIgnoreCase)
                            && Enum.TryParse(teile[1], true, out PlayerProfil audioProfil))
                        {
                            AudioPlayer.Profil = audioProfil;
                        }
                                                else if (string.Equals(teile[0], "AudioExe", StringComparison.OrdinalIgnoreCase))
                        {
                            AudioPlayer.ExePfad = teile[1];
                        }
                        else if (string.Equals(teile[0], "AudioUebergabe", StringComparison.OrdinalIgnoreCase)
                            && Enum.TryParse(teile[1], true, out PlayerUebergabe audioUebergabe))
                        {
                            AudioPlayer.Uebergabe = audioUebergabe;
                        }
                        else if (string.Equals(teile[0], "AudioArgumente", StringComparison.OrdinalIgnoreCase))
                        {
                            AudioPlayer.Argumente = teile[1];
                        }
                        else if (string.Equals(teile[0], "VideoProfil", StringComparison.OrdinalIgnoreCase)
                            && Enum.TryParse(teile[1], true, out PlayerProfil videoProfil))
                        {
                            VideoPlayer.Profil = videoProfil;
                        }
                        else if (string.Equals(teile[0], "VideoExe", StringComparison.OrdinalIgnoreCase))
                        {
                            VideoPlayer.ExePfad = teile[1];
                        }
                        else if (string.Equals(teile[0], "VideoUebergabe", StringComparison.OrdinalIgnoreCase)
                            && Enum.TryParse(teile[1], true, out PlayerUebergabe videoUebergabe))
                        {
                            VideoPlayer.Uebergabe = videoUebergabe;
                        }
                        else if (string.Equals(teile[0], "VideoArgumente", StringComparison.OrdinalIgnoreCase))
                        {
                            VideoPlayer.Argumente = teile[1];
                        }
                    }

                    continue;
                }

                string[] schluessel = teile[0].Split('.', 2);
                SpaltenEinstellung? spalte = Spalten.FirstOrDefault(
                    Spalte => string.Equals(Spalte.Id, schluessel[0], StringComparison.OrdinalIgnoreCase));
                if (spalte is null)
                {
                    continue;
                }

                switch (schluessel[1].ToLowerInvariant())
                {
                    case "sichtbar" when bool.TryParse(teile[1], out bool sichtbar):
                        spalte.Sichtbar = sichtbar;
                        break;
                    case "breite" when double.TryParse(
                        teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double breite):
                        spalte.Breite = breite;
                        break;
                    case "reihenfolge" when int.TryParse(teile[1], out int reihenfolge):
                        spalte.Reihenfolge = reihenfolge;
                        break;
                }
            }
        }

        private static string ErmittleDateipfad()
        {
            string programmordner = AppContext.BaseDirectory;
            string programmdatei = Path.Combine(programmordner, Konfigurationsdatei);
            if (KannSchreiben(programmdatei))
            {
                return programmdatei;
            }

            string ausweichordner = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Klangkiste");
            Directory.CreateDirectory(ausweichordner);
            return Path.Combine(ausweichordner, Konfigurationsdatei);
        }

        private static bool KannSchreiben(string pfad)
        {
            try
            {
                using var stream = new FileStream(pfad, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                return stream.CanWrite;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static List<SpaltenEinstellung> ErzeugeStandardeinstellungen() => new()
        {
            new() { Id = "Dateiname", Ueberschrift = "Dateiname", Sichtbar = true, Breite = 220, Reihenfolge = 0 },
            new() { Id = "Titel", Ueberschrift = "Titel", Sichtbar = true, Breite = 180, Reihenfolge = 1 },
            new() { Id = "Artist", Ueberschrift = "Artist", Sichtbar = true, Breite = 160, Reihenfolge = 2 },
            new() { Id = "Album", Ueberschrift = "Album", Sichtbar = true, Breite = 160, Reihenfolge = 3 },
            new() { Id = "Bitrate", Ueberschrift = "Bitrate", Sichtbar = true, Breite = 90, Reihenfolge = 4 },
            new() { Id = "Groesse", Ueberschrift = "Dateigröße", Sichtbar = true, Breite = 110, Reihenfolge = 5 },
            new() { Id = "Dauer", Ueberschrift = "Dauer", Sichtbar = true, Breite = 100, Reihenfolge = 6 },
            new() { Id = "Pfad", Ueberschrift = "Dateipfad", Sichtbar = false, Breite = 300, Reihenfolge = 7 },
            new() { Id = "Aenderungsdatum", Ueberschrift = "Änderungsdatum", Sichtbar = false, Breite = 150, Reihenfolge = 8 },
            new() { Id = "Medientyp", Ueberschrift = "Medientyp", Sichtbar = false, Breite = 90, Reihenfolge = 9 }
        };
    }
}
