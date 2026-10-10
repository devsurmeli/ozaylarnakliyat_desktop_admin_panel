using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Veri.Konfigurasyon;

internal sealed class KullaniciKonfig : IEntityTypeConfiguration<Kullanici>
{
    public void Configure(EntityTypeBuilder<Kullanici> b)
    {
        b.ToTable("Kullanici");
        b.Property(x => x.KullaniciAdi).HasMaxLength(50);
        b.Property(x => x.SifreHash).HasMaxLength(200);
        b.Property(x => x.AdSoyad).HasMaxLength(100);
        b.Property(x => x.Eposta).HasMaxLength(100);
        b.HasIndex(x => x.KullaniciAdi).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class RolKonfig : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.ToTable("Rol");
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Ad).HasMaxLength(50);
        b.HasIndex(x => x.Ad).IsUnique();
    }
}

internal sealed class IzinKonfig : IEntityTypeConfiguration<Izin>
{
    public void Configure(EntityTypeBuilder<Izin> b)
    {
        b.ToTable("Izin");
        b.Property(x => x.Kod).HasMaxLength(60);
        b.Property(x => x.Ad).HasMaxLength(100);
        b.Property(x => x.Grup).HasMaxLength(50);
        b.HasIndex(x => x.Kod).IsUnique();
    }
}

internal sealed class RolIzinKonfig : IEntityTypeConfiguration<RolIzin>
{
    public void Configure(EntityTypeBuilder<RolIzin> b)
    {
        b.ToTable("RolIzin");
        b.HasKey(x => new { x.RolId, x.IzinId });
        b.HasOne(x => x.Rol).WithMany(r => r.Izinler).HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Izin).WithMany().HasForeignKey(x => x.IzinId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class KullaniciRolKonfig : IEntityTypeConfiguration<KullaniciRol>
{
    public void Configure(EntityTypeBuilder<KullaniciRol> b)
    {
        b.ToTable("KullaniciRol");
        b.HasKey(x => new { x.KullaniciId, x.RolId });
        b.HasOne(x => x.Kullanici).WithMany(k => k.Roller).HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId);
    }
}

internal sealed class AuditLogKonfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLog");
        b.Property(x => x.Tablo).HasMaxLength(50);
        b.Property(x => x.KayitId).HasMaxLength(40);
        b.Property(x => x.Degisiklik).HasMaxLength(-1);
        b.HasIndex(x => new { x.Tablo, x.KayitId });
        b.HasIndex(x => x.Tarih);
    }
}

internal sealed class UlkeKonfig : IEntityTypeConfiguration<Ulke>
{
    public void Configure(EntityTypeBuilder<Ulke> b)
    {
        b.ToTable("Ulke");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Kod).HasMaxLength(2).IsFixedLength();
        b.Property(x => x.Ad).HasMaxLength(60);
        b.HasIndex(x => x.Kod).IsUnique();
    }
}

internal sealed class IlKonfig : IEntityTypeConfiguration<Il>
{
    public void Configure(EntityTypeBuilder<Il> b)
    {
        b.ToTable("Il");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Ad).HasMaxLength(30);
    }
}

internal sealed class LokasyonKonfig : IEntityTypeConfiguration<Lokasyon>
{
    public void Configure(EntityTypeBuilder<Lokasyon> b)
    {
        b.ToTable("Lokasyon");
        b.Property(x => x.Ad).HasMaxLength(100);
        b.Property(x => x.Ilce).HasMaxLength(50);
        b.Property(x => x.Adres).HasMaxLength(300);
        b.HasIndex(x => x.Ad);
    }
}

internal sealed class GuzergahKonfig : IEntityTypeConfiguration<Guzergah>
{
    public void Configure(EntityTypeBuilder<Guzergah> b)
    {
        b.ToTable("Guzergah");
        b.Property(x => x.Ad).HasMaxLength(150);
        b.HasOne(x => x.CikisLokasyon).WithMany().HasForeignKey(x => x.CikisLokasyonId);
        b.HasOne(x => x.VarisLokasyon).WithMany().HasForeignKey(x => x.VarisLokasyonId);
    }
}

internal sealed class MasrafTuruKonfig : IEntityTypeConfiguration<MasrafTuru>
{
    public void Configure(EntityTypeBuilder<MasrafTuru> b)
    {
        b.ToTable("MasrafTuru");
        b.Property(x => x.Ad).HasMaxLength(60);
        b.HasIndex(x => x.Ad).IsUnique();
    }
}

