using System.Windows.Controls;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.ViewModels;

namespace Ozaylar.Filo.Desktop.Sayfalar;

public partial class IceAktarmaSayfasi : Page
{
    public IceAktarmaSayfasi(IceAktarmaVM vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await Dialog.Calistir(vm.YukleAsync);
    }
}
