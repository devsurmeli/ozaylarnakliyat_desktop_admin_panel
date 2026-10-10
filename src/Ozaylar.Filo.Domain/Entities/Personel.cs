using System.ComponentModel;
using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public enum PrimTipi : byte
{
    [Description("Prim yok")] Yok = 0,
    [Description("Sefer başı")] SeferBasi = 1,
    [Description("Km başı")] KmBasi = 2,
}

public enum TahakkukTuru : byte
{
    [Description("Maaş")] Maas = 1,
    [Description("Sefer Primi")] SeferPrimi = 2,
    [Description("Km Primi")] KmPrimi = 3,
    [Description("Ek Ödeme / İkramiye")] Ek = 4,
    [Description("Kesinti")] Kesinti = 9,
}

public enum ZimmetTuru : byte
{
    [Description("Yakıt Kartı")] YakitKarti = 1,
    [Description("HGS/OGS Etiketi")] Hgs = 2,
    [Description("Telefon / Hat")] Telefon = 3,
    [Description("Takograf Kartı")] TakografKarti = 4,
    [Description("Ekipman")] Ekipman = 5,
    [Description("Diğer")] Diger = 9,
}

public enum ZimmetDurum : byte
{
    [Description("Zimmette")] Zimmette = 1,
    [Description("İade Edildi")] IadeEdildi = 2,
    [Description("Kayıp / Hasarlı")] Kayip = 3,
}

public enum AvansTuru : byte
{
    [Description("Sefer Avansı")] Avans = 1,
    [Description("Harçlık")] Harclik = 2,
    [Description("Yol Parası")] YolParasi = 3,
    [Description("Maaş / Hakediş Ödemesi")] MaasOdemesi = 4,
}

public enum KmKaynak : byte
{
    [Description("Sefer")] Sefer = 1,
    [Description("Yakıt")] Yakit = 2,
    [Description("Bakım")] Bakim = 3,
    [Description("Manuel")] Manuel = 4,
    [Description("Ekstre")] Ekstre = 5,
}

/// <summary>Şoför-araç atama geçmişi (Bitis null = halen kullanıyor).</summary>
public class SoforAracAtama : Kayit<int>
{
    public short SoforId { get; set; }
    public short AracId { get; set; }
    public DateOnly Baslangic { get; set; }
    public DateOnly? Bitis { get; set; }

    public Sofor? Sofor { get; set; }
    public Arac? Arac { get; set; }
}

/// <summary>Şoföre/araca zimmetlenen kart ve ekipman.</summary>
public class Zimmet : SilinebilirKayit<int>
{
    public ZimmetTuru Tur { get; set; } = ZimmetTuru.Ekipman;
    public string Ad { get; set; } = "";
    public string? SeriNo { get; set; }
    public short? SoforId { get; set; }
    public short? AracId { get; set; }
    public DateOnly VerilisTarihi { get; set; }
    public DateOnly? IadeTarihi { get; set; }
    public ZimmetDurum Durum { get; set; } = ZimmetDurum.Zimmette;
    public decimal? Deger { get; set; }
    public string? Aciklama { get; set; }

    public Sofor? Sofor { get; set; }
    public Arac? Arac { get; set; }
}

/// <summary>
/// Şoför hakediş satırı (iç takip, resmi bordro değildir). Maaş/prim/ek → şoför carisine alacak (biz borçluyuz),
/// kesinti → şoför carisine borç.
/// </summary>
public class PersonelTahakkuk : Kayit<int>, IDonemKilitli
{
    public short SoforId { get; set; }
    /// <summary>yyyyMM (ör. 202610).</summary>
    public int Donem { get; set; }
    public DateOnly Tarih { get; set; }
    public TahakkukTuru Tur { get; set; }
    public decimal Tutar { get; set; }
    public int? SeferId { get; set; }
    public short? AracId { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }

    public Sofor? Sofor { get; set; }
    public Sefer? Sefer { get; set; }
    public Arac? Arac { get; set; }

    public bool Kesinti => Tur == TahakkukTuru.Kesinti;
}

/// <summary>Araç km okuma geçmişi (sefer, yakıt, bakım, manuel).</summary>
public class AracKmKayit
{
    public int Id { get; set; }
    public short AracId { get; set; }
    public DateTime Zaman { get; set; }
    public int Km { get; set; }
    public KmKaynak Kaynak { get; set; }
    public int? KaynakId { get; set; }

    public Arac? Arac { get; set; }
}
