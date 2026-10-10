using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ozaylar.Filo.Application;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Infrastructure;
using Ozaylar.Filo.Infrastructure.Veri;

namespace Ozaylar.Filo.Sunucu;

/// <summary>
/// Programı denemek için örnek veri (servis katmanı üzerinden: tüm iş kuralları çalışır).
/// Yalnızca hiç sefer olmayan veritabanında çalışır; gerçek veritabanında KULLANMAYIN.
/// </summary>
public static class OrnekVeri
{
    public static async Task OlusturAsync(BaglantiBilgisi b)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddFiloApplication();
        s.AddFiloInfrastructure(b);
        await using var sp = s.BuildServiceProvider();
        sp.GetRequiredService<IKullaniciOturumu>().Baslat(1, "Örnek Veri", Izinler.Tumu.Select(i => i.Kod));
        var f = sp.GetRequiredService<IFiloDbContextFactory>();
        await using (var db = f.Olustur())
            if (await db.Seferler.IgnoreQueryFilters().AnyAsync())
                throw new InvalidOperationException("Veritabanında sefer var; örnek veri yalnızca boş veritabanına yüklenir.");

        var bugun = DateOnly.FromDateTime(DateTime.Today);
        var cari = sp.GetRequiredService<CariServisi>();
        async Task<Cari> C(string unvan, CariTipi tip, string? vkn = null, short vade = 30, decimal? risk = null, short? ulke = null) =>
            await cari.KaydetAsync(new Cari { Unvan = unvan, CariTipi = tip, VergiNo = vkn, VadeGun = vade, RiskLimiti = risk, UlkeId = ulke, Telefon = "0212 555 00 00" });

        var mAnadolu = await C("Anadolu Gıda San. ve Tic. A.Ş.", CariTipi.Musteri, null, 45, 750_000);
        var mEge = await C("Ege Tekstil Ltd. Şti.", CariTipi.Musteri, null, 30);
        var mKoln = await C("Rheinland Logistik GmbH", CariTipi.Musteri, null, 60, null, 2);
        var opet = await C("Opet Petrolcülük A.Ş.", CariTipi.Tedarikci, null, 15);
        var servisCari = await C("Usta Kamyon Servis", CariTipi.Tedarikci);
        var taseron = await C("Yıldız Nakliyat (Taşeron)", CariTipi.Taseron | CariTipi.Tedarikci);
        var leasing = await C("Garanti BBVA Filo Leasing", CariTipi.Finans);
        var hgsCari = await C("PTT HGS", CariTipi.Tedarikci);

        await using (var db = f.Olustur())
        {
            db.KasaHesaplari.Add(new KasaHesap { Ad = "Ziraat Bankası TL", HesapTipi = KasaHesapTipi.Banka, BankaAdi = "Ziraat", AcilisBakiye = 3_500_000, AcilisTarihi = bugun.AddMonths(-3) });
            db.KasaHesaplari.Add(new KasaHesap { Ad = "İş Bankası EUR", HesapTipi = KasaHesapTipi.Banka, BankaAdi = "İş Bankası", ParaBirimi = ParaBirimi.EUR, AcilisBakiye = 12_000, AcilisTarihi = bugun.AddMonths(-3) });
            var merkez = await db.KasaHesaplari.FirstAsync(k => k.Ad == "Merkez Kasa");
            merkez.AcilisBakiye = 150_000;
            merkez.AcilisTarihi = bugun.AddMonths(-3);
            foreach (var (ad, ulke, il) in new (string, short, byte?)[]
                     { ("Gebze Lojistik Merkezi", 1, 41), ("İzmir Kemalpaşa Depo", 1, 35), ("Ankara OSB", 1, 6), ("Mersin Limanı", 1, 33), ("Bursa NOSAB", 1, 16), ("Köln Eifeltor Terminal", 2, null) })
                db.Lokasyonlar.Add(new Lokasyon { Ad = ad, UlkeId = ulke, IlId = il });
            db.HarcirahTarifeleri.Add(new HarcirahTarife { UlkeId = 2, GunlukTutar = 45, ParaBirimi = ParaBirimi.EUR, GecerlilikBaslangic = new DateOnly(2026, 1, 1), Aciklama = "Almanya" });
            var yurtici = await db.HarcirahTarifeleri.FirstAsync(h => h.UlkeId == null);
            yurtici.GunlukTutar = 900;
            await db.SaveChangesAsync();
        }

