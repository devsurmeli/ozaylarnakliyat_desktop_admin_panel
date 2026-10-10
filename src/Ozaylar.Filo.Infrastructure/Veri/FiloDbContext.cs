using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Veri;

public partial class FiloDbContext : DbContext, IFiloDbContext
{
    public const string Collation = "Turkish_100_CI_AS_SC_UTF8";
    public const string AyarKapanisTarihi = "Donem.KapanisTarihi";

    private readonly IKullaniciOturumu? _oturum;
    private readonly ISaat _saat;

    public FiloDbContext(DbContextOptions<FiloDbContext> options, IKullaniciOturumu? oturum = null, ISaat? saat = null)
        : base(options)
    {
        _oturum = oturum;
        _saat = saat ?? new SistemSaati();
    }

    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<Rol> Roller => Set<Rol>();
    public DbSet<Izin> Izinler => Set<Izin>();
    public DbSet<RolIzin> RolIzinler => Set<RolIzin>();
    public DbSet<KullaniciRol> KullaniciRoller => Set<KullaniciRol>();
    public DbSet<AuditLog> AuditLoglar => Set<AuditLog>();
    public DbSet<Ulke> Ulkeler => Set<Ulke>();
    public DbSet<Il> Iller => Set<Il>();
    public DbSet<Lokasyon> Lokasyonlar => Set<Lokasyon>();
    public DbSet<Guzergah> Guzergahlar => Set<Guzergah>();
    public DbSet<MasrafTuru> MasrafTurleri => Set<MasrafTuru>();
    public DbSet<EvrakTuru> EvrakTurleri => Set<EvrakTuru>();
    public DbSet<DovizKur> DovizKurlari => Set<DovizKur>();
    public DbSet<HarcirahTarife> HarcirahTarifeleri => Set<HarcirahTarife>();
    public DbSet<Ayar> Ayarlar => Set<Ayar>();
    public DbSet<Arac> Araclar => Set<Arac>();
    public DbSet<Dorse> Dorseler => Set<Dorse>();
    public DbSet<AracDorseEslesme> AracDorseEslesmeleri => Set<AracDorseEslesme>();
    public DbSet<Sofor> Soforler => Set<Sofor>();
    public DbSet<Cari> Cariler => Set<Cari>();
    public DbSet<CariHareket> CariHareketleri => Set<CariHareket>();
    public DbSet<KasaHesap> KasaHesaplari => Set<KasaHesap>();
    public DbSet<KasaHareket> KasaHareketleri => Set<KasaHareket>();
    public DbSet<Fatura> Faturalar => Set<Fatura>();
    public DbSet<FaturaKalem> FaturaKalemleri => Set<FaturaKalem>();
    public DbSet<CekSenet> CekSenetler => Set<CekSenet>();
    public DbSet<CekSenetHareket> CekSenetHareketleri => Set<CekSenetHareket>();
    public DbSet<Sefer> Seferler => Set<Sefer>();
    public DbSet<SeferSofor> SeferSoforleri => Set<SeferSofor>();
    public DbSet<SeferDurak> SeferDuraklari => Set<SeferDurak>();
    public DbSet<Masraf> Masraflar => Set<Masraf>();
    public DbSet<Ceza> Cezalar => Set<Ceza>();
    public DbSet<Avans> Avanslar => Set<Avans>();
    public DbSet<Harcirah> Harcirahlar => Set<Harcirah>();
    public DbSet<Mahsup> Mahsuplar => Set<Mahsup>();
    public DbSet<Yakit> Yakitlar => Set<Yakit>();
    public DbSet<HgsGecis> HgsGecisleri => Set<HgsGecis>();
    public DbSet<IceAktarim> IceAktarimlar => Set<IceAktarim>();
    public DbSet<IceAktarimSablon> IceAktarimSablonlari => Set<IceAktarimSablon>();
    public DbSet<BakimPlani> BakimPlanlari => Set<BakimPlani>();
    public DbSet<Bakim> Bakimlar => Set<Bakim>();
    public DbSet<BakimKalem> BakimKalemleri => Set<BakimKalem>();
    public DbSet<Lastik> Lastikler => Set<Lastik>();
    public DbSet<LastikHareket> LastikHareketleri => Set<LastikHareket>();
    public DbSet<AracFinansman> AracFinansmanlari => Set<AracFinansman>();
    public DbSet<FinansmanTaksit> FinansmanTaksitleri => Set<FinansmanTaksit>();
    public DbSet<Evrak> Evraklar => Set<Evrak>();
    public DbSet<Ek> Ekler => Set<Ek>();

