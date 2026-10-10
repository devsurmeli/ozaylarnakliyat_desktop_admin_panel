using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record SeferFiltre(
    DateOnly? Baslangic = null, DateOnly? Bitis = null, SeferDurum? Durum = null, bool SadeceAcik = false,
    short? AracId = null, short? SoforId = null, int? MusteriId = null, string? Metin = null);

public sealed record SeferSatir(
    int Id, string SeferNo, SeferKapsam Kapsam, SeferDurum Durum, DateOnly PlanlananTarih, string Musteri,
    string Arac, string? Dorse, string Soforler, string Cikis, string Varis, string? YukTanimi,
    int? Km, decimal? NavlunTL, bool Taseron)
{
    public string DurumAd => Durum.Ad();
    public string KapsamAd => Kapsam.Ad();
}

public sealed record KarlilikKalemi(string Kalem, decimal TutarTL);

public sealed record SeferKarlilik(int SeferId, string SeferNo, decimal GelirTL, IReadOnlyList<KarlilikKalemi> Giderler, int? Km)
{
    public decimal GiderTL => Giderler.Sum(g => g.TutarTL);
    public decimal KarTL => GelirTL - GiderTL;
    public decimal? KarMarji => GelirTL == 0 ? null : Math.Round(KarTL * 100 / GelirTL, 1);
    public decimal? KmBasiMaliyet => Km is > 0 ? Math.Round(GiderTL / Km.Value, 2) : null;
}

