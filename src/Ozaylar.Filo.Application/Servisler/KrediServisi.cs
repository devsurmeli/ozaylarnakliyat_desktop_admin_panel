using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record KrediSatir(
    short Id, string KrediNo, KrediTuru Tur, string Banka, ParaBirimi ParaBirimi, DateOnly Kullandirim, decimal Anapara,
    decimal OdenenAnapara, decimal OdenenFaiz, decimal KalanFaiz, DateOnly? SonrakiVade, decimal? SonrakiTutar,
    int OdenenTaksit, int ToplamTaksit, int GecikenTaksit, KrediDurum Durum, string? Araclar) : IRenkliSatir
{
    public decimal KalanAnapara => Anapara - OdenenAnapara;
    public decimal Ilerleme => Anapara == 0 ? 100 : Math.Round(OdenenAnapara * 100 / Anapara, 1);
    public string TaksitMetni => ToplamTaksit == 0 ? "serbest" : $"{OdenenTaksit} / {ToplamTaksit}";
    public string TurAd => Tur.Ad();
    public string PB => ParaBirimi.Ad();
    public string DurumAd => GecikenTaksit > 0 ? $"Gecikmede ({GecikenTaksit})" : Durum.Ad();
    public string? Renk => Durum == KrediDurum.Kapandi ? "gri" : GecikenTaksit > 0 ? "kirmizi"
        : SonrakiVade is { } v && v.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber <= 7 ? "turuncu" : null;
}

public sealed record PlanSatir(int Id, byte Versiyon, short No, DateOnly Vade, decimal Anapara, decimal Faiz, decimal Bsmv, decimal Kkdf,
    decimal Toplam, decimal KalanAnapara, decimal Odenen, TaksitDurum Durum, DateOnly Bugun) : IRenkliSatir
{
    public decimal Kalan => Math.Max(0, Toplam - Odenen);
    public bool Gecikti => Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi && Vade < Bugun;
    public string DurumAd => Gecikti ? $"Gecikti ({Bugun.DayNumber - Vade.DayNumber} gün)" : Durum.Ad();
    public string? Renk => Gecikti ? "kirmizi" : Durum == TaksitDurum.Odendi ? "yesil"
        : Durum is TaksitDurum.Kapatildi or TaksitDurum.PlanDegisti ? "gri"
        : Vade.DayNumber - Bugun.DayNumber <= 7 ? "turuncu" : null;
}

public sealed record OdemeSatir(int Id, DateOnly Tarih, KrediOdemeTuru Tur, short? TaksitNo, string Kasa, decimal Tutar, decimal Kur, decimal KasaTutar,
    decimal Anapara, decimal Faiz, decimal Vergi, decimal Gecikme, decimal Komisyon, string? Aciklama, bool Iptal)
{
    public string TurAd => Tur.Ad();
}

public sealed record KrediOzetKpi(decimal Anapara, decimal OdenenAnapara, decimal KalanAnapara, decimal OdenenFaiz, decimal KalanFaiz,
    DateOnly? SonrakiVade, decimal? SonrakiTutar, int OdenenTaksit, int ToplamTaksit, int Geciken, decimal GecikenTutar)
{
    public decimal Ilerleme => Anapara == 0 ? 100 : Math.Round(OdenenAnapara * 100 / Anapara, 1);
}

public sealed record KrediDetay(Kredi Kredi, string Banka, IReadOnlyList<PlanSatir> Plan, IReadOnlyList<OdemeSatir> Odemeler,
    IReadOnlyList<(string Plaka, decimal Pay)> Araclar, KrediOzetKpi Ozet);

public sealed record BankaRiskSatir(int BankaId, string Banka, int KrediSayisi, decimal KalanAnaparaTL, decimal BuAyTaksitTL, decimal? LimitTL)
{
    public decimal? BosLimitTL => LimitTL - KalanAnaparaTL;
}

public sealed record TakvimSatir(int TaksitId, short KrediId, string KrediNo, string Banka, short No, DateOnly Vade, decimal Tutar, ParaBirimi ParaBirimi, decimal TutarTL, bool Gecikti) : IRenkliSatir
{
    public string PB => ParaBirimi.Ad();
    public string? Renk => Gecikti ? "kirmizi" : Vade.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber <= 7 ? "turuncu" : null;
}

public sealed record KrediOdemeRaporSatir(DateOnly Tarih, string Banka, string KrediNo, KrediTuru KrediTuru, KrediOdemeTuru Tur, ParaBirimi ParaBirimi, decimal Kur,
    decimal Anapara, decimal Faiz, decimal Vergi, decimal GecikmeKomisyon, decimal Tutar)
{
    public string TurAd => Tur.Ad();
    public string KrediTuruAd => KrediTuru.Ad();
    public string PB => ParaBirimi.Ad();
    public decimal AnaparaTL => Para.TL(Anapara, Kur);
    /// <summary>Finansman gideri (faiz + vergiler + gecikme + komisyon), TL.</summary>
    public decimal GiderTL => Para.TL(Faiz + Vergi + GecikmeKomisyon, Kur);
    public decimal TutarTL => Para.TL(Tutar, Kur);
}

public sealed record KrediGenelOzet(decimal KalanAnaparaTL, decimal BuAyOdenecekTL, decimal Gun30TL, decimal Gun60TL, decimal Gun90TL,
    int GecikenTaksit, decimal GecikenTutarTL, IReadOnlyList<BankaRiskSatir> Bankalar, IReadOnlyList<(string PB, decimal Kalan, decimal KalanTL)> Dovizler,
    IReadOnlyList<(DateOnly Ay, decimal AnaparaTL, decimal FaizTL)> AylikYuk);

/// <summary>
/// Kredi / leasing / rotatif yönetimi. Defter kuralları:
/// • Kullandırım: banka carisine alacak (anapara) + kasaya giriş (anapara − dosya masrafı, kasa varsa).
/// • Ödeme: kasadan çıkış; banka carisine faiz+vergi+gecikme+komisyon alacak (tahakkuk) ve ödenen toplam borç →
///   net etki yalnızca anapara azalışıdır (banka ekstresi kalan anapara borcunu gösterir). Faiz gideri KrediOdeme'den raporlanır.
/// </summary>
public sealed class KrediServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat, DovizServisi doviz)
{
    // ------------------------------------------------------------------ Sorgular

    public async Task<List<KrediSatir>> ListeAsync(bool kapananlarDahil = false, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanGor);
        var bugun = saat.Bugun;
        await using var db = factory.Olustur();
        var krediler = await db.Krediler.AsNoTracking().Where(k => kapananlarDahil || k.Durum == KrediDurum.Aktif)
            .Select(k => new { k.Id, k.KrediNo, k.Tur, Banka = k.Banka!.Unvan, k.ParaBirimi, k.KullandirimTarihi, k.Anapara, k.AktifVersiyon, k.Durum })
            .ToListAsync(ct);
        var ids = krediler.Select(k => k.Id).ToList();
        var odemeler = await db.KrediOdemeleri.AsNoTracking().Where(o => ids.Contains(o.KrediId) && !o.Iptal)
            .GroupBy(o => o.KrediId).Select(g => new { g.Key, Ana = g.Sum(o => o.Anapara), Faiz = g.Sum(o => o.Faiz + o.Bsmv + o.Kkdf + o.GecikmeFaizi) })
            .ToDictionaryAsync(g => g.Key, ct);
        var taksitler = await db.KrediTaksitleri.AsNoTracking().Where(t => ids.Contains(t.KrediId))
            .Select(t => new { t.KrediId, t.Versiyon, t.VadeTarihi, t.Faiz, t.Bsmv, t.Kkdf, t.Toplam, t.OdenenTutar, t.Durum }).ToListAsync(ct);
        var araclar = await db.KrediAraclari.AsNoTracking().Where(a => ids.Contains(a.KrediId))
            .Select(a => new { a.KrediId, Plaka = a.Arac != null ? a.Arac.Plaka : a.Dorse!.Plaka }).ToListAsync(ct);

