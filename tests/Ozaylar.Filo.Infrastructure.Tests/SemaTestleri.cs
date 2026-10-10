using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Infrastructure.Veri;

namespace Ozaylar.Filo.Infrastructure.Tests;

[Collection(VeritabaniKoleksiyonu.Ad)]
public class SemaTestleri(VeritabaniFixture fx)
{
    [Fact]
    public async Task Veritabani_UTF8_Turkce_collation_ile_olusur()
    {
        await using var db = fx.Db();
        var c = await db.Database.SqlQueryRaw<string>("SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS varchar(100)) AS Value").SingleAsync();
        Assert.Equal(FiloDbContext.Collation, c);
    }

    [Fact]
    public async Task Hicbir_kolon_nvarchar_veya_nchar_degil()
    {
        await using var db = fx.Db();
        var kolonlar = await db.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME + '.' + COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS WHERE DATA_TYPE IN ('nvarchar','nchar','ntext') AND TABLE_NAME <> '__EFMigrationsHistory'")
            .ToListAsync();
        Assert.Empty(kolonlar);
    }

    [Fact]
    public async Task Hareket_tablolari_PAGE_digerleri_ROW_sikistirilmis()
    {
        await using var db = fx.Db();
        var satirlar = await db.Database.SqlQueryRaw<string>("""
            SELECT (t.name + ':' + p.data_compression_desc) COLLATE DATABASE_DEFAULT AS Value
            FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
            WHERE t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
            """).ToListAsync();
        Assert.NotEmpty(satirlar);
        foreach (var s in satirlar)
        {
            var (tablo, sik) = (s.Split(':')[0], s.Split(':')[1]);
            Assert.Equal(Sikistirma.PageTablolar.Contains(tablo) ? "PAGE" : "ROW", sik);
        }
    }

    [Fact]
    public async Task Turkce_buyuk_kucuk_harf_duyarsiz_arama_dogru_calisir()
    {
        await using var db = fx.Db();
        // Turkish collation: "IĞDIR" araması "Iğdır"ı bulur, "İzmir" araması "izmir" ile eşleşir.
        Assert.True(await db.Iller.AnyAsync(i => i.Ad == "IĞDIR"));
        Assert.True(await db.Iller.AnyAsync(i => i.Ad == "izmir"));
        Assert.Equal(81, await db.Iller.CountAsync());
    }

    [Fact]
    public async Task Sequence_ile_numara_alinir()
    {
        await using var db = fx.Db();
        var a = await db.SiradakiNumaraAsync("SeferNoSeq");
        var b = await db.SiradakiNumaraAsync("SeferNoSeq");
        Assert.Equal(a + 1, b);
    }

    [Fact]
    public async Task Hazirlik_admin_kullanicisi_ve_tum_izinli_yonetici_rolu_olusturur()
    {
        await using var db = fx.Db();
        var admin = await db.Kullanicilar.Include(k => k.Roller).SingleAsync(k => k.KullaniciAdi == "admin");
        Assert.True(admin.SifreDegistirmeli);
        var yoneticiIzin = await db.RolIzinler.CountAsync(r => r.RolId == 1);
        Assert.Equal(await db.Izinler.CountAsync(), yoneticiIzin);
        Assert.True(await db.RolIzinler.AnyAsync(r => r.RolId == 3));
    }

