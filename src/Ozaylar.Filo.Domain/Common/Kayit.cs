namespace Ozaylar.Filo.Domain.Common;

/// <summary>Kim/ne zaman oluşturdu bilgisini taşıyan kayıtlar (değişmez defter satırları dahil).</summary>
public interface IOlusturmaBilgisi
{
    short? OlusturanId { get; set; }
    DateTime OlusturmaTarihi { get; set; }
}

/// <summary>Güncellenebilir kayıtlar: kim/ne zaman değiştirdi + iyimser kilit (rowversion).</summary>
public interface IGuncellemeBilgisi : IOlusturmaBilgisi
{
    short? GuncelleyenId { get; set; }
    DateTime? GuncellemeTarihi { get; set; }
    byte[] RowVer { get; set; }
}

/// <summary>Fiziksel olarak silinmeyen ana kayıtlar. Global query filter ile gizlenir.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

/// <summary>Kapanış tarihi (dönem kilidi) kontrolüne tabi, tarihli finansal belgeler.</summary>
public interface IDonemKilitli
{
    DateOnly Tarih { get; }
}

public abstract class Kayit<TKey> : IGuncellemeBilgisi where TKey : struct
{
    public TKey Id { get; set; }
    public short? OlusturanId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public short? GuncelleyenId { get; set; }
    public DateTime? GuncellemeTarihi { get; set; }
    public byte[] RowVer { get; set; } = [];
}

public abstract class SilinebilirKayit<TKey> : Kayit<TKey>, ISoftDelete where TKey : struct
{
    public bool IsDeleted { get; set; }
}

/// <summary>Değişmez defter satırı. Düzeltme yalnızca ters kayıtla yapılır; RowVer/IsDeleted yoktur (yer tasarrufu).</summary>
public abstract class DefterKaydi : IOlusturmaBilgisi, IDonemKilitli
{
    public int Id { get; set; }
    public DateOnly Tarih { get; set; }
    public KaynakTip KaynakTip { get; set; }
    public int? KaynakId { get; set; }
    public string? Aciklama { get; set; }
    /// <summary>Doluysa bu satır, belirtilen Id'li satırın ters (iptal) kaydıdır. Orijinal satır hiç değiştirilmez.</summary>
    public int? TersKayitId { get; set; }
    public short? OlusturanId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}

public class DomainException(string mesaj) : Exception(mesaj);
