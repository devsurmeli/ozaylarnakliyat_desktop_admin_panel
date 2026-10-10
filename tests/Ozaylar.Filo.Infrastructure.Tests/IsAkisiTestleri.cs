using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Tests;

[Collection(VeritabaniKoleksiyonu.Ad)]
public class IsAkisiTestleri(VeritabaniFixture fx)
{
    private static readonly DateOnly Bugun = new(2026, 6, 15);
    private readonly TestVerisi t = new(fx, new SabitSaat(Bugun));

    [Fact]
    public async Task Sofor_avans_harcama_harcirah_mahsup_akisi_bakiyeyi_sifirlar()
    {
        var kasa = await t.KasaAsync(100_000);
        var sofor = await t.SoforAsync();
        var arac = await t.AracAsync();
        var musteri = await t.CariAsync();
        var sefer = await t.SeferAsync(arac, sofor, musteri);

        // 1) 10.000 TL avans: kasa -10.000, şoför borçlu 10.000
        await t.SoforHesap.AvansVerAsync(new Avans { SoforId = sofor.Id, SeferId = sefer.Id, Tarih = Bugun, Tutar = 10_000, KasaHesapId = kasa });
        Assert.Equal(90_000, await t.KasaBakiyeAsync(kasa));
        Assert.Equal(10_000, await t.CariBakiyeAsync(sofor.CariId));

        // 2) Şoför yolda 3.000 TL otoyol + 1.500 TL yemek öder (nakit)
        await t.Gider.MasrafKaydetAsync(new Masraf { Tarih = Bugun, MasrafTuruId = 3, Merkez = MasrafMerkezi.Sefer, SeferId = sefer.Id, Tutar = 3_000, OdemeSekli = OdemeSekli.SoforNakit });
        await t.Gider.MasrafKaydetAsync(new Masraf { Tarih = Bugun, MasrafTuruId = 5, Merkez = MasrafMerkezi.Sefer, SeferId = sefer.Id, Tutar = 1_500, OdemeSekli = OdemeSekli.SoforNakit });
        Assert.Equal(5_500, await t.CariBakiyeAsync(sofor.CariId));

        // 3) 4 gün × 750 TL harcırah
        await t.SoforHesap.HarcirahKaydetAsync(new Harcirah { SoforId = sofor.Id, SeferId = sefer.Id, Baslangic = Bugun, Bitis = Bugun.AddDays(3), GunlukTutar = 750 });
        Assert.Equal(2_500, await t.CariBakiyeAsync(sofor.CariId));

        // 4) Mahsup: şoför 2.500 TL iade eder
        var acik = await t.SoforHesap.AcikKalemlerAsync(sofor.Id);
        Assert.Equal(4, acik.Count);
        var onizleme = SoforHesapServisi.Onizle(acik);
        Assert.Equal(2_500, onizleme.Fark);
        var m = await t.SoforHesap.MahsupYapAsync(sofor.Id, sefer.Id, Bugun, acik.Select(k => (k.Tur, k.Id)).ToList(), MahsupSonuc.IadeAlindi, kasa, null);

        Assert.Equal(0, await t.CariBakiyeAsync(sofor.CariId));
        Assert.Equal(92_500, await t.KasaBakiyeAsync(kasa));
        Assert.Empty(await t.SoforHesap.AcikKalemlerAsync(sofor.Id));

        // 5) Mahsup iptali kalemleri tekrar açar ve kasa iadesini geri alır
        await t.SoforHesap.MahsupIptalAsync(m.Id, "test");
        Assert.Equal(4, (await t.SoforHesap.AcikKalemlerAsync(sofor.Id)).Count);
        Assert.Equal(2_500, await t.CariBakiyeAsync(sofor.CariId));
        Assert.Equal(90_000, await t.KasaBakiyeAsync(kasa));
    }

