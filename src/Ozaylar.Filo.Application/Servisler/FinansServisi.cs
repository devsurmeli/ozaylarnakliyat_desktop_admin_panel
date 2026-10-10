using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record CariBakiyeSatir(int Id, string Kod, string Unvan, CariTipi Tip, string? Telefon, decimal Borc, decimal Alacak, decimal? RiskLimiti, bool Aktif)
{
    public decimal Bakiye => Borc - Alacak;
    public string Durum => Bakiye > 0 ? "Borçlu" : Bakiye < 0 ? "Alacaklı" : "-";
    public bool RiskAsildi => RiskLimiti is > 0 && Bakiye > RiskLimiti;
}

public sealed record EkstreSatir(int Id, DateOnly Tarih, DateOnly? Vade, string Islem, string? Aciklama,
    decimal Borc, decimal Alacak, decimal Bakiye, string? Doviz, bool Iptal);

public sealed record Ekstre(string Baslik, decimal Devreden, IReadOnlyList<EkstreSatir> Satirlar)
{
    public decimal ToplamBorc => Satirlar.Sum(s => s.Borc);
    public decimal ToplamAlacak => Satirlar.Sum(s => s.Alacak);
    public decimal Bakiye => Devreden + ToplamBorc - ToplamAlacak;
}

public sealed record YaslandirmaSatir(int CariId, string Unvan, YaslandirmaSonuc Sonuc);

public sealed record KasaBakiyeSatir(byte Id, string Ad, KasaHesapTipi Tip, ParaBirimi ParaBirimi, decimal Bakiye, bool Aktif)
{
    public string TipAd => Tip.Ad();
    public string PB => ParaBirimi.Ad();
}

public sealed record TahsilatOdeme(int CariId, byte KasaHesapId, DateOnly Tarih, decimal Tutar, string? Aciklama, decimal Kur = 1m);

