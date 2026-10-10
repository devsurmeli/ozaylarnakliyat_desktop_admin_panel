using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ozaylar.Filo.Infrastructure.Veri.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CariKodSeq");

            migrationBuilder.CreateSequence<int>(
                name: "IslemNoSeq");

            migrationBuilder.CreateSequence(
                name: "PortfoyNoSeq");

            migrationBuilder.CreateSequence(
                name: "SeferNoSeq");

            migrationBuilder.CreateTable(
                name: "Arac",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Plaka = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    AracTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    Marka = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Model = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    ModelYili = table.Column<short>(type: "smallint", nullable: true),
                    SaseNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    MotorNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Mulkiyet = table.Column<byte>(type: "tinyint", nullable: false),
                    AlisTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    AlisBedeli = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FaydaliOmurAy = table.Column<short>(type: "smallint", nullable: true),
                    HurdaDegeri = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    YakitDepoLitre = table.Column<short>(type: "smallint", nullable: true),
                    BeklenenTuketim = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    HgsEtiketNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    GuncelKm = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    SatisTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    Notlar = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arac", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tarih = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    KullaniciId = table.Column<short>(type: "smallint", nullable: true),
                    Tablo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    KayitId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Islem = table.Column<byte>(type: "tinyint", nullable: false),
                    Degisiklik = table.Column<string>(type: "varchar(max)", unicode: false, maxLength: -1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ayar",
                columns: table => new
                {
                    Anahtar = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Deger = table.Column<string>(type: "varchar(400)", unicode: false, maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ayar", x => x.Anahtar);
                });

            migrationBuilder.CreateTable(
                name: "Dorse",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Plaka = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    DorseTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    Marka = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    ModelYili = table.Column<short>(type: "smallint", nullable: true),
                    SaseNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    KapasiteTon = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    HacimM3 = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Mulkiyet = table.Column<byte>(type: "tinyint", nullable: false),
                    AlisTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    AlisBedeli = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FaydaliOmurAy = table.Column<short>(type: "smallint", nullable: true),
                    HurdaDegeri = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    Notlar = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dorse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DovizKur",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Alis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Satis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DovizKur", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ek",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HedefTip = table.Column<byte>(type: "tinyint", nullable: false),
                    HedefId = table.Column<int>(type: "int", nullable: false),
                    DosyaAdi = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    IcerikTipi = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Boyut = table.Column<int>(type: "int", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    EkIcerikId = table.Column<int>(type: "int", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ek", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvrakTuru",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    HedefTip = table.Column<byte>(type: "tinyint", nullable: false),
                    UyariGun = table.Column<short>(type: "smallint", nullable: false),
                    PeriyotAy = table.Column<short>(type: "smallint", nullable: true),
                    Zorunlu = table.Column<bool>(type: "bit", nullable: false),
                    SeferEngeller = table.Column<bool>(type: "bit", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvrakTuru", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IceAktarimSablon",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    Ad = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    BaslikSatiri = table.Column<byte>(type: "tinyint", nullable: false),
                    KolonEsleme = table.Column<string>(type: "varchar(2000)", unicode: false, maxLength: 2000, nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceAktarimSablon", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Il",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Ad = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Il", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Izin",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Ad = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Grup = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Izin", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KasaHesap",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    HesapTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    BankaAdi = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    Sube = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    IBAN = table.Column<string>(type: "varchar(34)", unicode: false, maxLength: 34, nullable: true),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    AcilisBakiye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AcilisTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    EksiBakiyeIzin = table.Column<bool>(type: "bit", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KasaHesap", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kullanici",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciAdi = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SifreHash = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    AdSoyad = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Eposta = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    SifreDegistirmeli = table.Column<bool>(type: "bit", nullable: false),
                    SonGiris = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    BasarisizGiris = table.Column<byte>(type: "tinyint", nullable: false),
                    KilitBitis = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanici", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasrafTuru",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Grup = table.Column<byte>(type: "tinyint", nullable: false),
                    VarsayilanKdv = table.Column<byte>(type: "tinyint", nullable: false),
                    SoforHarcamasi = table.Column<bool>(type: "bit", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasrafTuru", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rol",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Sistem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ulke",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Kod = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false),
                    Ad = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ulke", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AracDorseEslesme",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: false),
                    DorseId = table.Column<short>(type: "smallint", nullable: false),
                    Baslangic = table.Column<DateOnly>(type: "date", nullable: false),
                    Bitis = table.Column<DateOnly>(type: "date", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AracDorseEslesme", x => x.Id);
                    table.CheckConstraint("CK_AracDorseEslesme_Tarih", "[Bitis] IS NULL OR [Bitis] >= [Baslangic]");
                    table.ForeignKey(
                        name: "FK_AracDorseEslesme_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AracDorseEslesme_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BakimPlani",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    BakimAdi = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    PeriyotKm = table.Column<int>(type: "int", nullable: true),
                    PeriyotGun = table.Column<short>(type: "smallint", nullable: true),
                    SonKm = table.Column<int>(type: "int", nullable: true),
                    SonTarih = table.Column<DateOnly>(type: "date", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BakimPlani", x => x.Id);
                    table.CheckConstraint("CK_BakimPlani_Hedef", "([AracId] IS NOT NULL AND [DorseId] IS NULL) OR ([AracId] IS NULL AND [DorseId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BakimPlani_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BakimPlani_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KullaniciRol",
                columns: table => new
                {
                    KullaniciId = table.Column<short>(type: "smallint", nullable: false),
                    RolId = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KullaniciRol", x => new { x.KullaniciId, x.RolId });
                    table.ForeignKey(
                        name: "FK_KullaniciRol_Kullanici_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KullaniciRol_Rol_RolId",
                        column: x => x.RolId,
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolIzin",
                columns: table => new
                {
                    RolId = table.Column<byte>(type: "tinyint", nullable: false),
                    IzinId = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolIzin", x => new { x.RolId, x.IzinId });
                    table.ForeignKey(
                        name: "FK_RolIzin_Izin_IzinId",
                        column: x => x.IzinId,
                        principalTable: "Izin",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolIzin_Rol_RolId",
                        column: x => x.RolId,
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Unvan = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    CariTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    VergiDairesi = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    VergiNo = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: true),
                    Telefon = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Eposta = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Adres = table.Column<string>(type: "varchar(400)", unicode: false, maxLength: 400, nullable: true),
                    IlId = table.Column<byte>(type: "tinyint", nullable: true),
                    UlkeId = table.Column<short>(type: "smallint", nullable: true),
                    Yetkili = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    VadeGun = table.Column<short>(type: "smallint", nullable: false),
                    RiskLimiti = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    EFaturaMukellefi = table.Column<bool>(type: "bit", nullable: false),
                    IBAN = table.Column<string>(type: "varchar(34)", unicode: false, maxLength: 34, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    Notlar = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cari_Il_IlId",
                        column: x => x.IlId,
                        principalTable: "Il",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cari_Ulke_UlkeId",
                        column: x => x.UlkeId,
                        principalTable: "Ulke",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HarcirahTarife",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UlkeId = table.Column<short>(type: "smallint", nullable: true),
                    GunlukTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    GecerlilikBaslangic = table.Column<DateOnly>(type: "date", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarcirahTarife", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarcirahTarife_Ulke_UlkeId",
                        column: x => x.UlkeId,
                        principalTable: "Ulke",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AracFinansman",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    Tip = table.Column<byte>(type: "tinyint", nullable: false),
                    FinansKurumuCariId = table.Column<int>(type: "int", nullable: false),
                    SozlesmeNo = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    BaslangicTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    AnaparaTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    AylikFaizOrani = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    VadeSayisi = table.Column<short>(type: "smallint", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AracFinansman", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AracFinansman_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AracFinansman_Cari_FinansKurumuCariId",
                        column: x => x.FinansKurumuCariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AracFinansman_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bakim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    BakimTuru = table.Column<byte>(type: "tinyint", nullable: false),
                    BakimPlaniId = table.Column<short>(type: "smallint", nullable: true),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    Km = table.Column<int>(type: "int", nullable: true),
                    ServisCariId = table.Column<int>(type: "int", nullable: true),
                    ServisAdi = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvOrani = table.Column<byte>(type: "tinyint", nullable: false),
                    OdemeSekli = table.Column<byte>(type: "tinyint", nullable: false),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    BelgeNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    GarantiBitis = table.Column<DateOnly>(type: "date", nullable: true),
                    Iptal = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bakim", x => x.Id);
                    table.CheckConstraint("CK_Bakim_Hedef", "([AracId] IS NOT NULL AND [DorseId] IS NULL) OR ([AracId] IS NULL AND [DorseId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Bakim_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bakim_BakimPlani_BakimPlaniId",
                        column: x => x.BakimPlaniId,
                        principalTable: "BakimPlani",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bakim_Cari_ServisCariId",
                        column: x => x.ServisCariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bakim_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bakim_KasaHesap_KasaHesapId",
                        column: x => x.KasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CariHareket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CariId = table.Column<int>(type: "int", nullable: false),
                    VadeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    Borc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Alacak = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    DovizBorc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DovizAlacak = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    KaynakTip = table.Column<byte>(type: "tinyint", nullable: false),
                    KaynakId = table.Column<int>(type: "int", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    TersKayitId = table.Column<int>(type: "int", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CariHareket", x => x.Id);
                    table.CheckConstraint("CK_CariHareket_Tutar", "[Borc] >= 0 AND [Alacak] >= 0");
                    table.ForeignKey(
                        name: "FK_CariHareket_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CekSenet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tip = table.Column<byte>(type: "tinyint", nullable: false),
                    Yon = table.Column<byte>(type: "tinyint", nullable: false),
                    PortfoyNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SeriNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Banka = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    Sube = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    Kesideci = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    CariId = table.Column<int>(type: "int", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    IslemTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    VadeTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CekSenet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CekSenet_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fatura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FaturaTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    FaturaNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    VadeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    CariId = table.Column<int>(type: "int", nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AraToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TevkifatToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GenelToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EBelgeNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fatura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fatura_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceAktarim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    SablonId = table.Column<short>(type: "smallint", nullable: true),
                    DosyaAdi = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    CariId = table.Column<int>(type: "int", nullable: true),
                    ToplamSatir = table.Column<int>(type: "int", nullable: false),
                    BasariliSatir = table.Column<int>(type: "int", nullable: false),
                    HataliSatir = table.Column<int>(type: "int", nullable: false),
                    Hatalar = table.Column<string>(type: "varchar(max)", unicode: false, maxLength: -1, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceAktarim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IceAktarim_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IceAktarim_IceAktarimSablon_SablonId",
                        column: x => x.SablonId,
                        principalTable: "IceAktarimSablon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KasaHareket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: false),
                    Giris = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Cikis = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CariId = table.Column<int>(type: "int", nullable: true),
                    VirmanKarsiHareketId = table.Column<int>(type: "int", nullable: true),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    KaynakTip = table.Column<byte>(type: "tinyint", nullable: false),
                    KaynakId = table.Column<int>(type: "int", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    TersKayitId = table.Column<int>(type: "int", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KasaHareket", x => x.Id);
                    table.CheckConstraint("CK_KasaHareket_Tutar", "[Giris] >= 0 AND [Cikis] >= 0");
                    table.ForeignKey(
                        name: "FK_KasaHareket_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KasaHareket_KasaHesap_KasaHesapId",
                        column: x => x.KasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lastik",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeriNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Marka = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Desen = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Ebat = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    DotKodu = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    AlisTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    AlisBedeli = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TedarikciCariId = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    KaplamaSayisi = table.Column<byte>(type: "tinyint", nullable: false),
                    HurdaTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lastik", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lastik_Cari_TedarikciCariId",
                        column: x => x.TedarikciCariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Lokasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    UlkeId = table.Column<short>(type: "smallint", nullable: false),
                    IlId = table.Column<byte>(type: "tinyint", nullable: true),
                    Ilce = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Adres = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    CariId = table.Column<int>(type: "int", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lokasyon", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lokasyon_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Lokasyon_Il_IlId",
                        column: x => x.IlId,
                        principalTable: "Il",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Lokasyon_Ulke_UlkeId",
                        column: x => x.UlkeId,
                        principalTable: "Ulke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sofor",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TCKN = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: false),
                    Ad = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Soyad = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Telefon = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Eposta = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DogumTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    EhliyetSinifi = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    EhliyetNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    IBAN = table.Column<string>(type: "varchar(34)", unicode: false, maxLength: 34, nullable: true),
                    Adres = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    IseGiris = table.Column<DateOnly>(type: "date", nullable: true),
                    IstenCikis = table.Column<DateOnly>(type: "date", nullable: true),
                    CariId = table.Column<int>(type: "int", nullable: false),
                    VarsayilanAracId = table.Column<short>(type: "smallint", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    Notlar = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sofor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sofor_Arac_VarsayilanAracId",
                        column: x => x.VarsayilanAracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sofor_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinansmanTaksit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracFinansmanId = table.Column<short>(type: "smallint", nullable: false),
                    TaksitNo = table.Column<short>(type: "smallint", nullable: false),
                    VadeTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    Anapara = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Faiz = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Toplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OdemeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    OdemeKasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinansmanTaksit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinansmanTaksit_AracFinansman_AracFinansmanId",
                        column: x => x.AracFinansmanId,
                        principalTable: "AracFinansman",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinansmanTaksit_KasaHesap_OdemeKasaHesapId",
                        column: x => x.OdemeKasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BakimKalem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BakimId = table.Column<int>(type: "int", nullable: false),
                    KalemTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BakimKalem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BakimKalem_Bakim_BakimId",
                        column: x => x.BakimId,
                        principalTable: "Bakim",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CekSenetHareket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CekSenetId = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    YeniDurum = table.Column<byte>(type: "tinyint", nullable: false),
                    CariId = table.Column<int>(type: "int", nullable: true),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CekSenetHareket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CekSenetHareket_Cari_CariId",
                        column: x => x.CariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CekSenetHareket_CekSenet_CekSenetId",
                        column: x => x.CekSenetId,
                        principalTable: "CekSenet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CekSenetHareket_KasaHesap_KasaHesapId",
                        column: x => x.KasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LastikHareket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastikId = table.Column<int>(type: "int", nullable: false),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    Pozisyon = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    TakmaTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    TakmaKm = table.Column<int>(type: "int", nullable: false),
                    SokmeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    SokmeKm = table.Column<int>(type: "int", nullable: true),
                    DisDerinligiMm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LastikHareket", x => x.Id);
                    table.CheckConstraint("CK_LastikHareket_Km", "[SokmeKm] IS NULL OR [SokmeKm] >= [TakmaKm]");
                    table.ForeignKey(
                        name: "FK_LastikHareket_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LastikHareket_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LastikHareket_Lastik_LastikId",
                        column: x => x.LastikId,
                        principalTable: "Lastik",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Guzergah",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    CikisLokasyonId = table.Column<int>(type: "int", nullable: false),
                    VarisLokasyonId = table.Column<int>(type: "int", nullable: false),
                    StandartKm = table.Column<int>(type: "int", nullable: false),
                    StandartGun = table.Column<byte>(type: "tinyint", nullable: true),
                    StandartNavlun = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NavlunParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    StandartYolMasrafi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guzergah", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guzergah_Lokasyon_CikisLokasyonId",
                        column: x => x.CikisLokasyonId,
                        principalTable: "Lokasyon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Guzergah_Lokasyon_VarisLokasyonId",
                        column: x => x.VarisLokasyonId,
                        principalTable: "Lokasyon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Evrak",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvrakTuruId = table.Column<short>(type: "smallint", nullable: false),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    SoforId = table.Column<short>(type: "smallint", nullable: true),
                    BelgeNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Kurum = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DuzenlenmeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    BitisTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evrak", x => x.Id);
                    table.CheckConstraint("CK_Evrak_Hedef", "(CASE WHEN [AracId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [DorseId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SoforId] IS NULL THEN 0 ELSE 1 END) <= 1");
                    table.ForeignKey(
                        name: "FK_Evrak_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Evrak_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Evrak_EvrakTuru_EvrakTuruId",
                        column: x => x.EvrakTuruId,
                        principalTable: "EvrakTuru",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evrak_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sefer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeferNo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Kapsam = table.Column<byte>(type: "tinyint", nullable: false),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    MusteriCariId = table.Column<int>(type: "int", nullable: false),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    TaseronCariId = table.Column<int>(type: "int", nullable: true),
                    TaseronPlaka = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    TaseronBedel = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TaseronParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    TaseronKur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    GuzergahId = table.Column<short>(type: "smallint", nullable: true),
                    CikisLokasyonId = table.Column<int>(type: "int", nullable: false),
                    VarisLokasyonId = table.Column<int>(type: "int", nullable: false),
                    PlanlananTarih = table.Column<DateOnly>(type: "date", nullable: false),
                    CikisTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    TeslimTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DonusTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CikisKm = table.Column<int>(type: "int", nullable: true),
                    DonusKm = table.Column<int>(type: "int", nullable: true),
                    BosKm = table.Column<int>(type: "int", nullable: true),
                    YukTanimi = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Tonaj = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    NavlunTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NavlunParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    NavlunKur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    NavlunKdvOrani = table.Column<byte>(type: "tinyint", nullable: false),
                    NavlunTevkifatPay = table.Column<byte>(type: "tinyint", nullable: false),
                    CmrNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    IrsaliyeNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    BagliSeferId = table.Column<int>(type: "int", nullable: true),
                    Notlar = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sefer", x => x.Id);
                    table.CheckConstraint("CK_Sefer_AracVeyaTaseron", "[AracId] IS NOT NULL OR [TaseronCariId] IS NOT NULL");
                    table.CheckConstraint("CK_Sefer_Km", "[DonusKm] IS NULL OR [CikisKm] IS NULL OR [DonusKm] >= [CikisKm]");
                    table.ForeignKey(
                        name: "FK_Sefer_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sefer_Cari_MusteriCariId",
                        column: x => x.MusteriCariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sefer_Cari_TaseronCariId",
                        column: x => x.TaseronCariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sefer_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sefer_Guzergah_GuzergahId",
                        column: x => x.GuzergahId,
                        principalTable: "Guzergah",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sefer_Lokasyon_CikisLokasyonId",
                        column: x => x.CikisLokasyonId,
                        principalTable: "Lokasyon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sefer_Lokasyon_VarisLokasyonId",
                        column: x => x.VarisLokasyonId,
                        principalTable: "Lokasyon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sefer_Sefer_BagliSeferId",
                        column: x => x.BagliSeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ceza",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    SoforId = table.Column<short>(type: "smallint", nullable: true),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    CezaTuru = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    TutanakNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IndirimliTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TebligTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    OdemeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    OdemeKasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    RucuEdilsin = table.Column<bool>(type: "bit", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ceza", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ceza_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ceza_KasaHesap_OdemeKasaHesapId",
                        column: x => x.OdemeKasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ceza_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ceza_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FaturaKalem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FaturaId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<byte>(type: "tinyint", nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Birim = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IskontoOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    KdvOrani = table.Column<byte>(type: "tinyint", nullable: false),
                    TevkifatPay = table.Column<byte>(type: "tinyint", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TevkifatTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaturaKalem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaturaKalem_Fatura_FaturaId",
                        column: x => x.FaturaId,
                        principalTable: "Fatura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaturaKalem_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HgsGecis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: false),
                    GecisZamani = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GirisYeri = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    CikisYeri = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    IceAktarimId = table.Column<int>(type: "int", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HgsGecis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HgsGecis_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HgsGecis_IceAktarim_IceAktarimId",
                        column: x => x.IceAktarimId,
                        principalTable: "IceAktarim",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HgsGecis_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Mahsup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoforId = table.Column<short>(type: "smallint", nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    AvansToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HarcamaToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HarcirahToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fark = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sonuc = table.Column<byte>(type: "tinyint", nullable: false),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mahsup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mahsup_KasaHesap_KasaHesapId",
                        column: x => x.KasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Mahsup_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Mahsup_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Masraf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    MasrafTuruId = table.Column<short>(type: "smallint", nullable: false),
                    Merkez = table.Column<byte>(type: "tinyint", nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    SoforId = table.Column<short>(type: "smallint", nullable: true),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvOrani = table.Column<byte>(type: "tinyint", nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    OdemeSekli = table.Column<byte>(type: "tinyint", nullable: false),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    CariId = table.Column<int>(type: "int", nullable: true),
                    BelgeNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Onayli = table.Column<bool>(type: "bit", nullable: false),
                    MahsupId = table.Column<int>(type: "int", nullable: true),
                    Iptal = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Masraf", x => x.Id);
                    table.CheckConstraint("CK_Masraf_Merkez", "([Merkez] = 1 AND [SeferId] IS NOT NULL) OR ([Merkez] = 2 AND [AracId] IS NOT NULL) OR ([Merkez] = 3 AND [DorseId] IS NOT NULL) OR [Merkez] = 4");
                    table.CheckConstraint("CK_Masraf_SoforNakit", "[OdemeSekli] <> 1 OR [SoforId] IS NOT NULL");
                    table.CheckConstraint("CK_Masraf_Tutar", "[Tutar] > 0");
                    table.ForeignKey(
                        name: "FK_Masraf_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Masraf_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Masraf_MasrafTuru_MasrafTuruId",
                        column: x => x.MasrafTuruId,
                        principalTable: "MasrafTuru",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Masraf_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Masraf_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SeferDurak",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeferId = table.Column<int>(type: "int", nullable: false),
                    Sira = table.Column<byte>(type: "tinyint", nullable: false),
                    LokasyonId = table.Column<int>(type: "int", nullable: false),
                    DurakTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    PlanlananTarih = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    GerceklesenTarih = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeferDurak", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeferDurak_Lokasyon_LokasyonId",
                        column: x => x.LokasyonId,
                        principalTable: "Lokasyon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeferDurak_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeferSofor",
                columns: table => new
                {
                    SeferId = table.Column<int>(type: "int", nullable: false),
                    SoforId = table.Column<short>(type: "smallint", nullable: false),
                    Birincil = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeferSofor", x => new { x.SeferId, x.SoforId });
                    table.ForeignKey(
                        name: "FK_SeferSofor_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeferSofor_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Yakit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    SoforId = table.Column<short>(type: "smallint", nullable: true),
                    AlimZamani = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Km = table.Column<int>(type: "int", nullable: false),
                    Litre = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvOrani = table.Column<byte>(type: "tinyint", nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    FullDepo = table.Column<bool>(type: "bit", nullable: false),
                    IstasyonCariId = table.Column<int>(type: "int", nullable: true),
                    IstasyonAdi = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    OdemeSekli = table.Column<byte>(type: "tinyint", nullable: false),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    KartNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    FisNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    IceAktarimId = table.Column<int>(type: "int", nullable: true),
                    MahsupId = table.Column<int>(type: "int", nullable: true),
                    Iptal = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Yakit", x => x.Id);
                    table.CheckConstraint("CK_Yakit_Litre", "[Litre] > 0");
                    table.CheckConstraint("CK_Yakit_SoforNakit", "[OdemeSekli] <> 1 OR [SoforId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Yakit_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Yakit_Cari_IstasyonCariId",
                        column: x => x.IstasyonCariId,
                        principalTable: "Cari",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Yakit_IceAktarim_IceAktarimId",
                        column: x => x.IceAktarimId,
                        principalTable: "IceAktarim",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Yakit_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Yakit_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Avans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoforId = table.Column<short>(type: "smallint", nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    MahsupId = table.Column<int>(type: "int", nullable: true),
                    Iptal = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avans", x => x.Id);
                    table.CheckConstraint("CK_Avans_Tutar", "[Tutar] > 0");
                    table.ForeignKey(
                        name: "FK_Avans_KasaHesap_KasaHesapId",
                        column: x => x.KasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Avans_Mahsup_MahsupId",
                        column: x => x.MahsupId,
                        principalTable: "Mahsup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Avans_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Avans_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Harcirah",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoforId = table.Column<short>(type: "smallint", nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    UlkeId = table.Column<short>(type: "smallint", nullable: true),
                    Baslangic = table.Column<DateOnly>(type: "date", nullable: false),
                    Bitis = table.Column<DateOnly>(type: "date", nullable: false),
                    GunSayisi = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
                    GunlukTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    MahsupId = table.Column<int>(type: "int", nullable: true),
                    Iptal = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Harcirah", x => x.Id);
                    table.CheckConstraint("CK_Harcirah_Tarih", "[Bitis] >= [Baslangic]");
                    table.ForeignKey(
                        name: "FK_Harcirah_Mahsup_MahsupId",
                        column: x => x.MahsupId,
                        principalTable: "Mahsup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Harcirah_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Harcirah_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Harcirah_Ulke_UlkeId",
                        column: x => x.UlkeId,
                        principalTable: "Ulke",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Ayar",
                columns: new[] { "Anahtar", "Deger" },
                values: new object[,]
                {
                    { "Donem.KapanisTarihi", "" },
                    { "Fatura.TasimaTevkifatPay", "2" },
                    { "Fatura.VarsayilanKdv", "20" },
                    { "Firma.Unvan", "Ozaylar Nakliyat" },
                    { "Firma.VergiNo", "" },
                    { "Masraf.FissizLimit", "1000" },
                    { "Sefer.KmSapmaYuzde", "15" },
                    { "Yakit.SapmaUyariYuzde", "10" },
                    { "Yedek.SonBasarili", "" }
                });

            migrationBuilder.InsertData(
                table: "EvrakTuru",
                columns: new[] { "Id", "Ad", "Aktif", "HedefTip", "PeriyotAy", "SeferEngeller", "UyariGun", "Zorunlu" },
                values: new object[,]
                {
                    { (short)1, "Araç Muayenesi", true, (byte)3, (short)12, true, (short)30, true },
                    { (short)2, "Egzoz Emisyon", true, (byte)1, (short)12, false, (short)30, true },
                    { (short)3, "Trafik Sigortası", true, (byte)3, (short)12, true, (short)30, true },
                    { (short)4, "Kasko", true, (byte)3, (short)12, false, (short)30, false },
                    { (short)5, "Taşımacı Mali Sorumluluk Sig.", true, (byte)9, (short)12, false, (short)30, false },
                    { (short)6, "Takograf Kalibrasyonu", true, (byte)1, (short)24, false, (short)30, true },
                    { (short)7, "Ruhsat", true, (byte)3, null, false, (short)0, false },
                    { (short)8, "ATP Belgesi (Frigo)", true, (byte)2, (short)72, false, (short)60, false },
                    { (short)9, "K Yetki Belgesi", true, (byte)8, null, false, (short)60, true },
                    { (short)10, "Taşıt Kartı", true, (byte)1, null, false, (short)30, false },
                    { (short)11, "Ehliyet", true, (byte)4, null, true, (short)60, true },
                    { (short)12, "SRC Belgesi", true, (byte)4, (short)60, true, (short)60, true },
                    { (short)13, "Psikoteknik", true, (byte)4, (short)60, true, (short)60, true },
                    { (short)14, "Sağlık Raporu", true, (byte)4, (short)12, false, (short)30, false },
                    { (short)15, "Takograf Şoför Kartı", true, (byte)4, (short)60, false, (short)60, true },
                    { (short)16, "Pasaport", true, (byte)4, null, false, (short)90, false },
                    { (short)17, "Vize", true, (byte)4, null, false, (short)30, false },
                    { (short)18, "Uluslararası Yeşil Kart Sig.", true, (byte)3, (short)12, false, (short)30, false },
                    { (short)19, "TIR Karnesi", true, (byte)1, null, false, (short)15, false }
                });

            migrationBuilder.InsertData(
                table: "IceAktarimSablon",
                columns: new[] { "Id", "Ad", "Aktif", "BaslikSatiri", "KolonEsleme", "Tur" },
                values: new object[,]
                {
                    { (short)1, "Genel Akaryakıt Ekstresi", true, (byte)1, "{\"Plaka\":\"Plaka\",\"Tarih\":\"\\u0130\\u015Flem Tarihi\",\"Litre\":\"Miktar\",\"BirimFiyat\":\"Birim Fiyat\",\"Tutar\":\"Tutar\",\"Km\":\"Km\",\"Istasyon\":\"\\u0130stasyon\",\"KartNo\":\"Kart No\",\"FisNo\":\"Fi\\u015F No\"}", (byte)1 },
                    { (short)2, "Genel HGS Ekstresi", true, (byte)1, "{\"Plaka\":\"Plaka\",\"EtiketNo\":\"Etiket No\",\"Tarih\":\"Ge\\u00E7i\\u015F Tarihi\",\"Giris\":\"Giri\\u015F\",\"Cikis\":\"\\u00C7\\u0131k\\u0131\\u015F\",\"Tutar\":\"Tutar\"}", (byte)2 }
                });

            migrationBuilder.InsertData(
                table: "Il",
                columns: new[] { "Id", "Ad" },
                values: new object[,]
                {
                    { (byte)1, "Adana" },
                    { (byte)2, "Adıyaman" },
                    { (byte)3, "Afyonkarahisar" },
                    { (byte)4, "Ağrı" },
                    { (byte)5, "Amasya" },
                    { (byte)6, "Ankara" },
                    { (byte)7, "Antalya" },
                    { (byte)8, "Artvin" },
                    { (byte)9, "Aydın" },
                    { (byte)10, "Balıkesir" },
                    { (byte)11, "Bilecik" },
                    { (byte)12, "Bingöl" },
                    { (byte)13, "Bitlis" },
                    { (byte)14, "Bolu" },
                    { (byte)15, "Burdur" },
                    { (byte)16, "Bursa" },
                    { (byte)17, "Çanakkale" },
                    { (byte)18, "Çankırı" },
                    { (byte)19, "Çorum" },
                    { (byte)20, "Denizli" },
                    { (byte)21, "Diyarbakır" },
                    { (byte)22, "Edirne" },
                    { (byte)23, "Elazığ" },
                    { (byte)24, "Erzincan" },
                    { (byte)25, "Erzurum" },
                    { (byte)26, "Eskişehir" },
                    { (byte)27, "Gaziantep" },
                    { (byte)28, "Giresun" },
                    { (byte)29, "Gümüşhane" },
                    { (byte)30, "Hakkari" },
                    { (byte)31, "Hatay" },
                    { (byte)32, "Isparta" },
                    { (byte)33, "Mersin" },
                    { (byte)34, "İstanbul" },
                    { (byte)35, "İzmir" },
                    { (byte)36, "Kars" },
                    { (byte)37, "Kastamonu" },
                    { (byte)38, "Kayseri" },
                    { (byte)39, "Kırklareli" },
                    { (byte)40, "Kırşehir" },
                    { (byte)41, "Kocaeli" },
                    { (byte)42, "Konya" },
                    { (byte)43, "Kütahya" },
                    { (byte)44, "Malatya" },
                    { (byte)45, "Manisa" },
                    { (byte)46, "Kahramanmaraş" },
                    { (byte)47, "Mardin" },
                    { (byte)48, "Muğla" },
                    { (byte)49, "Muş" },
                    { (byte)50, "Nevşehir" },
                    { (byte)51, "Niğde" },
                    { (byte)52, "Ordu" },
                    { (byte)53, "Rize" },
                    { (byte)54, "Sakarya" },
                    { (byte)55, "Samsun" },
                    { (byte)56, "Siirt" },
                    { (byte)57, "Sinop" },
                    { (byte)58, "Sivas" },
                    { (byte)59, "Tekirdağ" },
                    { (byte)60, "Tokat" },
                    { (byte)61, "Trabzon" },
                    { (byte)62, "Tunceli" },
                    { (byte)63, "Şanlıurfa" },
                    { (byte)64, "Uşak" },
                    { (byte)65, "Van" },
                    { (byte)66, "Yozgat" },
                    { (byte)67, "Zonguldak" },
                    { (byte)68, "Aksaray" },
                    { (byte)69, "Bayburt" },
                    { (byte)70, "Karaman" },
                    { (byte)71, "Kırıkkale" },
                    { (byte)72, "Batman" },
                    { (byte)73, "Şırnak" },
                    { (byte)74, "Bartın" },
                    { (byte)75, "Ardahan" },
                    { (byte)76, "Iğdır" },
                    { (byte)77, "Yalova" },
                    { (byte)78, "Karabük" },
                    { (byte)79, "Kilis" },
                    { (byte)80, "Osmaniye" },
                    { (byte)81, "Düzce" }
                });

            migrationBuilder.InsertData(
                table: "MasrafTuru",
                columns: new[] { "Id", "Ad", "Aktif", "Grup", "SoforHarcamasi", "VarsayilanKdv" },
                values: new object[,]
                {
                    { (short)1, "Motorin (Fiş)", true, (byte)1, true, (byte)20 },
                    { (short)2, "AdBlue", true, (byte)1, true, (byte)20 },
                    { (short)3, "Otoyol / Köprü", true, (byte)2, true, (byte)0 },
                    { (short)4, "Feribot / Ro-Ro", true, (byte)12, true, (byte)0 },
                    { (short)5, "Yemek", true, (byte)4, true, (byte)0 },
                    { (short)6, "Konaklama", true, (byte)3, true, (byte)0 },
                    { (short)7, "Otopark", true, (byte)14, true, (byte)0 },
                    { (short)8, "Kantar", true, (byte)14, true, (byte)0 },
                    { (short)9, "Yükleme / Boşaltma Ücreti", true, (byte)13, true, (byte)0 },
                    { (short)10, "Gümrük / Sınır Masrafı", true, (byte)11, true, (byte)0 },
                    { (short)11, "Yol Tamiri / Lastikçi", true, (byte)5, true, (byte)20 },
                    { (short)12, "Yıkama", true, (byte)99, true, (byte)20 },
                    { (short)13, "Trafik Sigortası", true, (byte)7, false, (byte)0 },
                    { (short)14, "Kasko", true, (byte)7, false, (byte)0 },
                    { (short)15, "Taşımacı Mali Sorumluluk Sig.", true, (byte)7, false, (byte)0 },
                    { (short)16, "MTV", true, (byte)8, false, (byte)0 },
                    { (short)17, "Muayene Ücreti", true, (byte)8, false, (byte)20 },
                    { (short)18, "Takograf Kalibrasyon", true, (byte)5, false, (byte)20 },
                    { (short)19, "Belge / Harç", true, (byte)8, false, (byte)0 },
                    { (short)20, "Şoför Maaşı", true, (byte)10, false, (byte)0 },
                    { (short)21, "SGK Primi", true, (byte)10, false, (byte)0 },
                    { (short)22, "Sefer Primi", true, (byte)10, false, (byte)0 },
                    { (short)23, "Ofis Kira", true, (byte)15, false, (byte)20 },
                    { (short)24, "Muhasebe Ücreti", true, (byte)15, false, (byte)20 },
                    { (short)25, "Araç Takip (GPS)", true, (byte)15, false, (byte)20 },
                    { (short)26, "Diğer", true, (byte)99, true, (byte)20 }
                });

            migrationBuilder.InsertData(
                table: "Rol",
                columns: new[] { "Id", "Aciklama", "Ad", "Sistem" },
                values: new object[,]
                {
                    { (byte)1, "Tüm yetkiler", "Yönetici", true },
                    { (byte)2, "Fatura, cari, kasa, mahsup, raporlar", "Muhasebe", false },
                    { (byte)3, "Sefer, araç-şoför ataması, evrak (fiyat göremez)", "Operasyon", false },
                    { (byte)4, "Avans, masraf ve fiş girişi", "Kasiyer", false }
                });

            migrationBuilder.InsertData(
                table: "Ulke",
                columns: new[] { "Id", "Ad", "Kod" },
                values: new object[,]
                {
                    { (short)1, "Türkiye", "TR" },
                    { (short)2, "Almanya", "DE" },
                    { (short)3, "Avusturya", "AT" },
                    { (short)4, "Belçika", "BE" },
                    { (short)5, "Bulgaristan", "BG" },
                    { (short)6, "Çekya", "CZ" },
                    { (short)7, "Danimarka", "DK" },
                    { (short)8, "Fransa", "FR" },
                    { (short)9, "Hollanda", "NL" },
                    { (short)10, "Birleşik Krallık", "GB" },
                    { (short)11, "İspanya", "ES" },
                    { (short)12, "İtalya", "IT" },
                    { (short)13, "İsveç", "SE" },
                    { (short)14, "İsviçre", "CH" },
                    { (short)15, "Macaristan", "HU" },
                    { (short)16, "Polonya", "PL" },
                    { (short)17, "Portekiz", "PT" },
                    { (short)18, "Romanya", "RO" },
                    { (short)19, "Slovakya", "SK" },
                    { (short)20, "Slovenya", "SI" },
                    { (short)21, "Yunanistan", "GR" },
                    { (short)22, "Hırvatistan", "HR" },
                    { (short)23, "Sırbistan", "RS" },
                    { (short)24, "Kuzey Makedonya", "MK" },
                    { (short)25, "Bosna-Hersek", "BA" },
                    { (short)26, "Arnavutluk", "AL" },
                    { (short)27, "Litvanya", "LT" },
                    { (short)28, "Letonya", "LV" },
                    { (short)29, "Estonya", "EE" },
                    { (short)30, "Lüksemburg", "LU" },
                    { (short)31, "Gürcistan", "GE" },
                    { (short)32, "Azerbaycan", "AZ" },
                    { (short)33, "Ermenistan", "AM" },
                    { (short)34, "İran", "IR" },
                    { (short)35, "Irak", "IQ" },
                    { (short)36, "Suriye", "SY" },
                    { (short)37, "Rusya", "RU" },
                    { (short)38, "Ukrayna", "UA" },
                    { (short)39, "Moldova", "MD" },
                    { (short)40, "Kazakistan", "KZ" },
                    { (short)41, "Özbekistan", "UZ" },
                    { (short)42, "Türkmenistan", "TM" },
                    { (short)43, "Kırgızistan", "KG" },
                    { (short)44, "Tacikistan", "TJ" },
                    { (short)45, "Suudi Arabistan", "SA" },
                    { (short)46, "Birleşik Arap Emirlikleri", "AE" },
                    { (short)47, "Ürdün", "JO" },
                    { (short)48, "Lübnan", "LB" },
                    { (short)49, "İsrail", "IL" },
                    { (short)50, "Mısır", "EG" },
                    { (short)51, "Norveç", "NO" },
                    { (short)52, "Finlandiya", "FI" },
                    { (short)53, "Belarus", "BY" },
                    { (short)54, "Kuveyt", "KW" },
                    { (short)55, "Katar", "QA" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Arac_HgsEtiketNo",
                table: "Arac",
                column: "HgsEtiketNo");

            migrationBuilder.CreateIndex(
                name: "IX_Arac_Plaka",
                table: "Arac",
                column: "Plaka",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AracDorseEslesme_AracId_Baslangic",
                table: "AracDorseEslesme",
                columns: new[] { "AracId", "Baslangic" });

            migrationBuilder.CreateIndex(
                name: "IX_AracDorseEslesme_DorseId_Baslangic",
                table: "AracDorseEslesme",
                columns: new[] { "DorseId", "Baslangic" });

            migrationBuilder.CreateIndex(
                name: "IX_AracFinansman_AracId",
                table: "AracFinansman",
                column: "AracId");

            migrationBuilder.CreateIndex(
                name: "IX_AracFinansman_DorseId",
                table: "AracFinansman",
                column: "DorseId");

            migrationBuilder.CreateIndex(
                name: "IX_AracFinansman_FinansKurumuCariId",
                table: "AracFinansman",
                column: "FinansKurumuCariId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Tablo_KayitId",
                table: "AuditLog",
                columns: new[] { "Tablo", "KayitId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Tarih",
                table: "AuditLog",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_Avans_KasaHesapId",
                table: "Avans",
                column: "KasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_Avans_MahsupId",
                table: "Avans",
                column: "MahsupId");

            migrationBuilder.CreateIndex(
                name: "IX_Avans_SeferId",
                table: "Avans",
                column: "SeferId");

            migrationBuilder.CreateIndex(
                name: "IX_Avans_SoforId_MahsupId",
                table: "Avans",
                columns: new[] { "SoforId", "MahsupId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bakim_AracId_Tarih",
                table: "Bakim",
                columns: new[] { "AracId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Bakim_BakimPlaniId",
                table: "Bakim",
                column: "BakimPlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_Bakim_DorseId",
                table: "Bakim",
                column: "DorseId");

            migrationBuilder.CreateIndex(
                name: "IX_Bakim_KasaHesapId",
                table: "Bakim",
                column: "KasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_Bakim_ServisCariId",
                table: "Bakim",
                column: "ServisCariId");

            migrationBuilder.CreateIndex(
                name: "IX_BakimKalem_BakimId",
                table: "BakimKalem",
                column: "BakimId");

            migrationBuilder.CreateIndex(
                name: "IX_BakimPlani_AracId",
                table: "BakimPlani",
                column: "AracId");

            migrationBuilder.CreateIndex(
                name: "IX_BakimPlani_DorseId",
                table: "BakimPlani",
                column: "DorseId");

            migrationBuilder.CreateIndex(
                name: "IX_Cari_IlId",
                table: "Cari",
                column: "IlId");

            migrationBuilder.CreateIndex(
                name: "IX_Cari_Kod",
                table: "Cari",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cari_UlkeId",
                table: "Cari",
                column: "UlkeId");

            migrationBuilder.CreateIndex(
                name: "IX_Cari_Unvan",
                table: "Cari",
                column: "Unvan");

            migrationBuilder.CreateIndex(
                name: "IX_Cari_VergiNo",
                table: "Cari",
                column: "VergiNo",
                unique: true,
                filter: "[VergiNo] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareket_CariId_Tarih",
                table: "CariHareket",
                columns: new[] { "CariId", "Tarih" })
                .Annotation("SqlServer:Include", new[] { "Borc", "Alacak" });

            migrationBuilder.CreateIndex(
                name: "IX_CariHareket_KaynakTip_KaynakId",
                table: "CariHareket",
                columns: new[] { "KaynakTip", "KaynakId" });

            migrationBuilder.CreateIndex(
                name: "IX_CariHareket_TersKayitId",
                table: "CariHareket",
                column: "TersKayitId",
                filter: "[TersKayitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CekSenet_CariId",
                table: "CekSenet",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_CekSenet_Durum_VadeTarihi",
                table: "CekSenet",
                columns: new[] { "Durum", "VadeTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_CekSenet_PortfoyNo",
                table: "CekSenet",
                column: "PortfoyNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CekSenetHareket_CariId",
                table: "CekSenetHareket",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_CekSenetHareket_CekSenetId",
                table: "CekSenetHareket",
                column: "CekSenetId");

            migrationBuilder.CreateIndex(
                name: "IX_CekSenetHareket_KasaHesapId",
                table: "CekSenetHareket",
                column: "KasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_Ceza_AracId_Tarih",
                table: "Ceza",
                columns: new[] { "AracId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Ceza_OdemeKasaHesapId",
                table: "Ceza",
                column: "OdemeKasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_Ceza_SeferId",
                table: "Ceza",
                column: "SeferId");

            migrationBuilder.CreateIndex(
                name: "IX_Ceza_SoforId",
                table: "Ceza",
                column: "SoforId");

            migrationBuilder.CreateIndex(
                name: "IX_Dorse_Plaka",
                table: "Dorse",
                column: "Plaka",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DovizKur_Tarih_ParaBirimi",
                table: "DovizKur",
                columns: new[] { "Tarih", "ParaBirimi" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ek_HedefTip_HedefId",
                table: "Ek",
                columns: new[] { "HedefTip", "HedefId" });

            migrationBuilder.CreateIndex(
                name: "IX_Evrak_AracId",
                table: "Evrak",
                column: "AracId",
                filter: "[AracId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Evrak_BitisTarihi",
                table: "Evrak",
                column: "BitisTarihi",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Evrak_DorseId",
                table: "Evrak",
                column: "DorseId",
                filter: "[DorseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Evrak_EvrakTuruId",
                table: "Evrak",
                column: "EvrakTuruId");

            migrationBuilder.CreateIndex(
                name: "IX_Evrak_SoforId",
                table: "Evrak",
                column: "SoforId",
                filter: "[SoforId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EvrakTuru_Ad",
                table: "EvrakTuru",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fatura_CariId",
                table: "Fatura",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_Fatura_FaturaTipi_CariId_FaturaNo",
                table: "Fatura",
                columns: new[] { "FaturaTipi", "CariId", "FaturaNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fatura_Tarih",
                table: "Fatura",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalem_FaturaId",
                table: "FaturaKalem",
                column: "FaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalem_SeferId",
                table: "FaturaKalem",
                column: "SeferId",
                filter: "[SeferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinansmanTaksit_AracFinansmanId_TaksitNo",
                table: "FinansmanTaksit",
                columns: new[] { "AracFinansmanId", "TaksitNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinansmanTaksit_OdemeKasaHesapId",
                table: "FinansmanTaksit",
                column: "OdemeKasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_FinansmanTaksit_VadeTarihi_OdemeTarihi",
                table: "FinansmanTaksit",
                columns: new[] { "VadeTarihi", "OdemeTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_Guzergah_CikisLokasyonId",
                table: "Guzergah",
                column: "CikisLokasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_Guzergah_VarisLokasyonId",
                table: "Guzergah",
                column: "VarisLokasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_Harcirah_MahsupId",
                table: "Harcirah",
                column: "MahsupId");

            migrationBuilder.CreateIndex(
                name: "IX_Harcirah_SeferId",
                table: "Harcirah",
                column: "SeferId",
                filter: "[SeferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Harcirah_SoforId_MahsupId",
                table: "Harcirah",
                columns: new[] { "SoforId", "MahsupId" });

            migrationBuilder.CreateIndex(
                name: "IX_Harcirah_UlkeId",
                table: "Harcirah",
                column: "UlkeId");

            migrationBuilder.CreateIndex(
                name: "IX_HarcirahTarife_UlkeId_GecerlilikBaslangic",
                table: "HarcirahTarife",
                columns: new[] { "UlkeId", "GecerlilikBaslangic" });

            migrationBuilder.CreateIndex(
                name: "IX_HgsGecis_AracId_GecisZamani_Tutar",
                table: "HgsGecis",
                columns: new[] { "AracId", "GecisZamani", "Tutar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HgsGecis_IceAktarimId",
                table: "HgsGecis",
                column: "IceAktarimId");

            migrationBuilder.CreateIndex(
                name: "IX_HgsGecis_SeferId",
                table: "HgsGecis",
                column: "SeferId",
                filter: "[SeferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IceAktarim_CariId",
                table: "IceAktarim",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAktarim_SablonId",
                table: "IceAktarim",
                column: "SablonId");

            migrationBuilder.CreateIndex(
                name: "IX_Izin_Kod",
                table: "Izin",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareket_CariId",
                table: "KasaHareket",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareket_KasaHesapId_Tarih",
                table: "KasaHareket",
                columns: new[] { "KasaHesapId", "Tarih" })
                .Annotation("SqlServer:Include", new[] { "Giris", "Cikis" });

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareket_KaynakTip_KaynakId",
                table: "KasaHareket",
                columns: new[] { "KaynakTip", "KaynakId" });

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareket_TersKayitId",
                table: "KasaHareket",
                column: "TersKayitId",
                filter: "[TersKayitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanici_KullaniciAdi",
                table: "Kullanici",
                column: "KullaniciAdi",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KullaniciRol_RolId",
                table: "KullaniciRol",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_Lastik_SeriNo",
                table: "Lastik",
                column: "SeriNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Lastik_TedarikciCariId",
                table: "Lastik",
                column: "TedarikciCariId");

            migrationBuilder.CreateIndex(
                name: "IX_LastikHareket_AracId_SokmeTarihi",
                table: "LastikHareket",
                columns: new[] { "AracId", "SokmeTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_LastikHareket_DorseId",
                table: "LastikHareket",
                column: "DorseId");

            migrationBuilder.CreateIndex(
                name: "IX_LastikHareket_LastikId",
                table: "LastikHareket",
                column: "LastikId");

            migrationBuilder.CreateIndex(
                name: "IX_Lokasyon_Ad",
                table: "Lokasyon",
                column: "Ad");

            migrationBuilder.CreateIndex(
                name: "IX_Lokasyon_CariId",
                table: "Lokasyon",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_Lokasyon_IlId",
                table: "Lokasyon",
                column: "IlId");

            migrationBuilder.CreateIndex(
                name: "IX_Lokasyon_UlkeId",
                table: "Lokasyon",
                column: "UlkeId");

            migrationBuilder.CreateIndex(
                name: "IX_Mahsup_KasaHesapId",
                table: "Mahsup",
                column: "KasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_Mahsup_SeferId",
                table: "Mahsup",
                column: "SeferId");

            migrationBuilder.CreateIndex(
                name: "IX_Mahsup_SoforId_Tarih",
                table: "Mahsup",
                columns: new[] { "SoforId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Masraf_AracId_Tarih",
                table: "Masraf",
                columns: new[] { "AracId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Masraf_DorseId",
                table: "Masraf",
                column: "DorseId");

            migrationBuilder.CreateIndex(
                name: "IX_Masraf_MasrafTuruId",
                table: "Masraf",
                column: "MasrafTuruId");

            migrationBuilder.CreateIndex(
                name: "IX_Masraf_SeferId",
                table: "Masraf",
                column: "SeferId",
                filter: "[SeferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Masraf_SoforId_MahsupId",
                table: "Masraf",
                columns: new[] { "SoforId", "MahsupId" },
                filter: "[SoforId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Masraf_Tarih",
                table: "Masraf",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_MasrafTuru_Ad",
                table: "MasrafTuru",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rol_Ad",
                table: "Rol",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolIzin_IzinId",
                table: "RolIzin",
                column: "IzinId");

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_AracId_PlanlananTarih",
                table: "Sefer",
                columns: new[] { "AracId", "PlanlananTarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_BagliSeferId",
                table: "Sefer",
                column: "BagliSeferId");

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_CikisLokasyonId",
                table: "Sefer",
                column: "CikisLokasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_DorseId",
                table: "Sefer",
                column: "DorseId");

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_Durum_PlanlananTarih",
                table: "Sefer",
                columns: new[] { "Durum", "PlanlananTarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_GuzergahId",
                table: "Sefer",
                column: "GuzergahId");

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_MusteriCariId_PlanlananTarih",
                table: "Sefer",
                columns: new[] { "MusteriCariId", "PlanlananTarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_SeferNo",
                table: "Sefer",
                column: "SeferNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_TaseronCariId",
                table: "Sefer",
                column: "TaseronCariId");

            migrationBuilder.CreateIndex(
                name: "IX_Sefer_VarisLokasyonId",
                table: "Sefer",
                column: "VarisLokasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeferDurak_LokasyonId",
                table: "SeferDurak",
                column: "LokasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeferDurak_SeferId",
                table: "SeferDurak",
                column: "SeferId");

            migrationBuilder.CreateIndex(
                name: "IX_SeferSofor_SoforId",
                table: "SeferSofor",
                column: "SoforId");

            migrationBuilder.CreateIndex(
                name: "IX_Sofor_CariId",
                table: "Sofor",
                column: "CariId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sofor_TCKN",
                table: "Sofor",
                column: "TCKN",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Sofor_VarsayilanAracId",
                table: "Sofor",
                column: "VarsayilanAracId");

            migrationBuilder.CreateIndex(
                name: "IX_Ulke_Kod",
                table: "Ulke",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Yakit_AracId_AlimZamani",
                table: "Yakit",
                columns: new[] { "AracId", "AlimZamani" });

            migrationBuilder.CreateIndex(
                name: "IX_Yakit_IceAktarimId",
                table: "Yakit",
                column: "IceAktarimId");

            migrationBuilder.CreateIndex(
                name: "IX_Yakit_IstasyonCariId",
                table: "Yakit",
                column: "IstasyonCariId");

            migrationBuilder.CreateIndex(
                name: "IX_Yakit_SeferId",
                table: "Yakit",
                column: "SeferId",
                filter: "[SeferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Yakit_SoforId_MahsupId",
                table: "Yakit",
                columns: new[] { "SoforId", "MahsupId" },
                filter: "[SoforId] IS NOT NULL");

            // Elle eklendi: veri sıkıştırma (PAGE/ROW).
            Sikistirma.Uygula(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AracDorseEslesme");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "Avans");

            migrationBuilder.DropTable(
                name: "Ayar");

            migrationBuilder.DropTable(
                name: "BakimKalem");

            migrationBuilder.DropTable(
                name: "CariHareket");

            migrationBuilder.DropTable(
                name: "CekSenetHareket");

            migrationBuilder.DropTable(
                name: "Ceza");

            migrationBuilder.DropTable(
                name: "DovizKur");

            migrationBuilder.DropTable(
                name: "Ek");

            migrationBuilder.DropTable(
                name: "Evrak");

            migrationBuilder.DropTable(
                name: "FaturaKalem");

            migrationBuilder.DropTable(
                name: "FinansmanTaksit");

            migrationBuilder.DropTable(
                name: "Harcirah");

            migrationBuilder.DropTable(
                name: "HarcirahTarife");

            migrationBuilder.DropTable(
                name: "HgsGecis");

            migrationBuilder.DropTable(
                name: "KasaHareket");

            migrationBuilder.DropTable(
                name: "KullaniciRol");

            migrationBuilder.DropTable(
                name: "LastikHareket");

            migrationBuilder.DropTable(
                name: "Masraf");

            migrationBuilder.DropTable(
                name: "RolIzin");

            migrationBuilder.DropTable(
                name: "SeferDurak");

            migrationBuilder.DropTable(
                name: "SeferSofor");

            migrationBuilder.DropTable(
                name: "Yakit");

            migrationBuilder.DropTable(
                name: "Bakim");

            migrationBuilder.DropTable(
                name: "CekSenet");

            migrationBuilder.DropTable(
                name: "EvrakTuru");

            migrationBuilder.DropTable(
                name: "Fatura");

            migrationBuilder.DropTable(
                name: "AracFinansman");

            migrationBuilder.DropTable(
                name: "Mahsup");

            migrationBuilder.DropTable(
                name: "Kullanici");

            migrationBuilder.DropTable(
                name: "Lastik");

            migrationBuilder.DropTable(
                name: "MasrafTuru");

            migrationBuilder.DropTable(
                name: "Izin");

            migrationBuilder.DropTable(
                name: "Rol");

            migrationBuilder.DropTable(
                name: "IceAktarim");

            migrationBuilder.DropTable(
                name: "BakimPlani");

            migrationBuilder.DropTable(
                name: "KasaHesap");

            migrationBuilder.DropTable(
                name: "Sefer");

            migrationBuilder.DropTable(
                name: "Sofor");

            migrationBuilder.DropTable(
                name: "IceAktarimSablon");

            migrationBuilder.DropTable(
                name: "Dorse");

            migrationBuilder.DropTable(
                name: "Guzergah");

            migrationBuilder.DropTable(
                name: "Arac");

            migrationBuilder.DropTable(
                name: "Lokasyon");

            migrationBuilder.DropTable(
                name: "Cari");

            migrationBuilder.DropTable(
                name: "Il");

            migrationBuilder.DropTable(
                name: "Ulke");

            migrationBuilder.DropSequence(
                name: "CariKodSeq");

            migrationBuilder.DropSequence(
                name: "IslemNoSeq");

            migrationBuilder.DropSequence(
                name: "PortfoyNoSeq");

            migrationBuilder.DropSequence(
                name: "SeferNoSeq");
        }
    }
}
