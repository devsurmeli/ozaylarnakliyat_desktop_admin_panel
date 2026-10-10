using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Ozaylar.Filo.Desktop.Pencereler;

/// <summary>Fatura kaydı (bu sistem faturayı kesmez; harici programda kesilen faturayı takip eder).</summary>
public sealed class FaturaPenceresi : FluentWindow
{
    private readonly Fatura _f;
    private readonly FaturaServisi _servis;
    private readonly ObservableCollection<FaturaKalem> _kalemler;
    private readonly System.Windows.Controls.DataGrid _tablo;
    private readonly TextBlock _toplam = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right };
    private readonly bool _salt;

    private FaturaPenceresi(Fatura f, FaturaServisi servis, DovizServisi doviz, List<Secim> cariler)
    {
        _f = f;
        _servis = servis;
        _salt = f.Durum == FaturaDurum.Iptal;
        _kalemler = new ObservableCollection<FaturaKalem>(f.Kalemler.OrderBy(k => k.SiraNo));
        Title = f.Id == 0 ? "Yeni Fatura Kaydı" : $"Fatura {f.FaturaNo}";
        Width = 1100;
        Height = 720;
        Owner = Dialog.Sahip;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        DataContext = f;

        // ---- Başlık alanları ----
        var ust = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        for (var i = 0; i < 4; i++)
        {
            ust.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            ust.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var sira = 0;
        void Ekle(string etiket, FrameworkElement k)
        {
            var satir = sira / 4;
            var kol = sira % 4 * 2;
            if (ust.RowDefinitions.Count <= satir) ust.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var e = new TextBlock { Text = etiket, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(kol == 0 ? 0 : 12, 4, 6, 4) };
            k.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(e, satir); Grid.SetColumn(e, kol);
            Grid.SetRow(k, satir); Grid.SetColumn(k, kol + 1);
            ust.Children.Add(e);
            ust.Children.Add(k);
            sira++;
        }
        ComboBox Combo(string yol, System.Collections.IEnumerable kaynak, string degerYolu)
        {
            var cb = new ComboBox { ItemsSource = kaynak, SelectedValuePath = degerYolu, DisplayMemberPath = "Ad", IsEditable = degerYolu == "Id", IsTextSearchEnabled = true };
            cb.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(yol) { Converter = TipDonusturucu.Ornek, Mode = BindingMode.TwoWay });
            return cb;
        }
        TextBox Metin(string yol, IValueConverter? conv = null, string? param = null)
        {
            var tb = new TextBox();
            tb.SetBinding(System.Windows.Controls.TextBox.TextProperty, new Binding(yol) { Converter = conv, ConverterParameter = param, Mode = BindingMode.TwoWay, ValidatesOnExceptions = true });
            return tb;
        }
        DatePicker Tarih(string yol)
        {
            var dp = new DatePicker();
            dp.SetBinding(DatePicker.SelectedDateProperty, new Binding(yol) { Converter = TarihDonusturucu.Ornek, Mode = BindingMode.TwoWay });
            return dp;
        }

        Ekle("Tip", Combo(nameof(Fatura.FaturaTipi), EnumMetin.Liste<FaturaTipi>(), "Deger"));
        Ekle("Fatura no *", Metin(nameof(Fatura.FaturaNo)));
        Ekle("Tarih *", Tarih(nameof(Fatura.Tarih)));
        Ekle("Vade", Tarih(nameof(Fatura.VadeTarihi)));
        Ekle("Cari *", Combo(nameof(Fatura.CariId), cariler, "Id"));
        var pb = Combo(nameof(Fatura.ParaBirimi), EnumMetin.Liste<ParaBirimi>(), "Deger");
        Ekle("Para birimi", pb);
        var kur = Metin(nameof(Fatura.Kur), SayiDonusturucu.Ornek, "#,##0.0000");
        Ekle("Kur", kur);
        Ekle("e-Belge no", Metin(nameof(Fatura.EBelgeNo)));
        var aciklama = Metin(nameof(Fatura.Aciklama));
        Ekle("Açıklama", aciklama);
        Grid.SetColumnSpan(aciklama, 7);
        pb.SelectionChanged += async (_, _) =>
        {
            if (!IsLoaded) return;
            _f.Kur = await doviz.KurAsync(_f.ParaBirimi, _f.Tarih) ?? (_f.ParaBirimi == ParaBirimi.TRY ? 1 : 0);
            BindingOperations.GetBindingExpression(kur, System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
        };

        // ---- Kalemler ----
        _tablo = new System.Windows.Controls.DataGrid
        {
            ItemsSource = _kalemler, AutoGenerateColumns = false, CanUserAddRows = !_salt, CanUserDeleteRows = !_salt, IsReadOnly = _salt,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };
        DataGridTextColumn Kol(string baslik, string yol, double gen, bool ro = false, string? bicim = null, bool sayi = false)
        {
            var b = new Binding(yol) { Mode = ro ? BindingMode.OneWay : BindingMode.TwoWay, ValidatesOnExceptions = true };
            if (sayi) { b.Converter = SayiDonusturucu.Ornek; b.ConverterParameter = bicim ?? "#,##0.##"; }
            var c = new DataGridTextColumn { Header = baslik, Binding = b, IsReadOnly = ro, Width = gen > 0 ? new DataGridLength(gen) : new DataGridLength(360) };
            if (sayi)
            {
                var st = new Style(typeof(TextBlock));
                st.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                c.ElementStyle = st;
            }
            return c;
        }
        _tablo.Columns.Add(Kol("Açıklama", nameof(FaturaKalem.Aciklama), 0));
        _tablo.Columns.Add(Kol("Miktar", nameof(FaturaKalem.Miktar), 70, sayi: true));
        _tablo.Columns.Add(Kol("Birim", nameof(FaturaKalem.Birim), 70));
        _tablo.Columns.Add(Kol("Birim Fiyat", nameof(FaturaKalem.BirimFiyat), 110, bicim: "N2", sayi: true));
        _tablo.Columns.Add(Kol("İsk. %", nameof(FaturaKalem.IskontoOrani), 60, sayi: true));
        _tablo.Columns.Add(Kol("KDV %", nameof(FaturaKalem.KdvOrani), 60, sayi: true));
        _tablo.Columns.Add(Kol("Tevk. x/10", nameof(FaturaKalem.TevkifatPay), 80, sayi: true));
        _tablo.Columns.Add(Kol("Tutar", nameof(FaturaKalem.Tutar), 110, true, "N2", true));
        _tablo.Columns.Add(Kol("KDV", nameof(FaturaKalem.KdvTutar), 100, true, "N2", true));
        _tablo.Columns.Add(Kol("Tevkifat", nameof(FaturaKalem.TevkifatTutar), 100, true, "N2", true));
        _tablo.InitializingNewItem += (_, e) =>
        {
            var k = (FaturaKalem)e.NewItem;
            k.Miktar = 1;
            k.KdvOrani = 20;
            k.TevkifatPay = _f.FaturaTipi == FaturaTipi.Satis ? (byte)2 : (byte)0;
            k.Birim = "Adet";
        };
        _tablo.RowEditEnding += (_, _) => Dispatcher.BeginInvoke(Hesapla, System.Windows.Threading.DispatcherPriority.Background);
        _tablo.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(Hesapla, System.Windows.Threading.DispatcherPriority.Background);
        _kalemler.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(Hesapla, System.Windows.Threading.DispatcherPriority.Background);

        // ---- Butonlar ----
        var sefer = new Button { Content = "Faturalanmamış seferlerden ekle", Icon = new SymbolIcon(SymbolRegular.VehicleTruckProfile24), Margin = new Thickness(0, 0, 8, 0) };
        sefer.Click += async (_, _) => await Dialog.Calistir(SeferEkleAsync);
        var kaydet = new Button { Content = "Kaydet", Appearance = ControlAppearance.Primary, Icon = new SymbolIcon(SymbolRegular.Save24), Margin = new Thickness(0, 0, 8, 0), IsEnabled = !_salt };
        kaydet.Click += async (_, _) => await KaydetAsync();
        var kapat = new Button { Content = "Kapat", IsCancel = true };
        var alt = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var sag = new StackPanel { Orientation = Orientation.Horizontal };
        sag.Children.Add(kaydet);
        sag.Children.Add(kapat);
        DockPanel.SetDock(sag, Dock.Right);
        alt.Children.Add(sag);
        if (!_salt) alt.Children.Add(sefer);

        var ic = new DockPanel { Margin = new Thickness(16, 8, 16, 16) };
        DockPanel.SetDock(ust, Dock.Top);
        DockPanel.SetDock(alt, Dock.Bottom);
        DockPanel.SetDock(_toplam, Dock.Bottom);
        ic.Children.Add(ust);
        ic.Children.Add(alt);
        ic.Children.Add(_toplam);
        ic.Children.Add(_tablo);
        var kok = new DockPanel();
        var tb = new TitleBar { Title = Title + (_salt ? "  (İPTAL)" : "") };
        DockPanel.SetDock(tb, Dock.Top);
        kok.Children.Add(tb);
        kok.Children.Add(ic);
        Content = kok;
        if (_salt) ust.IsEnabled = false;
        Hesapla();
    }

    public static bool Ac(Fatura f, FaturaServisi servis, Secimler s, DovizServisi doviz, List<Secim> cariler) =>
        new FaturaPenceresi(f, servis, doviz, cariler).ShowDialog() == true;

    private void Hesapla()
    {
        try
        {
            _f.Kalemler = _kalemler.ToList();
            FaturaHesap.Hesapla(_f);
            _tablo.Items.Refresh();
            _toplam.Text = $"Ara toplam: {_f.AraToplam:N2}    KDV: {_f.KdvToplam:N2}    Tevkifat: −{_f.TevkifatToplam:N2}    GENEL TOPLAM: {_f.GenelToplam:N2} {_f.ParaBirimi.Ad()}";
        }
        catch (InvalidOperationException)
        {
            // Düzenleme sürerken Items.Refresh yapılamaz; satır bitince tekrar hesaplanır.
        }
        catch (DomainException ex)
        {
            _toplam.Text = ex.Message;
        }
    }

    private async Task SeferEkleAsync()
    {
        if (_f.CariId == 0) { await Dialog.Bilgi("Önce cariyi seçin."); return; }
        var liste = (await _servis.FaturalanmamisSeferlerAsync(_f.CariId))
            .Where(x => _kalemler.All(k => k.SeferId != x.SeferId)).ToList();
        if (liste.Count == 0) { await Dialog.Bilgi("Bu carinin faturalanmamış (teslim edilmiş) seferi yok."); return; }
        var secilen = SecimPenceresi.Sec("Faturalanacak seferler", liste,
        [
            new("Sefer", nameof(FaturalanmamisSefer.SeferNo)), Kolon.Tarih("Tarih", nameof(FaturalanmamisSefer.Tarih)),
            new("Güzergah", nameof(FaturalanmamisSefer.Guzergah)), new("Plaka", nameof(FaturalanmamisSefer.Plaka)),
            Kolon.Para("Navlun", nameof(FaturalanmamisSefer.Navlun)), new("PB", nameof(FaturalanmamisSefer.PB)),
        ]);
        foreach (var k in await _servis.SeferKalemleriAsync(secilen.Select(x => x.SeferId))) _kalemler.Add(k);
    }

    private async Task KaydetAsync()
    {
        _tablo.CommitEdit(DataGridEditingUnit.Row, true);
        _f.Kalemler = _kalemler.Where(k => !string.IsNullOrWhiteSpace(k.Aciklama) || k.BirimFiyat != 0).ToList();
        if (await Dialog.Calistir(() => _servis.KaydetAsync(_f)))
        {
            AnaPencere.Basarili($"Fatura {_f.FaturaNo} kaydedildi.");
            DialogResult = true;
        }
    }
}