    public async Task<long> SiradakiNumaraAsync(string sequenceAdi, CancellationToken ct = default)
    {
        if (!sequenceAdi.All(c => char.IsLetterOrDigit(c) || c == '_'))
            throw new ArgumentException("Geçersiz sequence adı", nameof(sequenceAdi));
        // SqlQueryRaw bir alt sorgu olarak sarar; NEXT VALUE FOR alt sorguda kullanılamadığı için doğrudan komut çalıştırılır.
        var conn = Database.GetDbConnection();
        var kapat = conn.State != System.Data.ConnectionState.Open;
        if (kapat) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT NEXT VALUE FOR dbo.{sequenceAdi}";
            cmd.Transaction = Database.CurrentTransaction?.GetDbTransaction();
            return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        }
        finally
        {
            if (kapat) await conn.CloseAsync();
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Yer tasarrufu: UTF-8 collation + varchar (Türkçe karakter 2, diğerleri 1 byte). Parametreler de varchar gider (index kullanımı).
        b.Properties<string>().AreUnicode(false).HaveMaxLength(200);
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<DateTime>().HaveColumnType("datetime2(0)");
        b.Properties<DateTime?>().HaveColumnType("datetime2(0)");
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.UseCollation(Collation);
        mb.HasSequence<long>("SeferNoSeq").StartsAt(1);
        mb.HasSequence<long>("CariKodSeq").StartsAt(1);
        mb.HasSequence<long>("PortfoyNoSeq").StartsAt(1);
        // Tahsilat/ödeme/virman gibi tablosuz işlemlerde defter satırlarını gruplayan ortak KaynakId.
        mb.HasSequence<int>("IslemNoSeq").StartsAt(1);

        mb.ApplyConfigurationsFromAssembly(typeof(FiloDbContext).Assembly, t => t.Namespace == typeof(FiloDbContext).Namespace + ".Konfigurasyon");

        foreach (var et in mb.Model.GetEntityTypes())
        {
            var clr = et.ClrType;
            if (clr == typeof(EkIcerik)) continue;

            // Varsayılan olarak cascade silme yok; alt kayıtlar konfigürasyonda açıkça cascade yapılır.
            foreach (var fk in et.GetForeignKeys().Where(f => !f.IsOwnership && f.DeleteBehavior == DeleteBehavior.Cascade))
                if (!CascadeIzinli.Contains((fk.DeclaringEntityType.ClrType, fk.PrincipalEntityType.ClrType)))
                    fk.DeleteBehavior = DeleteBehavior.Restrict;

            if (typeof(IGuncellemeBilgisi).IsAssignableFrom(clr))
                mb.Entity(clr).Property(nameof(IGuncellemeBilgisi.RowVer)).IsRowVersion();

            if (typeof(ISoftDelete).IsAssignableFrom(clr))
            {
                var p = Expression.Parameter(clr, "e");
                var body = Expression.Equal(Expression.Property(p, nameof(ISoftDelete.IsDeleted)), Expression.Constant(false));
                mb.Entity(clr).HasQueryFilter(Expression.Lambda(body, p));
            }
        }

        Seed.Uygula(mb);
    }

    private static readonly HashSet<(Type Child, Type Parent)> CascadeIzinli =
    [
        (typeof(FaturaKalem), typeof(Fatura)),
        (typeof(BakimKalem), typeof(Bakim)),
        (typeof(SeferSofor), typeof(Sefer)),
        (typeof(SeferDurak), typeof(Sefer)),
        (typeof(RolIzin), typeof(Rol)),
        (typeof(KullaniciRol), typeof(Kullanici)),
        (typeof(FinansmanTaksit), typeof(AracFinansman)),
        (typeof(CekSenetHareket), typeof(CekSenet)),
        (typeof(LastikHareket), typeof(Lastik)),
    ];

    // ---------------- SaveChanges: denetim, soft delete, dönem kilidi ----------------

    private DateOnly? _kapanisTarihi;
    private bool _kapanisOkundu;

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        ChangeTracker.DetectChanges();
        var simdi = _saat.UtcNow;
        var kullaniciId = _oturum?.KullaniciId;
        var auditBekleyen = new List<(EntityEntry Entry, AuditIslem Islem, string? Degisiklik)>();

