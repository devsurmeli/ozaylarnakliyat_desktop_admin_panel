using System.Windows;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Desktop.Sayfalar;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Pencereler;

public partial class AnaPencere : FluentWindow
{
    private static ISnackbarService? _bildirim;

    public AnaPencere(IServiceProvider servisler, IKullaniciOturumu oturum, ISnackbarService snackbar)
    {
        InitializeComponent();
        Baslik.Title = $"Ozaylar Filo  —  {oturum.AdSoyad}";
        Menu.SetServiceProvider(servisler);
        snackbar.SetSnackbarPresenter(Bildirim);
        _bildirim = snackbar;
        YetkiyeGoreGizle(Menu.MenuItems, oturum);
        YetkiyeGoreGizle(Menu.FooterMenuItems, oturum);
        Loaded += (_, _) => Menu.Navigate(typeof(PanelSayfasi));
    }

    /// <summary>Tag'de izin kodu olan menü öğelerini (| ile birden fazla, biri yeterli) yetkiye göre gizler; boş kalan grupları da gizler.</summary>
    private static bool YetkiyeGoreGizle(System.Collections.IList ogeler, IKullaniciOturumu oturum)
    {
        var gorunurVar = false;
        foreach (var o in ogeler.OfType<NavigationViewItem>())
        {
            bool gorunur;
            if (o.MenuItems.Count > 0) gorunur = YetkiyeGoreGizle(o.MenuItems, oturum);
            else gorunur = o.Tag is not string izin || izin.Split('|').Any(oturum.Yetkili);
            o.Visibility = gorunur ? Visibility.Visible : Visibility.Collapsed;
            gorunurVar |= gorunur;
        }
        return gorunurVar;
    }

    /// <summary>Kısa başarı bildirimi (sağ alt).</summary>
    public static void Basarili(string mesaj) =>
        _bildirim?.Show("Tamam", mesaj, ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(3));
}
