using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Ozaylar.Filo.Infrastructure.Veri;

namespace Ozaylar.Filo.Infrastructure.Yedek;

public sealed class YedekAyarlari
{
    /// <summary>SQL Server'ın yazabildiği yerel klasör (SQL servis hesabına yazma izni verilmeli).</summary>
    public string YerelKlasor { get; set; } = @"C:\OzaylarFilo\Yedek";
    /// <summary>İkinci kopya hedefi: NAS paylaşımı, USB disk veya OneDrive/Google Drive klasörü. Boşsa kopyalanmaz.</summary>
    public string? IkinciHedef { get; set; }
    /// <summary>Şifreli kopya parolası. Kaybedilirse yedek açılamaz — güvenli bir yerde saklanmalıdır.</summary>
    public string? SifrelemeParolasi { get; set; }
    public int TamYedekSaati { get; set; } = 2;
    public int FarkYedekAraligiSaat { get; set; } = 2;
    public int FarkYedekBaslangic { get; set; } = 8;
    public int FarkYedekBitis { get; set; } = 20;
    public int GunlukSaklamaGun { get; set; } = 14;
    public int AylikSaklamaAy { get; set; } = 12;
}

public enum YedekTuru { Tam, Fark }

/// <summary>
/// SQL Express'te SQL Agent olmadığı için yedekleme bu servisle yapılır. Recovery model SIMPLE:
/// gece tam yedek + gün içinde fark (differential) yedekleri. Ana ve ek veritabanları birlikte yedeklenir.
/// </summary>
public sealed class YedekServisi(BaglantiBilgisi yonetici, YedekAyarlari ayar, ILogger<YedekServisi> log)
{
    public async Task<IReadOnlyList<string>> YedekAlAsync(YedekTuru tur, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ayar.YerelKlasor);
        var damga = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        var dosyalar = new List<string>();
        foreach (var db in new[] { yonetici.Veritabani, yonetici.EkVeritabani })
        {
            if (tur == YedekTuru.Fark && !await TamYedekVarAsync(db, ct))
            {
                log.LogInformation("{Db} için tam yedek yok; fark yerine tam yedek alınıyor.", db);
                tur = YedekTuru.Tam;
            }
            var dosya = Path.Combine(ayar.YerelKlasor, $"{db}_{(tur == YedekTuru.Tam ? "TAM" : "FARK")}_{damga}.bak");
            var sql = $"BACKUP DATABASE [{db}] TO DISK = @d WITH INIT, CHECKSUM, NAME = @n{(tur == YedekTuru.Fark ? ", DIFFERENTIAL" : "")}";
            await using var c = new SqlConnection(yonetici.MasterBaglanti);
            await c.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 3600 };
            cmd.Parameters.AddWithValue("@d", dosya);
            cmd.Parameters.AddWithValue("@n", $"Ozaylar {tur} {damga}");
            await cmd.ExecuteNonQueryAsync(ct);
            await using var dogrula = new SqlCommand("RESTORE VERIFYONLY FROM DISK = @d WITH CHECKSUM", c) { CommandTimeout = 3600 };
            dogrula.Parameters.AddWithValue("@d", dosya);
            await dogrula.ExecuteNonQueryAsync(ct);
            dosyalar.Add(dosya);
            log.LogInformation("Yedek alındı ve doğrulandı: {Dosya} ({Boyut:N0} KB)", dosya, new FileInfo(dosya).Length / 1024);
        }

        await IkinciHedefeKopyalaAsync(dosyalar, ct);
        await SonYedekYazAsync(ct);
        Temizle();
        return dosyalar;
    }

    private async Task<bool> TamYedekVarAsync(string db, CancellationToken ct)
    {
        await using var c = new SqlConnection(yonetici.MasterBaglanti);
        await c.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = @db AND type = 'D'", c);
        cmd.Parameters.AddWithValue("@db", db);
        return (int)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    private async Task IkinciHedefeKopyalaAsync(List<string> dosyalar, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ayar.IkinciHedef)) return;
        try
        {
            Directory.CreateDirectory(ayar.IkinciHedef);
            foreach (var d in dosyalar)
            {
                var hedef = Path.Combine(ayar.IkinciHedef, Path.GetFileName(d));
                if (!string.IsNullOrEmpty(ayar.SifrelemeParolasi))
                    await YedekSifreleme.SifreleAsync(d, hedef + ".ozyd", ayar.SifrelemeParolasi, ct);
                else
                    File.Copy(d, hedef, overwrite: true);
            }
            log.LogInformation("Yedekler ikinci hedefe kopyalandı: {Hedef}", ayar.IkinciHedef);
        }
        catch (Exception ex)
        {
            // İkinci kopya başarısız olsa da yerel yedek geçerlidir; uyarı loglanır.
            log.LogError(ex, "Yedek ikinci hedefe kopyalanamadı: {Hedef}", ayar.IkinciHedef);
        }
    }

    private async Task SonYedekYazAsync(CancellationToken ct)
    {
        await using var c = new SqlConnection(yonetici.AnaBaglanti);
        await c.OpenAsync(ct);
        await using var cmd = new SqlCommand("UPDATE dbo.Ayar SET Deger = @d WHERE Anahtar = 'Yedek.SonBasarili'", c);
        cmd.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Saklama: son N gün tüm yedekler; daha eskilerden yalnızca her ayın ilk tam yedeği (M ay).</summary>
    private void Temizle()
    {
        foreach (var klasor in new[] { ayar.YerelKlasor, ayar.IkinciHedef }.Where(k => !string.IsNullOrWhiteSpace(k) && Directory.Exists(k)))
        {
            var dosyalar = Directory.GetFiles(klasor!, "*.bak*").Select(f => new FileInfo(f)).OrderBy(f => f.CreationTime).ToList();
            var gunSiniri = DateTime.Now.AddDays(-ayar.GunlukSaklamaGun);
            var aySiniri = DateTime.Now.AddMonths(-ayar.AylikSaklamaAy);
            var aylikTutulan = dosyalar.Where(f => f.Name.Contains("_TAM_"))
                .GroupBy(f => (f.CreationTime.Year, f.CreationTime.Month, Db: f.Name.Split("_TAM_")[0]))
                .Select(g => g.First().FullName).ToHashSet();
            foreach (var f in dosyalar.Where(f => f.CreationTime < gunSiniri))
            {
                if (aylikTutulan.Contains(f.FullName) && f.CreationTime >= aySiniri) continue;
                try { f.Delete(); log.LogInformation("Eski yedek silindi: {Dosya}", f.Name); }
                catch (Exception ex) { log.LogWarning(ex, "Yedek silinemedi: {Dosya}", f.Name); }
            }
        }
    }
}
