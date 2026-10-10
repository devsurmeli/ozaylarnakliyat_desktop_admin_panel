using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record GirisSonucu(bool Basarili, string? Mesaj, bool SifreDegistirmeli);

public sealed class OturumServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISifreHasher hasher, ISaat saat)
{
    public const int MaksHataliGiris = 5;
    public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);

    public async Task<GirisSonucu> GirisAsync(string kullaniciAdi, string sifre, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var k = await db.Kullanicilar.Include(x => x.Roller).SingleOrDefaultAsync(x => x.KullaniciAdi == kullaniciAdi.Trim(), ct);
        const string hatali = "Kullanıcı adı veya şifre hatalı.";
        if (k is null) return new(false, hatali, false);
        if (!k.Aktif) return new(false, "Kullanıcı pasif. Yöneticinize başvurun.", false);
        var simdi = saat.UtcNow;
        if (k.KilitBitis > simdi)
            return new(false, $"Çok sayıda hatalı deneme. Hesap {(k.KilitBitis.Value - simdi).Minutes + 1} dakika kilitli.", false);

        var (dogru, yeniden) = hasher.Dogrula(k.SifreHash, sifre);
        if (!dogru)
        {
            k.BasarisizGiris++;
            if (k.BasarisizGiris >= MaksHataliGiris)
            {
                k.KilitBitis = simdi + KilitSuresi;
                k.BasarisizGiris = 0;
            }
            await db.SaveChangesAsync(ct);
            return new(false, hatali, false);
        }

        if (yeniden) k.SifreHash = hasher.Hashle(sifre);
        k.BasarisizGiris = 0;
        k.KilitBitis = null;
        k.SonGiris = simdi;
        var rolIdleri = k.Roller.Select(r => r.RolId).ToList();
        var izinler = await db.RolIzinler.Where(r => rolIdleri.Contains(r.RolId)).Select(r => r.Izin!.Kod).Distinct().ToListAsync(ct);
        oturum.Baslat(k.Id, k.AdSoyad, izinler);
        await db.SaveChangesAsync(ct);
        return new(true, null, k.SifreDegistirmeli);
    }

    public async Task SifreDegistirAsync(string eski, string yeni, CancellationToken ct = default)
    {
        if (!oturum.GirisYapildi) throw new DomainException("Oturum açılmamış.");
        SifreKurali(yeni);
        await using var db = factory.Olustur();
        var k = await db.Kullanicilar.SingleAsync(x => x.Id == oturum.KullaniciId, ct);
        if (!hasher.Dogrula(k.SifreHash, eski).Dogru) throw new DomainException("Mevcut şifre hatalı.");
        if (eski == yeni) throw new DomainException("Yeni şifre eskisiyle aynı olamaz.");
        k.SifreHash = hasher.Hashle(yeni);
        k.SifreDegistirmeli = false;
        await db.KaydetAsync(ct);
    }

    public static void SifreKurali(string sifre)
    {
        if (sifre.Length < 8 || !sifre.Any(char.IsDigit) || !sifre.Any(char.IsLetter))
            throw new DomainException("Şifre en az 8 karakter olmalı, harf ve rakam içermelidir.");
    }

    public void Cikis() => oturum.Kapat();
}

public sealed record KullaniciSatir(short Id, string KullaniciAdi, string AdSoyad, string? Eposta, bool Aktif, DateTime? SonGiris, string Roller, bool Kilitli);

