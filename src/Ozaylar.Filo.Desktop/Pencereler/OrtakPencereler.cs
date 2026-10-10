using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Sayfalar;
using Ozaylar.Filo.Desktop.ViewModels;
using Ozaylar.Filo.Domain.Common;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Ozaylar.Filo.Desktop.Pencereler;

/// <summary>Herhangi bir liste VM'ini ayrı (modal) pencerede gösterir.</summary>
public sealed class ListePenceresi : FluentWindow
{
    public ListePenceresi(IListeVM vm, double genislik = 1100, double yukseklik = 680)
    {
        Title = vm.Baslik;
        Width = genislik;
        Height = yukseklik;
        Owner = Dialog.Sahip;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.None;
        SetResourceReference(BackgroundProperty, "Zemin");
        var kok = new DockPanel();
        var tb = new TitleBar { Title = vm.Baslik };
        DockPanel.SetDock(tb, Dock.Top);
        kok.Children.Add(tb);
        kok.Children.Add(new Frame { Content = new ListeSayfasi(vm), Margin = new Thickness(16, 8, 0, 0), NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden });
        Content = kok;
    }
}

public static class Ekranlar
{
    public static Task EvraklarAsync(EvrakHedef tip, (short? Id, string? Ad) hedef)
    {
        if (hedef.Id is null) return Task.CompletedTask;
        var vm = App.Servisler.GetRequiredService<EvrakListeVM>();
        vm.HedefFiltre = (tip, hedef.Id.Value, hedef.Ad ?? "");
        new ListePenceresi(vm, 1000, 560).ShowDialog();
        return Task.CompletedTask;
    }
}

public sealed record EkSatirVM(int Id, string DosyaAdi, string Boyut, DateTime Tarih, string? Aciklama);

/// <summary>Kayda bağlı dosya ekleri (fiş, fatura, poliçe, hasar fotoğrafı ...).</summary>
public sealed class EklerPenceresi : FluentWindow
{
    private readonly EkServisi _servis = App.Al<EkServisi>();
    private readonly EkHedef _hedef;
    private readonly int _hedefId;
    private readonly ObservableCollection<EkSatir> _liste = [];
    private readonly System.Windows.Controls.DataGrid _tablo;

    private EklerPenceresi(EkHedef hedef, int hedefId, string baslik)
    {
        _hedef = hedef;
        _hedefId = hedefId;
        Title = $"Ekler — {baslik}";
        Width = 760;
        Height = 480;
        Owner = Dialog.Sahip;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.None;
        SetResourceReference(BackgroundProperty, "Zemin");

        _tablo = new System.Windows.Controls.DataGrid { ItemsSource = _liste, IsReadOnly = true, AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column };
        KolonOlusturucu.Uygula(_tablo,
        [
            new Kolon("Dosya", nameof(EkSatir.DosyaAdi), Genislik: 300), new Kolon("Boyut", nameof(EkSatir.BoyutMetin)),
            Kolon.TarihSaat("Tarih", nameof(EkSatir.Tarih)), new Kolon("Açıklama", nameof(EkSatir.Aciklama)),
        ]);
        _tablo.MouseDoubleClick += async (_, _) => await AcAsync();

        var butonlar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        Button B(string metin, SymbolRegular ikon, Func<Task> islem, ControlAppearance g = ControlAppearance.Secondary)
        {
            var b = new Button { Content = metin, Icon = new SymbolIcon(ikon), Appearance = g, Margin = new Thickness(0, 0, 6, 0) };
            b.Click += async (_, _) => await Dialog.Calistir(islem);
            butonlar.Children.Add(b);
            return b;
        }
        B("Dosya ekle", SymbolRegular.Add24, EkleAsync, ControlAppearance.Primary);
        B("Aç", SymbolRegular.Open24, AcAsync);
        B("Kaydet...", SymbolRegular.Save24, FarkliKaydetAsync);
        B("Sil", SymbolRegular.Delete24, SilAsync);

        var ic = new DockPanel { Margin = new Thickness(16, 8, 16, 16) };
        DockPanel.SetDock(butonlar, Dock.Top);
        ic.Children.Add(butonlar);
        ic.Children.Add(_tablo);
        var kok = new DockPanel();
        var tb = new TitleBar { Title = Title };
        DockPanel.SetDock(tb, Dock.Top);
        kok.Children.Add(tb);
        kok.Children.Add(ic);
        Content = kok;
        Loaded += async (_, _) => await Dialog.Calistir(YukleAsync);
    }

