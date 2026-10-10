using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record BakimSatir(int Id, DateOnly Tarih, string Plaka, BakimTuru Tur, int? Km, string? Servis, string? Aciklama, decimal Tutar, OdemeSekli Odeme, bool Iptal)
{
    public string TurAd => Tur.Ad();
}

public sealed record YaklasanBakim(short PlanId, string Plaka, string BakimAdi, int? KalanKm, int? KalanGun, int? SonrakiKm, DateOnly? SonrakiTarih)
{
    public bool Gecti => KalanKm < 0 || KalanGun < 0;
    public string Durum => Gecti ? "Gecikti" : string.Join(" / ", new[] { KalanKm is null ? null : $"{KalanKm:N0} km", KalanGun is null ? null : $"{KalanGun} gün" }.Where(x => x is not null));
}

public sealed class BakimServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    public async Task<List<BakimSatir>> ListeAsync(DateOnly bas, DateOnly bit, short? aracId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.BakimGor);
        await using var db = factory.Olustur();
        return await db.Bakimlar.AsNoTracking()
            .Where(b => b.Tarih >= bas && b.Tarih <= bit && (aracId == null || b.AracId == aracId))
            .OrderByDescending(b => b.Tarih)
            .Select(b => new BakimSatir(b.Id, b.Tarih, b.Arac != null ? b.Arac.Plaka : b.Dorse!.Plaka, b.BakimTuru, b.Km,
                b.ServisCari != null ? b.ServisCari.Unvan : b.ServisAdi, b.Aciklama, b.ToplamTutar, b.OdemeSekli, b.Iptal))
            .ToListAsync(ct);
    }

    public async Task<Bakim?> GetirAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Bakimlar.AsNoTracking().Include(b => b.Kalemler).SingleOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Bakim> KaydetAsync(Bakim b, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.BakimYaz);
        if ((b.AracId is null) == (b.DorseId is null)) throw new DomainException("Bakım bir araç veya dorseye ait olmalıdır.");
        foreach (var k in b.Kalemler)
        {
            if (string.IsNullOrWhiteSpace(k.Aciklama)) throw new DomainException("Kalem açıklaması boş olamaz.");
            k.Tutar = Para.Yuvarla(k.Miktar * k.BirimFiyat);
        }
        if (b.Kalemler.Count > 0) b.ToplamTutar = b.Kalemler.Sum(k => k.Tutar);
        if (b.ToplamTutar <= 0) throw new DomainException("Bakım tutarı sıfırdan büyük olmalıdır (kalem veya toplam tutar girin).");
        if (b.OdemeSekli == OdemeSekli.Cari && b.ServisCariId is null) throw new DomainException("Veresiye ödemede servis carisi seçilmelidir.");
        if (b.OdemeSekli == OdemeSekli.Kasa && b.KasaHesapId is null) throw new DomainException("Kasa/banka seçilmelidir.");
        if (b.OdemeSekli == OdemeSekli.SoforNakit) throw new DomainException("Bakım için şoför nakit ödemesi yerine masraf kaydı kullanın.");

        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (b.Id == 0)
        {
            if (b.OdemeSekli == OdemeSekli.Kasa) await Defter.KasaCikisKontrolAsync(db, b.KasaHesapId!.Value, b.ToplamTutar, ct);
            db.Bakimlar.Add(b);
        }
        else
        {
            var eski = await db.Bakimlar.Include(x => x.Kalemler).SingleAsync(x => x.Id == b.Id, ct);
            if (eski.Iptal) throw new DomainException("İptal edilmiş bakım düzenlenemez.");
            await Defter.TersKayitAsync(db, KaynakTip.Bakim, b.Id, saat.Bugun, "bakım düzeltildi", ct);
            db.BakimKalemleri.RemoveRange(eski.Kalemler);
            var hedef = await db.BirlestirAsync(b, ct);
            foreach (var k in b.Kalemler) { k.Id = 0; k.BakimId = hedef.Id; db.BakimKalemleri.Add(k); }
        }
        await db.KaydetAsync(ct);

        var plaka = b.AracId is not null
            ? await db.Araclar.Where(a => a.Id == b.AracId).Select(a => a.Plaka).SingleAsync(ct)
            : await db.Dorseler.Where(a => a.Id == b.DorseId).Select(a => a.Plaka).SingleAsync(ct);
        var acik = $"{b.BakimTuru.Ad()} {plaka} {b.Aciklama}";
        if (b.OdemeSekli == OdemeSekli.Kasa) Defter.Kasa(db, b.KasaHesapId!.Value, b.Tarih, 0, b.ToplamTutar, KaynakTip.Bakim, b.Id, acik, b.ServisCariId);
        else if (b.ServisCariId is not null && b.OdemeSekli is OdemeSekli.Cari or OdemeSekli.Kart)
            Defter.Cari(db, b.ServisCariId.Value, b.Tarih, 0, b.ToplamTutar, KaynakTip.Bakim, b.Id, acik);

        // Bakım planını ilerlet; aracın km'sini güncelle.
        if (b.BakimPlaniId is not null)
        {
            var plan = await db.BakimPlanlari.SingleAsync(p => p.Id == b.BakimPlaniId, ct);
            if (plan.SonTarih is null || b.Tarih >= plan.SonTarih)
            {
                plan.SonTarih = b.Tarih;
                plan.SonKm = b.Km ?? plan.SonKm;
            }
        }
        if (b.AracId is not null && b.Km is not null)
        {
            var arac = await db.Araclar.SingleAsync(a => a.Id == b.AracId, ct);
            if (b.Km > arac.GuncelKm) arac.GuncelKm = b.Km.Value;
            AracKm.Kaydet(db, b.AracId.Value, b.Tarih.ToDateTime(new TimeOnly(12, 0)), b.Km.Value, KmKaynak.Bakim, b.Id);
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return b;
    }

    public async Task IptalAsync(int id, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        var b = await db.Bakimlar.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Bakım bulunamadı.");
        if (b.Iptal) return;
        b.Iptal = true;
        await Defter.TersKayitAsync(db, KaynakTip.Bakim, b.Id, saat.Bugun, neden, ct);
        await db.KaydetAsync(ct);
    }

    /// <summary>Km (araç güncel km) veya tarih bazlı yaklaşan/geciken bakımlar.</summary>
    public async Task<List<YaklasanBakim>> YaklasanlarAsync(int kmEsik = 2_000, int gunEsik = 15, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var bugun = saat.Bugun;
        var planlar = await db.BakimPlanlari.AsNoTracking().Where(p => p.Aktif)
            .Where(p => (p.AracId == null || p.Arac!.Aktif) && (p.DorseId == null || p.Dorse!.Aktif))
            .Select(p => new
            {
                p.Id, Plaka = p.Arac != null ? p.Arac.Plaka : p.Dorse!.Plaka, p.BakimAdi, p.PeriyotKm, p.PeriyotGun, p.SonKm, p.SonTarih,
                GuncelKm = p.Arac != null ? (int?)p.Arac.GuncelKm : null,
            }).ToListAsync(ct);
        var sonuc = new List<YaklasanBakim>();
        foreach (var p in planlar)
        {
            int? sonrakiKm = p.PeriyotKm is > 0 ? (p.SonKm ?? 0) + p.PeriyotKm : null;
            DateOnly? sonrakiTarih = p.PeriyotGun is > 0 && p.SonTarih is not null ? p.SonTarih.Value.AddDays(p.PeriyotGun.Value) : null;
            int? kalanKm = sonrakiKm is not null && p.GuncelKm is not null ? sonrakiKm - p.GuncelKm : null;
            int? kalanGun = sonrakiTarih is not null ? sonrakiTarih.Value.DayNumber - bugun.DayNumber : null;
            if (kalanKm <= kmEsik || kalanGun <= gunEsik)
                sonuc.Add(new YaklasanBakim(p.Id, p.Plaka, p.BakimAdi, kalanKm, kalanGun, sonrakiKm, sonrakiTarih));
        }
        return sonuc.OrderBy(y => Math.Min(y.KalanKm / 100 ?? int.MaxValue, y.KalanGun ?? int.MaxValue)).ToList();
    }
}