internal sealed class EvrakTuruKonfig : IEntityTypeConfiguration<EvrakTuru>
{
    public void Configure(EntityTypeBuilder<EvrakTuru> b)
    {
        b.ToTable("EvrakTuru");
        b.Property(x => x.Ad).HasMaxLength(60);
        b.HasIndex(x => x.Ad).IsUnique();
    }
}

internal sealed class DovizKurKonfig : IEntityTypeConfiguration<DovizKur>
{
    public void Configure(EntityTypeBuilder<DovizKur> b)
    {
        b.ToTable("DovizKur");
        b.Property(x => x.Alis).HasPrecision(18, 6);
        b.Property(x => x.Satis).HasPrecision(18, 6);
        b.HasIndex(x => new { x.Tarih, x.ParaBirimi }).IsUnique();
    }
}

internal sealed class HarcirahTarifeKonfig : IEntityTypeConfiguration<HarcirahTarife>
{
    public void Configure(EntityTypeBuilder<HarcirahTarife> b)
    {
        b.ToTable("HarcirahTarife");
        b.HasIndex(x => new { x.UlkeId, x.GecerlilikBaslangic });
    }
}

internal sealed class AyarKonfig : IEntityTypeConfiguration<Ayar>
{
    public void Configure(EntityTypeBuilder<Ayar> b)
    {
        b.ToTable("Ayar");
        b.HasKey(x => x.Anahtar);
        b.Property(x => x.Anahtar).HasMaxLength(50);
        b.Property(x => x.Deger).HasMaxLength(400);
    }
}

internal sealed class AracKonfig : IEntityTypeConfiguration<Arac>
{
    public void Configure(EntityTypeBuilder<Arac> b)
    {
        b.ToTable("Arac");
        b.Property(x => x.Plaka).HasMaxLength(15);
        b.Property(x => x.Marka).HasMaxLength(40);
        b.Property(x => x.Model).HasMaxLength(60);
        b.Property(x => x.SaseNo).HasMaxLength(20);
        b.Property(x => x.MotorNo).HasMaxLength(30);
        b.Property(x => x.HgsEtiketNo).HasMaxLength(30);
        b.Property(x => x.Notlar).HasMaxLength(500);
        b.Property(x => x.BeklenenTuketim).HasPrecision(5, 2);
        b.HasIndex(x => x.Plaka).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.HgsEtiketNo);
    }
}

internal sealed class DorseKonfig : IEntityTypeConfiguration<Dorse>
{
    public void Configure(EntityTypeBuilder<Dorse> b)
    {
        b.ToTable("Dorse");
        b.Property(x => x.Plaka).HasMaxLength(15);
        b.Property(x => x.Marka).HasMaxLength(40);
        b.Property(x => x.SaseNo).HasMaxLength(20);
        b.Property(x => x.Notlar).HasMaxLength(500);
        b.Property(x => x.KapasiteTon).HasPrecision(9, 2);
        b.Property(x => x.HacimM3).HasPrecision(9, 2);
        b.HasIndex(x => x.Plaka).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class AracDorseEslesmeKonfig : IEntityTypeConfiguration<AracDorseEslesme>
{
    public void Configure(EntityTypeBuilder<AracDorseEslesme> b)
    {
        b.ToTable("AracDorseEslesme");
        b.HasIndex(x => new { x.AracId, x.Baslangic });
        b.HasIndex(x => new { x.DorseId, x.Baslangic });
        b.ToTable(t => t.HasCheckConstraint("CK_AracDorseEslesme_Tarih", "[Bitis] IS NULL OR [Bitis] >= [Baslangic]"));
    }
}

internal sealed class SoforKonfig : IEntityTypeConfiguration<Sofor>
{
    public void Configure(EntityTypeBuilder<Sofor> b)
    {
        b.ToTable("Sofor");
        b.Property(x => x.TCKN).HasMaxLength(11).IsFixedLength();
        b.Property(x => x.Ad).HasMaxLength(50);
        b.Property(x => x.Soyad).HasMaxLength(50);
        b.Property(x => x.Telefon).HasMaxLength(20);
        b.Property(x => x.Eposta).HasMaxLength(100);
        b.Property(x => x.EhliyetSinifi).HasMaxLength(10);
        b.Property(x => x.EhliyetNo).HasMaxLength(20);
        b.Property(x => x.IBAN).HasMaxLength(34);
        b.Property(x => x.Adres).HasMaxLength(300);
        b.Property(x => x.Notlar).HasMaxLength(500);
        b.Ignore(x => x.AdSoyad);
        b.HasIndex(x => x.TCKN).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.CariId).IsUnique();
    }
}
