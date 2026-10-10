using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record SeferKarlilikSatir(int SeferId, string SeferNo, DateOnly Tarih, string Musteri, string? Plaka, string Guzergah,
    decimal Gelir, decimal Gider, decimal Kar, decimal? Marj, int? Km, decimal? KmBasiMaliyet, bool Taseron);

public sealed class AracKarlilikSatir
{
    public short AracId { get; init; }
    public string Plaka { get; init; } = "";
    public int SeferSayisi { get; set; }
    public int Km { get; set; }
    public int BosKm { get; set; }
    public decimal Gelir { get; set; }
    public decimal Yakit { get; set; }
    public decimal YolKopru { get; set; }
    public decimal Harcirah { get; set; }
    public decimal Bakim { get; set; }
    public decimal Sigorta { get; set; }
    public decimal VergiHarc { get; set; }
    public decimal Personel { get; set; }
    public decimal DigerMasraf { get; set; }
    public decimal Ceza { get; set; }
    public decimal Amortisman { get; set; }
    public decimal FinansmanFaizi { get; set; }
    public decimal DegiskenGider => Yakit + YolKopru + Harcirah + Bakim + DigerMasraf + Ceza;
    public decimal SabitGider => Sigorta + VergiHarc + Personel + Amortisman + FinansmanFaizi;
    public decimal ToplamGider => DegiskenGider + SabitGider;
    public decimal Kar => Gelir - ToplamGider;
    public decimal? Marj => Gelir == 0 ? null : Math.Round(Kar * 100 / Gelir, 1);
    public decimal? KmBasiMaliyet => Km > 0 ? Math.Round(ToplamGider / Km, 2) : null;
    public decimal? KmBasiGelir => Km > 0 ? Math.Round(Gelir / Km, 2) : null;
    public decimal? BosKmOrani => Km > 0 ? Math.Round(BosKm * 100m / Km, 1) : null;
}

public sealed record AylikSatir(int Yil, int Ay, int SeferSayisi, decimal Gelir, decimal Gider, decimal TaseronGider)
{
    public decimal Kar => Gelir - Gider - TaseronGider;
    public string Donem => $"{Ay:D2}.{Yil}";
}

public sealed record SoforPerformansSatir(short SoforId, string AdSoyad, int SeferSayisi, int Km, decimal Gelir, decimal SeferKari,
    decimal? LitreYuzKm, int CezaSayisi, decimal CezaTutari, decimal Harcirah);

public sealed record NakitAkisSatir(DateOnly Vade, string Tur, string Karsi, string? Aciklama, decimal Giris, decimal Cikis);

