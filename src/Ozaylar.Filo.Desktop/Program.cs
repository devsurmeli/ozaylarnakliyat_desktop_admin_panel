using Velopack;

namespace Ozaylar.Filo.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack kurulum/güncelleme kancaları her şeyden önce çalışmalıdır.
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
