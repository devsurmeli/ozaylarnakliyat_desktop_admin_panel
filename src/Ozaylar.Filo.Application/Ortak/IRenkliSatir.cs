namespace Ozaylar.Filo.Application.Ortak;

/// <summary>
/// Liste satırının durum rengi: "kirmizi" (gecikti/sorun), "turuncu" (yaklaşıyor), "yesil" (tamam), "gri" (kapalı/pasif) veya null.
/// Arayüz satırı bu değere göre renklendirir.
/// </summary>
public interface IRenkliSatir
{
    string? Renk { get; }
}
