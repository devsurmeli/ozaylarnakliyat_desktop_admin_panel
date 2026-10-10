using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ozaylar.Filo.Infrastructure.Veri.Migrations
{
    /// <inheritdoc />
    public partial class V2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BakiyeLimiti",
                table: "Sofor",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaasTutari",
                table: "Sofor",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "PrimTipi",
                table: "Sofor",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "PrimTutari",
                table: "Sofor",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "AracId",
                table: "Harcirah",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "AracId",
                table: "CariHareket",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeferId",
                table: "CariHareket",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "AracId",
                table: "Avans",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Tur",
                table: "Avans",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "AracKmKayit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: false),
                    Zaman = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Km = table.Column<int>(type: "int", nullable: false),
                    Kaynak = table.Column<byte>(type: "tinyint", nullable: false),
                    KaynakId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AracKmKayit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AracKmKayit_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankaLimit",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankaCariId = table.Column<int>(type: "int", nullable: false),
                    LimitTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    Teminat = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    GecerlilikBitis = table.Column<DateOnly>(type: "date", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankaLimit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankaLimit_Cari_BankaCariId",
                        column: x => x.BankaCariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kredi",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KrediNo = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    BankaCariId = table.Column<int>(type: "int", nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    KullandirimTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    KullandirimKasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    KullandirimKur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Anapara = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FaizOrani = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    FaizTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    BsmvOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    KkdfOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DosyaMasrafi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VadeSayisi = table.Column<short>(type: "smallint", nullable: false),
                    IlkVade = table.Column<DateOnly>(type: "date", nullable: false),
                    OdemesizAy = table.Column<byte>(type: "tinyint", nullable: false),
                    Yontem = table.Column<byte>(type: "tinyint", nullable: false),
                    BalonTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GecikmeArtisYuzde = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Amac = table.Column<byte>(type: "tinyint", nullable: false),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    AktifVersiyon = table.Column<byte>(type: "tinyint", nullable: false),
                    KapanisTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kredi", x => x.Id);
                    table.CheckConstraint("CK_Kredi_Anapara", "[Anapara] > 0");
                    table.ForeignKey(
                        name: "FK_Kredi_Cari_BankaCariId",
                        column: x => x.BankaCariId,
                        principalTable: "Cari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kredi_KasaHesap_KullandirimKasaHesapId",
                        column: x => x.KullandirimKasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PersonelTahakkuk",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoforId = table.Column<short>(type: "smallint", nullable: false),
                    Donem = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SeferId = table.Column<int>(type: "int", nullable: true),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Iptal = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonelTahakkuk", x => x.Id);
                    table.CheckConstraint("CK_PersonelTahakkuk_Tutar", "[Tutar] > 0");
                    table.ForeignKey(
                        name: "FK_PersonelTahakkuk_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PersonelTahakkuk_Sefer_SeferId",
                        column: x => x.SeferId,
                        principalTable: "Sefer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PersonelTahakkuk_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoforAracAtama",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoforId = table.Column<short>(type: "smallint", nullable: false),
                    AracId = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_SoforAracAtama", x => x.Id);
                    table.CheckConstraint("CK_SoforAracAtama_Tarih", "[Bitis] IS NULL OR [Bitis] >= [Baslangic]");
                    table.ForeignKey(
                        name: "FK_SoforAracAtama_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoforAracAtama_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zimmet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    Ad = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SeriNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    SoforId = table.Column<short>(type: "smallint", nullable: true),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    VerilisTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    IadeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    Deger = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_Zimmet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zimmet_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Zimmet_Sofor_SoforId",
                        column: x => x.SoforId,
                        principalTable: "Sofor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KrediArac",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KrediId = table.Column<short>(type: "smallint", nullable: false),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    PayYuzde = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KrediArac", x => x.Id);
                    table.CheckConstraint("CK_KrediArac_Hedef", "([AracId] IS NOT NULL AND [DorseId] IS NULL) OR ([AracId] IS NULL AND [DorseId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_KrediArac_Arac_AracId",
                        column: x => x.AracId,
                        principalTable: "Arac",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KrediArac_Dorse_DorseId",
                        column: x => x.DorseId,
                        principalTable: "Dorse",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KrediArac_Kredi_KrediId",
                        column: x => x.KrediId,
                        principalTable: "Kredi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KrediTaksit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KrediId = table.Column<short>(type: "smallint", nullable: false),
                    Versiyon = table.Column<byte>(type: "tinyint", nullable: false),
                    TaksitNo = table.Column<short>(type: "smallint", nullable: false),
                    VadeTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    Anapara = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Faiz = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Bsmv = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Kkdf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Toplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KalanAnapara = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OdenenTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KrediTaksit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KrediTaksit_Kredi_KrediId",
                        column: x => x.KrediId,
                        principalTable: "Kredi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KrediOdeme",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KrediId = table.Column<short>(type: "smallint", nullable: false),
                    KrediTaksitId = table.Column<int>(type: "int", nullable: true),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    KasaHesapId = table.Column<byte>(type: "tinyint", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    KasaTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Anapara = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Faiz = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Bsmv = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Kkdf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GecikmeFaizi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Komisyon = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Iptal = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KrediOdeme", x => x.Id);
                    table.CheckConstraint("CK_KrediOdeme_Tutar", "[Tutar] > 0");
                    table.ForeignKey(
                        name: "FK_KrediOdeme_KasaHesap_KasaHesapId",
                        column: x => x.KasaHesapId,
                        principalTable: "KasaHesap",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KrediOdeme_KrediTaksit_KrediTaksitId",
                        column: x => x.KrediTaksitId,
                        principalTable: "KrediTaksit",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KrediOdeme_Kredi_KrediId",
                        column: x => x.KrediId,
                        principalTable: "Kredi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CariHareket_AracId_Tarih",
                table: "CariHareket",
                columns: new[] { "AracId", "Tarih" },
                filter: "[AracId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareket_SeferId",
                table: "CariHareket",
                column: "SeferId",
                filter: "[SeferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Avans_AracId",
                table: "Avans",
                column: "AracId");

            migrationBuilder.CreateIndex(
                name: "IX_AracKmKayit_AracId_Zaman",
                table: "AracKmKayit",
                columns: new[] { "AracId", "Zaman" });

            migrationBuilder.CreateIndex(
                name: "IX_BankaLimit_BankaCariId",
                table: "BankaLimit",
                column: "BankaCariId");

            migrationBuilder.CreateIndex(
                name: "IX_Kredi_BankaCariId_Durum",
                table: "Kredi",
                columns: new[] { "BankaCariId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_Kredi_KullandirimKasaHesapId",
                table: "Kredi",
                column: "KullandirimKasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_KrediArac_AracId",
                table: "KrediArac",
                column: "AracId",
                filter: "[AracId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KrediArac_DorseId",
                table: "KrediArac",
                column: "DorseId");

            migrationBuilder.CreateIndex(
                name: "IX_KrediArac_KrediId",
                table: "KrediArac",
                column: "KrediId");

            migrationBuilder.CreateIndex(
                name: "IX_KrediOdeme_KasaHesapId",
                table: "KrediOdeme",
                column: "KasaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_KrediOdeme_KrediId_Tarih",
                table: "KrediOdeme",
                columns: new[] { "KrediId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_KrediOdeme_KrediTaksitId",
                table: "KrediOdeme",
                column: "KrediTaksitId");

            migrationBuilder.CreateIndex(
                name: "IX_KrediOdeme_Tarih",
                table: "KrediOdeme",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_KrediTaksit_KrediId_Versiyon_TaksitNo",
                table: "KrediTaksit",
                columns: new[] { "KrediId", "Versiyon", "TaksitNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KrediTaksit_VadeTarihi_Durum",
                table: "KrediTaksit",
                columns: new[] { "VadeTarihi", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonelTahakkuk_AracId",
                table: "PersonelTahakkuk",
                column: "AracId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonelTahakkuk_SeferId",
                table: "PersonelTahakkuk",
                column: "SeferId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonelTahakkuk_SoforId_Donem",
                table: "PersonelTahakkuk",
                columns: new[] { "SoforId", "Donem" });

            migrationBuilder.CreateIndex(
                name: "IX_SoforAracAtama_AracId_Baslangic",
                table: "SoforAracAtama",
                columns: new[] { "AracId", "Baslangic" });

            migrationBuilder.CreateIndex(
                name: "IX_SoforAracAtama_SoforId_Baslangic",
                table: "SoforAracAtama",
                columns: new[] { "SoforId", "Baslangic" });

            migrationBuilder.CreateIndex(
                name: "IX_Zimmet_AracId",
                table: "Zimmet",
                column: "AracId");

            migrationBuilder.CreateIndex(
                name: "IX_Zimmet_SoforId_Durum",
                table: "Zimmet",
                columns: new[] { "SoforId", "Durum" });

            migrationBuilder.AddForeignKey(
                name: "FK_Avans_Arac_AracId",
                table: "Avans",
                column: "AracId",
                principalTable: "Arac",
                principalColumn: "Id");

            // Elle eklendi: v1 araç finansmanı → v2 kredi modeli veri aktarımı (Id'ler korunur; v1 kasa kayıtları olduğu gibi kalır).
            migrationBuilder.Sql(V1KrediAktarimSql);

            migrationBuilder.DropTable(
                name: "FinansmanTaksit");

            migrationBuilder.DropTable(
                name: "AracFinansman");

            // Elle eklendi: yeni tablolara veri sıkıştırma (PAGE/ROW).
            Sikistirma.Uygula(migrationBuilder);
        }

        /// <summary>
        /// AracFinansman → Kredi (aylık faiz, vergiler sıfır: v1 oranı efektifti), FinansmanTaksit → KrediTaksit (kalan anapara yeniden hesaplanır),
        /// ödenmiş taksit → KrediOdeme (kasa kaydı v1'de yazılmıştı; yeniden yazılmaz), araç/dorse → KrediArac (%100).
        /// v1 kullandırımı deftere yazmadığı için banka carisine kalan anapara kadar alacak (KrediKullanim) eklenir.
        /// </summary>
        private const string V1KrediAktarimSql = """
            SET IDENTITY_INSERT [Kredi] ON;
            INSERT INTO [Kredi] ([Id], [KrediNo], [Tur], [BankaCariId], [ParaBirimi], [KullandirimTarihi], [KullandirimKasaHesapId], [KullandirimKur], [Anapara],
                [FaizOrani], [FaizTipi], [BsmvOrani], [KkdfOrani], [DosyaMasrafi], [VadeSayisi], [IlkVade], [OdemesizAy], [Yontem], [BalonTutar], [GecikmeArtisYuzde],
                [Amac], [Durum], [AktifVersiyon], [KapanisTarihi], [Aciklama], [OlusturanId], [OlusturmaTarihi], [GuncelleyenId], [GuncellemeTarihi], [IsDeleted])
            SELECT f.[Id], LEFT(COALESCE(NULLIF(f.[SozlesmeNo], ''), CONCAT('K-', f.[Id])), 40), CASE f.[Tip] WHEN 2 THEN 2 ELSE 1 END, f.[FinansKurumuCariId],
                f.[ParaBirimi], f.[BaslangicTarihi], NULL, 1, f.[AnaparaTutar], f.[AylikFaizOrani], 1, 0, 0, 0, f.[VadeSayisi],
                COALESCE((SELECT MIN(t.[VadeTarihi]) FROM [FinansmanTaksit] t WHERE t.[AracFinansmanId] = f.[Id]), DATEADD(MONTH, 1, f.[BaslangicTarihi])),
                0, 1, NULL, 30, CASE WHEN f.[AracId] IS NULL AND f.[DorseId] IS NULL THEN 2 ELSE 1 END, 1, 1, NULL, f.[Aciklama],
                f.[OlusturanId], f.[OlusturmaTarihi], f.[GuncelleyenId], f.[GuncellemeTarihi], f.[IsDeleted]
            FROM [AracFinansman] f;
            SET IDENTITY_INSERT [Kredi] OFF;

            SET IDENTITY_INSERT [KrediTaksit] ON;
            INSERT INTO [KrediTaksit] ([Id], [KrediId], [Versiyon], [TaksitNo], [VadeTarihi], [Anapara], [Faiz], [Bsmv], [Kkdf], [Toplam], [KalanAnapara],
                [OdenenTutar], [Durum], [OlusturanId], [OlusturmaTarihi], [GuncelleyenId], [GuncellemeTarihi])
            SELECT t.[Id], t.[AracFinansmanId], 1, t.[TaksitNo], t.[VadeTarihi], t.[Anapara], t.[Faiz], 0, 0, t.[Toplam],
                f.[AnaparaTutar] - SUM(t.[Anapara]) OVER (PARTITION BY t.[AracFinansmanId] ORDER BY t.[TaksitNo] ROWS UNBOUNDED PRECEDING),
                CASE WHEN t.[OdemeTarihi] IS NULL THEN 0 ELSE t.[Toplam] END, CASE WHEN t.[OdemeTarihi] IS NULL THEN 1 ELSE 3 END,
                t.[OlusturanId], t.[OlusturmaTarihi], t.[GuncelleyenId], t.[GuncellemeTarihi]
            FROM [FinansmanTaksit] t JOIN [AracFinansman] f ON f.[Id] = t.[AracFinansmanId];
            SET IDENTITY_INSERT [KrediTaksit] OFF;

            INSERT INTO [KrediOdeme] ([KrediId], [KrediTaksitId], [Tarih], [Tur], [KasaHesapId], [Tutar], [Kur], [KasaTutar], [Anapara], [Faiz], [Bsmv], [Kkdf],
                [GecikmeFaizi], [Komisyon], [Aciklama], [Iptal], [OlusturanId], [OlusturmaTarihi])
            SELECT t.[AracFinansmanId], t.[Id], t.[OdemeTarihi], 1, t.[OdemeKasaHesapId], t.[Toplam], 1, t.[Toplam], t.[Anapara], t.[Faiz], 0, 0, 0, 0,
                CONCAT('Taksit ', t.[TaksitNo], ' (v1 aktarım)'), 0, t.[GuncelleyenId], COALESCE(t.[GuncellemeTarihi], t.[OlusturmaTarihi])
            FROM [FinansmanTaksit] t
            WHERE t.[OdemeTarihi] IS NOT NULL AND t.[OdemeKasaHesapId] IS NOT NULL;

            INSERT INTO [KrediArac] ([KrediId], [AracId], [DorseId], [PayYuzde])
            SELECT f.[Id], f.[AracId], CASE WHEN f.[AracId] IS NULL THEN f.[DorseId] END, 100
            FROM [AracFinansman] f WHERE f.[AracId] IS NOT NULL OR f.[DorseId] IS NOT NULL;

            UPDATE k SET [Durum] = 2, [KapanisTarihi] = (SELECT MAX(o.[Tarih]) FROM [KrediOdeme] o WHERE o.[KrediId] = k.[Id])
            FROM [Kredi] k
            WHERE NOT EXISTS (SELECT 1 FROM [KrediTaksit] t WHERE t.[KrediId] = k.[Id] AND t.[Durum] = 1)
              OR EXISTS (SELECT 1 FROM [AracFinansman] f WHERE f.[Id] = k.[Id] AND f.[Aktif] = 0);

            INSERT INTO [CariHareket] ([CariId], [VadeTarihi], [Borc], [Alacak], [ParaBirimi], [Kur], [DovizBorc], [DovizAlacak], [Tarih], [KaynakTip], [KaynakId],
                [Aciklama], [TersKayitId], [OlusturanId], [OlusturmaTarihi])
            SELECT k.[BankaCariId], NULL, 0, x.[Kalan], 1, 1, NULL, NULL, k.[KullandirimTarihi], 20, k.[Id],
                LEFT(CONCAT('Kredi kalan anaparası ', k.[KrediNo], ' (v1 aktarım)'), 200), NULL, NULL, SYSUTCDATETIME()
            FROM [Kredi] k
            CROSS APPLY (SELECT k.[Anapara] - COALESCE((SELECT SUM(o.[Anapara]) FROM [KrediOdeme] o WHERE o.[KrediId] = k.[Id]), 0) AS [Kalan]) x
            WHERE x.[Kalan] > 0 AND k.[Durum] = 1 AND k.[ParaBirimi] = 1 AND k.[IsDeleted] = 0;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Avans_Arac_AracId",
                table: "Avans");

            migrationBuilder.DropTable(
                name: "AracKmKayit");

            migrationBuilder.DropTable(
                name: "BankaLimit");

            migrationBuilder.DropTable(
                name: "KrediArac");

            migrationBuilder.DropTable(
                name: "KrediOdeme");

            migrationBuilder.DropTable(
                name: "PersonelTahakkuk");

            migrationBuilder.DropTable(
                name: "SoforAracAtama");

            migrationBuilder.DropTable(
                name: "Zimmet");

            migrationBuilder.DropTable(
                name: "KrediTaksit");

            migrationBuilder.DropTable(
                name: "Kredi");

            migrationBuilder.DropIndex(
                name: "IX_CariHareket_AracId_Tarih",
                table: "CariHareket");

            migrationBuilder.DropIndex(
                name: "IX_CariHareket_SeferId",
                table: "CariHareket");

            migrationBuilder.DropIndex(
                name: "IX_Avans_AracId",
                table: "Avans");

            migrationBuilder.DropColumn(
                name: "BakiyeLimiti",
                table: "Sofor");

            migrationBuilder.DropColumn(
                name: "MaasTutari",
                table: "Sofor");

            migrationBuilder.DropColumn(
                name: "PrimTipi",
                table: "Sofor");

            migrationBuilder.DropColumn(
                name: "PrimTutari",
                table: "Sofor");

            migrationBuilder.DropColumn(
                name: "AracId",
                table: "Harcirah");

            migrationBuilder.DropColumn(
                name: "AracId",
                table: "CariHareket");

            migrationBuilder.DropColumn(
                name: "SeferId",
                table: "CariHareket");

            migrationBuilder.DropColumn(
                name: "AracId",
                table: "Avans");

            migrationBuilder.DropColumn(
                name: "Tur",
                table: "Avans");

            migrationBuilder.CreateTable(
                name: "AracFinansman",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracId = table.Column<short>(type: "smallint", nullable: true),
                    DorseId = table.Column<short>(type: "smallint", nullable: true),
                    FinansKurumuCariId = table.Column<int>(type: "int", nullable: false),
                    Aciklama = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    AnaparaTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AylikFaizOrani = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    BaslangicTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ParaBirimi = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SozlesmeNo = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Tip = table.Column<byte>(type: "tinyint", nullable: false),
                    VadeSayisi = table.Column<short>(type: "smallint", nullable: false)
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
                name: "FinansmanTaksit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AracFinansmanId = table.Column<short>(type: "smallint", nullable: false),
                    Anapara = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Faiz = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    GuncelleyenId = table.Column<short>(type: "smallint", nullable: true),
                    OdemeKasaHesapId = table.Column<byte>(type: "tinyint", nullable: true),
                    OdemeTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    OlusturanId = table.Column<short>(type: "smallint", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TaksitNo = table.Column<short>(type: "smallint", nullable: false),
                    Toplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VadeTarihi = table.Column<DateOnly>(type: "date", nullable: false)
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
        }
    }
}
