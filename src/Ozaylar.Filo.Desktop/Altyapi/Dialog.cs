using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Serilog;
using UiMessageBox = Wpf.Ui.Controls.MessageBox;
using UiResult = Wpf.Ui.Controls.MessageBoxResult;

namespace Ozaylar.Filo.Desktop.Altyapi;

public static class Dialog
{
    public static Window? Sahip =>
        System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? System.Windows.Application.Current?.MainWindow;

    private static UiMessageBox Kutu(string baslik, string mesaj) => new()
    {
        Title = baslik,
        Content = new TextBlock { Text = mesaj, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
        CloseButtonText = "Tamam",
        Owner = Sahip is { IsLoaded: true } s ? s : null,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };

    public static Task Bilgi(string mesaj, string baslik = "Bilgi") => GosterAsync(baslik, mesaj);

    public static Task Hata(string mesaj, string baslik = "Hata") => GosterAsync(baslik, mesaj);

    private static async Task GosterAsync(string baslik, string mesaj)
    {
        try
        {
            await Kutu(baslik, mesaj).ShowDialogAsync();
        }
        catch (Exception ex)
        {
            // Fluent mesaj kutusu açılamazsa (ör. sahip pencere yok) standart kutuya düş.
            Log.Warning(ex, "Mesaj kutusu gösterilemedi");
            System.Windows.MessageBox.Show(mesaj, baslik);
        }
    }

    public static async Task<bool> Onay(string mesaj, string baslik = "Onay", string evet = "Evet", string hayir = "Vazgeç")
    {
        var k = Kutu(baslik, mesaj);
        k.PrimaryButtonText = evet;
        k.CloseButtonText = hayir;
        return await k.ShowDialogAsync() == UiResult.Primary;
    }

    /// <summary>Tek satırlık metin sorar (ör. iptal nedeni). Vazgeçilirse null.</summary>
    public static async Task<string?> MetinIste(string soru, string baslik, string? varsayilan = null, bool zorunlu = true)
    {
        var tb = new Wpf.Ui.Controls.TextBox { Text = varsayilan ?? "", MinWidth = 380, Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = soru, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(tb);
        var k = new UiMessageBox
        {
            Title = baslik, Content = panel, PrimaryButtonText = "Tamam", CloseButtonText = "Vazgeç",
            Owner = Sahip is { IsLoaded: true } s ? s : null, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        k.Loaded += (_, _) => tb.Focus();
        while (true)
        {
            if (await k.ShowDialogAsync() != UiResult.Primary) return null;
            if (!zorunlu || !string.IsNullOrWhiteSpace(tb.Text)) return tb.Text.Trim();
            await Hata("Bu alan boş bırakılamaz.");
            k = new UiMessageBox { Title = baslik, Content = panel, PrimaryButtonText = "Tamam", CloseButtonText = "Vazgeç", Owner = k.Owner };
        }
    }

    /// <summary>Hataları kullanıcıya anlaşılır biçimde gösterir; beklenmeyenleri loglar.</summary>
    public static Task HataGoster(Exception ex)
    {
        Log.Warning(ex, "Kullanıcıya hata gösterildi");
        switch (ex)
        {
            case DomainException or EszamanlilikException:
                return Hata(ex.Message, "İşlem yapılamadı");
            case YetkiYokException:
                return Hata(ex.Message, "Yetki");
            case UyariVarException u:
                return Hata(string.Join(Environment.NewLine, u.Uyarilar), "Uyarı");
            case Microsoft.Data.SqlClient.SqlException or DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException }:
                Log.Error(ex, "Veritabanı hatası");
                return Hata("Veritabanı işlemi başarısız oldu. Sunucu bağlantısını kontrol edin.\n\nAyrıntı: " + (ex.InnerException ?? ex).Message, "Veritabanı");
            default:
                Log.Error(ex, "Beklenmeyen hata");
                return Hata("Beklenmeyen bir hata oluştu. Ayrıntılar log dosyasına yazıldı.\n\n" + ex.Message);
        }
    }

    /// <summary>
    /// Servis çağrısını hata yönetimiyle çalıştırır. Servis onay gerektiren uyarı fırlatırsa kullanıcıya sorar ve
    /// <paramref name="islem"/>'i onaylı (true) olarak tekrar çağırır. Başarılıysa true döner.
    /// </summary>
    public static async Task<bool> Calistir(Func<bool, Task> islem)
    {
        try
        {
            await islem(false);
            return true;
        }
        catch (UyariVarException u)
        {
            var mesaj = "Aşağıdaki uyarılar var:\n\n• " + string.Join("\n• ", u.Uyarilar) + "\n\nYine de devam edilsin mi?";
            if (!await Onay(mesaj, "Uyarı", "Devam et")) return false;
            try
            {
                await islem(true);
                return true;
            }
            catch (Exception ex)
            {
                await HataGoster(ex);
                return false;
            }
        }
        catch (Exception ex)
        {
            await HataGoster(ex);
            return false;
        }
    }

    public static Task<bool> Calistir(Func<Task> islem) => Calistir(_ => islem());
}
