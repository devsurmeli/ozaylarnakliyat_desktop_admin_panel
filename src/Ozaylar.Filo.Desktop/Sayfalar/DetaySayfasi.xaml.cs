using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Ozaylar.Filo.Desktop.Altyapi;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Ozaylar.Filo.Desktop.Sayfalar;

/// <summary>Genel 360° detay sayfası. Model (DetayVM) gezinme sırasında DataContext olarak verilir (Gezgin.Ac).</summary>
public partial class DetaySayfasi : Page
{
    public DetaySayfasi()
    {
        InitializeComponent();
        DataContextChanged += async (_, _) => await HazirlaAsync();
        Loaded += async (_, _) =>
        {
            // Menüden açılan pano sayfalarında (Panel, Finansman Özeti, Ayarlar) geri/konum gösterilmez.
            var menuSayfasi = GetType() != typeof(DetaySayfasi);
            GeriBtn.Visibility = Gezgin.GeriVar && !menuSayfasi ? Visibility.Visible : Visibility.Collapsed;
            var onceki = menuSayfasi ? null : Gezgin.OncekiBaslik;
            KonumOnceki.Text = onceki ?? "";
            Konum.Visibility = string.IsNullOrEmpty(onceki) ? Visibility.Collapsed : Visibility.Visible;
            // Sayfaya geri dönüldüğünde (veya panel tekrar açıldığında) güncel veriyi göster.
            var dahaOnce = Vm?.Yuklendi == true;
            await HazirlaAsync();
            if (dahaOnce && Vm is { } v) await v.YenileAsync();
        };
    }

    private DetayVM? Vm => DataContext as DetayVM;

    private async Task HazirlaAsync()
    {
        if (Vm is not { } vm) return;
        DigerBtn.Visibility = vm.DigerEylemler.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        YazdirBtn.Visibility = vm.Belgeler.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded) await vm.IlkYuklemeAsync();
    }

    private void GeriBtn_Click(object sender, RoutedEventArgs e) => Gezgin.Geri();

    private void KonumOnceki_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => Gezgin.Geri();

    private void DigerBtn_Click(object sender, RoutedEventArgs e) { if (Vm is { } vm) MenuAc(DigerBtn, vm.DigerEylemler); }

    private void YazdirBtn_Click(object sender, RoutedEventArgs e) { if (Vm is { } vm) MenuAc(YazdirBtn, vm.Belgeler); }

    private static void MenuAc(FrameworkElement hedef, IEnumerable<Eylem> eylemler)
    {
        var m = new ContextMenu { PlacementTarget = hedef, Placement = PlacementMode.Bottom };
        foreach (var e in eylemler) m.Items.Add(new MenuItem { Header = e.Metin, Command = e.Komut, Icon = new SymbolIcon(e.Ikon) });
        m.IsOpen = true;
    }
}
