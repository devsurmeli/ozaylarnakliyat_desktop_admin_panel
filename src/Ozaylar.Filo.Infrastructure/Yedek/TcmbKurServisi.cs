using System.Globalization;
using System.Xml.Linq;
using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Infrastructure.Yedek;

/// <summary>TCMB günlük döviz kurları (today.xml). Döviz alış/satış (Forex) değerleri kullanılır.</summary>
public sealed class TcmbKurServisi(HttpClient http)
{
    public const string Adres = "https://www.tcmb.gov.tr/kurlar/today.xml";

    public async Task<(DateOnly Tarih, List<(ParaBirimi Pb, decimal Alis, decimal Satis)> Kurlar)> GetirAsync(CancellationToken ct = default)
    {
        var xml = await http.GetStringAsync(Adres, ct);
        return Coz(xml);
    }

    public static (DateOnly Tarih, List<(ParaBirimi Pb, decimal Alis, decimal Satis)> Kurlar) Coz(string xml)
    {
        var doc = XDocument.Parse(xml);
        var kok = doc.Root!;
        var tarih = DateOnly.ParseExact((string)kok.Attribute("Tarih")!, "dd.MM.yyyy", CultureInfo.InvariantCulture);
        var liste = new List<(ParaBirimi, decimal, decimal)>();
        foreach (var (kod, pb) in new[] { ("USD", ParaBirimi.USD), ("EUR", ParaBirimi.EUR), ("GBP", ParaBirimi.GBP) })
        {
            var e = kok.Elements("Currency").FirstOrDefault(c => (string?)c.Attribute("Kod") == kod);
            if (e is null) continue;
            var birim = decimal.Parse((string?)e.Element("Unit") ?? "1", CultureInfo.InvariantCulture);
            if (decimal.TryParse((string?)e.Element("ForexBuying"), NumberStyles.Number, CultureInfo.InvariantCulture, out var alis)
                && decimal.TryParse((string?)e.Element("ForexSelling"), NumberStyles.Number, CultureInfo.InvariantCulture, out var satis))
                liste.Add((pb, alis / birim, satis / birim));
        }
        return (tarih, liste);
    }
}
