using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Ozaylar.Filo.Application.Ortak;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using PasswordBox = Wpf.Ui.Controls.PasswordBox;
using Selector = System.Windows.Controls.Primitives.Selector;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Ozaylar.Filo.Desktop.Altyapi;

public enum AlanTipi { Metin, CokSatir, Tamsayi, Ondalik, Para, Tarih, TarihSaat, Onay, Secim, Bayraklar, Sifre, Baslik, CokluSecim }

/// <summary>Genel form alanı. <see cref="Yol"/> modeldeki özellik adıdır (binding path).</summary>
public sealed class FormAlani
{
    public required string Etiket { get; init; }
    public string Yol { get; init; } = "";
    public AlanTipi Tip { get; init; } = AlanTipi.Metin;
    public bool Zorunlu { get; init; }
    public bool SaltOkunur { get; init; }
    /// <summary>Secim için: Secim / SecimOgesi&lt;TEnum&gt; listesi. Bayraklar için: SecimOgesi&lt;TFlags&gt; listesi.</summary>
    public IEnumerable? Secenekler { get; set; }
    public string? Ipucu { get; init; }
    public int MaksUzunluk { get; init; }
    /// <summary>Değer değiştiğinde çağrılır (ör. başka alanları doldurmak için). Sonrasında form yenilenir.</summary>
    public Func<FormPenceresi, Task>? Degisince { get; init; }
    /// <summary>Koşullu görünürlük (model üzerinden).</summary>
    public Func<object, bool>? Gorunur { get; init; }

    public static FormAlani Bolum(string baslik) => new() { Etiket = baslik, Tip = AlanTipi.Baslik };

    public static FormAlani EnumSecim<T>(string etiket, string yol, bool zorunlu = true) where T : struct, Enum =>
        new() { Etiket = etiket, Yol = yol, Tip = AlanTipi.Secim, Secenekler = EnumMetin.Liste<T>(), Zorunlu = zorunlu };
}

/// <summary>
/// Alan tanımlarından üretilen düzenleme penceresi. Kaydet: <c>kaydet(uyarilariOnayla)</c> çağrılır;
/// iş kuralı hatasında pencere açık kalır ve hata gösterilir.
/// </summary>
public sealed class FormPenceresi : FluentWindow
{
    private readonly List<(FormAlani Alan, FrameworkElement Kontrol, FrameworkElement Etiket)> _alanlar = [];
    private readonly Func<bool, Task> _kaydet;
    private readonly InfoBar _hata = new() { Severity = InfoBarSeverity.Error, IsClosable = true, Margin = new Thickness(0, 0, 0, 8) };
    private readonly Button _kaydetBtn;
    private readonly Grid _izgara;
    private readonly int _sutunSayisi;
    /// <summary>Tanım sırasıyla tüm öğeler (bölüm başlıklarında kontrol null).</summary>
    private readonly List<(FormAlani Alan, FrameworkElement? Kontrol, FrameworkElement Etiket)> _sira = [];

    public object Model { get; }

    public FormPenceresi(string baslik, object model, IReadOnlyList<FormAlani> alanlar, Func<bool, Task> kaydet, bool salt = false, double genislik = 0)
    {
        Model = model;
        _kaydet = kaydet;
        Title = baslik;
        DataContext = model;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Dialog.Sahip;
        ResizeMode = ResizeMode.CanResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.None;
        SetResourceReference(BackgroundProperty, "Zemin");
        var ikiSutun = alanlar.Count(a => a.Tip != AlanTipi.Baslik) > 9;
        Width = genislik > 0 ? genislik : ikiSutun ? 900 : 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height - 40;

        _izgara = new Grid { Margin = new Thickness(16, 4, 16, 8) };
        _sutunSayisi = ikiSutun ? 2 : 1;
        for (var i = 0; i < _sutunSayisi; i++)
        {
            _izgara.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            _izgara.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (i < _sutunSayisi - 1) _izgara.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        }

        foreach (var a in alanlar)
        {
            if (a.Tip == AlanTipi.Baslik)
            {
                var bt = new TextBlock { Text = a.Etiket, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 12, 0, 4) };
                _izgara.Children.Add(bt);
                _sira.Add((a, null, bt));
                continue;
            }
            var etiket = new TextBlock
            {
                Text = a.Etiket + (a.Zorunlu ? " *" : ""), VerticalAlignment = a.Tip is AlanTipi.CokSatir or AlanTipi.CokluSecim ? VerticalAlignment.Top : VerticalAlignment.Center,
                Margin = new Thickness(0, a.Tip is AlanTipi.CokSatir or AlanTipi.CokluSecim ? 10 : 4, 8, 4), TextWrapping = TextWrapping.Wrap,
            };
            var kontrol = KontrolOlustur(a);
            kontrol.Margin = new Thickness(0, 4, 0, 4);
            if (a.Ipucu is not null) kontrol.ToolTip = a.Ipucu;
            _izgara.Children.Add(etiket);
            _izgara.Children.Add(kontrol);
            _alanlar.Add((a, kontrol, etiket));
            _sira.Add((a, kontrol, etiket));
        }

