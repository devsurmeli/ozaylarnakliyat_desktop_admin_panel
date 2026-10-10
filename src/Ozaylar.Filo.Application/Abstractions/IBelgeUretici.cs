using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Abstractions;

public enum MakbuzTuru : byte { Avans = 1, Tahsilat = 2, Tediye = 3 }

public sealed record MakbuzVeri(MakbuzTuru Tur, string No, DateOnly Tarih, string KarsiTaraf, decimal Tutar, ParaBirimi ParaBirimi, string OdemeSekli,
    string? Aciklama, string? Plaka = null, string? SeferNo = null);

/// <summary>Yazdırılabilir belgeler (PDF). Firma başlığı ayarlardan okunur.</summary>
public interface IBelgeUretici
{
    byte[] Makbuz(MakbuzVeri v);
    byte[] SeferFoyu(SeferOzet o, IReadOnlyList<MasrafSatir> masraflar, IReadOnlyList<AvansSatir> avanslar);
    byte[] SoforEkstresi(string sofor, DateOnly bas, DateOnly bit, IReadOnlyList<SoforDefterSatir> satirlar);
    byte[] HakedisPusulasi(string sofor, int donem, IReadOnlyList<TahakkukSatir> satirlar, decimal guncelBakiye);
    byte[] KrediOdemePlani(KrediDetay d);
    byte[] ZimmetTutanagi(string sofor, IReadOnlyList<Zimmet> zimmetler);
    byte[] CariMutabakat(CariOzet c, DateOnly tarih);
}