        Dictionary<string, int> lok;
        byte kasaTl, kasaZiraat, kasaEur;
        await using (var db = f.Olustur())
        {
            lok = await db.Lokasyonlar.ToDictionaryAsync(l => l.Ad.Split(' ')[0], l => l.Id);
            kasaTl = await db.KasaHesaplari.Where(k => k.Ad == "Merkez Kasa").Select(k => k.Id).FirstAsync();
            kasaZiraat = await db.KasaHesaplari.Where(k => k.Ad == "Ziraat Bankası TL").Select(k => k.Id).FirstAsync();
            kasaEur = await db.KasaHesaplari.Where(k => k.Ad == "İş Bankası EUR").Select(k => k.Id).FirstAsync();
        }

        var aracS = sp.GetRequiredService<AracServisi>();
        var araclar = new List<Arac>();
        foreach (var (plaka, marka, model, yil) in new[] { ("34 OZY 101", "Mercedes", "Actros 1845", 2021), ("34 OZY 102", "Mercedes", "Actros 1845", 2021), ("34 OZY 103", "Volvo", "FH 500", 2022), ("34 OZY 104", "Scania", "R450", 2020), ("34 OZY 105", "Ford", "F-Max", 2023) })
            araclar.Add(await aracS.KaydetAsync(new Arac
            {
                Plaka = plaka, Marka = marka, Model = model, ModelYili = (short)yil, GuncelKm = 250_000 - (yil - 2020) * 40_000, BeklenenTuketim = 31, YakitDepoLitre = 700,
                AlisTarihi = new DateOnly(yil, 3, 1), AlisBedeli = 3_800_000 + (yil - 2020) * 400_000, FaydaliOmurAy = 120, HurdaDegeri = 800_000,
                Mulkiyet = plaka.EndsWith("105") ? Mulkiyet.Leasing : Mulkiyet.Ozmal,
            }));
        var dorseS = sp.GetRequiredService<DorseServisi>();
        for (var i = 0; i < araclar.Count; i++)
        {
            var d = await dorseS.KaydetAsync(new Dorse { Plaka = $"34 DRS {201 + i}", DorseTipi = i == 2 ? DorseTipi.Frigo : DorseTipi.Tenteli, Marka = "Tırsan", KapasiteTon = 26 });
            await aracS.DorseBaglaAsync(araclar[i].Id, d.Id, bugun.AddMonths(-3));
        }
        var soforS = sp.GetRequiredService<SoforServisi>();
        var soforler = new List<Sofor>();
        string[] adlar = ["Mehmet Yılmaz", "Ahmet Kaya", "Hasan Demir", "Mustafa Çelik", "İbrahim Şahin", "Osman Öztürk"];
        for (var i = 0; i < adlar.Length; i++)
            soforler.Add(await soforS.KaydetAsync(new Sofor
            {
                TCKN = TcUret(100_000_100 + i * 7), Ad = adlar[i].Split(' ')[0], Soyad = adlar[i].Split(' ')[1], Telefon = $"0532 000 00 0{i}",
                EhliyetSinifi = "CE", IseGiris = bugun.AddYears(-2), VarsayilanAracId = i < araclar.Count ? araclar[i].Id : null,
            }));

        // Evraklar: biri süresi dolmak üzere, biri dolmuş (sefer engellemeyen tür).
        await using (var db = f.Olustur())
        {
            var turler = await db.EvrakTurleri.ToDictionaryAsync(t => t.Ad, t => t.Id);
            foreach (var a in araclar)
            {
                db.Evraklar.Add(new Evrak { EvrakTuruId = turler["Araç Muayenesi"], AracId = a.Id, DuzenlenmeTarihi = bugun.AddMonths(-10), BitisTarihi = a.Plaka.EndsWith("103") ? bugun.AddDays(12) : bugun.AddMonths(2) });
                db.Evraklar.Add(new Evrak { EvrakTuruId = turler["Trafik Sigortası"], AracId = a.Id, Kurum = "Anadolu Sigorta", DuzenlenmeTarihi = bugun.AddMonths(-6), BitisTarihi = bugun.AddMonths(6) });
                db.Evraklar.Add(new Evrak { EvrakTuruId = turler["Kasko"], AracId = a.Id, Kurum = "Allianz", DuzenlenmeTarihi = bugun.AddMonths(-11).AddDays(-20), BitisTarihi = a.Plaka.EndsWith("104") ? bugun.AddDays(-3) : bugun.AddMonths(1) });
            }
            foreach (var so in soforler)
            {
                db.Evraklar.Add(new Evrak { EvrakTuruId = turler["SRC Belgesi"], SoforId = so.Id, BitisTarihi = bugun.AddYears(3) });
                db.Evraklar.Add(new Evrak { EvrakTuruId = turler["Psikoteknik"], SoforId = so.Id, BitisTarihi = so.Ad == "Hasan" ? bugun.AddDays(20) : bugun.AddYears(2) });
            }
            db.BakimPlanlari.Add(new BakimPlani { AracId = araclar[0].Id, BakimAdi = "Motor yağı + filtreler", PeriyotKm = 30_000, SonKm = araclar[0].GuncelKm - 28_500, SonTarih = bugun.AddMonths(-4) });
            db.BakimPlanlari.Add(new BakimPlani { AracId = araclar[2].Id, BakimAdi = "Fren balata kontrolü", PeriyotGun = 90, SonTarih = bugun.AddDays(-85) });
            await db.SaveChangesAsync();
        }

