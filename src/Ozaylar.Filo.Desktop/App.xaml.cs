using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ozaylar.Filo.Application;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Infrastructure;
using Ozaylar.Filo.Infrastructure.Veri;
using Serilog;

namespace Ozaylar.Filo.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    public static IServiceProvider Servisler { get; private set; } = null!;
    public static T Al<T>() where T : notnull => Servisler.GetRequiredService<T>();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var tr = new CultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = tr;
        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = tr;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(tr.IetfLanguageTag)));
        Tema.Yukle();

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OzaylarFilo", "Log", "istemci-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();
        DispatcherUnhandledException += OnDispatcherHata;
        AppDomain.CurrentDomain.UnhandledException += (_, a) => Log.Fatal(a.ExceptionObject as Exception, "Yakalanmayan hata");
        TaskScheduler.UnobservedTaskException += (_, a) => { Log.Error(a.Exception, "Gözlenmeyen görev hatası"); a.SetObserved(); };

        try
        {
            await BaslatAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Başlatma hatası");
            await Dialog.Hata("Program başlatılamadı: " + ex.Message);
            Shutdown(1);
        }
    }

    private async Task BaslatAsync()
    {
        while (true)
        {
            var baglanti = BaglantiDeposu.Oku();
            if (baglanti is null)
            {
                baglanti = KurulumPenceresi.Ac(null);
                if (baglanti is null) { Shutdown(); return; }
            }

            _host?.Dispose();
            _host = HostOlustur(baglanti);
            Servisler = _host.Services;

            var sema = await Al<SemaKontrol>().KontrolAsync();
            switch (sema.Durum)
            {
                case SemaDurumu.BaglantiYok:
                    if (await Dialog.Onay(sema.Mesaj + "\n\nBağlantı ayarlarını değiştirmek ister misiniz?", "Bağlantı", "Ayarları aç", "Kapat")
                        && KurulumPenceresi.Ac(baglanti) is not null)
                        continue;
                    Shutdown();
                    return;
                case SemaDurumu.IstemciEski:
                    if (await Guncelleyici.GuncelleVeYenidenBaslatAsync(zorunlu: true)) return;
                    await Dialog.Hata(sema.Mesaj);
                    Shutdown();
                    return;
                case SemaDurumu.VeritabaniEski:
                    await Dialog.Hata(sema.Mesaj);
                    Shutdown();
                    return;
            }

            // Arka planda güncelleme kontrolü (varsa kullanıcıya sorulur).
            _ = Guncelleyici.SessizKontrolAsync();

            if (!GirisPenceresi.Ac()) { Shutdown(); return; }
            var ana = Al<AnaPencere>();
            MainWindow = ana;
            ana.Closed += (_, _) => Shutdown();
            ana.Show();
            return;
        }
    }

    private static IHost HostOlustur(BaglantiBilgisi baglanti)
    {
        var b = Host.CreateApplicationBuilder();
        b.Services.AddSerilog();
        b.Services.AddFiloApplication();
        b.Services.AddFiloInfrastructure(baglanti);
        Kayitlar.Ekle(b.Services);
        return b.Build();
    }

    private async void OnDispatcherHata(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        await Dialog.HataGoster(e.Exception);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
