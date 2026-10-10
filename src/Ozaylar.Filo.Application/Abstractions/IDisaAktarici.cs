namespace Ozaylar.Filo.Application.Abstractions;

public enum KolonBicim : byte { Metin, Para, Sayi, Tarih, TarihSaat, Yuzde }

public sealed record TabloKolon(string Baslik, Func<object, object?> Deger, KolonBicim Bicim = KolonBicim.Metin, bool Toplam = false);

/// <summary>Excel/PDF'e aktarılacak tablo. Arayüzdeki listeler bu yapıya çevrilip dışa aktarılır.</summary>
public sealed record TabloVerisi(string Baslik, IReadOnlyList<TabloKolon> Kolonlar, IReadOnlyList<object> Satirlar)
{
    /// <summary>Başlığın altında gösterilecek bilgi satırları (ör. tarih aralığı, devreden bakiye).</summary>
    public IReadOnlyList<string> AltBilgi { get; init; } = [];
}

public interface IDisaAktarici
{
    byte[] Excel(TabloVerisi tablo);
    byte[] Pdf(TabloVerisi tablo, bool yatay = true);
}
