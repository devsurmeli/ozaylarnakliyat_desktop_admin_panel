using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public enum AcikKalemTuru : byte { Avans = 1, Masraf = 2, Yakit = 3, Harcirah = 4 }

public sealed record AcikKalem(AcikKalemTuru Tur, int Id, DateOnly Tarih, string? SeferNo, string Aciklama, decimal Tutar,
    ParaBirimi ParaBirimi, decimal TutarTL, bool Onayli)
{
    public string TurAd => Tur switch
    {
        AcikKalemTuru.Avans => "Avans",
        AcikKalemTuru.Masraf => "Harcama",
        AcikKalemTuru.Yakit => "Yakıt",
        _ => "Harcırah",
    };
    public string PB => ParaBirimi.Ad();
}

public sealed record SoforBakiyeSatir(short SoforId, string AdSoyad, int CariId, decimal Bakiye, int AcikAvans, int OnayBekleyen)
{
    /// <summary>Pozitif: şoför şirkete borçlu (elinde avans var). Negatif: şirket şoföre borçlu.</summary>
    public string Durum => Bakiye > 0 ? "Şoför borçlu" : Bakiye < 0 ? "Şoföre borçluyuz" : "Kapalı";
}

public sealed record AvansSatir(int Id, DateOnly Tarih, string Sofor, string? Sefer, decimal Tutar, ParaBirimi ParaBirimi, string? Kasa, string? Aciklama, bool Mahsuplu, bool Iptal)
{
    public string PB => ParaBirimi.Ad();
}

public sealed record HarcirahSatir(int Id, string Sofor, string? Sefer, string? Ulke, DateOnly Baslangic, DateOnly Bitis, decimal Gun, decimal Gunluk, decimal Tutar,
    ParaBirimi ParaBirimi, bool Mahsuplu, bool Iptal)
{
    public string PB => ParaBirimi.Ad();
}

public sealed record MahsupOnizleme(decimal AvansToplam, decimal HarcamaToplam, decimal HarcirahToplam)
{
    public decimal Fark => MahsupHesap.Fark(AvansToplam, HarcamaToplam, HarcirahToplam);
}

