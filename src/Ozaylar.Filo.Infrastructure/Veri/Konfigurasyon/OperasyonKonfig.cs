using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Veri.Konfigurasyon;

internal sealed class SeferKonfig : IEntityTypeConfiguration<Sefer>
{
    public void Configure(EntityTypeBuilder<Sefer> b)
    {
        b.ToTable("Sefer", t =>
        {
            t.HasCheckConstraint("CK_Sefer_Km", "[DonusKm] IS NULL OR [CikisKm] IS NULL OR [DonusKm] >= [CikisKm]");
            t.HasCheckConstraint("CK_Sefer_AracVeyaTaseron", "[AracId] IS NOT NULL OR [TaseronCariId] IS NOT NULL");
        });
        b.Property(x => x.SeferNo).HasMaxLength(20);
        b.Property(x => x.TaseronPlaka).HasMaxLength(15);
        b.Property(x => x.YukTanimi).HasMaxLength(200);
        b.Property(x => x.CmrNo).HasMaxLength(30);
        b.Property(x => x.IrsaliyeNo).HasMaxLength(30);
        b.Property(x => x.Notlar).HasMaxLength(500);
        b.Property(x => x.Tonaj).HasPrecision(9, 2);
        b.Property(x => x.NavlunKur).HasPrecision(18, 6);
        b.Property(x => x.TaseronKur).HasPrecision(18, 6);
        b.Ignore(x => x.TaseronSeferi);
        b.Ignore(x => x.ToplamKm);

        b.HasOne(x => x.Musteri).WithMany().HasForeignKey(x => x.MusteriCariId);
        b.HasOne(x => x.Taseron).WithMany().HasForeignKey(x => x.TaseronCariId);
        b.HasOne(x => x.CikisLokasyon).WithMany().HasForeignKey(x => x.CikisLokasyonId);
        b.HasOne(x => x.VarisLokasyon).WithMany().HasForeignKey(x => x.VarisLokasyonId);
        b.HasOne<Sefer>().WithMany().HasForeignKey(x => x.BagliSeferId);

        b.HasIndex(x => x.SeferNo).IsUnique();
        b.HasIndex(x => new { x.AracId, x.PlanlananTarih });
        b.HasIndex(x => new { x.MusteriCariId, x.PlanlananTarih });
        b.HasIndex(x => new { x.Durum, x.PlanlananTarih });
    }
}

internal sealed class SeferSoforKonfig : IEntityTypeConfiguration<SeferSofor>
{
    public void Configure(EntityTypeBuilder<SeferSofor> b)
    {
        b.ToTable("SeferSofor");
        b.HasKey(x => new { x.SeferId, x.SoforId });
        b.HasOne(x => x.Sefer).WithMany(s => s.Soforler).HasForeignKey(x => x.SeferId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.SoforId);
    }
}

