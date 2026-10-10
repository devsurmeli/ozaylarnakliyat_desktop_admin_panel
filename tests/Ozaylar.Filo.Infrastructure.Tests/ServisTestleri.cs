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

    [Fact]
    public async Task Kredi_taksiti_odenince_kasadan_cikar_ve_arac_karliliginda_faiz_gorunur()
    {
        var arac = await T.AracAsync();
        var bankaCari = await T.CariAsync(CariTipi.Finans);
        var kasa = await T.KasaAsync(1_000_000);
        var servis = new FinansmanServisi(T.Factory, fx.Oturum, saat);
        var f = new AracFinansman { AracId = arac.Id, FinansKurumuCariId = bankaCari.Id, AnaparaTutar = 600_000, AylikFaizOrani = 2.5m, VadeSayisi = 12, BaslangicTarihi = Bugun };
        var plan = FinansmanServisi.PlanOner(f, new DateOnly(2026, 8, 1));
        var id = await servis.KaydetAsync(f, plan);
        var taksitler = await servis.TaksitlerAsync(new DateOnly(2026, 8, 1), new DateOnly(2027, 12, 31), true);
        var ilk = taksitler.Where(t => t.FinansmanId == id).OrderBy(t => t.TaksitNo).First();
        Assert.Equal(15_000m, ilk.Faiz);

        await servis.TaksitOdeAsync(ilk.Id, kasa, new DateOnly(2026, 8, 1));
        Assert.Equal(1_000_000 - ilk.Toplam, await T.KasaBakiyeAsync(kasa));
        await Assert.ThrowsAsync<DomainException>(() => servis.TaksitOdeAsync(ilk.Id, kasa, new DateOnly(2026, 8, 1)));

        var rapor = new RaporServisi(T.Factory, fx.Oturum, saat);
        var satir = (await rapor.AracKarlilikAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))).Single(r => r.AracId == arac.Id);
        Assert.Equal(15_000m, satir.FinansmanFaizi);
        Assert.Equal(-15_000m, satir.Kar);
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
    public async Task Panel_ozeti_yetkiye_gore_doldurulur()
    {
        var f = T.Factory;
        var finans = new FinansServisi(f, fx.Oturum, saat);
        var panel = new PanelServisi(f, fx.Oturum, saat, finans,
            new EvrakServisi(f, fx.Oturum, [], saat), new BakimServisi(f, fx.Oturum, saat), new RaporServisi(f, fx.Oturum, saat), new AyarServisi(f, fx.Oturum));
        var o = await panel.OzetAsync();
        Assert.NotNull(o.Kasalar);
        Assert.NotNull(o.AcikAlacak);
    }
}
