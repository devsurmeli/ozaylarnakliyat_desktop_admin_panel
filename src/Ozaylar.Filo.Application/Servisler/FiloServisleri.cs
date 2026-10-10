using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Servisler;

public sealed class AracServisi(IFiloDbContextFactory f, IKullaniciOturumu o, IEnumerable<IValidator<Arac>> v, ISaat saat)
    : KayitServisi<Arac>(f, o, v)
{
    protected override void Normallestir(Arac kayit)
    {
        base.Normallestir(kayit);
        kayit.Plaka = Metin.Plaka(kayit.Plaka);
        kayit.SaseNo = kayit.SaseNo?.ToUpperInvariant();
    }

    protected override async Task OnceSilAsync(IFiloDbContext db, Arac kayit, CancellationToken ct)
    {
        if (await db.Seferler.AnyAsync(s => s.AracId == kayit.Id && s.Durum != SeferDurum.Iptal && s.Durum < SeferDurum.Kapatildi, ct))
            throw new DomainException("Aracın açık seferi var; önce seferleri kapatın veya iptal edin.");
    }

    /// <summary>Çekiciye dorse bağlar; aracın önceki dorsesi ve dorsenin önceki çekicisi ile bağlantıyı kapatır.</summary>
    public async Task DorseBaglaAsync(short aracId, short? dorseId, DateOnly tarih, CancellationToken ct = default)
    {
        Oturum.YetkiGerekli(Izinler.FiloYaz);
        await using var db = Factory.Olustur();
        var acik = await db.AracDorseEslesmeleri
            .Where(e => e.Bitis == null && (e.AracId == aracId || (dorseId != null && e.DorseId == dorseId)))
            .ToListAsync(ct);
        foreach (var e in acik)
        {
            if (e.AracId == aracId && e.DorseId == dorseId) return; // zaten bağlı
            e.Bitis = tarih < e.Baslangic ? e.Baslangic : tarih;
        }
        if (dorseId is not null)
            db.AracDorseEslesmeleri.Add(new AracDorseEslesme { AracId = aracId, DorseId = dorseId.Value, Baslangic = tarih });
        await db.KaydetAsync(ct);
    }

    public async Task<Dictionary<short, (short DorseId, string Plaka)>> GuncelDorselerAsync(CancellationToken ct = default)
    {
        await using var db = Factory.Olustur();
        return await db.AracDorseEslesmeleri.AsNoTracking()
            .Where(e => e.Bitis == null)
            .Select(e => new { e.AracId, e.DorseId, e.Dorse!.Plaka })
            .ToDictionaryAsync(e => e.AracId, e => (e.DorseId, e.Plaka), ct);
    }

    public async Task<List<AracSatir>> SatirlarAsync(bool sadeceAktif, CancellationToken ct = default)
    {
        Oturum.YetkiGerekli(Izinler.FiloGor);
        await using var db = Factory.Olustur();
        var bugun = saat.Bugun;
        return await db.Araclar.AsNoTracking()
            .Where(a => !sadeceAktif || a.Aktif)
            .OrderBy(a => a.Plaka)
            .Select(a => new AracSatir(
                a.Id, a.Plaka, a.AracTipi, a.Marka, a.Model, a.ModelYili, a.Mulkiyet, a.GuncelKm, a.Aktif,
                db.AracDorseEslesmeleri.Where(e => e.AracId == a.Id && e.Bitis == null).Select(e => e.Dorse!.Plaka).FirstOrDefault(),
                db.Evraklar.Count(e => e.AracId == a.Id && e.BitisTarihi != null && e.BitisTarihi < bugun
                    && !db.Evraklar.Any(y => y.AracId == a.Id && y.EvrakTuruId == e.EvrakTuruId && y.BitisTarihi > e.BitisTarihi))))
            .ToListAsync(ct);
    }
}

public sealed record AracSatir(short Id, string Plaka, AracTipi Tip, string? Marka, string? Model, short? ModelYili,
    Mulkiyet Mulkiyet, int GuncelKm, bool Aktif, string? Dorse, int SuresiDolanEvrak);

public sealed class DorseServisi(IFiloDbContextFactory f, IKullaniciOturumu o, IEnumerable<IValidator<Dorse>> v)
    : KayitServisi<Dorse>(f, o, v)
{
    protected override void Normallestir(Dorse kayit)
    {
        base.Normallestir(kayit);
        kayit.Plaka = Metin.Plaka(kayit.Plaka);
    }
}

public sealed class SoforServisi(IFiloDbContextFactory f, IKullaniciOturumu o, IEnumerable<IValidator<Sofor>> v)
    : KayitServisi<Sofor>(f, o, v)
{
    protected override void Normallestir(Sofor kayit)
    {
        base.Normallestir(kayit);
        kayit.IBAN = kayit.IBAN?.Replace(" ", "").ToUpperInvariant();
    }

    protected override async Task OnceKaydetAsync(IFiloDbContext db, Sofor kayit, bool yeni, CancellationToken ct)
    {
        var unvan = $"{kayit.Ad} {kayit.Soyad} (Şoför)";
        if (yeni)
        {
            // Her şoförün bir personel carisi vardır: avans/harcama/harcırah bakiyesi buradan izlenir.
            var cari = new Cari
            {
                Kod = await CariServisi.YeniKodAsync(db, ct),
                Unvan = unvan,
                CariTipi = CariTipi.Personel,
                VergiNo = kayit.TCKN,
                Telefon = kayit.Telefon,
                IBAN = kayit.IBAN,
            };
            // Navigasyon üzerinden bağlanır: cari ve şoför aynı SaveChanges'te (atomik) eklenir.
            kayit.Cari = cari;
        }
        else
        {
            var cari = await db.Cariler.FindAsync([kayit.CariId], ct);
            if (cari is not null)
            {
                cari.Unvan = unvan;
                cari.Telefon = kayit.Telefon;
                cari.IBAN = kayit.IBAN;
                cari.Aktif = kayit.Aktif;
            }
        }
    }

    protected override async Task OnceSilAsync(IFiloDbContext db, Sofor kayit, CancellationToken ct)
    {
        if (await Defter.CariBakiyeAsync(db, kayit.CariId, ct) != 0)
            throw new DomainException("Şoförün açık bakiyesi var; önce mahsuplaşın.");
        var cari = await db.Cariler.FindAsync([kayit.CariId], ct);
        if (cari is not null) db.Cariler.Remove(cari);
    }
}

