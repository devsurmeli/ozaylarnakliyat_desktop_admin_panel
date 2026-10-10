using System.IO;
using System.Text.Json;
using Ozaylar.Filo.Infrastructure.Veri;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace Ozaylar.Filo.Desktop.Altyapi;

/// <summary>İstemci ayarları (gizli olmayan): %ProgramData%\OzaylarFilo\istemci.json</summary>
public sealed class IstemciAyarlari
{
    /// <summary>Güncelleme paketlerinin bulunduğu ağ paylaşımı, ör. \\SUNUCU\OzaylarFiloGuncelleme</summary>
    public string? GuncellemeKlasoru { get; set; }

    private static string Yol => Path.Combine(BaglantiDeposu.Klasor, "istemci.json");

    public static IstemciAyarlari Oku()
    {
        try { return File.Exists(Yol) ? JsonSerializer.Deserialize<IstemciAyarlari>(File.ReadAllText(Yol)) ?? new() : new(); }
        catch { return new(); }
    }

    public void Yaz()
    {
        Directory.CreateDirectory(BaglantiDeposu.Klasor);
        File.WriteAllText(Yol, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>Velopack ile ağ paylaşımından güncelleme. Kurulu değilse (geliştirme) sessizce atlanır.</summary>
public static class Guncelleyici
{
    private static UpdateManager? Yonetici()
    {
        var klasor = IstemciAyarlari.Oku().GuncellemeKlasoru;
        if (string.IsNullOrWhiteSpace(klasor) || !Directory.Exists(klasor)) return null;
        var um = new UpdateManager(new SimpleFileSource(new DirectoryInfo(klasor)));
        return um.IsInstalled ? um : null;
    }

    public static async Task SessizKontrolAsync()
    {
        try
        {
            var um = Yonetici();
            if (um is null) return;
            var yeni = await um.CheckForUpdatesAsync();
            if (yeni is null) return;
            await um.DownloadUpdatesAsync(yeni);
            if (await Dialog.Onay($"Yeni sürüm ({yeni.TargetFullRelease.Version}) indirildi. Program şimdi yeniden başlatılsın mı?", "Güncelleme", "Yeniden başlat", "Sonra"))
                um.ApplyUpdatesAndRestart(yeni);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Güncelleme kontrolü başarısız");
        }
    }

    /// <returns>Güncelleme uygulanıp yeniden başlatılıyorsa true.</returns>
    public static async Task<bool> GuncelleVeYenidenBaslatAsync(bool zorunlu)
    {
        try
        {
            var um = Yonetici();
            if (um is null) return false;
            var yeni = await um.CheckForUpdatesAsync();
            if (yeni is null) return false;
            await um.DownloadUpdatesAsync(yeni);
            if (zorunlu) await Dialog.Bilgi("Program güncellenecek ve yeniden başlatılacak.", "Güncelleme");
            um.ApplyUpdatesAndRestart(yeni);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Güncelleme başarısız");
            return false;
        }
    }
}
