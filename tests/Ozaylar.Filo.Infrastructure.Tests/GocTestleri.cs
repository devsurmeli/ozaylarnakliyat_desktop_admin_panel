using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Infrastructure.Veri;

namespace Ozaylar.Filo.Infrastructure.Tests;

/// <summary>v1 → v2 migration'ı: eski araç finansmanı verisi kredi tablolarına doğru aktarılır.</summary>
public sealed class GocTestleri : IAsyncLifetime
{
    private readonly BaglantiBilgisi _b = new(Environment.GetEnvironmentVariable("OZAYLAR_TEST_SQL") ?? "localhost",
        "OzaylarFiloGoc_" + Guid.NewGuid().ToString("N")[..8], WindowsKimlik: true);

    private FiloDbContext Db() => new(DbSecenekleri.Ana(_b.AnaBaglanti));

    [Fact]
    public async Task V1_finansman_verisi_kredi_tablolarina_aktarilir()
    {
        int bankaId;
        short aracId;
        byte kasaId;
        await using (var db = Db())
        {
            await db.GetService<IMigrator>().MigrateAsync("20261010120038_Baslangic");
            var banka = new Cari { Kod = "B001", Unvan = "Test Bankası", CariTipi = CariTipi.Finans };
            var arac = new Arac { Plaka = "34 GOC 01", GuncelKm = 1_000, BeklenenTuketim = 30 };
            var kasa = new KasaHesap { Ad = "Goc Banka", AcilisBakiye = 1_000_000, AcilisTarihi = new DateOnly(2026, 1, 1) };
            db.AddRange(banka, arac, kasa);
            await db.SaveChangesAsync();
            (bankaId, aracId, kasaId) = (banka.Id, arac.Id, kasa.Id);

            // v1 şeması: 3 taksitli kredi, ilk taksit ödenmiş
            await db.Database.ExecuteSqlRawAsync($"""
                SET IDENTITY_INSERT [AracFinansman] ON;
                INSERT INTO [AracFinansman] ([Id],[AracId],[DorseId],[Tip],[FinansKurumuCariId],[SozlesmeNo],[BaslangicTarihi],[AnaparaTutar],[ParaBirimi],
                    [AylikFaizOrani],[VadeSayisi],[Aciklama],[Aktif],[OlusturmaTarihi],[IsDeleted])
                VALUES (7, {aracId}, NULL, 2, {bankaId}, 'LS-1', '2026-01-01', 30000, 1, 2, 3, NULL, 1, SYSUTCDATETIME(), 0);
                SET IDENTITY_INSERT [AracFinansman] OFF;
                INSERT INTO [FinansmanTaksit] ([AracFinansmanId],[TaksitNo],[VadeTarihi],[Anapara],[Faiz],[Toplam],[OdemeTarihi],[OdemeKasaHesapId],[OlusturmaTarihi])
                VALUES (7, 1, '2026-02-01', 9802.65, 600, 10402.65, '2026-02-01', {kasaId}, SYSUTCDATETIME()),
                       (7, 2, '2026-03-01', 9998.70, 403.95, 10402.65, NULL, NULL, SYSUTCDATETIME()),
                       (7, 3, '2026-04-01', 10198.65, 203.97, 10402.62, NULL, NULL, SYSUTCDATETIME());
                """);
        }

        await using (var db = Db())
        {
            await db.Database.MigrateAsync();
            var k = await db.Krediler.IgnoreQueryFilters().Include(x => x.Araclar).SingleAsync(x => x.Id == 7);
            Assert.Equal(("LS-1", KrediTuru.Leasing, 30_000m, 2m, FaizTipi.Aylik, KrediDurum.Aktif), (k.KrediNo, k.Tur, k.Anapara, k.FaizOrani, k.FaizTipi, k.Durum));
            Assert.Equal(new DateOnly(2026, 2, 1), k.IlkVade);
            Assert.Equal(aracId, Assert.Single(k.Araclar).AracId);

            var t = await db.KrediTaksitleri.Where(x => x.KrediId == 7).OrderBy(x => x.TaksitNo).ToListAsync();
            Assert.Equal([TaksitDurum.Odendi, TaksitDurum.Bekliyor, TaksitDurum.Bekliyor], t.Select(x => x.Durum));
            Assert.Equal([20_197.35m, 10_198.65m, 0m], t.Select(x => x.KalanAnapara));
            Assert.Equal(10_402.65m, t[0].OdenenTutar);

            var o = await db.KrediOdemeleri.SingleAsync(x => x.KrediId == 7);
            Assert.Equal((t[0].Id, 9_802.65m, 600m, kasaId), (o.KrediTaksitId!.Value, o.Anapara, o.Faiz, o.KasaHesapId));

            // Banka carisi: kalan anapara kadar alacaklı (v1 kullandırımı deftere yazmıyordu)
            var bakiye = await db.CariHareketleri.Where(h => h.CariId == bankaId).SumAsync(h => h.Borc - h.Alacak);
            Assert.Equal(-20_197.35m, bakiye);
        }

        // Aktarılan ödeme iptal edilebilir: taksit açılır, anapara borcu banka carisine geri yazılır.
        var oturum = new Application.Guvenlik.KullaniciOturumu();
        oturum.Baslat(1, "Test", Application.Guvenlik.Izinler.Tumu.Select(i => i.Kod));
        var factory = new FiloDbContextFactory(DbSecenekleri.Ana(_b.AnaBaglanti), oturum, new Application.Abstractions.SistemSaati());
        var kredi = new Application.Servisler.KrediServisi(factory, oturum, new Application.Abstractions.SistemSaati(), new Application.Servisler.DovizServisi(factory));
        var odeme = (await kredi.DetayAsync(7)).Odemeler.Single();
        await kredi.OdemeIptalAsync(odeme.Id, "test");
        var d = await kredi.DetayAsync(7);
        Assert.Equal(30_000m, d.Ozet.KalanAnapara);
        Assert.Equal(TaksitDurum.Bekliyor, d.Plan[0].Durum);
        await using (var db = Db())
            Assert.Equal(-30_000m, await db.CariHareketleri.Where(h => h.CariId == bankaId).SumAsync(h => h.Borc - h.Alacak));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection(_b.MasterBaglanti);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID(N'{_b.Veritabani}') IS NOT NULL BEGIN ALTER DATABASE [{_b.Veritabani}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_b.Veritabani}]; END";
        await cmd.ExecuteNonQueryAsync();
    }
}
