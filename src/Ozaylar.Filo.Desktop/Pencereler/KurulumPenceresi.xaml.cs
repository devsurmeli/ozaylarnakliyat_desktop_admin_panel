using System.Windows;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Infrastructure.Veri;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Pencereler;

/// <summary>İlk açılışta (veya bağlantı kurulamadığında) sunucu bağlantı bilgisini alır ve DPAPI ile şifreli kaydeder.</summary>
public partial class KurulumPenceresi : FluentWindow
{
    public BaglantiBilgisi? Sonuc_ { get; private set; }

    public KurulumPenceresi(BaglantiBilgisi? mevcut)
    {
        InitializeComponent();
        if (mevcut is not null)
        {
            Sunucu.Text = mevcut.Sunucu;
            Veritabani.Text = mevcut.Veritabani;
            Windows.IsChecked = mevcut.WindowsKimlik;
            Kullanici.Text = mevcut.Kullanici ?? "filo_app";
            Sifre.Password = mevcut.Sifre ?? "";
        }
        Guncelleme.Text = IstemciAyarlari.Oku().GuncellemeKlasoru ?? "";
        Windows_Degisti(this, new RoutedEventArgs());
    }

    public static BaglantiBilgisi? Ac(BaglantiBilgisi? mevcut)
    {
        var p = new KurulumPenceresi(mevcut);
        return p.ShowDialog() == true ? p.Sonuc_ : null;
    }

    private BaglantiBilgisi Oku() => new(
        Sunucu.Text.Trim(),
        string.IsNullOrWhiteSpace(Veritabani.Text) ? BaglantiBilgisi.VarsayilanVeritabani : Veritabani.Text.Trim(),
        Windows.IsChecked == true ? null : Kullanici.Text.Trim(),
        Windows.IsChecked == true ? null : Sifre.Password,
        Windows.IsChecked == true);

    private void Windows_Degisti(object sender, RoutedEventArgs e) =>
        SqlPanel.Visibility = Windows.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;

    private async Task<bool> TestAsync()
    {
        if (string.IsNullOrWhiteSpace(Sunucu.Text))
        {
            Goster("Sunucu adresi girin.", InfoBarSeverity.Warning);
            return false;
        }
        TestBtn.IsEnabled = KaydetBtn.IsEnabled = false;
        Goster("Bağlanılıyor...", InfoBarSeverity.Informational);
        try
        {
            var hata = await SemaKontrol.BaglantiTestAsync(Oku());
            if (hata is null)
            {
                Goster("Bağlantı başarılı.", InfoBarSeverity.Success);
                return true;
            }
            Goster("Bağlanılamadı: " + hata, InfoBarSeverity.Error);
            return false;
        }
        finally
        {
            TestBtn.IsEnabled = KaydetBtn.IsEnabled = true;
        }
    }

    private void Goster(string mesaj, InfoBarSeverity s)
    {
        Sonuc.Message = mesaj;
        Sonuc.Severity = s;
        Sonuc.IsOpen = true;
        Sonuc.Visibility = Visibility.Visible;
    }

    private async void Test_Click(object sender, RoutedEventArgs e) => await TestAsync();

    private async void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        if (!await TestAsync()) return;
        try
        {
            var b = Oku();
            BaglantiDeposu.Yaz(b);
            new IstemciAyarlari { GuncellemeKlasoru = string.IsNullOrWhiteSpace(Guncelleme.Text) ? null : Guncelleme.Text.Trim() }.Yaz();
            Sonuc_ = b;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Goster("Ayarlar kaydedilemedi: " + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Vazgec_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