internal sealed class SeferDurakKonfig : IEntityTypeConfiguration<SeferDurak>
{
    public void Configure(EntityTypeBuilder<SeferDurak> b)
    {
        b.ToTable("SeferDurak");
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasOne<Sefer>().WithMany(s => s.Duraklar).HasForeignKey(x => x.SeferId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MasrafKonfig : IEntityTypeConfiguration<Masraf>
{
    public void Configure(EntityTypeBuilder<Masraf> b)
    {
        b.ToTable("Masraf", t =>
        {
            t.HasCheckConstraint("CK_Masraf_Merkez",
                "([Merkez] = 1 AND [SeferId] IS NOT NULL) OR ([Merkez] = 2 AND [AracId] IS NOT NULL) OR ([Merkez] = 3 AND [DorseId] IS NOT NULL) OR [Merkez] = 4");
            t.HasCheckConstraint("CK_Masraf_SoforNakit", "[OdemeSekli] <> 1 OR [SoforId] IS NOT NULL");
            t.HasCheckConstraint("CK_Masraf_Tutar", "[Tutar] > 0");
        });
        b.Property(x => x.Kur).HasPrecision(18, 6);
        b.Property(x => x.BelgeNo).HasMaxLength(30);
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasIndex(x => new { x.AracId, x.Tarih });
        b.HasIndex(x => x.SeferId).HasFilter("[SeferId] IS NOT NULL");
        b.HasIndex(x => new { x.SoforId, x.MahsupId }).HasFilter("[SoforId] IS NOT NULL");
        b.HasIndex(x => x.Tarih);
    }
}

internal sealed class CezaKonfig : IEntityTypeConfiguration<Ceza>
{
    public void Configure(EntityTypeBuilder<Ceza> b)
    {
        b.ToTable("Ceza");
        b.Property(x => x.CezaTuru).HasMaxLength(100);
        b.Property(x => x.TutanakNo).HasMaxLength(30);
        b.Property(x => x.Aciklama).HasMaxLength(300);
        b.HasOne<Sefer>().WithMany().HasForeignKey(x => x.SeferId);
        b.HasOne<KasaHesap>().WithMany().HasForeignKey(x => x.OdemeKasaHesapId);
        b.HasIndex(x => new { x.AracId, x.Tarih });
    }
}

internal sealed class AvansKonfig : IEntityTypeConfiguration<Avans>
{
    public void Configure(EntityTypeBuilder<Avans> b)
    {
        b.ToTable("Avans", t => t.HasCheckConstraint("CK_Avans_Tutar", "[Tutar] > 0"));
        b.Property(x => x.Kur).HasPrecision(18, 6);
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasOne<KasaHesap>().WithMany().HasForeignKey(x => x.KasaHesapId);
        b.HasOne<Mahsup>().WithMany().HasForeignKey(x => x.MahsupId);
        b.HasIndex(x => new { x.SoforId, x.MahsupId });
    }
}

internal sealed class HarcirahKonfig : IEntityTypeConfiguration<Harcirah>
{
    public void Configure(EntityTypeBuilder<Harcirah> b)
    {
        b.ToTable("Harcirah", t => t.HasCheckConstraint("CK_Harcirah_Tarih", "[Bitis] >= [Baslangic]"));
        b.Ignore(x => x.Tarih);
        b.Property(x => x.GunSayisi).HasPrecision(5, 1);
        b.Property(x => x.Kur).HasPrecision(18, 6);
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasOne<Ulke>().WithMany().HasForeignKey(x => x.UlkeId);
        b.HasOne<Mahsup>().WithMany().HasForeignKey(x => x.MahsupId);
        b.HasIndex(x => new { x.SoforId, x.MahsupId });
        b.HasIndex(x => x.SeferId).HasFilter("[SeferId] IS NOT NULL");
    }
}

internal sealed class MahsupKonfig : IEntityTypeConfiguration<Mahsup>
{
    public void Configure(EntityTypeBuilder<Mahsup> b)
    {
        b.ToTable("Mahsup");
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.HasOne<KasaHesap>().WithMany().HasForeignKey(x => x.KasaHesapId);
        b.HasIndex(x => new { x.SoforId, x.Tarih });
    }
}

internal sealed class YakitKonfig : IEntityTypeConfiguration<Yakit>
{
    public void Configure(EntityTypeBuilder<Yakit> b)
    {
        b.ToTable("Yakit", t =>
        {
            t.HasCheckConstraint("CK_Yakit_Litre", "[Litre] > 0");
            t.HasCheckConstraint("CK_Yakit_SoforNakit", "[OdemeSekli] <> 1 OR [SoforId] IS NOT NULL");
        });
        b.Ignore(x => x.Tarih);
        b.Property(x => x.Litre).HasPrecision(9, 2);
        b.Property(x => x.BirimFiyat).HasPrecision(9, 3);
        b.Property(x => x.Kur).HasPrecision(18, 6);
        b.Property(x => x.IstasyonAdi).HasMaxLength(100);
        b.Property(x => x.KartNo).HasMaxLength(30);
        b.Property(x => x.FisNo).HasMaxLength(30);
        b.HasOne<Cari>().WithMany().HasForeignKey(x => x.IstasyonCariId);
        b.HasOne<IceAktarim>().WithMany().HasForeignKey(x => x.IceAktarimId);
        b.HasIndex(x => new { x.AracId, x.AlimZamani });
        b.HasIndex(x => x.SeferId).HasFilter("[SeferId] IS NOT NULL");
        b.HasIndex(x => new { x.SoforId, x.MahsupId }).HasFilter("[SoforId] IS NOT NULL");
    }
}

internal sealed class HgsGecisKonfig : IEntityTypeConfiguration<HgsGecis>
{
    public void Configure(EntityTypeBuilder<HgsGecis> b)
    {
        b.ToTable("HgsGecis");
        b.Property(x => x.GirisYeri).HasMaxLength(60);
        b.Property(x => x.CikisYeri).HasMaxLength(60);
        b.HasOne<IceAktarim>().WithMany().HasForeignKey(x => x.IceAktarimId);
        // Aynı ekstrenin iki kez aktarılmasını engeller.
        b.HasIndex(x => new { x.AracId, x.GecisZamani, x.Tutar }).IsUnique();
        b.HasIndex(x => x.SeferId).HasFilter("[SeferId] IS NOT NULL");
    }
}

internal sealed class IceAktarimKonfig : IEntityTypeConfiguration<IceAktarim>
{
    public void Configure(EntityTypeBuilder<IceAktarim> b)
    {
        b.ToTable("IceAktarim");
        b.Property(x => x.DosyaAdi).HasMaxLength(200);
        b.Property(x => x.Hatalar).HasMaxLength(-1);
        b.HasOne<IceAktarimSablon>().WithMany().HasForeignKey(x => x.SablonId);
        b.HasOne<Cari>().WithMany().HasForeignKey(x => x.CariId);
    }
}

internal sealed class IceAktarimSablonKonfig : IEntityTypeConfiguration<IceAktarimSablon>
{
    public void Configure(EntityTypeBuilder<IceAktarimSablon> b)
    {
        b.ToTable("IceAktarimSablon");
        b.Property(x => x.Ad).HasMaxLength(60);
        b.Property(x => x.KolonEsleme).HasMaxLength(2000);
    }
}

internal sealed class BakimPlaniKonfig : IEntityTypeConfiguration<BakimPlani>
{
    public void Configure(EntityTypeBuilder<BakimPlani> b)
    {
        b.ToTable("BakimPlani", t => t.HasCheckConstraint("CK_BakimPlani_Hedef",
            "([AracId] IS NOT NULL AND [DorseId] IS NULL) OR ([AracId] IS NULL AND [DorseId] IS NOT NULL)"));
        b.Property(x => x.BakimAdi).HasMaxLength(100);
        b.Ignore(x => x.SonrakiKm);
        b.Ignore(x => x.SonrakiTarih);
    }
}

internal sealed class BakimKonfig : IEntityTypeConfiguration<Bakim>
{
    public void Configure(EntityTypeBuilder<Bakim> b)
    {
        b.ToTable("Bakim", t => t.HasCheckConstraint("CK_Bakim_Hedef",
            "([AracId] IS NOT NULL AND [DorseId] IS NULL) OR ([AracId] IS NULL AND [DorseId] IS NOT NULL)"));
        b.Property(x => x.ServisAdi).HasMaxLength(100);
        b.Property(x => x.Aciklama).HasMaxLength(500);
        b.Property(x => x.BelgeNo).HasMaxLength(30);
        b.HasOne(x => x.ServisCari).WithMany().HasForeignKey(x => x.ServisCariId);
        b.HasOne<BakimPlani>().WithMany().HasForeignKey(x => x.BakimPlaniId);
        b.HasOne<KasaHesap>().WithMany().HasForeignKey(x => x.KasaHesapId);
        b.HasIndex(x => new { x.AracId, x.Tarih });
    }
}

internal sealed class BakimKalemKonfig : IEntityTypeConfiguration<BakimKalem>
{
    public void Configure(EntityTypeBuilder<BakimKalem> b)
    {
        b.ToTable("BakimKalem");
        b.Property(x => x.Aciklama).HasMaxLength(150);
        b.Property(x => x.Miktar).HasPrecision(9, 2);
        b.Property(x => x.BirimFiyat).HasPrecision(18, 4);
        b.HasOne<Bakim>().WithMany(x => x.Kalemler).HasForeignKey(x => x.BakimId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LastikKonfig : IEntityTypeConfiguration<Lastik>
{
    public void Configure(EntityTypeBuilder<Lastik> b)
    {
        b.ToTable("Lastik");
        b.Property(x => x.SeriNo).HasMaxLength(30);
        b.Property(x => x.Marka).HasMaxLength(40);
        b.Property(x => x.Desen).HasMaxLength(40);
        b.Property(x => x.Ebat).HasMaxLength(20);
        b.Property(x => x.DotKodu).HasMaxLength(10);
        b.HasOne<Cari>().WithMany().HasForeignKey(x => x.TedarikciCariId);
        b.HasIndex(x => x.SeriNo).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class LastikHareketKonfig : IEntityTypeConfiguration<LastikHareket>
{
    public void Configure(EntityTypeBuilder<LastikHareket> b)
    {
        b.ToTable("LastikHareket", t => t.HasCheckConstraint("CK_LastikHareket_Km", "[SokmeKm] IS NULL OR [SokmeKm] >= [TakmaKm]"));
        b.Property(x => x.Pozisyon).HasMaxLength(15);
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.Property(x => x.DisDerinligiMm).HasPrecision(4, 1);
        b.HasOne(x => x.Lastik).WithMany(l => l.Hareketler).HasForeignKey(x => x.LastikId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.AracId, x.SokmeTarihi });
    }
}

internal sealed class AracFinansmanKonfig : IEntityTypeConfiguration<AracFinansman>
{
    public void Configure(EntityTypeBuilder<AracFinansman> b)
    {
        b.ToTable("AracFinansman");
        b.Property(x => x.SozlesmeNo).HasMaxLength(40);
        b.Property(x => x.Aciklama).HasMaxLength(300);
        b.Property(x => x.AylikFaizOrani).HasPrecision(7, 4);
        b.HasOne(x => x.FinansKurumu).WithMany().HasForeignKey(x => x.FinansKurumuCariId);
    }
}

internal sealed class FinansmanTaksitKonfig : IEntityTypeConfiguration<FinansmanTaksit>
{
    public void Configure(EntityTypeBuilder<FinansmanTaksit> b)
    {
        b.ToTable("FinansmanTaksit");
        b.HasOne(x => x.AracFinansman).WithMany(f => f.Taksitler).HasForeignKey(x => x.AracFinansmanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<KasaHesap>().WithMany().HasForeignKey(x => x.OdemeKasaHesapId);
        b.HasIndex(x => new { x.VadeTarihi, x.OdemeTarihi });
        b.HasIndex(x => new { x.AracFinansmanId, x.TaksitNo }).IsUnique();
    }
}

internal sealed class EvrakKonfig : IEntityTypeConfiguration<Evrak>
{
    public void Configure(EntityTypeBuilder<Evrak> b)
    {
        b.ToTable("Evrak", t => t.HasCheckConstraint("CK_Evrak_Hedef",
            "(CASE WHEN [AracId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [DorseId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SoforId] IS NULL THEN 0 ELSE 1 END) <= 1"));
        b.Property(x => x.BelgeNo).HasMaxLength(50);
        b.Property(x => x.Kurum).HasMaxLength(100);
        b.Property(x => x.Aciklama).HasMaxLength(300);
        b.HasIndex(x => x.BitisTarihi).HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.AracId).HasFilter("[AracId] IS NOT NULL");
        b.HasIndex(x => x.DorseId).HasFilter("[DorseId] IS NOT NULL");
        b.HasIndex(x => x.SoforId).HasFilter("[SoforId] IS NOT NULL");
    }
}

internal sealed class EkKonfig : IEntityTypeConfiguration<Ek>
{
    public void Configure(EntityTypeBuilder<Ek> b)
    {
        b.ToTable("Ek");
        b.Property(x => x.DosyaAdi).HasMaxLength(200);
        b.Property(x => x.IcerikTipi).HasMaxLength(100);
        b.Property(x => x.Aciklama).HasMaxLength(200);
        b.Property(x => x.Sha256).HasMaxLength(32).IsFixedLength();
        b.HasIndex(x => new { x.HedefTip, x.HedefId });
    }
}
