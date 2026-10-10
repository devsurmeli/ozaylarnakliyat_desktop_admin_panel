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
        SKColor.Parse("#0F6CBD"), SKColor.Parse("#107C10"), SKColor.Parse("#C42B1C"), SKColor.Parse("#CA5010"),
        SKColor.Parse("#8764B8"), SKColor.Parse("#038387"), SKColor.Parse("#986F0B"), SKColor.Parse("#5C2E91"),
        SKColor.Parse("#4F6BED"), SKColor.Parse("#69797E"),
    ];

    public static readonly SKColor Mavi = Palet[0], Yesil = Palet[1], Kirmizi = Palet[2], Turuncu = Palet[3];

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
        p.Children.Add(new TextBlock { Text = baslik, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 0, 0, 6) });
        p.Children.Add(icerik);
        var b = new Border { Child = p, Padding = new Thickness(14, 10, 14, 10), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 10) };
        b.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        b.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        return b;
    }

    /// <summary>Kartları yan yana (sığmazsa alt alta) dizen kaydırılabilir panel.</summary>
    public static FrameworkElement Izgara(int sutun, params FrameworkElement[] kartlar)
    {
        var g = new System.Windows.Controls.Primitives.UniformGrid { Columns = sutun };
        foreach (var k in kartlar) g.Children.Add(k);
        return new ScrollViewer { Content = g, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static Axis[] X(IReadOnlyList<string> etiketler) => [new Axis { Labels = etiketler.ToArray(), TextSize = 12, LabelsRotation = etiketler.Count > 14 ? 45 : 0 }];
    private static Axis[] Y() => [new Axis { Labeler = Bicim, TextSize = 12, SeparatorsPaint = new SolidColorPaint(new SKColor(0, 0, 0, 25)) }];

    /// <summary>Gruplu sütun grafiği.</summary>
    public static CartesianChart Sutun(IReadOnlyList<string> etiketler, params (string Ad, IEnumerable<decimal> Degerler, SKColor? Renk)[] seriler) => new()
    {
        Series = seriler.Select((s, i) => (ISeries)new ColumnSeries<double>
        {
            Name = s.Ad, Values = s.Degerler.Select(d => (double)d).ToArray(),
            Fill = new SolidColorPaint(s.Renk ?? Palet[i % Palet.Length]), MaxBarWidth = 28,
        }).ToArray(),
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom,
    };

    /// <summary>Yığılmış sütun (ör. taksitte anapara + faiz).</summary>
    public static CartesianChart Yigilmis(IReadOnlyList<string> etiketler, params (string Ad, IEnumerable<decimal> Degerler, SKColor? Renk)[] seriler) => new()
    {
        Series = seriler.Select((s, i) => (ISeries)new StackedColumnSeries<double>
        {
            Name = s.Ad, Values = s.Degerler.Select(d => (double)d).ToArray(),
            Fill = new SolidColorPaint(s.Renk ?? Palet[i % Palet.Length]), MaxBarWidth = 28,
        }).ToArray(),
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom,
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
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom,
    };

    /// <summary>Gelir/gider sütunları + kâr çizgisi.</summary>
    public static CartesianChart GelirGiderKar(IReadOnlyList<string> etiketler, IEnumerable<decimal> gelir, IEnumerable<decimal> gider, IEnumerable<decimal> kar) => new()
    {
        Series =
        [
            new ColumnSeries<double> { Name = "Gelir", Values = gelir.Select(d => (double)d).ToArray(), Fill = new SolidColorPaint(Mavi), MaxBarWidth = 22 },
            new ColumnSeries<double> { Name = "Gider", Values = gider.Select(d => (double)d).ToArray(), Fill = new SolidColorPaint(Turuncu), MaxBarWidth = 22 },
            new LineSeries<double>
            {
                Name = "Kâr", Values = kar.Select(d => (double)d).ToArray(), Stroke = new SolidColorPaint(Yesil, 3), Fill = null,
                GeometrySize = 8, GeometryStroke = new SolidColorPaint(Yesil, 3), LineSmoothness = 0.2,
            },
        ],
        XAxes = X(etiketler), YAxes = Y(), LegendPosition = LegendPosition.Bottom,
    };

    /// <summary>Pasta / halka grafiği.</summary>
    public static PieChart Pasta(IEnumerable<(string Ad, decimal Deger)> dilimler, bool halka = true) => new()
    {
        Series = dilimler.Where(d => d.Deger > 0).OrderByDescending(d => d.Deger).Select((d, i) => (ISeries)new PieSeries<double>
        {
            Name = d.Ad, Values = [(double)d.Deger], Fill = new SolidColorPaint(Palet[i % Palet.Length]),
            InnerRadius = halka ? 60 : 0,
        }).ToArray(),
        LegendPosition = LegendPosition.Right,
    };

    /// <summary>Veri yoksa gösterilecek bilgi.</summary>
    public static FrameworkElement Bos(string mesaj = "Gösterilecek veri yok.") =>
        new TextBlock { Text = mesaj, Opacity = 0.7, Margin = new Thickness(4, 12, 0, 0) };
}
