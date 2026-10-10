using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record AracBaslik(short Id, string Plaka, string? MarkaModel, short? Yil, string? Sofor, string? Dorse, string Durum, int Km, bool Aktif);

public sealed record AracOzet(AracBaslik Baslik, AracKarlilikSatir? Donem, decimal? LitreYuzKm, decimal KalanKrediTL, IReadOnlyList<AylikTrend> Trend,
    IReadOnlyList<(string Kalem, decimal Tutar)> GiderDagilimi);

public sealed record AracEkstreSatir(DateOnly Tarih, string Tur, string? Belge, string? Aciklama, decimal Gelir, decimal Gider, decimal Bakiye);

public sealed record KmSatir(DateTime Zaman, int Km, KmKaynak Kaynak, int? Fark, bool Anormal) : IRenkliSatir
{
    public string KaynakAd => Kaynak.Ad();
    public string? Renk => Anormal ? "kirmizi" : null;
}

public sealed record CariOzet(int Id, string Unvan, CariTipi Tip, string? Telefon, decimal Bakiye, decimal VadesiGecen, decimal? OrtTahsilGun, decimal BuYilCiro,
    YaslandirmaSonuc Yaslandirma, decimal? RiskLimiti);

public sealed record SeferOzet(Sefer Sefer, SeferKarlilik? Karlilik, string Musteri, string Guzergah, string? Plaka, string? Dorse, string Soforler,
    IReadOnlyList<(string Durak, DurakTipi Tip, DateTime? Plan, DateTime? Gercek)> Duraklar, string? FaturaNo, TimeSpan? Sure);

