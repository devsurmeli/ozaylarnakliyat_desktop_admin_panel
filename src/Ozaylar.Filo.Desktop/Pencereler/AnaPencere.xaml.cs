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
    private readonly IKullaniciOturumu _oturum;
    private MenuOgesi _uyariOgesi = null!;
    private MenuOgesi _temaOgesi = null!;

    public AnaPencere(IServiceProvider servisler, IKullaniciOturumu oturum, ISnackbarService snackbar, PanelVM panel)
    {
        InitializeComponent();
        Grafik.Ayarla();
        _panel = panel;
        _oturum = oturum;
        Title = $"Ozaylar Filo — {oturum.AdSoyad}";
        Gezgin.Bagla(Menu);
        snackbar.SetSnackbarPresenter(Bildirim);
        _bildirim = snackbar;

        MenuOlustur();
        YanMenu.Kullanici(oturum.AdSoyad ?? "", (string)BasHarfDonusturucu.Ornek.Convert(oturum.AdSoyad, typeof(string), null, Kultur.Tr));
        YanMenu.SayfaIstendi += o => Menu.Git(o.Sayfa!);
        Gezgin.Degisti += () => YanMenu.SayfaDegisti(Gezgin.MevcutSayfa);
        panel.UyarilarDegisti += UyariRozeti;
        Tema.Degisti += TemaOgesiGuncelle;
        TemaOgesiGuncelle();

        // Geri: Alt+Sol veya fare geri tuşu
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.System && e.SystemKey == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) { Gezgin.Geri(); e.Handled = true; }
        };
        PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.XButton1) { Gezgin.Geri(); e.Handled = true; }
        };
        Loaded += (_, _) => Menu.Git(typeof(PanelSayfasi));

        // Uyarı rozeti 5 dakikada bir güncellenir (yeni gecikmiş taksit, evrak, şoför harcaması...).
        var zamanlayici = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        zamanlayici.Tick += async (_, _) => await _panel.UyarilariYenileAsync();
        zamanlayici.Start();
        Closed += (_, _) => zamanlayici.Stop();
    }

    /// <summary>Menü yapısı; yetkisi olmayan sayfalar ve boş kalan gruplar gösterilmez.</summary>
    private void MenuOlustur()
    {
        MenuOgesi? S(string baslik, Type sayfa, params string[] izinler) =>
            izinler.Length == 0 || izinler.Any(_oturum.Yetkili) ? new MenuOgesi(baslik, SymbolRegular.Circle24, sayfa) : null;
        void G(string baslik, SymbolRegular ikon, params MenuOgesi?[] alt)
        {
            var gorunen = alt.OfType<MenuOgesi>().ToArray();
            if (gorunen.Length > 0) YanMenu.Ogeler.Add(new MenuOgesi(baslik, ikon, null, gorunen));
        }

        YanMenu.Ogeler.Add(new MenuOgesi("Panel", SymbolRegular.Home24, typeof(PanelSayfasi)));
        G("Operasyon", SymbolRegular.VehicleTruckProfile24,
            S("Seferler", typeof(SeferlerSayfasi), Izinler.SeferGor),
            S("Masraflar", typeof(MasraflarSayfasi), Izinler.GiderGor),
            S("Yakıt", typeof(YakitSayfasi), Izinler.GiderGor),
            S("HGS / OGS Geçişleri", typeof(HgsSayfasi), Izinler.GiderGor),
            S("Trafik Cezaları", typeof(CezalarSayfasi), Izinler.GiderGor),
            S("Ekstre Aktarımı", typeof(IceAktarmaSayfasi), Izinler.IceAktar));
        G("Şoförler", SymbolRegular.PeopleTeam24,
            S("Şoför Listesi", typeof(SoforlerSayfasi), Izinler.FiloGor),
            S("Şoför Kasası (Günlük)", typeof(SoforGunlukSayfasi), Izinler.SoforHesapGor),
            S("Onay ve Mahsup", typeof(SoforHesapSayfasi), Izinler.SoforHesapGor),
            S("Mahsup Geçmişi", typeof(MahsuplarSayfasi), Izinler.SoforHesapGor),
            S("Hakediş (Maaş / Prim)", typeof(HakedisSayfasi), Izinler.SoforHesapGor),
            S("Zimmetler", typeof(ZimmetlerSayfasi), Izinler.SoforHesapGor));
        G("Finans", SymbolRegular.Wallet24,
            S("Cariler", typeof(CarilerSayfasi), Izinler.FinansGor),
            S("Kasa / Banka", typeof(KasaSayfasi), Izinler.FinansGor),
            S("Faturalar", typeof(FaturalarSayfasi), Izinler.FinansGor),
            S("Çek / Senet", typeof(CekSenetSayfasi), Izinler.FinansGor));
        G("Kredi / Leasing", SymbolRegular.BuildingBank24,
            S("Finansman Özeti", typeof(KrediOzetSayfasi), Izinler.FinansmanGor),
            S("Krediler", typeof(KredilerSayfasi), Izinler.FinansmanGor),
            S("Taksit Takvimi", typeof(TaksitlerSayfasi), Izinler.FinansmanGor));
        G("Filo", SymbolRegular.VehicleCar24,
            S("Araçlar", typeof(AraclarSayfasi), Izinler.FiloGor),
            S("Dorseler", typeof(DorselerSayfasi), Izinler.FiloGor),
            S("Evraklar / Muayene", typeof(EvraklarSayfasi), Izinler.BakimGor),
            S("Bakım / Onarım", typeof(BakimlarSayfasi), Izinler.BakimGor),
            S("Bakım Planları", typeof(BakimPlanlariSayfasi), Izinler.BakimGor),
            S("Lastikler", typeof(LastiklerSayfasi), Izinler.BakimGor));
        if (_oturum.Yetkili(Izinler.RaporOperasyon) || _oturum.Yetkili(Izinler.RaporFinans))
            YanMenu.Ogeler.Add(new MenuOgesi("Raporlar", SymbolRegular.DataBarVertical24, typeof(RaporlarSayfasi)));

        _uyariOgesi = new MenuOgesi("Uyarılar", SymbolRegular.Alert24)
        {
            Tikla = () =>
            {
                Menu.Git(typeof(PanelSayfasi));
                _panel.UyarilariGoster();
            },
        };
        _temaOgesi = new MenuOgesi("Koyu tema", SymbolRegular.WeatherMoon24) { Tikla = Tema.Degistir };
        YanMenu.AltOgeler.Add(_uyariOgesi);
        YanMenu.AltOgeler.Add(_temaOgesi);
        YanMenu.AltOgeler.Add(new MenuOgesi("Ayarlar", SymbolRegular.Settings24, typeof(AyarlarMerkeziSayfasi)));
    }

    private void UyariRozeti(int kritik, int toplam) => _uyariOgesi.Rozet = toplam == 0 ? null : toplam.ToString();

    private void TemaOgesiGuncelle()
    {
        // İkon değişimi için öğe yeniden oluşturulur (ikon salt okunur).
        var yeni = new MenuOgesi(Tema.Koyu ? "Açık tema" : "Koyu tema", Tema.Koyu ? SymbolRegular.WeatherSunny24 : SymbolRegular.WeatherMoon24) { Tikla = Tema.Degistir };
        var i = YanMenu.AltOgeler.IndexOf(_temaOgesi);
        if (i >= 0) YanMenu.AltOgeler[i] = yeni;
        _temaOgesi = yeni;
    }

    /// <summary>Kısa başarı bildirimi (sağ alt).</summary>
    public static void Basarili(string mesaj) =>
        _bildirim?.Show("Tamam", mesaj, ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(3));
}
