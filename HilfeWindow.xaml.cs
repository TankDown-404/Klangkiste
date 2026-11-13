using System.Windows;
using System.Windows.Controls;

namespace Klangkiste
{
    public partial class HilfeWindow : Window
    {
        private readonly IReadOnlyList<HilfeThema> alleThemen;

        public HilfeWindow()
        {
            InitializeComponent();
            DesignAnwendung.WendeAn(this);
            alleThemen = HilfeInhalte.ErzeugeThemen();
            ThemenListe.ItemsSource = alleThemen;
            if (alleThemen.Count > 0)
            {
                ThemenListe.SelectedIndex = 0;
            }
        }

        private void ThemenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemenListe.SelectedItem is HilfeThema thema)
            {
                InhaltTextBlock.Text = thema.Inhalt;
            }
        }

        private void SucheTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string suchtext = SucheTextBox.Text.Trim();
            IReadOnlyList<HilfeThema> gefilterteThemen = string.IsNullOrWhiteSpace(suchtext)
                ? alleThemen
                : alleThemen
                    .Where(Thema => Thema.Titel.Contains(suchtext, StringComparison.OrdinalIgnoreCase)
                        || Thema.Inhalt.Contains(suchtext, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            ThemenListe.ItemsSource = gefilterteThemen;
            if (gefilterteThemen.Count > 0)
            {
                ThemenListe.SelectedIndex = 0;
            }
            else
            {
                InhaltTextBlock.Text = "Keine passenden Hilfethemen gefunden.";
            }
        }
    }
}