        // Seferler
        var seferS = sp.GetRequiredService<SeferServisi>();
        var gider = sp.GetRequiredService<GiderServisi>();
        var soforH = sp.GetRequiredService<SoforHesapServisi>();
        var rnd = new Random(42);
        var rotalar = new (string Cikis, string Varis, int Km, decimal Navlun, int Gun)[]
        {
            ("Gebze", "İzmir", 1_050, 48_000, 2), ("Gebze", "Ankara", 900, 42_000, 2), ("Mersin", "Gebze", 1_900, 78_000, 3), ("Bursa", "Ankara", 800, 38_000, 2),
        };
        var acikSeferler = new List<Sefer>();
        for (var i = 0; i < 16; i++)
        {
            var arac = araclar[i % 4];
            var sofor = soforler[i % 4];
            var r = rotalar[i % rotalar.Length];
            var tarih = bugun.AddDays(-58 + (int)(i * 3.5));
            var musteri = i % 3 == 0 ? mEge : mAnadolu;
            var sefer = await seferS.KaydetAsync(new Sefer
            {
                MusteriCariId = musteri.Id, AracId = arac.Id, CikisLokasyonId = lok[r.Cikis], VarisLokasyonId = lok[r.Varis], PlanlananTarih = tarih,
                YukTanimi = musteri == mEge ? "Tekstil ürünü (palet)" : "Gıda (kuru yük)", Tonaj = 22, NavlunTutar = r.Navlun + rnd.Next(-3, 4) * 1_000,
            }, [sofor.Id], [], uyarilariOnayla: true);
            if (i >= 14) { acikSeferler.Add(sefer); continue; } // son iki sefer planlı kalsın

            var kayit = (await seferS.GetirAsync(sefer.Id))!;
            var cikisKm = (await AracKmAsync(f, arac.Id)) + 15;
            kayit.CikisKm = cikisKm;
            kayit.CikisTarihi = tarih.ToDateTime(new TimeOnly(6, 30));
            await seferS.KaydetAsync(kayit, [sofor.Id], [], true);
            await seferS.DurumDegistirAsync(sefer.Id, SeferDurum.Yolda);

            await soforH.AvansVerAsync(new Avans { SoforId = sofor.Id, SeferId = sefer.Id, Tarih = tarih, Tutar = 8_000, KasaHesapId = kasaTl });
            var yz = tarih.ToDateTime(new TimeOnly(7, 0));
            await gider.YakitKaydetAsync(new Yakit { AracId = arac.Id, SeferId = sefer.Id, AlimZamani = yz, Km = cikisKm + 5, Litre = 420, BirimFiyat = 52.4m, FullDepo = true, OdemeSekli = OdemeSekli.Kart, IstasyonCariId = opet.Id, IstasyonAdi = "Opet" }, true);
            var tuketim = 29 + rnd.Next(0, 6);
            await gider.YakitKaydetAsync(new Yakit { AracId = arac.Id, SeferId = sefer.Id, AlimZamani = yz.AddDays(r.Gun - 1).AddHours(5), Km = cikisKm + r.Km, Litre = Math.Round(r.Km * tuketim / 100m, 0), BirimFiyat = 52.9m, FullDepo = true, OdemeSekli = OdemeSekli.Kart, IstasyonCariId = opet.Id, IstasyonAdi = "Opet" }, true);
            await gider.MasrafKaydetAsync(new Masraf { Tarih = tarih, MasrafTuruId = 3, Merkez = MasrafMerkezi.Sefer, SeferId = sefer.Id, Tutar = 1_850, OdemeSekli = OdemeSekli.SoforNakit, Aciklama = "Otoyol + köprü" });
            await gider.MasrafKaydetAsync(new Masraf { Tarih = tarih.AddDays(1), MasrafTuruId = 5, Merkez = MasrafMerkezi.Sefer, SeferId = sefer.Id, Tutar = 900, OdemeSekli = OdemeSekli.SoforNakit, Aciklama = "Yemek" });

            kayit = (await seferS.GetirAsync(sefer.Id))!;
            kayit.DonusKm = cikisKm + r.Km + 20;
            kayit.BosKm = i % 2 == 0 ? r.Km / 2 : 0;
            kayit.TeslimTarihi = tarih.AddDays(r.Gun - 1).ToDateTime(new TimeOnly(14, 0));
            kayit.DonusTarihi = tarih.AddDays(r.Gun).ToDateTime(new TimeOnly(18, 0));
            await seferS.KaydetAsync(kayit, [sofor.Id], [], true);
            if (i >= 12) continue; // iki sefer yolda kalsın
            await seferS.DurumDegistirAsync(sefer.Id, SeferDurum.TeslimEdildi);
            await seferS.DurumDegistirAsync(sefer.Id, SeferDurum.Kapatildi);

            var h = await soforH.SeferdenHarcirahOnerAsync(sefer.Id, sofor.Id);
            await soforH.HarcirahKaydetAsync(h!);
            var acik = await soforH.AcikKalemlerAsync(sofor.Id, sefer.Id);
            var fark = SoforHesapServisi.Onizle(acik).Fark;
            await soforH.MahsupYapAsync(sofor.Id, sefer.Id, kayit.TeslimTarihi is { } t ? DateOnly.FromDateTime(t).AddDays(1) : tarih, acik.Select(k => (k.Tur, k.Id)).ToList(),
                fark > 0 ? MahsupSonuc.IadeAlindi : fark < 0 ? MahsupSonuc.SoforeOdendi : MahsupSonuc.FarkYok, kasaTl, null);
        }

