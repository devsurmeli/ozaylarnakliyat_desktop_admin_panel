using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Ozaylar.Filo.Infrastructure.Veri;

public sealed record BaglantiBilgisi(
    string Sunucu,
    string Veritabani = BaglantiBilgisi.VarsayilanVeritabani,
    string? Kullanici = null,
    string? Sifre = null,
    bool WindowsKimlik = false)
{
    public const string VarsayilanVeritabani = "OzaylarFilo";
    public string EkVeritabani => Veritabani + "_Ekler";

    public string AnaBaglanti => Olustur(Veritabani);
    public string EkBaglanti => Olustur(EkVeritabani);
    public string MasterBaglanti => Olustur("master");

    private string Olustur(string db)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = Sunucu,
            InitialCatalog = db,
            // LAN içinde kendinden imzalı sertifika: trafik şifrelenir.
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            ConnectTimeout = 10,
            ApplicationName = "OzaylarFilo",
        };
        if (WindowsKimlik) b.IntegratedSecurity = true;
        else
        {
            b.UserID = Kullanici;
            b.Password = Sifre;
        }
        return b.ConnectionString;
    }
}

/// <summary>
/// Bağlantı bilgisi istemcide %ProgramData%\OzaylarFilo\baglanti.dat dosyasında DPAPI (LocalMachine) ile şifreli saklanır.
/// Dosya başka bir bilgisayara kopyalanırsa çözülemez.
/// </summary>
public static class BaglantiDeposu
{
    private static readonly byte[] Entropy = "OzaylarFilo.v1"u8.ToArray();

    public static string Klasor => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OzaylarFilo");
    public static string DosyaYolu => Path.Combine(Klasor, "baglanti.dat");

    public static BaglantiBilgisi? Oku(string? yol = null)
    {
        yol ??= DosyaYolu;
        if (!File.Exists(yol)) return null;
        try
        {
            var acik = ProtectedData.Unprotect(File.ReadAllBytes(yol), Entropy, DataProtectionScope.LocalMachine);
            return JsonSerializer.Deserialize<BaglantiBilgisi>(Encoding.UTF8.GetString(acik));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static void Yaz(BaglantiBilgisi bilgi, string? yol = null)
    {
        yol ??= DosyaYolu;
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(bilgi));
        File.WriteAllBytes(yol, ProtectedData.Protect(json, Entropy, DataProtectionScope.LocalMachine));
    }
}
