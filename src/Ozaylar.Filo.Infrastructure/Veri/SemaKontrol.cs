using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ozaylar.Filo.Infrastructure.Veri;

public enum SemaDurumu
{
    Uyumlu,
    /// <summary>Veritabanında bu istemcinin bilmediği migration var: istemci güncellenmeli.</summary>
    IstemciEski,
    /// <summary>İstemcinin beklediği migration veritabanında yok: sunucu servisi güncellemeyi uygulamalı.</summary>
    VeritabaniEski,
    BaglantiYok,
}

public sealed record SemaSonucu(SemaDurumu Durum, string Mesaj);

/// <summary>
/// İstemci açılışında şema sürümünü kontrol eder. İstemci (filo_app) migration UYGULAMAZ;
/// migration'ları yalnızca sunucu servisi/kurulum yönetici yetkisiyle uygular.
/// </summary>
public sealed class SemaKontrol(BaglantiBilgisi baglanti)
{
    public async Task<SemaSonucu> KontrolAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = new FiloDbContext(DbSecenekleri.Ana(baglanti.AnaBaglanti));
            var beklenen = db.Database.GetMigrations().ToList();
            var uygulanan = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
            var bilinmeyen = uygulanan.Except(beklenen).ToList();
            if (bilinmeyen.Count > 0)
                return new(SemaDurumu.IstemciEski, "Veritabanı bu programdan daha yeni bir sürümde. Lütfen programı güncelleyin.");
            var eksik = beklenen.Except(uygulanan).ToList();
            if (eksik.Count > 0)
                return new(SemaDurumu.VeritabaniEski, "Veritabanı güncellemesi bekleniyor. Sunucu bilgisayardaki Ozaylar Filo Sunucu servisinin çalıştığından emin olun.");
            return new(SemaDurumu.Uyumlu, "");
        }
        catch (SqlException ex)
        {
            return new(SemaDurumu.BaglantiYok, $"Sunucuya bağlanılamadı ({baglanti.Sunucu}): {ex.Message}");
        }
    }

    public static async Task<string?> BaglantiTestAsync(BaglantiBilgisi b, CancellationToken ct = default)
    {
        try
        {
            await using var c = new SqlConnection(b.AnaBaglanti);
            await c.OpenAsync(ct);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
