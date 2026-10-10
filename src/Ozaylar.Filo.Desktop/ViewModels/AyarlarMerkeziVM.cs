using System.Windows.Controls;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Sayfalar;

namespace Ozaylar.Filo.Desktop.ViewModels;

/// <summary>Tanımlar + yönetim ekranlarını tek sekmeli sayfada toplar (menü sadeleştirme).</summary>
public sealed class AyarlarMerkeziVM : DetayVM
{
    public AyarlarMerkeziVM(IKullaniciOturumu oturum)
    {
        Baslik = "Ayarlar ve Tanımlar";
        AltBaslik = "Genel ayarlar, tanım listeleri, kullanıcılar ve denetim kaydı";
        Sekmeler.Add(new DetaySekme("Genel", () => new Frame
        {
            Content = App.Al<AyarlarSayfasi>(), NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden,
        }));
        void L<T>(string ad, string? izin) where T : IListeVM
        {
            if (izin is null || oturum.Yetkili(izin)) Sekmeler.Add(DetaySekme.Liste(ad, () => App.Al<T>()));
        }
        L<LokasyonListeVM>("Lokasyonlar", Izinler.TanimGor);
        L<GuzergahListeVM>("Güzergahlar", Izinler.TanimGor);
        L<MasrafTuruListeVM>("Masraf Türleri", Izinler.TanimGor);
        L<EvrakTuruListeVM>("Evrak Türleri", Izinler.TanimGor);
        L<HarcirahTarifeListeVM>("Harcırah Tarifeleri", Izinler.TanimGor);
        L<SablonListeVM>("Ekstre Şablonları", Izinler.IceAktar);
        L<KullaniciListeVM>("Kullanıcılar", Izinler.KullaniciYonet);
        L<RolListeVM>("Roller ve Yetkiler", Izinler.KullaniciYonet);
        L<AuditListeVM>("Denetim Kaydı", Izinler.AuditGor);
    }

    protected override Task YukleAsync() => Task.CompletedTask;
}
