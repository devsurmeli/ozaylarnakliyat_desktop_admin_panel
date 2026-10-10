using System.Globalization;
using ClosedXML.Excel;
using Ozaylar.Filo.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ozaylar.Filo.Infrastructure.Disa;

public sealed class DisaAktarici : IDisaAktarici
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private readonly string _firma;

    static DisaAktarici()
    {
        // NOT: QuestPDF Community lisansı yıllık brüt cirosu 1M USD altındaki firmalar içindir.
        // Ciro bu sınırı aşıyorsa Professional lisans alınmalı ve burası güncellenmelidir.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DisaAktarici(string firmaUnvani = "Ozaylar Nakliyat") => _firma = firmaUnvani;

    public byte[] Excel(TabloVerisi t)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SayfaAdi(t.Baslik));
        var satir = 1;
        ws.Cell(satir, 1).Value = t.Baslik;
        ws.Cell(satir, 1).Style.Font.SetBold().Font.SetFontSize(14);
        foreach (var a in t.AltBilgi) ws.Cell(++satir, 1).Value = a;
        satir += 2;
        var baslikSatiri = satir;
        for (var k = 0; k < t.Kolonlar.Count; k++)
        {
            var c = ws.Cell(satir, k + 1);
            c.Value = t.Kolonlar[k].Baslik;
            c.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));
        }
        foreach (var r in t.Satirlar)
        {
            satir++;
            for (var k = 0; k < t.Kolonlar.Count; k++)
            {
                var kol = t.Kolonlar[k];
                var c = ws.Cell(satir, k + 1);
                var v = kol.Deger(r);
                switch (v)
                {
                    case null: break;
                    case decimal d: c.Value = d; break;
                    case int i: c.Value = i; break;
                    case short sh: c.Value = sh; break;
                    case long l: c.Value = l; break;
                    case double db: c.Value = db; break;
                    case DateOnly dt: c.Value = dt.ToDateTime(TimeOnly.MinValue); break;
                    case DateTime dtt: c.Value = dtt; break;
                    case bool b: c.Value = b ? "Evet" : "Hayır"; break;
                    default: c.Value = Convert.ToString(v, Tr); break;
                }
                c.Style.NumberFormat.Format = kol.Bicim switch
                {
                    KolonBicim.Para => "#,##0.00",
                    KolonBicim.Sayi => "#,##0.##",
                    KolonBicim.Tarih => "dd.mm.yyyy",
                    KolonBicim.TarihSaat => "dd.mm.yyyy hh:mm",
                    KolonBicim.Yuzde => "0.0",
                    _ => c.Style.NumberFormat.Format,
                };
            }
        }
        if (t.Kolonlar.Any(k => k.Toplam) && t.Satirlar.Count > 0)
        {
            satir++;
            ws.Cell(satir, 1).Value = "TOPLAM";
            ws.Row(satir).Style.Font.SetBold();
            for (var k = 0; k < t.Kolonlar.Count; k++)
                if (t.Kolonlar[k].Toplam)
                {
                    var harf = ws.Column(k + 1).ColumnLetter();
                    ws.Cell(satir, k + 1).FormulaA1 = $"SUM({harf}{baslikSatiri + 1}:{harf}{satir - 1})";
                    ws.Cell(satir, k + 1).Style.NumberFormat.Format = "#,##0.00";
                }
        }
        if (t.Satirlar.Count > 0) ws.Range(baslikSatiri, 1, satir, t.Kolonlar.Count).SetAutoFilter();
        ws.SheetView.FreezeRows(baslikSatiri);
        ws.Columns().AdjustToContents(baslikSatiri, satir, 8, 60);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] Pdf(TabloVerisi t, bool yatay = true)
    {
        var toplamlar = t.Kolonlar.Select(k => k.Toplam
            ? t.Satirlar.Sum(r => k.Deger(r) is decimal d ? d : k.Deger(r) is int i ? i : 0m)
            : (decimal?)null).ToList();

        return Document.Create(doc => doc.Page(p =>
        {
            p.Size(yatay ? PageSizes.A4.Landscape() : PageSizes.A4);
            p.Margin(20);
            // QuestPDF'in gömülü Lato fontu (Türkçe karakter destekli) — sistem fontuna bağımlılık yok.
            p.DefaultTextStyle(x => x.FontSize(8));
            p.Header().Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(_firma).SemiBold().FontSize(10);
                    r.ConstantItem(160).AlignRight().Text($"Rapor tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(7);
                });
                c.Item().PaddingTop(4).Text(t.Baslik).Bold().FontSize(13);
                foreach (var a in t.AltBilgi) c.Item().Text(a).FontSize(8);
                c.Item().PaddingVertical(4).LineHorizontal(0.5f);
            });
            p.Content().Table(tbl =>
            {
                tbl.ColumnsDefinition(cd =>
                {
                    foreach (var k in t.Kolonlar)
                        if (k.Bicim is KolonBicim.Tarih) cd.ConstantColumn(52);
                        else if (k.Bicim is KolonBicim.Para or KolonBicim.Sayi or KolonBicim.Yuzde) cd.RelativeColumn(1);
                        else cd.RelativeColumn(2);
                });
                tbl.Header(h =>
                {
                    foreach (var k in t.Kolonlar)
                        h.Cell().Background("#E8EEF7").Padding(3).Text(k.Baslik).SemiBold();
                });
                var i = 0;
                foreach (var r in t.Satirlar)
                {
                    var arka = i++ % 2 == 0 ? "#FFFFFF" : "#F7F9FC";
                    foreach (var k in t.Kolonlar)
                    {
                        var hucre = tbl.Cell().Background(arka).Padding(3);
                        var metin = Bicimle(k.Deger(r), k.Bicim);
                        if (k.Bicim is KolonBicim.Para or KolonBicim.Sayi or KolonBicim.Yuzde) hucre.AlignRight().Text(metin);
                        else hucre.Text(metin);
                    }
                }
                if (toplamlar.Any(x => x is not null))
                    for (var k = 0; k < t.Kolonlar.Count; k++)
                    {
                        var hucre = tbl.Cell().BorderTop(1).Padding(3);
                        if (toplamlar[k] is { } tp) hucre.AlignRight().Text(Bicimle(tp, t.Kolonlar[k].Bicim)).Bold();
                        else if (k == 0) hucre.Text("TOPLAM").Bold();
                    }
            });
            p.Footer().AlignCenter().Text(x =>
            {
                x.Span("Sayfa ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        })).GeneratePdf();
    }

    private static string Bicimle(object? v, KolonBicim b) => v switch
    {
        null => "",
        decimal d when b == KolonBicim.Para => d.ToString("N2", Tr),
        decimal d when b == KolonBicim.Yuzde => "%" + d.ToString("N1", Tr),
        decimal d => d.ToString("#,##0.##", Tr),
        int i => i.ToString("N0", Tr),
        DateOnly dt => dt.ToString("dd.MM.yyyy", Tr),
        DateTime dt when b == KolonBicim.Tarih => dt.ToString("dd.MM.yyyy", Tr),
        DateTime dt => dt.ToString("dd.MM.yyyy HH:mm", Tr),
        bool x => x ? "Evet" : "Hayır",
        _ => Convert.ToString(v, Tr) ?? "",
    };

    private static string SayfaAdi(string baslik)
    {
        var s = new string(baslik.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        return s.Length > 31 ? s[..31] : s.Length == 0 ? "Rapor" : s;
    }
}