public sealed record LastikSatir(int Id, string SeriNo, string? Marka, string Ebat, LastikDurum Durum, string? TakiliOlduguYer, string? Pozisyon,
    int ToplamKm, decimal? AlisBedeli, byte KaplamaSayisi)
{
    public string DurumAd => Durum.Ad();
    public decimal? KmMaliyet => AlisBedeli is > 0 && ToplamKm > 0 ? Math.Round(AlisBedeli.Value / ToplamKm, 4) : null;
}

public sealed class LastikServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum)
{
    public async Task<List<LastikSatir>> ListeAsync(bool hurdaDahil = false, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.BakimGor);
        await using var db = factory.Olustur();
        var ham = await db.Lastikler.AsNoTracking()
            .Where(l => hurdaDahil || l.Durum != LastikDurum.Hurda)
            .OrderBy(l => l.SeriNo)
            .Select(l => new
            {
                l.Id, l.SeriNo, l.Marka, l.Ebat, l.Durum, l.AlisBedeli, l.KaplamaSayisi,
                Takili = l.Hareketler.Where(h => h.SokmeTarihi == null)
                    .Select(h => new { Yer = h.Arac != null ? h.Arac.Plaka : h.Dorse!.Plaka, h.Pozisyon, h.TakmaKm, Guncel = h.Arac != null ? (int?)h.Arac.GuncelKm : null })
                    .FirstOrDefault(),
                KapaliKm = l.Hareketler.Where(h => h.SokmeKm != null).Sum(h => (int?)(h.SokmeKm - h.TakmaKm)) ?? 0,
            }).ToListAsync(ct);
        return ham.Select(l => new LastikSatir(l.Id, l.SeriNo, l.Marka, l.Ebat, l.Durum, l.Takili?.Yer, l.Takili?.Pozisyon,
            l.KapaliKm + (l.Takili?.Guncel is { } g ? Math.Max(0, g - l.Takili.TakmaKm) : 0), l.AlisBedeli, l.KaplamaSayisi)).ToList();
    }

    public async Task<List<LastikHareket>> GecmisAsync(int lastikId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.LastikHareketleri.AsNoTracking().Include(h => h.Arac).Include(h => h.Dorse)
            .Where(h => h.LastikId == lastikId).OrderByDescending(h => h.TakmaTarihi).ToListAsync(ct);
    }

    public async Task TakAsync(int lastikId, short? aracId, short? dorseId, string pozisyon, DateOnly tarih, int km, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.BakimYaz);
        if ((aracId is null) == (dorseId is null)) throw new DomainException("Lastik bir araç veya dorseye takılmalıdır.");
        if (string.IsNullOrWhiteSpace(pozisyon)) throw new DomainException("Pozisyon girilmelidir.");
        await using var db = factory.Olustur();
        var l = await db.Lastikler.SingleAsync(x => x.Id == lastikId, ct);
        if (l.Durum == LastikDurum.Takili) throw new DomainException("Lastik zaten takılı; önce sökün.");
        if (l.Durum == LastikDurum.Hurda) throw new DomainException("Hurdaya ayrılmış lastik takılamaz.");
        var dolu = await db.LastikHareketleri.AnyAsync(h => h.SokmeTarihi == null && h.Pozisyon == pozisyon
                                                            && h.AracId == aracId && h.DorseId == dorseId, ct);
        if (dolu) throw new DomainException($"{pozisyon} pozisyonunda takılı başka bir lastik var.");
        l.Durum = LastikDurum.Takili;
        db.LastikHareketleri.Add(new LastikHareket { LastikId = lastikId, AracId = aracId, DorseId = dorseId, Pozisyon = pozisyon.Trim(), TakmaTarihi = tarih, TakmaKm = km });
        await db.KaydetAsync(ct);
    }

    public async Task SokAsync(int lastikId, DateOnly tarih, int km, decimal? disDerinligi, LastikDurum yeniDurum, string? aciklama, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.BakimYaz);
        if (yeniDurum == LastikDurum.Takili) throw new DomainException("Geçersiz durum.");
        await using var db = factory.Olustur();
        var l = await db.Lastikler.SingleAsync(x => x.Id == lastikId, ct);
        var h = await db.LastikHareketleri.SingleOrDefaultAsync(x => x.LastikId == lastikId && x.SokmeTarihi == null, ct)
                ?? throw new DomainException("Lastik takılı değil.");
        if (km < h.TakmaKm) throw new DomainException($"Sökme km'si takma km'sinden ({h.TakmaKm:N0}) küçük olamaz.");
        h.SokmeTarihi = tarih;
        h.SokmeKm = km;
        h.DisDerinligiMm = disDerinligi;
        h.Aciklama = aciklama;
        l.Durum = yeniDurum;
        if (yeniDurum == LastikDurum.Kaplamada) l.KaplamaSayisi++;
        if (yeniDurum == LastikDurum.Hurda) l.HurdaTarihi = tarih;
        await db.KaydetAsync(ct);
    }
}

