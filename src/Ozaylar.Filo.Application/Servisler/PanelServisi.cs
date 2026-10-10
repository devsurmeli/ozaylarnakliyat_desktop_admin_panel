using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record YoldakiArac(int SeferId, string SeferNo, string Plaka, string Sofor, string Guzergah, SeferDurum Durum, DateTime? Cikis)
{
    public string DurumAd => Durum.Ad();
}

public sealed record AylikTrend(string Donem, decimal Gelir, decimal Gider, decimal Kar);

public sealed record AracKarSatir(short AracId, string Plaka, decimal Gelir, decimal Kar, decimal? KmMaliyet);

public sealed record PanelOzeti(
    IReadOnlyList<KasaBakiyeSatir>? Kasalar,
    decimal? AcikAlacak,
    decimal? AcikBorc,
    decimal? VadesiGecenAlacak,
    int BuAySeferSayisi,
    decimal? BuAyGelir,
    decimal? BuAyGider,
    decimal? BuAyKar,
    IReadOnlyList<AylikTrend>? Trend,
    KrediGenelOzet? Kredi,
    IReadOnlyList<SoforBakiyeSatir>? SoforBakiyeleri,
    IReadOnlyList<AracKarSatir>? AracKarlari,
    int FaturalanmamisSefer,
    int OnayBekleyenHarcama,
    decimal? Odenecek30Gun,
    IReadOnlyList<YoldakiArac> Yoldakiler,
    IReadOnlyList<EvrakUyari> Evraklar,
    IReadOnlyList<YaklasanBakim> Bakimlar,
    DateTime? SonYedek);

