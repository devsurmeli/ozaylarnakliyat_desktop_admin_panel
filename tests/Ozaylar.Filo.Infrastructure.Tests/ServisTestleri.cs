using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Infrastructure.Disa;
using Ozaylar.Filo.Infrastructure.Guvenlik;

namespace Ozaylar.Filo.Infrastructure.Tests;

[Collection(VeritabaniKoleksiyonu.Ad)]
public class ServisTestleri(VeritabaniFixture fx)
{
    private static readonly DateOnly Bugun = new(2026, 7, 10);
    private readonly SabitSaat saat = new(Bugun);
    private TestVerisi T => new(fx, saat);

    [Fact]
    public async Task Giris_bes_hatali_denemede_kilitlenir_dogru_sifrede_izinler_yuklenir()
    {
        var oturum = new KullaniciOturumu();
        var hasher = new SifreHasher();
        await using (var db = fx.Db())
        {
            var k = new Kullanici { KullaniciAdi = $"kilit{TestVerisi.Sira()}", AdSoyad = "Kilit Test", SifreHash = hasher.Hashle("Dogru123") };
            k.Roller.Add(new KullaniciRol { RolId = 3 });
            db.Kullanicilar.Add(k);
            await db.SaveChangesAsync();
            var servis = new OturumServisi(T.Factory, oturum, hasher, saat);
            for (var i = 0; i < 5; i++) Assert.False((await servis.GirisAsync(k.KullaniciAdi, "yanlis")).Basarili);
            var kilitli = await servis.GirisAsync(k.KullaniciAdi, "Dogru123");
            Assert.False(kilitli.Basarili);
            Assert.Contains("kilitli", kilitli.Mesaj);
        }
        await using (var db = fx.Db())
        {
            var k = await db.Kullanicilar.OrderByDescending(x => x.Id).FirstAsync(x => x.AdSoyad == "Kilit Test");
            k.KilitBitis = null;
            await db.SaveChangesAsync();
            var servis = new OturumServisi(T.Factory, oturum, hasher, saat);
            // Türkçe collation: "kilit" büyük harfle "KİLİT" (noktalı İ) olarak eşleşir.
            Assert.True((await servis.GirisAsync(k.KullaniciAdi.ToUpper(new System.Globalization.CultureInfo("tr-TR")), "Dogru123")).Basarili);
            Assert.True(oturum.Yetkili(Izinler.SeferYaz));
            Assert.False(oturum.Yetkili(Izinler.SeferFiyatGor));
        }
    }

    [Fact]
    public async Task Akaryakit_ekstresi_onizlenir_aktarilir_ve_tekrar_aktarilmaz()
    {
        var arac = await T.AracAsync();
        var opet = await T.CariAsync(CariTipi.Tedarikci);
        using var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Ekstre");
            string[] b = ["Plaka", "İşlem Tarihi", "Miktar", "Birim Fiyat", "Tutar", "Km", "İstasyon", "Kart No", "Fiş No"];
            for (var i = 0; i < b.Length; i++) ws.Cell(1, i + 1).Value = b[i];
            ws.Cell(2, 1).Value = arac.Plaka.Replace(" ", "").ToLower();
            ws.Cell(2, 2).Value = new DateTime(2026, 7, 1, 8, 30, 0);
            ws.Cell(2, 3).Value = 350.5;
            ws.Cell(2, 4).Value = "48,250";
            ws.Cell(2, 5).Value = "16.911,63";
            ws.Cell(2, 6).Value = 120_500;
            ws.Cell(2, 7).Value = "Opet Gebze";
            ws.Cell(3, 1).Value = "99 YOK 999";
            ws.Cell(3, 2).Value = "02.07.2026 10:00";
            ws.Cell(3, 3).Value = 100;
            ws.Cell(3, 5).Value = 4800;
            wb.SaveAs(ms);
        }
        var servis = new IceAktarmaServisi(T.Factory, fx.Oturum, new ExcelOkuyucu());
        ms.Position = 0;
        var on = await servis.OnizleAsync(ms, "ekstre.xlsx", 1);
        Assert.Equal(2, on.Satirlar.Count);
        Assert.Equal(1, on.Hazir);
        Assert.Contains("Araç bulunamadı", on.Satirlar[1].Durum);
        Assert.Equal(16_911.63m, on.ToplamTutar);