public sealed class KullaniciServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISifreHasher hasher, ISaat saat)
{
    public async Task<List<KullaniciSatir>> ListeAsync(CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.KullaniciYonet);
        await using var db = factory.Olustur();
        var simdi = saat.UtcNow;
        var ham = await db.Kullanicilar.AsNoTracking().OrderBy(k => k.KullaniciAdi)
            .Select(k => new { k.Id, k.KullaniciAdi, k.AdSoyad, k.Eposta, k.Aktif, k.SonGiris, Roller = k.Roller.Select(r => r.Rol!.Ad).ToList(), k.KilitBitis })
            .ToListAsync(ct);
        return ham.Select(k => new KullaniciSatir(k.Id, k.KullaniciAdi, k.AdSoyad, k.Eposta, k.Aktif, k.SonGiris, string.Join(", ", k.Roller), k.KilitBitis > simdi)).ToList();
    }

    public async Task<(Kullanici Kullanici, List<byte> RolIdleri)> GetirAsync(short id, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.KullaniciYonet);
        await using var db = factory.Olustur();
        var k = await db.Kullanicilar.AsNoTracking().Include(x => x.Roller).SingleAsync(x => x.Id == id, ct);
        return (k, k.Roller.Select(r => r.RolId).ToList());
    }

    /// <summary>Kullanıcı ekler/günceller. Yeni kullanıcıda geçici şifre zorunludur ve ilk girişte değiştirilir.</summary>
    public async Task<short> KaydetAsync(Kullanici k, IReadOnlyList<byte> rolIdleri, string? geciciSifre, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.KullaniciYonet);
        k.KullaniciAdi = k.KullaniciAdi.Trim().ToLower(new CultureInfo("tr-TR"));
        if (k.KullaniciAdi.Length < 3) throw new DomainException("Kullanıcı adı en az 3 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(k.AdSoyad)) throw new DomainException("Ad soyad zorunludur.");
        if (rolIdleri.Count == 0) throw new DomainException("En az bir rol seçilmelidir.");
        await using var db = factory.Olustur();
        Kullanici hedef;
        if (k.Id == 0)
        {
            if (string.IsNullOrEmpty(geciciSifre)) throw new DomainException("Yeni kullanıcı için geçici şifre girilmelidir.");
            OturumServisi.SifreKurali(geciciSifre);
            k.SifreHash = hasher.Hashle(geciciSifre);
            k.SifreDegistirmeli = true;
            db.Kullanicilar.Add(k);
            hedef = k;
        }
        else
        {
            hedef = await db.Kullanicilar.Include(x => x.Roller).SingleAsync(x => x.Id == k.Id, ct);
            if (hedef.Id == oturum.KullaniciId && !k.Aktif) throw new DomainException("Kendi hesabınızı pasife alamazsınız.");
            db.Entry(hedef).Property(x => x.RowVer).OriginalValue = k.RowVer;
            hedef.KullaniciAdi = k.KullaniciAdi;
            hedef.AdSoyad = k.AdSoyad.Trim();
            hedef.Eposta = Metin.Temizle(k.Eposta);
            hedef.Aktif = k.Aktif;
            if (!string.IsNullOrEmpty(geciciSifre))
            {
                OturumServisi.SifreKurali(geciciSifre);
                hedef.SifreHash = hasher.Hashle(geciciSifre);
                hedef.SifreDegistirmeli = true;
                hedef.KilitBitis = null;
                hedef.BasarisizGiris = 0;
            }
            hedef.Roller.RemoveAll(r => !rolIdleri.Contains(r.RolId));
        }
        foreach (var r in rolIdleri.Where(r => hedef.Roller.All(x => x.RolId != r)))
            hedef.Roller.Add(new KullaniciRol { RolId = r });
        await YoneticiKalmaliAsync(db, hedef, rolIdleri, ct);
        await db.KaydetAsync(ct);
        return hedef.Id;
    }

    public async Task KilitAcAsync(short id, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.KullaniciYonet);
        await using var db = factory.Olustur();
        var k = await db.Kullanicilar.SingleAsync(x => x.Id == id, ct);
        k.KilitBitis = null;
        k.BasarisizGiris = 0;
        await db.KaydetAsync(ct);
    }

    /// <summary>Sistemde en az bir aktif yönetici kalmalıdır.</summary>
    private static async Task YoneticiKalmaliAsync(IFiloDbContext db, Kullanici hedef, IReadOnlyList<byte> rolIdleri, CancellationToken ct)
    {
        var yoneticiRol = await db.Roller.Where(r => r.Sistem).Select(r => r.Id).FirstAsync(ct);
        var buYonetici = hedef.Aktif && rolIdleri.Contains(yoneticiRol);
        if (buYonetici) return;
        var digerYonetici = await db.KullaniciRoller.AnyAsync(r => r.RolId == yoneticiRol && r.KullaniciId != hedef.Id && r.Kullanici!.Aktif, ct);
        if (!digerYonetici) throw new DomainException("Sistemde en az bir aktif yönetici kalmalıdır.");
    }
}

public sealed record IzinSecim(short Id, string Kod, string Grup, string Ad)
{
    public bool Secili { get; set; }
}

public sealed class RolServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum)
{
    public async Task<List<Rol>> RollerAsync(CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Roller.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
    }

    public async Task<List<IzinSecim>> IzinlerAsync(byte rolId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.KullaniciYonet);
        await using var db = factory.Olustur();
        var secili = await db.RolIzinler.Where(r => r.RolId == rolId).Select(r => r.IzinId).ToListAsync(ct);
        var liste = await db.Izinler.AsNoTracking().OrderBy(i => i.Grup).ThenBy(i => i.Kod).Select(i => new IzinSecim(i.Id, i.Kod, i.Grup, i.Ad)).ToListAsync(ct);
        foreach (var i in liste) i.Secili = secili.Contains(i.Id);
        return liste;
    }

    public async Task<byte> KaydetAsync(byte rolId, string ad, string? aciklama, IEnumerable<short> izinIdleri, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.KullaniciYonet);
        if (string.IsNullOrWhiteSpace(ad)) throw new DomainException("Rol adı zorunludur.");
        await using var db = factory.Olustur();
        Rol rol;
        if (rolId == 0)
        {
            rol = new Rol { Ad = ad.Trim(), Aciklama = aciklama };
            db.Roller.Add(rol);
        }
        else
        {
            rol = await db.Roller.Include(r => r.Izinler).SingleAsync(r => r.Id == rolId, ct);
            if (rol.Sistem) throw new DomainException("Yönetici rolünün izinleri değiştirilemez (her zaman tüm yetkiler).");
            rol.Ad = ad.Trim();
            rol.Aciklama = aciklama;
            rol.Izinler.Clear();
        }
        foreach (var i in izinIdleri.Distinct()) rol.Izinler.Add(new RolIzin { IzinId = i });
        await db.KaydetAsync(ct);
        return rol.Id;
    }

    public async Task SilAsync(byte rolId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.KullaniciYonet);
        await using var db = factory.Olustur();
        var rol = await db.Roller.SingleAsync(r => r.Id == rolId, ct);
        if (rol.Sistem) throw new DomainException("Sistem rolü silinemez.");
        if (await db.KullaniciRoller.AnyAsync(k => k.RolId == rolId, ct)) throw new DomainException("Bu role atanmış kullanıcılar var.");
        db.Roller.Remove(rol);
        await db.KaydetAsync(ct);
    }
}

