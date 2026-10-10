using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;

namespace Ozaylar.Filo.Desktop.Altyapi;

/// <summary>Liste kolonu: hem DataGrid kolonunu hem Excel/PDF kolonunu tanımlar.</summary>
public sealed record Kolon(string Baslik, string Yol, KolonBicim Bicim = KolonBicim.Metin, double Genislik = 0, bool Toplam = false)
{
    public static Kolon Para(string baslik, string yol, bool toplam = true) => new(baslik, yol, KolonBicim.Para, 110, toplam);
    public static Kolon Tarih(string baslik, string yol) => new(baslik, yol, KolonBicim.Tarih, 90);
    public static Kolon TarihSaat(string baslik, string yol) => new(baslik, yol, KolonBicim.TarihSaat, 120);
    public static Kolon Sayi(string baslik, string yol, bool toplam = false) => new(baslik, yol, KolonBicim.Sayi, 80, toplam);
}

public static class KolonOlusturucu
{
    public static void Uygula(DataGrid grid, IEnumerable<Kolon> kolonlar)
    {
        grid.Columns.Clear();
        foreach (var k in kolonlar)
        {
            var b = new Binding(k.Yol) { Mode = BindingMode.OneWay };
            var sagaYasli = k.Bicim is KolonBicim.Para or KolonBicim.Sayi or KolonBicim.Yuzde;
            DataGridColumn kolon;
            if (k.Bicim == KolonBicim.Metin && k.Yol.EndsWith("?bool"))
            {
                kolon = new DataGridCheckBoxColumn { Binding = new Binding(k.Yol[..^5]) { Mode = BindingMode.OneWay }, IsReadOnly = true };
            }
            else
            {
                switch (k.Bicim)
                {
                    case KolonBicim.Para: b.StringFormat = "N2"; break;
                    case KolonBicim.Sayi: b.StringFormat = "#,##0.##"; break;
                    case KolonBicim.Yuzde: b.StringFormat = "'%'#,##0.0"; break; // tırnaksız % değeri 100 ile çarpar
                    case KolonBicim.Tarih: b.StringFormat = "dd.MM.yyyy"; break;
                    case KolonBicim.TarihSaat: b.StringFormat = "dd.MM.yyyy HH:mm"; break;
                    default: b.Converter = EnumAdDonusturucu.Ornek; break;
                }
                var tc = new DataGridTextColumn { Binding = b, IsReadOnly = true };
                if (sagaYasli)
                {
                    var stil = new Style(typeof(TextBlock));
                    stil.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                    stil.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
                    tc.ElementStyle = stil;
                }
                kolon = tc;
            }
            kolon.Header = k.Baslik;
            kolon.Width = k.Genislik > 0 ? new DataGridLength(k.Genislik) : new DataGridLength(1, DataGridLengthUnitType.Auto);
            kolon.SortMemberPath = k.Yol.EndsWith("?bool") ? k.Yol[..^5] : k.Yol;
            grid.Columns.Add(kolon);
        }
    }

    /// <summary>Kolon tanımlarından dışa aktarım tablosu üretir (görünür/filtrelenmiş satırlarla).</summary>
    public static TabloVerisi Tablo(string baslik, IEnumerable<Kolon> kolonlar, IEnumerable satirlar, params string[] altBilgi)
    {
        var liste = satirlar.Cast<object>().ToList();
        var tip = liste.FirstOrDefault()?.GetType();
        var tk = kolonlar.Select(k =>
        {
            var yol = k.Yol.EndsWith("?bool") ? k.Yol[..^5] : k.Yol;
            var okuyucu = Okuyucu(tip, yol);
            return new TabloKolon(k.Baslik, r => okuyucu(r) is Enum e ? e.Ad() : okuyucu(r), k.Bicim, k.Toplam);
        }).ToList();
        return new TabloVerisi(baslik, tk, liste) { AltBilgi = altBilgi };
    }

    private static Func<object, object?> Okuyucu(Type? tip, string yol)
    {
        if (tip is null) return _ => null;
        var parcalar = yol.Split('.');
        return nesne =>
        {
            object? o = nesne;
            foreach (var p in parcalar)
            {
                if (o is null) return null;
                o = o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance)?.GetValue(o);
            }
            return o;
        };
    }
}
