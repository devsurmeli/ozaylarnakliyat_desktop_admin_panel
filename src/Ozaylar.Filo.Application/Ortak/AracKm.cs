using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Ortak;

/// <summary>Araç km okuma geçmişi (sefer, yakıt, bakım, ekstre). Anormal okuma (geri giden km) raporda işaretlenir.</summary>
public static class AracKm
{
    public static void Kaydet(IFiloDbContext db, short aracId, DateTime zaman, int km, KmKaynak kaynak, int? kaynakId)
    {
        if (km <= 0) return;
        db.AracKmKayitlari.Add(new AracKmKayit { AracId = aracId, Zaman = zaman, Km = km, Kaynak = kaynak, KaynakId = kaynakId });
    }
}