public sealed class SoforHesapServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    public async Task<List<SoforBakiyeSatir>> BakiyelerAsync(CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.Soforler.AsNoTracking().Where(s => s.Aktif || db.CariHareketleri.Any(h => h.CariId == s.CariId))
            .OrderBy(s => s.Ad).ThenBy(s => s.Soyad)
            .Select(s => new SoforBakiyeSatir(s.Id, s.Ad + " " + s.Soyad, s.CariId,
                db.CariHareketleri.Where(h => h.CariId == s.CariId).Sum(h => (decimal?)(h.Borc - h.Alacak)) ?? 0,
                db.Avanslar.Count(a => a.SoforId == s.Id && a.MahsupId == null && !a.Iptal),
                db.Masraflar.Count(m => m.SoforId == s.Id && m.OdemeSekli == OdemeSekli.SoforNakit && !m.Onayli && !m.Iptal)))
            .ToListAsync(ct);
    }

    // ------------------------------- Avans -------------------------------

    public async Task<Avans> AvansVerAsync(Avans a, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapYaz);
        if (a.Tutar <= 0) throw new DomainException("Avans tutarı sıfırdan büyük olmalıdır.");
        await using var db = factory.Olustur();
        var sofor = await SoforAsync(db, a.SoforId, ct);
        var kasa = await db.KasaHesaplari.AsNoTracking().SingleOrDefaultAsync(k => k.Id == a.KasaHesapId, ct) ?? throw new DomainException("Kasa/banka seçilmelidir.");
        a.ParaBirimi = kasa.ParaBirimi;
        if (a.ParaBirimi == ParaBirimi.TRY) a.Kur = 1;
        else if (a.Kur <= 0) throw new DomainException("Dövizli avans için kur girilmelidir.");
        await Defter.KasaCikisKontrolAsync(db, a.KasaHesapId, a.Tutar, ct);
        var seferNo = await SeferNoAsync(db, a.SeferId, ct);
        a.AracId ??= await AracBulAsync(db, a.SoforId, a.SeferId, a.Tarih, ct);
        var plaka = await PlakaAsync(db, a.AracId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        a.Id = 0;
        a.MahsupId = null;
        a.Iptal = false;
        db.Avanslar.Add(a);
        await db.KaydetAsync(ct);
        var kanal = kasa.HesapTipi == KasaHesapTipi.Kasa ? "elden" : "EFT";
        var acik = $"{a.Tur.Ad()} ({kanal}) - {sofor.Ad} {sofor.Soyad}{(plaka is null ? "" : " / " + plaka)}{(seferNo is null ? "" : " / " + seferNo)}"
                   + (string.IsNullOrWhiteSpace(a.Aciklama) ? "" : " - " + a.Aciklama);
        Defter.Kasa(db, a.KasaHesapId, a.Tarih, 0, a.Tutar, KaynakTip.Avans, a.Id, acik, sofor.CariId, a.Kur);
        Defter.Cari(db, sofor.CariId, a.Tarih, a.Tutar, 0, KaynakTip.Avans, a.Id, acik, a.ParaBirimi, a.Kur, aracId: a.AracId, seferId: a.SeferId);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return a;
    }

    public async Task AvansIptalAsync(int avansId, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        var a = await db.Avanslar.SingleOrDefaultAsync(x => x.Id == avansId, ct) ?? throw new DomainException("Avans bulunamadı.");
        if (a.MahsupId is not null) throw new DomainException("Mahsubu yapılmış avans iptal edilemez; önce mahsubu iptal edin.");
        if (a.Iptal) return;
        a.Iptal = true;
        await Defter.TersKayitAsync(db, KaynakTip.Avans, a.Id, saat.Bugun, neden, ct);
        await db.KaydetAsync(ct);
    }

    // ------------------------------- Harcırah -------------------------------

    /// <summary>Tarih ve ülkeye göre geçerli günlük harcırah tarifesini bulur (yurtdışında ülke tarifesi yoksa yurtiçi değil null döner).</summary>
    public async Task<HarcirahTarife?> TarifeBulAsync(short? ulkeId, DateOnly tarih, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var turkiye = await db.Ulkeler.Where(u => u.Kod == "TR").Select(u => (short?)u.Id).FirstOrDefaultAsync(ct);
        var hedef = ulkeId == turkiye ? null : ulkeId;
        return await db.HarcirahTarifeleri.AsNoTracking()
            .Where(t => t.UlkeId == hedef && t.GecerlilikBaslangic <= tarih)
            .OrderByDescending(t => t.GecerlilikBaslangic)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Harcirah> HarcirahKaydetAsync(Harcirah h, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapYaz);
        var gun = HarcirahHesap.GunSayisi(h.Baslangic, h.Bitis);
        if (h.GunSayisi <= 0 || h.GunSayisi > gun) h.GunSayisi = gun;
        if (h.GunlukTutar <= 0)
        {
            var tarife = await TarifeBulAsync(h.UlkeId, h.Baslangic, ct)
                         ?? throw new DomainException("Bu ülke/tarih için harcırah tarifesi tanımlı değil; günlük tutarı elle girin.");
            h.GunlukTutar = tarife.GunlukTutar;
            h.ParaBirimi = tarife.ParaBirimi;
        }
        if (h.GunlukTutar <= 0) throw new DomainException("Günlük harcırah tutarı sıfırdan büyük olmalıdır.");
        if (h.ParaBirimi == ParaBirimi.TRY) h.Kur = 1;
        else if (h.Kur <= 0) throw new DomainException("Dövizli harcırah için kur girilmelidir.");
        h.Tutar = HarcirahHesap.Tutar(h.GunSayisi, h.GunlukTutar);

        await using var db = factory.Olustur();
        var sofor = await SoforAsync(db, h.SoforId, ct);
        var seferNo = await SeferNoAsync(db, h.SeferId, ct);
        if (h.SeferId is not null && await db.Harcirahlar.AnyAsync(x => x.SeferId == h.SeferId && x.SoforId == h.SoforId && !x.Iptal && x.Id != h.Id, ct))
            throw new DomainException("Bu şoför için bu sefere zaten harcırah yazılmış.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (h.Id != 0)
        {
            var eski = await db.Harcirahlar.SingleAsync(x => x.Id == h.Id, ct);
            if (eski.MahsupId is not null) throw new DomainException("Mahsubu yapılmış harcırah değiştirilemez.");
            await Defter.TersKayitAsync(db, KaynakTip.Harcirah, h.Id, saat.Bugun, "harcırah düzeltildi", ct);
            await db.BirlestirAsync(h, ct);
        }
        else
        {
            db.Harcirahlar.Add(h);
        }
        h.AracId ??= await AracBulAsync(db, h.SoforId, h.SeferId, h.Baslangic, ct);
        await db.KaydetAsync(ct);
        Defter.Cari(db, sofor.CariId, h.Bitis, 0, h.Tutar, KaynakTip.Harcirah, h.Id,
            $"Harcırah {h.GunSayisi:0.#} gün × {h.GunlukTutar:N2}{(seferNo is null ? "" : " / " + seferNo)}", h.ParaBirimi, h.Kur, aracId: h.AracId, seferId: h.SeferId);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return h;
    }

    public async Task HarcirahIptalAsync(int id, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        var h = await db.Harcirahlar.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Harcırah bulunamadı.");
        if (h.MahsupId is not null) throw new DomainException("Mahsubu yapılmış harcırah iptal edilemez.");
        if (h.Iptal) return;
        h.Iptal = true;
        await Defter.TersKayitAsync(db, KaynakTip.Harcirah, h.Id, saat.Bugun, neden, ct);
        await db.KaydetAsync(ct);
    }

    /// <summary>Sefer tarihlerinden harcırah önerisi: çıkış → dönüş (dahil) gün sayısı, varış ülkesinin tarifesi.</summary>
    public async Task<Harcirah?> SeferdenHarcirahOnerAsync(int seferId, short soforId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var s = await db.Seferler.AsNoTracking().Where(x => x.Id == seferId)
            .Select(x => new { x.CikisTarihi, x.DonusTarihi, x.TeslimTarihi, x.PlanlananTarih, x.Kapsam, VarisUlke = x.VarisLokasyon!.UlkeId })
            .SingleOrDefaultAsync(ct);
        if (s is null) return null;
        var bas = s.CikisTarihi is not null ? DateOnly.FromDateTime(s.CikisTarihi.Value) : s.PlanlananTarih;
        var bitZ = s.DonusTarihi ?? s.TeslimTarihi;
        var bit = bitZ is not null ? DateOnly.FromDateTime(bitZ.Value) : bas;
        if (bit < bas) bit = bas;
        short? ulke = s.Kapsam == SeferKapsam.Yurtdisi ? s.VarisUlke : null;
        var tarife = await TarifeBulAsync(ulke, bas, ct);
        return new Harcirah
        {
            SoforId = soforId, SeferId = seferId, UlkeId = ulke, Baslangic = bas, Bitis = bit,
            GunSayisi = HarcirahHesap.GunSayisi(bas, bit),
            GunlukTutar = tarife?.GunlukTutar ?? 0, ParaBirimi = tarife?.ParaBirimi ?? ParaBirimi.TRY, Kur = 1,
        };
    }

    // ------------------------------- Mahsup -------------------------------

    /// <summary>Şoförün mahsuba girmemiş avans, şoför-nakit harcama/yakıt ve harcırahları.</summary>
    public async Task<List<AcikKalem>> AcikKalemlerAsync(short soforId, int? seferId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        var liste = new List<AcikKalem>();
        liste.AddRange((await db.Avanslar.AsNoTracking()
                .Where(a => a.SoforId == soforId && a.MahsupId == null && !a.Iptal && a.Tur != AvansTuru.MaasOdemesi && (seferId == null || a.SeferId == seferId))
                .Select(a => new { a.Id, a.Tarih, SeferNo = a.Sefer != null ? a.Sefer.SeferNo : null, a.Aciklama, a.Tutar, a.ParaBirimi, a.Kur })
                .ToListAsync(ct))
            .Select(a => new AcikKalem(AcikKalemTuru.Avans, a.Id, a.Tarih, a.SeferNo, a.Aciklama ?? "Avans", a.Tutar, a.ParaBirimi, Para.TL(a.Tutar, a.Kur), true)));
        liste.AddRange((await db.Masraflar.AsNoTracking()
                .Where(m => m.SoforId == soforId && m.OdemeSekli == OdemeSekli.SoforNakit && m.MahsupId == null && !m.Iptal && (seferId == null || m.SeferId == seferId))
                .Select(m => new { m.Id, m.Tarih, SeferNo = m.Sefer != null ? m.Sefer.SeferNo : null, Tur = m.MasrafTuru!.Ad, m.Aciklama, m.Tutar, m.ParaBirimi, m.Kur, m.Onayli })
                .ToListAsync(ct))
            .Select(m => new AcikKalem(AcikKalemTuru.Masraf, m.Id, m.Tarih, m.SeferNo, m.Aciklama is null ? m.Tur : $"{m.Tur} - {m.Aciklama}",
                m.Tutar, m.ParaBirimi, Para.TL(m.Tutar, m.Kur), m.Onayli)));
        liste.AddRange((await db.Yakitlar.AsNoTracking()
                .Where(y => y.SoforId == soforId && y.OdemeSekli == OdemeSekli.SoforNakit && y.MahsupId == null && !y.Iptal && (seferId == null || y.SeferId == seferId))
                .Select(y => new { y.Id, y.AlimZamani, SeferNo = y.Sefer != null ? y.Sefer.SeferNo : null, y.Litre, y.IstasyonAdi, y.Tutar, y.ParaBirimi, y.Kur })
                .ToListAsync(ct))
            .Select(y => new AcikKalem(AcikKalemTuru.Yakit, y.Id, DateOnly.FromDateTime(y.AlimZamani), y.SeferNo,
                $"Yakıt {y.Litre:N2} L {y.IstasyonAdi}", y.Tutar, y.ParaBirimi, Para.TL(y.Tutar, y.Kur), true)));
        liste.AddRange((await db.Harcirahlar.AsNoTracking()
                .Where(h => h.SoforId == soforId && h.MahsupId == null && !h.Iptal && (seferId == null || h.SeferId == seferId))
                .Select(h => new { h.Id, h.Bitis, SeferNo = h.Sefer != null ? h.Sefer.SeferNo : null, h.GunSayisi, h.GunlukTutar, h.Tutar, h.ParaBirimi, h.Kur })
                .ToListAsync(ct))
            .Select(h => new AcikKalem(AcikKalemTuru.Harcirah, h.Id, h.Bitis, h.SeferNo, $"Harcırah {h.GunSayisi:0.#} gün × {h.GunlukTutar:N2}",
                h.Tutar, h.ParaBirimi, Para.TL(h.Tutar, h.Kur), true)));
        return liste.OrderBy(k => k.Tarih).ThenBy(k => k.Tur).ToList();
    }

    public async Task HarcamaOnaylaAsync(IEnumerable<int> masrafIdleri, bool onay, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapOnay);
        await using var db = factory.Olustur();
        var ids = masrafIdleri.ToList();
        foreach (var m in await db.Masraflar.Where(m => ids.Contains(m.Id) && m.MahsupId == null).ToListAsync(ct))
            m.Onayli = onay;
        await db.KaydetAsync(ct);
    }

    public static MahsupOnizleme Onizle(IEnumerable<AcikKalem> secili)
    {
        var l = secili.ToList();
        return new MahsupOnizleme(
            l.Where(k => k.Tur == AcikKalemTuru.Avans).Sum(k => k.TutarTL),
            l.Where(k => k.Tur is AcikKalemTuru.Masraf or AcikKalemTuru.Yakit).Sum(k => k.TutarTL),
            l.Where(k => k.Tur == AcikKalemTuru.Harcirah).Sum(k => k.TutarTL));
    }

    /// <summary>
    /// Seçili kalemleri kapatır. Kalemlerin defter etkisi zaten işlenmiştir; mahsup yalnızca farkı kasaya bağlar:
    /// fark &gt; 0 ve iade alındıysa kasaya giriş + şoför carisine alacak; fark &lt; 0 ve ödendiyse kasadan çıkış + şoför carisine borç.
    /// "Devredildi" seçilirse fark şoför bakiyesinde kalır.
    /// </summary>
    public async Task<Mahsup> MahsupYapAsync(short soforId, int? seferId, DateOnly tarih, IReadOnlyList<(AcikKalemTuru Tur, int Id)> kalemler,
        MahsupSonuc sonuc, byte? kasaHesapId, string? aciklama, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapOnay);
        if (kalemler.Count == 0) throw new DomainException("Mahsup için en az bir kalem seçilmelidir.");
        var acik = await AcikKalemlerAsync(soforId, null, ct);
        var secili = kalemler.Select(k => acik.SingleOrDefault(a => a.Tur == k.Tur && a.Id == k.Id)
                                          ?? throw new DomainException("Seçilen kalemlerden biri artık açık değil; listeyi yenileyin.")).ToList();
        if (secili.Any(k => !k.Onayli)) throw new DomainException("Onaylanmamış harcama mahsuba alınamaz.");
        var onizleme = Onizle(secili);
        var fark = onizleme.Fark;

        if (fark == 0) sonuc = MahsupSonuc.FarkYok;
        else if (sonuc == MahsupSonuc.FarkYok) throw new DomainException("Fark var; iade/ödeme/devir seçeneklerinden biri seçilmelidir.");
        if (sonuc == MahsupSonuc.IadeAlindi && fark < 0) throw new DomainException("Şoföre borçlu olduğumuz durumda iade alınamaz; 'Şoföre Ödendi' seçin.");
        if (sonuc == MahsupSonuc.SoforeOdendi && fark > 0) throw new DomainException("Şoför borçlu; 'Şoförden İade Alındı' seçin.");
        if (sonuc is MahsupSonuc.IadeAlindi or MahsupSonuc.SoforeOdendi && kasaHesapId is null)
            throw new DomainException("İade/ödeme için kasa seçilmelidir.");

        await using var db = factory.Olustur();
        var sofor = await SoforAsync(db, soforId, ct);
        if (kasaHesapId is not null && sonuc is MahsupSonuc.IadeAlindi or MahsupSonuc.SoforeOdendi
            && await db.KasaHesaplari.AnyAsync(k => k.Id == kasaHesapId && k.ParaBirimi != ParaBirimi.TRY, ct))
            throw new DomainException("Mahsup farkı TL'dir; TL kasa/banka seçilmelidir.");
        if (sonuc == MahsupSonuc.SoforeOdendi) await Defter.KasaCikisKontrolAsync(db, kasaHesapId!.Value, -fark, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var m = new Mahsup
        {
            SoforId = soforId, SeferId = seferId, Tarih = tarih, AvansToplam = onizleme.AvansToplam,
            HarcamaToplam = onizleme.HarcamaToplam, HarcirahToplam = onizleme.HarcirahToplam, Fark = fark,
            Sonuc = sonuc, KasaHesapId = sonuc is MahsupSonuc.IadeAlindi or MahsupSonuc.SoforeOdendi ? kasaHesapId : null,
            Aciklama = aciklama,
        };
        db.Mahsuplar.Add(m);
        await db.KaydetAsync(ct);

        int[] Ids(AcikKalemTuru t) => kalemler.Where(k => k.Tur == t).Select(k => k.Id).ToArray();
        var avans = Ids(AcikKalemTuru.Avans);
        var masraf = Ids(AcikKalemTuru.Masraf);
        var yakit = Ids(AcikKalemTuru.Yakit);
        var harc = Ids(AcikKalemTuru.Harcirah);
        foreach (var x in await db.Avanslar.Where(a => avans.Contains(a.Id)).ToListAsync(ct)) x.MahsupId = m.Id;
        foreach (var x in await db.Masraflar.Where(a => masraf.Contains(a.Id)).ToListAsync(ct)) x.MahsupId = m.Id;
        foreach (var x in await db.Yakitlar.Where(a => yakit.Contains(a.Id)).ToListAsync(ct)) x.MahsupId = m.Id;
        foreach (var x in await db.Harcirahlar.Where(a => harc.Contains(a.Id)).ToListAsync(ct)) x.MahsupId = m.Id;

        var metin = $"Mahsup farkı - {sofor.Ad} {sofor.Soyad}";
        var mahsupArac = await AracBulAsync(db, soforId, seferId, tarih, ct);
        if (sonuc == MahsupSonuc.IadeAlindi)
        {
            Defter.Kasa(db, kasaHesapId!.Value, tarih, fark, 0, KaynakTip.Mahsup, m.Id, metin, sofor.CariId);
            Defter.Cari(db, sofor.CariId, tarih, 0, fark, KaynakTip.Mahsup, m.Id, metin, aracId: mahsupArac, seferId: seferId);
        }
        else if (sonuc == MahsupSonuc.SoforeOdendi)
        {
            Defter.Kasa(db, kasaHesapId!.Value, tarih, 0, -fark, KaynakTip.Mahsup, m.Id, metin, sofor.CariId);
            Defter.Cari(db, sofor.CariId, tarih, -fark, 0, KaynakTip.Mahsup, m.Id, metin, aracId: mahsupArac, seferId: seferId);
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return m;
    }

    public async Task MahsupIptalAsync(int mahsupId, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        _ = await db.Mahsuplar.SingleOrDefaultAsync(x => x.Id == mahsupId, ct) ?? throw new DomainException("Mahsup bulunamadı.");
        await db.Avanslar.Where(a => a.MahsupId == mahsupId).ExecuteUpdateAsync(u => u.SetProperty(a => a.MahsupId, (int?)null), ct);
        await db.Masraflar.Where(a => a.MahsupId == mahsupId).ExecuteUpdateAsync(u => u.SetProperty(a => a.MahsupId, (int?)null), ct);
        await db.Yakitlar.Where(a => a.MahsupId == mahsupId).ExecuteUpdateAsync(u => u.SetProperty(a => a.MahsupId, (int?)null), ct);
        await db.Harcirahlar.Where(a => a.MahsupId == mahsupId).ExecuteUpdateAsync(u => u.SetProperty(a => a.MahsupId, (int?)null), ct);
        await Defter.TersKayitAsync(db, KaynakTip.Mahsup, mahsupId, saat.Bugun, neden, ct);
        var m = await db.Mahsuplar.SingleAsync(x => x.Id == mahsupId, ct);
        db.Mahsuplar.Remove(m);
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<List<Mahsup>> MahsuplarAsync(short? soforId, DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.Mahsuplar.AsNoTracking().Include(m => m.Sofor).Include(m => m.Sefer)
            .Where(m => (soforId == null || m.SoforId == soforId) && m.Tarih >= bas && m.Tarih <= bit)
            .OrderByDescending(m => m.Tarih).ToListAsync(ct);
    }

    public async Task<List<AvansSatir>> AvanslarAsync(DateOnly bas, DateOnly bit, short? soforId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.Avanslar.AsNoTracking().Where(a => a.Tarih >= bas && a.Tarih <= bit && (soforId == null || a.SoforId == soforId)).OrderByDescending(a => a.Tarih).ThenByDescending(a => a.Id)
            .Select(a => new AvansSatir(a.Id, a.Tarih, a.Sofor!.Ad + " " + a.Sofor.Soyad, a.Sefer != null ? a.Sefer.SeferNo : null, a.Tutar, a.ParaBirimi,
                db.KasaHesaplari.Where(k => k.Id == a.KasaHesapId).Select(k => k.Ad).FirstOrDefault(), a.Aciklama, a.MahsupId != null, a.Iptal))
            .ToListAsync(ct);
    }

    public async Task<List<HarcirahSatir>> HarcirahlarAsync(DateOnly bas, DateOnly bit, short? soforId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.Harcirahlar.AsNoTracking().Where(h => h.Bitis >= bas && h.Baslangic <= bit && (soforId == null || h.SoforId == soforId)).OrderByDescending(h => h.Bitis)
            .Select(h => new HarcirahSatir(h.Id, h.Sofor!.Ad + " " + h.Sofor.Soyad, h.Sefer != null ? h.Sefer.SeferNo : null,
                h.UlkeId != null ? db.Ulkeler.Where(u => u.Id == h.UlkeId).Select(u => u.Ad).FirstOrDefault() : "Yurtiçi",
                h.Baslangic, h.Bitis, h.GunSayisi, h.GunlukTutar, h.Tutar, h.ParaBirimi, h.MahsupId != null, h.Iptal))
            .ToListAsync(ct);
    }

    private static async Task<Sofor> SoforAsync(IFiloDbContext db, short soforId, CancellationToken ct) =>
        await db.Soforler.AsNoTracking().SingleOrDefaultAsync(s => s.Id == soforId, ct) ?? throw new DomainException("Şoför seçilmelidir.");

    private static async Task<string?> SeferNoAsync(IFiloDbContext db, int? seferId, CancellationToken ct) =>
        seferId is null ? null : await db.Seferler.Where(s => s.Id == seferId).Select(s => s.SeferNo).SingleOrDefaultAsync(ct);

    private static async Task<string?> PlakaAsync(IFiloDbContext db, short? aracId, CancellationToken ct) =>
        aracId is null ? null : await db.Araclar.Where(a => a.Id == aracId).Select(a => a.Plaka).SingleOrDefaultAsync(ct);

    /// <summary>Hareketin aracı: seferin aracı → tarihteki şoför-araç ataması → şoförün varsayılan aracı.</summary>
    internal static async Task<short?> AracBulAsync(IFiloDbContext db, short soforId, int? seferId, DateOnly tarih, CancellationToken ct)
    {
        if (seferId is not null)
        {
            var a = await db.Seferler.Where(s => s.Id == seferId).Select(s => s.AracId).SingleOrDefaultAsync(ct);
            if (a is not null) return a;
        }
        var atama = await db.SoforAracAtamalari.Where(x => x.SoforId == soforId && x.Baslangic <= tarih && (x.Bitis == null || x.Bitis >= tarih))
            .OrderByDescending(x => x.Baslangic).Select(x => (short?)x.AracId).FirstOrDefaultAsync(ct);
        return atama ?? await db.Soforler.Where(s => s.Id == soforId).Select(s => s.VarsayilanAracId).SingleOrDefaultAsync(ct);
    }

    // ------------------------------- Para Al (iade / müşteri tahsilatı) -------------------------------

    public enum ParaAlTuru : byte { Iade = 1, MusteriTahsilati = 2 }

    /// <summary>
    /// Şoförden para alma. İade: kasaya giriş + şoför carisine alacak. Müşteri tahsilatı: şoför müşteriden nakit/navlun aldı →
    /// müşteri carisine alacak, şoför carisine borç (para şoförde); kasa seçilirse şoför parayı hemen teslim etmiş sayılır.
    /// </summary>
    public async Task<int> ParaAlAsync(short soforId, ParaAlTuru tur, DateOnly tarih, decimal tutar, byte? kasaHesapId, int? musteriCariId,
        int? seferId, short? aracId, string? aciklama, ParaBirimi pb = ParaBirimi.TRY, decimal kur = 1m, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapYaz);
        if (tutar <= 0) throw new DomainException("Tutar sıfırdan büyük olmalıdır.");
        if (tur == ParaAlTuru.Iade && kasaHesapId is null) throw new DomainException("İadenin girdiği kasa/banka seçilmelidir.");
        if (tur == ParaAlTuru.MusteriTahsilati && musteriCariId is null) throw new DomainException("Şoförün tahsilat yaptığı müşteri seçilmelidir.");
        if (pb == ParaBirimi.TRY) kur = 1;
        await using var db = factory.Olustur();
        var sofor = await SoforAsync(db, soforId, ct);
        aracId ??= await AracBulAsync(db, soforId, seferId, tarih, ct);
        if (kasaHesapId is { } kid && await db.KasaHesaplari.Where(k => k.Id == kid).Select(k => k.ParaBirimi).SingleAsync(ct) != pb)
            throw new DomainException("Kasa/banka para birimi tutarın para birimiyle aynı olmalıdır.");
        var seferNo = await SeferNoAsync(db, seferId, ct);
        var plaka = await PlakaAsync(db, aracId, ct);
        var ek = $"{(plaka is null ? "" : " / " + plaka)}{(seferNo is null ? "" : " / " + seferNo)}{(string.IsNullOrWhiteSpace(aciklama) ? "" : " - " + aciklama)}";

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var no = await Defter.IslemNoAsync(db, ct);
        if (tur == ParaAlTuru.MusteriTahsilati)
        {
            var musteri = await db.Cariler.Where(c => c.Id == musteriCariId).Select(c => c.Unvan).SingleAsync(ct);
            Defter.Cari(db, musteriCariId!.Value, tarih, 0, tutar, KaynakTip.SoforTahsilat, no, $"Şoför tahsilatı ({sofor.Ad} {sofor.Soyad}){ek}", pb, kur, aracId: aracId, seferId: seferId);
            Defter.Cari(db, sofor.CariId, tarih, tutar, 0, KaynakTip.SoforTahsilat, no, $"Müşteriden tahsil edilen: {musteri}{ek}", pb, kur, aracId: aracId, seferId: seferId);
        }
        if (kasaHesapId is { } kasa)
        {
            var metin = tur == ParaAlTuru.Iade ? $"Şoförden iade - {sofor.Ad} {sofor.Soyad}{ek}" : $"Şoför tahsilatı teslim - {sofor.Ad} {sofor.Soyad}{ek}";
            Defter.Kasa(db, kasa, tarih, tutar, 0, KaynakTip.SoforTahsilat, no, metin, sofor.CariId, kur);
            Defter.Cari(db, sofor.CariId, tarih, 0, tutar, KaynakTip.SoforTahsilat, no, metin, pb, kur, aracId: aracId, seferId: seferId);
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return no;
    }

    public async Task ParaAlIptalAsync(int islemNo, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        await Defter.TersKayitAsync(db, KaynakTip.SoforTahsilat, islemNo, saat.Bugun, neden, ct);
        await db.KaydetAsync(ct);
    }

    // ------------------------------- Günlük defter -------------------------------

    /// <summary>
    /// Şoför hareket defteri: verilen (şoför borçlanır: avans, harçlık, EFT, maaş ödemesi, rücu) / alınan (harcama, harcırah, iade,
    /// hakediş). Tarih aralığı, şoför ve araç filtresi. Şoför seçiliyse yürüyen bakiye hesaplanır.
    /// </summary>
    public async Task<List<SoforDefterSatir>> DefterAsync(DateOnly bas, DateOnly bit, short? soforId = null, short? aracId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        var soforler = await db.Soforler.AsNoTracking().IgnoreQueryFilters().Where(s => soforId == null || s.Id == soforId)
            .Select(s => new { s.Id, s.CariId, Ad = s.Ad + " " + s.Soyad }).ToListAsync(ct);
        var cariler = soforler.Select(s => s.CariId).ToList();
        var cariSofor = soforler.ToDictionary(s => s.CariId);
        decimal devir = 0;
        if (soforId is not null)
            devir = await db.CariHareketleri.Where(h => cariler.Contains(h.CariId) && h.Tarih < bas && (aracId == null || h.AracId == aracId)).SumAsync(h => h.Borc - h.Alacak, ct);
        var ham = await db.CariHareketleri.AsNoTracking()
            .Where(h => cariler.Contains(h.CariId) && h.Tarih >= bas && h.Tarih <= bit && (aracId == null || h.AracId == aracId))
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id)
            .Select(h => new
            {
                h.Id, h.CariId, h.Tarih, h.KaynakTip, h.KaynakId, h.Aciklama, h.Borc, h.Alacak, h.ParaBirimi, h.DovizBorc, h.DovizAlacak,
                Plaka = h.AracId != null ? db.Araclar.IgnoreQueryFilters().Where(a => a.Id == h.AracId).Select(a => a.Plaka).FirstOrDefault() : null,
                SeferNo = h.SeferId != null ? db.Seferler.IgnoreQueryFilters().Where(s => s.Id == h.SeferId).Select(s => s.SeferNo).FirstOrDefault() : null,
                Iptal = h.TersKayitId != null || db.CariHareketleri.Any(t => t.TersKayitId == h.Id),
            }).ToListAsync(ct);
        var bakiye = devir;
        return ham.Select(h =>
        {
            bakiye += h.Borc - h.Alacak;
            var doviz = h.ParaBirimi == ParaBirimi.TRY ? null : $"{h.DovizBorc ?? h.DovizAlacak:N2} {h.ParaBirimi.Ad()}";
            return new SoforDefterSatir(h.Id, h.Tarih, cariSofor[h.CariId].Id, cariSofor[h.CariId].Ad, h.Plaka, h.SeferNo, h.KaynakTip, h.KaynakId,
                h.Aciklama, h.Borc, h.Alacak, soforId is null ? null : bakiye, doviz, h.Iptal);
        }).ToList();
    }

    // ------------------------------- Şoför-araç atama -------------------------------

    public async Task<List<AtamaSatir>> AtamalarAsync(short? soforId, short? aracId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FiloGor);
        await using var db = factory.Olustur();
        return await db.SoforAracAtamalari.AsNoTracking().Where(a => (soforId == null || a.SoforId == soforId) && (aracId == null || a.AracId == aracId))
            .OrderByDescending(a => a.Baslangic)
            .Select(a => new AtamaSatir(a.Id, a.Sofor!.Ad + " " + a.Sofor.Soyad, a.Arac!.Plaka, a.Baslangic, a.Bitis)).ToListAsync(ct);
    }

    /// <summary>Şoförü araca atar: şoförün açık ataması ve aracın başka şoförle açık ataması kapanır; varsayılan araç güncellenir.</summary>
    public async Task AracAtaAsync(short soforId, short aracId, DateOnly tarih, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FiloYaz);
        await using var db = factory.Olustur();
        var acik = await db.SoforAracAtamalari.Where(a => a.Bitis == null && (a.SoforId == soforId || a.AracId == aracId)).ToListAsync(ct);
        foreach (var a in acik)
        {
            if (a.SoforId == soforId && a.AracId == aracId) return;
            a.Bitis = tarih.AddDays(-1) < a.Baslangic ? a.Baslangic : tarih.AddDays(-1);
        }
        db.SoforAracAtamalari.Add(new SoforAracAtama { SoforId = soforId, AracId = aracId, Baslangic = tarih });
        var s = await db.Soforler.SingleAsync(x => x.Id == soforId, ct);
        s.VarsayilanAracId = aracId;
        await db.KaydetAsync(ct);
    }

    // ------------------------------- Hakediş -------------------------------

    /// <summary>
    /// Dönem hakediş önerisi: maaş + prim (birincil şoför olduğu, dönemde teslim edilen seferler; sefer başı veya km başı).
    /// Dönem için zaten yazılmış tahakkuklar tekrar önerilmez.
    /// </summary>
    public async Task<List<HakedisOneri>> HakedisOnerAsync(int donem, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        var bas = new DateOnly(donem / 100, donem % 100, 1);
        var bit = bas.AddMonths(1).AddDays(-1);
        var b0 = bas.ToDateTime(TimeOnly.MinValue);
        var b1 = bit.ToDateTime(TimeOnly.MaxValue);
        await using var db = factory.Olustur();
        var soforler = await db.Soforler.AsNoTracking().Where(s => s.Aktif && (s.MaasTutari > 0 || s.PrimTipi != PrimTipi.Yok))
            .Select(s => new { s.Id, Ad = s.Ad + " " + s.Soyad, s.MaasTutari, s.PrimTipi, s.PrimTutari, s.VarsayilanAracId }).ToListAsync(ct);
        var mevcut = await db.PersonelTahakkuklari.AsNoTracking().Where(t => t.Donem == donem && !t.Iptal)
            .Select(t => new { t.SoforId, t.Tur, t.SeferId }).ToListAsync(ct);
        var seferler = await db.SeferSoforleri.AsNoTracking()
            .Where(x => x.Birincil && x.Sefer!.Durum != SeferDurum.Iptal && x.Sefer.TeslimTarihi >= b0 && x.Sefer.TeslimTarihi <= b1)
            .Select(x => new { x.SoforId, x.SeferId, x.Sefer!.SeferNo, x.Sefer.AracId, Km = x.Sefer.DonusKm - x.Sefer.CikisKm }).ToListAsync(ct);

        var sonuc = new List<HakedisOneri>();
        foreach (var s in soforler)
        {
            if (s.MaasTutari is > 0 && !mevcut.Any(m => m.SoforId == s.Id && m.Tur == TahakkukTuru.Maas))
                sonuc.Add(new HakedisOneri(s.Id, s.Ad, TahakkukTuru.Maas, s.MaasTutari.Value, null, null, s.VarsayilanAracId, $"{bas:MMMM yyyy} maaşı"));
            if (s.PrimTipi == PrimTipi.Yok || s.PrimTutari is not > 0) continue;
            foreach (var sf in seferler.Where(x => x.SoforId == s.Id && !mevcut.Any(m => m.SoforId == s.Id && m.SeferId == x.SeferId)))
            {
                var tutar = s.PrimTipi == PrimTipi.SeferBasi ? s.PrimTutari.Value : Para.Yuvarla((sf.Km ?? 0) * s.PrimTutari.Value);
                if (tutar <= 0) continue;
                sonuc.Add(new HakedisOneri(s.Id, s.Ad, s.PrimTipi == PrimTipi.SeferBasi ? TahakkukTuru.SeferPrimi : TahakkukTuru.KmPrimi, tutar,
                    sf.SeferId, sf.SeferNo, sf.AracId, s.PrimTipi == PrimTipi.SeferBasi ? $"Sefer primi {sf.SeferNo}" : $"Km primi {sf.SeferNo} ({sf.Km:N0} km)"));
            }
        }
        return sonuc;
    }

    /// <summary>Tahakkuk satırlarını yazar: maaş/prim/ek → şoför carisine alacak; kesinti → borç.</summary>
    public async Task TahakkukYazAsync(int donem, IReadOnlyList<PersonelTahakkuk> satirlar, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapOnay);
        if (satirlar.Count == 0) return;
        var tarih = new DateOnly(donem / 100, donem % 100, 1).AddMonths(1).AddDays(-1);
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var t in satirlar)
        {
            if (t.Tutar <= 0) throw new DomainException("Tahakkuk tutarı sıfırdan büyük olmalıdır.");
            var sofor = await SoforAsync(db, t.SoforId, ct);
            t.Id = 0;
            t.Donem = donem;
            if (t.Tarih == default) t.Tarih = tarih;
            db.PersonelTahakkuklari.Add(t);
            await db.KaydetAsync(ct);
            var metin = $"{t.Tur.Ad()} {donem % 100:D2}/{donem / 100}{(string.IsNullOrWhiteSpace(t.Aciklama) ? "" : " - " + t.Aciklama)}";
            Defter.Cari(db, sofor.CariId, t.Tarih, t.Kesinti ? t.Tutar : 0, t.Kesinti ? 0 : t.Tutar, KaynakTip.Hakedis, t.Id, metin, aracId: t.AracId, seferId: t.SeferId);
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task TahakkukIptalAsync(int id, string neden, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansIptal);
        await using var db = factory.Olustur();
        var t = await db.PersonelTahakkuklari.SingleAsync(x => x.Id == id, ct);
        if (t.Iptal) return;
        t.Iptal = true;
        await Defter.TersKayitAsync(db, KaynakTip.Hakedis, t.Id, saat.Bugun, neden, ct);
        await db.KaydetAsync(ct);
    }

    public async Task<List<TahakkukSatir>> TahakkuklarAsync(int? donem, short? soforId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.PersonelTahakkuklari.AsNoTracking().Where(t => (donem == null || t.Donem == donem) && (soforId == null || t.SoforId == soforId))
            .OrderByDescending(t => t.Donem).ThenBy(t => t.Sofor!.Ad)
            .Select(t => new TahakkukSatir(t.Id, t.Donem, t.Sofor!.Ad + " " + t.Sofor.Soyad, t.Tur, t.Tutar, t.Sefer != null ? t.Sefer.SeferNo : null,
                t.Arac != null ? t.Arac.Plaka : null, t.Aciklama, t.Iptal)).ToListAsync(ct);
    }

    // ------------------------------- Zimmet -------------------------------

    public async Task<List<ZimmetSatir>> ZimmetlerAsync(short? soforId, short? aracId, bool iadeDahil, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.Zimmetler.AsNoTracking()
            .Where(z => (soforId == null || z.SoforId == soforId) && (aracId == null || z.AracId == aracId) && (iadeDahil || z.Durum == ZimmetDurum.Zimmette))
            .OrderBy(z => z.Durum).ThenByDescending(z => z.VerilisTarihi)
            .Select(z => new ZimmetSatir(z.Id, z.Tur, z.Ad, z.SeriNo, z.Sofor != null ? z.Sofor.Ad + " " + z.Sofor.Soyad : null, z.Arac != null ? z.Arac.Plaka : null,
                z.VerilisTarihi, z.IadeTarihi, z.Durum, z.Deger, z.Aciklama)).ToListAsync(ct);
    }

    /// <summary>Zimmeti kapatır (iade / kayıp).</summary>
    public async Task ZimmetKapatAsync(int id, DateOnly tarih, ZimmetDurum durum, string? aciklama, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapYaz);
        if (durum == ZimmetDurum.Zimmette) throw new DomainException("İade veya kayıp seçilmelidir.");
        await using var db = factory.Olustur();
        var z = await db.Zimmetler.SingleAsync(x => x.Id == id, ct);
        if (tarih < z.VerilisTarihi) throw new DomainException("İade tarihi veriliş tarihinden önce olamaz.");
        z.Durum = durum;
        z.IadeTarihi = tarih;
        if (!string.IsNullOrWhiteSpace(aciklama)) z.Aciklama = string.IsNullOrWhiteSpace(z.Aciklama) ? aciklama : $"{z.Aciklama} / {aciklama}";
        await db.KaydetAsync(ct);
    }

    // ------------------------------- Şoför özeti (detay KPI) -------------------------------

    public async Task<SoforOzet> OzetAsync(short soforId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        var bugun = saat.Bugun;
        var ayBas = new DateOnly(bugun.Year, bugun.Month, 1);
        var donem = bugun.Year * 100 + bugun.Month;
        await using var db = factory.Olustur();
        var s = await db.Soforler.AsNoTracking().Include(x => x.VarsayilanArac).SingleAsync(x => x.Id == soforId, ct);
        var bakiye = await Defter.CariBakiyeAsync(db, s.CariId, ct);
        var acikAvans = await db.Avanslar.Where(a => a.SoforId == soforId && a.MahsupId == null && !a.Iptal && a.Tur != AvansTuru.MaasOdemesi).SumAsync(a => (decimal?)a.Tutar * a.Kur, ct) ?? 0;
        var onay = await db.Masraflar.CountAsync(m => m.SoforId == soforId && !m.Onayli && !m.Iptal, ct);
        var sefer = await db.SeferSoforleri.CountAsync(x => x.SoforId == soforId && x.Sefer!.PlanlananTarih >= ayBas && x.Sefer.Durum != SeferDurum.Iptal, ct);
        var hakedis = await db.PersonelTahakkuklari.Where(t => t.SoforId == soforId && t.Donem == donem && !t.Iptal)
            .SumAsync(t => (decimal?)(t.Tur == TahakkukTuru.Kesinti ? -t.Tutar : t.Tutar), ct) ?? 0;
        var zimmet = await db.Zimmetler.CountAsync(z => z.SoforId == soforId && z.Durum == ZimmetDurum.Zimmette, ct);
        return new SoforOzet(s.Id, s.AdSoyad, s.CariId, s.VarsayilanArac?.Plaka, s.Telefon, bakiye, acikAvans, onay, sefer, hakedis, zimmet, s.BakiyeLimiti);
    }
}

