using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace Klangkiste
{
    // Das ist der Container für die gesamte .mcd-Datenbank
    [XmlRoot("KlangkisteCollection")]
    public class Sammlung
    {
        [XmlAttribute("Version")]
        public string Version { get; set; } = "1.0";

        [XmlAttribute("ExportDatum")]
        public string ExportDatum { get; set; } = DateTime.Now.ToString("O");

        [XmlIgnore]
        public int AnzahlDateien => AudioDateien.Count;

        // Die eigentliche Liste mit allen Songs
        [XmlIgnore]
        public ObservableCollection<AudioDatei> AudioDateien { get; } = new();

        public ObservableCollection<MedienOrdner> Stammordner { get; } = new();

        // Kennzeichnet, ob seit dem letzten Laden/Speichern Änderungen vorgenommen wurden.
        [XmlIgnore]
        public bool IstGeaendert { get; set; }
    }
}

