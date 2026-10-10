using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Servisler;

public sealed record EkSatir(int Id, string DosyaAdi, string IcerikTipi, int Boyut, DateTime Tarih, string? Aciklama)
{
    public string BoyutMetin => Boyut < 1024 * 1024 ? $"{Boyut / 1024.0:N0} KB" : $"{Boyut / 1024.0 / 1024.0:N1} MB";
}

/// <summary>Dosya ekleri. İçerik ayrı veritabanında saklanır; aynı içerik (SHA-256) aynı hedefe ikinci kez eklenmez.</summary>
public sealed class EkServisi(IFiloDbContextFactory factory, IEkDepo depo, IKullaniciOturumu oturum)
{
    public const int MaksBoyut = 10 * 1024 * 1024;

    public async Task<List<EkSatir>> ListeAsync(EkHedef hedef, int hedefId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.Ekler.AsNoTracking().Where(e => e.HedefTip == hedef && e.HedefId == hedefId).OrderByDescending(e => e.Id)
            .Select(e => new EkSatir(e.Id, e.DosyaAdi, e.IcerikTipi, e.Boyut, e.OlusturmaTarihi, e.Aciklama)).ToListAsync(ct);
    }

    /// <param name="icerik">Görseller arayüzde küçültülmüş/sıkıştırılmış olarak gelir.</param>
    public async Task<int> EkleAsync(EkHedef hedef, int hedefId, string dosyaAdi, string icerikTipi, byte[] icerik, string? aciklama, CancellationToken ct = default)
    {
        if (!oturum.GirisYapildi) throw new YetkiYokException("oturum");
        if (icerik.Length == 0) throw new DomainException("Dosya boş.");
        if (icerik.Length > MaksBoyut) throw new DomainException($"Dosya en fazla {MaksBoyut / 1024 / 1024} MB olabilir.");
        var hash = SHA256.HashData(icerik);
        await using var db = factory.Olustur();
        if (await db.Ekler.AnyAsync(e => e.HedefTip == hedef && e.HedefId == hedefId && e.Sha256 == hash, ct))
            throw new DomainException("Bu dosya zaten ekli.");
        var icerikId = await depo.KaydetAsync(icerik, ct);
        try
        {
            var ek = new Ek
            {
                HedefTip = hedef, HedefId = hedefId, DosyaAdi = Path.GetFileName(dosyaAdi) is { Length: > 200 } u ? u[^200..] : Path.GetFileName(dosyaAdi),
                IcerikTipi = icerikTipi, Boyut = icerik.Length, Sha256 = hash, EkIcerikId = icerikId, Aciklama = aciklama,
            };
            db.Ekler.Add(ek);
            await db.SaveChangesAsync(ct);
            return ek.Id;
        }
        catch
        {
            await depo.SilAsync(icerikId, CancellationToken.None);
            throw;
        }
    }

    public async Task<(string DosyaAdi, byte[] Icerik)> IndirAsync(int ekId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var ek = await db.Ekler.AsNoTracking().SingleOrDefaultAsync(e => e.Id == ekId, ct) ?? throw new DomainException("Ek bulunamadı.");
        var icerik = await depo.OkuAsync(ek.EkIcerikId, ct) ?? throw new DomainException("Ek içeriği bulunamadı.");
        return (ek.DosyaAdi, icerik);
    }

    /// <summary>Eki gizler (soft delete) ve içeriği ek veritabanından siler (yer kazanımı).</summary>
    public async Task SilAsync(int ekId, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        var ek = await db.Ekler.SingleOrDefaultAsync(e => e.Id == ekId, ct) ?? throw new DomainException("Ek bulunamadı.");
        if (ek.OlusturanId != oturum.KullaniciId && !oturum.Yetkili(Izinler.AyarYonet))
            throw new DomainException("Yalnızca eki ekleyen kullanıcı veya yönetici silebilir.");
        db.Ekler.Remove(ek);
        await db.SaveChangesAsync(ct);
        await depo.SilAsync(ek.EkIcerikId, ct);
    }
}
