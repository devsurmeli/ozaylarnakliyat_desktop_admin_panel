using System.Diagnostics;
using System.IO;

namespace Ozaylar.Filo.Desktop.Altyapi;

/// <summary>Üretilen PDF belgeyi geçici klasöre yazıp varsayılan görüntüleyicide açar (oradan yazdırılır/kaydedilir).</summary>
public static class Belge
{
    public static async Task AcAsync(string ad, Func<byte[]> uret)
    {
        var veri = await Task.Run(uret);
        var klasor = Path.Combine(Path.GetTempPath(), "OzaylarFilo", "Belgeler");
        Directory.CreateDirectory(klasor);
        var temiz = string.Concat(ad.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        var dosya = Path.Combine(klasor, $"{temiz} {DateTime.Now:yyyyMMdd-HHmmss}.pdf");
        await File.WriteAllBytesAsync(dosya, veri);
        Process.Start(new ProcessStartInfo(dosya) { UseShellExecute = true });
    }
}