public sealed class SeferServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    public bool FiyatGorebilir => oturum.Yetkili(Izinler.SeferFiyatGor);

    public async Task<List<SeferSatir>> ListeAsync(SeferFiltre f, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SeferGor);
        var fiyat = FiyatGorebilir;
        await using var db = factory.Olustur();
        var q = db.Seferler.AsNoTracking();
        if (f.Baslangic is not null) q = q.Where(s => s.PlanlananTarih >= f.Baslangic);
        if (f.Bitis is not null) q = q.Where(s => s.PlanlananTarih <= f.Bitis);
        if (f.Durum is not null) q = q.Where(s => s.Durum == f.Durum);
        if (f.SadeceAcik) q = q.Where(s => s.Durum < SeferDurum.Kapatildi);
        if (f.AracId is not null) q = q.Where(s => s.AracId == f.AracId);
        if (f.MusteriId is not null) q = q.Where(s => s.MusteriCariId == f.MusteriId);
        if (f.SoforId is not null) q = q.Where(s => s.Soforler.Any(x => x.SoforId == f.SoforId));
        if (!string.IsNullOrWhiteSpace(f.Metin))
        {
            var m = f.Metin.Trim();
            q = q.Where(s => s.SeferNo.Contains(m) || s.Musteri!.Unvan.Contains(m) || s.Arac!.Plaka.Contains(m)
                             || s.TaseronPlaka!.Contains(m) || s.YukTanimi!.Contains(m) || s.CmrNo!.Contains(m)
                             || s.CikisLokasyon!.Ad.Contains(m) || s.VarisLokasyon!.Ad.Contains(m));
        }

        var ham = await q.OrderByDescending(s => s.PlanlananTarih).ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.Id, s.SeferNo, s.Kapsam, s.Durum, s.PlanlananTarih, Musteri = s.Musteri!.Unvan,
                Arac = s.Arac != null ? s.Arac.Plaka : s.TaseronPlaka, Dorse = s.Dorse != null ? s.Dorse.Plaka : null,
                Soforler = s.Soforler.OrderByDescending(x => x.Birincil).Select(x => x.Sofor!.Ad + " " + x.Sofor.Soyad).ToList(),
                Cikis = s.CikisLokasyon!.Ad, Varis = s.VarisLokasyon!.Ad, s.YukTanimi, s.CikisKm, s.DonusKm,
                s.NavlunTutar, s.NavlunKur, Taseron = s.TaseronCariId != null,
            })
            .Take(2000)
            .ToListAsync(ct);

        return ham.Select(s => new SeferSatir(s.Id, s.SeferNo, s.Kapsam, s.Durum, s.PlanlananTarih, s.Musteri,
            s.Arac ?? "", s.Dorse, string.Join(", ", s.Soforler), s.Cikis, s.Varis, s.YukTanimi,
            s.DonusKm - s.CikisKm, fiyat ? Para.TL(s.NavlunTutar, s.NavlunKur) : null, s.Taseron)).ToList();
    }

    public async Task<Sefer?> GetirAsync(int id, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SeferGor);
        await using var db = factory.Olustur();
        var s = await db.Seferler.AsNoTracking()
            .Include(x => x.Soforler).Include(x => x.Duraklar.OrderBy(d => d.Sira))
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s is not null && !FiyatGorebilir)
        {
            s.NavlunTutar = 0;
            s.TaseronBedel = null;
        }
        return s;
    }

    /// <summary>Yeni sefer için varsayılanlar: aracın bağlı dorsesi ve varsayılan şoförü.</summary>
    public async Task<(short? DorseId, short? SoforId)> AracVarsayilanlariAsync(short aracId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var dorse = await db.AracDorseEslesmeleri.Where(e => e.AracId == aracId && e.Bitis == null).Select(e => (short?)e.DorseId).FirstOrDefaultAsync(ct);
        var sofor = await db.Soforler.Where(s => s.VarsayilanAracId == aracId && s.Aktif).Select(s => (short?)s.Id).FirstOrDefaultAsync(ct);
        return (dorse, sofor);
    }

    /// <summary>Seferi kaydeder. Onay gerektiren uyarılar varsa <see cref="UyariVarException"/> fırlatır.</summary>
    public async Task<Sefer> KaydetAsync(Sefer gelen, IReadOnlyList<short> soforIdleri, IReadOnlyList<SeferDurak> duraklar,
        bool uyarilariOnayla = false, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SeferYaz);
        await using var db = factory.Olustur();
        var yeni = gelen.Id == 0;

        if (!yeni)
        {
            var eskiDurum = await db.Seferler.Where(s => s.Id == gelen.Id).Select(s => (SeferDurum?)s.Durum).SingleOrDefaultAsync(ct)
                            ?? throw new DomainException("Sefer bulunamadı.");
            if (eskiDurum is SeferDurum.Kapatildi or SeferDurum.Faturalandi or SeferDurum.Iptal)
                throw new DomainException($"'{eskiDurum.Ad()}' durumundaki sefer düzenlenemez. Gerekirse yetkili kullanıcı seferi yeniden açmalıdır.");
            if (gelen.Durum != eskiDurum)
                throw new DomainException("Sefer durumu bu ekrandan değil, durum butonlarıyla değiştirilir.");
            if (!FiyatGorebilir)
            {
                // Fiyat göremeyen kullanıcı navlunu ezemez.
                var mevcutFiyat = await db.Seferler.AsNoTracking().Where(s => s.Id == gelen.Id)
                    .Select(s => new { s.NavlunTutar, s.TaseronBedel }).SingleAsync(ct);
                gelen.NavlunTutar = mevcutFiyat.NavlunTutar;
                gelen.TaseronBedel = mevcutFiyat.TaseronBedel;
            }
        }

        Normallestir(gelen, soforIdleri);
        // Dorse girilmediyse aracın o anki bağlı dorsesi kullanılır.
        if (yeni && gelen.AracId is not null && gelen.DorseId is null)
            gelen.DorseId = await db.AracDorseEslesmeleri.Where(e => e.AracId == gelen.AracId && e.Bitis == null)
                .Select(e => (short?)e.DorseId).FirstOrDefaultAsync(ct);
        var uyarilar = new Uyarilar();
        await DogrulaAsync(db, gelen, soforIdleri, uyarilar, ct);
        uyarilar.OnaylanmadiysaFirlat(uyarilariOnayla);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Sefer hedef;
        if (yeni)
        {
            gelen.SeferNo = $"S{saat.Bugun.Year}-{await db.SiradakiNumaraAsync("SeferNoSeq", ct):D5}";
            gelen.Durum = SeferDurum.Planlandi;
            gelen.Soforler = [];
            gelen.Duraklar = [];
            db.Seferler.Add(gelen);
            hedef = gelen;
        }
        else
        {
            hedef = await db.BirlestirAsync(gelen, ct);
            db.Entry(hedef).Property(x => x.SeferNo).IsModified = false;
            await db.SeferSoforleri.Where(x => x.SeferId == hedef.Id).ExecuteDeleteAsync(ct);
            await db.SeferDuraklari.Where(x => x.SeferId == hedef.Id).ExecuteDeleteAsync(ct);
        }

        for (var i = 0; i < soforIdleri.Count; i++)
            hedef.Soforler.Add(new SeferSofor { SoforId = soforIdleri[i], Birincil = i == 0 });
        byte sira = 1;
        foreach (var d in duraklar)
            hedef.Duraklar.Add(new SeferDurak
            {
                Sira = sira++, LokasyonId = d.LokasyonId, DurakTipi = d.DurakTipi,
                PlanlananTarih = d.PlanlananTarih, GerceklesenTarih = d.GerceklesenTarih, Aciklama = d.Aciklama,
            });

        await AracKmGuncelleAsync(db, hedef, ct);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        gelen.Id = hedef.Id;
        gelen.SeferNo = hedef.SeferNo;
        gelen.RowVer = hedef.RowVer;
        return gelen;
    }

    private static void Normallestir(Sefer s, IReadOnlyList<short> soforIdleri)
    {
        s.TaseronPlaka = Metin.Temizle(s.TaseronPlaka) is { } p ? Metin.Plaka(p) : null;
        s.CmrNo = Metin.Temizle(s.CmrNo);
        s.IrsaliyeNo = Metin.Temizle(s.IrsaliyeNo);
        s.YukTanimi = Metin.Temizle(s.YukTanimi);
        s.Notlar = Metin.Temizle(s.Notlar);
        if (s.NavlunParaBirimi == ParaBirimi.TRY) s.NavlunKur = 1;
        if (s.TaseronParaBirimi == ParaBirimi.TRY) s.TaseronKur = 1;
        if (s.TaseronCariId is null)
        {
            s.TaseronPlaka = null;
            s.TaseronBedel = null;
        }
        else
        {
            // Taşeron seferinde kendi aracımız/şoförümüz kullanılmaz.
            s.AracId = null;
            s.DorseId = null;
        }
        if (s.Kapsam == SeferKapsam.Yurtici && s.NavlunParaBirimi == ParaBirimi.TRY && s.NavlunTevkifatPay == 0 && s.NavlunKdvOrani == 0)
            s.NavlunKdvOrani = 20;
        if (s.TaseronCariId is not null && soforIdleri.Count > 0)
            throw new DomainException("Taşeron seferine kendi şoförünüz atanamaz.");
    }

    private async Task DogrulaAsync(IFiloDbContext db, Sefer s, IReadOnlyList<short> soforIdleri, Uyarilar uyarilar, CancellationToken ct)
    {
        if (s.MusteriCariId == 0) throw new DomainException("Müşteri seçilmelidir.");
        if (s.CikisLokasyonId == 0 || s.VarisLokasyonId == 0) throw new DomainException("Çıkış ve varış lokasyonu seçilmelidir.");
        if (s.AracId is null && s.TaseronCariId is null) throw new DomainException("Araç seçilmeli veya taşeron sefer olarak işaretlenmelidir.");
        if (s.TaseronCariId is not null && s.TaseronBedel is null or <= 0 && FiyatGorebilir)
            throw new DomainException("Taşeron seferinde taşerona ödenecek bedel girilmelidir.");
        if (s.AracId is not null && soforIdleri.Count == 0) throw new DomainException("En az bir şoför atanmalıdır.");
        if (soforIdleri.Distinct().Count() != soforIdleri.Count) throw new DomainException("Aynı şoför iki kez atanamaz.");
        if (s.NavlunTutar < 0) throw new DomainException("Navlun negatif olamaz.");
        if (s.NavlunParaBirimi != ParaBirimi.TRY && s.NavlunKur <= 0) throw new DomainException("Dövizli navlun için kur girilmelidir.");
        if (s.CikisKm is not null && s.DonusKm is not null && s.DonusKm < s.CikisKm)
            throw new DomainException("Dönüş km'si çıkış km'sinden küçük olamaz.");
        if (s.TeslimTarihi is not null && s.CikisTarihi is not null && s.TeslimTarihi < s.CikisTarihi)
            throw new DomainException("Teslim tarihi çıkış tarihinden önce olamaz.");
        if (s.DonusTarihi is not null && s.CikisTarihi is not null && s.DonusTarihi < s.CikisTarihi)
            throw new DomainException("Dönüş tarihi çıkış tarihinden önce olamaz.");

        // Kilometre: aracın bilinen km'sinden geri gidemez, güzergah standardından çok saparsa uyar.
        if (s.AracId is not null && s.CikisKm is not null)
        {
            var aracKm = await db.Araclar.Where(a => a.Id == s.AracId).Select(a => a.GuncelKm).SingleAsync(ct);
            var oncekiDonus = await db.Seferler.Where(x => x.AracId == s.AracId && x.Id != s.Id && x.DonusKm != null && x.PlanlananTarih <= s.PlanlananTarih && x.Durum != SeferDurum.Iptal)
                .MaxAsync(x => x.DonusKm, ct);
            if (oncekiDonus is not null && s.CikisKm < oncekiDonus)
                uyarilar.Ekle($"Çıkış km'si ({s.CikisKm:N0}) aracın önceki seferinin dönüş km'sinden ({oncekiDonus:N0}) küçük.");
            else if (oncekiDonus is null && aracKm > 0 && s.CikisKm < aracKm - 1000)
                uyarilar.Ekle($"Çıkış km'si ({s.CikisKm:N0}) araç kartındaki km'den ({aracKm:N0}) belirgin şekilde düşük.");
        }
        if (s.GuzergahId is not null && s.CikisKm is not null && s.DonusKm is not null)
        {
            var std = await db.Guzergahlar.Where(g => g.Id == s.GuzergahId).Select(g => g.StandartKm).SingleAsync(ct);
            var esik = await AyarSayiAsync(db, "Sefer.KmSapmaYuzde", 15, ct);
            var gercek = s.DonusKm.Value - s.CikisKm.Value;
            if (std > 0 && Math.Abs(gercek - std) * 100m / std > esik)
                uyarilar.Ekle($"Gerçekleşen km ({gercek:N0}) güzergah standardından ({std:N0}) %{esik}'ten fazla sapıyor.");
        }

        // Kaynak çakışması: aynı araç/dorse/şoför aynı anda iki aktif (yüklemede/yolda) seferde olamaz;
        // planlanmış seferler için aynı gün başka bir sefer varsa uyarılır.
        var aktifDurumlar = new[] { SeferDurum.Yuklemede, SeferDurum.Yolda };
        var digerleri = db.Seferler.Where(x => x.Id != s.Id && x.Durum != SeferDurum.Iptal && x.Durum < SeferDurum.Kapatildi);
        async Task Cakisma(IQueryable<Sefer> q, string kaynak)
        {
            if (aktifDurumlar.Contains(s.Durum))
            {
                var no = await q.Where(x => aktifDurumlar.Contains(x.Durum)).Select(x => x.SeferNo).FirstOrDefaultAsync(ct);
                if (no is not null) throw new DomainException($"{kaynak} şu anda {no} numaralı seferde (yüklemede/yolda).");
            }
            var ayniGun = await q.Where(x => x.PlanlananTarih == s.PlanlananTarih).Select(x => x.SeferNo).FirstOrDefaultAsync(ct);
            if (ayniGun is not null) uyarilar.Ekle($"{kaynak} aynı gün {ayniGun} numaralı seferde de planlanmış.");
        }
        if (s.AracId is not null)
        {
            var plaka = await db.Araclar.Where(a => a.Id == s.AracId).Select(a => a.Plaka).SingleAsync(ct);
            await Cakisma(digerleri.Where(x => x.AracId == s.AracId), $"{plaka} plakalı araç");
        }
        if (s.DorseId is not null)
        {
            var plaka = await db.Dorseler.Where(a => a.Id == s.DorseId).Select(a => a.Plaka).SingleAsync(ct);
            await Cakisma(digerleri.Where(x => x.DorseId == s.DorseId), $"{plaka} plakalı dorse");
        }
        foreach (var sid in soforIdleri)
        {
            var ad = await db.Soforler.Where(a => a.Id == sid).Select(a => a.Ad + " " + a.Soyad).SingleAsync(ct);
            await Cakisma(digerleri.Where(x => x.Soforler.Any(y => y.SoforId == sid)), $"Şoför {ad}");
        }

        await EvrakKontrolAsync(db, s, soforIdleri, uyarilar, ct);

        // Müşteri risk limiti.
        var musteri = await db.Cariler.AsNoTracking().Where(c => c.Id == s.MusteriCariId).Select(c => new { c.Unvan, c.RiskLimiti }).SingleAsync(ct);
        if (musteri.RiskLimiti is > 0)
        {
            var bakiye = await Defter.CariBakiyeAsync(db, s.MusteriCariId, ct);
            if (bakiye > musteri.RiskLimiti)
                uyarilar.Ekle($"{musteri.Unvan} bakiyesi ({bakiye:N2} TL) risk limitini ({musteri.RiskLimiti:N2} TL) aşıyor.");
        }
    }

    /// <summary>Sefer tarihinde süresi dolmuş evrak: "sefer engeller" işaretli türlerde hata, diğer zorunlu türlerde uyarı.</summary>
    private static async Task EvrakKontrolAsync(IFiloDbContext db, Sefer s, IReadOnlyList<short> soforIdleri, Uyarilar uyarilar, CancellationToken ct)
    {
        var tarih = s.CikisTarihi is not null ? DateOnly.FromDateTime(s.CikisTarihi.Value) : s.PlanlananTarih;
        var sonEvraklar = db.Evraklar
            .Where(e => e.BitisTarihi != null && (e.EvrakTuru!.SeferEngeller || e.EvrakTuru.Zorunlu))
            .Where(e => (s.AracId != null && e.AracId == s.AracId) || (s.DorseId != null && e.DorseId == s.DorseId)
                        || (e.SoforId != null && soforIdleri.Contains(e.SoforId.Value)))
            .Where(e => !db.Evraklar.Any(y => y.EvrakTuruId == e.EvrakTuruId && y.AracId == e.AracId && y.DorseId == e.DorseId
                                              && y.SoforId == e.SoforId && y.BitisTarihi > e.BitisTarihi))
            .Where(e => e.BitisTarihi < tarih)
            .Select(e => new
            {
                Tur = e.EvrakTuru!.Ad, e.EvrakTuru.SeferEngeller, e.BitisTarihi,
                Kime = e.AracId != null ? e.Arac!.Plaka : e.DorseId != null ? e.Dorse!.Plaka : e.Sofor!.Ad + " " + e.Sofor.Soyad,
            });
        foreach (var e in await sonEvraklar.ToListAsync(ct))
        {
            var msj = $"{e.Kime}: {e.Tur} süresi {e.BitisTarihi:dd.MM.yyyy} tarihinde dolmuş.";
            if (e.SeferEngeller) throw new DomainException(msj + " Bu evrak yenilenmeden sefer yapılamaz.");
            uyarilar.Ekle(msj);
        }
    }

    private static async Task AracKmGuncelleAsync(IFiloDbContext db, Sefer s, CancellationToken ct)
    {
        if (s.AracId is null) return;
        var km = s.DonusKm ?? s.CikisKm;
        if (km is null) return;
        var arac = await db.Araclar.FindAsync([s.AracId.Value], ct);
        if (arac is not null && km > arac.GuncelKm)
        {
            arac.GuncelKm = km.Value;
            AracKm.Kaydet(db, s.AracId.Value, s.DonusTarihi ?? s.TeslimTarihi ?? s.CikisTarihi ?? s.PlanlananTarih.ToDateTime(TimeOnly.MinValue), km.Value, KmKaynak.Sefer, s.Id > 0 ? s.Id : null);
        }
    }

    private static async Task<decimal> AyarSayiAsync(IFiloDbContext db, string anahtar, decimal varsayilan, CancellationToken ct)
    {
        var d = await db.Ayarlar.AsNoTracking().Where(a => a.Anahtar == anahtar).Select(a => a.Deger).FirstOrDefaultAsync(ct);
        return decimal.TryParse(d, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : varsayilan;
    }

    /// <summary>
    /// Durum geçişi. Kapatıldı: km zorunlu (özmal), taşeron bedeli taşeron carisine alacak yazılır.
    /// Kapatıldı → Teslim Edildi geri alma: yalnızca yetkili kullanıcı, taşeron kaydı ters çevrilir.
    /// Faturalandı durumu yalnızca fatura servisi tarafından verilir.
    /// </summary>
    public async Task DurumDegistirAsync(int seferId, SeferDurum yeni, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var s = await db.Seferler.SingleOrDefaultAsync(x => x.Id == seferId, ct) ?? throw new DomainException("Sefer bulunamadı.");
        var eski = s.Durum;
        if (eski == yeni) return;
        if (yeni == SeferDurum.Faturalandi || eski == SeferDurum.Faturalandi)
            throw new DomainException("Faturalandı durumu fatura kaydıyla verilir/kaldırılır.");

        if (eski == SeferDurum.Kapatildi || eski == SeferDurum.Iptal)
            oturum.YetkiGerekli(Izinler.SeferYenidenAc);
        else
            oturum.YetkiGerekli(Izinler.SeferYaz);

        if (eski == SeferDurum.Iptal && yeni != SeferDurum.Planlandi)
            throw new DomainException("İptal edilmiş sefer yalnızca 'Planlandı' durumuna geri alınabilir.");

        var bugun = saat.Bugun;
        switch (yeni)
        {
            case SeferDurum.Yuklemede or SeferDurum.Yolda:
                var soforler = await db.SeferSoforleri.Where(x => x.SeferId == s.Id).Select(x => x.SoforId).ToListAsync(ct);
                await CakismaKontrolAktifAsync(db, s, soforler, ct);
                if (yeni == SeferDurum.Yolda) s.CikisTarihi ??= saat.Simdi;
                break;
            case SeferDurum.TeslimEdildi:
                s.TeslimTarihi ??= saat.Simdi;
                break;
            case SeferDurum.Kapatildi:
                if (s.AracId is not null && (s.CikisKm is null || s.DonusKm is null))
                    throw new DomainException("Seferi kapatmak için çıkış ve dönüş km'si girilmelidir.");
                if (s.TeslimTarihi is null) throw new DomainException("Seferi kapatmak için teslim tarihi girilmelidir.");
                await TaseronBedelIsleAsync(db, s, bugun, ct);
                break;
            case SeferDurum.Iptal:
                if (await db.FaturaKalemleri.AnyAsync(k => k.SeferId == s.Id && k.Fatura!.Durum == FaturaDurum.Kayitli, ct))
                    throw new DomainException("Faturalanmış sefer iptal edilemez; önce faturayı iptal edin.");
                break;
        }

        // Kapatılmış seferi geri açarken veya iptal ederken taşeron kaydı ters çevrilir.
        if (eski == SeferDurum.Kapatildi && s.TaseronCariId is not null)
            await Defter.TersKayitAsync(db, KaynakTip.TaseronBedel, s.Id, bugun, "sefer yeniden açıldı", ct);

        s.Durum = yeni;
        await db.KaydetAsync(ct);
    }

    /// <summary>Taşeron bedelini taşeron carisine alacak yazar. Zaten işlenmişse (ve iptal edilmemişse) tekrar yazmaz.</summary>
    internal static async Task TaseronBedelIsleAsync(IFiloDbContext db, Sefer s, DateOnly tarih, CancellationToken ct)
    {
        if (s.TaseronCariId is null || s.TaseronBedel is not > 0) return;
        var islenmis = await db.CariHareketleri.AnyAsync(h => h.KaynakTip == KaynakTip.TaseronBedel && h.KaynakId == s.Id
            && h.TersKayitId == null && !db.CariHareketleri.Any(t => t.TersKayitId == h.Id), ct);
        if (islenmis) return;
        Defter.Cari(db, s.TaseronCariId.Value, tarih, 0, s.TaseronBedel.Value, KaynakTip.TaseronBedel, s.Id,
            $"Taşeron bedeli - {s.SeferNo} {s.TaseronPlaka}", s.TaseronParaBirimi, s.TaseronKur, seferId: s.Id);
    }

    private static async Task CakismaKontrolAktifAsync(IFiloDbContext db, Sefer s, List<short> soforler, CancellationToken ct)
    {
        var aktif = new[] { SeferDurum.Yuklemede, SeferDurum.Yolda };
        var q = db.Seferler.Where(x => x.Id != s.Id && aktif.Contains(x.Durum));
        var no = await q.Where(x => (s.AracId != null && x.AracId == s.AracId) || (s.DorseId != null && x.DorseId == s.DorseId)
                                    || x.Soforler.Any(y => soforler.Contains(y.SoforId)))
            .Select(x => x.SeferNo).FirstOrDefaultAsync(ct);
        if (no is not null) throw new DomainException($"Araç, dorse veya şoför şu anda {no} numaralı seferde (yüklemede/yolda).");
    }

    public async Task SilAsync(int seferId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SeferYaz);
        await using var db = factory.Olustur();
        var s = await db.Seferler.SingleOrDefaultAsync(x => x.Id == seferId, ct) ?? throw new DomainException("Sefer bulunamadı.");
        if (s.Durum != SeferDurum.Planlandi && s.Durum != SeferDurum.Iptal)
            throw new DomainException("Yalnızca planlanmış veya iptal edilmiş sefer silinebilir.");
        if (await db.Masraflar.AnyAsync(m => m.SeferId == seferId && !m.Iptal, ct)
            || await db.Yakitlar.AnyAsync(m => m.SeferId == seferId && !m.Iptal, ct)
            || await db.Avanslar.AnyAsync(m => m.SeferId == seferId && !m.Iptal, ct))
            throw new DomainException("Sefere bağlı masraf/yakıt/avans kaydı var; silmek yerine iptal edin.");
        db.Seferler.Remove(s);
        await db.KaydetAsync(ct);
    }

    /// <summary>Sefer kârlılığı (TL, KDV hariç). Gelir: navlun. Gider: masraf, yakıt, HGS, harcırah, taşeron, rücu edilmeyen ceza.</summary>
    public async Task<SeferKarlilik> KarlilikAsync(int seferId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.RaporFinans);
        await using var db = factory.Olustur();
        return (await KarlilikHesaplaAsync(db, db.Seferler.Where(s => s.Id == seferId), ct)).Single();
    }

    internal static async Task<List<SeferKarlilik>> KarlilikHesaplaAsync(IFiloDbContext db, IQueryable<Sefer> seferler, CancellationToken ct)
    {
        var temel = await seferler.AsNoTracking()
            .Select(s => new { s.Id, s.SeferNo, s.NavlunTutar, s.NavlunKur, s.TaseronBedel, s.TaseronKur, Km = s.DonusKm - s.CikisKm })
            .ToListAsync(ct);
        var idler = temel.Select(s => s.Id).ToList();

        var masraf = (await db.Masraflar.AsNoTracking().Where(m => m.SeferId != null && idler.Contains(m.SeferId.Value) && !m.Iptal)
                .Select(m => new { SeferId = m.SeferId!.Value, Grup = m.MasrafTuru!.Grup, m.Tutar, m.Kur, m.KdvOrani }).ToListAsync(ct))
            .GroupBy(m => (m.SeferId, m.Grup))
            .ToDictionary(g => g.Key, g => g.Sum(m => Para.TL(Para.KdvHaric(m.Tutar, m.KdvOrani), m.Kur)));
        var yakit = (await db.Yakitlar.AsNoTracking().Where(y => y.SeferId != null && idler.Contains(y.SeferId.Value) && !y.Iptal)
                .Select(y => new { SeferId = y.SeferId!.Value, y.Tutar, y.Kur, y.KdvOrani }).ToListAsync(ct))
            .GroupBy(y => y.SeferId).ToDictionary(g => g.Key, g => g.Sum(y => Para.TL(Para.KdvHaric(y.Tutar, y.KdvOrani), y.Kur)));
        var hgs = await db.HgsGecisleri.AsNoTracking().Where(h => h.SeferId != null && idler.Contains(h.SeferId.Value))
            .GroupBy(h => h.SeferId!.Value).Select(g => new { g.Key, T = g.Sum(h => h.Tutar) }).ToDictionaryAsync(g => g.Key, g => g.T, ct);
        var harcirah = (await db.Harcirahlar.AsNoTracking().Where(h => h.SeferId != null && idler.Contains(h.SeferId.Value) && !h.Iptal)
                .Select(h => new { SeferId = h.SeferId!.Value, h.Tutar, h.Kur }).ToListAsync(ct))
            .GroupBy(h => h.SeferId).ToDictionary(g => g.Key, g => g.Sum(h => Para.TL(h.Tutar, h.Kur)));
        var ceza = await db.Cezalar.AsNoTracking().Where(c => c.SeferId != null && idler.Contains(c.SeferId.Value) && !c.RucuEdilsin)
            .GroupBy(c => c.SeferId!.Value).Select(g => new { g.Key, T = g.Sum(c => c.IndirimliTutar ?? c.Tutar) }).ToDictionaryAsync(g => g.Key, g => g.T, ct);

        return temel.Select(s =>
        {
            var kalemler = new List<KarlilikKalemi>();
            void Ekle(string ad, decimal t) { if (t != 0) kalemler.Add(new KarlilikKalemi(ad, t)); }
            if (s.TaseronBedel is > 0) Ekle("Taşeron Bedeli", Para.TL(s.TaseronBedel.Value, s.TaseronKur));
            Ekle("Yakıt (kayıt)", yakit.GetValueOrDefault(s.Id));
            foreach (var g in masraf.Where(m => m.Key.SeferId == s.Id).OrderBy(m => m.Key.Grup))
                Ekle(g.Key.Grup.Ad(), g.Value);
            Ekle("HGS/OGS", hgs.GetValueOrDefault(s.Id));
            Ekle("Harcırah", harcirah.GetValueOrDefault(s.Id));
            Ekle("Ceza", ceza.GetValueOrDefault(s.Id));
            return new SeferKarlilik(s.Id, s.SeferNo, Para.TL(s.NavlunTutar, s.NavlunKur), kalemler, s.Km);
        }).ToList();
    }
}
