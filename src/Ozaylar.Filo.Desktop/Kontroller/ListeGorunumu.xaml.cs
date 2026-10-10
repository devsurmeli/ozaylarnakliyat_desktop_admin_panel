using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ozaylar.Filo.Desktop.Altyapi;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Ozaylar.Filo.Desktop.Kontroller;

/// <summary>
/// Genel liste görünümü: komut çubuğu, "Diğer" menüsü, sağ tık menüsü, kısayollar, renkli satırlar.
/// Hem menü sayfalarında (ListeSayfasi) hem detay sayfası sekmelerinde kullanılır.
/// </summary>
public partial class ListeGorunumu : UserControl
{
    private IListeVM? _vm;

    public ListeGorunumu()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bagla();
        Loaded += async (_, _) =>
        {
            Bagla();
            if (_vm is not null) await _vm.IlkYuklemeAsync();
        };
        // Ctrl+F: arama kutusu
        InputBindings.Add(new KeyBinding(new RelayKomut(() => AramaKutusu.Focus()), Key.F, ModifierKeys.Control));
    }

    private void Bagla()
    {
        if (DataContext is not IListeVM vm || ReferenceEquals(vm, _vm)) return;
        _vm = vm;
        KolonOlusturucu.Uygula(Tablo, vm.Kolonlar);
        DigerBtn.Visibility = vm.DigerEylemler.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Tablo.ContextMenu = MenuOlustur(vm, tumu: true);
    }

    private static ContextMenu MenuOlustur(IListeVM vm, bool tumu)
    {
        var m = new ContextMenu();
        void Ekle(string metin, SymbolRegular ikon, ICommand komut) =>
            m.Items.Add(new MenuItem { Header = metin, Command = komut, Icon = new SymbolIcon(ikon) });
        if (tumu)
        {
            if (vm.DetayVar) Ekle("Aç (detay)", SymbolRegular.Open24, vm.DetayKomut);
            if (vm.DuzenleVar) Ekle("Düzenle", SymbolRegular.Edit24, vm.DuzenleKomut);
            if (vm.SilVar) Ekle(vm.SilMetni, SymbolRegular.Delete24, vm.SilKomut);
            if (m.Items.Count > 0 && vm.Eylemler.Count > 0) m.Items.Add(new Separator());
        }
        foreach (var e in tumu ? vm.Eylemler : vm.DigerEylemler) Ekle(e.Metin, e.Ikon, e.Komut);
        if (tumu)
        {
            m.Items.Add(new Separator());
            Ekle("Excel'e aktar", SymbolRegular.DocumentTable24, vm.ExcelKomut);
            Ekle("PDF", SymbolRegular.DocumentPdf24, vm.PdfKomut);
        }
        return m;
    }

    private void DigerBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var m = MenuOlustur(_vm, tumu: false);
        m.PlacementTarget = DigerBtn;
        m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        m.IsOpen = true;
    }

    private void Tablo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: not null } && _vm?.SeciliNesne is not null) Varsayilan();
    }

    private void Tablo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        switch (e.Key)
        {
            case Key.Enter when _vm.SeciliNesne is not null:
                Varsayilan();
                e.Handled = true;
                break;
            case Key.Delete when _vm.SilVar:
                _vm.SilKomut.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Çift tık / Enter: detay varsa detay, yoksa düzenle, yoksa ilk eylem.</summary>
    private void Varsayilan()
    {
        if (_vm is null) return;
        if (_vm.DetayVar) _vm.DetayKomut.Execute(null);
        else if (_vm.DuzenleVar) _vm.DuzenleKomut.Execute(null);
        else if (_vm.Eylemler.FirstOrDefault() is { } ilk) ilk.Komut.Execute(null);
    }
}

/// <summary>Basit senkron komut.</summary>
public sealed class RelayKomut(Action calistir) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => calistir();
}
