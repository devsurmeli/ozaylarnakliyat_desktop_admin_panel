using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record GiderFiltre(DateOnly Baslangic, DateOnly Bitis, short? AracId = null, int? SeferId = null, short? SoforId = null, string? Metin = null);

public sealed record MasrafSatir(int Id, DateOnly Tarih, string Tur, MasrafMerkezi Merkez, string? Sefer, string? Plaka, string? Sofor,
    decimal Tutar, ParaBirimi ParaBirimi, decimal TutarTL, OdemeSekli OdemeSekli, string? Aciklama, bool Onayli, bool Mahsuplu, bool Iptal)
{
    public string MerkezAd => Merkez.Ad();
    public string OdemeAd => OdemeSekli.Ad();
    public string PB => ParaBirimi.Ad();
}

public sealed record YakitSatir(int Id, DateTime Zaman, string Plaka, string? Sefer, string? Sofor, int Km, decimal Litre, decimal BirimFiyat,
    decimal Tutar, ParaBirimi ParaBirimi, bool FullDepo, string? Istasyon, OdemeSekli OdemeSekli, bool Aktarim, bool Iptal)
{
    public string OdemeAd => OdemeSekli.Ad();
}

public sealed record TuketimRaporSatir(short AracId, string Plaka, int Km, decimal Litre, decimal? LitreYuzKm, decimal? Beklenen, decimal? SapmaYuzde)
{
    public bool Alarm { get; init; }
}

public sealed record HgsSatir(int Id, DateTime Zaman, string Plaka, string? Giris, string? Cikis, decimal Tutar, string? Sefer);

public sealed record CezaSatir(int Id, DateOnly Tarih, string? Plaka, string? Sofor, string CezaTuru, decimal Tutar, decimal? IndirimliTutar,
    DateOnly? OdemeTarihi, bool RucuEdilsin);

