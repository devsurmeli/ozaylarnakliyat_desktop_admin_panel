using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Abstractions;

public interface IFiloDbContext : IDisposable, IAsyncDisposable
{
    DbSet<Kullanici> Kullanicilar { get; }
    DbSet<Rol> Roller { get; }
    DbSet<Izin> Izinler { get; }
    DbSet<RolIzin> RolIzinler { get; }
    DbSet<KullaniciRol> KullaniciRoller { get; }
    DbSet<AuditLog> AuditLoglar { get; }

    DbSet<Ulke> Ulkeler { get; }
    DbSet<Il> Iller { get; }
    DbSet<Lokasyon> Lokasyonlar { get; }
    DbSet<Guzergah> Guzergahlar { get; }
    DbSet<MasrafTuru> MasrafTurleri { get; }
    DbSet<EvrakTuru> EvrakTurleri { get; }
    DbSet<DovizKur> DovizKurlari { get; }
    DbSet<HarcirahTarife> HarcirahTarifeleri { get; }
    DbSet<Ayar> Ayarlar { get; }

    DbSet<Arac> Araclar { get; }
    DbSet<Dorse> Dorseler { get; }
    DbSet<AracDorseEslesme> AracDorseEslesmeleri { get; }
    DbSet<Sofor> Soforler { get; }

    DbSet<Cari> Cariler { get; }
    DbSet<CariHareket> CariHareketleri { get; }
    DbSet<KasaHesap> KasaHesaplari { get; }
    DbSet<KasaHareket> KasaHareketleri { get; }
    DbSet<Fatura> Faturalar { get; }
    DbSet<FaturaKalem> FaturaKalemleri { get; }
    DbSet<CekSenet> CekSenetler { get; }
    DbSet<CekSenetHareket> CekSenetHareketleri { get; }

    DbSet<Sefer> Seferler { get; }
    DbSet<SeferSofor> SeferSoforleri { get; }
    DbSet<SeferDurak> SeferDuraklari { get; }
    DbSet<Masraf> Masraflar { get; }
    DbSet<Ceza> Cezalar { get; }
    DbSet<Avans> Avanslar { get; }
    DbSet<Harcirah> Harcirahlar { get; }
    DbSet<Mahsup> Mahsuplar { get; }
    DbSet<Yakit> Yakitlar { get; }
    DbSet<HgsGecis> HgsGecisleri { get; }
    DbSet<IceAktarim> IceAktarimlar { get; }
    DbSet<IceAktarimSablon> IceAktarimSablonlari { get; }

    DbSet<BakimPlani> BakimPlanlari { get; }
    DbSet<Bakim> Bakimlar { get; }
    DbSet<BakimKalem> BakimKalemleri { get; }
    DbSet<Lastik> Lastikler { get; }
    DbSet<LastikHareket> LastikHareketleri { get; }
    DbSet<Kredi> Krediler { get; }
    DbSet<KrediTaksit> KrediTaksitleri { get; }
    DbSet<KrediOdeme> KrediOdemeleri { get; }
    DbSet<KrediArac> KrediAraclari { get; }
    DbSet<BankaLimit> BankaLimitleri { get; }
    DbSet<SoforAracAtama> SoforAracAtamalari { get; }
    DbSet<Zimmet> Zimmetler { get; }
    DbSet<PersonelTahakkuk> PersonelTahakkuklari { get; }
    DbSet<AracKmKayit> AracKmKayitlari { get; }
    DbSet<Evrak> Evraklar { get; }
    DbSet<Ek> Ekler { get; }

    DatabaseFacade Database { get; }
    DbSet<T> Set<T>() where T : class;
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> Entry<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>SQL SEQUENCE'dan sonraki değeri alır (belge numaraları).</summary>
    Task<long> SiradakiNumaraAsync(string sequenceAdi, CancellationToken ct = default);
}

/// <summary>Her işlem için kısa ömürlü bir DbContext üretir (WPF'te uzun ömürlü context kullanılmaz).</summary>
public interface IFiloDbContextFactory
{
    IFiloDbContext Olustur();
}

/// <summary>Ek içeriklerinin tutulduğu ayrı veritabanı.</summary>
public interface IEkDepo
{
    Task<int> KaydetAsync(byte[] icerik, CancellationToken ct = default);
    Task<byte[]?> OkuAsync(int ekIcerikId, CancellationToken ct = default);
    Task SilAsync(int ekIcerikId, CancellationToken ct = default);
}

/// <summary>
/// Zaman kaynağı. Denetim/oluşturma damgaları UTC; kullanıcının girdiği iş zamanları
/// (sefer çıkış/teslim, yakıt alımı, HGS geçişi) Türkiye yerel saatidir.
/// </summary>
public interface ISaat
{
    DateTime UtcNow { get; }
    /// <summary>Türkiye yerel saati.</summary>
    DateTime Simdi { get; }
    DateOnly Bugun { get; }
}

public sealed class SistemSaati : ISaat
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Simdi => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz), DateTimeKind.Unspecified);
    public DateOnly Bugun => DateOnly.FromDateTime(Simdi);
}
