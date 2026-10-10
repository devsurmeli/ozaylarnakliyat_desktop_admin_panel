using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Ozaylar.Filo.Infrastructure.Yedek;

/// <summary>
/// Yedek dosyalarını sıkıştırıp (GZip) AES-256-GCM ile şifreler. SQL Express'te yedek sıkıştırma ve şifreleme olmadığı için gereklidir.
/// Biçim: "OZYD1" | tuz(16) | iterasyon(4) | [parça: uzunluk(4) | nonce(12) | etiket(16) | şifreli veri] ...
/// </summary>
public static class YedekSifreleme
{
    private static readonly byte[] Imza = "OZYD1"u8.ToArray();
    private const int Parca = 1024 * 1024;
    private const int Iterasyon = 200_000;

    public static async Task SifreleAsync(string kaynak, string hedef, string parola, CancellationToken ct = default)
    {
        var tuz = RandomNumberGenerator.GetBytes(16);
        var anahtar = Anahtar(parola, tuz, Iterasyon);
        await using var cikis = File.Create(hedef);
        await cikis.WriteAsync(Imza, ct);
        await cikis.WriteAsync(tuz, ct);
        await cikis.WriteAsync(BitConverter.GetBytes(Iterasyon), ct);

        // Önce sıkıştır (bellekte değil, geçici dosyada), sonra parça parça şifrele.
        var gecici = hedef + ".gz.tmp";
        try
        {
            await using (var kaynakAkis = File.OpenRead(kaynak))
            await using (var gz = new GZipStream(File.Create(gecici), CompressionLevel.SmallestSize))
                await kaynakAkis.CopyToAsync(gz, ct);

            using var aes = new AesGcm(anahtar, 16);
            await using var giris = File.OpenRead(gecici);
            var tampon = new byte[Parca];
            var sifreli = new byte[Parca];
            var etiket = new byte[16];
            int okunan;
            while ((okunan = await giris.ReadAtLeastAsync(tampon, Parca, throwOnEndOfStream: false, ct)) > 0)
            {
                var nonce = RandomNumberGenerator.GetBytes(12);
                aes.Encrypt(nonce, tampon.AsSpan(0, okunan), sifreli.AsSpan(0, okunan), etiket);
                await cikis.WriteAsync(BitConverter.GetBytes(okunan), ct);
                await cikis.WriteAsync(nonce, ct);
                await cikis.WriteAsync(etiket, ct);
                await cikis.WriteAsync(sifreli.AsMemory(0, okunan), ct);
            }
        }
        finally
        {
            File.Delete(gecici);
        }
    }

    public static async Task CozAsync(string kaynak, string hedef, string parola, CancellationToken ct = default)
    {
        await using var giris = File.OpenRead(kaynak);
        var imza = new byte[Imza.Length];
        await giris.ReadExactlyAsync(imza, ct);
        if (!imza.SequenceEqual(Imza)) throw new InvalidDataException("Geçerli bir Ozaylar yedek dosyası değil.");
        var tuz = new byte[16];
        await giris.ReadExactlyAsync(tuz, ct);
        var itBuf = new byte[4];
        await giris.ReadExactlyAsync(itBuf, ct);
        var anahtar = Anahtar(parola, tuz, BitConverter.ToInt32(itBuf));

        var gecici = hedef + ".gz.tmp";
        try
        {
            using (var aes = new AesGcm(anahtar, 16))
            await using (var gz = File.Create(gecici))
            {
                var uzBuf = new byte[4];
                var nonce = new byte[12];
                var etiket = new byte[16];
                while (await giris.ReadAtLeastAsync(uzBuf, 4, throwOnEndOfStream: false, ct) == 4)
                {
                    var uz = BitConverter.ToInt32(uzBuf);
                    if (uz is <= 0 or > Parca) throw new InvalidDataException("Bozuk yedek dosyası.");
                    await giris.ReadExactlyAsync(nonce, ct);
                    await giris.ReadExactlyAsync(etiket, ct);
                    var sifreli = new byte[uz];
                    await giris.ReadExactlyAsync(sifreli, ct);
                    var acik = new byte[uz];
                    try { aes.Decrypt(nonce, sifreli, etiket, acik); }
                    catch (AuthenticationTagMismatchException) { throw new CryptographicException("Parola hatalı veya dosya bozulmuş."); }
                    await gz.WriteAsync(acik, ct);
                }
            }
            await using var gzOku = new GZipStream(File.OpenRead(gecici), CompressionMode.Decompress);
            await using var cikis = File.Create(hedef);
            await gzOku.CopyToAsync(cikis, ct);
        }
        finally
        {
            File.Delete(gecici);
        }
    }

    private static byte[] Anahtar(string parola, byte[] tuz, int iterasyon) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(parola), tuz, iterasyon, HashAlgorithmName.SHA256, 32);
}