public sealed class RaporServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat)
{
    public async Task<List<SeferKarlilikSatir>> SeferKarlilikAsync(DateOnly bas, DateOnly bit, short? aracId = null, int? musteriId = null, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.RaporFinans);
        await using var db = factory.Olustur();
        var q = db.Seferler.Where(s => s.PlanlananTarih >= bas && s.PlanlananTarih <= bit && s.Durum != SeferDurum.Iptal);
        if (aracId is not null) q = q.Where(s => s.AracId == aracId);
        if (musteriId is not null) q = q.Where(s => s.MusteriCariId == musteriId);
        var bilgi = await q.AsNoTracking().Select(s => new
        {
            s.Id, s.PlanlananTarih, Musteri = s.Musteri!.Unvan, Plaka = s.Arac != null ? s.Arac.Plaka : s.TaseronPlaka,
            Guzergah = s.CikisLokasyon!.Ad + " → " + s.VarisLokasyon!.Ad, Taseron = s.TaseronCariId != null,
        }).ToDictionaryAsync(s => s.Id, ct);
        var karlar = await SeferServisi.KarlilikHesaplaAsync(db, q, ct);
        return karlar.Select(k =>
        {
            var b = bilgi[k.SeferId];
            return new SeferKarlilikSatir(k.SeferId, k.SeferNo, b.PlanlananTarih, b.Musteri, b.Plaka, b.Guzergah,
                k.GelirTL, k.GiderTL, k.KarTL, k.KarMarji, k.Km, k.KmBasiMaliyet, b.Taseron);
        }).OrderBy(s => s.Tarih).ThenBy(s => s.SeferNo).ToList();
    }

    /// <summary>
    /// Araç kârlılığı (TL, KDV hariç). Gelir: dönemdeki seferlerin navlunu. Gider: araca bağlı masraf (sefer masrafları dahil),
    /// yakıt, HGS, bakım, rücu edilmeyen ceza, sefer harcırahları, dönem amortismanı ve vadesi dönemdeki kredi/leasing faizi.
    /// </summary>
    public async Task<List<AracKarlilikSatir>> AracKarlilikAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.RaporFinans);
        await using var db = factory.Olustur();
        var araclar = await db.Araclar.AsNoTracking()
            .Where(a => a.Aktif || db.Seferler.Any(s => s.AracId == a.Id && s.PlanlananTarih >= bas && s.PlanlananTarih <= bit))
            .Select(a => new { a.Id, a.Plaka, a.AlisBedeli, a.HurdaDegeri, a.FaydaliOmurAy, a.AlisTarihi }).ToListAsync(ct);
        var sonuc = araclar.ToDictionary(a => a.Id, a => new AracKarlilikSatir
        {
            AracId = a.Id, Plaka = a.Plaka,
            Amortisman = AmortismanHesap.Donem(a.AlisBedeli, a.HurdaDegeri, a.FaydaliOmurAy, a.AlisTarihi, bas, bit),
        });

        var seferler = await db.Seferler.AsNoTracking()
            .Where(s => s.AracId != null && s.PlanlananTarih >= bas && s.PlanlananTarih <= bit && s.Durum != SeferDurum.Iptal)
            .Select(s => new { AracId = s.AracId!.Value, s.Id, s.NavlunTutar, s.NavlunKur, Km = s.DonusKm - s.CikisKm, s.BosKm }).ToListAsync(ct);
        foreach (var s in seferler.Where(s => sonuc.ContainsKey(s.AracId)))
        {
            var r = sonuc[s.AracId];
            r.SeferSayisi++;
            r.Gelir += Para.TL(s.NavlunTutar, s.NavlunKur);
            r.Km += s.Km ?? 0;
            r.BosKm += s.BosKm ?? 0;
        }

        var masraflar = await db.Masraflar.AsNoTracking()
            .Where(m => m.AracId != null && !m.Iptal && m.Tarih >= bas && m.Tarih <= bit)
            .Select(m => new { AracId = m.AracId!.Value, m.MasrafTuru!.Grup, m.Tutar, m.Kur, m.KdvOrani }).ToListAsync(ct);
        foreach (var m in masraflar.Where(m => sonuc.ContainsKey(m.AracId)))
        {
            var r = sonuc[m.AracId];
            var t = Para.TL(Para.KdvHaric(m.Tutar, m.KdvOrani), m.Kur);
            switch (m.Grup)
            {
                case MasrafGrubu.Yakit: r.Yakit += t; break;
                case MasrafGrubu.YolKopru or MasrafGrubu.Feribot: r.YolKopru += t; break;
                case MasrafGrubu.Bakim or MasrafGrubu.Lastik: r.Bakim += t; break;
                case MasrafGrubu.Sigorta: r.Sigorta += t; break;
                case MasrafGrubu.Vergi: r.VergiHarc += t; break;
                case MasrafGrubu.Personel: r.Personel += t; break;
                case MasrafGrubu.Ceza: r.Ceza += t; break;
                default: r.DigerMasraf += t; break;
            }
        }

        var b0 = bas.ToDateTime(TimeOnly.MinValue);
        var b1 = bit.ToDateTime(TimeOnly.MaxValue);
        var yakitlar = await db.Yakitlar.AsNoTracking().Where(y => !y.Iptal && y.AlimZamani >= b0 && y.AlimZamani <= b1)
            .Select(y => new { y.AracId, y.Tutar, y.Kur, y.KdvOrani }).ToListAsync(ct);
        foreach (var y in yakitlar.Where(y => sonuc.ContainsKey(y.AracId)))
            sonuc[y.AracId].Yakit += Para.TL(Para.KdvHaric(y.Tutar, y.KdvOrani), y.Kur);

        foreach (var h in await db.HgsGecisleri.AsNoTracking().Where(h => h.GecisZamani >= b0 && h.GecisZamani <= b1)
                     .GroupBy(h => h.AracId).Select(g => new { g.Key, T = g.Sum(h => h.Tutar) }).ToListAsync(ct))
            if (sonuc.TryGetValue(h.Key, out var r)) r.YolKopru += h.T;

        foreach (var b in await db.Bakimlar.AsNoTracking().Where(b => b.AracId != null && !b.Iptal && b.Tarih >= bas && b.Tarih <= bit)
                     .Select(b => new { AracId = b.AracId!.Value, b.ToplamTutar, b.KdvOrani }).ToListAsync(ct))
            if (sonuc.TryGetValue(b.AracId, out var r)) r.Bakim += Para.KdvHaric(b.ToplamTutar, b.KdvOrani);

        foreach (var c in await db.Cezalar.AsNoTracking().Where(c => c.AracId != null && !c.RucuEdilsin && c.Tarih >= bas && c.Tarih <= bit)
                     .Select(c => new { AracId = c.AracId!.Value, T = c.IndirimliTutar ?? c.Tutar }).ToListAsync(ct))
            if (sonuc.TryGetValue(c.AracId, out var r)) r.Ceza += c.T;

        foreach (var h in await db.Harcirahlar.AsNoTracking().Where(h => !h.Iptal && h.SeferId != null && h.Sefer!.AracId != null && h.Bitis >= bas && h.Bitis <= bit)
                     .Select(h => new { AracId = h.Sefer!.AracId!.Value, h.Tutar, h.Kur }).ToListAsync(ct))
            if (sonuc.TryGetValue(h.AracId, out var r)) r.Harcirah += Para.TL(h.Tutar, h.Kur);

        foreach (var t in await db.FinansmanTaksitleri.AsNoTracking().Where(t => t.AracFinansman!.AracId != null && t.VadeTarihi >= bas && t.VadeTarihi <= bit)
                     .Select(t => new { AracId = t.AracFinansman!.AracId!.Value, t.Faiz }).ToListAsync(ct))
            if (sonuc.TryGetValue(t.AracId, out var r)) r.FinansmanFaizi += t.Faiz;

        return sonuc.Values.OrderByDescending(r => r.Kar).ToList();
    }

    public async Task<List<AylikSatir>> AylikAsync(int yil, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.RaporFinans);
        var bas = new DateOnly(yil, 1, 1);
        var bit = new DateOnly(yil, 12, 31);
        await using var db = factory.Olustur();
        var aylar = Enumerable.Range(1, 12).ToDictionary(a => a, _ => (Sefer: 0, Gelir: 0m, Gider: 0m, Taseron: 0m));
        void Ekle(int ay, int sefer = 0, decimal gelir = 0, decimal gider = 0, decimal taseron = 0)
        {
            var x = aylar[ay];
            aylar[ay] = (x.Sefer + sefer, x.Gelir + gelir, x.Gider + gider, x.Taseron + taseron);
        }

        foreach (var s in await db.Seferler.AsNoTracking().Where(s => s.PlanlananTarih >= bas && s.PlanlananTarih <= bit && s.Durum != SeferDurum.Iptal)
                     .Select(s => new { s.PlanlananTarih.Month, s.NavlunTutar, s.NavlunKur, s.TaseronBedel, s.TaseronKur }).ToListAsync(ct))
            Ekle(s.Month, 1, Para.TL(s.NavlunTutar, s.NavlunKur), taseron: s.TaseronBedel is > 0 ? Para.TL(s.TaseronBedel.Value, s.TaseronKur) : 0);
        foreach (var m in await db.Masraflar.AsNoTracking().Where(m => !m.Iptal && m.Tarih >= bas && m.Tarih <= bit)
                     .Select(m => new { m.Tarih.Month, m.Tutar, m.Kur, m.KdvOrani }).ToListAsync(ct))
            Ekle(m.Month, gider: Para.TL(Para.KdvHaric(m.Tutar, m.KdvOrani), m.Kur));
        var b0 = bas.ToDateTime(TimeOnly.MinValue);
        var b1 = bit.ToDateTime(TimeOnly.MaxValue);
        foreach (var y in await db.Yakitlar.AsNoTracking().Where(y => !y.Iptal && y.AlimZamani >= b0 && y.AlimZamani <= b1)
                     .Select(y => new { y.AlimZamani.Month, y.Tutar, y.Kur, y.KdvOrani }).ToListAsync(ct))
            Ekle(y.Month, gider: Para.TL(Para.KdvHaric(y.Tutar, y.KdvOrani), y.Kur));
        foreach (var h in await db.HgsGecisleri.AsNoTracking().Where(h => h.GecisZamani >= b0 && h.GecisZamani <= b1)
                     .GroupBy(h => h.GecisZamani.Month).Select(g => new { g.Key, T = g.Sum(h => h.Tutar) }).ToListAsync(ct))
            Ekle(h.Key, gider: h.T);
        foreach (var b in await db.Bakimlar.AsNoTracking().Where(b => !b.Iptal && b.Tarih >= bas && b.Tarih <= bit)
                     .Select(b => new { b.Tarih.Month, b.ToplamTutar, b.KdvOrani }).ToListAsync(ct))
            Ekle(b.Month, gider: Para.KdvHaric(b.ToplamTutar, b.KdvOrani));
        foreach (var h in await db.Harcirahlar.AsNoTracking().Where(h => !h.Iptal && h.Bitis >= bas && h.Bitis <= bit)
                     .Select(h => new { h.Bitis.Month, h.Tutar, h.Kur }).ToListAsync(ct))
            Ekle(h.Month, gider: Para.TL(h.Tutar, h.Kur));
        foreach (var c in await db.Cezalar.AsNoTracking().Where(c => !c.RucuEdilsin && c.Tarih >= bas && c.Tarih <= bit)
                     .Select(c => new { c.Tarih.Month, T = c.IndirimliTutar ?? c.Tutar }).ToListAsync(ct))
            Ekle(c.Month, gider: c.T);
        foreach (var t in await db.FinansmanTaksitleri.AsNoTracking().Where(t => t.VadeTarihi >= bas && t.VadeTarihi <= bit)
                     .Select(t => new { t.VadeTarihi.Month, t.Faiz }).ToListAsync(ct))
            Ekle(t.Month, gider: t.Faiz);

        return aylar.Select(a => new AylikSatir(yil, a.Key, a.Value.Sefer, a.Value.Gelir, a.Value.Gider, a.Value.Taseron)).ToList();
    }

    public async Task<List<SoforPerformansSatir>> SoforPerformansAsync(DateOnly bas, DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.RaporFinans);
        await using var db = factory.Olustur();
        var seferQ = db.Seferler.Where(s => s.PlanlananTarih >= bas && s.PlanlananTarih <= bit && s.Durum != SeferDurum.Iptal && s.AracId != null);
        var karlar = (await SeferServisi.KarlilikHesaplaAsync(db, seferQ, ct)).ToDictionary(k => k.SeferId);
        var atamalar = await db.SeferSoforleri.AsNoTracking().Where(x => seferQ.Any(s => s.Id == x.SeferId) && x.Birincil)
            .Select(x => new { x.SoforId, x.SeferId, Ad = x.Sofor!.Ad + " " + x.Sofor.Soyad }).ToListAsync(ct);
        var yakit = await db.Yakitlar.AsNoTracking().Where(y => !y.Iptal && y.SeferId != null && seferQ.Any(s => s.Id == y.SeferId))
            .Select(y => new { SeferId = y.SeferId!.Value, y.Litre }).ToListAsync(ct);
        var cezalar = await db.Cezalar.AsNoTracking().Where(c => c.SoforId != null && c.Tarih >= bas && c.Tarih <= bit)
            .Select(c => new { SoforId = c.SoforId!.Value, c.Tutar }).ToListAsync(ct);
        var harc = await db.Harcirahlar.AsNoTracking().Where(h => !h.Iptal && h.Bitis >= bas && h.Bitis <= bit)
            .Select(h => new { h.SoforId, h.Tutar, h.Kur }).ToListAsync(ct);

        return atamalar.GroupBy(a => (a.SoforId, a.Ad)).Select(g =>
        {
            var seferIdleri = g.Select(x => x.SeferId).ToHashSet();
            var km = seferIdleri.Sum(id => karlar.TryGetValue(id, out var k) ? k.Km ?? 0 : 0);
            var litre = yakit.Where(y => seferIdleri.Contains(y.SeferId)).Sum(y => y.Litre);
            var sc = cezalar.Where(c => c.SoforId == g.Key.SoforId).ToList();
            return new SoforPerformansSatir(g.Key.SoforId, g.Key.Ad, seferIdleri.Count, km,
                seferIdleri.Sum(id => karlar.TryGetValue(id, out var k) ? k.GelirTL : 0),
                seferIdleri.Sum(id => karlar.TryGetValue(id, out var k) ? k.KarTL : 0),
                km > 0 && litre > 0 ? Math.Round(litre * 100 / km, 2) : null,
                sc.Count, sc.Sum(c => c.Tutar), harc.Where(h => h.SoforId == g.Key.SoforId).Sum(h => Para.TL(h.Tutar, h.Kur)));
        }).OrderByDescending(s => s.SeferKari).ToList();
    }

    /// <summary>
    /// Nakit akış tahmini: müşterilerin açık (FIFO) borçları vadesinde giriş, tedarikçi/taşeronlara açık borçlarımız çıkış,
    /// portföydeki çekler, verilen çekler ve ödenmemiş kredi taksitleri.
    /// </summary>
    public async Task<List<NakitAkisSatir>> NakitAkisAsync(DateOnly bit, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.RaporFinans);
        await using var db = factory.Olustur();
        var sonuc = new List<NakitAkisSatir>();
        var hareketler = await db.CariHareketleri.AsNoTracking()
            .Where(h => (h.Cari!.CariTipi & CariTipi.Personel) == 0)
            .Select(h => new { h.CariId, h.Cari!.Unvan, Vade = h.VadeTarihi ?? h.Tarih, h.Borc, h.Alacak, h.Aciklama })
            .ToListAsync(ct);
        foreach (var g in hareketler.GroupBy(h => (h.CariId, h.Unvan)))
        {
            var bakiye = g.Sum(h => h.Borc - h.Alacak);
            if (bakiye == 0) continue;
            // Bakiye yönündeki en yeni kalemler açıktır (FIFO: eskiler kapanmıştır).
            var alacakli = bakiye > 0;
            var kalan = Math.Abs(bakiye);
            foreach (var h in g.Where(h => alacakli ? h.Borc > 0 : h.Alacak > 0).OrderByDescending(h => h.Vade))
            {
                if (kalan <= 0) break;
                var tutar = Math.Min(kalan, alacakli ? h.Borc : h.Alacak);
                kalan -= tutar;
                if (h.Vade > bit) continue;
                sonuc.Add(alacakli
                    ? new NakitAkisSatir(h.Vade, "Müşteri alacağı", g.Key.Unvan, h.Aciklama, tutar, 0)
                    : new NakitAkisSatir(h.Vade, "Cari borcu", g.Key.Unvan, h.Aciklama, 0, tutar));
            }
        }
        foreach (var c in await db.CekSenetler.AsNoTracking()
                     .Where(c => c.VadeTarihi <= bit && ((c.Yon == CekSenetYon.Alinan && (c.Durum == CekSenetDurum.Portfoyde || c.Durum == CekSenetDurum.TahsileVerildi))
                                                         || (c.Yon == CekSenetYon.Verilen && c.Durum == CekSenetDurum.CiroEdildi)))
                     .Select(c => new { c.VadeTarihi, c.Yon, c.Tip, c.PortfoyNo, Cari = c.Cari!.Unvan, c.Tutar }).ToListAsync(ct))
            sonuc.Add(new NakitAkisSatir(c.VadeTarihi, $"{c.Yon.Ad()} {c.Tip.Ad()}", c.Cari, c.PortfoyNo,
                c.Yon == CekSenetYon.Alinan ? c.Tutar : 0, c.Yon == CekSenetYon.Verilen ? c.Tutar : 0));
        foreach (var t in await db.FinansmanTaksitleri.AsNoTracking().Where(t => t.OdemeTarihi == null && t.VadeTarihi <= bit && t.AracFinansman!.Aktif)
                     .Select(t => new { t.VadeTarihi, Kurum = t.AracFinansman!.FinansKurumu!.Unvan, t.TaksitNo, t.AracFinansman.SozlesmeNo, t.Toplam }).ToListAsync(ct))
            sonuc.Add(new NakitAkisSatir(t.VadeTarihi, "Kredi/leasing taksiti", t.Kurum, $"{t.SozlesmeNo} #{t.TaksitNo}", 0, t.Toplam));
        return sonuc.OrderBy(s => s.Vade).ToList();
    }
}
