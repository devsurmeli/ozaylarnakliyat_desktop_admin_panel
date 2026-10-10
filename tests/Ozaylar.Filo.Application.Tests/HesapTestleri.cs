using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;

namespace Ozaylar.Filo.Application.Tests;

public class HesapTestleri
{
    [Fact]
    public void Fatura_kalemleri_kdv_iskonto_ve_tevkifatla_hesaplanir()
    {
        var f = new Fatura
        {
            Kalemler =
            [
                new FaturaKalem { Aciklama = "Nakliye", Miktar = 1, BirimFiyat = 33_333.33m, KdvOrani = 20, TevkifatPay = 2 },
                new FaturaKalem { Aciklama = "Bekleme", Miktar = 2, BirimFiyat = 1_000, IskontoOrani = 10, KdvOrani = 20, TevkifatPay = 0 },
            ],
        };
        FaturaHesap.Hesapla(f);
        Assert.Equal(33_333.33m + 1_800m, f.AraToplam);
        Assert.Equal(6_666.67m + 360m, f.KdvToplam);
        Assert.Equal(1_333.33m, f.TevkifatToplam);
        Assert.Equal(f.AraToplam + f.KdvToplam - f.TevkifatToplam, f.GenelToplam);
        Assert.Equal((byte)2, f.Kalemler[1].SiraNo);
    }

    [Fact]
    public void Gecersiz_tevkifat_payi_reddedilir() =>
        Assert.Throws<DomainException>(() => FaturaHesap.KalemHesapla(new FaturaKalem { BirimFiyat = 1, TevkifatPay = 11 }));

    [Fact]
    public void Kdv_dahil_tutardan_net_bulunur()
    {
        Assert.Equal(1_000m, Para.KdvHaric(1_200m, 20));
        Assert.Equal(500m, Para.KdvHaric(500m, 0));
    }

    [Fact]
    public void Harcirah_gun_sayisi_bas_ve_bitis_dahil()
    {
        Assert.Equal(1, HarcirahHesap.GunSayisi(new(2026, 3, 1), new(2026, 3, 1)));
        Assert.Equal(5, HarcirahHesap.GunSayisi(new(2026, 2, 26), new(2026, 3, 2)));
        Assert.Throws<DomainException>(() => HarcirahHesap.GunSayisi(new(2026, 3, 2), new(2026, 3, 1)));
    }

    [Fact]
    public void Taksit_plani_anaparayi_tam_kapatir_ve_faiz_azalir()
    {
        var plan = TaksitPlani.Olustur(1_000_000m, 3m, 24, new DateOnly(2026, 1, 15));
        Assert.Equal(24, plan.Count);
        Assert.Equal(1_000_000m, plan.Sum(p => p.Anapara));
        Assert.Equal(0m, plan[^1].KalanAnapara);
        Assert.Equal(new DateOnly(2027, 12, 15), plan[^1].Vade);
        Assert.True(plan[0].Faiz > plan[^1].Faiz);
        Assert.Equal(30_000m, plan[0].Faiz);
        // Eşit taksit: son taksit yuvarlama farkı kadar sapabilir
        Assert.All(plan.Take(23), p => Assert.Equal(plan[0].Toplam, p.Toplam));
        Assert.InRange(Math.Abs(plan[^1].Toplam - plan[0].Toplam), 0m, 1m);
    }

    [Fact]
    public void Faizsiz_taksit_plani_esit_bolunur()
    {
        var plan = TaksitPlani.Olustur(1_000m, 0m, 3, new DateOnly(2026, 1, 31));
        Assert.Equal([333.33m, 333.33m, 333.34m], plan.Select(p => p.Anapara));
        Assert.Equal(new DateOnly(2026, 2, 28), plan[1].Vade);
    }

    [Fact]
    public void Amortisman_donem_kesisimine_gore_hesaplanir()
    {
        // 1.200.000 - 0 hurda, 120 ay → aylık 10.000; Mart 2026 alış
        var alis = new DateOnly(2026, 3, 10);
        Assert.Equal(10_000m, AmortismanHesap.AylikTutar(1_200_000m, 0, 120));
        Assert.Equal(100_000m, AmortismanHesap.Donem(1_200_000m, 0, 120, alis, new(2026, 1, 1), new(2026, 12, 31)));
        Assert.Equal(0m, AmortismanHesap.Donem(1_200_000m, 0, 120, alis, new(2026, 1, 1), new(2026, 2, 28)));
        // Ömür bittikten sonra amortisman yok
        Assert.Equal(0m, AmortismanHesap.Donem(1_200_000m, 0, 120, alis, new(2036, 3, 1), new(2036, 12, 31)));
        Assert.Equal(0m, AmortismanHesap.Donem(null, null, null, null, new(2026, 1, 1), new(2026, 12, 31)));
    }

