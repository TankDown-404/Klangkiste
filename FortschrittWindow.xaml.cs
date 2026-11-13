using System.Windows;

namespace Klangkiste
{
    /// <summary>
    /// Kleines Modell-Fenster, das während des Einlesens/Aktualisierens angezeigt wird.
    /// Zeigt nur einen Statustext und eine unbestimmte Fortschrittsleiste. Der Aufrufer
    /// öffnet es vor dem Hintergrund-Scan und schließt es danach wieder.
    /// </summary>
    public partial class FortschrittWindow : Window
    {
        public FortschrittWindow()
        {
            InitializeComponent();
            DesignAnwendung.WendeAn(this);
        }

        /// <summary>
        /// Setzt den angezeigten Statustext (optional).
        /// </summary>
        public void SetzeMeldung(string meldung)
        {
            MeldungTextBlock.Text = meldung;
        }
    }
}
