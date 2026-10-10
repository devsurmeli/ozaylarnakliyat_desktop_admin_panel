using System.ComponentModel;
using System.Reflection;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Application.Ortak;

/// <summary>
/// Kullanıcı onayı gerektiren uyarılar (ör. km sapması). Servis önce uyarıları döndürür;
/// arayüz kullanıcıya sorar ve <c>uyarilariOnayla: true</c> ile tekrar çağırır.
/// </summary>
public sealed class UyariVarException(IReadOnlyList<string> uyarilar)
    : Exception(string.Join(Environment.NewLine, uyarilar))
{
    public IReadOnlyList<string> Uyarilar { get; } = uyarilar;
}

public sealed class Uyarilar
{
    private readonly List<string> _liste = [];
    public void Ekle(string mesaj) => _liste.Add(mesaj);
    public bool Var => _liste.Count > 0;
    public IReadOnlyList<string> Liste => _liste;

    public void OnaylanmadiysaFirlat(bool onaylandi)
    {
        if (Var && !onaylandi) throw new UyariVarException(_liste);
    }
}

public static class EnumMetin
{
    private static readonly Dictionary<Enum, string> Onbellek = [];

    public static string Ad(this Enum deger)
    {
        lock (Onbellek)
        {
            if (Onbellek.TryGetValue(deger, out var s)) return s;
            var f = deger.GetType().GetField(deger.ToString());
            s = f?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? deger.ToString();
            Onbellek[deger] = s;
            return s;
        }
    }

    public static IReadOnlyList<SecimOgesi<T>> Liste<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new SecimOgesi<T>(v, ((Enum)(object)v).Ad())).ToList();
}

public sealed record SecimOgesi<T>(T Deger, string Ad)
{
    public override string ToString() => Ad;
}

public sealed record Secim(int Id, string Ad)
{
    public override string ToString() => Ad;
}

public static class DogrulamaUzantilari
{
    public static async Task DogrulaAsync<T>(this IValidator<T>? validator, T nesne, CancellationToken ct = default)
    {
        if (validator is null) return;
        var sonuc = await validator.ValidateAsync(nesne, ct);
        if (!sonuc.IsValid)
            throw new DomainException(string.Join(Environment.NewLine, sonuc.Errors.Select(e => e.ErrorMessage).Distinct()));
    }
}

public static class DbUzantilari
{
    /// <summary>
    /// Arayüzde düzenlenmiş (bağlantısız) kaydı veritabanındaki kayıtla birleştirir:
    /// yalnızca değişen alanlar güncellenir (audit doğru olur), RowVer ile eşzamanlılık kontrolü yapılır.
    /// </summary>
    public static async Task<T> BirlestirAsync<T>(this IFiloDbContext db, T gelen, CancellationToken ct = default) where T : class
    {
        var pk = db.Entry(gelen).Metadata.FindPrimaryKey()!;
        var anahtar = pk.Properties.Select(p => p.PropertyInfo!.GetValue(gelen)).ToArray();
        var mevcut = await db.Set<T>().FindAsync(anahtar, ct)
                     ?? throw new DomainException("Kayıt bulunamadı; başka bir kullanıcı tarafından silinmiş olabilir.");
        var entry = db.Entry(mevcut);
        entry.CurrentValues.SetValues(gelen);
        if (gelen is IGuncellemeBilgisi g)
        {
            // Ekranda yüklenen sürüm ile karşılaştır.
            entry.Property(nameof(IGuncellemeBilgisi.RowVer)).OriginalValue = g.RowVer;
            // Oluşturma bilgileri arayüzden ezilmesin.
            entry.Property(nameof(IOlusturmaBilgisi.OlusturanId)).IsModified = false;
            entry.Property(nameof(IOlusturmaBilgisi.OlusturmaTarihi)).IsModified = false;
        }
        if (gelen is ISoftDelete) entry.Property(nameof(ISoftDelete.IsDeleted)).IsModified = false;
        return mevcut;
    }

    /// <summary>Kayıt yeni mi (anahtar varsayılan değerde mi)?</summary>
    public static bool YeniMi<T>(this IFiloDbContext db, T kayit) where T : class =>
        db.Entry(kayit).Metadata.FindPrimaryKey()!.Properties
            .All(p => Equals(p.PropertyInfo!.GetValue(kayit), Activator.CreateInstance(p.ClrType)));

    public static async Task KaydetAsync(this IFiloDbContext db, CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new EszamanlilikException();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message is { } m && (m.Contains("UNIQUE") || m.Contains("duplicate key") || m.Contains("yinelenen")))
        {
            throw new DomainException("Bu bilgilerle bir kayıt zaten var (tekil alan çakışması: plaka, kod, numara vb.).");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message is { } m && m.Contains("REFERENCE constraint"))
        {
            throw new DomainException("Bu kayıt başka kayıtlarda kullanıldığı için silinemez.");
        }
    }
}

public sealed class EszamanlilikException()
    : Exception("Kayıt siz düzenlerken başka bir kullanıcı tarafından değiştirildi. Lütfen ekranı yenileyip tekrar deneyin.");

public static class Metin
{
    public static string? Temizle(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Plakayı standart biçime getirir: "34abc123" → "34 ABC 123".</summary>
    public static string Plaka(string plaka)
    {
        var s = new string(plaka.Where(char.IsLetterOrDigit).ToArray()).ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        var m = System.Text.RegularExpressions.Regex.Match(s, @"^(\d{2})([A-ZÇĞİÖŞÜ]{1,3})(\d{2,5})$");
        return m.Success ? $"{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value}" : s;
    }
}
