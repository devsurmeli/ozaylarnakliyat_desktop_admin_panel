using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Tests;

/// <summary>Her test kendi benzersiz kayıtlarını oluşturur (testler aynı veritabanını paylaşır).</summary>
public sealed class TestVerisi(VeritabaniFixture fx, ISaat saat)
{
    private static int _sayac = Random.Shared.Next(1000, 9000) * 1000;

    /// <summary>Algoritmaya uygun, benzersiz T.C. kimlik no üretir.</summary>
    public static string TcUret()
    {
        var ilk9 = (100_000_000 + Sira()).ToString();
        var d = ilk9.Select(c => c - '0').ToArray();
        var d10 = (((d[0] + d[2] + d[4] + d[6] + d[8]) * 7 - (d[1] + d[3] + d[5] + d[7])) % 10 + 10) % 10;
        var d11 = (d.Sum() + d10) % 10;
        return $"{ilk9}{d10}{d11}";
    }

    public IFiloDbContextFactory Factory { get; } = fx.Factory(saat);
    public SeferServisi Sefer => new(Factory, fx.Oturum, saat);
    public FaturaServisi Fatura => new(Factory, fx.Oturum, saat);
    public FinansServisi Finans => new(Factory, fx.Oturum, saat);
    public SoforHesapServisi SoforHesap => new(Factory, fx.Oturum, saat);
    public GiderServisi Gider => new(Factory, fx.Oturum, saat);

    public static int Sira() => Interlocked.Increment(ref _sayac);

    public async Task<Cari> CariAsync(CariTipi tip = CariTipi.Musteri)
    {
        await using var db = fx.Db(saat);
        var c = new Cari { Kod = $"T{Sira():D6}", Unvan = $"Test Cari {Sira()}", CariTipi = tip };
        db.Cariler.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<Arac> AracAsync()
    {
        await using var db = fx.Db(saat);
        var a = new Arac { Plaka = $"34 T {Sira():D4}", GuncelKm = 100_000, BeklenenTuketim = 30 };
        db.Araclar.Add(a);
        await db.SaveChangesAsync();
        return a;
    }

    public async Task<Sofor> SoforAsync()
    {
        var servis = new SoforServisi(Factory, fx.Oturum, [new SoforDogrulayici()]);
        return await servis.KaydetAsync(new Sofor { TCKN = TcUret(), Ad = "Ali", Soyad = $"Test{Sira()}" });
    }

    public async Task<byte> KasaAsync(decimal acilis, ParaBirimi pb = ParaBirimi.TRY)
    {
        await using var db = fx.Db(saat);
        var k = new KasaHesap { Ad = $"Kasa {Sira()}", AcilisBakiye = acilis, AcilisTarihi = new DateOnly(2026, 1, 1), ParaBirimi = pb };
        db.KasaHesaplari.Add(k);
        await db.SaveChangesAsync();
        return k.Id;
    }

    public async Task<(int Cikis, int Varis)> LokasyonlarAsync()
    {
        await using var db = fx.Db(saat);
        var a = new Lokasyon { Ad = $"İstanbul Depo {Sira()}", UlkeId = 1, IlId = 34 };
        var b = new Lokasyon { Ad = $"Ankara Depo {Sira()}", UlkeId = 1, IlId = 6 };
        db.Lokasyonlar.AddRange(a, b);
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    public async Task<Sefer> SeferAsync(Arac arac, Sofor sofor, Cari musteri, decimal navlun = 50_000)
    {
        var (c, v) = await LokasyonlarAsync();
        var s = new Sefer
        {
            MusteriCariId = musteri.Id, AracId = arac.Id, CikisLokasyonId = c, VarisLokasyonId = v,
            PlanlananTarih = saat.Bugun, NavlunTutar = navlun, NavlunKdvOrani = 20, NavlunTevkifatPay = 2,
        };
        return await Sefer.KaydetAsync(s, [sofor.Id], [], uyarilariOnayla: true);
    }

    public async Task<decimal> CariBakiyeAsync(int cariId)
    {
        await using var db = fx.Db(saat);
        return await Defter.CariBakiyeAsync(db, cariId);
    }

    public async Task<decimal> KasaBakiyeAsync(byte kasaId)
    {
        await using var db = fx.Db(saat);
        return await Defter.KasaBakiyeAsync(db, kasaId);
    }
}