    public static Task AcAsync(EkHedef hedef, int hedefId, string baslik)
    {
        new EklerPenceresi(hedef, hedefId, baslik).ShowDialog();
        return Task.CompletedTask;
    }

    private async Task YukleAsync()
    {
        _liste.Clear();
        foreach (var e in await _servis.ListeAsync(_hedef, _hedefId)) _liste.Add(e);
    }

    private async Task EkleAsync()
    {
        var dlg = new OpenFileDialog { Multiselect = true, Filter = "Belgeler ve görseller|*.pdf;*.jpg;*.jpeg;*.png;*.heic;*.xlsx;*.docx;*.txt|Tüm dosyalar|*.*" };
        if (dlg.ShowDialog() != true) return;
        foreach (var yol in dlg.FileNames)
        {
            var (icerik, tip, ad) = await Task.Run(() => Hazirla(yol));
            await _servis.EkleAsync(_hedef, _hedefId, ad, tip, icerik, null);
        }
        await YukleAsync();
        AnaPencere.Basarili($"{dlg.FileNames.Length} dosya eklendi.");
    }

    /// <summary>Görselleri yer kaplamaması için en fazla 1600 px ve JPEG %75 olarak küçültür.</summary>
    private static (byte[] Icerik, string Tip, string Ad) Hazirla(string yol)
    {
        var uzanti = Path.GetExtension(yol).ToLowerInvariant();
        var ham = File.ReadAllBytes(yol);
        if (uzanti is not (".jpg" or ".jpeg" or ".png" or ".bmp")) return (ham, IcerikTipi(uzanti), Path.GetFileName(yol));
        try
        {
            var kaynak = BitmapFrame.Create(new MemoryStream(ham), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var olcek = Math.Min(1.0, 1600.0 / Math.Max(kaynak.PixelWidth, kaynak.PixelHeight));
            BitmapSource hedef = olcek < 1 ? new TransformedBitmap(kaynak, new System.Windows.Media.ScaleTransform(olcek, olcek)) : kaynak;
            var enc = new JpegBitmapEncoder { QualityLevel = 75 };
            enc.Frames.Add(BitmapFrame.Create(hedef));
            using var ms = new MemoryStream();
            enc.Save(ms);
            var kucuk = ms.ToArray();
            return kucuk.Length < ham.Length ? (kucuk, "image/jpeg", Path.ChangeExtension(Path.GetFileName(yol), ".jpg")) : (ham, IcerikTipi(uzanti), Path.GetFileName(yol));
        }
        catch
        {
            return (ham, IcerikTipi(uzanti), Path.GetFileName(yol));
        }
    }

    private static string IcerikTipi(string uzanti) => uzanti switch
    {
        ".pdf" => "application/pdf", ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".txt" => "text/plain", _ => "application/octet-stream",
    };

    private async Task AcAsync()
    {
        if (_tablo.SelectedItem is not EkSatir s) return;
        var (ad, icerik) = await _servis.IndirAsync(s.Id);
        var yol = Path.Combine(Path.GetTempPath(), "OzaylarFilo", $"{s.Id}_{ad}");
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        await File.WriteAllBytesAsync(yol, icerik);
        Process.Start(new ProcessStartInfo(yol) { UseShellExecute = true });
    }

    private async Task FarkliKaydetAsync()
    {
        if (_tablo.SelectedItem is not EkSatir s) return;
        var dlg = new SaveFileDialog { FileName = s.DosyaAdi };
        if (dlg.ShowDialog() != true) return;
        var (_, icerik) = await _servis.IndirAsync(s.Id);
        await File.WriteAllBytesAsync(dlg.FileName, icerik);
    }

    private async Task SilAsync()
    {
        if (_tablo.SelectedItem is not EkSatir s) return;
        if (!await Dialog.Onay($"'{s.DosyaAdi}' silinsin mi?", "Ek sil", "Sil")) return;
        await _servis.SilAsync(s.Id);
        await YukleAsync();
    }
}

/// <summary>Cari veya kasa ekstresi: tarih aralığı, devreden bakiye, yürüyen bakiye, satır iptali, Excel/PDF.</summary>
public sealed class EkstrePenceresi : FluentWindow
{
    private readonly Func<DateOnly, DateOnly, Task<Ekstre>> _getir;
    private readonly Func<int, Task<(KaynakTip Tip, int? No)>>? _kaynak;
    private readonly DatePicker _bas = new() { Width = 130 };
    private readonly DatePicker _bit = new() { Width = 130, Margin = new Thickness(6, 0, 12, 0) };
    private readonly System.Windows.Controls.DataGrid _tablo = new() { IsReadOnly = true, AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column };
    private readonly TextBlock _ozet = new() { Margin = new Thickness(0, 8, 0, 0), FontWeight = FontWeights.SemiBold };
    private Ekstre? _ekstre;
    private readonly string _baslik;
    private readonly bool _kasa;

