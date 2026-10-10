using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Ozaylar.Filo.Infrastructure.Guvenlik;
using Ozaylar.Filo.Infrastructure.Veri;
using Ozaylar.Filo.Infrastructure.Yedek;

namespace Ozaylar.Filo.Sunucu;

/// <summary>
/// Sunucu yönetici bağlantısı (migration/yedek için yetkili). %ProgramData%\OzaylarFilo\sunucu.dat, DPAPI (LocalMachine) ile şifreli.
/// İstemciler bu bilgiyi asla görmez; istemciler kısıtlı 'filo_app' login'i kullanır.
/// </summary>
public static class SunucuBaglanti
{
    public static string DosyaYolu => Path.Combine(BaglantiDeposu.Klasor, "sunucu.dat");
    public static BaglantiBilgisi? Oku() => BaglantiDeposu.Oku(DosyaYolu);
    public static void Yaz(BaglantiBilgisi b) => BaglantiDeposu.Yaz(b, DosyaYolu);
}

public static class Komutlar
{
    public static async Task<int> CalistirAsync(string[] args)
    {
        var p = Parametreler(args.Skip(1));
        switch (args[0])
        {
            case "kur":
                return await KurAsync(p);
            case "yedek":
            {
                var b = SunucuBaglanti.Oku() ?? throw new InvalidOperationException("Önce 'kur' çalıştırın.");
                var ayar = AyarOku();
                var dosyalar = await new YedekServisi(b, ayar, NullLogger<YedekServisi>.Instance).YedekAlAsync(YedekTuru.Tam);
                Console.WriteLine("Yedek alındı:\n  " + string.Join("\n  ", dosyalar));
                return 0;
            }
            case "ornek-veri":
            {
                var b = SunucuBaglanti.Oku() ?? throw new InvalidOperationException("Önce 'kur' çalıştırın.");
                await OrnekVeri.OlusturAsync(b);
                Console.WriteLine("Örnek veri yüklendi.");
                return 0;
            }
            case "coz":
                if (args.Length < 4) { Yardim(); return 2; }
                await YedekSifreleme.CozAsync(args[1], args[2], args[3]);
                Console.WriteLine($"Çözüldü: {args[2]} — SQL Server Management Studio > Restore Database ile geri yükleyebilirsiniz.");
                return 0;
            default:
                Yardim();
                return 0;
        }
    }