        await servis.AktarAsync(on, opet.Id);
        Assert.Equal(-16_911.63m, await T.CariBakiyeAsync(opet.Id));
        await using (var db = fx.Db())
        {
            var y = await db.Yakitlar.SingleAsync(x => x.AracId == arac.Id);
            Assert.Equal(350.5m, y.Litre);
            Assert.Equal(48.25m, y.BirimFiyat);
            Assert.Equal(120_500, (await db.Araclar.SingleAsync(a => a.Id == arac.Id)).GuncelKm);
        }

        ms.Position = 0;
        var tekrar = await servis.OnizleAsync(ms, "ekstre.xlsx", 1);
        Assert.True(tekrar.Satirlar[0].Mukerrer);
        Assert.Equal(0, tekrar.Hazir);
    }

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("₺ 2.500", 2500)]
    [InlineData("350,5", 350.5)]
    public void Sayi_cozumleme_farkli_bicimleri_okur(string girdi, double beklenen) =>
        Assert.Equal((decimal)beklenen, IceAktarmaServisi.SayiCoz(girdi));

    [Fact]
    public async Task Alinan_cek_cariyi_kapatir_tahsil_edilince_bankaya_girer_karsiliksizda_geri_borclanir()
    {
        var musteri = await T.CariAsync();
        var banka = await T.KasaAsync(0);
        var finans = T.Finans;
        await finans.CariManuelKayitAsync(musteri.Id, Bugun, 50_000, 0, "Açılış", acilis: true);
        var servis = new CekSenetServisi(T.Factory, fx.Oturum, saat);
        var c1 = await servis.KaydetAsync(new CekSenet { Yon = CekSenetYon.Alinan, CariId = musteri.Id, Tutar = 30_000, IslemTarihi = Bugun, VadeTarihi = Bugun.AddDays(60), SeriNo = "A1" });
        var c2 = await servis.KaydetAsync(new CekSenet { Yon = CekSenetYon.Alinan, CariId = musteri.Id, Tutar = 20_000, IslemTarihi = Bugun, VadeTarihi = Bugun.AddDays(30), SeriNo = "A2" });
        Assert.Equal(0, await T.CariBakiyeAsync(musteri.Id));
        Assert.StartsWith("A", c1.PortfoyNo);

        await servis.DurumDegistirAsync(c1.Id, CekSenetDurum.TahsilEdildi, Bugun.AddDays(60), banka, null, null);
        Assert.Equal(30_000, await T.KasaBakiyeAsync(banka));
        await servis.DurumDegistirAsync(c2.Id, CekSenetDurum.Karsiliksiz, Bugun.AddDays(30), null, null, null);
        Assert.Equal(20_000, await T.CariBakiyeAsync(musteri.Id));
        await Assert.ThrowsAsync<DomainException>(() => servis.DurumDegistirAsync(c2.Id, CekSenetDurum.TahsilEdildi, Bugun, banka, null, null));
    }

    private KrediServisi Kredi => new(T.Factory, fx.Oturum, saat, new DovizServisi(T.Factory));

    [Fact]
    public async Task Kredi_kullandirimi_kasaya_ve_bankaya_yazilir_taksit_anapara_faiz_ayrilir_ve_raporda_faiz_gorunur()
    {
        var arac = await T.AracAsync();
        var banka = await T.CariAsync(CariTipi.Finans);
        var kasa = await T.KasaAsync(1_000_000);
        var k = await Kredi.KaydetAsync(new Kredi
        {
            KrediNo = $"TK-{TestVerisi.Sira()}", BankaCariId = banka.Id, KullandirimTarihi = Bugun, KullandirimKasaHesapId = kasa, Anapara = 600_000,
            FaizOrani = 2.5m, BsmvOrani = 5, VadeSayisi = 12, IlkVade = new DateOnly(2026, 8, 10), DosyaMasrafi = 1_000,
        }, null, [new KrediArac { AracId = arac.Id, PayYuzde = 100 }]);

        // Kullandırım: kasaya net tutar girer, banka carisi anapara kadar alacaklı
        Assert.Equal(1_599_000m, await T.KasaBakiyeAsync(kasa));
        Assert.Equal(-600_000m, await T.CariBakiyeAsync(banka.Id));

        var d = await Kredi.DetayAsync(k.Id);
        Assert.Equal(12, d.Plan.Count);
        var ilk = d.Plan[0];
        Assert.Equal((15_000m, 750m), (ilk.Faiz, ilk.Bsmv));

        await Kredi.TaksitOdeAsync(ilk.Id, ilk.Vade, kasa, null, null);
        Assert.Equal(1_599_000m - ilk.Toplam, await T.KasaBakiyeAsync(kasa));
        // Banka carisinde net etki yalnızca anapara azalışı
        Assert.Equal(-600_000m + ilk.Anapara, await T.CariBakiyeAsync(banka.Id));
        d = await Kredi.DetayAsync(k.Id);
        Assert.Equal(600_000m - ilk.Anapara, d.Ozet.KalanAnapara);
        Assert.Equal(15_750m, d.Ozet.OdenenFaiz);
        Assert.Equal(1, d.Ozet.OdenenTaksit);
        await Assert.ThrowsAsync<DomainException>(() => Kredi.TaksitOdeAsync(ilk.Id, ilk.Vade, kasa, null, null));

        var rapor = new RaporServisi(T.Factory, fx.Oturum, saat, Kredi);
        var agustos = (await rapor.AracKarlilikAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))).Single(r => r.AracId == arac.Id);
        Assert.Equal(15_750m, agustos.FinansmanFaizi);
        var temmuz = (await rapor.AracKarlilikAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31))).Single(r => r.AracId == arac.Id);
        Assert.Equal(1_000m, temmuz.FinansmanFaizi); // dosya masrafı kullandırım ayında

        // Son ödeme iptal edilebilir: kasa ve banka eski haline döner
        await Kredi.OdemeIptalAsync(d.Odemeler.Single().Id, "test");
        Assert.Equal(1_599_000m, await T.KasaBakiyeAsync(kasa));
        Assert.Equal(-600_000m, await T.CariBakiyeAsync(banka.Id));
        Assert.Equal(600_000m, (await Kredi.DetayAsync(k.Id)).Ozet.KalanAnapara);
    }

    [Fact]
    public async Task Dovizli_kredi_tl_hesaptan_odeme_gunu_kuruyla_odenir()
    {
        var banka = await T.CariAsync(CariTipi.Finans);
        var kasa = await T.KasaAsync(2_000_000);
        var k = await Kredi.KaydetAsync(new Kredi
        {
            KrediNo = $"EUR-{TestVerisi.Sira()}", Tur = KrediTuru.DovizeEndeksli, BankaCariId = banka.Id, ParaBirimi = ParaBirimi.EUR, KullandirimTarihi = Bugun,
            KullandirimKur = 40m, Anapara = 10_000, FaizOrani = 12m, FaizTipi = FaizTipi.Yillik, BsmvOrani = 0, VadeSayisi = 10, IlkVade = Bugun.AddMonths(1),
        }, null, []);
        Assert.Equal(-400_000m, await T.CariBakiyeAsync(banka.Id));
        var ilk = (await Kredi.DetayAsync(k.Id)).Plan[0];
        Assert.Equal(100m, ilk.Faiz); // 10.000 × %1 aylık

        await Kredi.TaksitOdeAsync(ilk.Id, ilk.Vade, kasa, null, null, kur: 42m);
        var o = (await Kredi.DetayAsync(k.Id)).Odemeler.Single();
        Assert.Equal(42m, o.Kur);
        Assert.Equal(Math.Round(ilk.Toplam * 42m, 2), o.KasaTutar);
        Assert.Equal(2_000_000m - o.KasaTutar, await T.KasaBakiyeAsync(kasa));
        await Assert.ThrowsAsync<DomainException>(async () =>
        {
            var usdKasa = await T.KasaAsync(100, ParaBirimi.USD);
            var ikinci = (await Kredi.DetayAsync(k.Id)).Plan[1];
            await Kredi.TaksitOdeAsync(ikinci.Id, ikinci.Vade, usdKasa, null, null, 42m);
        });
    }

    [Fact]
    public async Task Ara_odeme_yeni_plan_versiyonu_olusturur_erken_kapama_krediyi_kapatir()
    {
        var banka = await T.CariAsync(CariTipi.Finans);
        var kasa = await T.KasaAsync(1_000_000);
        var k = await Kredi.KaydetAsync(new Kredi
        {
            KrediNo = $"AO-{TestVerisi.Sira()}", BankaCariId = banka.Id, KullandirimTarihi = Bugun, Anapara = 120_000, FaizOrani = 2m, BsmvOrani = 0,
            VadeSayisi = 12, IlkVade = Bugun.AddMonths(1),
        }, null, []);
        var d = await Kredi.DetayAsync(k.Id);
        foreach (var t in d.Plan.Take(2)) await Kredi.TaksitOdeAsync(t.Id, t.Vade, kasa, null, null);
        var kalanOnce = (await Kredi.DetayAsync(k.Id)).Ozet.KalanAnapara;

        await Kredi.AraOdemeAsync(k.Id, d.Plan[1].Vade.AddDays(5), kasa, 30_000, YenidenPlanModu.VadeyiKisalt, 0);
        d = await Kredi.DetayAsync(k.Id);
        Assert.Equal((byte)2, d.Kredi.AktifVersiyon);
        Assert.Equal(kalanOnce - 30_000m, d.Ozet.KalanAnapara);
        var acik = d.Plan.Where(p => p.Durum == TaksitDurum.Bekliyor).ToList();
        Assert.True(acik.Count < 10);
        Assert.Equal(d.Ozet.KalanAnapara, acik.Sum(p => p.Anapara));
        await Assert.ThrowsAsync<DomainException>(() => Kredi.KaydetAsync(d.Kredi.Tap(x => x.Anapara = 1), null, []));

        var on = await Kredi.ErkenKapamaOnizleAsync(k.Id, acik[0].Vade, 0);
        Assert.Equal(d.Ozet.KalanAnapara, on.KalanAnapara);
        await Kredi.ErkenKapatAsync(k.Id, acik[0].Vade, kasa, 0);
        d = await Kredi.DetayAsync(k.Id);
        Assert.Equal(KrediDurum.Kapandi, d.Kredi.Durum);
        Assert.Equal(0m, d.Ozet.KalanAnapara);
        Assert.Equal(0m, await T.CariBakiyeAsync(banka.Id));
        Assert.DoesNotContain(d.Plan, p => p.Durum == TaksitDurum.Bekliyor);
    }

    [Fact]
    public async Task Sofor_para_ver_al_musteri_tahsilati_bakiye_ve_arac_bazli_defter()
    {
        var arac = await T.AracAsync();
        var sofor = await T.SoforAsync();
        var musteri = await T.CariAsync();
        var kasa = await T.KasaAsync(100_000);
        var sh = T.SoforHesap;
        await sh.AracAtaAsync(sofor.Id, arac.Id, Bugun.AddDays(-5));

        await sh.AvansVerAsync(new Avans { SoforId = sofor.Id, Tur = AvansTuru.Harclik, Tarih = Bugun, Tutar = 5_000, KasaHesapId = kasa });
        await sh.ParaAlAsync(sofor.Id, SoforHesapServisi.ParaAlTuru.Iade, Bugun, 1_000, kasa, null, null, null, null);
        await sh.ParaAlAsync(sofor.Id, SoforHesapServisi.ParaAlTuru.MusteriTahsilati, Bugun, 10_000, null, musteri.Id, null, null, "nakit navlun");

        Assert.Equal(96_000m, await T.KasaBakiyeAsync(kasa));
        Assert.Equal(14_000m, await T.CariBakiyeAsync(sofor.CariId)); // şoförün elinde 4.000 avans + 10.000 tahsilat
        Assert.Equal(-10_000m, await T.CariBakiyeAsync(musteri.Id));

        var defter = await sh.DefterAsync(Bugun, Bugun, sofor.Id);
        Assert.Equal(3, defter.Count); // avans, iade, müşteri tahsilatı
        Assert.All(defter, x => Assert.Equal(arac.Plaka, x.Plaka));
        Assert.Equal(14_000m, defter[^1].Bakiye);
        Assert.Equal(3, (await sh.DefterAsync(Bugun, Bugun, null, arac.Id)).Count);
        Assert.Equal(14_000m, (await sh.OzetAsync(sofor.Id)).Bakiye);
    }

    [Fact]
    public async Task Hakedis_maas_onerir_tahakkuk_sofor_carisine_alacak_yazar_tekrar_onermez()
    {
        var sofor = await T.SoforAsync();
        await using (var db = fx.Db())
        {
            var s = await db.Soforler.SingleAsync(x => x.Id == sofor.Id);
            s.MaasTutari = 30_000;
            await db.SaveChangesAsync();
        }
        var sh = T.SoforHesap;
        var oneri = (await sh.HakedisOnerAsync(202607)).Where(o => o.SoforId == sofor.Id).ToList();
        Assert.Equal(30_000m, Assert.Single(oneri).Tutar);
        await sh.TahakkukYazAsync(202607, oneri.Select(o => new PersonelTahakkuk { SoforId = o.SoforId, Tur = o.Tur, Tutar = o.Tutar }).ToList());
        await sh.TahakkukYazAsync(202607, [new PersonelTahakkuk { SoforId = sofor.Id, Tur = TahakkukTuru.Kesinti, Tutar = 500, Aciklama = "hasar" }]);
        Assert.Equal(-29_500m, await T.CariBakiyeAsync(sofor.CariId)); // şirket şoföre borçlu
        Assert.DoesNotContain(await sh.HakedisOnerAsync(202607), o => o.SoforId == sofor.Id);
        Assert.Equal(29_500m, (await sh.TahakkuklarAsync(202607, sofor.Id)).Sum(t => t.Net));
    }

    [Fact]
    public async Task Bakim_kaydi_plani_ilerletir_ve_servise_borc_yazar()
    {
        var arac = await T.AracAsync();
        var servisCari = await T.CariAsync(CariTipi.Tedarikci);
        int planId;
        await using (var db = fx.Db())
        {
            var p = new BakimPlani { AracId = arac.Id, BakimAdi = "Yağ değişimi", PeriyotKm = 30_000, SonKm = 65_000, SonTarih = Bugun.AddMonths(-6) };
            db.BakimPlanlari.Add(p);
            await db.SaveChangesAsync();
            planId = p.Id;
        }
        var servis = new BakimServisi(T.Factory, fx.Oturum, saat);
        Assert.Contains(await servis.YaklasanlarAsync(), y => y.PlanId == planId && y.Gecti);

        await servis.KaydetAsync(new Bakim
        {
            AracId = arac.Id, BakimPlaniId = (short)planId, Tarih = Bugun, Km = 101_000, OdemeSekli = OdemeSekli.Cari, ServisCariId = servisCari.Id,
            Kalemler = [new BakimKalem { Aciklama = "Motor yağı", Miktar = 40, BirimFiyat = 300 }, new BakimKalem { Aciklama = "İşçilik", KalemTipi = BakimKalemTipi.Iscilik, BirimFiyat = 3_000 }],
        });
        Assert.Equal(-15_000m, await T.CariBakiyeAsync(servisCari.Id));
        Assert.DoesNotContain(await servis.YaklasanlarAsync(), y => y.PlanId == planId);
    }

    [Fact]
    public async Task Disa_aktarici_excel_ve_pdf_uretir()
    {
        var d = new DisaAktarici("Test Nakliyat");
        var tablo = new TabloVerisi("Cari Ekstre - Çağlar İnşaat",
            [new TabloKolon("Tarih", r => ((dynamic)r).Tarih, KolonBicim.Tarih), new TabloKolon("Açıklama", r => ((dynamic)r).Aciklama),
             new TabloKolon("Borç", r => ((dynamic)r).Borc, KolonBicim.Para, Toplam: true)],
            [new { Tarih = Bugun, Aciklama = "Satış faturası ŞĞÜ", Borc = 1_234.5m }, new { Tarih = Bugun, Aciklama = "İade", Borc = 100m }])
        { AltBilgi = ["01.07.2026 - 31.07.2026"] };
        var xlsx = d.Excel(tablo);
        using (var wb = new XLWorkbook(new MemoryStream(xlsx)))
            Assert.Equal("Satış faturası ŞĞÜ", wb.Worksheets.First().Cell(5, 2).GetString());
        var pdf = d.Pdf(tablo);
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task Tum_belgeler_pdf_olarak_uretilir()
    {
        var klasor = Path.Combine(Path.GetTempPath(), "OzaylarBelgeTest");
        Directory.CreateDirectory(klasor);
        var sira = 0;
        void Pdf(byte[] x)
        {
            Assert.True(x.Length > 1000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(x, 0, 4));
            File.WriteAllBytes(Path.Combine(klasor, $"belge{++sira}.pdf"), x); // görsel kontrol için
        }
        var b = new BelgeUretici(T.Factory);
        var kasa = await T.KasaAsync(500_000);
        var arac = await T.AracAsync();
        var sofor = await T.SoforAsync();
        var musteri = await T.CariAsync();
        var k = await Kredi.KaydetAsync(new Kredi
        {
            KrediNo = $"PDF-{TestVerisi.Sira()}", BankaCariId = (await T.CariAsync(CariTipi.Finans)).Id, KullandirimTarihi = Bugun, Anapara = 100_000,
            FaizOrani = 2.5m, VadeSayisi = 6, IlkVade = Bugun.AddMonths(1),
        }, null, [new KrediArac { AracId = arac.Id, PayYuzde = 100 }]);
        Pdf(b.KrediOdemePlani(await Kredi.DetayAsync(k.Id)));
        Pdf(b.Makbuz(new MakbuzVeri(MakbuzTuru.Avans, "15", Bugun, "Ali Test", 1_250.50m, ParaBirimi.TRY, "Merkez Kasa", "Sefer avansı", "34 T 1", "S2026-1")));
        Pdf(b.Makbuz(new MakbuzVeri(MakbuzTuru.Tahsilat, "16", Bugun, "Çağlar Gıda", 3_000m, ParaBirimi.EUR, "Banka", null)));

        var sh = T.SoforHesap;
        await sh.AvansVerAsync(new Avans { SoforId = sofor.Id, Tarih = Bugun, Tutar = 1_000, KasaHesapId = kasa });
        Pdf(b.SoforEkstresi("Ali Test", Bugun, Bugun, await sh.DefterAsync(Bugun, Bugun, sofor.Id)));
        Pdf(b.HakedisPusulasi("Ali Test", 202607, [new TahakkukSatir(1, 202607, "Ali Test", TahakkukTuru.Maas, 30_000, null, null, "Temmuz maaşı", false),
            new TahakkukSatir(2, 202607, "Ali Test", TahakkukTuru.Kesinti, 500, null, null, "Hasar", false)], 1_000));
        Pdf(b.ZimmetTutanagi("Ali Test", [new Zimmet { Ad = "Opet taşıt kartı", SeriNo = "7006", Tur = ZimmetTuru.YakitKarti, VerilisTarihi = Bugun }]));

        var f = T.Factory;
        var rapor = new RaporServisi(f, fx.Oturum, saat, Kredi);
        var detay = new DetayServisi(f, fx.Oturum, saat, rapor, T.Gider, Kredi, T.Finans, T.Sefer);
        Pdf(b.CariMutabakat(await detay.CariOzetAsync(musteri.Id), Bugun));
        var sefer = await T.SeferAsync(arac, sofor, musteri);
        Pdf(b.SeferFoyu(await detay.SeferOzetAsync(sefer.Id), [], await sh.AvanslarAsync(Bugun, Bugun, sofor.Id)));
    }

    [Fact]
    public async Task Panel_ozeti_yetkiye_gore_doldurulur()
    {
        var f = T.Factory;
        var finans = new FinansServisi(f, fx.Oturum, saat);
        var panel = new PanelServisi(f, fx.Oturum, saat, finans, new EvrakServisi(f, fx.Oturum, [], saat), new BakimServisi(f, fx.Oturum, saat),
            new RaporServisi(f, fx.Oturum, saat, Kredi), new AyarServisi(f, fx.Oturum), Kredi, T.SoforHesap);
        var o = await panel.OzetAsync();
        Assert.NotNull(o.Kasalar);
        Assert.NotNull(o.AcikAlacak);
        Assert.NotNull(o.Kredi);
        Assert.Equal(12, o.Trend!.Count);
    }
}

internal static class TestUzanti
{
    public static T Tap<T>(this T x, Action<T> a)
    {
        a(x);
        return x;
    }
}
