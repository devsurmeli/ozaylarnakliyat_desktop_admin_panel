using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record YoldakiArac(int SeferId, string SeferNo, string Plaka, string Sofor, string Guzergah, SeferDurum Durum, DateTime? Cikis)
{
    public string DurumAd => Ortak.EnumMetin.Ad(Durum);
}

public sealed record PanelOzeti(
    IReadOnlyList<KasaBakiyeSatir>? Kasalar,
    decimal? AcikAlacak,
    decimal? AcikBorc,
    decimal? VadesiGecenAlacak,
    int BuAySeferSayisi,
    decimal? BuAyCiro,
    decimal? BuAyKar,
    int FaturalanmamisSefer,
    int OnayBekleyenHarcama,
    decimal? Odenecek30Gun,
    IReadOnlyList<YoldakiArac> Yoldakiler,
    IReadOnlyList<EvrakUyari> Evraklar,
    IReadOnlyList<YaklasanBakim> Bakimlar,
    DateTime? SonYedek);

public sealed class PanelServisi(
    IFiloDbContextFactory factory, IKullaniciOturumu oturum, ISaat saat,
    FinansServisi finans, EvrakServisi evrak, BakimServisi bakim, RaporServisi rapor, AyarServisi ayar)
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
            var bakiyeler = await db.Cariler.AsNoTracking().Where(c => (c.CariTipi & CariTipi.Personel) == 0)
                .Select(c => db.CariHareketleri.Where(h => h.CariId == c.Id).Sum(h => (decimal?)(h.Borc - h.Alacak)) ?? 0)
                .ToListAsync(ct);
            acikAlacak = bakiyeler.Where(b => b > 0).Sum();
            acikBorc = -bakiyeler.Where(b => b < 0).Sum();
            vadesiGecen = (await finans.YaslandirmaAsync(CariTipi.Musteri, ct))
                .Sum(y => y.Sonuc.Gun0_30 + y.Sonuc.Gun31_60 + y.Sonuc.Gun61_90 + y.Sonuc.Gun90Ustu);
            var ay = bugun.AddDays(30);
            odenecek = await db.FinansmanTaksitleri.Where(t => t.OdemeTarihi == null && t.VadeTarihi <= ay && t.AracFinansman!.Aktif).SumAsync(t => (decimal?)t.Toplam, ct) ?? 0;
            odenecek += await db.CekSenetler.Where(c => c.Yon == CekSenetYon.Verilen && c.Durum == CekSenetDurum.CiroEdildi && c.VadeTarihi <= ay).SumAsync(c => (decimal?)c.Tutar, ct) ?? 0;
        }

        var buAySefer = await db.Seferler.CountAsync(s => s.PlanlananTarih >= ayBas && s.PlanlananTarih <= bugun && s.Durum != SeferDurum.Iptal, ct);
        decimal? ciro = null, kar = null;
        if (raporFinans)
        {
            var karlar = await rapor.SeferKarlilikAsync(ayBas, bugun, ct: ct);
            ciro = karlar.Sum(k => k.Gelir);
            kar = karlar.Sum(k => k.Kar);
        }

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

        return new PanelOzeti(kasalar, acikAlacak, acikBorc, vadesiGecen, buAySefer, ciro, kar, faturalanmamis, onay, odenecek,
            yoldakiler, evraklar, bakimlar, await ayar.SonYedekAsync(ct));
    }
}