public sealed class FinansServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    public async Task<List<CariBakiyeSatir>> CariListesiAsync(CariTipi? tip = null, bool sadeceAktif = true, bool sadeceBakiyeli = false, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        var q = db.Cariler.AsNoTracking();
        if (sadeceAktif) q = q.Where(c => c.Aktif);
        if (tip is not null) q = q.Where(c => (c.CariTipi & tip.Value) != 0);
        var liste = await q.OrderBy(c => c.Unvan)
            .Select(c => new CariBakiyeSatir(c.Id, c.Kod, c.Unvan, c.CariTipi, c.Telefon,
                db.CariHareketleri.Where(h => h.CariId == c.Id).Sum(h => (decimal?)h.Borc) ?? 0,
                db.CariHareketleri.Where(h => h.CariId == c.Id).Sum(h => (decimal?)h.Alacak) ?? 0,
                c.RiskLimiti, c.Aktif))
            .ToListAsync(ct);
        return sadeceBakiyeli ? liste.Where(c => c.Bakiye != 0).ToList() : liste;
    }

    public async Task<Ekstre> CariEkstreAsync(int cariId, DateOnly baslangic, DateOnly bitis, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        var unvan = await db.Cariler.Where(c => c.Id == cariId).Select(c => c.Unvan).SingleAsync(ct);
        var devreden = await db.CariHareketleri.Where(h => h.CariId == cariId && h.Tarih < baslangic).SumAsync(h => h.Borc - h.Alacak, ct);
        var ham = await db.CariHareketleri.AsNoTracking()
            .Where(h => h.CariId == cariId && h.Tarih >= baslangic && h.Tarih <= bitis)
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id)
            .Select(h => new
            {
                h.Id, h.Tarih, h.VadeTarihi, h.KaynakTip, h.Aciklama, h.Borc, h.Alacak, h.ParaBirimi, h.DovizBorc, h.DovizAlacak, h.Kur,
                Iptal = h.TersKayitId != null || db.CariHareketleri.Any(t => t.TersKayitId == h.Id),
            })
            .ToListAsync(ct);
        var bakiye = devreden;
        var satirlar = ham.Select(h =>
        {
            bakiye += h.Borc - h.Alacak;
            var doviz = h.ParaBirimi == ParaBirimi.TRY ? null : $"{h.DovizBorc ?? h.DovizAlacak:N2} {h.ParaBirimi.Ad()} @ {h.Kur:N4}";
            return new EkstreSatir(h.Id, h.Tarih, h.VadeTarihi, h.KaynakTip.Ad(), h.Aciklama, h.Borc, h.Alacak, bakiye, doviz, h.Iptal);
        }).ToList();
        return new Ekstre(unvan, devreden, satirlar);
    }

    /// <summary>Cari bazında FIFO yaşlandırma (yalnızca bize borçlu cariler).</summary>
    public async Task<List<YaslandirmaSatir>> YaslandirmaAsync(CariTipi tip = CariTipi.Musteri, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        var bugun = saat.Bugun;
        var hareketler = await db.CariHareketleri.AsNoTracking()
            .Where(h => (h.Cari!.CariTipi & tip) != 0)
            .Select(h => new { h.CariId, h.Cari!.Unvan, Vade = h.VadeTarihi ?? h.Tarih, h.Borc, h.Alacak })
            .ToListAsync(ct);
        return hareketler.GroupBy(h => (h.CariId, h.Unvan))
            .Select(g => new YaslandirmaSatir(g.Key.CariId, g.Key.Unvan,
                Yaslandirma.Hesapla(g.Select(h => (h.Vade, h.Borc, h.Alacak)), bugun)))
            .Where(y => y.Sonuc.Toplam > 0)
            .OrderByDescending(y => y.Sonuc.Gun90Ustu).ThenByDescending(y => y.Sonuc.Toplam)
            .ToList();
    }

    /// <summary>Cariden tahsilat: kasaya giriş, cariye alacak.</summary>
    public Task<int> TahsilatAsync(TahsilatOdeme t, CancellationToken ct = default) => TahsilatOdemeAsync(t, tahsilat: true, ct);

    /// <summary>Cariye ödeme: kasadan çıkış, cariye borç.</summary>
    public Task<int> OdemeAsync(TahsilatOdeme t, CancellationToken ct = default) => TahsilatOdemeAsync(t, tahsilat: false, ct);

    private async Task<int> TahsilatOdemeAsync(TahsilatOdeme t, bool tahsilat, CancellationToken ct)
    {
        oturum.YetkiGerekli(Izinler.FinansYaz);
        if (t.Tutar <= 0) throw new DomainException("Tutar sıfırdan büyük olmalıdır.");
        await using var db = factory.Olustur();
        var kasa = await db.KasaHesaplari.AsNoTracking().SingleOrDefaultAsync(k => k.Id == t.KasaHesapId, ct) ?? throw new DomainException("Kasa/banka seçilmelidir.");
        var kur = kasa.ParaBirimi == ParaBirimi.TRY ? 1m : t.Kur;
        if (kasa.ParaBirimi != ParaBirimi.TRY && kur <= 0) throw new DomainException("Dövizli hesap için kur girilmelidir.");
        var unvan = await db.Cariler.Where(c => c.Id == t.CariId).Select(c => c.Unvan).SingleOrDefaultAsync(ct) ?? throw new DomainException("Cari seçilmelidir.");
        if (!tahsilat) await Defter.KasaCikisKontrolAsync(db, t.KasaHesapId, t.Tutar, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var no = await Defter.IslemNoAsync(db, ct);
        var tip = tahsilat ? KaynakTip.Tahsilat : KaynakTip.Odeme;
        var acik = t.Aciklama ?? (tahsilat ? $"Tahsilat - {unvan}" : $"Ödeme - {unvan}");
        Defter.Kasa(db, t.KasaHesapId, t.Tarih, tahsilat ? t.Tutar : 0, tahsilat ? 0 : t.Tutar, tip, no, acik, t.CariId, kur);
        Defter.Cari(db, t.CariId, t.Tarih, tahsilat ? 0 : t.Tutar, tahsilat ? t.Tutar : 0, tip, no, acik, kasa.ParaBirimi, kur);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return no;
    }

    /// <summary>Hesaplar arası transfer. Farklı para birimlerinde hedef tutar ayrıca verilir.</summary>
    public async Task<int> VirmanAsync(byte kaynakId, byte hedefId, DateOnly tarih, decimal tutar, decimal? hedefTutar, string? aciklama, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansYaz);
        if (kaynakId == hedefId) throw new DomainException("Kaynak ve hedef hesap aynı olamaz.");
        if (tutar <= 0) throw new DomainException("Tutar sıfırdan büyük olmalıdır.");
        await using var db = factory.Olustur();
        var hesaplar = await db.KasaHesaplari.AsNoTracking().Where(k => k.Id == kaynakId || k.Id == hedefId).ToDictionaryAsync(k => k.Id, ct);
        if (hesaplar.Count != 2) throw new DomainException("Hesap bulunamadı.");
        var ayniPb = hesaplar[kaynakId].ParaBirimi == hesaplar[hedefId].ParaBirimi;
        var hedefMiktar = ayniPb ? tutar : hedefTutar ?? throw new DomainException("Farklı para birimleri arasında virmanda hedef tutar girilmelidir.");
        await Defter.KasaCikisKontrolAsync(db, kaynakId, tutar, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var no = await Defter.IslemNoAsync(db, ct);
        var acik = aciklama ?? $"Virman: {hesaplar[kaynakId].Ad} → {hesaplar[hedefId].Ad}";
        Defter.Kasa(db, kaynakId, tarih, 0, tutar, KaynakTip.Virman, no, acik);
        Defter.Kasa(db, hedefId, tarih, hedefMiktar, 0, KaynakTip.Virman, no, acik);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return no;
    }

    /// <summary>Açılış bakiyesi veya düzeltme kaydı (yalnızca cari defterine).</summary>
    public async Task<int> CariManuelKayitAsync(int cariId, DateOnly tarih, decimal borc, decimal alacak, string aciklama, bool acilis,
        ParaBirimi pb = ParaBirimi.TRY, decimal kur = 1m, DateOnly? vade = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansYaz);
        if (borc < 0 || alacak < 0 || borc + alacak == 0 || (borc > 0 && alacak > 0))
            throw new DomainException("Borç veya alacaktan yalnızca biri girilmelidir.");
        if (string.IsNullOrWhiteSpace(aciklama)) throw new DomainException("Açıklama zorunludur.");
        await using var db = factory.Olustur();
        var no = await Defter.IslemNoAsync(db, ct);
        Defter.Cari(db, cariId, tarih, borc, alacak, acilis ? KaynakTip.Acilis : KaynakTip.Manuel, no, aciklama, pb, kur, vade);
        await db.KaydetAsync(ct);
        return no;
    }

    /// <summary>Tahsilat/ödeme/virman/manuel işlemin iptali (ters kayıt).</summary>
    public async Task IslemIptalAsync(KaynakTip tip, int islemNo, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        if (tip is not (KaynakTip.Tahsilat or KaynakTip.Odeme or KaynakTip.Virman or KaynakTip.Manuel or KaynakTip.Acilis))
            throw new DomainException("Bu kayıt kendi belgesinden (fatura, avans, masraf ...) iptal edilmelidir.");
        await using var db = factory.Olustur();
        await Defter.TersKayitAsync(db, tip, islemNo, saat.Bugun, neden, ct);
        await db.KaydetAsync(ct);
    }

    public async Task<List<KasaBakiyeSatir>> KasaListesiAsync(bool sadeceAktif = true, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        return await db.KasaHesaplari.AsNoTracking()
            .Where(k => !sadeceAktif || k.Aktif)
            .OrderBy(k => k.HesapTipi).ThenBy(k => k.Ad)
            .Select(k => new KasaBakiyeSatir(k.Id, k.Ad, k.HesapTipi, k.ParaBirimi,
                k.AcilisBakiye + (db.KasaHareketleri.Where(h => h.KasaHesapId == k.Id).Sum(h => (decimal?)(h.Giris - h.Cikis)) ?? 0), k.Aktif))
            .ToListAsync(ct);
    }

    public async Task<Ekstre> KasaEkstreAsync(byte kasaHesapId, DateOnly baslangic, DateOnly bitis, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        var hesap = await db.KasaHesaplari.AsNoTracking().SingleAsync(k => k.Id == kasaHesapId, ct);
        var devreden = hesap.AcilisBakiye + await db.KasaHareketleri.Where(h => h.KasaHesapId == kasaHesapId && h.Tarih < baslangic).SumAsync(h => h.Giris - h.Cikis, ct);
        var ham = await db.KasaHareketleri.AsNoTracking()
            .Where(h => h.KasaHesapId == kasaHesapId && h.Tarih >= baslangic && h.Tarih <= bitis)
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id)
            .Select(h => new
            {
                h.Id, h.Tarih, h.KaynakTip, h.KaynakId, h.Aciklama, h.Giris, h.Cikis, Cari = h.Cari != null ? h.Cari.Unvan : null,
                Iptal = h.TersKayitId != null || db.KasaHareketleri.Any(t => t.TersKayitId == h.Id),
            })
            .ToListAsync(ct);
        var bakiye = devreden;
        var satirlar = ham.Select(h =>
        {
            bakiye += h.Giris - h.Cikis;
            return new EkstreSatir(h.Id, h.Tarih, null, h.KaynakTip.Ad(), h.Cari is null ? h.Aciklama : $"{h.Cari} - {h.Aciklama}",
                h.Giris, h.Cikis, bakiye, h.KaynakId?.ToString(), h.Iptal);
        }).ToList();
        return new Ekstre($"{hesap.Ad} ({hesap.ParaBirimi.Ad()})", devreden, satirlar);
    }

    /// <summary>Ekstredeki satırın iptal edilebilmesi için kaynak tip/no bilgisi.</summary>
    public async Task<(KaynakTip Tip, int? No)> KasaSatirKaynagiAsync(int kasaHareketId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var h = await db.KasaHareketleri.AsNoTracking().Where(x => x.Id == kasaHareketId).Select(x => new { x.KaynakTip, x.KaynakId }).SingleAsync(ct);
        return (h.KaynakTip, h.KaynakId);
    }

    public async Task<(KaynakTip Tip, int? No)> CariSatirKaynagiAsync(int cariHareketId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var h = await db.CariHareketleri.AsNoTracking().Where(x => x.Id == cariHareketId).Select(x => new { x.KaynakTip, x.KaynakId }).SingleAsync(ct);
        return (h.KaynakTip, h.KaynakId);
    }
}
