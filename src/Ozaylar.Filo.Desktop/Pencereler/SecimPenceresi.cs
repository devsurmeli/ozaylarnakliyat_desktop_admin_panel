using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Ozaylar.Filo.Desktop.Altyapi;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace Ozaylar.Filo.Desktop.Pencereler;

public sealed partial class SecimSatiri<T>(T oge) : ObservableObject
{
    public T Oge { get; } = oge;
    [ObservableProperty] private bool _secili;
}

/// <summary>Listeden çoklu seçim penceresi (ör. faturalanacak seferler).</summary>
public static class SecimPenceresi
{
    public static List<T> Sec<T>(string baslik, IEnumerable<T> ogeler, IReadOnlyList<Kolon> kolonlar)
    {
        var liste = new ObservableCollection<SecimSatiri<T>>(ogeler.Select(o => new SecimSatiri<T>(o)));
        var tablo = new System.Windows.Controls.DataGrid
        {
            ItemsSource = liste, AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = false,
        };
        tablo.Columns.Add(new DataGridCheckBoxColumn { Header = "Seç", Binding = new System.Windows.Data.Binding("Secili") { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged } });
        var gecici = new System.Windows.Controls.DataGrid();
        KolonOlusturucu.Uygula(gecici, kolonlar.Select(k => k with { Yol = "Oge." + k.Yol }));
        foreach (var c in gecici.Columns.ToList()) { gecici.Columns.Remove(c); tablo.Columns.Add(c); }

        var w = new FluentWindow
        {
            Title = baslik, Width = 980, Height = 560, Owner = Dialog.Sahip, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ExtendsContentIntoTitleBar = true, WindowBackdropType = WindowBackdropType.Mica,
        };
        var tamam = new Button { Content = "Seçilenleri ekle", Appearance = ControlAppearance.Primary, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        tamam.Click += (_, _) => w.DialogResult = true;
        var iptal = new Button { Content = "Vazgeç", IsCancel = true };
        var tumu = new CheckBox { Content = "Tümünü seç", VerticalAlignment = VerticalAlignment.Center };
        tumu.Click += (_, _) => { foreach (var s in liste) s.Secili = tumu.IsChecked == true; };
        var alt = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var sag = new StackPanel { Orientation = Orientation.Horizontal };
        sag.Children.Add(tamam);
        sag.Children.Add(iptal);
        DockPanel.SetDock(sag, Dock.Right);
        alt.Children.Add(sag);
        alt.Children.Add(tumu);
        var ic = new DockPanel { Margin = new Thickness(16, 8, 16, 16) };
        DockPanel.SetDock(alt, Dock.Bottom);
        ic.Children.Add(alt);
        ic.Children.Add(tablo);
        var kok = new DockPanel();
        var tb = new TitleBar { Title = baslik, ShowMinimize = false };
        DockPanel.SetDock(tb, Dock.Top);
        kok.Children.Add(tb);
        kok.Children.Add(ic);
        w.Content = kok;
        return w.ShowDialog() == true ? liste.Where(s => s.Secili).Select(s => s.Oge).ToList() : [];
    }
}

/// <summary>Sefer ara durak düzenleyici.</summary>
public static class DurakPenceresi
{
    public static bool Ac(string baslik, ObservableCollection<ViewModels.SeferListeVM.DurakSatiri> liste, List<Application.Ortak.Secim> lokasyonlar, bool duzenlenebilir)
    {
        var tablo = new System.Windows.Controls.DataGrid
        {
            ItemsSource = liste, AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = duzenlenebilir,
            CanUserDeleteRows = duzenlenebilir, IsReadOnly = !duzenlenebilir,
        };
        tablo.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Lokasyon", ItemsSource = lokasyonlar, SelectedValuePath = "Id", DisplayMemberPath = "Ad",
            SelectedValueBinding = new System.Windows.Data.Binding("LokasyonId"), Width = 300,
        });
        tablo.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Tip", ItemsSource = Application.Ortak.EnumMetin.Liste<Domain.Common.DurakTipi>(), SelectedValuePath = "Deger", DisplayMemberPath = "Ad",
            SelectedValueBinding = new System.Windows.Data.Binding("DurakTipi"), Width = 140,
        });
        tablo.Columns.Add(new DataGridTextColumn { Header = "Planlanan (gg.aa.yyyy ss:dd)", Binding = new System.Windows.Data.Binding("PlanlananTarih") { StringFormat = "dd.MM.yyyy HH:mm" }, Width = 180 });
        tablo.Columns.Add(new DataGridTextColumn { Header = "Açıklama", Binding = new System.Windows.Data.Binding("Aciklama"), Width = 220 });

        var w = new FluentWindow
        {
            Title = baslik, Width = 900, Height = 460, Owner = Dialog.Sahip, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ExtendsContentIntoTitleBar = true, WindowBackdropType = WindowBackdropType.Mica,
        };
        var kaydet = new Button { Content = "Kaydet", Appearance = ControlAppearance.Primary, Margin = new Thickness(0, 0, 8, 0), IsEnabled = duzenlenebilir };
        kaydet.Click += (_, _) => { tablo.CommitEdit(DataGridEditingUnit.Row, true); w.DialogResult = true; };
        var iptal = new Button { Content = "Kapat", IsCancel = true };
        var alt = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        alt.Children.Add(kaydet);
        alt.Children.Add(iptal);
        var ic = new DockPanel { Margin = new Thickness(16, 8, 16, 16) };
        DockPanel.SetDock(alt, Dock.Bottom);
        ic.Children.Add(alt);
        ic.Children.Add(new System.Windows.Controls.TextBlock { Text = "Son satıra yazarak durak ekleyin; Delete tuşu ile silin. Sıra listedeki sıradır.", Opacity = 0.7, Margin = new Thickness(0, 0, 0, 6) });
        DockPanel.SetDock(ic.Children[^1], Dock.Top);
        ic.Children.Add(tablo);
        var kok = new DockPanel();
        var tb = new TitleBar { Title = baslik, ShowMinimize = false };
        DockPanel.SetDock(tb, Dock.Top);
        kok.Children.Add(tb);
        kok.Children.Add(ic);
        w.Content = kok;
        return w.ShowDialog() == true;
    }
}
