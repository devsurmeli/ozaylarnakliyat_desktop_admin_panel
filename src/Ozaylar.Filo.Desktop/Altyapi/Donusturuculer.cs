using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Ozaylar.Filo.Application.Ortak;

namespace Ozaylar.Filo.Desktop.Altyapi;

public static class Kultur
{
    public static readonly CultureInfo Tr = new("tr-TR");
}

/// <summary>Enum → Türkçe açıklama ([Description]).</summary>
public sealed class EnumAdDonusturucu : IValueConverter
{
    public static readonly EnumAdDonusturucu Ornek = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is Enum e ? e.Ad() : value;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>DateOnly / DateOnly? ↔ DateTime? (DatePicker için).</summary>
public sealed class TarihDonusturucu : IValueConverter
{
    public static readonly TarihDonusturucu Ornek = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : value is DateTime dt ? dt : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hedef = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is not DateTime dt) return Nullable.GetUnderlyingType(targetType) is null ? DependencyProperty.UnsetValue : null;
        return hedef == typeof(DateOnly) ? DateOnly.FromDateTime(dt) : dt;
    }
}

/// <summary>
/// Sayı ↔ metin (tr-TR). Hem "1.234,56" hem "1234.56" kabul eder; boş metin nullable hedefte null olur.
/// Parametre: gösterim biçimi (ör. "N2", "N0", "#,##0.###").
/// </summary>
public sealed class SayiDonusturucu : IValueConverter
{
    public static readonly SayiDonusturucu Ornek = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => "",
        IFormattable f => f.ToString(parameter as string ?? "#,##0.##", Kultur.Tr),
        _ => value.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string)?.Trim();
        var nullable = Nullable.GetUnderlyingType(targetType) is not null;
        var hedef = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (string.IsNullOrEmpty(s)) return nullable ? null : System.Convert.ChangeType(0, hedef, CultureInfo.InvariantCulture);
        // Hata fırlatılır: binding ValidatesOnExceptions ile alanı kırmızı işaretler, kaynak değişmez.
        var d = Ozaylar.Filo.Application.Servisler.IceAktarmaServisi.SayiCoz(s) ?? throw new FormatException("Geçerli bir sayı girin.");
        try
        {
            return hedef == typeof(decimal) ? d : System.Convert.ChangeType(Math.Round(d), hedef, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            throw new FormatException("Sayı çok büyük.");
        }
    }
}

/// <summary>ComboBox SelectedValue (int/enum/...) ↔ hedef özellik tipi (short?, byte, enum ...).</summary>
public sealed class TipDonusturucu : IValueConverter
{
    public static readonly TipDonusturucu Ornek = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => null,
        Enum e => e,
        IConvertible c when value.GetType() != typeof(string) => System.Convert.ToInt32(c, CultureInfo.InvariantCulture),
        _ => value,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hedef = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is null) return Nullable.GetUnderlyingType(targetType) is not null || !targetType.IsValueType ? null : Binding.DoNothing;
        if (hedef.IsEnum) return Enum.ToObject(hedef, value);
        return System.Convert.ChangeType(value, hedef, CultureInfo.InvariantCulture);
    }
}

public sealed class BoolGorunurlukDonusturucu : IValueConverter
{
    public static readonly BoolGorunurlukDonusturucu Ornek = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ (parameter as string == "ters") ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class NullGorunurlukDonusturucu : IValueConverter
{
    public static readonly NullGorunurlukDonusturucu Ornek = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is null or "") ^ (parameter as string == "ters") ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>KPI kart başlığından ikon seçer (kartlara tek tek ikon tanımlamaya gerek kalmaz).</summary>
public sealed class KpiIkonDonusturucu : IValueConverter
{
    public static readonly KpiIkonDonusturucu Ornek = new();

    private static readonly (string Anahtar, Wpf.Ui.Controls.SymbolRegular Ikon)[] Kurallar =
    [
        ("gecik", Wpf.Ui.Controls.SymbolRegular.Warning24), ("uyarı", Wpf.Ui.Controls.SymbolRegular.Alert24),
        ("kredi", Wpf.Ui.Controls.SymbolRegular.BuildingBank24), ("anapara", Wpf.Ui.Controls.SymbolRegular.BuildingBank24),
        ("faiz", Wpf.Ui.Controls.SymbolRegular.DataTrending24), ("taksit", Wpf.Ui.Controls.SymbolRegular.CalendarClock24),
        ("gün", Wpf.Ui.Controls.SymbolRegular.CalendarLtr24), ("ödenecek", Wpf.Ui.Controls.SymbolRegular.CalendarClock24),
        ("kasa", Wpf.Ui.Controls.SymbolRegular.Wallet24), ("bakiye", Wpf.Ui.Controls.SymbolRegular.Wallet24), ("para", Wpf.Ui.Controls.SymbolRegular.Money24),
        ("gelir", Wpf.Ui.Controls.SymbolRegular.ArrowTrendingLines24), ("ciro", Wpf.Ui.Controls.SymbolRegular.ArrowTrendingLines24), ("navlun", Wpf.Ui.Controls.SymbolRegular.ArrowTrendingLines24),
        ("gider", Wpf.Ui.Controls.SymbolRegular.ReceiptMoney24), ("kâr", Wpf.Ui.Controls.SymbolRegular.Trophy24),
        ("alacak", Wpf.Ui.Controls.SymbolRegular.ArrowDownload24), ("borç", Wpf.Ui.Controls.SymbolRegular.ArrowUpload24), ("tahsil", Wpf.Ui.Controls.SymbolRegular.Timer24),
        ("sefer", Wpf.Ui.Controls.SymbolRegular.VehicleTruckProfile24), ("km başı", Wpf.Ui.Controls.SymbolRegular.Gauge24), ("km", Wpf.Ui.Controls.SymbolRegular.Gauge24),
        ("tüketim", Wpf.Ui.Controls.SymbolRegular.GasPump24), ("yakıt", Wpf.Ui.Controls.SymbolRegular.GasPump24), ("süre", Wpf.Ui.Controls.SymbolRegular.Clock24),
        ("fatura", Wpf.Ui.Controls.SymbolRegular.DocumentText24), ("zimmet", Wpf.Ui.Controls.SymbolRegular.Key24), ("hakediş", Wpf.Ui.Controls.SymbolRegular.PersonMoney24),
        ("avans", Wpf.Ui.Controls.SymbolRegular.MoneyHand24), ("onay", Wpf.Ui.Controls.SymbolRegular.TaskListSquareLtr24), ("şoför", Wpf.Ui.Controls.SymbolRegular.Person24),
        ("limit", Wpf.Ui.Controls.SymbolRegular.ShieldCheckmark24), ("risk", Wpf.Ui.Controls.SymbolRegular.ShieldCheckmark24),
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var baslik = (value as string ?? "").ToLower(Kultur.Tr);
        foreach (var (anahtar, ikon) in Kurallar)
            if (baslik.Contains(anahtar)) return ikon;
        return Wpf.Ui.Controls.SymbolRegular.DataUsage24;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Ad soyaddan baş harfler (kullanıcı rozeti).</summary>
public sealed class BasHarfDonusturucu : IValueConverter
{
    public static readonly BasHarfDonusturucu Ornek = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Concat((value as string ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0], Kultur.Tr)));
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