        foreach (var e in ChangeTracker.Entries().ToList())
        {
            if (e.State is EntityState.Unchanged or EntityState.Detached) continue;

            if (e.Entity is DefterKaydi && e.State != EntityState.Added)
                throw new DomainException("Defter kayıtları değiştirilemez veya silinemez; ters kayıt kullanın.");

            if (e.Entity is IDonemKilitli dk)
                await DonemKontrolAsync(e, dk, ct);

            if (e.State == EntityState.Deleted && e.Entity is ISoftDelete sd)
            {
                e.State = EntityState.Modified;
                sd.IsDeleted = true;
            }

            switch (e.State)
            {
                case EntityState.Added:
                    if (e.Entity is IOlusturmaBilgisi ob)
                    {
                        ob.OlusturanId ??= kullaniciId;
                        if (ob.OlusturmaTarihi == default) ob.OlusturmaTarihi = simdi;
                    }
                    if (Denetlenir(e)) auditBekleyen.Add((e, AuditIslem.Ekle, null));
                    break;
                case EntityState.Modified:
                    if (e.Entity is IGuncellemeBilgisi gb)
                    {
                        gb.GuncelleyenId = kullaniciId;
                        gb.GuncellemeTarihi = simdi;
                    }
                    if (Denetlenir(e))
                    {
                        var silme = e.Entity is ISoftDelete { IsDeleted: true } && e.Property(nameof(ISoftDelete.IsDeleted)).IsModified;
                        auditBekleyen.Add((e, silme ? AuditIslem.Sil : AuditIslem.Guncelle, silme ? null : Degisiklikler(e)));
                    }
                    break;
                case EntityState.Deleted:
                    if (Denetlenir(e)) auditBekleyen.Add((e, AuditIslem.Sil, null));
                    break;
            }
        }

        if (auditBekleyen.Count == 0)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        // Eklenen kayıtların Id'si kayıttan sonra belli olur: iki aşamalı kayıt, tek transaction.
        var disTransaction = Database.CurrentTransaction;
        var tx = disTransaction ?? await Database.BeginTransactionAsync(ct);
        try
        {
            var meta = auditBekleyen.Select(a => (a.Entry.Metadata.GetTableName() ?? a.Entry.Metadata.ClrType.Name, a.Entry, a.Islem, a.Degisiklik)).ToList();
            var sonuc = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            foreach (var (tablo, entry, islem, degisiklik) in meta)
            {
                AuditLoglar.Add(new AuditLog
                {
                    Tarih = simdi,
                    KullaniciId = kullaniciId,
                    Tablo = tablo,
                    KayitId = AnahtarMetni(entry),
                    Islem = islem,
                    Degisiklik = degisiklik,
                });
            }
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            if (disTransaction is null) await tx.CommitAsync(ct);
            return sonuc;
        }
        catch
        {
            if (disTransaction is null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (disTransaction is null) await tx.DisposeAsync();
        }
    }

    private static bool Denetlenir(EntityEntry e) => e.Entity is IGuncellemeBilgisi;

    private static string AnahtarMetni(EntityEntry e) =>
        string.Join(",", e.Metadata.FindPrimaryKey()!.Properties.Select(p => e.Property(p.Name).CurrentValue?.ToString()));

    private static string? Degisiklikler(EntityEntry e)
    {
        var dict = new Dictionary<string, object?[]>();
        foreach (var p in e.Properties)
        {
            if (!p.IsModified || p.Metadata.IsConcurrencyToken) continue;
            var ad = p.Metadata.Name;
            if (ad is nameof(IGuncellemeBilgisi.GuncelleyenId) or nameof(IGuncellemeBilgisi.GuncellemeTarihi) or nameof(Kullanici.SifreHash)) continue;
            if (Equals(p.OriginalValue, p.CurrentValue)) continue;
            dict[ad] = [p.OriginalValue, p.CurrentValue];
        }
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
    }

    private async Task DonemKontrolAsync(EntityEntry e, IDonemKilitli dk, CancellationToken ct)
    {
        if (!_kapanisOkundu)
        {
            var deger = await Ayarlar.AsNoTracking().Where(a => a.Anahtar == AyarKapanisTarihi).Select(a => a.Deger).FirstOrDefaultAsync(ct);
            _kapanisTarihi = DateOnly.TryParse(deger, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : null;
            _kapanisOkundu = true;
        }
        if (_kapanisTarihi is null) return;
        var tarih = dk.Tarih;
        if (e.State == EntityState.Modified)
        {
            // Eski tarih de kapalı dönemdeyse değiştirilemez.
            var eski = e.Properties.FirstOrDefault(p => p.Metadata.Name == "Tarih")?.OriginalValue;
            if (eski is DateOnly eskiTarih && eskiTarih <= _kapanisTarihi)
                throw new DomainException($"{_kapanisTarihi:dd.MM.yyyy} tarihine kadar dönem kapalıdır; bu kayıt değiştirilemez.");
        }
        if (tarih <= _kapanisTarihi)
            throw new DomainException($"{_kapanisTarihi:dd.MM.yyyy} tarihine kadar dönem kapalıdır; bu tarihe kayıt girilemez.");
    }
}

