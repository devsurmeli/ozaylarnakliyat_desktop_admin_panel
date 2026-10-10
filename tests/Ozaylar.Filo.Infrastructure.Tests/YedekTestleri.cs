using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Infrastructure.Yedek;

namespace Ozaylar.Filo.Infrastructure.Tests;

[Collection(VeritabaniKoleksiyonu.Ad)]
public class YedekTestleri(VeritabaniFixture fx)
{
    [Fact]
    public async Task Sifreleme_coz_ayni_icerigi_verir_yanlis_parola_reddedilir()
    {
        var klasor = Directory.CreateTempSubdirectory("ozyd").FullName;
        try
        {
            var kaynak = Path.Combine(klasor, "a.bak");
            var veri = new byte[3 * 1024 * 1024 + 123];
            RandomNumberGenerator.Fill(veri.AsSpan(0, 1024 * 1024)); // kısmen rastgele, kısmen sıkıştırılabilir
            await File.WriteAllBytesAsync(kaynak, veri);
            await YedekSifreleme.SifreleAsync(kaynak, Path.Combine(klasor, "a.ozyd"), "Parola-123");
            await YedekSifreleme.CozAsync(Path.Combine(klasor, "a.ozyd"), Path.Combine(klasor, "b.bak"), "Parola-123");
            Assert.Equal(veri, await File.ReadAllBytesAsync(Path.Combine(klasor, "b.bak")));
            Assert.True(new FileInfo(Path.Combine(klasor, "a.ozyd")).Length < veri.Length);
            await Assert.ThrowsAsync<CryptographicException>(() =>
                YedekSifreleme.CozAsync(Path.Combine(klasor, "a.ozyd"), Path.Combine(klasor, "c.bak"), "yanlis"));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public async Task Yedek_alinir_dogrulanir_sifreli_kopyalanir_ve_son_yedek_yazilir()
    {
        // Kurulumdaki gibi: yedek klasörü oluşturulur ve SQL Server servis hesabına yazma izni verilir.
        string servisHesabi;
        await using (var db = fx.Db())
            servisHesabi = await db.Database.SqlQueryRaw<string>("SELECT TOP 1 CAST(service_account AS nvarchar(200)) AS Value FROM sys.dm_server_services WHERE servicename LIKE 'SQL Server (%'").SingleAsync();
        var yerel = Directory.CreateTempSubdirectory("ozyd1").FullName;
        using (var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("icacls", $"\"{yerel}\" /grant \"{servisHesabi}:(OI)(CI)M\"") { CreateNoWindow = true, UseShellExecute = false })!)
            await p.WaitForExitAsync();
        var ikinci = Directory.CreateTempSubdirectory("ozyd2").FullName;
        try
        {
            var ayar = new YedekAyarlari { YerelKlasor = yerel, IkinciHedef = ikinci, SifrelemeParolasi = "Test-Parola-9" };
            var servis = new YedekServisi(fx.Baglanti, ayar, NullLogger<YedekServisi>.Instance);
            var dosyalar = await servis.YedekAlAsync(YedekTuru.Fark); // tam yedek yoksa otomatik tam alınır
            Assert.Equal(2, dosyalar.Count);
            Assert.All(dosyalar, d => Assert.Contains("_TAM_", d));
            Assert.Equal(2, Directory.GetFiles(ikinci, "*.ozyd").Length);

            var fark = await servis.YedekAlAsync(YedekTuru.Fark);
            Assert.All(fark, d => Assert.Contains("_FARK_", d));

            await using var db = fx.Db();
            var son = await db.Ayarlar.Where(a => a.Anahtar == "Yedek.SonBasarili").Select(a => a.Deger).SingleAsync();
            Assert.True(DateTime.Parse(son!, null, System.Globalization.DateTimeStyles.RoundtripKind) > DateTime.UtcNow.AddMinutes(-5));
        }
        finally
        {
            if (Directory.Exists(yerel)) Directory.Delete(yerel, true);
            Directory.Delete(ikinci, true);
        }
    }

    [Fact]
    public void Tcmb_xml_cozulur()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Tarih_Date Tarih="09.10.2026" Date="10/09/2026" Bulten_No="2026/190">
              <Currency CrossOrder="0" Kod="USD" CurrencyCode="USD"><Unit>1</Unit><ForexBuying>41.5123</ForexBuying><ForexSelling>41.5871</ForexSelling></Currency>
              <Currency CrossOrder="9" Kod="EUR" CurrencyCode="EUR"><Unit>1</Unit><ForexBuying>48.1010</ForexBuying><ForexSelling>48.1877</ForexSelling></Currency>
              <Currency CrossOrder="2" Kod="JPY" CurrencyCode="JPY"><Unit>100</Unit><ForexBuying>27.9</ForexBuying><ForexSelling>28.1</ForexSelling></Currency>
            </Tarih_Date>
            """;
        var (tarih, kurlar) = TcmbKurServisi.Coz(xml);
        Assert.Equal(new DateOnly(2026, 10, 9), tarih);
        Assert.Equal(2, kurlar.Count);
        Assert.Contains(kurlar, k => k.Pb == ParaBirimi.EUR && k.Satis == 48.1877m);
    }
}
