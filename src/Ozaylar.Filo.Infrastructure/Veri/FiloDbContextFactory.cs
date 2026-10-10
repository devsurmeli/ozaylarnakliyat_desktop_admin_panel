using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;

namespace Ozaylar.Filo.Infrastructure.Veri;

public sealed class FiloDbContextFactory(DbContextOptions<FiloDbContext> options, IKullaniciOturumu oturum, ISaat saat)
    : IFiloDbContextFactory
{
    public IFiloDbContext Olustur() => new FiloDbContext(options, oturum, saat);
    public FiloDbContext OlusturSomut() => new(options, oturum, saat);
}

public static class DbSecenekleri
{
    public static DbContextOptions<FiloDbContext> Ana(string baglanti) =>
        new DbContextOptionsBuilder<FiloDbContext>()
            .UseSqlServer(baglanti, o =>
            {
                o.CommandTimeout(60);
                o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
            })
            .Options;

    public static DbContextOptions<EkDbContext> Ek(string baglanti) =>
        new DbContextOptionsBuilder<EkDbContext>()
            .UseSqlServer(baglanti)
            .Options;
}

/// <summary>dotnet ef migrations için (geliştirme makinesi, Windows kimlik doğrulaması).</summary>
public sealed class TasarimZamaniFactory : IDesignTimeDbContextFactory<FiloDbContext>
{
    public FiloDbContext CreateDbContext(string[] args) =>
        new(DbSecenekleri.Ana(new BaglantiBilgisi("localhost", WindowsKimlik: true).AnaBaglanti));
}

public sealed class EkTasarimZamaniFactory : IDesignTimeDbContextFactory<EkDbContext>
{
    public EkDbContext CreateDbContext(string[] args) =>
        new(DbSecenekleri.Ek(new BaglantiBilgisi("localhost", WindowsKimlik: true).EkBaglanti));
}

