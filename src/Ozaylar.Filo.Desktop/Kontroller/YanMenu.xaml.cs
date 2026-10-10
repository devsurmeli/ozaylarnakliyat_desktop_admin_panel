using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Kontroller;

/// <summary>Yan menü öğesi: sayfa (Sayfa), eylem (Tikla) veya alt öğeleri olan grup.</summary>
public sealed partial class MenuOgesi : ObservableObject
{
    public MenuOgesi(string baslik, SymbolRegular ikon, Type? sayfa = null, params MenuOgesi[] alt)
    {
        Baslik = baslik;
        Ikon = ikon;
        Sayfa = sayfa;
        Alt = new ObservableCollection<MenuOgesi>(alt);
        foreach (var a in alt) a.Ust = this;
    }

    public string Baslik { get; set; }
    public SymbolRegular Ikon { get; }
    public Type? Sayfa { get; }
    public Action? Tikla { get; init; }
    public ObservableCollection<MenuOgesi> Alt { get; }
    public MenuOgesi? Ust { get; private set; }
    public bool Grup => Alt.Count > 0;
    public bool Gorunur => true;

    [ObservableProperty] private bool _acik;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RozetVar))] private string? _rozet;
    [ObservableProperty] private bool _secili;
    [ObservableProperty] private bool _altSecili;

    public bool RozetVar => !string.IsNullOrEmpty(Rozet);

    public void BaslikDegistir(string baslik)
    {
        Baslik = baslik;
        OnPropertyChanged(nameof(Baslik));
    }
}

/// <summary>
/// Turuncu degrade, yüzen yan menü. Seçili sayfa beyaz "hap" ile gösterilir; gruplar açılır/kapanır;
/// daraltınca yalnız ikonlar kalır (genişlik animasyonlu).
/// </summary>
public partial class YanMenu : UserControl
{
    private const double GenisGenislik = 268, DarGenislik = 84;

    public static readonly DependencyProperty DarProperty =
        DependencyProperty.Register(nameof(Dar), typeof(bool), typeof(YanMenu), new PropertyMetadata(false, (d, _) => ((YanMenu)d).DarDegisti()));

    public bool Dar
    {
        get => (bool)GetValue(DarProperty);
        set => SetValue(DarProperty, value);
    }

    public ObservableCollection<MenuOgesi> Ogeler { get; } = [];
    public ObservableCollection<MenuOgesi> AltOgeler { get; } = [];

    /// <summary>Sayfası olan öğe tıklanınca.</summary>
    public event Action<MenuOgesi>? SayfaIstendi;

    public YanMenu()
    {
        InitializeComponent();
        // Işık katmanları yuvarlak köşelerin dışına taşmasın.
        Govde.SizeChanged += (_, e) => Ic.Clip = new RectangleGeometry(new Rect(e.NewSize), 26, 26);
    }

    public void Kullanici(string adSoyad, string basHarf)
    {
        KullaniciAd.Text = adSoyad;
        BasHarf.Text = basHarf;
    }

    private void Oge_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MenuOgesi o) return;
        if (o.Grup)
        {
            if (Dar)
            {
                Dar = false;
                o.Acik = true;
                return;
            }
            o.Acik = !o.Acik;
            return;
        }
        if (o.Sayfa is not null) SayfaIstendi?.Invoke(o);
        else o.Tikla?.Invoke();
    }

    /// <summary>Gezinme sonrası: açık sayfanın menü öğesini seçili gösterir, grubunu açar.</summary>
    public void SayfaDegisti(Type? sayfa)
    {
        if (sayfa is null) return;
        var hedef = Tumu().FirstOrDefault(x => x.Sayfa == sayfa);
        if (hedef is null) return; // detay sayfası: son seçim kalır
        foreach (var x in Tumu())
        {
            x.Secili = ReferenceEquals(x, hedef);
            x.AltSecili = x.Grup && x.Alt.Contains(hedef);
        }
        if (hedef.Ust is { } u) u.Acik = true;
    }

    private IEnumerable<MenuOgesi> Tumu() => Ogeler.Concat(AltOgeler).SelectMany(o => o.Alt.Prepend(o));

    private void Daralt_Click(object sender, RoutedEventArgs e) => Dar = !Dar;

    private void DarDegisti()
    {
        var animasyon = new DoubleAnimation(Dar ? DarGenislik : GenisGenislik, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        Govde.BeginAnimation(WidthProperty, animasyon);
        var gizle = Dar ? Visibility.Collapsed : Visibility.Visible;
        MarkaYazi.Visibility = MenuEtiket.Visibility = KullaniciYazi.Visibility = gizle;
    }
}
