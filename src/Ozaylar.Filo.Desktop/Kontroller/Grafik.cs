using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using Ozaylar.Filo.Desktop.Altyapi;
using SkiaSharp;

namespace Ozaylar.Filo.Desktop.Kontroller;

/// <summary>Grafik üreticileri (LiveCharts2). Tek palet; tutarlar binlik ayraçlı.</summary>
public static class Grafik
{
    public static readonly SKColor[] Palet =
    [
        SKColor.Parse("#F97316"), SKColor.Parse("#475569"), SKColor.Parse("#F59E0B"), SKColor.Parse("#0EA5E9"),
        SKColor.Parse("#16A34A"), SKColor.Parse("#E11D48"), SKColor.Parse("#8B5CF6"), SKColor.Parse("#14B8A6"),
        SKColor.Parse("#A8A29E"), SKColor.Parse("#FB923C"),
    ];

    /// <summary>Ana seri (gelir, anapara) turuncu; ikincil (gider, faiz) arduvaz; kâr yeşil.</summary>
    public static readonly SKColor Ana = Palet[0], Ikincil = Palet[1], Yesil = Palet[4], Kirmizi = Palet[5];

    private static bool _ayarlandi;

    /// <summary>Uygulama başında bir kez: Türkçe karakterli yazı tipi.</summary>
    public static void Ayarla()
    {
        if (_ayarlandi) return;
        _ayarlandi = true;
        LiveCharts.Configure(c => c.HasTextSettings(new TextSettings { DefaultTypeface = SKTypeface.FromFamilyName("Segoe UI") }));
    }

    private static string Bicim(double v) => Math.Abs(v) >= 1_000_000 ? (v / 1_000_000).ToString("#,##0.#", Kultur.Tr) + " Mn"
        : Math.Abs(v) >= 10_000 ? (v / 1000).ToString("#,##0", Kultur.Tr) + " B" : v.ToString("#,##0", Kultur.Tr);