    [Fact]
    public void Yaslandirma_odemeleri_en_eski_borca_uygular()
    {
        var bugun = new DateOnly(2026, 6, 30);
        var s = Yaslandirma.Hesapla(
        [
            (new DateOnly(2026, 2, 1), 1_000m, 0m),   // 149 gün
            (new DateOnly(2026, 5, 15), 2_000m, 0m),  // 46 gün
            (new DateOnly(2026, 6, 20), 3_000m, 0m),  // 10 gün
            (new DateOnly(2026, 7, 30), 4_000m, 0m),  // vadesi gelmemiş
            (new DateOnly(2026, 6, 1), 0m, 1_500m),   // ödeme
        ], bugun);
        Assert.Equal(0m, s.Gun90Ustu);
        Assert.Equal(1_500m, s.Gun31_60);
        Assert.Equal(3_000m, s.Gun0_30);
        Assert.Equal(4_000m, s.VadesiGelmemis);
        Assert.Equal(8_500m, s.Toplam);
    }

    [Fact]
    public void Yakit_tuketimi_ilk_fullden_once_yapilan_alimlari_saymaz()
    {
        var t0 = new DateTime(2026, 1, 1);
        var araliklar = YakitTuketimHesap.Araliklar(
        [
            new YakitAlimi(t0, 1_000, 50, false),              // ilk full'den önce: yok sayılır
            new YakitAlimi(t0.AddDays(1), 1_200, 300, true),   // başlangıç
            new YakitAlimi(t0.AddDays(2), 1_800, 120, false),
            new YakitAlimi(t0.AddDays(3), 2_200, 200, true),   // 1000 km, 320 L
            new YakitAlimi(t0.AddDays(4), 2_700, 160, true),   // 500 km, 160 L
        ]);
        Assert.Equal(2, araliklar.Count);
        Assert.Equal(32m, araliklar[0].LitreYuzKm);
        Assert.Equal(32m, araliklar[1].LitreYuzKm);
        Assert.Equal(32m, YakitTuketimHesap.Ortalama(araliklar));
        Assert.Equal(6.7m, YakitTuketimHesap.SapmaYuzde(32m, 30m));
    }

    [Fact]
    public void Mahsup_farki_isaretli_hesaplanir()
    {
        Assert.Equal(2_500m, MahsupHesap.Fark(10_000, 4_500, 3_000));
        Assert.Equal(-500m, MahsupHesap.Fark(0, 500, 0));
        var o = SoforHesapServisi.Onizle(
        [
            new AcikKalem(AcikKalemTuru.Avans, 1, default, null, "", 1_000, ParaBirimi.TRY, 1_000, true),
            new AcikKalem(AcikKalemTuru.Yakit, 2, default, null, "", 300, ParaBirimi.TRY, 300, true),
            new AcikKalem(AcikKalemTuru.Masraf, 3, default, null, "", 200, ParaBirimi.TRY, 200, true),
            new AcikKalem(AcikKalemTuru.Harcirah, 4, default, null, "", 100, ParaBirimi.EUR, 3_500, true),
        ]);
        Assert.Equal(500m, o.HarcamaToplam);
        Assert.Equal(-3_000m, o.Fark);
    }

    [Theory]
    [InlineData("10000000146", true)]
    [InlineData("10000000147", false)]
    [InlineData("01234567890", false)]
    [InlineData("1234", false)]
    public void Tckn_dogrulama(string tc, bool gecerli) => Assert.Equal(gecerli, Kontrol.TcknGecerli(tc));

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("1234567891", false)]
    [InlineData("123456789", false)]
    public void Vkn_dogrulama(string vkn, bool gecerli) => Assert.Equal(gecerli, Kontrol.VknGecerli(vkn));

    [Theory]
    [InlineData("TR33 0006 1005 1978 6457 8413 26", true)]
    [InlineData("TR330006100519786457841327", false)]
    [InlineData("", true)]
    public void Iban_dogrulama(string iban, bool gecerli) => Assert.Equal(gecerli, Kontrol.IbanGecerli(iban));

    [Theory]
    [InlineData("34abc123", "34 ABC 123")]
    [InlineData(" 06 a 1234 ", "06 A 1234")]
    [InlineData("34çiğ12", "34 ÇİĞ 12")]
    [InlineData("B-AB 1234", "BAB1234")]
    public void Plaka_standartlastirilir(string girdi, string beklenen) => Assert.Equal(beklenen, Metin.Plaka(girdi));
}
