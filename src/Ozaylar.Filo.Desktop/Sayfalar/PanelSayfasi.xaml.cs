using System.Windows.Controls;
using Ozaylar.Filo.Desktop.ViewModels;

namespace Ozaylar.Filo.Desktop.Sayfalar;

public partial class PanelSayfasi : Page
{
    public PanelSayfasi(PanelVM vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.YenileAsync();
    }
}