        // Yurtdışı sefer (EUR navlun) ve taşeron seferi
        var eurKur = 48.1m;
        var yd = await seferS.KaydetAsync(new Sefer
        {
            Kapsam = SeferKapsam.Yurtdisi, MusteriCariId = mKoln.Id, AracId = araclar[4].Id, CikisLokasyonId = lok["Gebze"], VarisLokasyonId = lok["Köln"],
            PlanlananTarih = bugun.AddDays(-20), NavlunTutar = 3_900, NavlunParaBirimi = ParaBirimi.EUR, NavlunKur = eurKur, NavlunKdvOrani = 0, NavlunTevkifatPay = 0,
            CmrNo = "TR-2026-0815", YukTanimi = "Beyaz eşya", CikisKm = await AracKmAsync(f, araclar[4].Id) + 10, CikisTarihi = bugun.AddDays(-20).ToDateTime(new TimeOnly(8, 0)),
        }, [soforler[4].Id, soforler[5].Id], [], true);
        await seferS.DurumDegistirAsync(yd.Id, SeferDurum.Yolda);
        await soforH.AvansVerAsync(new Avans { SoforId = soforler[4].Id, SeferId = yd.Id, Tarih = bugun.AddDays(-20), Tutar = 600, KasaHesapId = kasaEur, Kur = eurKur });

        var ts = await seferS.KaydetAsync(new Sefer
        {
            MusteriCariId = mAnadolu.Id, TaseronCariId = taseron.Id, TaseronPlaka = "06 YLD 77", TaseronBedel = 36_000, CikisLokasyonId = lok["Ankara"], VarisLokasyonId = lok["Mersin"],
            PlanlananTarih = bugun.AddDays(-10), NavlunTutar = 44_000, YukTanimi = "Konserve",
        }, [], [], true);
        await seferS.DurumDegistirAsync(ts.Id, SeferDurum.Yolda);
        await seferS.DurumDegistirAsync(ts.Id, SeferDurum.TeslimEdildi);
        await seferS.DurumDegistirAsync(ts.Id, SeferDurum.Kapatildi);

