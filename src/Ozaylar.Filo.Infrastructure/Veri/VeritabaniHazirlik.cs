using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Veri;

/// <summary>
/// Kurulum ve sunucu servisi tarafından (yönetici SQL yetkisiyle) çalıştırılır:
/// migration, izin senkronizasyonu, ilk kullanıcı, istemci SQL login'i.
/// İstemciler (filo_app) şema değiştiremez.
/// </summary>
public sealed class VeritabaniHazirlik(BaglantiBilgisi yonetici, ISifreHasher hasher)
{
    public const string UygulamaLogin = "filo_app";
    public const string IlkKullaniciAdi = "admin";
    public const string IlkSifre = "admin";

    public async Task<IReadOnlyList<string>> BekleyenMigrationlarAsync(CancellationToken ct = default)
    {
        await using var db = new FiloDbContext(DbSecenekleri.Ana(yonetici.AnaBaglanti));
        return (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
    }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await using (var db = new FiloDbContext(DbSecenekleri.Ana(yonetici.AnaBaglanti)))
            await db.Database.MigrateAsync(ct);
        await using (var ek = new EkDbContext(DbSecenekleri.Ek(yonetici.EkBaglanti)))
            await ek.Database.MigrateAsync(ct);
    }

    public async Task VerileriHazirlaAsync(CancellationToken ct = default)
    {
        await using var db = new FiloDbContext(DbSecenekleri.Ana(yonetici.AnaBaglanti));

        // 1) İzinler: koddaki sabitlerle eşitle.
        var mevcut = await db.Izinler.ToDictionaryAsync(i => i.Kod, ct);
        foreach (var (kod, grup, ad) in Izinler.Tumu)
        {
            if (mevcut.TryGetValue(kod, out var i)) { i.Ad = ad; i.Grup = grup; }
            else db.Izinler.Add(new Izin { Kod = kod, Ad = ad, Grup = grup });
        }
        await db.SaveChangesAsync(ct);

        // 2) Rol izinleri: Yönetici her zaman tümü; diğer roller yalnızca hiç izni yoksa varsayılanla doldurulur.
        var izinIdleri = await db.Izinler.ToDictionaryAsync(i => i.Kod, i => i.Id, ct);
        var roller = await db.Roller.Include(r => r.Izinler).ToListAsync(ct);
        foreach (var rol in roller)
        {
            IEnumerable<string> hedef = rol.Id == Seed.RolYonetici
                ? izinIdleri.Keys
                : rol.Izinler.Count == 0 && VarsayilanRolIzinleri.TryGetValue(rol.Ad, out var v) ? v : [];
            foreach (var kod in hedef)
                if (rol.Izinler.All(ri => ri.IzinId != izinIdleri[kod]))
                    rol.Izinler.Add(new RolIzin { RolId = rol.Id, IzinId = izinIdleri[kod] });
        }
        await db.SaveChangesAsync(ct);

        // 3) İlk yönetici kullanıcı.
        if (!await db.Kullanicilar.IgnoreQueryFilters().AnyAsync(ct))
        {
            var k = new Kullanici
            {
                KullaniciAdi = IlkKullaniciAdi,
                AdSoyad = "Sistem Yöneticisi",
                SifreHash = hasher.Hashle(IlkSifre),
                SifreDegistirmeli = true,
            };
            k.Roller.Add(new KullaniciRol { RolId = Seed.RolYonetici });
            db.Kullanicilar.Add(k);
        }

        // 4) Varsayılan kasa ve yurtiçi harcırah tarifesi.
        if (!await db.KasaHesaplari.IgnoreQueryFilters().AnyAsync(ct))
            db.KasaHesaplari.Add(new KasaHesap { Ad = "Merkez Kasa", HesapTipi = KasaHesapTipi.Kasa, AcilisTarihi = new SistemSaati().Bugun });
        if (!await db.HarcirahTarifeleri.AnyAsync(ct))
            db.HarcirahTarifeleri.Add(new HarcirahTarife
            {
                GunlukTutar = 0, GecerlilikBaslangic = new DateOnly(2026, 1, 1),
                Aciklama = "Yurtiçi günlük harcırah — tutarı Tanımlar > Harcırah Tarifeleri'nden girin",
            });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>İstemcilerin kullanacağı kısıtlı SQL login'ini oluşturur/şifresini günceller (DDL yetkisi yok).</summary>
    public async Task UygulamaLoginiOlusturAsync(string sifre, CancellationToken ct = default)
    {
        var sifreLit = sifre.Replace("'", "''");
        await using (var c = new SqlConnection(yonetici.MasterBaglanti))
        {
            await c.OpenAsync(ct);
            await Calistir(c, $"""
                IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{UygulamaLogin}')
                    CREATE LOGIN [{UygulamaLogin}] WITH PASSWORD = N'{sifreLit}', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [{yonetici.Veritabani}];
                ELSE
                    ALTER LOGIN [{UygulamaLogin}] WITH PASSWORD = N'{sifreLit}';
                """, ct);
        }
        foreach (var db in new[] { yonetici.AnaBaglanti, yonetici.EkBaglanti })
        {
            await using var c = new SqlConnection(db);
            await c.OpenAsync(ct);
            await Calistir(c, $"""
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{UygulamaLogin}')
                    CREATE USER [{UygulamaLogin}] FOR LOGIN [{UygulamaLogin}];
                ALTER ROLE db_datareader ADD MEMBER [{UygulamaLogin}];
                ALTER ROLE db_datawriter ADD MEMBER [{UygulamaLogin}];
                GRANT UPDATE ON SCHEMA::dbo TO [{UygulamaLogin}];   -- NEXT VALUE FOR (sequence) için
                DENY ALTER ON SCHEMA::dbo TO [{UygulamaLogin}];
                """, ct);
        }
    }

    private static async Task Calistir(SqlConnection c, string sql, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static readonly Dictionary<string, string[]> VarsayilanRolIzinleri = new()
    {
        ["Muhasebe"] =
        [
            Izinler.TanimGor, Izinler.TanimYaz, Izinler.FiloGor, Izinler.SeferGor, Izinler.SeferFiyatGor,
            Izinler.FinansGor, Izinler.FinansYaz, Izinler.FinansIptal,
            Izinler.SoforHesapGor, Izinler.SoforHesapYaz, Izinler.SoforHesapOnay,
            Izinler.GiderGor, Izinler.GiderYaz, Izinler.IceAktar, Izinler.BakimGor,
            Izinler.FinansmanGor, Izinler.FinansmanYaz, Izinler.RaporOperasyon, Izinler.RaporFinans,
        ],
        ["Operasyon"] =
        [
            Izinler.TanimGor, Izinler.FiloGor, Izinler.FiloYaz, Izinler.SeferGor, Izinler.SeferYaz,
            Izinler.GiderGor, Izinler.GiderYaz, Izinler.BakimGor, Izinler.BakimYaz, Izinler.RaporOperasyon,
        ],
        ["Kasiyer"] =
        [
            Izinler.TanimGor, Izinler.FiloGor, Izinler.SeferGor, Izinler.SoforHesapGor, Izinler.SoforHesapYaz,
            Izinler.GiderGor, Izinler.GiderYaz,
        ],
    };
}
