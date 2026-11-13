using System.Windows;
using Microsoft.VisualBasic;

namespace Klangkiste
{
    public partial class DesignKonfliktDialog : Window
    {
        public DesignKonfliktAktion Aktion { get; private set; } = DesignKonfliktAktion.Abbrechen;
        public string NeuerName { get; private set; } = string.Empty;

        public DesignKonfliktDialog(string designname)
        {
            InitializeComponent();
            DesignAnwendung.WendeAn(this);
            DesignnameTextBlock.Text = designname;
        }

        private void Ueberschreiben_Click(object sender, RoutedEventArgs e)
        {
            Aktion = DesignKonfliktAktion.Ueberschreiben;
            DialogResult = true;
        }

        private void Umbenennen_Click(object sender, RoutedEventArgs e)
        {
            string name = Interaction.InputBox("Neuer Name des Designs:", "Design umbenennen", DesignnameTextBlock.Text).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            NeuerName = name;
            Aktion = DesignKonfliktAktion.Umbenennen;
            DialogResult = true;
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            Aktion = DesignKonfliktAktion.Abbrechen;
            DialogResult = true;
        }
    }

    public enum DesignKonfliktAktion
    {
        Ueberschreiben,
        Umbenennen,
        Abbrechen
    }
}
