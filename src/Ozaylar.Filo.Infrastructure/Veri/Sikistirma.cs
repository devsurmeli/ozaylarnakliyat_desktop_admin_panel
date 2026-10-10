using Microsoft.EntityFrameworkCore.Migrations;

namespace Ozaylar.Filo.Infrastructure.Veri;

/// <summary>
/// Veri sıkıştırma (SQL Server Express'te de var). Büyüyen hareket tablolarına PAGE, diğerlerine ROW uygulanır.
/// Tekrar çalıştırılabilir: yalnızca istenen sıkıştırmada olmayan tablo/indeksler yeniden oluşturulur.
/// Yeni tablo ekleyen migration'ların sonunda <c>Sikistirma.Uygula(migrationBuilder)</c> çağrılmalıdır.
/// </summary>
public static class Sikistirma
{
    public static readonly string[] PageTablolar =
        ["CariHareket", "KasaHareket", "Yakit", "Masraf", "HgsGecis", "AuditLog", "Sefer", "FaturaKalem", "Harcirah", "Avans"];

    public static void Uygula(MigrationBuilder mb) => mb.Sql(Sql, suppressTransaction: true);

    public static string Sql =>
        $"""
        DECLARE @sql nvarchar(max) = N'';
        SELECT @sql += N'ALTER INDEX ' + QUOTENAME(i.name) + N' ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                     + N' REBUILD WITH (DATA_COMPRESSION = ' + x.hedef + N');' + CHAR(10)
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.indexes i ON i.object_id = t.object_id AND i.index_id > 0
        JOIN sys.partitions p ON p.object_id = i.object_id AND p.index_id = i.index_id
        CROSS APPLY (SELECT CASE WHEN t.name IN ({string.Join(",", PageTablolar.Select(x => $"N'{x}'"))}) THEN N'PAGE' ELSE N'ROW' END AS hedef) x
        WHERE t.is_ms_shipped = 0 AND t.name <> N'__EFMigrationsHistory' AND p.data_compression_desc <> x.hedef;

        SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                     + N' REBUILD WITH (DATA_COMPRESSION = ' + x.hedef + N');' + CHAR(10)
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id = 0
        CROSS APPLY (SELECT CASE WHEN t.name IN ({string.Join(",", PageTablolar.Select(x => $"N'{x}'"))}) THEN N'PAGE' ELSE N'ROW' END AS hedef) x
        WHERE t.is_ms_shipped = 0 AND p.data_compression_desc <> x.hedef;

        EXEC sys.sp_executesql @sql;
        """;
}
