using Microsoft.Data.SqlClient;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Infrastructure.Guvenlik;
using Ozaylar.Filo.Infrastructure.Veri;

namespace Ozaylar.Filo.Infrastructure.Tests;

/// <summary>
/// Yerel SQL Server'da (Windows kimlik) geçici bir test veritabanı oluşturur, migration uygular, sonunda siler.
/// Sunucu adresi OZAYLAR_TEST_SQL ortam değişkeniyle değiştirilebilir.
/// </summary>
public sealed class VeritabaniFixture : IAsyncLifetime
{
    public BaglantiBilgisi Baglanti { get; } = new(
        Environment.GetEnvironmentVariable("OZAYLAR_TEST_SQL") ?? "localhost",
        "OzaylarFiloTest_" + Guid.NewGuid().ToString("N")[..8],
        WindowsKimlik: true);

    public KullaniciOturumu Oturum { get; } = new();

    public async Task InitializeAsync()
    {
        var h = new VeritabaniHazirlik(Baglanti, new SifreHasher());
        await h.MigrateAsync();
        await h.VerileriHazirlaAsync();
        Oturum.Baslat(1, "Test", Izinler.Tumu.Select(i => i.Kod));
    }

    public FiloDbContext Db(ISaat? saat = null) => new(DbSecenekleri.Ana(Baglanti.AnaBaglanti), Oturum, saat);

    public IFiloDbContextFactory Factory(ISaat? saat = null) =>
        new FiloDbContextFactory(DbSecenekleri.Ana(Baglanti.AnaBaglanti), Oturum, saat ?? new SistemSaati());

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection(Baglanti.MasterBaglanti);
        await c.OpenAsync();
        foreach (var db in new[] { Baglanti.Veritabani, Baglanti.EkVeritabani })
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"IF DB_ID(N'{db}') IS NOT NULL BEGIN ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{db}]; END";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Ad)]
public sealed class VeritabaniKoleksiyonu : ICollectionFixture<VeritabaniFixture>
{
    public const string Ad = "Veritabani";
}

public sealed class SabitSaat(DateOnly bugun) : ISaat
{
    public DateTime UtcNow => bugun.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
    public DateTime Simdi => bugun.ToDateTime(new TimeOnly(9, 0));
    public DateOnly Bugun => bugun;
}
