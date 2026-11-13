using System.Text;

namespace Klangkiste
{
    // Formatiert Sekunden als lesbare Gesamtdauer: „Tage, Stunden, Minuten, Sekunden".
    // Es werden nur die relevanten (führenden) Einheiten angezeigt; Sekunden werden
    // immer angezeigt, sofern die Dauer > 0 ist. Bei 0 wird „-" geliefert.
    internal static class DauerFormat
    {
        public static string Formatiere(long gesamtSekunden)
        {
            if (gesamtSekunden <= 0)
            {
                return "-";
            }

            long tage = gesamtSekunden / 86400;
            long stunden = gesamtSekunden % 86400 / 3600;
            long minuten = gesamtSekunden % 3600 / 60;
            long sekunden = gesamtSekunden % 60;

            var teile = new List<string>(4);

            if (tage > 0)
            {
                teile.Add($"{tage} {(tage == 1 ? "Tag" : "Tage")}");
                teile.Add($"{stunden} Std.");
                teile.Add($"{minuten} Min.");
                teile.Add($"{sekunden} Sek.");
            }
            else if (stunden > 0)
            {
                teile.Add($"{stunden} Std.");
                teile.Add($"{minuten} Min.");
                teile.Add($"{sekunden} Sek.");
            }
            else if (minuten > 0)
            {
                teile.Add($"{minuten} Min.");
                teile.Add($"{sekunden} Sek.");
            }
            else
            {
                teile.Add($"{sekunden} Sek.");
            }

            return string.Join(", ", teile);
        }
    }
}
