using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Veri;

/// <summary>
/// Dosya ekleri ayrı veritabanında tutulur: ana veritabanı küçük ve hızlı kalır,
/// istemcilerin SMB paylaşımına erişmesi gerekmez, her iki DB aynı yedek işiyle yedeklenir.
/// </summary>
public class EkDbContext(DbContextOptions<EkDbContext> options) : DbContext(options)
{
    public DbSet<EkIcerik> Icerikler => Set<EkIcerik>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.UseCollation(FiloDbContext.Collation);
        mb.Entity<EkIcerik>(b =>
        {
            b.ToTable("EkIcerik");
            b.Property(x => x.Icerik).HasColumnType("varbinary(max)");
        });
    }
}

public sealed class EkDepo(IDbContextFactory<EkDbContext> factory) : IEkDepo
{
    public async Task<int> KaydetAsync(byte[] icerik, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var e = new EkIcerik { Icerik = icerik };
        db.Icerikler.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task<byte[]?> OkuAsync(int ekIcerikId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Icerikler.Where(x => x.Id == ekIcerikId).Select(x => x.Icerik).FirstOrDefaultAsync(ct);
    }

    public async Task SilAsync(int ekIcerikId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Icerikler.Where(x => x.Id == ekIcerikId).ExecuteDeleteAsync(ct);
    }
}
