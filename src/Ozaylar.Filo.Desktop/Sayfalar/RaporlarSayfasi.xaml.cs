using System.Windows.Controls;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.ViewModels;

namespace Ozaylar.Filo.Desktop.Sayfalar;

public partial class RaporlarSayfasi : Page
{
    public RaporlarSayfasi(RaporVM vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.KolonlarDegisti += k => KolonOlusturucu.Uygula(Tablo, k);
        if (vm.Secili is not null) KolonOlusturucu.Uygula(Tablo, vm.Secili.Kolonlar);
    }
}
