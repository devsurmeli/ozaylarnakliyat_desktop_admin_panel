using Microsoft.Extensions.DependencyInjection;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Desktop.Sayfalar;
using Ozaylar.Filo.Desktop.ViewModels;
using Wpf.Ui;

namespace Ozaylar.Filo.Desktop;

/// <summary>Masaüstü katmanı DI kayıtları: sayfalar ve ViewModel'ler tekil (gezinti sırasında filtre/durum korunur).</summary>
public static class Kayitlar
{
    public static void Ekle(IServiceCollection s)
    {
        s.AddSingleton<ISnackbarService, SnackbarService>();
        s.AddSingleton<Secimler>();
        s.AddSingleton<AnaPencere>();

        // Sayfa + VM çiftleri
        void P<TSayfa, TVm>() where TSayfa : class where TVm : class
        {
            s.AddSingleton<TVm>();
            s.AddSingleton<TSayfa>();
        }
        P<PanelSayfasi, PanelVM>();
        P<SeferlerSayfasi, SeferListeVM>();
        P<MasraflarSayfasi, MasrafListeVM>();
        P<YakitSayfasi, YakitListeVM>();
        P<HgsSayfasi, HgsListeVM>();
        P<CezalarSayfasi, CezaListeVM>();
        P<IceAktarmaSayfasi, IceAktarmaVM>();
        P<SoforHesapSayfasi, SoforHesapVM>();
        P<AvanslarSayfasi, AvansListeVM>();
        P<HarcirahlarSayfasi, HarcirahListeVM>();
        P<MahsuplarSayfasi, MahsupListeVM>();
        P<CarilerSayfasi, CariListeVM>();
        P<KasaSayfasi, KasaListeVM>();
        P<FaturalarSayfasi, FaturaListeVM>();
        P<CekSenetSayfasi, CekSenetListeVM>();
        P<FinansmanSayfasi, FinansmanListeVM>();
        P<TaksitlerSayfasi, TaksitListeVM>();
        P<AraclarSayfasi, AracListeVM>();
        P<DorselerSayfasi, DorseListeVM>();
        P<SoforlerSayfasi, SoforListeVM>();
        P<BakimlarSayfasi, BakimListeVM>();
        P<BakimPlanlariSayfasi, BakimPlaniListeVM>();
        P<LastiklerSayfasi, LastikListeVM>();
        P<RaporlarSayfasi, RaporVM>();
        P<LokasyonlarSayfasi, LokasyonListeVM>();
        P<GuzergahlarSayfasi, GuzergahListeVM>();
        P<MasrafTurleriSayfasi, MasrafTuruListeVM>();
        P<EvrakTurleriSayfasi, EvrakTuruListeVM>();
        P<HarcirahTarifeleriSayfasi, HarcirahTarifeListeVM>();
        P<SablonlarSayfasi, SablonListeVM>();
        P<KullanicilarSayfasi, KullaniciListeVM>();
        P<RollerSayfasi, RolListeVM>();
        P<AyarlarSayfasi, AyarlarVM>();
        P<AuditSayfasi, AuditListeVM>();

        // Evrak listesi hem menü sayfası hem araç/şoför kartından filtreli pencere olarak açılır.
        s.AddTransient<EvrakListeVM>();
        s.AddSingleton<EvraklarSayfasi>();
    }
}
