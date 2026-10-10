using System.Windows;
using System.Windows.Input;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Kontroller;
using Ozaylar.Filo.Desktop.Sayfalar;
using Ozaylar.Filo.Desktop.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Pencereler;

public partial class AnaPencere : FluentWindow
{
    private static ISnackbarService? _bildirim;
    private readonly PanelVM _panel;

    public AnaPencere(IServiceProvider servisler, IKullaniciOturumu oturum, ISnackbarService snackbar, PanelVM panel)
    {
        InitializeComponent();
        Grafik.Ayarla();
        _panel = panel;
        Baslik.Title = $"Ozaylar Filo  —  {oturum.AdSoyad}";
        Menu.SetServiceProvider(servisler);
        Gezgin.Bagla(Menu);
        snackbar.SetSnackbarPresenter(Bildirim);
        _bildirim = snackbar;
        YetkiyeGoreGizle(Menu.MenuItems, oturum);
        YetkiyeGoreGizle(Menu.FooterMenuItems, oturum);
        panel.UyarilarDegisti += UyariRozeti;

        // Geri: Alt+Sol veya fare geri tuşu
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.System && e.SystemKey == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) { Gezgin.Geri(); e.Handled = true; }
        };
        PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.XButton1) { Gezgin.Geri(); e.Handled = true; }
        };
        Loaded += (_, _) => Menu.Navigate(typeof(PanelSayfasi));
    }

    private void UyariRozeti(int kritik, int toplam)
    {
        UyariOgesi.Content = toplam == 0 ? "Uyarılar" : $"Uyarılar ({toplam})";
        var ikon = new SymbolIcon(toplam == 0 ? SymbolRegular.Alert24 : SymbolRegular.AlertUrgent24);
        if (kritik > 0) ikon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC4, 0x2B, 0x1C));
        UyariOgesi.Icon = ikon;
    }

    private void UyariOgesi_Click(object sender, RoutedEventArgs e)
    {
        Menu.Navigate(typeof(PanelSayfasi));
        _panel.UyarilariGoster();
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