public sealed class AyarServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum)
{
    public const string KapanisTarihi = "Donem.KapanisTarihi";
    public const string SonYedek = "Yedek.SonBasarili";

    public async Task<Dictionary<string, string?>> TumuAsync(CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Ayarlar.AsNoTracking().ToDictionaryAsync(a => a.Anahtar, a => a.Deger, ct);
    }

    public async Task<string?> OkuAsync(string anahtar, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Ayarlar.AsNoTracking().Where(a => a.Anahtar == anahtar).Select(a => a.Deger).FirstOrDefaultAsync(ct);
    }

    public async Task YazAsync(string anahtar, string? deger, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.AyarYonet);
        await using var db = factory.Olustur();
        var a = await db.Ayarlar.SingleOrDefaultAsync(x => x.Anahtar == anahtar, ct);
        if (a is null) db.Ayarlar.Add(new Ayar { Anahtar = anahtar, Deger = deger });
        else a.Deger = deger;
        await db.KaydetAsync(ct);
    }

    public async Task<DateOnly?> KapanisTarihiAsync(CancellationToken ct = default) =>
        DateOnly.TryParse(await OkuAsync(KapanisTarihi, ct), CultureInfo.InvariantCulture, out var t) ? t : null;

    /// <summary>Dönem kilidi: bu tarih ve öncesine finansal kayıt girilemez/değiştirilemez.</summary>
    public Task KapanisTarihiYazAsync(DateOnly? tarih, CancellationToken ct = default) =>
        YazAsync(KapanisTarihi, tarih?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", ct);

    public async Task<DateTime?> SonYedekAsync(CancellationToken ct = default) =>
        DateTime.TryParse(await OkuAsync(SonYedek, ct), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t) ? t : null;
}

public sealed record AuditSatir(long Id, DateTime Tarih, string? Kullanici, string Tablo, string KayitId, AuditIslem Islem, string? Degisiklik);

public sealed class AuditServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum)
{
    public async Task<List<AuditSatir>> ListeAsync(DateTime basUtc, DateTime bitUtc, string? tablo, short? kullaniciId, string? kayitId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.AuditGor);
        await using var db = factory.Olustur();
        var q = db.AuditLoglar.AsNoTracking().Where(a => a.Tarih >= basUtc && a.Tarih <= bitUtc);
        if (!string.IsNullOrWhiteSpace(tablo)) q = q.Where(a => a.Tablo == tablo);
        if (kullaniciId is not null) q = q.Where(a => a.KullaniciId == kullaniciId);
        if (!string.IsNullOrWhiteSpace(kayitId)) q = q.Where(a => a.KayitId == kayitId);
        return await q.OrderByDescending(a => a.Id).Take(3000)
            .Select(a => new AuditSatir(a.Id, a.Tarih, db.Kullanicilar.IgnoreQueryFilters().Where(k => k.Id == a.KullaniciId).Select(k => k.AdSoyad).FirstOrDefault(),
                a.Tablo, a.KayitId, a.Islem, a.Degisiklik))
            .ToListAsync(ct);
    }
}

public sealed class DovizServisi(IFiloDbContextFactory factory)
{
    /// <summary>Tarihteki (yoksa önceki en yakın) TCMB döviz satış kuru.</summary>
    public async Task<decimal?> KurAsync(ParaBirimi pb, DateOnly tarih, CancellationToken ct = default)
    {
        if (pb == ParaBirimi.TRY) return 1m;
        await using var db = factory.Olustur();
        return await db.DovizKurlari.AsNoTracking().Where(k => k.ParaBirimi == pb && k.Tarih <= tarih)
            .OrderByDescending(k => k.Tarih).Select(k => (decimal?)k.Satis).FirstOrDefaultAsync(ct);
    }

    public async Task KaydetAsync(DateOnly tarih, IEnumerable<(ParaBirimi Pb, decimal Alis, decimal Satis)> kurlar, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        foreach (var (pb, alis, satis) in kurlar)
        {
            var k = await db.DovizKurlari.SingleOrDefaultAsync(x => x.Tarih == tarih && x.ParaBirimi == pb, ct);
            if (k is null) db.DovizKurlari.Add(new DovizKur { Tarih = tarih, ParaBirimi = pb, Alis = alis, Satis = satis });
            else { k.Alis = alis; k.Satis = satis; }
        }
        await db.SaveChangesAsync(ct);
    }
}