        // Faturalar ve tahsilat
        var fatura = sp.GetRequiredService<FaturaServisi>();
        var finans = sp.GetRequiredService<FinansServisi>();
        var no = 1;
        foreach (var grp in (await fatura.FaturalanmamisSeferlerAsync()).Where(x => x.ParaBirimi == ParaBirimi.TRY).GroupBy(x => x.Musteri))
        {
            var ilk = grp.OrderBy(x => x.Tarih).Take(grp.Count() - 1).ToList(); // her müşteride bir sefer faturasız kalsın
            if (ilk.Count == 0) continue;
            var cariId = (await seferS.GetirAsync(ilk[0].SeferId))!.MusteriCariId;
            var fa = await fatura.KaydetAsync(new Fatura
            {
                FaturaNo = $"OZY2026{no++:D6}", Tarih = ilk.Max(x => x.Tarih).AddDays(2), VadeTarihi = ilk.Max(x => x.Tarih).AddDays(32), CariId = cariId,
                Kalemler = await fatura.SeferKalemleriAsync(ilk.Select(x => x.SeferId)),
            });
            await finans.TahsilatAsync(new TahsilatOdeme(cariId, kasaZiraat, fa.Tarih.AddDays(15), Math.Round(fa.GenelToplam * 0.6m, 0), $"{fa.FaturaNo} kısmi tahsilat"));
        }
        await finans.OdemeAsync(new TahsilatOdeme(opet.Id, kasaZiraat, bugun.AddDays(-5), 150_000, "Akaryakıt ödemesi"));

        // Bakım, ceza, leasing
        await sp.GetRequiredService<BakimServisi>().KaydetAsync(new Bakim
        {
            AracId = araclar[3].Id, BakimTuru = BakimTuru.Ariza, Tarih = bugun.AddDays(-15), Km = await AracKmAsync(f, araclar[3].Id), ServisCariId = servisCari.Id, OdemeSekli = OdemeSekli.Cari,
            Aciklama = "Debriyaj seti değişimi",
            Kalemler = [new BakimKalem { Aciklama = "Debriyaj seti", Miktar = 1, BirimFiyat = 38_000 }, new BakimKalem { Aciklama = "İşçilik", KalemTipi = BakimKalemTipi.Iscilik, Miktar = 1, BirimFiyat = 7_500 }],
        });
        await gider.CezaKaydetAsync(new Ceza { Tarih = bugun.AddDays(-25), AracId = araclar[1].Id, SoforId = soforler[1].Id, CezaTuru = "Hız ihlali (%10-30)", Tutar = 2_167, RucuEdilsin = true });
        await gider.MasrafKaydetAsync(new Masraf { Tarih = bugun.AddMonths(-1), MasrafTuruId = 13, Merkez = MasrafMerkezi.Arac, AracId = araclar[0].Id, Tutar = 24_500, OdemeSekli = OdemeSekli.Kasa, KasaHesapId = kasaZiraat, Aciklama = "Trafik sigortası yenileme" });
        var fin = sp.GetRequiredService<FinansmanServisi>();
        var fs = new AracFinansman { AracId = araclar[4].Id, Tip = FinansmanTipi.Leasing, FinansKurumuCariId = leasing.Id, SozlesmeNo = "FL-2025-3310", BaslangicTarihi = new DateOnly(2025, 6, 1), AnaparaTutar = 3_200_000, AylikFaizOrani = 2.4m, VadeSayisi = 48 };
        await fin.KaydetAsync(fs, FinansmanServisi.PlanOner(fs, new DateOnly(2025, 7, 1)));
        foreach (var t in (await fin.TaksitlerAsync(new DateOnly(2025, 7, 1), bugun.AddMonths(-1), true)).OrderBy(t => t.Vade))
            await fin.TaksitOdeAsync(t.Id, kasaZiraat, t.Vade);
        _ = hgsCari;
    }

    private static async Task<int> AracKmAsync(IFiloDbContextFactory f, short aracId)
    {
        await using var db = f.Olustur();
        return await db.Araclar.Where(a => a.Id == aracId).Select(a => a.GuncelKm).SingleAsync();
    }

    private static string TcUret(int ilk9)
    {
        var d = ilk9.ToString().Select(c => c - '0').ToArray();
        var d10 = (((d[0] + d[2] + d[4] + d[6] + d[8]) * 7 - (d[1] + d[3] + d[5] + d[7])) % 10 + 10) % 10;
        var d11 = (d.Sum() + d10) % 10;
        return $"{ilk9}{d10}{d11}";
    }
}