    /// <summary>Başlıklı kart çerçevesi.</summary>
    public static FrameworkElement Kart(string baslik, FrameworkElement icerik, double yukseklik = 300)
    {
        icerik.Height = yukseklik;
        var p = new StackPanel();
        var ust = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var cizgi = new Border { Width = 4, Height = 16, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        cizgi.SetResourceReference(Border.BackgroundProperty, "HeroGradyan");
        ust.Children.Add(cizgi);
        ust.Children.Add(new TextBlock { Text = baslik, FontWeight = FontWeights.SemiBold, FontSize = 14.5, VerticalAlignment = VerticalAlignment.Center });
        p.Children.Add(ust);
        p.Children.Add(icerik);
        var b = new Border { Child = p, Padding = new Thickness(18, 14, 18, 12), Margin = new Thickness(0, 4, 14, 14), VerticalAlignment = VerticalAlignment.Top };
        b.SetResourceReference(FrameworkElement.StyleProperty, "Kart");
        return b;
    }

    /// <summary>Kartları yan yana (sığmazsa alt alta) dizen kaydırılabilir panel.</summary>
    public static FrameworkElement Izgara(int sutun, params FrameworkElement[] kartlar)
    {
        var g = new System.Windows.Controls.Primitives.UniformGrid { Columns = sutun };
        foreach (var k in kartlar) g.Children.Add(k);
        return new ScrollViewer { Content = g, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static Axis[] X(IReadOnlyList<string> etiketler) => [new Axis { Labels = etiketler.ToArray(), TextSize = 12, LabelsRotation = etiketler.Count > 14 ? 45 : 0, LabelsPaint = Etiket() }];
    private static Axis[] Y() => [new Axis { Labeler = Bicim, TextSize = 12, LabelsPaint = Etiket(), SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128, 45)) }];

    /// <summary>Açık ve koyu temada okunur nötr yazı rengi.</summary>
    private static SolidColorPaint Etiket() => new(new SKColor(0x8A, 0x81, 0x7C));

    /// <summary>Gruplu sütun grafiği.</summary>
    public static CartesianChart Sutun(IReadOnlyList<string> etiketler, params (string Ad, IEnumerable<decimal> Degerler, SKColor? Renk)[] seriler) => new()
    {
        Series = seriler.Select((s, i) => (ISeries)new ColumnSeries<double>
        {
            Name = s.Ad, Values = s.Degerler.Select(d => (double)d).ToArray(),
            Fill = new SolidColorPaint(s.Renk ?? Palet[i % Palet.Length]), MaxBarWidth = 28,
        }).ToArray(),
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom, LegendTextPaint = Etiket(), Background = System.Windows.Media.Brushes.Transparent,
    };

    /// <summary>Yığılmış sütun (ör. taksitte anapara + faiz).</summary>
    public static CartesianChart Yigilmis(IReadOnlyList<string> etiketler, params (string Ad, IEnumerable<decimal> Degerler, SKColor? Renk)[] seriler) => new()
    {
        Series = seriler.Select((s, i) => (ISeries)new StackedColumnSeries<double>
        {
            Name = s.Ad, Values = s.Degerler.Select(d => (double)d).ToArray(),
            Fill = new SolidColorPaint(s.Renk ?? Palet[i % Palet.Length]), MaxBarWidth = 28,
        }).ToArray(),
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom, LegendTextPaint = Etiket(), Background = System.Windows.Media.Brushes.Transparent,
    };

    /// <summary>Çizgi grafiği (ör. kalan anapara eğrisi, kâr trendi).</summary>
    public static CartesianChart Cizgi(IReadOnlyList<string> etiketler, params (string Ad, IEnumerable<decimal> Degerler, SKColor? Renk)[] seriler) => new()
    {
        Series = seriler.Select((s, i) =>
        {
            var renk = s.Renk ?? Palet[i % Palet.Length];
            return (ISeries)new LineSeries<double>
            {
                Name = s.Ad, Values = s.Degerler.Select(d => (double)d).ToArray(),
                Stroke = new SolidColorPaint(renk, 3), Fill = new SolidColorPaint(renk.WithAlpha(30)),
                GeometrySize = etiketler.Count > 24 ? 0 : 7, GeometryStroke = new SolidColorPaint(renk, 3), LineSmoothness = 0.3,
            };
        }).ToArray(),
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom, LegendTextPaint = Etiket(), Background = System.Windows.Media.Brushes.Transparent,
    };

    /// <summary>Gelir/gider sütunları + kâr çizgisi.</summary>
    public static CartesianChart GelirGiderKar(IReadOnlyList<string> etiketler, IEnumerable<decimal> gelir, IEnumerable<decimal> gider, IEnumerable<decimal> kar) => new()
    {
        Series =
        [
            new ColumnSeries<double> { Name = "Gelir", Values = gelir.Select(d => (double)d).ToArray(), Fill = new SolidColorPaint(Ana), MaxBarWidth = 22 },
            new ColumnSeries<double> { Name = "Gider", Values = gider.Select(d => (double)d).ToArray(), Fill = new SolidColorPaint(Ikincil), MaxBarWidth = 22 },
            new LineSeries<double>
            {
                Name = "Kâr", Values = kar.Select(d => (double)d).ToArray(), Stroke = new SolidColorPaint(Yesil, 3), Fill = null,
                GeometrySize = 8, GeometryStroke = new SolidColorPaint(Yesil, 3), LineSmoothness = 0.2,
            },
        ],
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom, LegendTextPaint = Etiket(), Background = System.Windows.Media.Brushes.Transparent,
    };

    /// <summary>Pasta / halka grafiği.</summary>
    public static PieChart Pasta(IEnumerable<(string Ad, decimal Deger)> dilimler, bool halka = true) => new()
    {
        Series = dilimler.Where(d => d.Deger > 0).OrderByDescending(d => d.Deger).Select((d, i) => (ISeries)new PieSeries<double>
        {
            Name = d.Ad, Values = [(double)d.Deger], Fill = new SolidColorPaint(Palet[i % Palet.Length]),
            InnerRadius = halka ? 60 : 0,
        }).ToArray(),
        LegendPosition = LegendPosition.Right, LegendTextPaint = Etiket(), Background = System.Windows.Media.Brushes.Transparent,
    };

    /// <summary>Sütuna tıklanınca (etiket sırası) çalışır: grafikten detaya inme.</summary>
    public static CartesianChart Tiklayinca(this CartesianChart grafik, Action<int> islem)
    {
        grafik.Cursor = System.Windows.Input.Cursors.Hand;
        grafik.ToolTip = null;
        grafik.DataPointerDown += (_, noktalar) =>
        {
            if (noktalar?.FirstOrDefault() is { } n) islem(n.Index);
        };
        return grafik;
    }

    /// <summary>Pasta dilimine tıklanınca (dilim adı) çalışır.</summary>
    public static PieChart Tiklayinca(this PieChart grafik, Action<string> islem)
    {
        grafik.Cursor = System.Windows.Input.Cursors.Hand;
        grafik.DataPointerDown += (_, noktalar) =>
        {
            if (noktalar?.FirstOrDefault()?.Context.Series.Name is { } ad) islem(ad);
        };
        return grafik;
    }

    /// <summary>Veri yoksa gösterilecek bilgi.</summary>
    public static FrameworkElement Bos(string mesaj = "Gösterilecek veri yok.") =>
        new TextBlock { Text = mesaj, Opacity = 0.7, Margin = new Thickness(4, 12, 0, 0) };
}
