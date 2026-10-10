using System.Windows.Controls;
using Ozaylar.Filo.Desktop.ViewModels;

namespace Ozaylar.Filo.Desktop.Sayfalar;

public partial class SoforHesapSayfasi : Page
{
    public SoforHesapSayfasi(SoforHesapVM vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.YenileAsync();
    }
}