        return krediler.Select(k =>
        {
            var aktif = taksitler.Where(t => t.KrediId == k.Id && (t.Versiyon == k.AktifVersiyon || t.Durum == TaksitDurum.Odendi)).ToList();
            var acik = aktif.Where(t => t.Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi).OrderBy(t => t.VadeTarihi).ToList();
            var od = odemeler.GetValueOrDefault(k.Id);
            var sonraki = acik.FirstOrDefault();
            return new KrediSatir(k.Id, k.KrediNo, k.Tur, k.Banka, k.ParaBirimi, k.KullandirimTarihi, k.Anapara,
                od?.Ana ?? 0, od?.Faiz ?? 0, acik.Sum(t => Math.Max(0, t.Faiz + t.Bsmv + t.Kkdf - Math.Min(t.OdenenTutar, t.Faiz + t.Bsmv + t.Kkdf))),
                sonraki?.VadeTarihi, sonraki is null ? null : sonraki.Toplam - sonraki.OdenenTutar,
                aktif.Count(t => t.Durum == TaksitDurum.Odendi), aktif.Count(t => t.Durum is not (TaksitDurum.PlanDegisti or TaksitDurum.Kapatildi)),
                acik.Count(t => t.VadeTarihi < bugun), k.Durum,
                string.Join(", ", araclar.Where(a => a.KrediId == k.Id).Select(a => a.Plaka)));
        }).OrderBy(k => k.Durum).ThenBy(k => k.Banka).ThenBy(k => k.KrediNo).ToList();
    }

    public async Task<Kredi?> GetirAsync(short id, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Krediler.AsNoTracking().Include(k => k.Araclar).SingleOrDefaultAsync(k => k.Id == id, ct);
    }

    public async Task<KrediDetay> DetayAsync(short id, byte? versiyon = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanGor);
        var bugun = saat.Bugun;
        await using var db = factory.Olustur();
        var k = await db.Krediler.AsNoTracking().Include(x => x.Banka).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Kredi bulunamadı.");
        var v = versiyon ?? k.AktifVersiyon;
        var tumTaksit = await db.KrediTaksitleri.AsNoTracking().Where(t => t.KrediId == id).OrderBy(t => t.VadeTarihi).ThenBy(t => t.Versiyon).ToListAsync(ct);
        var gorunen = tumTaksit.Where(t => t.Versiyon == v || (versiyon is null && t.Durum == TaksitDurum.Odendi && t.Versiyon < v))
            .OrderBy(t => t.VadeTarihi).ThenBy(t => t.TaksitNo).ToList();
        var plan = gorunen.Select(t => new PlanSatir(t.Id, t.Versiyon, t.TaksitNo, t.VadeTarihi, t.Anapara, t.Faiz, t.Bsmv, t.Kkdf, t.Toplam,
            t.KalanAnapara, t.OdenenTutar, t.Durum, bugun)).ToList();

        var odemeler = await db.KrediOdemeleri.AsNoTracking().Where(o => o.KrediId == id).OrderBy(o => o.Tarih).ThenBy(o => o.Id)
            .Select(o => new OdemeSatir(o.Id, o.Tarih, o.Tur,
                db.KrediTaksitleri.Where(t => t.Id == o.KrediTaksitId).Select(t => (short?)t.TaksitNo).FirstOrDefault(),
                db.KasaHesaplari.Where(h => h.Id == o.KasaHesapId).Select(h => h.Ad).FirstOrDefault() ?? "",
                o.Tutar, o.Kur, o.KasaTutar, o.Anapara, o.Faiz, o.Bsmv + o.Kkdf, o.GecikmeFaizi, o.Komisyon, o.Aciklama, o.Iptal))
            .ToListAsync(ct);
        var araclar = (await db.KrediAraclari.AsNoTracking().Where(a => a.KrediId == id)
                .Select(a => new { Plaka = a.Arac != null ? a.Arac.Plaka : a.Dorse!.Plaka, a.PayYuzde }).ToListAsync(ct))
            .Select(a => (a.Plaka, a.PayYuzde)).ToList();

        var gecerli = odemeler.Where(o => !o.Iptal).ToList();
        var aktifPlan = plan.Where(p => p.Durum is not (TaksitDurum.PlanDegisti or TaksitDurum.Kapatildi)).ToList();
        var acik = aktifPlan.Where(p => p.Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi).ToList();
        var sonraki = acik.OrderBy(p => p.Vade).FirstOrDefault();
        var ozet = new KrediOzetKpi(k.Anapara, gecerli.Sum(o => o.Anapara), k.Anapara - gecerli.Sum(o => o.Anapara),
            gecerli.Sum(o => o.Faiz + o.Vergi + o.Gecikme), acik.Sum(p => Math.Max(0, p.Faiz + p.Bsmv + p.Kkdf - Math.Min(p.Odenen, p.Faiz + p.Bsmv + p.Kkdf))),
            sonraki?.Vade, sonraki?.Kalan, aktifPlan.Count(p => p.Durum == TaksitDurum.Odendi), aktifPlan.Count,
            acik.Count(p => p.Gecikti), acik.Where(p => p.Gecikti).Sum(p => p.Kalan));
        return new KrediDetay(k, k.Banka!.Unvan, plan, odemeler, araclar, ozet);
    }

    public static List<KrediPlanSatiri> PlanOnizle(Kredi k) =>
        KrediHesap.PlanOlustur(new KrediPlanParametre(k.Anapara, k.AylikOran, k.BsmvOrani, k.KkdfOrani, k.VadeSayisi, k.IlkVade, k.OdemesizAy, k.Yontem, k.BalonTutar));

    // ------------------------------------------------------------------ Kayıt

    /// <summary>
    /// Yeni kredi: plan (otomatik veya verilen banka planı) + kullandırım defter kaydı. Mevcut kredi: ödeme yoksa her şey
    /// düzenlenebilir (plan ve kullandırım yeniden kurulur); ödeme varsa yalnızca finansal olmayan alanlar ve araçlar.
    /// </summary>
    public async Task<Kredi> KaydetAsync(Kredi gelen, IReadOnlyList<KrediTaksit>? bankaPlani, IReadOnlyList<KrediArac> araclar, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        Dogrula(gelen, araclar);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Kredi hedef;
        if (gelen.Id == 0)
        {
            gelen.Taksitler = [];
            gelen.Araclar = [];
            gelen.AktifVersiyon = 1;
            gelen.Durum = KrediDurum.Aktif;
            db.Krediler.Add(gelen);
            await db.KaydetAsync(ct);
            hedef = gelen;
            PlanEkle(db, hedef, 1, bankaPlani);
            await KullandirimIsleAsync(db, hedef, ct);
        }
        else
        {
            var odemeVar = await db.KrediOdemeleri.AnyAsync(o => o.KrediId == gelen.Id && !o.Iptal, ct);
            var eski = await db.Krediler.AsNoTracking().SingleAsync(k => k.Id == gelen.Id, ct);
            if (odemeVar && FinansalDegisti(eski, gelen))
                throw new DomainException("Bu krediye ödeme yapılmış; anapara, faiz, vade, tarih ve kullandırım bilgileri değiştirilemez. " +
                                          "Plan değişikliği için 'Ara Ödeme', 'Planı Değiştir / Banka Planı Al' kullanın.");
            hedef = await db.BirlestirAsync(gelen, ct);
            if (!odemeVar)
            {
                await Defter.TersKayitAsync(db, KaynakTip.KrediKullanim, hedef.Id, gelen.KullandirimTarihi, "kredi düzeltildi", ct);
                await db.KrediTaksitleri.Where(t => t.KrediId == hedef.Id).ExecuteDeleteAsync(ct);
                hedef.AktifVersiyon = 1;
                PlanEkle(db, hedef, 1, bankaPlani);
                await db.KaydetAsync(ct);
                await KullandirimIsleAsync(db, hedef, ct);
            }
            await db.KrediAraclari.Where(a => a.KrediId == hedef.Id).ExecuteDeleteAsync(ct);
        }
        foreach (var a in araclar)
            db.KrediAraclari.Add(new KrediArac { KrediId = hedef.Id, AracId = a.AracId, DorseId = a.DorseId, PayYuzde = a.PayYuzde });
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        gelen.Id = hedef.Id;
        return gelen;
    }

    private static bool FinansalDegisti(Kredi a, Kredi b) =>
        a.Anapara != b.Anapara || a.FaizOrani != b.FaizOrani || a.FaizTipi != b.FaizTipi || a.VadeSayisi != b.VadeSayisi || a.IlkVade != b.IlkVade
        || a.OdemesizAy != b.OdemesizAy || a.Yontem != b.Yontem || a.BalonTutar != b.BalonTutar || a.BsmvOrani != b.BsmvOrani || a.KkdfOrani != b.KkdfOrani
        || a.ParaBirimi != b.ParaBirimi || a.KullandirimTarihi != b.KullandirimTarihi || a.KullandirimKasaHesapId != b.KullandirimKasaHesapId
        || a.BankaCariId != b.BankaCariId || a.DosyaMasrafi != b.DosyaMasrafi || a.KullandirimKur != b.KullandirimKur;

    private static void Dogrula(Kredi k, IReadOnlyList<KrediArac> araclar)
    {
        k.KrediNo = k.KrediNo?.Trim() ?? "";
        if (k.KrediNo.Length == 0) throw new DomainException("Kredi / sözleşme numarası zorunludur.");
        if (k.BankaCariId == 0) throw new DomainException("Banka / leasing şirketi seçilmelidir.");
        if (k.Anapara <= 0) throw new DomainException("Anapara sıfırdan büyük olmalıdır.");
        if (k.FaizOrani < 0) throw new DomainException("Faiz oranı negatif olamaz.");
        if (k.ParaBirimi == ParaBirimi.TRY) k.KullandirimKur = 1;
        else if (k.KullandirimKur <= 0) throw new DomainException("Dövizli kredide kullandırım kuru girilmelidir.");
        if (k.Yontem != PlanYontemi.Manuel)
        {
            if (k.VadeSayisi <= 0) throw new DomainException("Vade sayısı girilmelidir.");
            if (k.IlkVade <= k.KullandirimTarihi) throw new DomainException("İlk taksit vadesi kullandırım tarihinden sonra olmalıdır.");
        }
        if (k.DosyaMasrafi < 0 || k.DosyaMasrafi >= k.Anapara) throw new DomainException("Dosya masrafı geçersiz.");
        if (araclar.Any(a => (a.AracId is null) == (a.DorseId is null))) throw new DomainException("Bağlı araç satırında araç veya dorseden biri seçilmelidir.");
        if (araclar.Sum(a => a.PayYuzde) > 100.01m) throw new DomainException("Araç payları toplamı %100'ü geçemez.");
        if (araclar.Count > 0 && k.Amac == KrediAmaci.Isletme) k.Amac = KrediAmaci.Arac;
    }

    private static void PlanEkle(IFiloDbContext db, Kredi k, byte versiyon, IReadOnlyList<KrediTaksit>? bankaPlani)
    {
        if (bankaPlani is { Count: > 0 })
        {
            short no = 1;
            foreach (var t in bankaPlani.OrderBy(t => t.VadeTarihi))
            {
                if (t.Toplam <= 0) t.Toplam = t.Anapara + t.Faiz + t.Bsmv + t.Kkdf;
                db.KrediTaksitleri.Add(new KrediTaksit
                {
                    KrediId = k.Id, Versiyon = versiyon, TaksitNo = t.TaksitNo > 0 ? t.TaksitNo : no, VadeTarihi = t.VadeTarihi,
                    Anapara = t.Anapara, Faiz = t.Faiz, Bsmv = t.Bsmv, Kkdf = t.Kkdf, Toplam = t.Toplam, KalanAnapara = t.KalanAnapara,
                });
                no++;
            }
            return;
        }
        if (k.Yontem == PlanYontemi.Manuel) return; // rotatif / kredi kartı: serbest ödeme
        foreach (var s in PlanOnizle(k))
            db.KrediTaksitleri.Add(new KrediTaksit
            {
                KrediId = k.Id, Versiyon = versiyon, TaksitNo = s.No, VadeTarihi = s.Vade, Anapara = s.Anapara, Faiz = s.Faiz,
                Bsmv = s.Bsmv, Kkdf = s.Kkdf, Toplam = s.Toplam, KalanAnapara = s.KalanAnapara,
            });
    }

    private static async Task KullandirimIsleAsync(IFiloDbContext db, Kredi k, CancellationToken ct)
    {
        var acik = $"{k.Tur.Ad()} kullanımı {k.KrediNo}";
        Defter.Cari(db, k.BankaCariId, k.KullandirimTarihi, 0, k.Anapara, KaynakTip.KrediKullanim, k.Id, acik, k.ParaBirimi, k.KullandirimKur);
        if (k.KullandirimKasaHesapId is { } kasaId)
        {
            var net = k.Anapara - k.DosyaMasrafi;
            var kasaTutar = await KasaTutariAsync(db, kasaId, k.ParaBirimi, net, k.KullandirimKur, ct);
            Defter.Kasa(db, kasaId, k.KullandirimTarihi, kasaTutar.Tutar, 0, KaynakTip.KrediKullanim, k.Id,
                acik + (k.DosyaMasrafi > 0 ? $" (dosya masrafı {k.DosyaMasrafi:N2} düşülerek)" : ""), k.BankaCariId, kasaTutar.Kur);
        }
    }

    /// <summary>Kredi PB'sindeki tutarın kasa PB'sindeki karşılığı (aynı PB veya TL kasa + dövizli kredi).</summary>
    private static async Task<(decimal Tutar, decimal Kur)> KasaTutariAsync(IFiloDbContext db, byte kasaId, ParaBirimi krediPb, decimal tutar, decimal kur, CancellationToken ct)
    {
        var kasaPb = await db.KasaHesaplari.Where(h => h.Id == kasaId).Select(h => h.ParaBirimi).SingleAsync(ct);
        if (kasaPb == krediPb) return (tutar, krediPb == ParaBirimi.TRY ? 1 : kur);
        if (kasaPb == ParaBirimi.TRY) return (Para.TL(tutar, kur), 1);
        throw new DomainException($"{krediPb.Ad()} kredi {kasaPb.Ad()} hesabından ödenemez; aynı para birimindeki veya TL hesabı seçin.");
    }

    /// <summary>Banka planını değiştirir/içe aktarır. Ödeme yoksa mevcut plan değişir; varsa açık taksitler yeni versiyona taşınır.</summary>
    public async Task PlanDegistirAsync(short krediId, IReadOnlyList<KrediTaksit> yeniPlan, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        if (yeniPlan.Count == 0) throw new DomainException("Plan boş.");
        if (yeniPlan.Any(t => t.Anapara < 0 || t.Faiz < 0)) throw new DomainException("Planda negatif tutar var.");
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var k = await db.Krediler.SingleAsync(x => x.Id == krediId, ct);
        var taksitler = await db.KrediTaksitleri.Where(t => t.KrediId == krediId && t.Versiyon == k.AktifVersiyon).ToListAsync(ct);
        var odemeVar = taksitler.Any(t => t.OdenenTutar > 0) || await db.KrediOdemeleri.AnyAsync(o => o.KrediId == krediId && !o.Iptal, ct);
        if (!odemeVar)
        {
            db.KrediTaksitleri.RemoveRange(taksitler);
            await db.KaydetAsync(ct);
            PlanEkle(db, k, k.AktifVersiyon, yeniPlan);
        }
        else
        {
            foreach (var t in taksitler.Where(t => t.Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi)) t.Durum = TaksitDurum.PlanDegisti;
            k.AktifVersiyon++;
            PlanEkle(db, k, k.AktifVersiyon, yeniPlan);
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task SilAsync(short krediId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        await using var db = factory.Olustur();
        if (await db.KrediOdemeleri.AnyAsync(o => o.KrediId == krediId && !o.Iptal, ct))
            throw new DomainException("Ödemesi olan kredi silinemez; ödemeleri iptal edin veya krediyi kapatın.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var k = await db.Krediler.SingleAsync(x => x.Id == krediId, ct);
        await Defter.TersKayitAsync(db, KaynakTip.KrediKullanim, k.Id, saat.Bugun, "kredi silindi", ct);
        db.Krediler.Remove(k);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ------------------------------------------------------------------ Ödemeler

    public sealed record TaksitOdemeOnizleme(decimal Kalan, decimal GecikmeFaizi, int GecikmeGun)
    {
        public decimal Toplam => Kalan + GecikmeFaizi;
    }

    /// <summary>Taksit ödeme ekranı için: kalan tutar ve (geç ödemede) gecikme faizi önerisi.</summary>
    public async Task<TaksitOdemeOnizleme> TaksitOnizleAsync(int taksitId, DateOnly tarih, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var t = await db.KrediTaksitleri.AsNoTracking().Include(x => x.Kredi).SingleAsync(x => x.Id == taksitId, ct);
        var gun = Math.Max(0, tarih.DayNumber - t.VadeTarihi.DayNumber);
        var gecikme = KrediHesap.GecikmeFaizi(t.KalanTutar, t.Kredi!.AylikOran, t.Kredi.GecikmeArtisYuzde, gun, t.Kredi.BsmvOrani, t.Kredi.KkdfOrani);
        return new TaksitOdemeOnizleme(t.KalanTutar, gecikme, gun);
    }

    /// <summary>Taksit ödemesi (tam veya kısmi). Tutar boşsa kalan + gecikme faizi ödenir. Dağılım: gecikme → BSMV → KKDF → faiz → anapara.</summary>
    public async Task<int> TaksitOdeAsync(int taksitId, DateOnly tarih, byte kasaHesapId, decimal? tutar, decimal? gecikmeFaizi, decimal kur = 1m, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var t = await db.KrediTaksitleri.Include(x => x.Kredi).SingleAsync(x => x.Id == taksitId, ct);
        var k = t.Kredi!;
        if (k.Durum == KrediDurum.Kapandi) throw new DomainException("Kredi kapanmış.");
        if (t.Durum is not (TaksitDurum.Bekliyor or TaksitDurum.Kismi)) throw new DomainException("Bu taksit ödenmiş veya plan dışı kalmış.");
        if (t.Versiyon != k.AktifVersiyon) throw new DomainException("Bu taksit eski plana ait.");
        var onceki = await db.KrediOdemeleri.Where(o => o.KrediTaksitId == taksitId && !o.Iptal)
            .GroupBy(o => 1).Select(g => new { B = g.Sum(o => o.Bsmv), K = g.Sum(o => o.Kkdf), F = g.Sum(o => o.Faiz), A = g.Sum(o => o.Anapara) })
            .FirstOrDefaultAsync(ct);
        var gun = Math.Max(0, tarih.DayNumber - t.VadeTarihi.DayNumber);
        var gecikme = gecikmeFaizi ?? KrediHesap.GecikmeFaizi(t.KalanTutar, k.AylikOran, k.GecikmeArtisYuzde, gun, k.BsmvOrani, k.KkdfOrani);
        var odenecek = tutar ?? t.KalanTutar + gecikme;
        if (odenecek <= 0) throw new DomainException("Ödeme tutarı sıfırdan büyük olmalıdır.");
        var d = KrediHesap.Dagit(odenecek, gecikme, t.Bsmv - (onceki?.B ?? 0), t.Kkdf - (onceki?.K ?? 0), t.Faiz - (onceki?.F ?? 0), t.Anapara - (onceki?.A ?? 0));
        t.OdenenTutar += d.Toplam - d.Gecikme;
        t.Durum = t.OdenenTutar >= t.Toplam - 0.009m ? TaksitDurum.Odendi : TaksitDurum.Kismi;
        var o = await OdemeIsleAsync(db, k, t.Id, tarih, KrediOdemeTuru.Taksit, kasaHesapId, kur, d, 0, $"Taksit {t.TaksitNo}", ct);
        await KapanisKontrolAsync(db, k, tarih, ct);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return o.Id;
    }

    /// <summary>
    /// Ara (kısmi) ödeme: anaparadan <paramref name="anapara"/> düşülür; son vadeden bu yana işlemiş faiz + vergiler ve komisyon
    /// eklenir; kalan taksitler seçilen moda göre yeni plan versiyonuna çevrilir.
    /// </summary>
    public async Task<int> AraOdemeAsync(short krediId, DateOnly tarih, byte kasaHesapId, decimal anapara, YenidenPlanModu mod, decimal komisyonYuzde,
        decimal kur = 1m, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        if (anapara <= 0) throw new DomainException("Ödenecek anapara sıfırdan büyük olmalıdır.");
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var k = await db.Krediler.SingleAsync(x => x.Id == krediId, ct);
        var (kalan, acik, sonVade) = await KalanDurumAsync(db, k, ct);
        if (acik.Any(t => t.VadeTarihi < tarih)) throw new DomainException("Vadesi geçmiş taksit varken ara ödeme yapılamaz; önce geciken taksitleri ödeyin.");
        if (anapara >= kalan) throw new DomainException($"Ara ödeme kalan anaparadan ({kalan:N2}) küçük olmalı; tamamı için 'Erken Kapat' kullanın.");
        var (faiz, bsmv, kkdf) = KrediHesap.IslemisFaiz(kalan, k.AylikOran, k.BsmvOrani, k.KkdfOrani, sonVade, tarih);
        var komisyon = Para.Yuvarla(anapara * komisyonYuzde / 100m);
        var o = await OdemeIsleAsync(db, k, null, tarih, KrediOdemeTuru.KismiOdeme, kasaHesapId, kur, new OdemeDagilimi(0, bsmv, kkdf, faiz, anapara), komisyon,
            $"Ara ödeme ({mod.Ad()})", ct);

        // Yeni plan: kalan anapara − ara ödeme; işlemiş faiz ödendiği için sonraki taksitin faizi ödeme tarihinden itibaren işler.
        if (acik.Count > 0)
        {
            var ilk = acik.OrderBy(t => t.VadeTarihi).First();
            var mevcutTaksit = acik.OrderBy(t => t.VadeTarihi).Select(t => t.Toplam).First();
            var yeni = KrediHesap.YenidenPlanla(kalan - anapara, k.AylikOran, k.BsmvOrani, k.KkdfOrani, (short)acik.Count, ilk.VadeTarihi, mevcutTaksit, mod, ilk.TaksitNo);
            foreach (var t in acik) t.Durum = TaksitDurum.PlanDegisti;
            k.AktifVersiyon++;
            foreach (var s in yeni)
                db.KrediTaksitleri.Add(new KrediTaksit
                {
                    KrediId = k.Id, Versiyon = k.AktifVersiyon, TaksitNo = s.No, VadeTarihi = s.Vade, Anapara = s.Anapara, Faiz = s.Faiz,
                    Bsmv = s.Bsmv, Kkdf = s.Kkdf, Toplam = s.Toplam, KalanAnapara = s.KalanAnapara,
                });
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return o.Id;
    }

    public sealed record ErkenKapamaOnizleme(decimal KalanAnapara, decimal Faiz, decimal Vergi, decimal Komisyon, decimal GecikenTaksitler)
    {
        public decimal Toplam => KalanAnapara + Faiz + Vergi + Komisyon + GecikenTaksitler;
    }

    public async Task<ErkenKapamaOnizleme> ErkenKapamaOnizleAsync(short krediId, DateOnly tarih, decimal komisyonYuzde, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var k = await db.Krediler.AsNoTracking().SingleAsync(x => x.Id == krediId, ct);
        var (kalan, acik, sonVade) = await KalanDurumAsync(db, k, ct);
        var geciken = acik.Where(t => t.VadeTarihi < tarih).ToList();
        var gecikenFaizVergi = geciken.Sum(t => t.Faiz + t.Bsmv + t.Kkdf) - geciken.Sum(t => Math.Min(t.OdenenTutar, t.Faiz + t.Bsmv + t.Kkdf));
        var sonVadeEtkin = geciken.Count > 0 ? geciken.Max(t => t.VadeTarihi) : sonVade;
        var (faiz, bsmv, kkdf) = KrediHesap.IslemisFaiz(kalan, k.AylikOran, k.BsmvOrani, k.KkdfOrani, sonVadeEtkin, tarih);
        return new ErkenKapamaOnizleme(kalan, faiz, bsmv + kkdf, Para.Yuvarla(kalan * komisyonYuzde / 100m), Math.Max(0, gecikenFaizVergi));
    }

    /// <summary>Kalan anaparanın tamamı + işlemiş faiz + vergiler + komisyon ödenir; açık taksitler kapanır, kredi kapanır.</summary>
    public async Task<int> ErkenKapatAsync(short krediId, DateOnly tarih, byte kasaHesapId, decimal komisyonYuzde, decimal kur = 1m, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        var on = await ErkenKapamaOnizleAsync(krediId, tarih, komisyonYuzde, ct);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var k = await db.Krediler.SingleAsync(x => x.Id == krediId, ct);
        var (kalan, acik, _) = await KalanDurumAsync(db, k, ct);
        if (kalan <= 0) throw new DomainException("Kredinin kalan anaparası yok.");
        var vergiOrani = k.BsmvOrani + k.KkdfOrani;
        var bsmv = vergiOrani == 0 ? 0 : Para.Yuvarla(on.Vergi * k.BsmvOrani / vergiOrani);
        var o = await OdemeIsleAsync(db, k, null, tarih, KrediOdemeTuru.ErkenKapama, kasaHesapId, kur,
            new OdemeDagilimi(0, bsmv, on.Vergi - bsmv, on.Faiz + on.GecikenTaksitler, kalan), on.Komisyon, "Erken kapama", ct);
        foreach (var t in acik) t.Durum = TaksitDurum.Kapatildi;
        k.Durum = KrediDurum.Kapandi;
        k.KapanisTarihi = tarih;
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return o.Id;
    }

    /// <summary>Rotatif / kredi kartı / manuel kredilerde serbest ödeme (bileşenler elle).</summary>
    public async Task<int> SerbestOdemeAsync(short krediId, DateOnly tarih, byte kasaHesapId, decimal anapara, decimal faiz, decimal bsmv, decimal kkdf,
        decimal kur, string? aciklama, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        if (anapara < 0 || faiz < 0 || bsmv < 0 || kkdf < 0 || anapara + faiz + bsmv + kkdf <= 0) throw new DomainException("Tutarlar geçersiz.");
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var k = await db.Krediler.SingleAsync(x => x.Id == krediId, ct);
        var (kalan, _, _) = await KalanDurumAsync(db, k, ct);
        if (anapara > kalan + 0.009m) throw new DomainException($"Anapara ödemesi kalan anaparadan ({kalan:N2}) fazla olamaz.");
        var o = await OdemeIsleAsync(db, k, null, tarih, KrediOdemeTuru.Serbest, kasaHesapId, kur, new OdemeDagilimi(0, bsmv, kkdf, faiz, anapara), 0, aciklama ?? "Ödeme", ct);
        await KapanisKontrolAsync(db, k, tarih, ct);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return o.Id;
    }

    /// <summary>Yalnızca kredinin son ödemesi iptal edilebilir (plan sırası korunur).</summary>
    public async Task OdemeIptalAsync(int odemeId, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var o = await db.KrediOdemeleri.SingleAsync(x => x.Id == odemeId, ct);
        if (o.Iptal) return;
        var sonId = await db.KrediOdemeleri.Where(x => x.KrediId == o.KrediId && !x.Iptal).MaxAsync(x => x.Id, ct);
        if (sonId != o.Id) throw new DomainException("Yalnızca son ödeme iptal edilebilir; önce daha sonraki ödemeleri iptal edin.");
        var k = await db.Krediler.SingleAsync(x => x.Id == o.KrediId, ct);
        if (o.Tur is KrediOdemeTuru.KismiOdeme or KrediOdemeTuru.ErkenKapama)
        {
            // Planı ödemeden önceki haline döndür.
            if (o.Tur == KrediOdemeTuru.KismiOdeme)
            {
                await db.KrediTaksitleri.Where(t => t.KrediId == k.Id && t.Versiyon == k.AktifVersiyon).ExecuteDeleteAsync(ct);
                k.AktifVersiyon--;
                foreach (var t in await db.KrediTaksitleri.Where(t => t.KrediId == k.Id && t.Versiyon == k.AktifVersiyon && t.Durum == TaksitDurum.PlanDegisti).ToListAsync(ct))
                    t.Durum = t.OdenenTutar > 0 ? TaksitDurum.Kismi : TaksitDurum.Bekliyor;
            }
            else
            {
                foreach (var t in await db.KrediTaksitleri.Where(t => t.KrediId == k.Id && t.Versiyon == k.AktifVersiyon && t.Durum == TaksitDurum.Kapatildi).ToListAsync(ct))
                    t.Durum = t.OdenenTutar > 0 ? TaksitDurum.Kismi : TaksitDurum.Bekliyor;
            }
        }
        if (o.KrediTaksitId is { } tid)
        {
            var t = await db.KrediTaksitleri.SingleAsync(x => x.Id == tid, ct);
            t.OdenenTutar = Math.Max(0, t.OdenenTutar - (o.Tutar - o.GecikmeFaizi));
            t.Durum = t.OdenenTutar > 0 ? TaksitDurum.Kismi : TaksitDurum.Bekliyor;
        }
        k.Durum = KrediDurum.Aktif;
        k.KapanisTarihi = null;
        o.Iptal = true;
        o.Aciklama = $"{o.Aciklama} [İPTAL: {neden}]";
        await Defter.TersKayitAsync(db, KaynakTip.KrediOdeme, o.Id, saat.Bugun, neden, ct);
        if (o.Aciklama?.Contains("(v1 aktarım)") == true && o.KrediTaksitId is { } v1Taksit)
        {
            // v1'den aktarılan ödeme: kasa kaydı eski "Finansman Taksiti" tipinde; o ters çevrilir ve
            // geri gelen anapara borcu banka carisine yazılır (v2 defter düzeniyle tutarlı kalsın).
            await Defter.TersKayitAsync(db, KaynakTip.FinansmanTaksit, v1Taksit, saat.Bugun, neden, ct);
            if (o.Anapara > 0)
                Defter.Cari(db, k.BankaCariId, saat.Bugun, 0, o.Anapara, KaynakTip.KrediOdeme, o.Id,
                    $"v1 kredi ödemesi iptali {k.KrediNo} — anapara borcu geri", k.ParaBirimi, o.Kur);
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Değişken faiz: kalan anapara yeni oranla, kalan taksit sayısı korunarak yeniden planlanır (yeni plan versiyonu).
    /// Ödenmiş taksitler değişmez; açık taksitler "Plan değişti" olur.
    /// </summary>
    public async Task FaizDegistirAsync(short krediId, decimal yeniOran, FaizTipi tip, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanYaz);
        if (yeniOran < 0) throw new DomainException("Faiz oranı negatif olamaz.");
        var d = await DetayAsync(krediId, ct: ct);
        var k = d.Kredi;
        if (k.Durum == KrediDurum.Kapandi) throw new DomainException("Kredi kapanmış.");
        var acik = d.Plan.Where(p => p.Versiyon == k.AktifVersiyon && p.Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi).OrderBy(p => p.Vade).ToList();
        if (acik.Count == 0) throw new DomainException("Açık taksit yok; yeniden planlanacak bir şey kalmadı.");
        if (acik.Any(p => p.Odenen > 0)) throw new DomainException("Kısmi ödenmiş taksit var; önce o taksiti tamamlayın.");
        var aylik = tip == FaizTipi.Aylik ? yeniOran / 100m : yeniOran / 1200m;
        var plan = KrediHesap.YenidenPlanla(d.Ozet.KalanAnapara, aylik, k.BsmvOrani, k.KkdfOrani, (short)acik.Count, acik[0].Vade, 0,
                YenidenPlanModu.TaksitiDusur, acik[0].No)
            .Select(s => new KrediTaksit
            {
                TaksitNo = s.No, VadeTarihi = s.Vade, Anapara = s.Anapara, Faiz = s.Faiz, Bsmv = s.Bsmv, Kkdf = s.Kkdf, Toplam = s.Toplam, KalanAnapara = s.KalanAnapara,
            }).ToList();
        var odemeVar = d.Odemeler.Any(o => !o.Iptal);
        if (!odemeVar)
        {
            // Hiç ödeme yoksa kredi kaydı da yeni oranla güncellenir (plan baştan kurulur).
            await using var db = factory.Olustur();
            var kayit = await db.Krediler.SingleAsync(x => x.Id == krediId, ct);
            kayit.FaizOrani = yeniOran;
            kayit.FaizTipi = tip;
            await db.KaydetAsync(ct);
            await PlanDegistirAsync(krediId, plan, ct);
            return;
        }
        // Ödemeli kredide finansal alanlar kilitlidir; oran plan versiyonu ile değişir, kredi kartında not düşülür.
        await PlanDegistirAsync(krediId, plan, ct);
        await using (var db = factory.Olustur())
        {
            var kayit = await db.Krediler.SingleAsync(x => x.Id == krediId, ct);
            kayit.FaizOrani = yeniOran;
            kayit.FaizTipi = tip;
            kayit.Aciklama = Metin.Temizle($"{kayit.Aciklama} | {saat.Bugun:dd.MM.yyyy}: faiz %{yeniOran:0.##} {(tip == FaizTipi.Aylik ? "aylık" : "yıllık")} oldu");
            await db.KaydetAsync(ct);
        }
    }

    private async Task<KrediOdeme> OdemeIsleAsync(IFiloDbContext db, Kredi k, int? taksitId, DateOnly tarih, KrediOdemeTuru tur, byte kasaId, decimal kur,
        OdemeDagilimi d, decimal komisyon, string aciklama, CancellationToken ct)
    {
        if (k.ParaBirimi == ParaBirimi.TRY) kur = 1;
        else if (kur <= 1) kur = await doviz.KurAsync(k.ParaBirimi, tarih, ct) ?? throw new DomainException("Dövizli kredi ödemesi için kur girilmelidir.");
        var toplam = d.Toplam + komisyon;
        var (kasaTutar, kasaKur) = await KasaTutariAsync(db, kasaId, k.ParaBirimi, toplam, kur, ct);
        await Defter.KasaCikisKontrolAsync(db, kasaId, kasaTutar, ct);
        var o = new KrediOdeme
        {
            KrediId = k.Id, KrediTaksitId = taksitId, Tarih = tarih, Tur = tur, KasaHesapId = kasaId, Tutar = toplam, Kur = kur, KasaTutar = kasaTutar,
            Anapara = d.Anapara, Faiz = d.Faiz, Bsmv = d.Bsmv, Kkdf = d.Kkdf, GecikmeFaizi = d.Gecikme, Komisyon = komisyon, Aciklama = aciklama,
        };
        db.KrediOdemeleri.Add(o);
        await db.KaydetAsync(ct);
        var metin = $"{k.KrediNo} {aciklama}";
        var gider = toplam - d.Anapara;
        if (gider > 0) Defter.Cari(db, k.BankaCariId, tarih, 0, gider, KaynakTip.KrediOdeme, o.Id, $"{metin} faiz/vergi/masraf", k.ParaBirimi, kur);
        Defter.Cari(db, k.BankaCariId, tarih, toplam, 0, KaynakTip.KrediOdeme, o.Id, metin, k.ParaBirimi, kur);
        Defter.Kasa(db, kasaId, tarih, 0, kasaTutar, KaynakTip.KrediOdeme, o.Id, metin, k.BankaCariId, kasaKur);
        return o;
    }

    private static async Task KapanisKontrolAsync(IFiloDbContext db, Kredi k, DateOnly tarih, CancellationToken ct)
    {
        await db.KaydetAsync(ct);
        var odenen = await db.KrediOdemeleri.Where(o => o.KrediId == k.Id && !o.Iptal).SumAsync(o => o.Anapara, ct);
        if (odenen >= k.Anapara - 0.009m)
        {
            k.Durum = KrediDurum.Kapandi;
            k.KapanisTarihi = tarih;
        }
    }

    /// <summary>Kalan anapara (ödemelerden), aktif plandaki açık taksitler ve son ödenmiş vade (faiz başlangıcı).</summary>
    private static async Task<(decimal Kalan, List<KrediTaksit> Acik, DateOnly SonVade)> KalanDurumAsync(IFiloDbContext db, Kredi k, CancellationToken ct)
    {
        var odenen = await db.KrediOdemeleri.Where(o => o.KrediId == k.Id && !o.Iptal).SumAsync(o => o.Anapara, ct);
        var aktif = await db.KrediTaksitleri.Where(t => t.KrediId == k.Id && t.Versiyon == k.AktifVersiyon).ToListAsync(ct);
        var acik = aktif.Where(t => t.Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi).ToList();
        var odenmisVadeler = await db.KrediTaksitleri.Where(t => t.KrediId == k.Id && t.Durum == TaksitDurum.Odendi).Select(t => (DateOnly?)t.VadeTarihi).MaxAsync(ct);
        var sonOdeme = await db.KrediOdemeleri.Where(o => o.KrediId == k.Id && !o.Iptal && o.Tur != KrediOdemeTuru.Taksit).Select(o => (DateOnly?)o.Tarih).MaxAsync(ct);
        var son = new[] { odenmisVadeler, sonOdeme, (DateOnly?)k.KullandirimTarihi }.Where(x => x is not null).Max()!.Value;
        return (k.Anapara - odenen, acik, son);
    }

    // ------------------------------------------------------------------ Özetler

    public async Task<List<TakvimSatir>> TakvimAsync(DateOnly bas, DateOnly bit, bool odenmisDahil = false, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanGor);
        var bugun = saat.Bugun;
        await using var db = factory.Olustur();
        var liste = await db.KrediTaksitleri.AsNoTracking()
            .Where(t => t.VadeTarihi >= bas && t.VadeTarihi <= bit && t.Versiyon == t.Kredi!.AktifVersiyon && t.Kredi.Durum == KrediDurum.Aktif
                        && (odenmisDahil ? t.Durum != TaksitDurum.PlanDegisti : t.Durum == TaksitDurum.Bekliyor || t.Durum == TaksitDurum.Kismi))
            .OrderBy(t => t.VadeTarihi)
            .Select(t => new { t.Id, t.KrediId, t.Kredi!.KrediNo, Banka = t.Kredi.Banka!.Unvan, t.TaksitNo, t.VadeTarihi, Kalan = t.Toplam - t.OdenenTutar, t.Kredi.ParaBirimi, t.Durum })
            .ToListAsync(ct);
        var kurlar = await GuncelKurlarAsync(ct);
        return liste.Select(t => new TakvimSatir(t.Id, t.KrediId, t.KrediNo, t.Banka, t.TaksitNo, t.VadeTarihi, t.Kalan, t.ParaBirimi,
            Para.TL(t.Kalan, kurlar[t.ParaBirimi]), t.VadeTarihi < bugun && t.Durum != TaksitDurum.Odendi)).ToList();
    }

    public async Task<KrediGenelOzet> GenelOzetAsync(CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanGor);
        var bugun = saat.Bugun;
        var krediler = await ListeAsync(false, ct);
        var kurlar = await GuncelKurlarAsync(ct);
        decimal Tl(decimal t, ParaBirimi pb) => Para.TL(t, kurlar[pb]);
        var takvim = await TakvimAsync(bugun.AddYears(-5), bugun.AddMonths(12), false, ct);
        var ayBas = new DateOnly(bugun.Year, bugun.Month, 1);
        var ayBit = ayBas.AddMonths(1).AddDays(-1);

        await using var db = factory.Olustur();
        var limitler = await db.BankaLimitleri.AsNoTracking().Select(l => new { l.BankaCariId, l.LimitTutar, l.ParaBirimi }).ToListAsync(ct);
        var bankaIds = await db.Krediler.AsNoTracking().Where(k => k.Durum == KrediDurum.Aktif).Select(k => new { k.Id, k.BankaCariId }).ToDictionaryAsync(k => k.Id, k => k.BankaCariId, ct);
        var bankalar = krediler.GroupBy(k => (Id: bankaIds[k.Id], k.Banka)).Select(g => new BankaRiskSatir(g.Key.Id, g.Key.Banka, g.Count(),
                g.Sum(k => Tl(k.KalanAnapara, k.ParaBirimi)),
                takvim.Where(t => g.Any(k => k.Id == t.KrediId) && t.Vade >= ayBas && t.Vade <= ayBit).Sum(t => t.TutarTL),
                limitler.Where(l => l.BankaCariId == g.Key.Id).Select(l => (decimal?)Tl(l.LimitTutar, l.ParaBirimi)).Sum()))
            .OrderByDescending(b => b.KalanAnaparaTL).ToList();

        var planlar = await db.KrediTaksitleri.AsNoTracking()
            .Where(t => t.Versiyon == t.Kredi!.AktifVersiyon && t.Kredi.Durum == KrediDurum.Aktif && (t.Durum == TaksitDurum.Bekliyor || t.Durum == TaksitDurum.Kismi)
                        && t.VadeTarihi <= bugun.AddMonths(12))
            .Select(t => new { t.VadeTarihi, t.Anapara, Faiz = t.Faiz + t.Bsmv + t.Kkdf, t.Kredi!.ParaBirimi }).ToListAsync(ct);
        var aylik = Enumerable.Range(0, 12).Select(i => ayBas.AddMonths(i)).Select(ay => (ay,
                planlar.Where(p => p.VadeTarihi.Year == ay.Year && p.VadeTarihi.Month == ay.Month).Sum(p => Tl(p.Anapara, p.ParaBirimi)),
                planlar.Where(p => p.VadeTarihi.Year == ay.Year && p.VadeTarihi.Month == ay.Month).Sum(p => Tl(p.Faiz, p.ParaBirimi))))
            .ToList();

        return new KrediGenelOzet(
            krediler.Sum(k => Tl(k.KalanAnapara, k.ParaBirimi)),
            takvim.Where(t => t.Vade >= ayBas && t.Vade <= ayBit).Sum(t => t.TutarTL),
            takvim.Where(t => t.Vade >= bugun && t.Vade <= bugun.AddDays(30)).Sum(t => t.TutarTL),
            takvim.Where(t => t.Vade > bugun.AddDays(30) && t.Vade <= bugun.AddDays(60)).Sum(t => t.TutarTL),
            takvim.Where(t => t.Vade > bugun.AddDays(60) && t.Vade <= bugun.AddDays(90)).Sum(t => t.TutarTL),
            takvim.Count(t => t.Gecikti), takvim.Where(t => t.Gecikti).Sum(t => t.TutarTL),
            bankalar,
            krediler.GroupBy(k => k.ParaBirimi).Select(g => (g.Key.Ad(), g.Sum(k => k.KalanAnapara), g.Sum(k => Tl(k.KalanAnapara, k.ParaBirimi)))).ToList(),
            aylik);
    }

    /// <summary>Dönemdeki tüm kredi ödemeleri (faiz gideri raporu): ödeme kuru ile TL karşılıkları.</summary>
    public async Task<List<KrediOdemeRaporSatir>> OdemelerAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansmanGor);
        await using var db = factory.Olustur();
        return await db.KrediOdemeleri.AsNoTracking().Where(o => !o.Iptal && o.Tarih >= bas && o.Tarih <= bit).OrderBy(o => o.Tarih)
            .Select(o => new KrediOdemeRaporSatir(o.Tarih, o.Kredi!.Banka!.Unvan, o.Kredi.KrediNo, o.Kredi.Tur, o.Tur, o.Kredi.ParaBirimi, o.Kur,
                o.Anapara, o.Faiz, o.Bsmv + o.Kkdf, o.GecikmeFaizi + o.Komisyon, o.Tutar))
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------ Banka planı içe aktarma

    /// <summary>
    /// Banka ödeme planı tablosunu (Excel/CSV satırları) taksitlere çevirir. Başlıklar esnek eşlenir:
    /// Vade/Tarih, Taksit No/Sıra, Anapara, Faiz, BSMV, KKDF (veya tek "Vergi"), Taksit Tutarı/Toplam, Kalan Anapara/Bakiye.
    /// Kalan anapara verilmemişse hesaplanır; toplam verilmemişse bileşenlerden bulunur.
    /// </summary>
    public static List<KrediTaksit> BankaPlaniAyristir(IReadOnlyList<Dictionary<string, string?>> satirlar, decimal anapara)
    {
        if (satirlar.Count == 0) throw new DomainException("Dosyada satır bulunamadı.");
        static string N(string s) => new(s.ToLower(new System.Globalization.CultureInfo("tr-TR"))
            .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g').Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c')
            .Where(char.IsLetterOrDigit).ToArray());
        var basliklar = satirlar[0].Keys.ToList();
        var kullanilan = new HashSet<string>();
        string? Bul(params string[] adaylar)
        {
            var bos = basliklar.Where(b => !kullanilan.Contains(b)).ToList();
            var k = adaylar.Select(a => bos.FirstOrDefault(b => N(b) == a)).FirstOrDefault(x => x is not null)
                    ?? adaylar.Select(a => bos.FirstOrDefault(b => N(b).Contains(a))).FirstOrDefault(x => x is not null);
            if (k is not null) kullanilan.Add(k);
            return k;
        }
        var kVade = Bul("vadetarihi", "vade", "odemetarihi", "tarih") ?? throw new DomainException("Planda 'Vade' / 'Tarih' kolonu bulunamadı.");
        var kNo = Bul("taksitno", "no", "sirano", "sira", "taksitsira");
        var kKalan = Bul("kalananapara", "anaparabakiyesi", "kalananaparabakiyesi", "bakiye", "kalan");
        var kAnapara = Bul("anapara", "anaparatutari") ?? throw new DomainException("Planda 'Anapara' kolonu bulunamadı.");
        var kFaiz = Bul("faiz", "faiztutari", "akdifaiz");
        var kBsmv = Bul("bsmv");
        var kKkdf = Bul("kkdf");
        var kVergi = kBsmv is null && kKkdf is null ? Bul("vergi", "vergiler") : null;
        var kToplam = Bul("taksittutari", "taksit", "toplam", "odenecektutar", "tutar");

        var sonuc = new List<KrediTaksit>();
        foreach (var r in satirlar)
        {
            var vade = IceAktarmaServisi.TarihCoz(r.GetValueOrDefault(kVade), null);
            var ana = IceAktarmaServisi.SayiCoz(r.GetValueOrDefault(kAnapara));
            if (vade is null || ana is null) continue; // toplam / boş satırları atla
            decimal S(string? k) => k is null ? 0 : IceAktarmaServisi.SayiCoz(r.GetValueOrDefault(k)) ?? 0;
            var t = new KrediTaksit
            {
                VadeTarihi = DateOnly.FromDateTime(vade.Value), Anapara = ana.Value, Faiz = S(kFaiz),
                Bsmv = kVergi is not null ? S(kVergi) : S(kBsmv), Kkdf = S(kKkdf), Toplam = S(kToplam), KalanAnapara = S(kKalan),
                TaksitNo = (short)(kNo is not null && IceAktarmaServisi.SayiCoz(r.GetValueOrDefault(kNo)) is { } no and > 0 and < 1000 ? no : 0),
            };
            if (t.Toplam <= 0) t.Toplam = t.Anapara + t.Faiz + t.Bsmv + t.Kkdf;
            sonuc.Add(t);
        }
        if (sonuc.Count == 0) throw new DomainException("Planda geçerli taksit satırı bulunamadı (vade ve anapara dolu olmalı).");
        sonuc = sonuc.OrderBy(t => t.VadeTarihi).ToList();
        var kalan = anapara;
        short sira = 1;
        foreach (var t in sonuc)
        {
            kalan -= t.Anapara;
            if (kKalan is null || t.KalanAnapara == 0 && kalan > 0.01m) t.KalanAnapara = Math.Max(0, kalan);
            if (t.TaksitNo == 0) t.TaksitNo = sira;
            sira++;
        }
        var fark = sonuc.Sum(t => t.Anapara) - anapara;
        if (Math.Abs(fark) > 1m)
            throw new DomainException($"Plandaki anapara toplamı ({sonuc.Sum(t => t.Anapara):N2}) kredi anaparasından ({anapara:N2}) farklı. " +
                                      "Ara ödeme sonrası plan ise kalan anaparayla uyumlu olmalıdır.");
        return sonuc;
    }

    private async Task<Dictionary<ParaBirimi, decimal>> GuncelKurlarAsync(CancellationToken ct)
    {
        var d = new Dictionary<ParaBirimi, decimal>();
        foreach (var pb in Enum.GetValues<ParaBirimi>())
            d[pb] = await doviz.KurAsync(pb, saat.Bugun, ct) ?? 1m;
        return d;
    }
}
