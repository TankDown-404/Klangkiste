using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace Klangkiste
{
    public class MedienOrdner
    {
        public string Name { get; set; } = string.Empty;
        public string Pfad { get; set; } = string.Empty;
        public DateitypAuswahl Dateityp { get; set; } = DateitypAuswahl.AudioUndVideo;
        public string BenutzerdefinierterFilter { get; set; } = "*.*";
        public bool UnterordnerEinbeziehen { get; set; } = true;
        [XmlIgnore]
        public MedienOrdner? Parent { get; set; }
        public ObservableCollection<MedienOrdner> Unterordner { get; } = new();
        public ObservableCollection<AudioDatei> AudioDateien { get; } = new();

        // Zwischengespeicherte Gesamtdauer (Sekunden) NUR dieses Ordners (ohne Unterordner).
        // Wird beim Laden/Einlesen/Import berechnet und bei Änderungen aktualisiert; dient
        // nur der schnellen Anzeige in der Statusleiste.
                [XmlIgnore]
        public long GesamtdauerSekunden { get; set; }

                // ---- XML abspecken: nur den Default-Filter weglassen ----
                // Ist der Filter der Standard "*.*", wird er nicht geschrieben; beim Laden
                // greift der Feld-Initialisierer erneut (Default "*.*"). Name/Pfad/Dateityp/
                // UnterordnerEinbeziehen werden bewusst immer geschrieben.
                public bool ShouldSerializeBenutzerdefinierterFilter() =>
                    !string.IsNullOrEmpty(BenutzerdefinierterFilter) && BenutzerdefinierterFilter != "*.*";

                public override string ToString() => Name;

                // Ordner-Icon-Glyphe: Stammordner = Festplatte, Unterordner = normaler Ordner.
                [XmlIgnore]
                public string IconGlyph => Parent is null ? "\U0001F4BD" : "\U0001F4C1";
    }
}

