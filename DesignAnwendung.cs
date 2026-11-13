using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Klangkiste
{
    /// <summary>
    /// Wendet das aktive Design (Heller/Dunkler Modus, automatisch nach Windows)
    /// auf ein Fenster an. Die Farben sind fest im Code hinterlegt (zwei Paletten),
    /// nur Schriftart und Schriftgröße kommen aus der Konfiguration.
    /// </summary>
    public static class DesignAnwendung
    {
        private static KlangkisteKonfiguration? konfiguration;

                public static void SetzeKonfiguration(KlangkisteKonfiguration neueKonfiguration)
        {
            konfiguration = neueKonfiguration;
        }

        // Fest definierte Farbpalette eines Modus.
        private sealed class Palette
        {
            public Color Fenster;
            public Color Text;
            public Color Dateiliste;
            public Color DateilisteText;
            public Color Chrome;          // Toolbar / Statusleiste
            public Color Tooltip;
            public Color Button;
            public Color ButtonText;
            public Color ButtonHover;
            public Color Highlight;       // Auswahl
            public Color HighlightText;   // Text auf Auswahl
                        public Color Akzent;          // Rahmen / Fokus (im Dunkelmodus ein Grauton)
            public Color Trenn;           // Trennlinien / Gitter
            public Color Gitternetz;      // Farbe der DataGrid-Gitternetzlinien
            public Color Menu;
            public Color MenuText;
            public Color MenuHover;
        }

                private static readonly Palette HellePalette = new()
        {
            Fenster = Hex("#F5F5F5"),
            Text = Hex("#202020"),
            Dateiliste = Hex("#FFFFFF"),
            DateilisteText = Hex("#202020"),
            Chrome = Hex("#ECECEC"),
            Tooltip = Hex("#FFFFFF"),
            Button = Hex("#E1E1E1"),
            ButtonText = Hex("#202020"),
            ButtonHover = Hex("#D4D4D4"),
            Highlight = Hex("#3399FF"),
            HighlightText = Hex("#FFFFFF"),
                        Akzent = Hex("#0078D4"),
            Trenn = Hex("#D5D5D5"),
            Gitternetz = Hex("#D5D5D5"),
            Menu = Hex("#FAFAFA"),
            MenuText = Hex("#202020"),
            MenuHover = Hex("#E6F0FA")
        };

                private static readonly Palette DunklePalette = new()
        {
                        Fenster = Hex("#1E1E1E"),
            Text = Hex("#AFAFB0"),
            Dateiliste = Hex("#252526"),
            DateilisteText = Hex("#AFAFB0"),
            Chrome = Hex("#2D2D2D"),
            Tooltip = Hex("#333333"),
            Button = Hex("#3A3A3A"),
            ButtonText = Hex("#F0F0F0"),
            ButtonHover = Hex("#474747"),
            Highlight = Hex("#4A4A4A"),
            HighlightText = Hex("#FFFFFF"),
            Akzent = Hex("#585858"),
            Trenn = Hex("#3F3F3F"),
            Gitternetz = Hex("#737373"),
            Menu = Hex("#2D2D2D"),
            MenuText = Hex("#F0F0F0"),
            MenuHover = Hex("#474747")
        };

        private static Color Hex(string farbe) => (Color)ColorConverter.ConvertFromString(farbe)!;

        private static Palette ErmittlePalette()
        {
            DesignModus modus = konfiguration?.DesignModus ?? DesignModus.HellerModus;
            if (modus == DesignModus.Automatisch)
            {
                modus = IstWindowsHell() ? DesignModus.HellerModus : DesignModus.DunklerModus;
            }

            return modus == DesignModus.DunklerModus ? DunklePalette : HellePalette;
        }

        private static bool IstWindowsHell()
        {
            try
            {
                using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? wert = key?.GetValue("AppsUseLightTheme");
                return wert is int integer && integer != 0;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static void WendeAn(Window fenster)
        {
            Palette p = ErmittlePalette();

            Brush fensterfarbe = new SolidColorBrush(p.Fenster);
            Brush textfarbe = new SolidColorBrush(p.Text);
            Brush dateilistenfarbe = new SolidColorBrush(p.Dateiliste);
            Brush chrome = new SolidColorBrush(p.Chrome);
            Brush buttonfarbe = new SolidColorBrush(p.Button);
            Brush highlight = new SolidColorBrush(p.Highlight);
            Brush highlightText = new SolidColorBrush(p.HighlightText);
                        Brush akzent = new SolidColorBrush(p.Akzent);
            Brush trenn = new SolidColorBrush(p.Trenn);

            // Schriftart/-größe bewusst NICHT global auf das Fenster anwenden – sie
            // sollen nur auf Ordner- und Dateiliste wirken (siehe WendeElementAn).
            fenster.Background = fensterfarbe;
            fenster.Foreground = textfarbe;

            // Systemfarben überschreiben, damit auch Standard-Templates (Win32-artige
            // Controls wie ScrollBar, Menu, ToolTip, Popups) den Theme-Wert verwenden.
            SetzeSystemfarben(fenster.Resources, p, fensterfarbe, textfarbe, chrome,
                buttonfarbe, highlight, highlightText, akzent, trenn);

            // Global gültige Styles für Standard-Controls mit eigenen Templates.
            SetzeControlStyles(fenster, p);

            foreach (object child in LogicalTreeHelper.GetChildren(fenster))
            {
                if (child is FrameworkElement element)
                {
                    WendeElementAn(element, p, textfarbe, dateilistenfarbe, chrome, buttonfarbe,
                        highlight, highlightText, akzent, trenn);
                }
            }
        }

        private static void SetzeSystemfarben(
            ResourceDictionary res,
            Palette p,
            Brush fenster, Brush text, Brush chrome,
            Brush button, Brush highlight, Brush highlightText, Brush akzent, Brush trenn)
        {
                        Brush menu = new SolidColorBrush(p.Menu);
            Brush menuText = new SolidColorBrush(p.MenuText);

                        void Set(object key, Brush brush)
            {
                res[key] = brush;
                // Global zusätzlich setzen, damit auch Popup-Fenster (Kontextmenüs,
                // ComboBox-Dropdowns) die Theme-Farben zuverlässig erben.
                if (Application.Current is not null)
                {
                    Application.Current.Resources[key] = brush;
                }
            }

            Set(SystemColors.WindowBrushKey, fenster);
            Set(SystemColors.WindowTextBrushKey, text);
            Set(SystemColors.ControlBrushKey, button);
            Set(SystemColors.ControlTextBrushKey, text);
            Set(SystemColors.ControlLightBrushKey, chrome);
            Set(SystemColors.ControlLightLightBrushKey, fenster);
            Set(SystemColors.ControlDarkBrushKey, trenn);
            Set(SystemColors.ControlDarkDarkBrushKey, trenn);
            Set(SystemColors.HighlightBrushKey, highlight);
            Set(SystemColors.HighlightTextBrushKey, highlightText);
            Set(SystemColors.AccentColorBrushKey, akzent);
            Set(SystemColors.MenuBrushKey, menu);
            Set(SystemColors.MenuTextBrushKey, menuText);
                        Set(SystemColors.MenuBarBrushKey, chrome);
            Set(SystemColors.InactiveSelectionHighlightBrushKey, highlight);
            Set(SystemColors.InactiveSelectionHighlightTextBrushKey, highlightText);
            Set(SystemColors.GrayTextBrushKey, new SolidColorBrush(p.Trenn));
        }

        // Styles für Controls, die ihre Farben über eigene Templates beziehen und
        // deshalb nicht zuverlässig über System-Brushes umgefärbt werden.
        private static void SetzeControlStyles(Window fenster, Palette p)
        {
            Brush menu = new SolidColorBrush(p.Menu);
            Brush menuText = new SolidColorBrush(p.MenuText);
            Brush menuHover = new SolidColorBrush(p.MenuHover);
            Brush tooltip = new SolidColorBrush(p.Tooltip);
            Brush text = new SolidColorBrush(p.Text);

            // ComboBox-Items inkl. Hover – gilt global, damit auch das Popup
            // (eigenes Fenster-Scope) die richtigen Farben erbt.
            var comboItemStil = new Style(typeof(ComboBoxItem));
            comboItemStil.Setters.Add(new Setter(Control.BackgroundProperty, menu));
            comboItemStil.Setters.Add(new Setter(Control.ForegroundProperty, menuText));
            comboItemStil.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)));
                        comboItemStil.Triggers.Add(new Trigger
            {
                Property = ComboBoxItem.IsHighlightedProperty,
                Value = true,
                Setters =
                {
                    new Setter(Control.BackgroundProperty, menuHover),
                    new Setter(Control.ForegroundProperty, menuText)
                }
            });
            fenster.Resources[typeof(ComboBoxItem)] = comboItemStil;

            // WICHTIG: Das aufgeklappte ComboBox-Popup ist ein eigenes visuelles
            // Fenster und erbt fenster.Resources NICHT zuverlässig. Deshalb den Item-
            // Style zusätzlich global registrieren, damit die Dropdown-Einträge auch
            // im Dark-Mode dunkel bleiben (statt weiß mit weißer Schrift).
            if (Application.Current is not null)
            {
                Application.Current.Resources[typeof(ComboBoxItem)] = comboItemStil;
            }

                                                // MenuItem: eigenes, schlankes Template. Das Aero2-Template zeichnet
            // Submenu-Popups mit festen (weißen) Pinseln und einer leeren Icon-Spalte.
            // Wir bauen einen flachen Eintrag mit dunklem Grund, Hover und Unterstützung
            // für Untermenüs – ohne die störende weiße Umrandung/Icon-Gutter.
            var menuItemStil = new Style(typeof(MenuItem));
            menuItemStil.Setters.Add(new Setter(Control.BackgroundProperty, menu));
            menuItemStil.Setters.Add(new Setter(Control.ForegroundProperty, menuText));
            menuItemStil.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 4, 10, 4)));
            menuItemStil.Setters.Add(new Setter(Control.TemplateProperty, ErzeugeMenuItemTemplate(p, menu, menuText, menuHover)));
            menuItemStil.Triggers.Add(new Trigger
            {
                Property = MenuItem.IsHighlightedProperty,
                Value = true,
                Setters =
                {
                    new Setter(Control.BackgroundProperty, menuHover),
                    new Setter(Control.ForegroundProperty, menuText)
                }
            });
            fenster.Resources[typeof(MenuItem)] = menuItemStil;

                        // ContextMenu: eigenes Template (dunkler Grund + Rahmen). Das System-Template
            // zeichnet sonst einen hellen Rand um das Popup.
            var contextStil = new Style(typeof(ContextMenu));
            contextStil.Setters.Add(new Setter(Control.BackgroundProperty, menu));
            contextStil.Setters.Add(new Setter(Control.ForegroundProperty, menuText));
            contextStil.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(p.Akzent)));
            contextStil.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            contextStil.Setters.Add(new Setter(Control.TemplateProperty, ErzeugeContextMenuTemplate(menu)));
            fenster.Resources[typeof(ContextMenu)] = contextStil;

            // Separator im Menü: dezente Trennfarbe statt System-Grau.
            var separatorStil = new Style(typeof(Separator));
            separatorStil.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(p.Trenn)));
            separatorStil.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(p.Trenn)));
            fenster.Resources[typeof(Separator)] = separatorStil;

            // Kontextmenü-Elemente global registrieren, damit das Popup-Fenster
            // die dunklen Farben sicher erbt (wie bei ComboBoxItem).
            if (Application.Current is not null)
            {
                Application.Current.Resources[typeof(MenuItem)] = menuItemStil;
                Application.Current.Resources[typeof(ContextMenu)] = contextStil;
                Application.Current.Resources[typeof(Separator)] = separatorStil;
            }

            // ToolTip.
            var tooltipStil = new Style(typeof(ToolTip));
            tooltipStil.Setters.Add(new Setter(Control.BackgroundProperty, tooltip));
            tooltipStil.Setters.Add(new Setter(Control.ForegroundProperty, text));
            tooltipStil.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(p.Akzent)));
            fenster.Resources[typeof(ToolTip)] = tooltipStil;
        }

                private static void WendeElementAn(
            FrameworkElement element,
            Palette p,
            Brush textfarbe,
            Brush dateilistenfarbe,
            Brush chrome,
            Brush buttonfarbe,
            Brush highlight,
            Brush highlightText,
            Brush akzent,
            Brush trenn)
        {
            Brush text = new SolidColorBrush(p.Text);
            Brush listText = new SolidColorBrush(p.DateilisteText);
                        if (element is ToolBar toolbar)
                        {
                            toolbar.Background = chrome;
                            toolbar.Foreground = textfarbe;
                        }
                        else if (element is StatusBar statusbar)
                        {
                            statusbar.Background = chrome;
                            statusbar.Foreground = textfarbe;
                        }
                                                else if (element is DataGrid datagrid)
                        {
                                                        WendeDataGridAn(datagrid, dateilistenfarbe, chrome, text, listText,
                                highlight, highlightText, akzent, trenn, new SolidColorBrush(p.Gitternetz));
                        }
                        else if (element is TextBox textBox)
                        {
                            textBox.Background = new SolidColorBrush(p.Button);
                            textBox.Foreground = text;
                            textBox.BorderBrush = akzent;
                            textBox.CaretBrush = text;
                        }
                                                else if (element is Button button)
                        {
                            WendeButtonAn(button, p, buttonfarbe, trenn);
                        }
                        else if (element is ComboBox comboBox)
                        {
                            WendeComboBoxAn(comboBox, p, buttonfarbe, akzent, text);
                        }
                        else if (element is CheckBox checkBox)
                        {
                            checkBox.Foreground = textfarbe;
                        }
                        else if (element is RadioButton radioButton)
                        {
                            radioButton.Foreground = textfarbe;
                        }
                        else if (element is ListBox listBox)
                        {
                            listBox.Background = dateilistenfarbe;
                            listBox.Foreground = listText;
                        }
                                                else if (element is TreeView treeView)
                                                {
                                                    treeView.Background = dateilistenfarbe;
                                                    treeView.Foreground = listText;

                                                    // Schriftart/-größe nur auf die Ordnerliste anwenden.
                                                    WendeListenSchriftAn(treeView);

                                                    // WICHTIG: Der in XAML gesetzte ItemContainerStyle (enthält den
                            // ContextMenu!) muss erhalten bleiben. Deshalb den vorhandenen
                            // Style als Basis übernehmen und nur Farben/Trigger ergänzen.
                            var tvItemStil = new Style(typeof(TreeViewItem));
                            if (treeView.ItemContainerStyle is not null)
                            {
                                tvItemStil.BasedOn = treeView.ItemContainerStyle;
                            }
                            tvItemStil.Setters.Add(new Setter(Control.ForegroundProperty, listText));
                            tvItemStil.Triggers.Add(new Trigger
                            {
                                Property = TreeViewItem.IsSelectedProperty,
                                Value = true,
                                Setters =
                                {
                                    new Setter(Control.BackgroundProperty, highlight),
                                    new Setter(Control.ForegroundProperty, highlightText)
                                }
                            });
                            treeView.ItemContainerStyle = tvItemStil;
                        }
                        else if (element is GroupBox groupBox)
                        {
                            groupBox.Background = new SolidColorBrush(p.Fenster);
                            groupBox.Foreground = textfarbe;
                            groupBox.BorderBrush = trenn;
                        }
                        else if (element is TabControl tabControl)
                        {
                            tabControl.Background = new SolidColorBrush(p.Fenster);
                            tabControl.Foreground = textfarbe;
                            tabControl.BorderBrush = trenn;
                        }
                                                else if (element is TabItem tabItem)
                                                {
                                                    WendeTabItemAn(tabItem, p, buttonfarbe, trenn);
                                                }
                        else if (element is ScrollViewer scrollViewer)
                        {
                            scrollViewer.Background = new SolidColorBrush(p.Fenster);
                            scrollViewer.Foreground = textfarbe;
                        }
                        else if (element is Border border)
                        {
                            border.BorderBrush = trenn;
                        }
                        else if (element is GridSplitter gridSplitter)
                        {
                            gridSplitter.Background = trenn;
                        }
                        else if (element is TextBlock textBlock)
                        {
                            textBlock.Foreground = textfarbe;
                        }
                        else if (element is Panel panel)
                        {
                            panel.Background = new SolidColorBrush(p.Fenster);
                        }

                        foreach (object child in LogicalTreeHelper.GetChildren(element))
                        {
                            if (child is FrameworkElement childElement)
                            {
                                WendeElementAn(childElement, p, textfarbe, dateilistenfarbe, chrome, buttonfarbe,
                                    highlight, highlightText, akzent, trenn);
                            }
                        }
        }

                // Styling für das DataGrid inkl. sichtbarer Auswahl-Markierung im dunklen Modus.
                private static void WendeDataGridAn(
            DataGrid datagrid,
            Brush dateilistenfarbe,
            Brush chrome,
            Brush text,
            Brush listText,
            Brush highlight,
            Brush highlightText,
            Brush akzent,
            Brush trenn,
            Brush gitternetz)
        {
                        datagrid.Background = dateilistenfarbe;
            datagrid.Foreground = listText;
            datagrid.BorderBrush = akzent;
            datagrid.RowBackground = dateilistenfarbe;
            datagrid.AlternatingRowBackground = dateilistenfarbe;
            datagrid.HorizontalGridLinesBrush = gitternetz;
            datagrid.VerticalGridLinesBrush = gitternetz;

            // Schriftart/-größe nur auf die Dateiliste anwenden.
            WendeListenSchriftAn(datagrid);

                        // WICHTIG: Den in XAML gesetzten ColumnHeaderStyle (enthält das Kontextmenü
            // „Anpassen") als Basis übernehmen, statt ihn zu ersetzen. Sonst geht das
            // Rechtsklick-Menü am Spaltenkopf verloren. Kein Template-Setter → Sortieren
            // per Linksklick bleibt unverändert.
            Style headerStil = new Style(typeof(DataGridColumnHeader));
            if (datagrid.TryFindResource("SpaltenHeaderStyle") is Style basisHeaderStil)
            {
                headerStil.BasedOn = basisHeaderStil;
            }
            headerStil.Setters.Add(new Setter(Control.BackgroundProperty, chrome));
            headerStil.Setters.Add(new Setter(Control.ForegroundProperty, text));
            headerStil.Setters.Add(new Setter(Control.BorderBrushProperty, trenn));
            headerStil.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            headerStil.Triggers.Add(new Trigger
            {
                Property = DataGridColumnHeader.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, highlight) }
            });
            datagrid.ColumnHeaderStyle = headerStil;

                        // WICHTIG: Zell-/Zeilen-Style mit explizitem Selection-Trigger.
            // Der Selection-Trigger kommt nach dem Background-Setter und übersteuert
            // ihn bei Auswahl -> Markierung wird sichtbar.
                        double staerke = Math.Max(1, konfiguration?.GitternetzStaerke ?? 1);
            bool gitternetzAn = konfiguration?.GitternetzAnzeigen ?? true;
            double linienstarke = gitternetzAn ? staerke : 0;

            var zeilenStil = new Style(typeof(DataGridRow));
            zeilenStil.Setters.Add(new Setter(Control.BackgroundProperty, dateilistenfarbe));
            zeilenStil.Setters.Add(new Setter(Control.ForegroundProperty, listText));
            zeilenStil.Setters.Add(new Setter(Control.BorderBrushProperty, trenn));
            zeilenStil.Triggers.Add(new Trigger
            {
                Property = DataGridRow.IsSelectedProperty,
                Value = true,
                Setters =
                {
                    new Setter(Control.BackgroundProperty, highlight),
                    new Setter(Control.ForegroundProperty, highlightText)
                }
            });
            datagrid.RowStyle = zeilenStil;

            // Gitternetzlinien exakt in der gewünschten Stärke über die Zell-Ränder
            // zeichnen (rechts + unten). Die eingebauten GridLines werden dafür in
            // MainWindow auf "None" gestellt, sonst käme 1px doppelt hinzu.
                        var zellStil = new Style(typeof(DataGridCell));
            zellStil.Setters.Add(new Setter(Control.ForegroundProperty, listText));
            zellStil.Setters.Add(new Setter(Control.BorderBrushProperty, gitternetz));
            zellStil.Setters.Add(new Setter(Control.BorderThicknessProperty,
                new Thickness(0, 0, linienstarke, linienstarke)));
            zellStil.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            zellStil.Triggers.Add(new Trigger
            {
                Property = DataGridCell.IsSelectedProperty,
                Value = true,
                Setters =
                {
                    new Setter(Control.BackgroundProperty, highlight),
                    new Setter(Control.ForegroundProperty, highlightText)
                }
            });
            datagrid.CellStyle = zellStil;
        }

                // Button: eigenes Template, weil das Aero2-Standard-Template den Disabled-
                // Zustand mit einer festen hellgrauen Farbe zeichnet und Background ignoriert.
                private static void WendeButtonAn(Button button, Palette p, Brush buttonfarbe, Brush trenn)
                {
                    var stil = new Style(typeof(Button));
                    stil.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(p.ButtonText)));
                    stil.Setters.Add(new Setter(Control.BackgroundProperty, buttonfarbe));
                    stil.Setters.Add(new Setter(Control.BorderBrushProperty, trenn));
                    stil.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 3, 10, 3)));
                    stil.Setters.Add(new Setter(Control.TemplateProperty, ErzeugeButtonTemplate(p)));
                    stil.Triggers.Add(new Trigger
                    {
                        Property = UIElement.IsMouseOverProperty,
                        Value = true,
                        Setters = { new Setter(Control.BackgroundProperty, new SolidColorBrush(p.ButtonHover)) }
                    });
                    stil.Triggers.Add(new Trigger
                    {
                        Property = UIElement.IsEnabledProperty,
                        Value = false,
                        Setters =
                        {
                            new Setter(Control.BackgroundProperty, new SolidColorBrush(p.Chrome)),
                            new Setter(Control.ForegroundProperty, new SolidColorBrush(p.Trenn)),
                            new Setter(Control.BorderBrushProperty, trenn)
                        }
                    });
                    button.Style = stil;
                }

                private static ControlTemplate ErzeugeButtonTemplate(Palette p)
                {
                    var template = new ControlTemplate(typeof(Button));
                    var border = new FrameworkElementFactory(typeof(Border));
                    border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                    border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
                    border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                    border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
                    border.SetValue(Border.SnapsToDevicePixelsProperty, true);

                    var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                    presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                    presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
                    border.AppendChild(presenter);

                    template.VisualTree = border;
                    return template;
        }

        // TabItem: eigenes Template, weil das Aero2-Template den ausgewählten Tab
        // mit einem festen hellgrauen Verlauf zeichnet und Background ignoriert.
        private static void WendeTabItemAn(TabItem tabItem, Palette p, Brush buttonfarbe, Brush trenn)
        {
                    var stil = new Style(typeof(TabItem));
                    stil.Setters.Add(new Setter(Control.BackgroundProperty, buttonfarbe));
                    stil.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(p.Text)));
                    stil.Setters.Add(new Setter(Control.BorderBrushProperty, trenn));
                    stil.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 4, 10, 4)));
                    stil.Setters.Add(new Setter(Control.TemplateProperty, ErzeugeTabItemTemplate(p)));
                    stil.Triggers.Add(new Trigger
                    {
                        Property = TabItem.IsMouseOverProperty,
                        Value = true,
                        Setters = { new Setter(Control.BackgroundProperty, new SolidColorBrush(p.ButtonHover)) }
                    });
                    stil.Triggers.Add(new Trigger
                    {
                        Property = TabItem.IsSelectedProperty,
                        Value = true,
                        Setters =
                        {
                            new Setter(Control.BackgroundProperty, new SolidColorBrush(p.Fenster)),
                            new Setter(Control.ForegroundProperty, new SolidColorBrush(p.Text)),
                            new Setter(Control.BorderBrushProperty, new SolidColorBrush(p.Akzent))
                        }
                    });
                    tabItem.Style = stil;
        }

        private static ControlTemplate ErzeugeTabItemTemplate(Palette p)
        {
                    var template = new ControlTemplate(typeof(TabItem));
                    var border = new FrameworkElementFactory(typeof(Border));
                    border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                    border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
                    border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
                    border.SetValue(Border.SnapsToDevicePixelsProperty, true);

                    var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                    presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
                    presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                    presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
                    border.AppendChild(presenter);

                    template.VisualTree = border;
                    return template;
        }

                // ComboBox: eigenes Template, weil das Aero2-Popup einen festen hellen
        // Border/Hintergrund zeichnet. Wir bauen ein dunkles Feld + dunkles Popup.
        private static void WendeComboBoxAn(ComboBox comboBox, Palette p, Brush buttonfarbe, Brush akzent, Brush text)
        {
            Brush menu = new SolidColorBrush(p.Menu);
            comboBox.Background = buttonfarbe;
            comboBox.Foreground = text;
            comboBox.BorderBrush = akzent;
            comboBox.Resources[SystemColors.WindowBrushKey] = menu;
            comboBox.Resources[SystemColors.WindowTextBrushKey] = text;
            comboBox.Resources[SystemColors.ControlBrushKey] = menu;
            comboBox.Resources[SystemColors.ControlTextBrushKey] = text;

            var stil = new Style(typeof(ComboBox));
            stil.Setters.Add(new Setter(Control.BackgroundProperty, buttonfarbe));
            stil.Setters.Add(new Setter(Control.ForegroundProperty, text));
            stil.Setters.Add(new Setter(Control.BorderBrushProperty, akzent));
            stil.Setters.Add(new Setter(Control.TemplateProperty, ErzeugeComboBoxTemplate(p)));
            comboBox.Style = stil;
        }

        private static ControlTemplate ErzeugeComboBoxTemplate(Palette p)
        {
            Brush menu = new SolidColorBrush(p.Menu);
            Brush menuText = new SolidColorBrush(p.MenuText);
            Brush button = new SolidColorBrush(p.Button);

            var template = new ControlTemplate(typeof(ComboBox));

            // Äußerer Rahmen des geschlossenen Felds.
            var outer = new FrameworkElementFactory(typeof(Border));
            outer.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            outer.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            outer.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            outer.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

            var grid = new FrameworkElementFactory(typeof(Grid));

                        // Sichtbarer Inhalt (Auswahltext) + Pfeil als ToggleButton-Optik.
            var toggle = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton));
            toggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.FocusableProperty, false);
                        toggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.ClickModeProperty,
                System.Windows.Controls.ClickMode.Press);
            // WICHTIG: IsChecked<->IsDropDownOpen verbinden (TwoWay), sonst öffnet
            // das Dropdown beim Klick nicht.
            toggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                new Binding(nameof(ComboBox.IsDropDownOpen))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                    Mode = BindingMode.TwoWay
                });
            var toggleTemplate = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
            var toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            toggleTemplate.VisualTree = toggleBorder;
            toggle.SetValue(Control.TemplateProperty, toggleTemplate);
            grid.AppendChild(toggle);

            // ContentPresenter zeigt den aktuell gewählten Eintrag.
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.IsHitTestVisibleProperty, false);
            presenter.SetValue(ContentPresenter.MarginProperty, new Thickness(6, 3, 24, 3));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            presenter.SetValue(ContentPresenter.ContentProperty,
                new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
            presenter.SetValue(ContentPresenter.ContentTemplateProperty,
                new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
            grid.AppendChild(presenter);

            // Pfeil rechts.
            var arrow = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.RepeatButton));
            arrow.SetValue(System.Windows.Controls.Primitives.RepeatButton.WidthProperty, 20.0);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(Control.BackgroundProperty, Brushes.Transparent);
            arrow.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            arrow.SetValue(System.Windows.Controls.Primitives.ButtonBase.FocusableProperty, false);
            var arrowTemplate = new ControlTemplate(typeof(System.Windows.Controls.Primitives.RepeatButton));
            var arrowText = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            arrowText.SetValue(TextBlock.TextProperty, "\u25BC");
            arrowText.SetValue(TextBlock.ForegroundProperty, menuText);
            arrowText.SetValue(TextBlock.FontSizeProperty, 8.0);
            arrowText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            arrowText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrowTemplate.VisualTree = arrowText;
            arrow.SetValue(Control.TemplateProperty, arrowTemplate);
            grid.AppendChild(arrow);

            outer.AppendChild(grid);

            // Popup mit dunklem Container.
            var popup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty,
                System.Windows.Controls.Primitives.PlacementMode.Bottom);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
                new TemplateBindingExtension(ComboBox.IsDropDownOpenProperty));
            popup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PopupAnimationProperty,
                System.Windows.Controls.Primitives.PopupAnimation.Slide);

                        var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, menu);
            popupBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(p.Akzent));
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.MinWidthProperty,
                new TemplateBindingExtension(FrameworkElement.ActualWidthProperty));

            // ScrollViewer, damit lange Listen (z. B. alle installierten Schriften)
            // scrollbar sind und das Popup nicht über den Bildschirm hinauswächst.
            var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);
            scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,
                ScrollBarVisibility.Auto);
            scrollViewer.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,
                ScrollBarVisibility.Disabled);
            scrollViewer.SetValue(FrameworkElement.MaxHeightProperty, 320.0);

            var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            itemsPresenter.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, menuText);
            scrollViewer.AppendChild(itemsPresenter);
            popupBorder.AppendChild(scrollViewer);
            popup.AppendChild(popupBorder);

            // Popup muss im Template als logisches Kind vorhanden sein; wir hängen es
            // an ein Grid, das den gesamten Bereich füllt.
            var rootGrid = new FrameworkElementFactory(typeof(Grid));
            rootGrid.AppendChild(outer);
            rootGrid.AppendChild(popup);

            template.VisualTree = rootGrid;
            return template;
        }

                                // Schlankes ContextMenu-Template: dunkler Rahmen + ItemsPresenter.
        private static ControlTemplate ErzeugeContextMenuTemplate(Brush menu)
        {
            var template = new ControlTemplate(typeof(ContextMenu));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, menu);
            border.SetValue(Border.BorderBrushProperty,
                new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty,
                new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new Thickness(2));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            border.AppendChild(items);

            template.VisualTree = border;
            return template;
        }

        // Schlankes MenuItem-Template (flach, dunkel, mit Untermenü-Unterstützung).
        private static ControlTemplate ErzeugeMenuItemTemplate(Palette p, Brush menu, Brush menuText, Brush menuHover)
        {
            var template = new ControlTemplate(typeof(MenuItem));

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(FrameworkElement.MinWidthProperty, 140.0);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.MarginProperty,
                new TemplateBindingExtension(Control.PaddingProperty));
            grid.AppendChild(contentPresenter);

            // Kleiner Pfeil rechts, sichtbar nur bei vorhandenen Untermenü-Einträgen.
            var arrow = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            arrow.SetValue(System.Windows.Controls.TextBlock.TextProperty, "\u25B6");
            arrow.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 8.0);
            arrow.SetValue(System.Windows.Controls.TextBlock.ForegroundProperty, menuText);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
                        arrow.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            arrow.Name = "UntermenuPfeil";
            grid.AppendChild(arrow);

            border.AppendChild(grid);

            // Popup für Untermenüs.
            var popup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty,
                System.Windows.Controls.Primitives.PlacementMode.Right);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
                new TemplateBindingExtension(MenuItem.IsSubmenuOpenProperty));
            popup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PopupAnimationProperty,
                System.Windows.Controls.Primitives.PopupAnimation.Slide);

            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, menu);
            popupBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(p.Akzent));
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(FrameworkElement.MinWidthProperty, 140.0);

            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            items.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, menuText);
            popupBorder.AppendChild(items);
            popup.AppendChild(popupBorder);

            var root = new FrameworkElementFactory(typeof(Grid));
            root.AppendChild(border);
            root.AppendChild(popup);

            template.VisualTree = root;

            // Untermenü-Pfeil anzeigen, wenn das Item Kinder hat.
            var childTrigger = new Trigger
            {
                Property = MenuItem.RoleProperty,
                Value = MenuItemRole.SubmenuHeader
            };
            childTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "UntermenuPfeil"));
            template.Triggers.Add(childTrigger);

            return template;
        }

        // Setzt Schriftart und -größe aus der Konfiguration nur auf Listen-Controls
        // (Ordner- und Dateiliste), nicht global auf das ganze Fenster.
        private static void WendeListenSchriftAn(Control control)
        {
            if (konfiguration is null)
            {
                return;
            }

            control.FontFamily = new FontFamily(
                string.IsNullOrWhiteSpace(konfiguration.Schriftart) ? "Segoe UI" : konfiguration.Schriftart);
            control.FontSize = konfiguration.Schriftgroesse;
        }

        // Baut einen Style aus Settern (DependencyProperty/Wert) und optionalen Triggern.
        private static Style ErzeugeStil(Type zieltyp, params object[] eintraege)
        {
            var stil = new Style(zieltyp);
            foreach (object eintrag in eintraege)
            {
                if (eintrag is (DependencyProperty eigenschaft, object wert))
                {
                    stil.Setters.Add(new Setter(eigenschaft, wert));
                }
                else if (eintrag is Trigger trigger)
                {
                    stil.Triggers.Add(trigger);
                }
            }

            return stil;
        }
    }
}
