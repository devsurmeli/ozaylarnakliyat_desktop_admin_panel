using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Ozaylar.Filo.Application.Servisler;

namespace Ozaylar.Filo.Infrastructure.Disa;

/// <summary>.xlsx ve .csv ekstreleri başlık satırına göre okur. Hücre tarihleri "dd.MM.yyyy HH:mm:ss" metnine çevrilir.</summary>
public sealed class ExcelOkuyucu : IExcelOkuyucu
{
    private static readonly CultureInfo Tr = new("tr-TR");

    public List<Dictionary<string, string?>> Oku(Stream dosya, string dosyaAdi, int baslikSatiri)
    {
        var uzanti = Path.GetExtension(dosyaAdi).ToLowerInvariant();
        return uzanti switch
        {
            ".xlsx" or ".xlsm" => Xlsx(dosya, baslikSatiri),
            ".csv" or ".txt" => Csv(dosya, baslikSatiri),
            ".xls" => throw new Domain.Common.DomainException("Eski .xls biçimi desteklenmiyor; dosyayı Excel'de .xlsx olarak kaydedin."),
            _ => throw new Domain.Common.DomainException("Desteklenen biçimler: .xlsx, .csv"),
        };
    }

    private static List<Dictionary<string, string?>> Xlsx(Stream dosya, int baslikSatiri)
    {
        using var wb = new XLWorkbook(dosya);
        var ws = wb.Worksheets.First();
        var son = ws.LastRowUsed()?.RowNumber() ?? 0;
        var sonKolon = ws.Row(baslikSatiri).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var basliklar = Enumerable.Range(1, sonKolon).Select(k => ws.Cell(baslikSatiri, k).GetString().Trim()).ToList();
        var sonuc = new List<Dictionary<string, string?>>();
        for (var r = baslikSatiri + 1; r <= son; r++)
        {
            var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var k = 1; k <= sonKolon; k++)
            {
                if (string.IsNullOrEmpty(basliklar[k - 1]) || d.ContainsKey(basliklar[k - 1])) continue;
                var c = ws.Cell(r, k);
                d[basliklar[k - 1]] = c.DataType switch
                {
                    XLDataType.DateTime => c.GetDateTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                    XLDataType.TimeSpan => c.GetTimeSpan().ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
                    // Türkçe biçim (ondalık virgül, binlik ayırıcı yok): SayiCoz ile belirsizlik oluşmaz.
                    XLDataType.Number => c.GetDouble().ToString("0.############", Tr),
                    _ => c.GetString(),
                };
            }
            sonuc.Add(d);
        }
        return sonuc;
    }

    private static List<Dictionary<string, string?>> Csv(Stream dosya, int baslikSatiri)
    {
        using var sr = new StreamReader(dosya, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var satirlar = new List<string>();
        while (sr.ReadLine() is { } l) satirlar.Add(l);
        if (satirlar.Count < baslikSatiri) return [];
        var baslik = satirlar[baslikSatiri - 1];
        var ayirac = baslik.Count(c => c == ';') >= baslik.Count(c => c == ',') ? ';' : ',';
        var basliklar = Bol(baslik, ayirac);
        return satirlar.Skip(baslikSatiri).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l =>
        {
            var degerler = Bol(l, ayirac);
            var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < basliklar.Count; i++)
                d.TryAdd(basliklar[i], i < degerler.Count ? degerler[i] : null);
            return d;
        }).ToList();
    }

    private static List<string> Bol(string satir, char ayirac)
    {
        var sonuc = new List<string>();
        var sb = new StringBuilder();
        var tirnak = false;
        for (var i = 0; i < satir.Length; i++)
        {
            var c = satir[i];
            if (c == '"')
            {
                if (tirnak && i + 1 < satir.Length && satir[i + 1] == '"') { sb.Append('"'); i++; }
                else tirnak = !tirnak;
            }
            else if (c == ayirac && !tirnak) { sonuc.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        sonuc.Add(sb.ToString().Trim());
        return sonuc;
    }
}
