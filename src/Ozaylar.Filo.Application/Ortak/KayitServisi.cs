using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Application.Ortak;

/// <summary>Basit ana kayıtlar (tanımlar, araç, dorse, cari ...) için ortak CRUD.</summary>
public interface IKayitServisi<T> where T : class
{
    Task<List<T>> ListeAsync(Func<IQueryable<T>, IQueryable<T>>? sorgu = null, CancellationToken ct = default);
    Task<T?> GetirAsync(object id, CancellationToken ct = default);
    Task<T> KaydetAsync(T kayit, CancellationToken ct = default);
    Task SilAsync(object id, CancellationToken ct = default);
    bool YazmaYetkisi { get; }
}

public class KayitServisi<T>(
    IFiloDbContextFactory factory,
    IKullaniciOturumu oturum,
    IEnumerable<IValidator<T>> validatorlar) : IKayitServisi<T> where T : class
{
    protected IFiloDbContextFactory Factory { get; } = factory;
    protected IKullaniciOturumu Oturum { get; } = oturum;
    private readonly IValidator<T>? _validator = validatorlar.FirstOrDefault();

    protected virtual string GorIzin => KayitIzinleri.Gor<T>();
    protected virtual string YazIzin => KayitIzinleri.Yaz<T>();
    public bool YazmaYetkisi => Oturum.Yetkili(YazIzin);

    public virtual async Task<List<T>> ListeAsync(Func<IQueryable<T>, IQueryable<T>>? sorgu = null, CancellationToken ct = default)
    {
        Oturum.YetkiGerekli(GorIzin);
        await using var db = Factory.Olustur();
        var q = db.Set<T>().AsNoTracking();
        return await (sorgu?.Invoke(q) ?? q).ToListAsync(ct);
    }

    public virtual async Task<T?> GetirAsync(object id, CancellationToken ct = default)
    {
        Oturum.YetkiGerekli(GorIzin);
        await using var db = Factory.Olustur();
        var k = await db.Set<T>().FindAsync([id], ct);
        if (k is not null) db.Entry(k).State = EntityState.Detached;
        return k;
    }

    public virtual async Task<T> KaydetAsync(T kayit, CancellationToken ct = default)
    {
        Oturum.YetkiGerekli(YazIzin);
        Normallestir(kayit);
        await _validator.DogrulaAsync(kayit, ct);
        await using var db = Factory.Olustur();
        var yeni = db.YeniMi(kayit);
        await OnceKaydetAsync(db, kayit, yeni, ct);
        T hedef;
        if (yeni)
        {
            db.Set<T>().Add(kayit);
            hedef = kayit;
        }
        else
        {
            hedef = await db.BirlestirAsync(kayit, ct);
        }
        await db.KaydetAsync(ct);
        await SonraKaydetAsync(db, hedef, yeni, ct);
        // Yeni RowVer/Id değerlerini arayüzdeki nesneye geri yaz.
        if (!ReferenceEquals(hedef, kayit)) db.Entry(kayit).CurrentValues.SetValues(db.Entry(hedef).CurrentValues);
        return kayit;
    }

    public virtual async Task SilAsync(object id, CancellationToken ct = default)
    {
        Oturum.YetkiGerekli(YazIzin);
        await using var db = Factory.Olustur();
        var k = await db.Set<T>().FindAsync([id], ct) ?? throw new DomainException("Kayıt bulunamadı.");
        await OnceSilAsync(db, k, ct);
        db.Set<T>().Remove(k);
        await db.KaydetAsync(ct);
    }

    /// <summary>Metin alanlarını kırpar, boşları null yapar.</summary>
    protected virtual void Normallestir(T kayit)
    {
        foreach (var p in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string) && p.CanWrite && p.GetSetMethod() is not null))
        {
            var v = (string?)p.GetValue(kayit);
            if (v is null) continue;
            var t = v.Trim();
            var nullable = new System.Reflection.NullabilityInfoContext().Create(p).WriteState == System.Reflection.NullabilityState.Nullable;
            p.SetValue(kayit, t.Length == 0 && nullable ? null : t);
        }
    }

    protected virtual Task OnceKaydetAsync(IFiloDbContext db, T kayit, bool yeni, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task SonraKaydetAsync(IFiloDbContext db, T kayit, bool yeni, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnceSilAsync(IFiloDbContext db, T kayit, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Varlık tipi → görüntüleme/yazma izni.</summary>
public static class KayitIzinleri
{
    private static readonly Dictionary<Type, (string Gor, string Yaz)> Harita = new()
    {
        [typeof(Domain.Entities.Arac)] = (Izinler.FiloGor, Izinler.FiloYaz),
        [typeof(Domain.Entities.Dorse)] = (Izinler.FiloGor, Izinler.FiloYaz),
        [typeof(Domain.Entities.Sofor)] = (Izinler.FiloGor, Izinler.FiloYaz),
        [typeof(Domain.Entities.AracDorseEslesme)] = (Izinler.FiloGor, Izinler.FiloYaz),
        [typeof(Domain.Entities.Cari)] = (Izinler.FinansGor, Izinler.FinansYaz),
        [typeof(Domain.Entities.KasaHesap)] = (Izinler.FinansGor, Izinler.AyarYonet),
        [typeof(Domain.Entities.Evrak)] = (Izinler.BakimGor, Izinler.BakimYaz),
        [typeof(Domain.Entities.BakimPlani)] = (Izinler.BakimGor, Izinler.BakimYaz),
        [typeof(Domain.Entities.Lastik)] = (Izinler.BakimGor, Izinler.BakimYaz),
        [typeof(Domain.Entities.Kullanici)] = (Izinler.KullaniciYonet, Izinler.KullaniciYonet),
        [typeof(Domain.Entities.BankaLimit)] = (Izinler.FinansmanGor, Izinler.FinansmanYaz),
        [typeof(Domain.Entities.Zimmet)] = (Izinler.SoforHesapGor, Izinler.SoforHesapYaz),
    };

    public static string Gor<T>() => Harita.TryGetValue(typeof(T), out var i) ? i.Gor : Izinler.TanimGor;
    public static string Yaz<T>() => Harita.TryGetValue(typeof(T), out var i) ? i.Yaz : Izinler.TanimYaz;
}