public sealed record CekSenetSatir(int Id, CekSenetTipi Tip, CekSenetYon Yon, string PortfoyNo, string? SeriNo, string? Banka, string Cari,
    decimal Tutar, ParaBirimi ParaBirimi, DateOnly Vade, CekSenetDurum Durum)
{
    public string TipAd => Tip.Ad();
    public string YonAd => Yon.Ad();
    public string DurumAd => Durum.Ad();
}

public sealed class CekSenetServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    public async Task<List<CekSenetSatir>> ListeAsync(CekSenetYon? yon, bool sadeceAcik, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        await using var db = factory.Olustur();
        var acikDurumlar = new[] { CekSenetDurum.Portfoyde, CekSenetDurum.TahsileVerildi };
        return await db.CekSenetler.AsNoTracking()
            .Where(c => (yon == null || c.Yon == yon) && (!sadeceAcik || acikDurumlar.Contains(c.Durum) || (c.Yon == CekSenetYon.Verilen && c.Durum != CekSenetDurum.Odendi && c.Durum != CekSenetDurum.Iade)))
            .OrderBy(c => c.VadeTarihi)
            .Select(c => new CekSenetSatir(c.Id, c.Tip, c.Yon, c.PortfoyNo, c.SeriNo, c.Banka, c.Cari!.Unvan, c.Tutar, c.ParaBirimi, c.VadeTarihi, c.Durum))
            .ToListAsync(ct);
    }

    /// <summary>Alınan çek/senet: müşteri carisine alacak (ödeme yerine geçer). Verilen: tedarikçi carisine borç.</summary>
    public async Task<CekSenet> KaydetAsync(CekSenet c, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansYaz);
        if (c.Id != 0) throw new DomainException("Kayıtlı çek/senet düzenlenemez; durum değişikliği veya iade kullanın.");
        if (c.CariId == 0 || c.Tutar <= 0) throw new DomainException("Cari ve tutar zorunludur.");
        if (c.ParaBirimi != ParaBirimi.TRY) throw new DomainException("Şu an yalnızca TL çek/senet desteklenir.");
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        c.PortfoyNo = $"{(c.Yon == CekSenetYon.Alinan ? "A" : "V")}{await db.SiradakiNumaraAsync("PortfoyNoSeq", ct):D6}";
        c.Durum = c.Yon == CekSenetYon.Alinan ? CekSenetDurum.Portfoyde : CekSenetDurum.CiroEdildi;
        c.Hareketler = [new CekSenetHareket { Tarih = c.IslemTarihi, YeniDurum = c.Durum, CariId = c.CariId, Aciklama = "Giriş" }];
        db.CekSenetler.Add(c);
        await db.KaydetAsync(ct);
        var acik = $"{c.Tip.Ad()} {c.PortfoyNo} {c.SeriNo} vade {c.VadeTarihi:dd.MM.yyyy}";
        if (c.Yon == CekSenetYon.Alinan) Defter.Cari(db, c.CariId, c.IslemTarihi, 0, c.Tutar, KaynakTip.CekSenet, c.Id, "Alınan " + acik);
        else Defter.Cari(db, c.CariId, c.IslemTarihi, c.Tutar, 0, KaynakTip.CekSenet, c.Id, "Verilen " + acik, vade: c.VadeTarihi);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return c;
    }

    /// <summary>
    /// Durum geçişleri: Alınan → Tahsil Edildi (kasa/bankaya giriş) | Ciro Edildi (ciro edilen cariye borç) | Karşılıksız/İade (müşteriye tekrar borç).
    /// Verilen → Ödendi (kasa/bankadan çıkış) | İade (tedarikçi borcu geri alınır).
    /// </summary>
    public async Task DurumDegistirAsync(int id, CekSenetDurum yeni, DateOnly tarih, byte? kasaHesapId, int? ciroCariId, string? aciklama, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansYaz);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await db.CekSenetler.SingleAsync(x => x.Id == id, ct);
        var no = await Defter.IslemNoAsync(db, ct);
        var acik = $"{c.Tip.Ad()} {c.PortfoyNo} {yeni.Ad()}";
        switch (c.Yon, c.Durum, yeni)
        {
            case (CekSenetYon.Alinan, CekSenetDurum.Portfoyde or CekSenetDurum.TahsileVerildi, CekSenetDurum.TahsilEdildi):
                if (kasaHesapId is null) throw new DomainException("Tahsil edildiği kasa/banka seçilmelidir.");
                Defter.Kasa(db, kasaHesapId.Value, tarih, c.Tutar, 0, KaynakTip.CekSenet, no, acik, c.CariId);
                break;
            case (CekSenetYon.Alinan, CekSenetDurum.Portfoyde, CekSenetDurum.TahsileVerildi):
                break;
            case (CekSenetYon.Alinan, CekSenetDurum.Portfoyde, CekSenetDurum.CiroEdildi):
                if (ciroCariId is null) throw new DomainException("Ciro edilen cari seçilmelidir.");
                Defter.Cari(db, ciroCariId.Value, tarih, c.Tutar, 0, KaynakTip.CekSenet, no, acik, vade: c.VadeTarihi);
                break;
            case (CekSenetYon.Alinan, CekSenetDurum.Portfoyde or CekSenetDurum.TahsileVerildi, CekSenetDurum.Karsiliksiz or CekSenetDurum.Iade):
                Defter.Cari(db, c.CariId, tarih, c.Tutar, 0, KaynakTip.CekSenet, no, acik);
                break;
            case (CekSenetYon.Verilen, CekSenetDurum.CiroEdildi, CekSenetDurum.Odendi):
                if (kasaHesapId is null) throw new DomainException("Ödendiği banka seçilmelidir.");
                await Defter.KasaCikisKontrolAsync(db, kasaHesapId.Value, c.Tutar, ct);
                Defter.Kasa(db, kasaHesapId.Value, tarih, 0, c.Tutar, KaynakTip.CekSenet, no, acik, c.CariId);
                break;
            case (CekSenetYon.Verilen, CekSenetDurum.CiroEdildi, CekSenetDurum.Iade):
                Defter.Cari(db, c.CariId, tarih, 0, c.Tutar, KaynakTip.CekSenet, no, acik);
                break;
            default:
                throw new DomainException($"{c.Yon.Ad()} {c.Tip.Ad().ToLower()} '{c.Durum.Ad()}' durumundan '{yeni.Ad()}' durumuna geçemez.");
        }
        c.Durum = yeni;
        db.CekSenetHareketleri.Add(new CekSenetHareket { CekSenetId = c.Id, Tarih = tarih, YeniDurum = yeni, CariId = ciroCariId, KasaHesapId = kasaHesapId, Aciklama = aciklama });
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
    }
}