public sealed class CariServisi(IFiloDbContextFactory f, IKullaniciOturumu o, IEnumerable<IValidator<Cari>> v)
    : KayitServisi<Cari>(f, o, v)
{
    public static async Task<string> YeniKodAsync(IFiloDbContext db, CancellationToken ct) =>
        $"C{await db.SiradakiNumaraAsync("CariKodSeq", ct):D5}";

    protected override async Task OnceKaydetAsync(IFiloDbContext db, Cari kayit, bool yeni, CancellationToken ct)
    {
        if (yeni && string.IsNullOrWhiteSpace(kayit.Kod)) kayit.Kod = await YeniKodAsync(db, ct);
        if (!yeni && (kayit.CariTipi & CariTipi.Personel) == 0
                  && await db.Soforler.AnyAsync(s => s.CariId == kayit.Id, ct))
            kayit.CariTipi |= CariTipi.Personel;
    }

    protected override async Task OnceSilAsync(IFiloDbContext db, Cari kayit, CancellationToken ct)
    {
        if (await db.CariHareketleri.AnyAsync(h => h.CariId == kayit.Id, ct))
            throw new DomainException("Hareketi olan cari silinemez; pasife alabilirsiniz.");
        if (await db.Soforler.AnyAsync(s => s.CariId == kayit.Id, ct))
            throw new DomainException("Bu cari bir şoföre bağlı; şoför kartından işlem yapın.");
    }

    /// <summary>Seçim listeleri için cariler (tipe göre filtreli).</summary>
    public async Task<List<Secim>> SecimListesiAsync(CariTipi? tip = null, CancellationToken ct = default)
    {
        await using var db = Factory.Olustur();
        var q = db.Cariler.AsNoTracking().Where(c => c.Aktif);
        if (tip is not null) q = q.Where(c => (c.CariTipi & tip.Value) != 0);
        return await q.OrderBy(c => c.Unvan).Select(c => new Secim(c.Id, c.Unvan)).ToListAsync(ct);
    }
}

public sealed class EvrakServisi(IFiloDbContextFactory f, IKullaniciOturumu o, IEnumerable<IValidator<Evrak>> v, ISaat saat)
    : KayitServisi<Evrak>(f, o, v)
{
    /// <summary>
    /// Her (hedef, evrak türü) için en son evrakı alır; bitişi uyarı süresi içinde veya geçmiş olanları listeler.
    /// Yenilenmiş (daha geç bitişli kaydı olan) evraklar listelenmez.
    /// </summary>
    public async Task<List<EvrakUyari>> UyarilarAsync(int? ekGun = null, CancellationToken ct = default)
    {
        await using var db = Factory.Olustur();
        var bugun = saat.Bugun;
        var ham = await db.Evraklar.AsNoTracking()
            .Where(e => e.BitisTarihi != null)
            .Where(e => (e.AracId == null || e.Arac!.Aktif) && (e.DorseId == null || e.Dorse!.Aktif) && (e.SoforId == null || e.Sofor!.Aktif))
            .Where(e => !db.Evraklar.Any(y => y.EvrakTuruId == e.EvrakTuruId && y.AracId == e.AracId && y.DorseId == e.DorseId
                                              && y.SoforId == e.SoforId && y.BitisTarihi > e.BitisTarihi))
            .Select(e => new
            {
                e.Id, e.EvrakTuruId, Tur = e.EvrakTuru!.Ad, e.EvrakTuru.UyariGun, e.EvrakTuru.SeferEngeller, e.BitisTarihi, e.BelgeNo,
                Hedef = e.AracId != null ? e.Arac!.Plaka : e.DorseId != null ? e.Dorse!.Plaka
                    : e.SoforId != null ? e.Sofor!.Ad + " " + e.Sofor.Soyad : "Firma",
                HedefTip = e.AracId != null ? EvrakHedef.Arac : e.DorseId != null ? EvrakHedef.Dorse : e.SoforId != null ? EvrakHedef.Sofor : EvrakHedef.Firma,
            })
            .ToListAsync(ct);
        return ham
            .Select(e => new EvrakUyari(e.Id, e.Tur, e.Hedef, e.HedefTip, e.BelgeNo, e.BitisTarihi!.Value,
                e.BitisTarihi.Value.DayNumber - bugun.DayNumber, e.SeferEngeller))
            .Where(e => e.KalanGun <= (ekGun ?? ham.First(h => h.Id == e.EvrakId).UyariGun))
            .OrderBy(e => e.KalanGun)
            .ToList();
    }
}

public sealed record EvrakUyari(int EvrakId, string Tur, string Hedef, EvrakHedef HedefTip, string? BelgeNo, DateOnly Bitis, int KalanGun, bool SeferEngeller)
{
    public string Durum => KalanGun < 0 ? $"{-KalanGun} gün geçti" : KalanGun == 0 ? "Bugün doluyor" : $"{KalanGun} gün kaldı";
    public bool SuresiDoldu => KalanGun < 0;
}
