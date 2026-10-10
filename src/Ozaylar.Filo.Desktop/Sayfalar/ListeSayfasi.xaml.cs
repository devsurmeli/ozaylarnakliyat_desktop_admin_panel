using System.Windows.Controls;
using Ozaylar.Filo.Desktop.Altyapi;

namespace Ozaylar.Filo.Desktop.Sayfalar;

/// <summary>Tüm liste ekranlarının ortak sayfası. Somut sayfalar yalnızca ViewModel'i verir.</summary>
public partial class ListeSayfasi : Page
{
    public ListeSayfasi(IListeVM vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
