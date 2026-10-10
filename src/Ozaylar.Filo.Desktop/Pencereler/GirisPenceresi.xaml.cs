using System.IO;
using System.Windows;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Pencereler;

public partial class GirisPenceresi : FluentWindow
{
    private static readonly string SonKullaniciDosyasi =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OzaylarFilo", "son-kullanici.txt");

    private readonly OturumServisi _oturum = App.Al<OturumServisi>();

    public GirisPenceresi()
    {
        InitializeComponent();
        Surum.Text = "Sürüm " + typeof(App).Assembly.GetName().Version?.ToString(3);
        try { if (File.Exists(SonKullaniciDosyasi)) Kullanici.Text = File.ReadAllText(SonKullaniciDosyasi).Trim(); } catch { /* yok say */ }
        Loaded += async (_, _) =>
        {
            if (string.IsNullOrEmpty(Kullanici.Text)) Kullanici.Focus(); else Sifre.Focus();
            try { Firma.Text = await App.Al<AyarServisi>().OkuAsync("Firma.Unvan") ?? ""; } catch { /* bağlantı hatası girişte görünür */ }
        };
    }

    public static bool Ac() => new GirisPenceresi().ShowDialog() == true;

    private async void Giris_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Kullanici.Text) || string.IsNullOrEmpty(Sifre.Password))
        {
            Goster("Kullanıcı adı ve şifre girin.");
            return;
        }
        GirisBtn.IsEnabled = false;
        try
        {
            var sonuc = await _oturum.GirisAsync(Kullanici.Text, Sifre.Password);
            if (!sonuc.Basarili)
            {
                Goster(sonuc.Mesaj ?? "Giriş başarısız.");
                Sifre.Password = "";
                Sifre.Focus();
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SonKullaniciDosyasi)!);
                File.WriteAllText(SonKullaniciDosyasi, Kullanici.Text.Trim());
            }
            catch { /* kritik değil */ }

            if (sonuc.SifreDegistirmeli && !await SifreDegistir.AcAsync(zorunlu: true, mevcutSifre: Sifre.Password))
            {
                _oturum.Cikis();
                Goster("İlk girişte şifrenizi değiştirmeniz gerekiyor.");
                return;
            }
            DialogResult = true;
        }
        catch (Exception ex)
        {
            await Dialog.HataGoster(ex);
        }
        finally
        {
            GirisBtn.IsEnabled = true;
        }
    }

    private void Goster(string mesaj)
    {
        Mesaj.Message = mesaj;
        Mesaj.IsOpen = true;
        Mesaj.Visibility = Visibility.Visible;
    }
}

public sealed class SifreModel
{
    public string? Eski { get; set; }
    public string? Yeni { get; set; }
    public string? Tekrar { get; set; }
}

public static class SifreDegistir
{
    public static Task<bool> AcAsync(bool zorunlu, string? mevcutSifre = null)
    {
        var m = new SifreModel { Eski = mevcutSifre };
        var alanlar = new List<FormAlani>();
        if (mevcutSifre is null) alanlar.Add(new FormAlani { Etiket = "Mevcut şifre", Yol = nameof(SifreModel.Eski), Tip = AlanTipi.Sifre, Zorunlu = true });
        alanlar.Add(new FormAlani { Etiket = "Yeni şifre", Yol = nameof(SifreModel.Yeni), Tip = AlanTipi.Sifre, Zorunlu = true, Ipucu = "En az 8 karakter, harf ve rakam" });
        alanlar.Add(new FormAlani { Etiket = "Yeni şifre (tekrar)", Yol = nameof(SifreModel.Tekrar), Tip = AlanTipi.Sifre, Zorunlu = true });
        var ok = FormPenceresi.Ac(zorunlu ? "Şifrenizi değiştirin" : "Şifre değiştir", m, alanlar, async _ =>
        {
            if (m.Yeni != m.Tekrar) throw new Domain.Common.DomainException("Yeni şifreler eşleşmiyor.");
            await App.Al<OturumServisi>().SifreDegistirAsync(m.Eski ?? "", m.Yeni ?? "");
        });
        return Task.FromResult(ok);
    }
}
