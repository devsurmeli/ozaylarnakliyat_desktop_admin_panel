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