        _kaydetBtn = new Button { Content = "Kaydet", Appearance = ControlAppearance.Primary, MinWidth = 110, IsDefault = true, Icon = new SymbolIcon(SymbolRegular.Save24) };
        _kaydetBtn.Click += async (_, _) => await KaydetAsync();
        var iptal = new Button { Content = salt ? "Kapat" : "Vazgeç", MinWidth = 110, IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        iptal.Click += (_, _) => DialogResult = false;
        var butonlar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 8, 16, 16) };
        if (!salt) butonlar.Children.Add(_kaydetBtn);
        butonlar.Children.Add(iptal);

        _hata.Visibility = Visibility.Collapsed;
        var icerik = new StackPanel();
        icerik.Children.Add(new Border { Padding = new Thickness(16, 0, 16, 0), Child = _hata });
        icerik.Children.Add(_izgara);

        var kok = new DockPanel();
        var baslikCubugu = new TitleBar { Title = baslik, ShowMaximize = false, ShowMinimize = false };
        DockPanel.SetDock(baslikCubugu, Dock.Top);
        DockPanel.SetDock(butonlar, Dock.Bottom);
        kok.Children.Add(baslikCubugu);
        kok.Children.Add(butonlar);
        kok.Children.Add(new ScrollViewer { Content = icerik, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = kok;

        if (salt) foreach (var (_, k, _) in _alanlar) k.IsEnabled = false;
        GorunurlukGuncelle();
        Loaded += (_, _) => IlkAlanaOdaklan();
        if (!salt) PreviewKeyDown += KlavyeAsync;
    }

    private void IlkAlanaOdaklan()
    {
        var ilk = _alanlar.FirstOrDefault(x => x.Kontrol.IsEnabled && x.Kontrol.Visibility == Visibility.Visible).Kontrol;
        if (ilk is null) return;
        if (ilk.Focusable) ilk.Focus();
        else ilk.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    /// <summary>Klavye ile hızlı giriş: Enter sonraki alana geçer (son alanda kaydeder), Ctrl+S kaydeder.</summary>
    private async void KlavyeAsync(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await KaydetAsync();
            return;
        }
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is not DependencyObject odak) return;
        if (odak is System.Windows.Controls.TextBox { AcceptsReturn: true } or System.Windows.Controls.Primitives.ButtonBase) return;
        var cb = odak as ComboBox ?? Ata<ComboBox>(odak);
        if (cb is { IsDropDownOpen: true })
        {
            // Açık listede seçim yoksa ilk (filtrelenmiş) seçenek seçilir.
            if (cb.SelectedItem is null && cb.Items.Count > 0) cb.SelectedIndex = 0;
            cb.IsDropDownOpen = false;
        }
        e.Handled = true;
        var son = _alanlar.LastOrDefault(x => x.Kontrol.Visibility == Visibility.Visible && x.Kontrol.IsEnabled).Kontrol;
        if (son is not null && (ReferenceEquals(son, odak) || son.IsAncestorOf(odak))) await KaydetAsync();
        else (odak as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private static T? Ata<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            d = d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            if (d is T t) return t;
        }
        return null;
    }

    /// <summary>Tarih kutusu kısayolları: B = bugün, D = dün, + / − = bir gün ileri / geri.</summary>
    private static void TarihKisayollari(DatePicker dp)
    {
        dp.ToolTip ??= "Kısayol: B = bugün, D = dün, + / − = gün ileri / geri";
        dp.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            DateTime? yeni = e.Key switch
            {
                Key.B => DateTime.Today,
                Key.D => DateTime.Today.AddDays(-1),
                Key.Add or Key.OemPlus => (dp.SelectedDate ?? DateTime.Today).AddDays(1),
                Key.Subtract or Key.OemMinus => (dp.SelectedDate ?? DateTime.Today).AddDays(-1),
                _ => null,
            };
            if (yeni is null) return;
            dp.SelectedDate = yeni;
            e.Handled = true;
        };
    }

    private static ListCollectionView Gorunum(IEnumerable? secenekler) => new((secenekler?.Cast<object>() ?? []).ToList());

    private static string SecimAdi(object o) => o.GetType().GetProperty("Ad")?.GetValue(o) as string ?? o.ToString() ?? "";

    /// <summary>
    /// Aramalı seçim: yazılan metni içeren seçenekler listelenir (Türkçe büyük/küçük harf duyarsız, ör. "ozy 103" → "34 OZY 103").
    /// Odaktan çıkarken tek eşleşme kalmışsa otomatik seçilir.
    /// </summary>
    private static void AramaliYap(ComboBox cb, IEnumerable? secenekler)
    {
        cb.ItemsSource = Gorunum(secenekler);
        ListCollectionView G() => (ListCollectionView)cb.ItemsSource;
        cb.IsTextSearchEnabled = false;
        var tr = Kultur.Tr.CompareInfo;
        bool Eslesir(object o, string metin) => metin.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(p => tr.IndexOf(SecimAdi(o), p, System.Globalization.CompareOptions.IgnoreCase) >= 0);
        void Filtrele(System.Windows.Controls.TextBox tb)
        {
            if (cb.SelectedItem is { } s && SecimAdi(s) == tb.Text || string.IsNullOrWhiteSpace(tb.Text))
            {
                if (G().Filter is not null) G().Filter = null;
                return;
            }
            var metin = tb.Text;
            G().Filter = o => Eslesir(o, metin);
            if (cb.IsKeyboardFocusWithin && !cb.IsDropDownOpen)
            {
                // Liste açılınca ComboBox metni seçili hale getirir; sonraki tuş yazılanı silmesin diye imleç sona alınır.
                var konum = tb.CaretIndex;
                cb.IsDropDownOpen = true;
                tb.SelectionLength = 0;
                tb.CaretIndex = konum;
            }
        }
        cb.Loaded += (_, _) =>
        {
            if (cb.Template?.FindName("PART_EditableTextBox", cb) is System.Windows.Controls.TextBox tb)
                tb.TextChanged += (_, _) => { if (tb.IsKeyboardFocusWithin) Filtrele(tb); };
        };
        cb.LostKeyboardFocus += (_, _) =>
        {
            if (cb.IsKeyboardFocusWithin) return;
            if (cb.SelectedItem is null && !string.IsNullOrWhiteSpace(cb.Text) && G().Count == 1) cb.SelectedItem = G().GetItemAt(0);
            if (cb.SelectedItem is not null) G().Filter = null;
        };
    }

    /// <summary>Formu modal açar; kaydedilirse true.</summary>
    public static bool Ac(string baslik, object model, IReadOnlyList<FormAlani> alanlar, Func<bool, Task> kaydet, double genislik = 0) =>
        new FormPenceresi(baslik, model, alanlar, kaydet, genislik: genislik).ShowDialog() == true;

    /// <summary>Model kodda değiştirildiğinde kontrolleri yeniler.</summary>
    public void Yenile()
    {
        foreach (var (_, k, _) in _alanlar)
        {
            foreach (var dp in new DependencyProperty[] { System.Windows.Controls.TextBox.TextProperty, DatePicker.SelectedDateProperty, Selector.SelectedValueProperty, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty })
                BindingOperations.GetBindingExpression(k, dp)?.UpdateTarget();
            if (k is DockPanel { Tag: Action oku }) oku();
        }
        GorunurlukGuncelle();
    }

    /// <summary>Bir seçim alanının seçeneklerini değiştirir (bağımlı listeler).</summary>
    public void SecenekleriAyarla(string yol, IEnumerable secenekler)
    {
        var x = _alanlar.FirstOrDefault(a => a.Alan.Yol == yol);
        if (x.Kontrol is ComboBox cb)
        {
            cb.ItemsSource = Gorunum(secenekler);
            BindingOperations.GetBindingExpression(cb, Selector.SelectedValueProperty)?.UpdateTarget();
        }
    }

    private void GorunurlukGuncelle()
    {
        foreach (var (a, k, e) in _alanlar)
        {
            if (a.Gorunur is null) continue;
            var v = a.Gorunur(Model) ? Visibility.Visible : Visibility.Collapsed;
            k.Visibility = e.Visibility = v;
        }
        Yerlestir();
    }

    /// <summary>Görünür alanları boşluk bırakmadan satır/sütunlara yerleştirir (bölüm başlıkları ve çok satırlı alanlar tam genişlik).</summary>
    private void Yerlestir()
    {
        _izgara.RowDefinitions.Clear();
        int satir = -1, sutun = _sutunSayisi;
        foreach (var (a, k, e) in _sira)
        {
            if (k is not null && k.Visibility != Visibility.Visible) continue;
            var tamGenislik = a.Tip is AlanTipi.Baslik or AlanTipi.CokSatir or AlanTipi.CokluSecim;
            if (tamGenislik || sutun >= _sutunSayisi)
            {
                satir++;
                sutun = 0;
                _izgara.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            if (k is null)
            {
                Grid.SetRow(e, satir);
                Grid.SetColumnSpan(e, _sutunSayisi * 3);
                sutun = _sutunSayisi;
                continue;
            }
            var kolon = sutun * 3;
            Grid.SetRow(e, satir);
            Grid.SetColumn(e, kolon);
            Grid.SetRow(k, satir);
            Grid.SetColumn(k, kolon + 1);
            Grid.SetColumnSpan(k, tamGenislik && _sutunSayisi > 1 ? _sutunSayisi * 3 - 1 : 1);
            sutun = tamGenislik ? _sutunSayisi : sutun + 1;
        }
    }

    private FrameworkElement KontrolOlustur(FormAlani a)
    {
        Binding B(IValueConverter? conv = null, string? param = null) => new(a.Yol)
        {
            Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            Converter = conv, ConverterParameter = param, ValidatesOnExceptions = true,
        };

        FrameworkElement k;
        switch (a.Tip)
        {
            case AlanTipi.Metin or AlanTipi.CokSatir:
            {
                var tb = new TextBox { IsReadOnly = a.SaltOkunur };
                if (a.MaksUzunluk > 0) tb.MaxLength = a.MaksUzunluk;
                if (a.Tip == AlanTipi.CokSatir) { tb.AcceptsReturn = true; tb.TextWrapping = TextWrapping.Wrap; tb.MinHeight = 64; tb.VerticalContentAlignment = VerticalAlignment.Top; }
                tb.SetBinding(System.Windows.Controls.TextBox.TextProperty, B());
                k = tb;
                break;
            }
            case AlanTipi.Sifre:
            {
                var pb = new PasswordBox();
                pb.PasswordChanged += (_, _) => Ozellik(a.Yol).Ver(pb.Password);
                k = pb;
                break;
            }
            case AlanTipi.Tamsayi or AlanTipi.Ondalik or AlanTipi.Para:
            {
                var tb = new TextBox { IsReadOnly = a.SaltOkunur, TextAlignment = TextAlignment.Right };
                var bicim = a.Tip switch { AlanTipi.Para => "N2", AlanTipi.Tamsayi => "0", _ => "#,##0.####" };
                var b = B(SayiDonusturucu.Ornek, bicim);
                b.UpdateSourceTrigger = UpdateSourceTrigger.LostFocus;
                tb.SetBinding(System.Windows.Controls.TextBox.TextProperty, b);
                tb.GotKeyboardFocus += (_, _) => tb.SelectAll();
                k = tb;
                break;
            }
            case AlanTipi.Tarih:
            {
                var dp = new DatePicker { SelectedDateFormat = DatePickerFormat.Short };
                TarihKisayollari(dp);
                dp.SetBinding(DatePicker.SelectedDateProperty, B(TarihDonusturucu.Ornek));
                k = dp;
                break;
            }
            case AlanTipi.TarihSaat:
            {
                // Tarih + saat (SS:dd) — iki kontrol, tek DateTime/DateTime? özelliğine yazar.
                var panel = new DockPanel();
                var saat = new TextBox { Width = 70, Margin = new Thickness(6, 0, 0, 0), PlaceholderText = "SS:dd" };
                var dp = new DatePicker();
                TarihKisayollari(dp);
                DockPanel.SetDock(saat, Dock.Right);
                panel.Children.Add(saat);
                panel.Children.Add(dp);
                var prop = Ozellik(a.Yol);
                void Oku()
                {
                    var v = prop.Al() as DateTime?;
                    dp.SelectedDate = v?.Date;
                    saat.Text = v is null ? "" : v.Value.ToString("HH:mm");
                }
                void Yaz()
                {
                    if (dp.SelectedDate is not { } t) { if (Nullable.GetUnderlyingType(prop.Tip) is not null) prop.Ver(null); return; }
                    var s = TimeSpan.TryParse(saat.Text.Replace('.', ':'), Kultur.Tr, out var ts) ? ts : TimeSpan.Zero;
                    prop.Ver(t.Date + s);
                }
                Oku();
                dp.SelectedDateChanged += (_, _) => Yaz();
                saat.LostFocus += (_, _) => Yaz();
                panel.Tag = (Action)Oku;
                k = panel;
                break;
            }
            case AlanTipi.Onay:
            {
                var cb = new CheckBox();
                cb.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, B());
                k = cb;
                break;
            }
            case AlanTipi.Secim:
            {
                var cb = new ComboBox
                {
                    IsEditable = true, SelectedValuePath = SecimDegerYolu(a.Secenekler), DisplayMemberPath = "Ad",
                    IsEnabled = !a.SaltOkunur,
                };
                AramaliYap(cb, a.Secenekler);
                cb.SetBinding(Selector.SelectedValueProperty, B(TipDonusturucu.Ornek));
                // Zorunlu olmayan seçimde "temizle" için Delete tuşu.
                if (!a.Zorunlu) cb.PreviewKeyDown += (_, e) => { if (e.Key == Key.Delete) { cb.SelectedIndex = -1; cb.SelectedValue = null; } };
                k = cb;
                break;
            }
            case AlanTipi.Bayraklar:
            {
                var wp = new WrapPanel();
                var prop = Ozellik(a.Yol);
                foreach (var o in a.Secenekler!)
                {
                    var deger = o.GetType().GetProperty("Deger")!.GetValue(o)!;
                    var ad = o.GetType().GetProperty("Ad")!.GetValue(o) as string;
                    var cb = new CheckBox { Content = ad, Margin = new Thickness(0, 0, 12, 0) };
                    var bit = Convert.ToInt64(deger);
                    cb.IsChecked = (Convert.ToInt64(prop.Al()) & bit) != 0;
                    cb.Click += (_, _) =>
                    {
                        var mevcut = Convert.ToInt64(prop.Al());
                        mevcut = cb.IsChecked == true ? mevcut | bit : mevcut & ~bit;
                        prop.Ver(Enum.ToObject(prop.Tip, mevcut));
                    };
                    wp.Children.Add(cb);
                }
                k = wp;
                break;
            }
            case AlanTipi.CokluSecim:
            {
                // Model özelliği bir IList (List<byte>, List<short> ...); seçenekler Secim(Id, Ad) veya Grup içeren kayıtlar.
                var liste = (IList)Ozellik(a.Yol).Al()!;
                var elemanTipi = liste.GetType().GetGenericArguments()[0];
                var panel = new StackPanel();
                string? sonGrup = null;
                foreach (var o in a.Secenekler!)
                {
                    var t = o.GetType();
                    var id = Convert.ChangeType(t.GetProperty("Id")!.GetValue(o)!, elemanTipi);
                    var grup = t.GetProperty("Grup")?.GetValue(o) as string;
                    if (grup is not null && grup != sonGrup)
                    {
                        panel.Children.Add(new TextBlock { Text = grup, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2) });
                        sonGrup = grup;
                    }
                    var cb = new CheckBox { Content = t.GetProperty("Ad")!.GetValue(o), IsChecked = liste.Contains(id), Margin = new Thickness(grup is null ? 0 : 12, 0, 0, 0) };
                    cb.Click += (_, _) => { if (cb.IsChecked == true) { if (!liste.Contains(id)) liste.Add(id); } else liste.Remove(id); };
                    panel.Children.Add(cb);
                }
                k = panel;
                break;
            }
            default:
                throw new NotSupportedException(a.Tip.ToString());
        }

        if (a.Degisince is not null)
        {
            async void Tetikle()
            {
                try
                {
                    await a.Degisince(this);
                    Yenile();
                }
                catch (Exception ex)
                {
                    await Dialog.HataGoster(ex);
                }
            }
            switch (k)
            {
                case ComboBox cb: cb.SelectionChanged += (_, e) => { if (cb.IsKeyboardFocusWithin || cb.IsDropDownOpen || e.AddedItems.Count > 0 && IsLoaded) Tetikle(); }; break;
                case CheckBox ch: ch.Click += (_, _) => Tetikle(); break;
                case DatePicker dp: dp.SelectedDateChanged += (_, _) => { if (IsLoaded) Tetikle(); }; break;
                case System.Windows.Controls.TextBox tb: tb.LostFocus += (_, _) => Tetikle(); break;
                case DockPanel dpn: foreach (var c in dpn.Children.OfType<DatePicker>()) c.SelectedDateChanged += (_, _) => { if (IsLoaded) Tetikle(); }; break;
                case WrapPanel wp: foreach (var c in wp.Children.OfType<CheckBox>()) c.Click += (_, _) => Tetikle(); break;
            }
        }
        if (a.Gorunur is not null)
        {
            switch (k)
            {
                case ComboBox cb: cb.SelectionChanged += (_, _) => GorunurlukGuncelle(); break;
                case CheckBox ch: ch.Click += (_, _) => GorunurlukGuncelle(); break;
            }
        }
        // Görünürlüğü belirleyen alanlar değişince de güncelle.
        switch (k)
        {
            case ComboBox cb: cb.SelectionChanged += (_, _) => { if (IsLoaded) GorunurlukGuncelle(); }; break;
            case CheckBox ch: ch.Click += (_, _) => GorunurlukGuncelle(); break;
        }
        return k;
    }

    private sealed record Erisim(object Hedef, System.Reflection.PropertyInfo Prop)
    {
        public object? Al() => Prop.GetValue(Hedef);
        public void Ver(object? v) => Prop.SetValue(Hedef, v);
        public Type Tip => Prop.PropertyType;
    }

    /// <summary>"A.B.C" biçimindeki yolu modelde çözer (iç içe nesneler).</summary>
    private Erisim Ozellik(string yol)
    {
        object? o = Model;
        var parcalar = yol.Split('.');
        foreach (var p in parcalar[..^1]) o = o?.GetType().GetProperty(p)?.GetValue(o);
        var prop = o?.GetType().GetProperty(parcalar[^1]);
        return o is null || prop is null ? throw new InvalidOperationException($"Form alanı bulunamadı: {yol}") : new Erisim(o, prop);
    }

    private static string SecimDegerYolu(IEnumerable? secenekler)
    {
        var ilk = secenekler?.Cast<object>().FirstOrDefault();
        return ilk is null || ilk.GetType().GetProperty("Id") is not null ? "Id" : "Deger";
    }

    private async Task KaydetAsync()
    {
        // Odaktaki alanın değeri kaynağa yazılsın.
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox odak)
            BindingOperations.GetBindingExpression(odak, System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        foreach (var (_, k, _) in _alanlar) if (k is DockPanel { Tag: Action }) k.RaiseEvent(new RoutedEventArgs(LostFocusEvent));

        var eksik = _alanlar.Where(x => x.Alan.Zorunlu && x.Kontrol.Visibility == Visibility.Visible && Bos(x.Alan)).Select(x => x.Alan.Etiket).ToList();
        if (eksik.Count > 0)
        {
            HataGoster("Zorunlu alanlar boş: " + string.Join(", ", eksik));
            return;
        }
        if (_alanlar.Any(x => Validation.GetHasError(x.Kontrol)))
        {
            HataGoster("Hatalı değerleri düzeltin (kırmızı işaretli alanlar).");
            return;
        }

        _kaydetBtn.IsEnabled = false;
        try
        {
            var ok = await Dialog.Calistir(onay => _kaydet(onay));
            if (ok) DialogResult = true;
        }
        finally
        {
            _kaydetBtn.IsEnabled = true;
        }
    }

    private bool Bos(FormAlani a)
    {
        if (a.Tip is AlanTipi.Onay or AlanTipi.Bayraklar or AlanTipi.Baslik) return false;
        if (a.Yol.Contains('[')) return false;
        if (a.Tip == AlanTipi.CokluSecim) return Ozellik(a.Yol).Al() is ICollection { Count: 0 };
        var v = Ozellik(a.Yol).Al();
        return v switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            int i when a.Tip == AlanTipi.Secim => i == 0,
            short sh when a.Tip == AlanTipi.Secim => sh == 0,
            byte by when a.Tip == AlanTipi.Secim => by == 0,
            _ => false,
        };
    }

    private void HataGoster(string mesaj)
    {
        _hata.Message = mesaj;
        _hata.IsOpen = true;
        _hata.Visibility = Visibility.Visible;
    }
}