    private static readonly IReadOnlyList<Kolon> CariKolonlar =
    [
        Kolon.Tarih("Tarih", nameof(EkstreSatir.Tarih)), Kolon.Tarih("Vade", nameof(EkstreSatir.Vade)), new("İşlem", nameof(EkstreSatir.Islem)),
        new("Açıklama", nameof(EkstreSatir.Aciklama), Genislik: 360), Kolon.Para("Borç", nameof(EkstreSatir.Borc)), Kolon.Para("Alacak", nameof(EkstreSatir.Alacak)),
        Kolon.Para("Bakiye", nameof(EkstreSatir.Bakiye), false), new("Döviz", nameof(EkstreSatir.Doviz)), new("İptal", "Iptal?bool"),
    ];

    private static readonly IReadOnlyList<Kolon> KasaKolonlar =
    [
        Kolon.Tarih("Tarih", nameof(EkstreSatir.Tarih)), new("İşlem", nameof(EkstreSatir.Islem)), new("Açıklama", nameof(EkstreSatir.Aciklama), Genislik: 380),
        Kolon.Para("Giriş", nameof(EkstreSatir.Borc)), Kolon.Para("Çıkış", nameof(EkstreSatir.Alacak)), Kolon.Para("Bakiye", nameof(EkstreSatir.Bakiye), false),
        new("İşlem No", nameof(EkstreSatir.Doviz)), new("İptal", "Iptal?bool"),
    ];

