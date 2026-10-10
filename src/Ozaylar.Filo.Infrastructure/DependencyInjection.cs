using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Infrastructure.Disa;
using Ozaylar.Filo.Infrastructure.Guvenlik;
using Ozaylar.Filo.Infrastructure.Veri;

namespace Ozaylar.Filo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFiloInfrastructure(this IServiceCollection s, BaglantiBilgisi baglanti)
    {
        s.AddSingleton(baglanti);
        s.AddSingleton(DbSecenekleri.Ana(baglanti.AnaBaglanti));
        s.AddSingleton<FiloDbContextFactory>();
        s.AddSingleton<IFiloDbContextFactory>(sp => sp.GetRequiredService<FiloDbContextFactory>());
        s.AddDbContextFactory<EkDbContext>(o => o.UseSqlServer(baglanti.EkBaglanti), ServiceLifetime.Singleton);
        s.AddSingleton<IEkDepo, EkDepo>();
        s.AddSingleton<ISifreHasher, SifreHasher>();
        s.AddSingleton<IExcelOkuyucu, ExcelOkuyucu>();
        s.AddSingleton<IDisaAktarici>(_ => new DisaAktarici());
        s.AddSingleton<SemaKontrol>();
        s.AddSingleton<IBelgeUretici, BelgeUretici>();
        return s;
    }
}