public sealed record SoforDefterSatir(int Id, DateOnly Tarih, short SoforId, string Sofor, string? Plaka, string? SeferNo, KaynakTip KaynakTip, int? KaynakId,
    string? Aciklama, decimal Verilen, decimal Alinan, decimal? Bakiye, string? Doviz, bool Iptal) : IRenkliSatir
{
    public string Islem => KaynakTip.Ad();
    public string? Renk => Iptal ? "gri" : null;
}

public sealed record ZimmetSatir(int Id, ZimmetTuru Tur, string Ad, string? SeriNo, string? Sofor, string? Plaka, DateOnly Verilis, DateOnly? Iade,
    ZimmetDurum Durum, decimal? Deger, string? Aciklama) : IRenkliSatir
{
    public string TurAd => Tur.Ad();
    public string DurumAd => Durum.Ad();
    public string? Renk => Durum == ZimmetDurum.Kayip ? "kirmizi" : Durum == ZimmetDurum.IadeEdildi ? "gri" : null;
}

public sealed record AtamaSatir(int Id, string Sofor, string Plaka, DateOnly Baslangic, DateOnly? Bitis)
{
    public bool Aktif => Bitis is null;
}

public sealed record HakedisOneri(short SoforId, string Sofor, TahakkukTuru Tur, decimal Tutar, int? SeferId, string? SeferNo, short? AracId, string Aciklama)
{
    public string TurAd => Tur.Ad();
}

public sealed record TahakkukSatir(int Id, int Donem, string Sofor, TahakkukTuru Tur, decimal Tutar, string? SeferNo, string? Plaka, string? Aciklama, bool Iptal) : IRenkliSatir
{
    public string TurAd => Tur.Ad();
    public string DonemAd => $"{Donem % 100:D2}.{Donem / 100}";
    public decimal Net => Tur == TahakkukTuru.Kesinti ? -Tutar : Tutar;
    public string? Renk => Iptal ? "gri" : Tur == TahakkukTuru.Kesinti ? "turuncu" : null;
}

public sealed record SoforOzet(short Id, string AdSoyad, int CariId, string? Plaka, string? Telefon, decimal Bakiye, decimal AcikAvans, int OnayBekleyen,
    int BuAySefer, decimal BuAyHakedis, int Zimmet, decimal? BakiyeLimiti)
{
    public bool LimitAsildi => BakiyeLimiti is > 0 && Bakiye > BakiyeLimiti;
}