public sealed class PanelServisi(
    IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat,
    FinansServisi finans, EvrakServisi evrak, BakimServisi bakim, RaporServisi rapor, AyarServisi ayar, KrediServisi kredi, SoforHesapServisi sofor)
{
    public async Task<PanelOzeti> OzetAsync(CancellationToken ct = default)
    {
        var finansGor = oturum.Yetkili(Izinler.FinansGor);
        var raporFinans = oturum.Yetkili(Izinler.RaporFinans);
        var bugun = saat.Bugun;
        var ayBas = new DateOnly(bugun.Year, bugun.Month, 1);

        await using var db = factory.Olustur();

        IReadOnlyList<KasaBakiyeSatir>? kasalar = null;
        decimal? acikAlacak = null, acikBorc = null, vadesiGecen = null, odenecek = null;
        if (finansGor)
        {
            kasalar = await finans.KasaListesiAsync(true, ct);
            // Kredi borçları (banka carileri) ayrı gösterilir; açık borca dahil edilmez.
            var krediBankalari = await db.Krediler.Where(k => k.Durum == Domain.Entities.KrediDurum.Aktif).Select(k => k.BankaCariId).Distinct().ToListAsync(ct);
            var bakiyeler = await db.Cariler.AsNoTracking().Where(c => (c.CariTipi & CariTipi.Personel) == 0 && !krediBankalari.Contains(c.Id))
                .Select(c => db.CariHareketleri.Where(h => h.CariId == c.Id).Sum(h => (decimal?)(h.Borc - h.Alacak)) ?? 0)
                .ToListAsync(ct);
            acikAlacak = bakiyeler.Where(b => b > 0).Sum();
            acikBorc = -bakiyeler.Where(b => b < 0).Sum();
            vadesiGecen = (await finans.YaslandirmaAsync(CariTipi.Musteri, ct))
                .Sum(y => y.Sonuc.Gun0_30 + y.Sonuc.Gun31_60 + y.Sonuc.Gun61_90 + y.Sonuc.Gun90Ustu);
            var ay = bugun.AddDays(30);
            odenecek = oturum.Yetkili(Izinler.FinansmanGor) ? (await kredi.TakvimAsync(DateOnly.MinValue, ay, false, ct)).Sum(t => t.TutarTL) : 0;
            odenecek += await db.CekSenetler.Where(c => c.Yon == CekSenetYon.Verilen && c.Durum == CekSenetDurum.CiroEdildi && c.VadeTarihi <= ay).SumAsync(c => (decimal?)c.Tutar, ct) ?? 0;
        }

        var buAySefer = await db.Seferler.CountAsync(s => s.PlanlananTarih >= ayBas && s.PlanlananTarih <= bugun && s.Durum != SeferDurum.Iptal, ct);
        decimal? gelir = null, gider = null, kar = null;
        List<AylikTrend>? trend = null;
        List<AracKarSatir>? aracKar = null;
        if (raporFinans)
        {
            var buYil = await rapor.AylikAsync(bugun.Year, ct);
            var gecenYil = await rapor.AylikAsync(bugun.Year - 1, ct);
            var son12 = gecenYil.Concat(buYil).Where(a => new DateOnly(a.Yil, a.Ay, 1) <= ayBas).TakeLast(12).ToList();
            trend = son12.Select(a => new AylikTrend($"{a.Ay:D2}.{a.Yil % 100:D2}", a.Gelir, a.Gider + a.TaseronGider, a.Kar)).ToList();
            var buAy = buYil.Single(a => a.Ay == bugun.Month);
            gelir = buAy.Gelir;
            gider = buAy.Gider + buAy.TaseronGider;
            kar = buAy.Kar;
            aracKar = (await rapor.AracKarlilikAsync(ayBas, bugun, ct))
                .Select(r => new AracKarSatir(r.AracId, r.Plaka, r.Gelir, r.Kar, r.KmBasiMaliyet)).OrderByDescending(r => r.Kar).ToList();
        }

        KrediGenelOzet? krediOzet = oturum.Yetkili(Izinler.FinansmanGor) ? await kredi.GenelOzetAsync(ct) : null;
        IReadOnlyList<SoforBakiyeSatir>? soforler = oturum.Yetkili(Izinler.SoforHesapGor)
            ? (await sofor.BakiyelerAsync(ct)).Where(s => s.Bakiye != 0).OrderByDescending(s => Math.Abs(s.Bakiye)).Take(8).ToList()
            : null;

        var faturalanmamis = await db.Seferler.CountAsync(s => (s.Durum == SeferDurum.TeslimEdildi || s.Durum == SeferDurum.Kapatildi)
            && !db.FaturaKalemleri.Any(k => k.SeferId == s.Id && k.Fatura!.Durum == FaturaDurum.Kayitli && k.Fatura.FaturaTipi == FaturaTipi.Satis), ct);
        var onay = await db.Masraflar.CountAsync(m => !m.Onayli && !m.Iptal && m.OdemeSekli == OdemeSekli.SoforNakit, ct);

        var yoldakiler = (await db.Seferler.AsNoTracking()
                .Where(s => s.Durum == SeferDurum.Yuklemede || s.Durum == SeferDurum.Yolda || s.Durum == SeferDurum.TeslimEdildi)
                .OrderBy(s => s.CikisTarihi)
                .Select(s => new
                {
                    s.Id, s.SeferNo, Plaka = s.Arac != null ? s.Arac.Plaka : s.TaseronPlaka ?? "",
                    Sofor = s.Soforler.Where(x => x.Birincil).Select(x => x.Sofor!.Ad + " " + x.Sofor.Soyad).FirstOrDefault() ?? (s.TaseronCariId != null ? "Taşeron" : ""),
                    Guzergah = s.CikisLokasyon!.Ad + " → " + s.VarisLokasyon!.Ad, s.Durum, s.CikisTarihi,
                }).ToListAsync(ct))
            .Select(s => new YoldakiArac(s.Id, s.SeferNo, s.Plaka, s.Sofor, s.Guzergah, s.Durum, s.CikisTarihi)).ToList();

        var evraklar = oturum.Yetkili(Izinler.BakimGor) || oturum.Yetkili(Izinler.FiloGor) ? await evrak.UyarilarAsync(null, ct) : [];
        var bakimlar = oturum.Yetkili(Izinler.BakimGor) ? await bakim.YaklasanlarAsync(ct: ct) : [];

        return new PanelOzeti(kasalar, acikAlacak, acikBorc, vadesiGecen, buAySefer, gelir, gider, kar, trend, krediOzet, soforler, aracKar,
            faturalanmamis, onay, odenecek, yoldakiler, evraklar, bakimlar, await ayar.SonYedekAsync(ct));
    }
}

public enum UyariSeviye : byte { Bilgi = 1, Uyari = 2, Kritik = 3 }

public enum UyariHedef : byte { Yok, Kredi, Evrak, Bakim, Sofor, Cari, Masraf, Ayarlar, Sefer }

public sealed record Uyari(UyariSeviye Seviye, string Kategori, string Metin, UyariHedef Hedef = UyariHedef.Yok, int? HedefId = null) : IRenkliSatir
{
    public string? Renk => Seviye == UyariSeviye.Kritik ? "kirmizi" : Seviye == UyariSeviye.Uyari ? "turuncu" : null;
    public string SeviyeAd => Seviye switch { UyariSeviye.Kritik => "Kritik", UyariSeviye.Uyari => "Uyarı", _ => "Bilgi" };
}

