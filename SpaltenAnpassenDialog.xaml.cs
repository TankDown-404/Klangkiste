using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Klangkiste
{
    public partial class SpaltenAnpassenDialog : Window
    {
        private readonly ObservableCollection<SpaltenEinstellung> spalten;
        private Point dragStartpunkt;
        private SpaltenEinstellung? gezogeneSpalte;

        public event EventHandler? SpaltenGeaendert;

        public SpaltenAnpassenDialog(KlangkisteKonfiguration konfiguration)
        {
            InitializeComponent();
            DesignAnwendung.WendeAn(this);
            spalten = new ObservableCollection<SpaltenEinstellung>(
                konfiguration.Spalten.OrderBy(Spalte => Spalte.Reihenfolge));
            SpaltenListe.ItemsSource = spalten;
        }

        private void SpaltenListe_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartpunkt = e.GetPosition(SpaltenListe);
            gezogeneSpalte = (e.OriginalSource as FrameworkElement)?.DataContext as SpaltenEinstellung;
        }

        private void SpaltenListe_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || gezogeneSpalte is null)
            {
                return;
            }

            Point aktuellePosition = e.GetPosition(SpaltenListe);
            if (Math.Abs(aktuellePosition.X - dragStartpunkt.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(aktuellePosition.Y - dragStartpunkt.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(SpaltenListe, gezogeneSpalte, DragDropEffects.Move);
            gezogeneSpalte = null;
        }

        private void SpaltenListe_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(SpaltenEinstellung)) is not SpaltenEinstellung gezogene
                || (e.OriginalSource as DependencyObject) is not DependencyObject quellElement)
            {
                return;
            }

            ListViewItem? zielElement = FindParent<ListViewItem>(quellElement);
            if (zielElement?.DataContext is not SpaltenEinstellung ziel || ReferenceEquals(gezogene, ziel))
            {
                return;
            }

            int altePosition = spalten.IndexOf(gezogene);
            int neuePosition = spalten.IndexOf(ziel);
            if (altePosition < 0 || neuePosition < 0)
            {
                return;
            }

            spalten.Move(altePosition, neuePosition);
            for (int index = 0; index < spalten.Count; index++)
            {
                spalten[index].Reihenfolge = index;
            }

            SpaltenGeaendert?.Invoke(this, EventArgs.Empty);
        }

        private void SpalteSichtbarkeit_Click(object sender, RoutedEventArgs e)
        {
            SpaltenGeaendert?.Invoke(this, EventArgs.Empty);
        }

        private static T? FindParent<T>(DependencyObject element) where T : DependencyObject
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(element);
            while (parent is not null)
            {
                if (parent is T matchingParent)
                {
                    return matchingParent;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }
    }
}