    private EkstrePenceresi(string baslik, bool kasa, Func<DateOnly, DateOnly, Task<Ekstre>> getir, Func<int, Task<(KaynakTip, int?)>>? kaynak)
    {
        _baslik = baslik;
        _kasa = kasa;
        _getir = getir;
        _kaynak = kaynak;
        Title = baslik;
        Width = 1150;
        Height = 680;
        Owner = Dialog.Sahip;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.None;
        SetResourceReference(BackgroundProperty, "Zemin");
        var bugun = DateTime.Today;
        _bas.SelectedDate = new DateTime(bugun.Year, 1, 1);
        _bit.SelectedDate = bugun;
        KolonOlusturucu.Uygula(_tablo, kasa ? KasaKolonlar : CariKolonlar);

        var ust = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        ust.Children.Add(new TextBlock { Text = "Tarih:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        ust.Children.Add(_bas);
        ust.Children.Add(new TextBlock { Text = "–", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        ust.Children.Add(_bit);
        void B(string metin, SymbolRegular ikon, Func<Task> islem, ControlAppearance g = ControlAppearance.Secondary)
        {
            var b = new Button { Content = metin, Icon = new SymbolIcon(ikon), Appearance = g, Margin = new Thickness(0, 0, 6, 0) };
            b.Click += async (_, _) => await Dialog.Calistir(islem);
            ust.Children.Add(b);
        }
        B("Getir", SymbolRegular.ArrowClockwise24, YukleAsync, ControlAppearance.Primary);
        if (kaynak is not null) B("Seçili işlemi iptal et", SymbolRegular.Dismiss24, IptalAsync);
        B("Excel", SymbolRegular.DocumentTable24, () => DisaAsync(false));
        B("PDF", SymbolRegular.DocumentPdf24, () => DisaAsync(true));

        var ic = new DockPanel { Margin = new Thickness(16, 8, 16, 16) };
        DockPanel.SetDock(ust, Dock.Top);
        DockPanel.SetDock(_ozet, Dock.Bottom);
        ic.Children.Add(ust);
        ic.Children.Add(_ozet);
        ic.Children.Add(_tablo);
        var kok = new DockPanel();
        var tb = new TitleBar { Title = baslik };
        DockPanel.SetDock(tb, Dock.Top);
        kok.Children.Add(tb);
        kok.Children.Add(ic);
        Content = kok;
        Loaded += async (_, _) => await Dialog.Calistir(YukleAsync);
    }

    public static Task CariAsync(int cariId, string unvan)
    {
        var f = App.Al<FinansServisi>();
        new EkstrePenceresi($"Cari Ekstre — {unvan}", false, (b, e) => f.CariEkstreAsync(cariId, b, e), id => f.CariSatirKaynagiAsync(id)).ShowDialog();
        return Task.CompletedTask;
    }

    public static Task KasaAsync(byte kasaId, string ad)
    {
        var f = App.Al<FinansServisi>();
        new EkstrePenceresi($"Kasa/Banka Ekstresi — {ad}", true, (b, e) => f.KasaEkstreAsync(kasaId, b, e), id => f.KasaSatirKaynagiAsync(id)).ShowDialog();
        return Task.CompletedTask;
    }

    private DateOnly Bas => DateOnly.FromDateTime(_bas.SelectedDate ?? DateTime.Today.AddYears(-1));
    private DateOnly Bit => DateOnly.FromDateTime(_bit.SelectedDate ?? DateTime.Today);

    private async Task YukleAsync()
    {
        _ekstre = await _getir(Bas, Bit);
        _tablo.ItemsSource = _ekstre.Satirlar;
        _ozet.Text = _kasa
            ? $"Devreden: {_ekstre.Devreden:N2}   Giriş: {_ekstre.ToplamBorc:N2}   Çıkış: {_ekstre.ToplamAlacak:N2}   Bakiye: {_ekstre.Bakiye:N2}"
            : $"Devreden: {_ekstre.Devreden:N2}   Borç: {_ekstre.ToplamBorc:N2}   Alacak: {_ekstre.ToplamAlacak:N2}   Bakiye: {_ekstre.Bakiye:N2} TL ({(_ekstre.Bakiye > 0 ? "borçlu" : _ekstre.Bakiye < 0 ? "alacaklı" : "kapalı")})";
    }

    private async Task IptalAsync()
    {
        if (_tablo.SelectedItem is not EkstreSatir s || _kaynak is null) return;
        if (s.Iptal) { await Dialog.Bilgi("Bu satır zaten iptal edilmiş veya bir iptal kaydıdır."); return; }
        var (tip, no) = await _kaynak(s.Id);
        if (no is null || tip is not (KaynakTip.Tahsilat or KaynakTip.Odeme or KaynakTip.Virman or KaynakTip.Manuel or KaynakTip.Acilis))
        {
            await Dialog.Bilgi($"Bu satır bir '{tip.Ad()}' belgesinden geliyor; ilgili ekrandan (fatura, avans, masraf...) iptal edilmelidir.");
            return;
        }
        var neden = await Dialog.MetinIste("İptal nedeni:", "İşlem iptali");
        if (neden is null) return;
        await App.Al<FinansServisi>().IslemIptalAsync(tip, no.Value, neden);
        await YukleAsync();
        AnaPencere.Basarili("İşlem iptal edildi (ters kayıt oluşturuldu).");
    }

    private async Task DisaAsync(bool pdf)
    {
        if (_ekstre is null) return;
        var tablo = KolonOlusturucu.Tablo(_baslik, _kasa ? KasaKolonlar : CariKolonlar, _ekstre.Satirlar,
            $"Dönem: {Bas:dd.MM.yyyy} - {Bit:dd.MM.yyyy}", $"Devreden bakiye: {_ekstre.Devreden:N2}", $"Dönem sonu bakiye: {_ekstre.Bakiye:N2}");
        var dlg = new SaveFileDialog { FileName = $"{_baslik} {DateTime.Now:yyyy-MM-dd}", Filter = pdf ? "PDF|*.pdf" : "Excel|*.xlsx" };
        if (dlg.ShowDialog() != true) return;
        var d = App.Al<IDisaAktarici>();
        await File.WriteAllBytesAsync(dlg.FileName, pdf ? d.Pdf(tablo) : d.Excel(tablo));
        Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
    }
}