/// <summary>Uyarı merkezi: kullanıcının yetkili olduğu alanlardaki dikkat gerektiren durumlar.</summary>
public sealed class UyariServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat, KrediServisi kredi, EvrakServisi evrak,
    BakimServisi bakim, SoforHesapServisi sofor, FinansServisi finans, AyarServisi ayar)
{
    public async Task<List<Uyari>> ListeAsync(CancellationToken ct = default)
    {
        var bugun = saat.Bugun;
        var liste = new List<Uyari>();
        if (oturum.Yetkili(Izinler.FinansmanGor))
            foreach (var t in await kredi.TakvimAsync(DateOnly.MinValue, bugun.AddDays(7), false, ct))
                liste.Add(t.Gecikti
                    ? new Uyari(UyariSeviye.Kritik, "Kredi", $"{t.Banka} {t.KrediNo} #{t.No} taksiti {bugun.DayNumber - t.Vade.DayNumber} gün gecikti ({t.TutarTL:N2} TL)", UyariHedef.Kredi, t.KrediId)
                    : new Uyari(UyariSeviye.Uyari, "Kredi", $"{t.Banka} {t.KrediNo} #{t.No} taksiti {t.Vade:dd.MM} tarihinde ({t.TutarTL:N2} TL)", UyariHedef.Kredi, t.KrediId));
        if (oturum.Yetkili(Izinler.BakimGor) || oturum.Yetkili(Izinler.FiloGor))
            foreach (var e in await evrak.UyarilarAsync(null, ct))
                liste.Add(new Uyari(e.SuresiDoldu ? UyariSeviye.Kritik : UyariSeviye.Uyari, "Evrak", $"{e.Hedef}: {e.Tur} — {e.Durum}", UyariHedef.Evrak, e.EvrakId));
        if (oturum.Yetkili(Izinler.BakimGor))
            foreach (var b in await bakim.YaklasanlarAsync(ct: ct))
                liste.Add(new Uyari(b.Gecti ? UyariSeviye.Kritik : UyariSeviye.Uyari, "Bakım", $"{b.Plaka}: {b.BakimAdi} — {b.Durum}", UyariHedef.Bakim, b.PlanId));
        if (oturum.Yetkili(Izinler.SoforHesapGor))
        {
            await using var db = factory.Olustur();
            var limitler = await db.Soforler.AsNoTracking().Where(s => s.BakiyeLimiti > 0).ToDictionaryAsync(s => s.Id, s => s.BakiyeLimiti!.Value, ct);
            foreach (var s in await sofor.BakiyelerAsync(ct))
                if (limitler.TryGetValue(s.SoforId, out var l) && s.Bakiye > l)
                    liste.Add(new Uyari(UyariSeviye.Uyari, "Şoför", $"{s.AdSoyad}: bakiye {s.Bakiye:N2} TL, limit {l:N2} TL", UyariHedef.Sofor, s.SoforId));
            var esik = bugun.AddDays(-30);
            foreach (var a in await db.Avanslar.AsNoTracking().Where(a => a.MahsupId == null && !a.Iptal && a.Tur != Domain.Entities.AvansTuru.MaasOdemesi && a.Tarih < esik)
                         .GroupBy(a => new { a.SoforId, Ad = a.Sofor!.Ad + " " + a.Sofor.Soyad }).Select(g => new { g.Key.SoforId, g.Key.Ad, Adet = g.Count(), T = g.Sum(a => a.Tutar) }).ToListAsync(ct))
                liste.Add(new Uyari(UyariSeviye.Uyari, "Şoför", $"{a.Ad}: 30 günden eski {a.Adet} açık avans ({a.T:N2})", UyariHedef.Sofor, a.SoforId));
            var onay = await db.Masraflar.CountAsync(m => !m.Onayli && !m.Iptal && m.OdemeSekli == OdemeSekli.SoforNakit, ct);
            if (onay > 0) liste.Add(new Uyari(UyariSeviye.Bilgi, "Şoför", $"{onay} şoför harcaması onay bekliyor", UyariHedef.Masraf));
        }
        if (oturum.Yetkili(Izinler.FinansGor))
            foreach (var y in (await finans.YaslandirmaAsync(CariTipi.Musteri, ct)).Where(y => y.Sonuc.Gun31_60 + y.Sonuc.Gun61_90 + y.Sonuc.Gun90Ustu > 0))
                liste.Add(new Uyari(y.Sonuc.Gun90Ustu > 0 ? UyariSeviye.Kritik : UyariSeviye.Uyari, "Alacak",
                    $"{y.Unvan}: 30 günü geçen alacak {y.Sonuc.Gun31_60 + y.Sonuc.Gun61_90 + y.Sonuc.Gun90Ustu:N2} TL", UyariHedef.Cari, y.CariId));
        var yedek = await ayar.SonYedekAsync(ct);
        if (yedek is null || DateTime.UtcNow - yedek > TimeSpan.FromHours(26))
            liste.Add(new Uyari(UyariSeviye.Kritik, "Yedek", yedek is null ? "Başarılı otomatik yedek kaydı yok" : $"Son yedek {yedek.Value.ToLocalTime():dd.MM.yyyy HH:mm} — 24 saatten eski", UyariHedef.Ayarlar));
        return liste.OrderByDescending(u => u.Seviye).ThenBy(u => u.Kategori).ToList();
    }
}