    [Fact]
    public async Task Plaka_tekil_ama_silinen_aracin_plakasi_tekrar_kullanilabilir()
    {
        await using (var db = fx.Db())
        {
            db.Araclar.Add(new Arac { Plaka = "34 TST 001" });
            await db.SaveChangesAsync();
        }
        await using (var db = fx.Db())
        {
            db.Araclar.Add(new Arac { Plaka = "34 TST 001" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = fx.Db())
        {
            var a = await db.Araclar.SingleAsync(x => x.Plaka == "34 TST 001");
            db.Araclar.Remove(a);
            await db.SaveChangesAsync();
            Assert.False(await db.Araclar.AnyAsync(x => x.Plaka == "34 TST 001"));
            Assert.True(await db.Araclar.IgnoreQueryFilters().AnyAsync(x => x.Plaka == "34 TST 001" && x.IsDeleted));
            db.Araclar.Add(new Arac { Plaka = "34 TST 001" });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Ayni_kaydi_iki_kullanici_degistirirse_ikincisi_cakisma_alir()
    {
        short id;
        await using (var db = fx.Db())
        {
            var d = new Dorse { Plaka = "34 DRS 900" };
            db.Dorseler.Add(d);
            await db.SaveChangesAsync();
            id = d.Id;
        }
        await using var db1 = fx.Db();
        await using var db2 = fx.Db();
        var k1 = await db1.Dorseler.SingleAsync(x => x.Id == id);
        var k2 = await db2.Dorseler.SingleAsync(x => x.Id == id);
        k1.Marka = "Tırsan";
        await db1.SaveChangesAsync();
        k2.Marka = "Krone";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Degisiklik_audit_loga_sadece_degisen_alanlarla_yazilir()
    {
        short id;
        await using (var db = fx.Db())
        {
            var d = new Dorse { Plaka = "34 AUD 1", Marka = "Eski" };
            db.Dorseler.Add(d);
            await db.SaveChangesAsync();
            id = d.Id;
            d.Marka = "Yeni";
            await db.SaveChangesAsync();
        }
        await using (var db = fx.Db())
        {
            var loglar = await db.AuditLoglar.Where(a => a.Tablo == "Dorse" && a.KayitId == id.ToString()).OrderBy(a => a.Id).ToListAsync();
            Assert.Equal(2, loglar.Count);
            Assert.Equal(AuditIslem.Ekle, loglar[0].Islem);
            Assert.Equal(AuditIslem.Guncelle, loglar[1].Islem);
            Assert.Contains("\"Marka\":[\"Eski\",\"Yeni\"]", loglar[1].Degisiklik);
            Assert.DoesNotContain("Plaka", loglar[1].Degisiklik);
            var d = await db.Dorseler.SingleAsync(x => x.Id == id);
            Assert.Equal((short)1, d.OlusturanId);
            Assert.Equal((short)1, d.GuncelleyenId);
        }
    }

    [Fact]
    public async Task Defter_kaydi_degistirilemez()
    {
        await using var db = fx.Db();
        var cari = new Cari { Kod = "T-DEF-1", Unvan = "Defter Test" };
        db.Cariler.Add(cari);
        await db.SaveChangesAsync();
        var h = new CariHareket { CariId = cari.Id, Tarih = new DateOnly(2026, 5, 1), Borc = 100, KaynakTip = KaynakTip.Manuel };
        db.CariHareketleri.Add(h);
        await db.SaveChangesAsync();
        h.Borc = 50;
        await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Kapali_doneme_kayit_girilemez()
    {
        await using (var db = fx.Db())
        {
            var ayar = await db.Ayarlar.SingleAsync(a => a.Anahtar == FiloDbContext.AyarKapanisTarihi);
            ayar.Deger = "2025-12-31";
            await db.SaveChangesAsync();
        }
        try
        {
            await using var db = fx.Db();
            var cari = new Cari { Kod = "T-DON-1", Unvan = "Dönem Test" };
            db.Cariler.Add(cari);
            await db.SaveChangesAsync();
            db.CariHareketleri.Add(new CariHareket { CariId = cari.Id, Tarih = new DateOnly(2025, 12, 31), Borc = 1, KaynakTip = KaynakTip.Manuel });
            var ex = await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
            Assert.Contains("dönem kapalı", ex.Message);
        }
        finally
        {
            await using var db = fx.Db();
            var ayar = await db.Ayarlar.SingleAsync(a => a.Anahtar == FiloDbContext.AyarKapanisTarihi);
            ayar.Deger = "";
            await db.SaveChangesAsync();
        }
    }
}