    [Fact]
    public async Task Fatura_tevkifatli_cariye_islenir_tahsilat_kapatir_iptal_seferi_geri_alir()
    {
        var kasa = await t.KasaAsync(0);
        var sofor = await t.SoforAsync();
        var arac = await t.AracAsync();
        var musteri = await t.CariAsync();
        var sefer = await t.SeferAsync(arac, sofor, musteri, navlun: 100_000);

        await t.Sefer.DurumDegistirAsync(sefer.Id, SeferDurum.Yolda);
        var kayitli = (await t.Sefer.GetirAsync(sefer.Id))!;
        kayitli.CikisKm = 100_000;
        kayitli.DonusKm = 100_900;
        await t.Sefer.KaydetAsync(kayitli, kayitli.Soforler.Select(x => x.SoforId).ToList(), [], uyarilariOnayla: true);
        await t.Sefer.DurumDegistirAsync(sefer.Id, SeferDurum.TeslimEdildi);

        Assert.Contains(await t.Fatura.FaturalanmamisSeferlerAsync(musteri.Id), s => s.SeferId == sefer.Id);
        var kalemler = await t.Fatura.SeferKalemleriAsync([sefer.Id]);
        var f = await t.Fatura.KaydetAsync(new Fatura { FaturaNo = $"F{TestVerisi.Sira()}", Tarih = Bugun, VadeTarihi = Bugun.AddDays(30), CariId = musteri.Id, Kalemler = kalemler });

        // 100.000 + %20 KDV (20.000) - 2/10 tevkifat (4.000) = 116.000
        Assert.Equal(20_000, f.KdvToplam);
        Assert.Equal(4_000, f.TevkifatToplam);
        Assert.Equal(116_000, f.GenelToplam);
        Assert.Equal(116_000, await t.CariBakiyeAsync(musteri.Id));
        Assert.Equal(SeferDurum.Faturalandi, (await t.Sefer.GetirAsync(sefer.Id))!.Durum);
        Assert.DoesNotContain(await t.Fatura.FaturalanmamisSeferlerAsync(musteri.Id), s => s.SeferId == sefer.Id);

        // Aynı sefer ikinci kez faturalanamaz
        await Assert.ThrowsAsync<DomainException>(() => t.Fatura.KaydetAsync(new Fatura
            { FaturaNo = $"F{TestVerisi.Sira()}", Tarih = Bugun, CariId = musteri.Id, Kalemler = kalemler.Select(k => new FaturaKalem { SeferId = k.SeferId, Aciklama = "x", BirimFiyat = 1, Miktar = 1 }).ToList() }));

        // Kısmi tahsilat ve yaşlandırma
        await t.Finans.TahsilatAsync(new TahsilatOdeme(musteri.Id, kasa, Bugun, 16_000, null));
        Assert.Equal(100_000, await t.CariBakiyeAsync(musteri.Id));
        Assert.Equal(16_000, await t.KasaBakiyeAsync(kasa));
        var yas = (await t.Finans.YaslandirmaAsync()).Single(y => y.CariId == musteri.Id);
        Assert.Equal(100_000, yas.Sonuc.VadesiGelmemis);

        // Fatura iptali: cari ters kayıt, sefer tekrar faturalanabilir
        await t.Fatura.IptalAsync(f.Id, "test");
        Assert.Equal(-16_000, await t.CariBakiyeAsync(musteri.Id));
        Assert.Equal(SeferDurum.Kapatildi, (await t.Sefer.GetirAsync(sefer.Id))!.Durum);
        Assert.Contains(await t.Fatura.FaturalanmamisSeferlerAsync(musteri.Id), s => s.SeferId == sefer.Id);

        var ekstre = await t.Finans.CariEkstreAsync(musteri.Id, Bugun.AddDays(-1), Bugun);
        Assert.Equal(-16_000, ekstre.Bakiye);
        Assert.Equal(2, ekstre.Satirlar.Count(s => s.Iptal));
    }

    [Fact]
    public async Task Taseron_seferi_kapaninca_bedel_taserona_yazilir_yeniden_acilinca_geri_alinir()
    {
        var musteri = await t.CariAsync();
        var taseron = await t.CariAsync(CariTipi.Taseron);
        var (c, v) = await t.LokasyonlarAsync();
        var s = await t.Sefer.KaydetAsync(new Sefer
        {
            MusteriCariId = musteri.Id, TaseronCariId = taseron.Id, TaseronPlaka = "06abc123", TaseronBedel = 40_000,
            CikisLokasyonId = c, VarisLokasyonId = v, PlanlananTarih = Bugun, NavlunTutar = 48_000,
        }, [], [], uyarilariOnayla: true);
        Assert.Equal("06 ABC 123", (await t.Sefer.GetirAsync(s.Id))!.TaseronPlaka);

        await t.Sefer.DurumDegistirAsync(s.Id, SeferDurum.Yolda);
        await t.Sefer.DurumDegistirAsync(s.Id, SeferDurum.TeslimEdildi);
        await t.Sefer.DurumDegistirAsync(s.Id, SeferDurum.Kapatildi);
        Assert.Equal(-40_000, await t.CariBakiyeAsync(taseron.Id));

        var k = await t.Sefer.KarlilikAsync(s.Id);
        Assert.Equal(48_000, k.GelirTL);
        Assert.Equal(8_000, k.KarTL);

        await t.Sefer.DurumDegistirAsync(s.Id, SeferDurum.TeslimEdildi);
        Assert.Equal(0, await t.CariBakiyeAsync(taseron.Id));
        // Tekrar kapatınca bir kez yazılır
        await t.Sefer.DurumDegistirAsync(s.Id, SeferDurum.Kapatildi);
        Assert.Equal(-40_000, await t.CariBakiyeAsync(taseron.Id));
    }

