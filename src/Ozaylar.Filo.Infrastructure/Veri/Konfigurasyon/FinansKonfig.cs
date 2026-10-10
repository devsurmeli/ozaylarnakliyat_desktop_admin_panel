using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Veri.Konfigurasyon;

internal sealed class CariKonfig : IEntityTypeConfiguration<Cari>
{
    public void Configure(EntityTypeBuilder<Cari> b)
    {
        b.ToTable("Cari");
        b.Property(x => x.Kod).HasMaxLength(20);
        b.Property(x => x.Unvan).HasMaxLength(200);
        b.Property(x => x.VergiDairesi).HasMaxLength(60);
        b.Property(x => x.VergiNo).HasMaxLength(11);
        b.Property(x => x.Telefon).HasMaxLength(20);
        b.Property(x => x.Eposta).HasMaxLength(100);
        b.Property(x => x.Adres).HasMaxLength(400);
        b.Property(x => x.Yetkili).HasMaxLength(100);
        b.Property(x => x.IBAN).HasMaxLength(34);
        b.Property(x => x.Notlar).HasMaxLength(500);
        b.HasIndex(x => x.Kod).IsUnique();
        b.HasIndex(x => x.Unvan);
        b.HasIndex(x => x.VergiNo).IsUnique().HasFilter("[VergiNo] IS NOT NULL AND [IsDeleted] = 0");
    }
}

internal sealed class CariHareketKonfig : IEntityTypeConfiguration<CariHareket>
{
    public void Configure(EntityTypeBuilder<CariHareket> b)
    {
        b.ToTable("CariHareket", t => t.HasCheckConstraint("CK_CariHareket_Tutar", "[Borc] >= 0 AND [Alacak] >= 0"));
        b.Property(x => x.Kur).HasPrecision(18, 6);
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasIndex(x => new { x.CariId, x.Tarih }).IncludeProperties(x => new { x.Borc, x.Alacak });
        b.HasIndex(x => new { x.KaynakTip, x.KaynakId });
        b.HasIndex(x => x.TersKayitId).HasFilter("[TersKayitId] IS NOT NULL");
        b.HasIndex(x => new { x.AracId, x.Tarih }).HasFilter("[AracId] IS NOT NULL");
        b.HasIndex(x => x.SeferId).HasFilter("[SeferId] IS NOT NULL");
    }
}

internal sealed class KasaHesapKonfig : IEntityTypeConfiguration<KasaHesap>
{
    public void Configure(EntityTypeBuilder<KasaHesap> b)
    {
        b.ToTable("KasaHesap");
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Ad).HasMaxLength(60);
        b.Property(x => x.BankaAdi).HasMaxLength(60);
        b.Property(x => x.Sube).HasMaxLength(60);
        b.Property(x => x.IBAN).HasMaxLength(34);
    }
}

internal sealed class KasaHareketKonfig : IEntityTypeConfiguration<KasaHareket>
{
    public void Configure(EntityTypeBuilder<KasaHareket> b)
    {
        b.ToTable("KasaHareket", t => t.HasCheckConstraint("CK_KasaHareket_Tutar", "[Giris] >= 0 AND [Cikis] >= 0"));
        b.Property(x => x.Kur).HasPrecision(18, 6);
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasIndex(x => new { x.KasaHesapId, x.Tarih }).IncludeProperties(x => new { x.Giris, x.Cikis });
        b.HasIndex(x => new { x.KaynakTip, x.KaynakId });
        b.HasIndex(x => x.TersKayitId).HasFilter("[TersKayitId] IS NOT NULL");
    }
}

internal sealed class FaturaKonfig : IEntityTypeConfiguration<Fatura>
{
    public void Configure(EntityTypeBuilder<Fatura> b)
    {
        b.ToTable("Fatura");
        b.Property(x => x.FaturaNo).HasMaxLength(30);
        b.Property(x => x.EBelgeNo).HasMaxLength(50);
        b.Property(x => x.Aciklama).HasMaxLength(500);
        b.Property(x => x.Kur).HasPrecision(18, 6);
        b.HasIndex(x => new { x.FaturaTipi, x.CariId, x.FaturaNo }).IsUnique();
        b.HasIndex(x => x.Tarih);
    }
}

internal sealed class FaturaKalemKonfig : IEntityTypeConfiguration<FaturaKalem>
{
    public void Configure(EntityTypeBuilder<FaturaKalem> b)
    {
        b.ToTable("FaturaKalem");
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.Property(x => x.Birim).HasMaxLength(10);
        b.Property(x => x.Miktar).HasPrecision(12, 3);
        b.Property(x => x.BirimFiyat).HasPrecision(18, 4);
        b.Property(x => x.IskontoOrani).HasPrecision(5, 2);
        b.HasOne(x => x.Fatura).WithMany(f => f.Kalemler).HasForeignKey(x => x.FaturaId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.SeferId).HasFilter("[SeferId] IS NOT NULL");
    }
}

internal sealed class CekSenetKonfig : IEntityTypeConfiguration<CekSenet>
{
    public void Configure(EntityTypeBuilder<CekSenet> b)
    {
        b.ToTable("CekSenet");
        b.Property(x => x.PortfoyNo).HasMaxLength(20);
        b.Property(x => x.SeriNo).HasMaxLength(30);
        b.Property(x => x.Banka).HasMaxLength(60);
        b.Property(x => x.Sube).HasMaxLength(60);
        b.Property(x => x.Kesideci).HasMaxLength(150);
        b.Property(x => x.Aciklama).HasMaxLength(300);
        b.HasIndex(x => x.PortfoyNo).IsUnique();
        b.HasIndex(x => new { x.Durum, x.VadeTarihi });
    }
}

internal sealed class CekSenetHareketKonfig : IEntityTypeConfiguration<CekSenetHareket>
{
    public void Configure(EntityTypeBuilder<CekSenetHareket> b)
    {
        b.ToTable("CekSenetHareket");
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasOne<CekSenet>().WithMany(c => c.Hareketler).HasForeignKey(x => x.CekSenetId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Cari>().WithMany().HasForeignKey(x => x.CariId);
        b.HasOne<KasaHesap>().WithMany().HasForeignKey(x => x.KasaHesapId);
    }
}
