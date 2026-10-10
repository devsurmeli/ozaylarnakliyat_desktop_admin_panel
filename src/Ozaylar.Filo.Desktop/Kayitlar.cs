using Microsoft.Extensions.DependencyInjection;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Desktop.Sayfalar;
using Ozaylar.Filo.Desktop.ViewModels;
using Wpf.Ui;

namespace Ozaylar.Filo.Desktop;

/// <summary>
/// Masaüstü katmanı DI kayıtları: menü sayfaları ve ViewModel'ler tekil (gezinti sırasında filtre/durum korunur).
/// Detay sayfası geçicidir; modeli (DetayVM) gezinme sırasında verilir.
/// </summary>
public static class Kayitlar
{
    public static void Ekle(IServiceCollection s)
    {
        s.AddSingleton<ISnackbarService, SnackbarService>();
        s.AddSingleton<Secimler>();
        s.AddSingleton<AnaPencere>();
        s.AddTransient<DetaySayfasi>();

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
        P<SoforGunlukSayfasi, SoforGunlukVM>();
        P<HakedisSayfasi, HakedisListeVM>();
        P<ZimmetlerSayfasi, ZimmetListeVM>();
        P<CarilerSayfasi, CariListeVM>();
        P<KasaSayfasi, KasaListeVM>();
        P<FaturalarSayfasi, FaturaListeVM>();
        P<CekSenetSayfasi, CekSenetListeVM>();
        P<KrediOzetSayfasi, KrediOzetVM>();
        P<KredilerSayfasi, KrediListeVM>();
        P<TaksitlerSayfasi, KrediTakvimVM>();
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
        P<AyarlarMerkeziSayfasi, AyarlarMerkeziVM>();

        // Evrak listesi hem menü sayfası hem araç/şoför detayında filtreli sekme olarak açılır.
        s.AddTransient<EvrakListeVM>();
        s.AddSingleton<EvraklarSayfasi>();
    }
}
