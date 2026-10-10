using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Ortak;

/// <summary>
/// Cari ve kasa defterlerine satır yazan tek nokta. Tüm finansal belgeler defter satırlarını buradan üretir.
/// Satırlar değişmez; iptal/düzeltme <see cref="TersKayitAsync"/> ile yapılır.
/// </summary>
public static class Defter
{
    /// <summary>Cari satırı. Tutar belge para birimindedir; TL karşılığı kur ile hesaplanır.</summary>
    public static CariHareket Cari(IFiloDbContext db, int cariId, DateOnly tarih, decimal borc, decimal alacak,
        KaynakTip kaynak, int? kaynakId, string? aciklama, ParaBirimi pb = ParaBirimi.TRY, decimal kur = 1m, DateOnly? vade = null,
        short? aracId = null, int? seferId = null)
    {
        if (borc < 0 || alacak < 0) throw new DomainException("Defter tutarları negatif olamaz.");
        var dovizli = pb != ParaBirimi.TRY;
        if (!dovizli) kur = 1m;
        var h = new CariHareket
        {
            CariId = cariId,
            Tarih = tarih,
            VadeTarihi = vade,
            Borc = Para.TL(borc, kur),
            Alacak = Para.TL(alacak, kur),
            ParaBirimi = pb,
            Kur = kur,
            DovizBorc = dovizli && borc > 0 ? borc : null,
            DovizAlacak = dovizli && alacak > 0 ? alacak : null,
            KaynakTip = kaynak,
            KaynakId = kaynakId,
            Aciklama = Kisalt(aciklama),
            AracId = aracId,
            SeferId = seferId,
        };
        db.CariHareketleri.Add(h);
        return h;
    }

    /// <summary>Kasa satırı. Tutar kasanın kendi para birimindedir.</summary>
    public static KasaHareket Kasa(IFiloDbContext db, byte kasaHesapId, DateOnly tarih, decimal giris, decimal cikis,
        KaynakTip kaynak, int? kaynakId, string? aciklama, int? cariId = null, decimal kur = 1m)
    {
        if (giris < 0 || cikis < 0) throw new DomainException("Kasa tutarları negatif olamaz.");
        var h = new KasaHareket
        {
            KasaHesapId = kasaHesapId,
            Tarih = tarih,
            Giris = Para.Yuvarla(giris),
            Cikis = Para.Yuvarla(cikis),
            Kur = kur,
            CariId = cariId,
            KaynakTip = kaynak,
            KaynakId = kaynakId,
            Aciklama = Kisalt(aciklama),
        };
        db.KasaHareketleri.Add(h);
        return h;
    }

    /// <summary>Kaynağa ait, henüz iptal edilmemiş tüm cari ve kasa satırlarının ters kaydını oluşturur.</summary>
    public static async Task TersKayitAsync(IFiloDbContext db, KaynakTip kaynak, int kaynakId, DateOnly tarih, string neden, CancellationToken ct = default)
    {
        var cariler = await db.CariHareketleri
            .Where(h => h.KaynakTip == kaynak && h.KaynakId == kaynakId && h.TersKayitId == null
                        && !db.CariHareketleri.Any(t => t.TersKayitId == h.Id))
            .ToListAsync(ct);
        foreach (var h in cariler)
        {
            db.CariHareketleri.Add(new CariHareket
            {
                CariId = h.CariId, Tarih = tarih, VadeTarihi = h.VadeTarihi,
                Borc = h.Alacak, Alacak = h.Borc, ParaBirimi = h.ParaBirimi, Kur = h.Kur,
                DovizBorc = h.DovizAlacak, DovizAlacak = h.DovizBorc,
                KaynakTip = kaynak, KaynakId = kaynakId, TersKayitId = h.Id, AracId = h.AracId, SeferId = h.SeferId,
                Aciklama = Kisalt($"İPTAL ({neden}): {h.Aciklama}"),
            });
        }

        var kasalar = await db.KasaHareketleri
            .Where(h => h.KaynakTip == kaynak && h.KaynakId == kaynakId && h.TersKayitId == null
                        && !db.KasaHareketleri.Any(t => t.TersKayitId == h.Id))
            .ToListAsync(ct);
        foreach (var h in kasalar)
        {
            db.KasaHareketleri.Add(new KasaHareket
            {
                KasaHesapId = h.KasaHesapId, Tarih = tarih, Giris = h.Cikis, Cikis = h.Giris, Kur = h.Kur, CariId = h.CariId,
                KaynakTip = kaynak, KaynakId = kaynakId, TersKayitId = h.Id,
                Aciklama = Kisalt($"İPTAL ({neden}): {h.Aciklama}"),
            });
        }
    }

    /// <summary>Kasa bakiyesini (kasanın para biriminde) hesaplar; açılış bakiyesi dahil.</summary>
    public static async Task<decimal> KasaBakiyeAsync(IFiloDbContext db, byte kasaHesapId, CancellationToken ct = default)
    {
        var acilis = await db.KasaHesaplari.Where(k => k.Id == kasaHesapId).Select(k => k.AcilisBakiye).SingleAsync(ct);
        var hareket = await db.KasaHareketleri.Where(h => h.KasaHesapId == kasaHesapId).SumAsync(h => h.Giris - h.Cikis, ct);
        return acilis + hareket;
    }

    /// <summary>Kasadan çıkış yapılmadan önce yeterli bakiye kontrolü (eksiye izin verilen hesaplar hariç).</summary>
    public static async Task KasaCikisKontrolAsync(IFiloDbContext db, byte kasaHesapId, decimal cikis, CancellationToken ct = default)
    {
        var hesap = await db.KasaHesaplari.AsNoTracking().SingleOrDefaultAsync(k => k.Id == kasaHesapId, ct)
                    ?? throw new DomainException("Kasa/banka hesabı bulunamadı.");
        if (!hesap.Aktif) throw new DomainException($"'{hesap.Ad}' hesabı pasif.");
        if (hesap.EksiBakiyeIzin) return;
        var bakiye = await KasaBakiyeAsync(db, kasaHesapId, ct);
        if (bakiye - cikis < 0)
            throw new DomainException($"'{hesap.Ad}' bakiyesi yetersiz. Bakiye: {bakiye:N2}, çıkış: {cikis:N2}.");
    }

    /// <summary>Cari bakiyesi (TL): pozitif = cari bize borçlu, negatif = biz cariye borçluyuz.</summary>
    public static Task<decimal> CariBakiyeAsync(IFiloDbContext db, int cariId, CancellationToken ct = default) =>
        db.CariHareketleri.Where(h => h.CariId == cariId).SumAsync(h => h.Borc - h.Alacak, ct);

    public static async Task<int> IslemNoAsync(IFiloDbContext db, CancellationToken ct = default) =>
        (int)await db.SiradakiNumaraAsync("IslemNoSeq", ct);

    private static string? Kisalt(string? s) => s is { Length: > 200 } ? s[..200] : s;
}
