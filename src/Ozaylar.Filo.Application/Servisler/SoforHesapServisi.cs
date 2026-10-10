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

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        a.Id = 0;
        a.MahsupId = null;
        a.Iptal = false;
        db.Avanslar.Add(a);
        await db.KaydetAsync(ct);
        var acik = $"Avans - {sofor.Ad} {sofor.Soyad}{(seferNo is null ? "" : " / " + seferNo)}";
        Defter.Kasa(db, a.KasaHesapId, a.Tarih, 0, a.Tutar, KaynakTip.Avans, a.Id, acik, sofor.CariId, a.Kur);
        Defter.Cari(db, sofor.CariId, a.Tarih, a.Tutar, 0, KaynakTip.Avans, a.Id, acik, a.ParaBirimi, a.Kur);
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
        await db.KaydetAsync(ct);
        Defter.Cari(db, sofor.CariId, h.Bitis, 0, h.Tutar, KaynakTip.Harcirah, h.Id,
            $"Harcırah {h.GunSayisi:0.#} gün × {h.GunlukTutar:N2}{(seferNo is null ? "" : " / " + seferNo)}", h.ParaBirimi, h.Kur);
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
                .Where(a => a.SoforId == soforId && a.MahsupId == null && !a.Iptal && (seferId == null || a.SeferId == seferId))
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
        if (sonuc == MahsupSonuc.IadeAlindi)
        {
            Defter.Kasa(db, kasaHesapId!.Value, tarih, fark, 0, KaynakTip.Mahsup, m.Id, metin, sofor.CariId);
            Defter.Cari(db, sofor.CariId, tarih, 0, fark, KaynakTip.Mahsup, m.Id, metin);
        }
        else if (sonuc == MahsupSonuc.SoforeOdendi)
        {
            Defter.Kasa(db, kasaHesapId!.Value, tarih, 0, -fark, KaynakTip.Mahsup, m.Id, metin, sofor.CariId);
            Defter.Cari(db, sofor.CariId, tarih, -fark, 0, KaynakTip.Mahsup, m.Id, metin);
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

    public async Task<List<AvansSatir>> AvanslarAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.Avanslar.AsNoTracking().Where(a => a.Tarih >= bas && a.Tarih <= bit).OrderByDescending(a => a.Tarih).ThenByDescending(a => a.Id)
            .Select(a => new AvansSatir(a.Id, a.Tarih, a.Sofor!.Ad + " " + a.Sofor.Soyad, a.Sefer != null ? a.Sefer.SeferNo : null, a.Tutar, a.ParaBirimi,
                db.KasaHesaplari.Where(k => k.Id == a.KasaHesapId).Select(k => k.Ad).FirstOrDefault(), a.Aciklama, a.MahsupId != null, a.Iptal))
            .ToListAsync(ct);
    }

    public async Task<List<HarcirahSatir>> HarcirahlarAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SoforHesapGor);
        await using var db = factory.Olustur();
        return await db.Harcirahlar.AsNoTracking().Where(h => h.Bitis >= bas && h.Baslangic <= bit).OrderByDescending(h => h.Bitis)
            .Select(h => new HarcirahSatir(h.Id, h.Sofor!.Ad + " " + h.Sofor.Soyad, h.Sefer != null ? h.Sefer.SeferNo : null,
                h.UlkeId != null ? db.Ulkeler.Where(u => u.Id == h.UlkeId).Select(u => u.Ad).FirstOrDefault() : "Yurtiçi",
                h.Baslangic, h.Bitis, h.GunSayisi, h.GunlukTutar, h.Tutar, h.ParaBirimi, h.MahsupId != null, h.Iptal))
            .ToListAsync(ct);
    }

    private static async Task<Sofor> SoforAsync(IFiloDbContext db, short soforId, CancellationToken ct) =>
        await db.Soforler.AsNoTracking().SingleOrDefaultAsync(s => s.Id == soforId, ct) ?? throw new DomainException("Şoför seçilmelidir.");

    private static async Task<string?> SeferNoAsync(IFiloDbContext db, int? seferId, CancellationToken ct) =>
        seferId is null ? null : await db.Seferler.Where(s => s.Id == seferId).Select(s => s.SeferNo).SingleOrDefaultAsync(ct);
}
