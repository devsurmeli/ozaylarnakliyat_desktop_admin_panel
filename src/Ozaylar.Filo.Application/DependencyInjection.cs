using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFiloApplication(this IServiceCollection s)
    {
        s.AddSingleton<ISaat, SistemSaati>();
        s.AddSingleton<IKullaniciOturumu, KullaniciOturumu>();
        s.AddValidatorsFromAssemblyContaining<AracDogrulayici>(ServiceLifetime.Singleton);

        // Servisler durumsuzdur; her çağrı kendi kısa ömürlü DbContext'ini açar.
        s.AddSingleton(typeof(IKayitServisi<>), typeof(KayitServisi<>));
        s.AddSingleton<AracServisi>();
        s.AddSingleton<IKayitServisi<Arac>>(sp => sp.GetRequiredService<AracServisi>());
        s.AddSingleton<DorseServisi>();
        s.AddSingleton<IKayitServisi<Dorse>>(sp => sp.GetRequiredService<DorseServisi>());
        s.AddSingleton<SoforServisi>();
        s.AddSingleton<IKayitServisi<Sofor>>(sp => sp.GetRequiredService<SoforServisi>());
        s.AddSingleton<CariServisi>();
        s.AddSingleton<IKayitServisi<Cari>>(sp => sp.GetRequiredService<CariServisi>());
        s.AddSingleton<EvrakServisi>();
        s.AddSingleton<IKayitServisi<Evrak>>(sp => sp.GetRequiredService<EvrakServisi>());

        s.AddSingleton<OturumServisi>();
        s.AddSingleton<KullaniciServisi>();
        s.AddSingleton<RolServisi>();
        s.AddSingleton<AyarServisi>();
        s.AddSingleton<AuditServisi>();
        s.AddSingleton<DovizServisi>();
        s.AddSingleton<SeferServisi>();
        s.AddSingleton<FaturaServisi>();
        s.AddSingleton<FinansServisi>();
        s.AddSingleton<SoforHesapServisi>();
        s.AddSingleton<GiderServisi>();
        s.AddSingleton<IceAktarmaServisi>();
        s.AddSingleton<BakimServisi>();
        s.AddSingleton<LastikServisi>();
        s.AddSingleton<CekSenetServisi>();
        s.AddSingleton<EkServisi>();
        s.AddSingleton<RaporServisi>();
        s.AddSingleton<PanelServisi>();
        s.AddSingleton<KrediServisi>();
        s.AddSingleton<UyariServisi>();
        s.AddSingleton<DetayServisi>();
        return s;
    }
}
