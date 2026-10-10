using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Infrastructure.Veri;
using Ozaylar.Filo.Infrastructure.Yedek;

namespace Ozaylar.Filo.Sunucu;

/// <summary>
/// Sunucu bilgisayardaki tek arka plan işçisi:
/// 1) Açılışta bekleyen migration varsa önce yedek alır, sonra uygular; izin/veri hazırlığını yapar.
/// 2) Dakikada bir zamanlamayı kontrol eder: gece tam yedek, gün içinde fark yedeği, hafta içi TCMB kurları.
/// </summary>
public sealed class SunucuIsci(
    VeritabaniHazirlik hazirlik,
    YedekServisi yedek,
    YedekAyarlari ayar,
    TcmbKurServisi tcmb,
    DovizServisi doviz,
    ILogger<SunucuIsci> log) : BackgroundService
{
    private DateOnly _sonTamYedek;
    private DateTime _sonFarkYedek;
    private DateOnly _sonKur;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await HazirlaAsync(ct);
        using var zamanlayici = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await ZamanlanmisIslerAsync(DateTime.Now, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Zamanlanmış iş hatası");
            }
        } while (await zamanlayici.WaitForNextTickAsync(ct));
    }

    private async Task HazirlaAsync(CancellationToken ct)
    {
        for (var deneme = 1; ; deneme++)
        {
            try
            {
                var bekleyen = await hazirlik.BekleyenMigrationlarAsync(ct);
                if (bekleyen.Count > 0)
                {
                    log.LogInformation("Bekleyen veritabanı güncellemeleri: {Liste}", string.Join(", ", bekleyen));
                    try { await yedek.YedekAlAsync(YedekTuru.Tam, ct); }
                    catch (Exception ex) { log.LogWarning(ex, "Güncelleme öncesi yedek alınamadı (yeni kurulum olabilir)."); }
                    await hazirlik.MigrateAsync(ct);
                    log.LogInformation("Veritabanı güncellendi.");
                }
                await hazirlik.VerileriHazirlaAsync(ct);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && deneme < 30)
            {
                // SQL Server servisi bilgisayar açılışında henüz hazır olmayabilir.
                log.LogWarning(ex, "Veritabanı hazırlığı başarısız (deneme {Deneme}); 20 sn sonra tekrar denenecek.", deneme);
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
            }
        }
    }

    private async Task ZamanlanmisIslerAsync(DateTime simdi, CancellationToken ct)
    {
        var bugun = DateOnly.FromDateTime(simdi);
        if (simdi.Hour == ayar.TamYedekSaati && _sonTamYedek != bugun)
        {
            _sonTamYedek = bugun;
            await yedek.YedekAlAsync(YedekTuru.Tam, ct);
        }
        else if (simdi.Hour >= ayar.FarkYedekBaslangic && simdi.Hour <= ayar.FarkYedekBitis
                 && (simdi - _sonFarkYedek).TotalHours >= ayar.FarkYedekAraligiSaat && simdi.Minute < 5)
        {
            _sonFarkYedek = simdi;
            await yedek.YedekAlAsync(YedekTuru.Fark, ct);
        }

        // TCMB kurları hafta içi 15:30 civarı ilan edilir.
        if (simdi.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && simdi.Hour >= 16 && _sonKur != bugun)
        {
            try
            {
                var (tarih, kurlar) = await tcmb.GetirAsync(ct);
                await doviz.KaydetAsync(tarih, kurlar, ct);
                _sonKur = bugun;
                log.LogInformation("TCMB kurları kaydedildi: {Tarih} {Kurlar}", tarih, string.Join(", ", kurlar.Select(k => $"{k.Pb}={k.Satis}")));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "TCMB kurları alınamadı (internet bağlantısı?)");
                _sonKur = bugun; // aynı gün tekrar tekrar denemesin; ertesi gün yeniden denenir
            }
        }
    }
}
