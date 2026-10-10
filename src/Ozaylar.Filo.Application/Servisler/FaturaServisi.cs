using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record FaturaSatir(int Id, FaturaTipi Tip, string FaturaNo, DateOnly Tarih, DateOnly? Vade, string Cari,
    decimal AraToplam, decimal Kdv, decimal Tevkifat, decimal GenelToplam, ParaBirimi ParaBirimi, FaturaDurum Durum, string? EBelgeNo)
{
    public string TipAd => Tip.Ad();
    public string DurumAd => Durum.Ad();
    public string PB => ParaBirimi.Ad();
}

public sealed record FaturalanmamisSefer(int SeferId, string SeferNo, DateOnly Tarih, string Musteri, string Guzergah, string? Plaka,
    decimal Navlun, ParaBirimi ParaBirimi, decimal Kur, byte KdvOrani, byte TevkifatPay)
{
    public string PB => ParaBirimi.Ad();
}

public sealed class FaturaServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    public async Task<List<FaturaSatir>> ListeAsync(DateOnly bas, DateOnly bit, FaturaTipi? tip = null, int? cariId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        var q = db.Faturalar.AsNoTracking().Where(f => f.Tarih >= bas && f.Tarih <= bit);
        if (tip is not null) q = q.Where(f => f.FaturaTipi == tip);
        if (cariId is not null) q = q.Where(f => f.CariId == cariId);
        return await q.OrderByDescending(f => f.Tarih).ThenByDescending(f => f.Id)
            .Select(f => new FaturaSatir(f.Id, f.FaturaTipi, f.FaturaNo, f.Tarih, f.VadeTarihi, f.Cari!.Unvan,
                f.AraToplam, f.KdvToplam, f.TevkifatToplam, f.GenelToplam, f.ParaBirimi, f.Durum, f.EBelgeNo))
            .ToListAsync(ct);
    }

    public async Task<Fatura?> GetirAsync(int id, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        return await db.Faturalar.AsNoTracking().Include(f => f.Kalemler.OrderBy(k => k.SiraNo)).SingleOrDefaultAsync(f => f.Id == id, ct);
    }

    /// <summary>Kapatılmış/teslim edilmiş ama henüz faturalanmamış seferler.</summary>
    public async Task<List<FaturalanmamisSefer>> FaturalanmamisSeferlerAsync(int? musteriId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        var q = db.Seferler.AsNoTracking().Where(s => s.Durum == SeferDurum.TeslimEdildi || s.Durum == SeferDurum.Kapatildi);
        if (musteriId is not null) q = q.Where(s => s.MusteriCariId == musteriId);
        return await q
            .Where(s => !db.FaturaKalemleri.Any(k => k.SeferId == s.Id && k.Fatura!.Durum == FaturaDurum.Kayitli && k.Fatura.FaturaTipi == FaturaTipi.Satis))
            .OrderBy(s => s.PlanlananTarih)
            .Select(s => new FaturalanmamisSefer(s.Id, s.SeferNo, s.PlanlananTarih, s.Musteri!.Unvan,
                s.CikisLokasyon!.Ad + " → " + s.VarisLokasyon!.Ad, s.Arac != null ? s.Arac.Plaka : s.TaseronPlaka,
                s.NavlunTutar, s.NavlunParaBirimi, s.NavlunKur, s.NavlunKdvOrani, s.NavlunTevkifatPay))
            .ToListAsync(ct);
    }

    /// <summary>Seçilen seferlerden fatura kalemleri üretir (kaydetmez).</summary>
    public async Task<List<FaturaKalem>> SeferKalemleriAsync(IEnumerable<int> seferIdleri, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var ids = seferIdleri.ToList();
        var seferler = await db.Seferler.AsNoTracking().Where(s => ids.Contains(s.Id))
            .Select(s => new
            {
                s.Id, s.SeferNo, s.NavlunTutar, s.NavlunKdvOrani, s.NavlunTevkifatPay, s.YukTanimi, s.CmrNo,
                Cikis = s.CikisLokasyon!.Ad, Varis = s.VarisLokasyon!.Ad, Plaka = s.Arac != null ? s.Arac.Plaka : s.TaseronPlaka,
            })
            .ToListAsync(ct);
        return seferler.Select(s => new FaturaKalem
        {
            SeferId = s.Id,
            Aciklama = $"Nakliye {s.Cikis} - {s.Varis} / {s.Plaka} ({s.SeferNo}{(s.CmrNo is null ? "" : " CMR " + s.CmrNo)})",
            Miktar = 1, Birim = "Sefer", BirimFiyat = s.NavlunTutar, KdvOrani = s.NavlunKdvOrani, TevkifatPay = s.NavlunTevkifatPay,
        }).ToList();
    }

    public async Task<Fatura> KaydetAsync(Fatura gelen, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansYaz);
        gelen.FaturaNo = gelen.FaturaNo?.Trim() ?? "";
        if (string.IsNullOrEmpty(gelen.FaturaNo)) throw new DomainException("Fatura numarası zorunludur.");
        if (gelen.CariId == 0) throw new DomainException("Cari seçilmelidir.");
        if (gelen.Kalemler.Count == 0) throw new DomainException("Faturada en az bir kalem olmalıdır.");
        if (gelen.Kalemler.Any(k => string.IsNullOrWhiteSpace(k.Aciklama))) throw new DomainException("Kalem açıklaması boş olamaz.");
        if (gelen.ParaBirimi == ParaBirimi.TRY) gelen.Kur = 1;
        else if (gelen.Kur <= 0) throw new DomainException("Dövizli fatura için kur girilmelidir.");
        FaturaHesap.Hesapla(gelen);
        if (gelen.GenelToplam <= 0) throw new DomainException("Fatura toplamı sıfırdan büyük olmalıdır.");

        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var bugun = saat.Bugun;
        var satis = gelen.FaturaTipi == FaturaTipi.Satis;
        var seferIdleri = gelen.Kalemler.Where(k => k.SeferId != null).Select(k => k.SeferId!.Value).Distinct().ToList();

        if (satis && seferIdleri.Count > 0)
        {
            var baskaFatura = await db.FaturaKalemleri
                .Where(k => k.SeferId != null && seferIdleri.Contains(k.SeferId.Value) && k.FaturaId != gelen.Id
                            && k.Fatura!.Durum == FaturaDurum.Kayitli && k.Fatura.FaturaTipi == FaturaTipi.Satis)
                .Select(k => new { k.Sefer!.SeferNo, k.Fatura!.FaturaNo }).FirstOrDefaultAsync(ct);
            if (baskaFatura is not null)
                throw new DomainException($"{baskaFatura.SeferNo} numaralı sefer zaten {baskaFatura.FaturaNo} numaralı faturada.");
            var uygunsuz = await db.Seferler.Where(s => seferIdleri.Contains(s.Id) && s.Durum != SeferDurum.TeslimEdildi
                                                         && s.Durum != SeferDurum.Kapatildi && s.Durum != SeferDurum.Faturalandi)
                .Select(s => s.SeferNo).FirstOrDefaultAsync(ct);
            if (uygunsuz is not null) throw new DomainException($"{uygunsuz} henüz teslim edilmemiş; faturalanamaz.");
        }

        Fatura hedef;
        List<int> eskiSeferler = [];
        if (gelen.Id == 0)
        {
            gelen.Durum = FaturaDurum.Kayitli;
            db.Faturalar.Add(gelen);
            hedef = gelen;
            await db.KaydetAsync(ct);
        }
        else
        {
            var mevcut = await db.Faturalar.Include(f => f.Kalemler).SingleOrDefaultAsync(f => f.Id == gelen.Id, ct)
                         ?? throw new DomainException("Fatura bulunamadı.");
            if (mevcut.Durum == FaturaDurum.Iptal) throw new DomainException("İptal edilmiş fatura düzenlenemez.");
            eskiSeferler = mevcut.Kalemler.Where(k => k.SeferId != null).Select(k => k.SeferId!.Value).ToList();
            // Önceki cari kaydını ters çevir, yeni tutarla tekrar işle.
            await Defter.TersKayitAsync(db, KaynakTip.Fatura, mevcut.Id, bugun, "fatura düzeltildi", ct);
            db.FaturaKalemleri.RemoveRange(mevcut.Kalemler);
            hedef = await db.BirlestirAsync(gelen, ct);
            foreach (var k in gelen.Kalemler)
            {
                k.Id = 0;
                k.FaturaId = hedef.Id;
                k.Fatura = null;
                k.Sefer = null;
                db.FaturaKalemleri.Add(k);
            }
            await db.KaydetAsync(ct);
        }

        var cariAd = await db.Cariler.Where(c => c.Id == hedef.CariId).Select(c => c.Unvan).SingleAsync(ct);
        var (borc, alacak) = hedef.FaturaTipi is FaturaTipi.Satis or FaturaTipi.AlisIade ? (hedef.GenelToplam, 0m) : (0m, hedef.GenelToplam);
        Defter.Cari(db, hedef.CariId, hedef.Tarih, borc, alacak, KaynakTip.Fatura, hedef.Id,
            $"{hedef.FaturaTipi.Ad()} faturası {hedef.FaturaNo}", hedef.ParaBirimi, hedef.Kur, hedef.VadeTarihi);

        if (satis) await SeferDurumlariniGuncelleAsync(db, seferIdleri, eskiSeferler.Except(seferIdleri).ToList(), ct);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return hedef;
    }

    public async Task IptalAsync(int faturaId, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var f = await db.Faturalar.Include(x => x.Kalemler).SingleOrDefaultAsync(x => x.Id == faturaId, ct) ?? throw new DomainException("Fatura bulunamadı.");
        if (f.Durum == FaturaDurum.Iptal) return;
        f.Durum = FaturaDurum.Iptal;
        f.Aciklama = Kisalt($"{f.Aciklama} [İPTAL: {neden}]", 500);
        await Defter.TersKayitAsync(db, KaynakTip.Fatura, f.Id, saat.Bugun, neden, ct);
        if (f.FaturaTipi == FaturaTipi.Satis)
            await SeferDurumlariniGuncelleAsync(db, [], f.Kalemler.Where(k => k.SeferId != null).Select(k => k.SeferId!.Value).ToList(), ct);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task SeferDurumlariniGuncelleAsync(IFiloDbContext db, List<int> faturalanan, List<int> cikarilan, CancellationToken ct)
    {
        foreach (var s in await db.Seferler.Where(s => faturalanan.Contains(s.Id)).ToListAsync(ct))
        {
            // Teslimden doğrudan faturalanan taşeron seferinde taşeron bedeli burada işlenir.
            await SeferServisi.TaseronBedelIsleAsync(db, s, saat.Bugun, ct);
            s.Durum = SeferDurum.Faturalandi;
        }
        foreach (var s in await db.Seferler.Where(s => cikarilan.Contains(s.Id) && s.Durum == SeferDurum.Faturalandi).ToListAsync(ct))
            s.Durum = s.AracId is not null && (s.CikisKm is null || s.DonusKm is null) ? SeferDurum.TeslimEdildi : SeferDurum.Kapatildi;
    }

    private static string Kisalt(string s, int n) => s.Length > n ? s[..n] : s;
}