public sealed class GiderServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    // ------------------------------- Masraf -------------------------------

    public async Task<List<MasrafSatir>> MasraflarAsync(GiderFiltre f, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderGor);
        await using var db = factory.Olustur();
        var q = db.Masraflar.AsNoTracking().Where(m => m.Tarih >= f.Baslangic && m.Tarih <= f.Bitis);
        if (f.AracId is not null) q = q.Where(m => m.AracId == f.AracId);
        if (f.SeferId is not null) q = q.Where(m => m.SeferId == f.SeferId);
        if (f.SoforId is not null) q = q.Where(m => m.SoforId == f.SoforId);
        if (!string.IsNullOrWhiteSpace(f.Metin)) q = q.Where(m => m.Aciklama!.Contains(f.Metin) || m.BelgeNo!.Contains(f.Metin) || m.MasrafTuru!.Ad.Contains(f.Metin));
        var ham = await q.OrderByDescending(m => m.Tarih).ThenByDescending(m => m.Id)
            .Select(m => new
            {
                m.Id, m.Tarih, Tur = m.MasrafTuru!.Ad, m.Merkez, Sefer = m.Sefer != null ? m.Sefer.SeferNo : null,
                Plaka = m.Arac != null ? m.Arac.Plaka : m.Dorse != null ? m.Dorse.Plaka : null,
                Sofor = m.Sofor != null ? m.Sofor.Ad + " " + m.Sofor.Soyad : null,
                m.Tutar, m.ParaBirimi, m.Kur, m.OdemeSekli, m.Aciklama, m.Onayli, Mahsuplu = m.MahsupId != null, m.Iptal,
            }).Take(5000).ToListAsync(ct);
        return ham.Select(m => new MasrafSatir(m.Id, m.Tarih, m.Tur, m.Merkez, m.Sefer, m.Plaka, m.Sofor, m.Tutar, m.ParaBirimi,
            Para.TL(m.Tutar, m.Kur), m.OdemeSekli, m.Aciklama, m.Onayli, m.Mahsuplu, m.Iptal)).ToList();
    }

    public async Task<Masraf?> MasrafGetirAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Masraflar.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<Masraf> MasrafKaydetAsync(Masraf m, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderYaz);
        if (m.Tutar <= 0) throw new DomainException("Tutar sıfırdan büyük olmalıdır.");
        if (m.MasrafTuruId == 0) throw new DomainException("Masraf türü seçilmelidir.");
        if (m.ParaBirimi == ParaBirimi.TRY) m.Kur = 1;
        else if (m.Kur <= 0) throw new DomainException("Dövizli masraf için kur girilmelidir.");
        m.Aciklama = Metin.Temizle(m.Aciklama);
        m.BelgeNo = Metin.Temizle(m.BelgeNo);

        await using var db = factory.Olustur();
        await MerkezTamamlaAsync(db, m, ct);
        OdemeKontrol(m.OdemeSekli, m.SoforId, m.KasaHesapId, m.CariId);
        // Şoförün nakit harcaması onay gerektirir (fişsiz limit üstü ise); ofis girişleri doğrudan onaylıdır.
        if (m.Id == 0 && m.OdemeSekli == OdemeSekli.SoforNakit && !oturum.Yetkili(Izinler.SoforHesapOnay))
            m.Onayli = false;
        if (m.OdemeSekli == OdemeSekli.Kasa) await KasaKontrolAsync(db, m.KasaHesapId!.Value, m.Tutar, m.ParaBirimi, m.Id, KaynakTip.Masraf, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (m.Id == 0)
        {
            m.MahsupId = null;
            m.Iptal = false;
            db.Masraflar.Add(m);
        }
        else
        {
            var eski = await db.Masraflar.AsNoTracking().SingleAsync(x => x.Id == m.Id, ct);
            if (eski.Iptal) throw new DomainException("İptal edilmiş masraf düzenlenemez.");
            if (eski.MahsupId is not null) throw new DomainException("Mahsubu yapılmış masraf düzenlenemez; önce mahsubu iptal edin.");
            await Defter.TersKayitAsync(db, KaynakTip.Masraf, m.Id, saat.Bugun, "masraf düzeltildi", ct);
            await db.BirlestirAsync(m, ct);
        }
        await db.KaydetAsync(ct);
        var tur = await db.MasrafTurleri.Where(t => t.Id == m.MasrafTuruId).Select(t => t.Ad).SingleAsync(ct);
        await OdemeIsleAsync(db, KaynakTip.Masraf, m.Id, m.Tarih, m.Tutar, m.ParaBirimi, m.Kur, m.OdemeSekli, m.KasaHesapId, m.CariId, m.SoforId,
            $"{tur}{(m.Aciklama is null ? "" : " - " + m.Aciklama)}", m.AracId, m.SeferId, ct);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return m;
    }

    public async Task MasrafIptalAsync(int id, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        var m = await db.Masraflar.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Masraf bulunamadı.");
        if (m.MahsupId is not null) throw new DomainException("Mahsubu yapılmış masraf iptal edilemez; önce mahsubu iptal edin.");
        if (m.Iptal) return;
        m.Iptal = true;
        await Defter.TersKayitAsync(db, KaynakTip.Masraf, m.Id, saat.Bugun, neden, ct);
        await db.KaydetAsync(ct);
    }

    /// <summary>Merkez = Sefer ise araç/dorse seferden doldurulur (araç bazlı raporlar için).</summary>
    private static async Task MerkezTamamlaAsync(IFiloDbContext db, Masraf m, CancellationToken ct)
    {
        switch (m.Merkez)
        {
            case MasrafMerkezi.Sefer:
                if (m.SeferId is null) throw new DomainException("Sefer masrafında sefer seçilmelidir.");
                var s = await db.Seferler.AsNoTracking().Where(x => x.Id == m.SeferId)
                    .Select(x => new { x.AracId, x.DorseId, x.Durum, Sofor = x.Soforler.Where(y => y.Birincil).Select(y => (short?)y.SoforId).FirstOrDefault() })
                    .SingleOrDefaultAsync(ct) ?? throw new DomainException("Sefer bulunamadı.");
                if (s.Durum == SeferDurum.Iptal) throw new DomainException("İptal edilmiş sefere masraf girilemez.");
                m.AracId = s.AracId;
                m.DorseId = s.DorseId;
                if (m.OdemeSekli == OdemeSekli.SoforNakit) m.SoforId ??= s.Sofor;
                break;
            case MasrafMerkezi.Arac:
                if (m.AracId is null) throw new DomainException("Araç masrafında araç seçilmelidir.");
                m.SeferId = null;
                break;
            case MasrafMerkezi.Dorse:
                if (m.DorseId is null) throw new DomainException("Dorse masrafında dorse seçilmelidir.");
                m.SeferId = null;
                m.AracId = null;
                break;
            case MasrafMerkezi.Genel:
                m.SeferId = null;
                m.AracId = null;
                m.DorseId = null;
                break;
            default:
                throw new DomainException("Maliyet merkezi seçilmelidir.");
        }
    }

    private static void OdemeKontrol(OdemeSekli o, short? soforId, byte? kasaId, int? cariId)
    {
        switch (o)
        {
            case OdemeSekli.SoforNakit when soforId is null: throw new DomainException("Şoför nakit ödemesinde şoför seçilmelidir.");
            case OdemeSekli.Kasa when kasaId is null: throw new DomainException("Kasa/banka seçilmelidir.");
            case OdemeSekli.Cari when cariId is null: throw new DomainException("Veresiye ödemede tedarikçi cari seçilmelidir.");
        }
    }

    private static async Task KasaKontrolAsync(IFiloDbContext db, byte kasaId, decimal tutar, ParaBirimi pb, int mevcutId, KaynakTip tip, CancellationToken ct)
    {
        var kasaPb = await db.KasaHesaplari.Where(k => k.Id == kasaId).Select(k => k.ParaBirimi).SingleAsync(ct);
        if (kasaPb != pb) throw new DomainException($"Seçilen kasa {kasaPb.Ad()} hesabı; masraf para birimi ile aynı olmalıdır.");
        // Düzeltmede eski çıkış iade edileceği için bakiyeye eklenir.
        var eskiCikis = mevcutId == 0 ? 0 : await db.KasaHareketleri.Where(h => h.KaynakTip == tip && h.KaynakId == mevcutId).SumAsync(h => h.Cikis - h.Giris, ct);
        await Defter.KasaCikisKontrolAsync(db, kasaId, tutar - eskiCikis, ct);
    }

    /// <summary>Ödeme şekline göre defter etkisi.</summary>
    private static async Task OdemeIsleAsync(IFiloDbContext db, KaynakTip tip, int id, DateOnly tarih, decimal tutar, ParaBirimi pb, decimal kur,
        OdemeSekli odeme, byte? kasaId, int? cariId, short? soforId, string aciklama, short? aracId, int? seferId, CancellationToken ct)
    {
        switch (odeme)
        {
            case OdemeSekli.SoforNakit:
                var soforCari = await db.Soforler.Where(s => s.Id == soforId).Select(s => s.CariId).SingleAsync(ct);
                Defter.Cari(db, soforCari, tarih, 0, tutar, tip, id, aciklama, pb, kur, aracId: aracId, seferId: seferId);
                break;
            case OdemeSekli.Kasa:
                Defter.Kasa(db, kasaId!.Value, tarih, 0, tutar, tip, id, aciklama, cariId, kur);
                break;
            case OdemeSekli.Cari:
            case OdemeSekli.Kart when cariId is not null:
                Defter.Cari(db, cariId!.Value, tarih, 0, tutar, tip, id, aciklama, pb, kur, aracId: aracId, seferId: seferId);
                break;
        }
    }

    // ------------------------------- Yakıt -------------------------------

    public async Task<List<YakitSatir>> YakitlarAsync(GiderFiltre f, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderGor);
        await using var db = factory.Olustur();
        var bas = f.Baslangic.ToDateTime(TimeOnly.MinValue);
        var bit = f.Bitis.ToDateTime(TimeOnly.MaxValue);
        var q = db.Yakitlar.AsNoTracking().Where(y => y.AlimZamani >= bas && y.AlimZamani <= bit);
        if (f.AracId is not null) q = q.Where(y => y.AracId == f.AracId);
        if (f.SeferId is not null) q = q.Where(y => y.SeferId == f.SeferId);
        if (f.SoforId is not null) q = q.Where(y => y.SoforId == f.SoforId);
        return await q.OrderByDescending(y => y.AlimZamani)
            .Select(y => new YakitSatir(y.Id, y.AlimZamani, y.Arac!.Plaka, y.Sefer != null ? y.Sefer.SeferNo : null,
                y.Sofor != null ? y.Sofor.Ad + " " + y.Sofor.Soyad : null, y.Km, y.Litre, y.BirimFiyat, y.Tutar, y.ParaBirimi,
                y.FullDepo, y.IstasyonAdi, y.OdemeSekli, y.IceAktarimId != null, y.Iptal))
            .Take(5000).ToListAsync(ct);
    }

    public async Task<Yakit?> YakitGetirAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Yakitlar.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<Yakit> YakitKaydetAsync(Yakit y, bool uyarilariOnayla = false, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderYaz);
        if (y.AracId == 0) throw new DomainException("Araç seçilmelidir.");
        if (y.Litre <= 0) throw new DomainException("Litre sıfırdan büyük olmalıdır.");
        if (y.Tutar <= 0 && y.BirimFiyat > 0) y.Tutar = Para.Yuvarla(y.Litre * y.BirimFiyat);
        if (y.BirimFiyat <= 0 && y.Tutar > 0) y.BirimFiyat = Math.Round(y.Tutar / y.Litre, 3);
        if (y.Tutar <= 0) throw new DomainException("Tutar veya birim fiyat girilmelidir.");
        if (y.ParaBirimi == ParaBirimi.TRY) y.Kur = 1;
        else if (y.Kur <= 0) throw new DomainException("Dövizli yakıt için kur girilmelidir.");
        OdemeKontrol(y.OdemeSekli, y.SoforId, y.KasaHesapId, y.IstasyonCariId);

        await using var db = factory.Olustur();
        var uyarilar = new Uyarilar();
        var onceki = await db.Yakitlar.Where(x => x.AracId == y.AracId && x.Id != y.Id && !x.Iptal && x.AlimZamani < y.AlimZamani)
            .OrderByDescending(x => x.AlimZamani).Select(x => (int?)x.Km).FirstOrDefaultAsync(ct);
        if (onceki is not null && y.Km < onceki) uyarilar.Ekle($"Km ({y.Km:N0}) aracın önceki yakıt alımındaki km'den ({onceki:N0}) küçük.");
        var depo = await db.Araclar.Where(a => a.Id == y.AracId).Select(a => a.YakitDepoLitre).SingleAsync(ct);
        if (depo is > 0 && y.Litre > depo * 1.05m) uyarilar.Ekle($"Alınan yakıt ({y.Litre:N0} L) depo hacmini ({depo} L) aşıyor.");
        uyarilar.OnaylanmadiysaFirlat(uyarilariOnayla);

        if (y.SeferId is null) y.SeferId = await SeferBulAsync(db, y.AracId, y.AlimZamani, ct);
        if (y.OdemeSekli == OdemeSekli.Kasa) await KasaKontrolAsync(db, y.KasaHesapId!.Value, y.Tutar, y.ParaBirimi, y.Id, KaynakTip.Yakit, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (y.Id == 0)
        {
            y.MahsupId = null;
            y.Iptal = false;
            db.Yakitlar.Add(y);
        }
        else
        {
            var eski = await db.Yakitlar.AsNoTracking().SingleAsync(x => x.Id == y.Id, ct);
            if (eski.Iptal || eski.MahsupId is not null) throw new DomainException("İptal edilmiş veya mahsubu yapılmış yakıt kaydı düzenlenemez.");
            if (eski.IceAktarimId is not null) throw new DomainException("Ekstreden aktarılan kayıt düzenlenemez; iptal edip yeniden girin.");
            await Defter.TersKayitAsync(db, KaynakTip.Yakit, y.Id, saat.Bugun, "yakıt düzeltildi", ct);
            await db.BirlestirAsync(y, ct);
        }
        var arac = await db.Araclar.SingleAsync(a => a.Id == y.AracId, ct);
        if (y.Km > arac.GuncelKm) arac.GuncelKm = y.Km;
        await db.KaydetAsync(ct);
        AracKm.Kaydet(db, y.AracId, y.AlimZamani, y.Km, KmKaynak.Yakit, y.Id);
        await OdemeIsleAsync(db, KaynakTip.Yakit, y.Id, y.Tarih, y.Tutar, y.ParaBirimi, y.Kur, y.OdemeSekli, y.KasaHesapId, y.IstasyonCariId, y.SoforId,
            $"Yakıt {arac.Plaka} {y.Litre:N2} L {y.IstasyonAdi}", y.AracId, y.SeferId, ct);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return y;
    }

    public async Task YakitIptalAsync(int id, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        var y = await db.Yakitlar.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Yakıt kaydı bulunamadı.");
        if (y.MahsupId is not null) throw new DomainException("Mahsubu yapılmış kayıt iptal edilemez.");
        if (y.Iptal) return;
        y.Iptal = true;
        await Defter.TersKayitAsync(db, KaynakTip.Yakit, y.Id, saat.Bugun, neden, ct);
        // Ekstreden aktarılan satırın tutarı ekstre toplamıyla cariye yazılmıştı: o kısmı düş.
        if (y.IceAktarimId is not null)
        {
            var ekstreCari = await db.IceAktarimlar.Where(i => i.Id == y.IceAktarimId).Select(i => i.CariId).SingleAsync(ct);
            if (ekstreCari is not null)
                Defter.Cari(db, ekstreCari.Value, saat.Bugun, y.Tutar, 0, KaynakTip.IceAktarim, y.IceAktarimId,
                    $"Ekstre satırı iptal: yakıt #{y.Id} ({neden})", y.ParaBirimi, y.Kur);
        }
        await db.KaydetAsync(ct);
    }

    /// <summary>Aracın ilgili anda devam eden seferini bulur (yakıt/HGS otomatik eşleştirme).</summary>
    internal static Task<int?> SeferBulAsync(IFiloDbContext db, short aracId, DateTime an, CancellationToken ct)
    {
        var gun = DateOnly.FromDateTime(an);
        return db.Seferler.Where(s => s.AracId == aracId && s.Durum != SeferDurum.Iptal)
            .Where(s => (s.CikisTarihi != null ? s.CikisTarihi <= an : s.PlanlananTarih <= gun)
                        && (s.DonusTarihi != null ? s.DonusTarihi >= an
                            : s.TeslimTarihi != null ? s.TeslimTarihi >= an || s.Durum < SeferDurum.Kapatildi
                            : s.Durum < SeferDurum.Kapatildi))
            .OrderByDescending(s => s.PlanlananTarih).ThenByDescending(s => s.Id)
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>Araç bazında L/100km (depo-dolu yöntemi) ve beklenen tüketime göre sapma.</summary>
    public async Task<List<TuketimRaporSatir>> TuketimRaporuAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderGor);
        await using var db = factory.Olustur();
        var b = bas.ToDateTime(TimeOnly.MinValue);
        var e = bit.ToDateTime(TimeOnly.MaxValue);
        var alimlar = await db.Yakitlar.AsNoTracking().Where(y => !y.Iptal && y.AlimZamani >= b && y.AlimZamani <= e)
            .Select(y => new { y.AracId, y.AlimZamani, y.Km, y.Litre, y.FullDepo }).ToListAsync(ct);
        var araclar = await db.Araclar.AsNoTracking().Select(a => new { a.Id, a.Plaka, a.BeklenenTuketim }).ToDictionaryAsync(a => a.Id, ct);
        var esik = decimal.TryParse(await db.Ayarlar.Where(a => a.Anahtar == "Yakit.SapmaUyariYuzde").Select(a => a.Deger).FirstOrDefaultAsync(ct),
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 10m;

        return alimlar.GroupBy(a => a.AracId).Select(g =>
        {
            var araliklar = YakitTuketimHesap.Araliklar(g.Select(a => new YakitAlimi(a.AlimZamani, a.Km, a.Litre, a.FullDepo)));
            var ort = YakitTuketimHesap.Ortalama(araliklar);
            var arac = araclar[g.Key];
            decimal? sapma = ort is not null && arac.BeklenenTuketim is > 0 ? YakitTuketimHesap.SapmaYuzde(ort.Value, arac.BeklenenTuketim.Value) : null;
            return new TuketimRaporSatir(g.Key, arac.Plaka, araliklar.Sum(a => a.Km), araliklar.Sum(a => a.Litre), ort, arac.BeklenenTuketim, sapma)
            {
                Alarm = sapma > esik,
            };
        }).OrderBy(r => r.Plaka).ToList();
    }

    public async Task<List<HgsSatir>> HgsGecisleriAsync(GiderFiltre f, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderGor);
        await using var db = factory.Olustur();
        var bas = f.Baslangic.ToDateTime(TimeOnly.MinValue);
        var bit = f.Bitis.ToDateTime(TimeOnly.MaxValue);
        return await db.HgsGecisleri.AsNoTracking()
            .Where(h => h.GecisZamani >= bas && h.GecisZamani <= bit && (f.AracId == null || h.AracId == f.AracId))
            .OrderByDescending(h => h.GecisZamani)
            .Select(h => new HgsSatir(h.Id, h.GecisZamani, h.Arac!.Plaka, h.GirisYeri, h.CikisYeri, h.Tutar, h.Sefer != null ? h.Sefer.SeferNo : null))
            .Take(10000).ToListAsync(ct);
    }

    /// <summary>Eşleşmemiş HGS geçişlerini tekrar seferlere eşleştirmeyi dener (sefer sonradan girildiyse).</summary>
    public async Task<int> HgsYenidenEslestirAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderYaz);
        await using var db = factory.Olustur();
        var b0 = bas.ToDateTime(TimeOnly.MinValue);
        var b1 = bit.ToDateTime(TimeOnly.MaxValue);
        var bos = await db.HgsGecisleri.Where(h => h.SeferId == null && h.GecisZamani >= b0 && h.GecisZamani <= b1).ToListAsync(ct);
        var n = 0;
        foreach (var h in bos)
        {
            h.SeferId = await SeferBulAsync(db, h.AracId, h.GecisZamani, ct);
            if (h.SeferId is not null) n++;
        }
        await db.SaveChangesAsync(ct);
        return n;
    }

    // ------------------------------- Ceza -------------------------------

    public async Task<List<CezaSatir>> CezalarAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderGor);
        await using var db = factory.Olustur();
        return await db.Cezalar.AsNoTracking().Where(c => c.Tarih >= bas && c.Tarih <= bit).OrderByDescending(c => c.Tarih)
            .Select(c => new CezaSatir(c.Id, c.Tarih, c.Arac != null ? c.Arac.Plaka : null, c.Sofor != null ? c.Sofor.Ad + " " + c.Sofor.Soyad : null,
                c.CezaTuru, c.Tutar, c.IndirimliTutar, c.OdemeTarihi, c.RucuEdilsin))
            .ToListAsync(ct);
    }

    public async Task<Ceza?> CezaGetirAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Cezalar.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
    }

    /// <summary>Ceza kaydı. Rücu edilecekse şoför carisine borç yazılır.</summary>
    public async Task<Ceza> CezaKaydetAsync(Ceza c, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderYaz);
        if (string.IsNullOrWhiteSpace(c.CezaTuru)) throw new DomainException("Ceza türü/açıklaması zorunludur.");
        if (c.Tutar <= 0) throw new DomainException("Ceza tutarı sıfırdan büyük olmalıdır.");
        if (c.RucuEdilsin && c.SoforId is null) throw new DomainException("Rücu edilecek ceza için şoför seçilmelidir.");
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (c.Id == 0)
        {
            c.OdemeTarihi = null;
            c.OdemeKasaHesapId = null;
            db.Cezalar.Add(c);
        }
        else
        {
            var eski = await db.Cezalar.AsNoTracking().SingleAsync(x => x.Id == c.Id, ct);
            c.OdemeTarihi = eski.OdemeTarihi;
            c.OdemeKasaHesapId = eski.OdemeKasaHesapId;
            if (eski.RucuEdilsin != c.RucuEdilsin || eski.SoforId != c.SoforId || eski.Tutar != c.Tutar)
                await RucuTersAsync(db, c.Id, ct);
            await db.BirlestirAsync(c, ct);
        }
        await db.KaydetAsync(ct);
        if (c.RucuEdilsin && !await RucuVarAsync(db, c.Id, ct))
        {
            var soforCari = await db.Soforler.Where(s => s.Id == c.SoforId).Select(s => s.CariId).SingleAsync(ct);
            Defter.Cari(db, soforCari, c.Tarih, c.IndirimliTutar ?? c.Tutar, 0, KaynakTip.Ceza, c.Id, $"Ceza rücu: {c.CezaTuru} {c.TutanakNo}",
                aracId: c.AracId, seferId: c.SeferId);
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return c;
    }

    /// <summary>Cezanın ödenmesi: kasadan çıkış.</summary>
    public async Task CezaOdeAsync(int cezaId, byte kasaHesapId, DateOnly tarih, decimal tutar, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.GiderYaz);
        await using var db = factory.Olustur();
        var c = await db.Cezalar.SingleOrDefaultAsync(x => x.Id == cezaId, ct) ?? throw new DomainException("Ceza bulunamadı.");
        if (c.OdemeTarihi is not null) throw new DomainException("Ceza zaten ödenmiş.");
        if (tutar <= 0) throw new DomainException("Tutar sıfırdan büyük olmalıdır.");
        await Defter.KasaCikisKontrolAsync(db, kasaHesapId, tutar, ct);
        c.OdemeTarihi = tarih;
        c.OdemeKasaHesapId = kasaHesapId;
        if (tutar < c.Tutar) c.IndirimliTutar = tutar;
        Defter.Kasa(db, kasaHesapId, tarih, 0, tutar, KaynakTip.Ceza, c.Id, $"Ceza ödemesi: {c.CezaTuru} {c.TutanakNo}");
        await db.KaydetAsync(ct);
    }

    private static Task<bool> RucuVarAsync(IFiloDbContext db, int cezaId, CancellationToken ct) =>
        db.CariHareketleri.AnyAsync(h => h.KaynakTip == KaynakTip.Ceza && h.KaynakId == cezaId && h.TersKayitId == null
                                         && !db.CariHareketleri.Any(t => t.TersKayitId == h.Id), ct);

    private async Task RucuTersAsync(IFiloDbContext db, int cezaId, CancellationToken ct)
    {
        // Yalnızca cari (rücu) tarafı ters çevrilir; ödeme kasa kaydı korunur.
        var rucular = await db.CariHareketleri.Where(h => h.KaynakTip == KaynakTip.Ceza && h.KaynakId == cezaId && h.TersKayitId == null
                                                          && !db.CariHareketleri.Any(t => t.TersKayitId == h.Id)).ToListAsync(ct);
        foreach (var h in rucular)
            db.CariHareketleri.Add(new CariHareket
            {
                CariId = h.CariId, Tarih = saat.Bugun, Borc = h.Alacak, Alacak = h.Borc, KaynakTip = KaynakTip.Ceza, KaynakId = cezaId,
                TersKayitId = h.Id, Aciklama = "İPTAL (rücu değişti): " + h.Aciklama,
            });
    }
}