    [Fact]
    public async Task Kasa_bakiyesi_yetersizse_avans_verilemez()
    {
        var kasa = await t.KasaAsync(1_000);
        var sofor = await t.SoforAsync();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            t.SoforHesap.AvansVerAsync(new Avans { SoforId = sofor.Id, Tarih = Bugun, Tutar = 5_000, KasaHesapId = kasa }));
        Assert.Contains("yetersiz", ex.Message);
    }

    [Fact]
    public async Task Ayni_arac_iki_seferde_ayni_anda_yolda_olamaz()
    {
        var sofor1 = await t.SoforAsync();
        var sofor2 = await t.SoforAsync();
        var arac = await t.AracAsync();
        var musteri = await t.CariAsync();
        var s1 = await t.SeferAsync(arac, sofor1, musteri);
        var s2 = await t.SeferAsync(arac, sofor2, musteri);
        await t.Sefer.DurumDegistirAsync(s1.Id, SeferDurum.Yolda);
        var ex = await Assert.ThrowsAsync<DomainException>(() => t.Sefer.DurumDegistirAsync(s2.Id, SeferDurum.Yolda));
        Assert.Contains(s1.SeferNo, ex.Message);
    }

    [Fact]
    public async Task Ayni_gun_ayni_araca_ikinci_sefer_uyari_verir()
    {
        var sofor = await t.SoforAsync();
        var arac = await t.AracAsync();
        var musteri = await t.CariAsync();
        await t.SeferAsync(arac, sofor, musteri);
        var (c, v) = await t.LokasyonlarAsync();
        var ex = await Assert.ThrowsAsync<UyariVarException>(() => t.Sefer.KaydetAsync(new Sefer
        {
            MusteriCariId = musteri.Id, AracId = arac.Id, CikisLokasyonId = c, VarisLokasyonId = v, PlanlananTarih = Bugun,
        }, [sofor.Id], []));
        Assert.Contains(ex.Uyarilar, u => u.Contains("aynı gün"));
    }

    [Fact]
    public async Task Suresi_dolmus_sefer_engelleyen_evrak_seferi_engeller()
    {
        var sofor = await t.SoforAsync();
        var arac = await t.AracAsync();
        var musteri = await t.CariAsync();
        await using (var db = fx.Db())
        {
            var muayene = await db.EvrakTurleri.SingleAsync(e => e.Ad == "Araç Muayenesi");
            db.Evraklar.Add(new Evrak { EvrakTuruId = muayene.Id, AracId = arac.Id, BitisTarihi = Bugun.AddDays(-1) });
            await db.SaveChangesAsync();
        }
        var ex = await Assert.ThrowsAsync<DomainException>(() => t.SeferAsync(arac, sofor, musteri));
        Assert.Contains("Araç Muayenesi", ex.Message);

        // Yenilenince engel kalkar
        await using (var db = fx.Db())
        {
            var muayene = await db.EvrakTurleri.SingleAsync(e => e.Ad == "Araç Muayenesi");
            db.Evraklar.Add(new Evrak { EvrakTuruId = muayene.Id, AracId = arac.Id, BitisTarihi = Bugun.AddYears(1) });
            await db.SaveChangesAsync();
        }
        await t.SeferAsync(arac, sofor, musteri);
    }

    [Fact]
    public async Task Yakit_otomatik_sefere_eslesir_ve_tuketim_hesaplanir()
    {
        var sofor = await t.SoforAsync();
        var arac = await t.AracAsync();
        var musteri = await t.CariAsync();
        var istasyon = await t.CariAsync(CariTipi.Tedarikci);
        var sefer = await t.SeferAsync(arac, sofor, musteri);
        await t.Sefer.DurumDegistirAsync(sefer.Id, SeferDurum.Yolda);

        var z = Bugun.ToDateTime(new TimeOnly(10, 0));
        await t.Gider.YakitKaydetAsync(new Yakit { AracId = arac.Id, AlimZamani = z, Km = 100_000, Litre = 400, BirimFiyat = 50, FullDepo = true, OdemeSekli = OdemeSekli.Cari, IstasyonCariId = istasyon.Id });
        await t.Gider.YakitKaydetAsync(new Yakit { AracId = arac.Id, AlimZamani = z.AddHours(4), Km = 100_500, Litre = 100, BirimFiyat = 50, FullDepo = false, OdemeSekli = OdemeSekli.Cari, IstasyonCariId = istasyon.Id });
        await t.Gider.YakitKaydetAsync(new Yakit { AracId = arac.Id, AlimZamani = z.AddHours(8), Km = 101_000, Litre = 230, BirimFiyat = 50, FullDepo = true, OdemeSekli = OdemeSekli.Cari, IstasyonCariId = istasyon.Id });

        var liste = await t.Gider.YakitlarAsync(new GiderFiltre(Bugun, Bugun, AracId: arac.Id));
        Assert.All(liste, y => Assert.Equal(sefer.SeferNo, y.Sefer));
        Assert.Equal(-36_500, await t.CariBakiyeAsync(istasyon.Id));

        // (100 + 230) L / 1000 km = 33 L/100km, beklenen 30 → %10 sapma
        var r = (await t.Gider.TuketimRaporuAsync(Bugun, Bugun)).Single(x => x.AracId == arac.Id);
        Assert.Equal(33m, r.LitreYuzKm);
        Assert.Equal(10m, r.SapmaYuzde);
    }
}