/// <summary>Araç / cari / sefer 360° detay ekranlarının özet sorguları.</summary>
public sealed class DetayServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat, RaporServisi rapor, GiderServisi gider,
    KrediServisi kredi, FinansServisi finans, SeferServisi sefer)
{
    // ------------------------------------------------------------ Araç

    public async Task<AracBaslik> AracBaslikAsync(short aracId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FiloGor);
        await using var db = factory.Olustur();
        var a = await db.Araclar.AsNoTracking().IgnoreQueryFilters().SingleAsync(x => x.Id == aracId, ct);
        var sofor = await db.SoforAracAtamalari.Where(x => x.AracId == aracId && x.Bitis == null).Select(x => x.Sofor!.Ad + " " + x.Sofor.Soyad).FirstOrDefaultAsync(ct)
                    ?? await db.Soforler.Where(s => s.VarsayilanAracId == aracId && s.Aktif).Select(s => s.Ad + " " + s.Soyad).FirstOrDefaultAsync(ct);
        var dorse = await db.AracDorseEslesmeleri.Where(e => e.AracId == aracId && e.Bitis == null).Select(e => e.Dorse!.Plaka).FirstOrDefaultAsync(ct);
        var seferde = await db.Seferler.Where(s => s.AracId == aracId && (s.Durum == SeferDurum.Yuklemede || s.Durum == SeferDurum.Yolda)).Select(s => s.SeferNo).FirstOrDefaultAsync(ct);
        return new AracBaslik(a.Id, a.Plaka, $"{a.Marka} {a.Model}".Trim(), a.ModelYili, sofor, dorse,
            !a.Aktif ? "Pasif" : seferde is not null ? $"Seferde ({seferde})" : "Boşta", a.GuncelKm, a.Aktif);
    }

    public async Task<AracOzet> AracOzetAsync(short aracId, DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        var baslik = await AracBaslikAsync(aracId, ct);
        AracKarlilikSatir? donem = null;
        var trend = new List<AylikTrend>();
        if (oturum.Yetkili(Izinler.RaporFinans))
        {
            donem = (await rapor.AracKarlilikAsync(bas, bit, ct)).SingleOrDefault(r => r.AracId == aracId);
            // 12 ay eşzamanlı hesaplanır (her ay kendi bağlantısını kullanır).
            var ilkAy = new DateOnly(bit.Year, bit.Month, 1).AddMonths(-11);
            var aylar = Enumerable.Range(0, 12).Select(i => ilkAy.AddMonths(i)).ToList();
            var sonuclar = await Task.WhenAll(aylar.Select(ay => rapor.AracKarlilikAsync(ay, ay.AddMonths(1).AddDays(-1), ct)));
            for (var i = 0; i < 12; i++)
            {
                var r = sonuclar[i].SingleOrDefault(x => x.AracId == aracId);
                trend.Add(new AylikTrend($"{aylar[i].Month:D2}.{aylar[i].Year % 100:D2}", r?.Gelir ?? 0, r?.ToplamGider ?? 0, r?.Kar ?? 0));
            }
        }
        decimal? lt = null;
        if (oturum.Yetkili(Izinler.GiderGor))
            lt = (await gider.TuketimRaporuAsync(bas, bit, ct)).SingleOrDefault(t => t.AracId == aracId)?.LitreYuzKm;
        decimal kalanKredi = 0;
        if (oturum.Yetkili(Izinler.FinansmanGor))
        {
            await using var db = factory.Olustur();
            var paylar = await db.KrediAraclari.AsNoTracking().Where(k => k.AracId == aracId).Select(k => new { k.KrediId, k.PayYuzde }).ToListAsync(ct);
            var krediler = await kredi.ListeAsync(false, ct);
            var kurlar = new Dictionary<ParaBirimi, decimal>();
            foreach (var p in paylar)
                if (krediler.FirstOrDefault(k => k.Id == p.KrediId) is { } k)
                {
                    if (!kurlar.TryGetValue(k.ParaBirimi, out var kur))
                        kurlar[k.ParaBirimi] = kur = await new DovizServisi(factory).KurAsync(k.ParaBirimi, saat.Bugun, ct) ?? 1;
                    kalanKredi += Para.TL(k.KalanAnapara * p.PayYuzde / 100m, kur);
                }
        }
        var dagilim = donem is null ? new List<(string, decimal)>() : new List<(string, decimal)>
        {
            ("Yakıt", donem.Yakit), ("Yol/Köprü", donem.YolKopru), ("Harcırah", donem.Harcirah), ("Bakım/Lastik", donem.Bakim),
            ("Sigorta", donem.Sigorta), ("Vergi/Harç", donem.VergiHarc), ("Personel", donem.Personel), ("Diğer", donem.DigerMasraf),
            ("Ceza", donem.Ceza), ("Amortisman", donem.Amortisman), ("Kredi faizi", donem.FinansmanFaizi),
        }.Where(x => x.Item2 > 0).ToList();
        return new AracOzet(baslik, donem, lt, kalanKredi, trend, dagilim);
    }

    /// <summary>Aracın gelir/gider hareketleri (TL, KDV hariç): sefer navlunu, masraf, yakıt, HGS, bakım, ceza, harcırah, kredi gideri payı.</summary>
    public async Task<List<AracEkstreSatir>> AracEkstreAsync(short aracId, DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.RaporFinans);
        await using var db = factory.Olustur();
        var b0 = bas.ToDateTime(TimeOnly.MinValue);
        var b1 = bit.ToDateTime(TimeOnly.MaxValue);
        var l = new List<(DateOnly T, string Tur, string? Belge, string? Acik, decimal G, decimal C)>();
        foreach (var s in await db.Seferler.AsNoTracking().Where(s => s.AracId == aracId && s.Durum != SeferDurum.Iptal && s.PlanlananTarih >= bas && s.PlanlananTarih <= bit)
                     .Select(s => new { s.PlanlananTarih, s.SeferNo, Musteri = s.Musteri!.Unvan, G = s.CikisLokasyon!.Ad + " → " + s.VarisLokasyon!.Ad, s.NavlunTutar, s.NavlunKur }).ToListAsync(ct))
            l.Add((s.PlanlananTarih, "Sefer geliri", s.SeferNo, $"{s.Musteri} — {s.G}", Para.TL(s.NavlunTutar, s.NavlunKur), 0));
        foreach (var m in await db.Masraflar.AsNoTracking().Where(m => m.AracId == aracId && !m.Iptal && m.Tarih >= bas && m.Tarih <= bit)
                     .Select(m => new { m.Tarih, Tur = m.MasrafTuru!.Ad, m.BelgeNo, m.Aciklama, Sefer = m.Sefer != null ? m.Sefer.SeferNo : null, m.Tutar, m.Kur, m.KdvOrani }).ToListAsync(ct))
            l.Add((m.Tarih, m.Tur, m.Sefer ?? m.BelgeNo, m.Aciklama, 0, Para.TL(Para.KdvHaric(m.Tutar, m.KdvOrani), m.Kur)));
        foreach (var y in await db.Yakitlar.AsNoTracking().Where(y => y.AracId == aracId && !y.Iptal && y.AlimZamani >= b0 && y.AlimZamani <= b1)
                     .Select(y => new { y.AlimZamani, y.Litre, y.IstasyonAdi, Sefer = y.Sefer != null ? y.Sefer.SeferNo : null, y.Tutar, y.Kur, y.KdvOrani }).ToListAsync(ct))
            l.Add((DateOnly.FromDateTime(y.AlimZamani), "Yakıt", y.Sefer, $"{y.Litre:N2} L {y.IstasyonAdi}", 0, Para.TL(Para.KdvHaric(y.Tutar, y.KdvOrani), y.Kur)));
        foreach (var h in await db.HgsGecisleri.AsNoTracking().Where(h => h.AracId == aracId && h.GecisZamani >= b0 && h.GecisZamani <= b1)
                     .GroupBy(h => h.GecisZamani.Date).Select(g => new { g.Key, T = g.Sum(h => h.Tutar), N = g.Count() }).ToListAsync(ct))
            l.Add((DateOnly.FromDateTime(h.Key), "HGS/OGS", null, $"{h.N} geçiş", 0, h.T));
        foreach (var b in await db.Bakimlar.AsNoTracking().Where(b => b.AracId == aracId && !b.Iptal && b.Tarih >= bas && b.Tarih <= bit)
                     .Select(b => new { b.Tarih, b.BakimTuru, b.BelgeNo, b.Aciklama, b.ToplamTutar, b.KdvOrani }).ToListAsync(ct))
            l.Add((b.Tarih, b.BakimTuru.Ad(), b.BelgeNo, b.Aciklama, 0, Para.KdvHaric(b.ToplamTutar, b.KdvOrani)));
        foreach (var c in await db.Cezalar.AsNoTracking().Where(c => c.AracId == aracId && !c.RucuEdilsin && c.Tarih >= bas && c.Tarih <= bit)
                     .Select(c => new { c.Tarih, c.CezaTuru, c.TutanakNo, T = c.IndirimliTutar ?? c.Tutar }).ToListAsync(ct))
            l.Add((c.Tarih, "Ceza", c.TutanakNo, c.CezaTuru, 0, c.T));
        foreach (var h in await db.Harcirahlar.AsNoTracking().Where(h => !h.Iptal && (h.AracId == aracId || (h.AracId == null && h.Sefer!.AracId == aracId)) && h.Bitis >= bas && h.Bitis <= bit)
                     .Select(h => new { h.Bitis, Sefer = h.Sefer != null ? h.Sefer.SeferNo : null, Sofor = h.Sofor!.Ad + " " + h.Sofor.Soyad, h.Tutar, h.Kur }).ToListAsync(ct))
            l.Add((h.Bitis, "Harcırah", h.Sefer, h.Sofor, 0, Para.TL(h.Tutar, h.Kur)));
        var paylar = await db.KrediAraclari.AsNoTracking().Where(a => a.AracId == aracId).Select(a => new { a.KrediId, a.PayYuzde }).ToListAsync(ct);
        var kids = paylar.Select(p => p.KrediId).ToList();
        foreach (var o in await db.KrediOdemeleri.AsNoTracking().Where(o => !o.Iptal && kids.Contains(o.KrediId) && o.Tarih >= bas && o.Tarih <= bit)
                     .Select(o => new { o.Tarih, o.KrediId, o.Kredi!.KrediNo, Gider = o.Faiz + o.Bsmv + o.Kkdf + o.GecikmeFaizi + o.Komisyon, o.Kur }).ToListAsync(ct))
        {
            var pay = paylar.First(p => p.KrediId == o.KrediId).PayYuzde;
            l.Add((o.Tarih, "Kredi faizi/vergisi", o.KrediNo, pay < 100 ? $"%{pay:0.#} pay" : null, 0, Para.TL(o.Gider * pay / 100m, o.Kur)));
        }
        decimal bakiye = 0;
        return l.OrderBy(x => x.T).Select(x =>
        {
            bakiye += x.G - x.C;
            return new AracEkstreSatir(x.T, x.Tur, x.Belge, x.Acik, x.G, x.C, bakiye);
        }).ToList();
    }

    public async Task<List<KmSatir>> KmGecmisiAsync(short aracId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FiloGor);
        await using var db = factory.Olustur();
        var ham = await db.AracKmKayitlari.AsNoTracking().Where(k => k.AracId == aracId).OrderBy(k => k.Zaman).ThenBy(k => k.Id).ToListAsync(ct);
        var sonuc = new List<KmSatir>();
        int? onceki = null;
        foreach (var k in ham)
        {
            var fark = onceki is null ? (int?)null : k.Km - onceki;
            sonuc.Add(new KmSatir(k.Zaman, k.Km, k.Kaynak, fark, fark < 0));
            onceki = k.Km;
        }
        sonuc.Reverse();
        return sonuc;
    }

    // ------------------------------------------------------------ Cari

    public async Task<CariOzet> CariOzetAsync(int cariId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.FinansGor);
        var bugun = saat.Bugun;
        await using var db = factory.Olustur();
        var c = await db.Cariler.AsNoTracking().IgnoreQueryFilters().SingleAsync(x => x.Id == cariId, ct);
        var hareketler = await db.CariHareketleri.AsNoTracking().Where(h => h.CariId == cariId)
            .Select(h => new { Vade = h.VadeTarihi ?? h.Tarih, h.Borc, h.Alacak }).ToListAsync(ct);
        var yas = Yaslandirma.Hesapla(hareketler.Select(h => (h.Vade, h.Borc, h.Alacak)), bugun);
        var bakiye = hareketler.Sum(h => h.Borc - h.Alacak);
        var yilBas = new DateOnly(bugun.Year, 1, 1);
        var ciro = await db.Faturalar.Where(f => f.CariId == cariId && f.FaturaTipi == FaturaTipi.Satis && f.Durum == FaturaDurum.Kayitli && f.Tarih >= yilBas)
            .SumAsync(f => (decimal?)(f.AraToplam * f.Kur), ct) ?? 0;
        // Ortalama tahsil süresi (DSO): açık alacak / son 90 günlük satış × 90
        var son90 = await db.Faturalar.Where(f => f.CariId == cariId && f.FaturaTipi == FaturaTipi.Satis && f.Durum == FaturaDurum.Kayitli && f.Tarih >= bugun.AddDays(-90))
            .SumAsync(f => (decimal?)(f.GenelToplam * f.Kur), ct) ?? 0;
        decimal? dso = son90 > 0 && bakiye > 0 ? Math.Round(bakiye / son90 * 90, 0) : null;
        return new CariOzet(c.Id, c.Unvan, c.CariTipi, c.Telefon, bakiye, yas.Gun0_30 + yas.Gun31_60 + yas.Gun61_90 + yas.Gun90Ustu, dso, Para.Yuvarla(ciro), yas, c.RiskLimiti);
    }

    // ------------------------------------------------------------ Sefer

    public async Task<SeferOzet> SeferOzetAsync(int seferId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.SeferGor);
        var s = await sefer.GetirAsync(seferId, ct) ?? throw new DomainException("Sefer bulunamadı.");
        await using var db = factory.Olustur();
        var bilgi = await db.Seferler.AsNoTracking().Where(x => x.Id == seferId).Select(x => new
        {
            Musteri = x.Musteri!.Unvan, G = x.CikisLokasyon!.Ad + " → " + x.VarisLokasyon!.Ad,
            Plaka = x.Arac != null ? x.Arac.Plaka : x.TaseronPlaka, Dorse = x.Dorse != null ? x.Dorse.Plaka : null,
            Soforler = x.Soforler.OrderByDescending(y => y.Birincil).Select(y => y.Sofor!.Ad + " " + y.Sofor.Soyad).ToList(),
            Duraklar = x.Duraklar.OrderBy(d => d.Sira).Select(d => new { d.Lokasyon!.Ad, d.DurakTipi, d.PlanlananTarih, d.GerceklesenTarih }).ToList(),
        }).SingleAsync(ct);
        var fatura = await db.FaturaKalemleri.Where(k => k.SeferId == seferId && k.Fatura!.Durum == FaturaDurum.Kayitli).Select(k => k.Fatura!.FaturaNo).FirstOrDefaultAsync(ct);
        SeferKarlilik? kar = oturum.Yetkili(Izinler.RaporFinans) ? await sefer.KarlilikAsync(seferId, ct) : null;
        TimeSpan? sure = s.CikisTarihi is { } c ? (s.DonusTarihi ?? s.TeslimTarihi ?? saat.Simdi) - c : null;
        return new SeferOzet(s, kar, bilgi.Musteri, bilgi.G, bilgi.Plaka, bilgi.Dorse, string.Join(", ", bilgi.Soforler),
            bilgi.Duraklar.Select(d => (d.Ad, d.DurakTipi, d.PlanlananTarih, d.GerceklesenTarih)).ToList(), fatura, sure);
    }
}
