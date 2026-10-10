using System.Windows.Controls;
using System.Windows.Input;
using Ozaylar.Filo.Desktop.Altyapi;

namespace Ozaylar.Filo.Desktop.Sayfalar;

/// <summary>Tüm liste ekranlarının ortak sayfası. Somut sayfalar yalnızca ViewModel'i verir.</summary>
public partial class ListeSayfasi : Page
{
    private readonly IListeVM _vm;

    public ListeSayfasi(IListeVM vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        KolonOlusturucu.Uygula(Tablo, vm.Kolonlar);
        Loaded += async (_, _) => await vm.IlkYuklemeAsync();
    }

    private void Tablo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.FrameworkElement { DataContext: not null } && _vm.SeciliNesne is not null)
        {
            if (_vm.DuzenleVar && _vm.DuzenleKomut.CanExecute(null)) _vm.DuzenleKomut.Execute(null);
            else if (_vm.Eylemler.FirstOrDefault() is { } ilk) ilk.Komut.Execute(null);
        }
    }
}