    /// <summary>
    /// Kurulum: yönetici bağlantısını kaydeder, veritabanlarını oluşturur/günceller, başlangıç verilerini hazırlar,
    /// istemciler için kısıtlı 'filo_app' login'ini oluşturur.
    /// Örnek: kur --sql localhost\SQLEXPRESS --windows --app-sifre "Guclu!Sifre123"
    /// </summary>
    private static async Task<int> KurAsync(Dictionary<string, string?> p)
    {
        var sunucu = p.GetValueOrDefault("sql") ?? @"localhost\SQLEXPRESS";
        var db = p.GetValueOrDefault("veritabani") ?? BaglantiBilgisi.VarsayilanVeritabani;
        var b = p.ContainsKey("windows")
            ? new BaglantiBilgisi(sunucu, db, WindowsKimlik: true)
            : new BaglantiBilgisi(sunucu, db, p.GetValueOrDefault("sql-kullanici") ?? "sa", p.GetValueOrDefault("sql-sifre"));
        var appSifre = p.GetValueOrDefault("app-sifre");
        if (string.IsNullOrEmpty(appSifre))
        {
            appSifre = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', 'x').Replace('/', 'y') + "!9a";
            Console.WriteLine("Uygulama SQL şifresi otomatik üretildi.");
        }

        var hata = await SemaKontrol.BaglantiTestAsync(b with { Veritabani = "master" });
        if (hata is not null)
        {
            Console.Error.WriteLine($"SQL Server'a bağlanılamadı: {hata}");
            return 3;
        }

        var sadeceWindows = await SadeceWindowsKimlikAsync(b);
        if (sadeceWindows)
        {
            Console.Error.WriteLine("""
                UYARI: SQL Server yalnızca Windows kimlik doğrulamasına açık. Ağdaki istemciler 'filo_app' SQL login'i ile bağlanamaz.
                Çözüm: SQL Server Management Studio > Sunucu Özellikleri > Güvenlik > "SQL Server and Windows Authentication mode"
                seçip SQL Server servisini yeniden başlatın (kurulum programı SQL Express'i bu modda kurar).
                Kurulum devam ediyor; mod değiştirildikten sonra istemciler bağlanabilir.
                """);
        }

        SunucuBaglanti.Yaz(b);
        var h = new VeritabaniHazirlik(b, new SifreHasher());
        Console.WriteLine("Veritabanı oluşturuluyor/güncelleniyor...");
        await h.MigrateAsync();
        await h.VerileriHazirlaAsync();
        await h.UygulamaLoginiOlusturAsync(appSifre);

        // Sunucu bilgisayar da programı kullanır: bu makinenin istemci bağlantısını hazır yaz (kurulum sihirbazı çıkmasın).
        BaglantiDeposu.Yaz(sadeceWindows
            ? new BaglantiBilgisi(sunucu, db, WindowsKimlik: true)
            : new BaglantiBilgisi(sunucu, db, VeritabaniHazirlik.UygulamaLogin, appSifre));

        // İstemci kurulum bilgisi: yönetici bunu istemcilerin ilk açılış sihirbazına girer.
        var bilgi = new StringBuilder()
            .AppendLine("OZAYLAR FİLO — İSTEMCİ BAĞLANTI BİLGİSİ (gizli tutun)")
            .AppendLine($"Sunucu adresi : {IstemciSunucuAdi(sunucu)}")
            .AppendLine($"Veritabanı    : {db}")
            .AppendLine($"SQL kullanıcı : {VeritabaniHazirlik.UygulamaLogin}")
            .AppendLine($"SQL şifresi   : {appSifre}")
            .AppendLine()
            .AppendLine($"İlk uygulama girişi: kullanıcı '{VeritabaniHazirlik.IlkKullaniciAdi}', şifre '{VeritabaniHazirlik.IlkSifre}' (ilk girişte değiştirilmesi zorunludur).");
        var dosya = Path.Combine(BaglantiDeposu.Klasor, "istemci-baglanti-bilgisi.txt");
        await File.WriteAllTextAsync(dosya, bilgi.ToString(), Encoding.UTF8);
        Console.WriteLine(bilgi);
        Console.WriteLine($"Bu bilgi ayrıca şuraya yazıldı: {dosya}");
        return 0;
    }

    private static async Task<bool> SadeceWindowsKimlikAsync(BaglantiBilgisi b)
    {
        await using var c = new Microsoft.Data.SqlClient.SqlConnection(b.MasterBaglanti);
        await c.OpenAsync();
        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS int)", c);
        return (int)(await cmd.ExecuteScalarAsync())! == 1;
    }

    /// <summary>"localhost\SQLEXPRESS" → istemcilerin kullanacağı "BILGISAYARADI\SQLEXPRESS".</summary>
    private static string IstemciSunucuAdi(string sunucu)
    {
        var parca = sunucu.Split('\\', 2);
        var host = parca[0] is "." or "localhost" or "(local)" ? Environment.MachineName : parca[0];
        return parca.Length == 2 ? $"{host}\\{parca[1]}" : host;
    }

    private static YedekAyarlari AyarOku()
    {
        var yol = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(yol)) return new YedekAyarlari();
        using var doc = JsonDocument.Parse(File.ReadAllText(yol));
        return doc.RootElement.TryGetProperty("Yedek", out var y) ? y.Deserialize<YedekAyarlari>() ?? new() : new();
    }

    private static Dictionary<string, string?> Parametreler(IEnumerable<string> args)
    {
        var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        string? anahtar = null;
        foreach (var a in args)
        {
            if (a.StartsWith("--")) { anahtar = a[2..]; d[anahtar] = null; }
            else if (anahtar is not null) { d[anahtar] = a; anahtar = null; }
        }
        return d;
    }

    private static void Yardim() => Console.WriteLine("""
        Ozaylar Filo Sunucu
          (parametresiz)                         Windows servisi olarak çalışır
          kur --sql <sunucu\örnek> [--windows | --sql-kullanici sa --sql-sifre X] [--app-sifre Y] [--veritabani OzaylarFilo]
                                                 Veritabanını kurar/günceller, istemci login'ini oluşturur
          yedek                                  Hemen tam yedek alır
          coz <dosya.ozyd> <hedef.bak> <parola>  Şifreli yedek kopyasını çözer
          ornek-veri                             Deneme için örnek veri yükler (yalnızca boş veritabanı)
        """);
}
