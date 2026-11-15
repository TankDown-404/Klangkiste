using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace Klangkiste
{
    // Diese Klasse repräsentiert einen einzelnen Musikschatz
    public class AudioDatei : INotifyPropertyChanged
    {
        private bool istAngehakt;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Datei-Informationen
        public string Dateiname { get; set; } = string.Empty;
        public string Pfad { get; set; } = string.Empty;
        public long GroesseBytes { get; set; }
        public DateTime Aenderungsdatum { get; set; }
        public string Medientyp { get; set; } = string.Empty;
        public long DauerSekunden { get; set; }

        [XmlIgnore]
        public string DauerAnzeige => DauerSekunden > 0
            ? TimeSpan.FromSeconds(DauerSekunden).ToString("c")
            : string.Empty;

        [XmlIgnore]
        public string GroesseAnzeige => FormatiereGroesse(GroesseBytes);

        // Musik-Metadaten (Tags)
        public string Titel { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
                public string Album { get; set; } = string.Empty;
                public string Jahr { get; set; } = string.Empty;
        public int Bitrate { get; set; }

        [XmlIgnore]
        public bool IstAngehakt
        {
            get => istAngehakt;
            set
            {
                if (istAngehakt == value)
                {
                    return;
                }

                istAngehakt = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IstAngehakt)));
            }
        }

                [XmlIgnore]
                public bool IstAbspielbar => Medientyp is "Audio" or "Video";

                // Icon-Glyphe für die Dateiliste:
                // Audio = Note, Video = Filmklappe, Sonstige = weißes Blatt,
                // beschädigt/gelöscht = rotes X.
                [XmlIgnore]
                public string IconGlyph
                {
                    get
                    {
                        if (!IstVorhanden)
                        {
                            return "\u274C"; // rotes X (beschädigt/gelöscht)
                        }

                        return Medientyp switch
                        {
                            "Audio" => "\U0001F3B5", // 🎵 Note
                            "Video" => "\U0001F3AC", // 🎬 Filmklappe
                            _ => "\U0001F4C4"         // 📄 weißes Blatt
                        };
                    }
                }

                // True, solange die Datei auf dem Datenträger existiert.
                                [XmlIgnore]
                public bool IstVorhanden => !string.IsNullOrWhiteSpace(Pfad) && System.IO.File.Exists(Pfad);

                // ---- XML abspecken: leere / nicht gesetzte Felder NICHT schreiben ----
                // Der XmlSerializer ruft ShouldSerialize<Name>() vor dem Schreiben einer
                // Eigenschaft auf. Liefert es false, wird die Eigenschaft komplett weggelassen.
                // Beim LADEN bleiben fehlende Felder einfach auf ihrem Default (string.Empty / 0),
                // daher ist das Format abwärtskompatibel (alte .mcd laden weiterhin).
                public bool ShouldSerializeTitel() => !string.IsNullOrEmpty(Titel);
                public bool ShouldSerializeArtist() => !string.IsNullOrEmpty(Artist);
                public bool ShouldSerializeAlbum() => !string.IsNullOrEmpty(Album);
                public bool ShouldSerializeJahr() => !string.IsNullOrEmpty(Jahr);
                public bool ShouldSerializeMedientyp() => !string.IsNullOrEmpty(Medientyp);
                // Zahlenwerte: 0 = nicht gesetzt -> weglassen.
                public bool ShouldSerializeBitrate() => Bitrate != 0;
                public bool ShouldSerializeDauerSekunden() => DauerSekunden != 0;
                public bool ShouldSerializeGroesseBytes() => GroesseBytes != 0;

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
    }
}