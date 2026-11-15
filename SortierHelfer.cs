using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Klangkiste
{
    // Stellt die Zeichenreihenfolge für natürliche Textvergleiche der Dateiliste bereit.
    // Gewünschte Reihenfolge: Sonderzeichen (_ ( ) [ ] { } …) VOR Ziffern (0-9),
    // und Ziffern VOR Buchstaben (A-Z / a-z). Groß-/Kleinschreibung wird ignoriert.
    internal static class NatuerlicheTextReihenfolge
    {
        // Ordnet ein Zeichen einer Gruppe zu (kleiner Wert = weiter oben).
        private static int GruppenRang(char c)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                if (char.IsDigit(c))
                {
                    return 1; // Ziffern 0-9
                }

                if (char.IsLetter(c))
                {
                    return 2; // Buchstaben A-Z
                }

                return 0; // Leerraum wie ein Sonderzeichen behandeln
            }

            return 0; // Sonderzeichen (_, (, ), [, ], {, }, -, ., etc.)
        }

        // Vergleicht zwei Zeichen nach der gewünschten Gruppenordnung; innerhalb einer
        // Gruppe alphabetisch (ignoriert Groß-/Kleinschreibung).
        private static int VergleicheZeichen(char a, char b)
        {
            int rangA = GruppenRang(a);
            int rangB = GruppenRang(b);
            if (rangA != rangB)
            {
                return rangA.CompareTo(rangB);
            }

            char normA = char.ToUpperInvariant(a);
            char normB = char.ToUpperInvariant(b);
            return normA.CompareTo(normB);
        }

        // Vergleicht zwei Texte nach der gewünschten Reihenfolge:
        // Sonderzeichen < Ziffern < Buchstaben. Bei Gleichheit im Muster wird das
        // kürzere Wort zuerst einsortiert; danach entscheidet die normale
        // (culture-invariante) Reihenfolge als stabile Tiebraker-Regel.
        public static int Vergleiche(string? links, string? rechts)
        {
            links ??= string.Empty;
            rechts ??= string.Empty;

            int laenge = Math.Min(links.Length, rechts.Length);
            for (int i = 0; i < laenge; i++)
            {
                int ergebnis = VergleicheZeichen(links[i], rechts[i]);
                if (ergebnis != 0)
                {
                    return ergebnis;
                }
            }

            if (links.Length != rechts.Length)
            {
                return links.Length.CompareTo(rechts.Length);
            }

            // Völlig gleich in der Musterordnung: als Tiebraker die Kultur-sichere
            // Reihenfolge, damit z. B. "Ö" und "O" stabil bleiben.
            return string.Compare(links, rechts, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    // IComparer<string> für Dateinamen in natürlicher Reihenfolge. Wird zum Sortieren
    // beim Einlesen/Speichern genutzt (OrderBy(..., Comparer)), damit die Reihenfolge in
    // der .mcd der Anzeige entspricht.
    internal sealed class NatuerlicherDateiComparer : IComparer<string>
    {
        public static readonly NatuerlicherDateiComparer Instanz = new();

        public int Compare(string? x, string? y)
        {
            return NatuerlicheTextReihenfolge.Vergleiche(x, y);
        }
    }

    // IComparer-Adapter, der eine Comparison<AudioDatei> mit der gewählten Richtung
    // auf ein ListCollectionView.CustomSort anwendet.
    internal sealed class AudioDateiSortComparer : IComparer
    {
        private readonly Comparison<AudioDatei> _vergleich;
        private readonly int _richtung;

        public AudioDateiSortComparer(Comparison<AudioDatei> vergleich, ListSortDirection richtung)
        {
            _vergleich = vergleich;
            _richtung = richtung == ListSortDirection.Descending ? -1 : 1;
        }

        public int Compare(object? x, object? y)
        {
            AudioDatei? links = x as AudioDatei;
            AudioDatei? rechts = y as AudioDatei;

            if (links is null || rechts is null)
            {
                return 0;
            }

            return _vergleich(links, rechts) * _richtung;
        }
    }
}
