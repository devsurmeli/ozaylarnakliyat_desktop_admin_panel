using System.Globalization;
using Ozaylar.Filo.Application;
using Ozaylar.Filo.Infrastructure;
using Ozaylar.Filo.Infrastructure.Guvenlik;
using Ozaylar.Filo.Infrastructure.Veri;
using Ozaylar.Filo.Infrastructure.Yedek;
using Ozaylar.Filo.Sunucu;
using Serilog;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("tr-TR");

var logKlasor = Path.Combine(BaglantiDeposu.Klasor, "Log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.File(Path.Combine(logKlasor, "sunucu-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 60)
    .WriteTo.Console()
    .CreateLogger();

try
{
    // Komut satırı modları (kurulum programı ve yönetici için).
    if (args.Length > 0 && args[0] is "kur" or "yedek" or "coz" or "yardim" or "ornek-veri")
        return await Komutlar.CalistirAsync(args);

    var yonetici = SunucuBaglanti.Oku()
                   ?? throw new InvalidOperationException($"Sunucu bağlantı bilgisi yok ({SunucuBaglanti.DosyaYolu}). Önce 'Ozaylar.Filo.Sunucu.exe kur ...' çalıştırın.");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddWindowsService(o => o.ServiceName = "OzaylarFiloSunucu");
    builder.Services.AddSerilog();
    builder.Services.AddFiloApplication();
    builder.Services.AddFiloInfrastructure(yonetici);
    builder.Services.AddSingleton(builder.Configuration.GetSection("Yedek").Get<YedekAyarlari>() ?? new YedekAyarlari());
    builder.Services.AddSingleton<YedekServisi>();
    builder.Services.AddHttpClient<TcmbKurServisi>(c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddSingleton(sp => new VeritabaniHazirlik(yonetici, new SifreHasher()));
    builder.Services.AddHostedService<SunucuIsci>();

    await builder.Build().RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Sunucu servisi başlatılamadı");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
