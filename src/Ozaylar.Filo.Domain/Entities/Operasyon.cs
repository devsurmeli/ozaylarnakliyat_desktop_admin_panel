using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public class Sefer : SilinebilirKayit<int>
{
    public string SeferNo { get; set; } = "";
    public SeferKapsam Kapsam { get; set; } = SeferKapsam.Yurtici;
    public SeferDurum Durum { get; set; } = SeferDurum.Planlandi;
    public int MusteriCariId { get; set; }

    /// <summary>Özmal araçla seferde dolu; taşeron seferinde null olabilir.</summary>
    public short? AracId { get; set; }
    public short? DorseId { get; set; }

    /// <summary>Taşeron (dış araç) seferi: taşeron cari, plaka ve ödenecek bedel.</summary>
    public int? TaseronCariId { get; set; }
    public string? TaseronPlaka { get; set; }
    public decimal? TaseronBedel { get; set; }
    public ParaBirimi TaseronParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal TaseronKur { get; set; } = 1m;

    public short? GuzergahId { get; set; }
    public int CikisLokasyonId { get; set; }
    public int VarisLokasyonId { get; set; }

    public DateOnly PlanlananTarih { get; set; }
    public DateTime? CikisTarihi { get; set; }
    public DateTime? TeslimTarihi { get; set; }
    public DateTime? DonusTarihi { get; set; }

    public int? CikisKm { get; set; }
    public int? DonusKm { get; set; }
    /// <summary>Boş (yüksüz) gidilen km. Boş dönüş analizi için.</summary>
    public int? BosKm { get; set; }

    public string? YukTanimi { get; set; }
    public decimal? Tonaj { get; set; }

    public decimal NavlunTutar { get; set; }
    public ParaBirimi NavlunParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal NavlunKur { get; set; } = 1m;
    public byte NavlunKdvOrani { get; set; } = 20;
    public byte NavlunTevkifatPay { get; set; } = 2;

    public string? CmrNo { get; set; }
    public string? IrsaliyeNo { get; set; }
    /// <summary>Dönüş yükü olarak bağlı olduğu ana sefer.</summary>
    public int? BagliSeferId { get; set; }
    public string? Notlar { get; set; }

    public Cari? Musteri { get; set; }
    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
    public Cari? Taseron { get; set; }
    public Guzergah? Guzergah { get; set; }
    public Lokasyon? CikisLokasyon { get; set; }
    public Lokasyon? VarisLokasyon { get; set; }
    public List<SeferSofor> Soforler { get; set; } = [];
    public List<SeferDurak> Duraklar { get; set; } = [];

    public bool TaseronSeferi => TaseronCariId is not null;
    public int? ToplamKm => CikisKm is not null && DonusKm is not null ? DonusKm - CikisKm : null;
}

public class SeferSofor
{
    public int SeferId { get; set; }
    public short SoforId { get; set; }
    public bool Birincil { get; set; }

    public Sefer? Sefer { get; set; }
    public Sofor? Sofor { get; set; }
}

public class SeferDurak
{
    public int Id { get; set; }
    public int SeferId { get; set; }
    public byte Sira { get; set; }
    public int LokasyonId { get; set; }
    public DurakTipi DurakTipi { get; set; }
    public DateTime? PlanlananTarih { get; set; }
    public DateTime? GerceklesenTarih { get; set; }
    public string? Aciklama { get; set; }

    public Lokasyon? Lokasyon { get; set; }
}

/// <summary>
/// Genel gider kaydı. Tutar KDV dahil, kendi para biriminde. Maliyet merkezi zorunludur.
/// Şoför nakit ödemesi şoför carisine alacak yazar ve mahsupta kapanır.
/// </summary>
public class Masraf : Kayit<int>, IDonemKilitli
{
    public DateOnly Tarih { get; set; }
    public short MasrafTuruId { get; set; }
    public MasrafMerkezi Merkez { get; set; }
    public int? SeferId { get; set; }
    public short? AracId { get; set; }
    public short? DorseId { get; set; }
    public short? SoforId { get; set; }
    public decimal Tutar { get; set; }
    /// <summary>İndirilebilir KDV oranı (fişsiz/KDV'siz için 0).</summary>
    public byte KdvOrani { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal Kur { get; set; } = 1m;
    public OdemeSekli OdemeSekli { get; set; } = OdemeSekli.Kasa;
    public byte? KasaHesapId { get; set; }
    public int? CariId { get; set; }
    public string? BelgeNo { get; set; }
    public string? Aciklama { get; set; }
    /// <summary>Şoför harcamaları onaylanmadan mahsuba giremez.</summary>
    public bool Onayli { get; set; } = true;
    public int? MahsupId { get; set; }
    public bool Iptal { get; set; }

    public MasrafTuru? MasrafTuru { get; set; }
    public Sefer? Sefer { get; set; }
    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
    public Sofor? Sofor { get; set; }
}

public class Ceza : Kayit<int>, IDonemKilitli
{
    public DateOnly Tarih { get; set; }
    public short? AracId { get; set; }
    public short? SoforId { get; set; }
    public int? SeferId { get; set; }
    public string CezaTuru { get; set; } = "";
    public string? TutanakNo { get; set; }
    public decimal Tutar { get; set; }
    public decimal? IndirimliTutar { get; set; }
    public DateOnly? TebligTarihi { get; set; }
    public DateOnly? OdemeTarihi { get; set; }
    public byte? OdemeKasaHesapId { get; set; }
    /// <summary>Ceza şoföre rücu edilecek mi (şoför carisine borç yazılır).</summary>
    public bool RucuEdilsin { get; set; }
    public string? Aciklama { get; set; }

    public Arac? Arac { get; set; }
    public Sofor? Sofor { get; set; }
}

public class Avans : Kayit<int>, IDonemKilitli
{
    public short SoforId { get; set; }
    public int? SeferId { get; set; }
    public DateOnly Tarih { get; set; }
    public decimal Tutar { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal Kur { get; set; } = 1m;
    public byte KasaHesapId { get; set; }
    public string? Aciklama { get; set; }
    public int? MahsupId { get; set; }
    public bool Iptal { get; set; }

    public Sofor? Sofor { get; set; }
    public Sefer? Sefer { get; set; }
}

public class Harcirah : Kayit<int>, IDonemKilitli
{
    public short SoforId { get; set; }
    public int? SeferId { get; set; }
    public short? UlkeId { get; set; }
    public DateOnly Baslangic { get; set; }
    public DateOnly Bitis { get; set; }
    public decimal GunSayisi { get; set; }
    public decimal GunlukTutar { get; set; }
    public decimal Tutar { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal Kur { get; set; } = 1m;
    public string? Aciklama { get; set; }
    public int? MahsupId { get; set; }
    public bool Iptal { get; set; }

    public DateOnly Tarih => Bitis;

    public Sofor? Sofor { get; set; }
    public Sefer? Sefer { get; set; }
}

/// <summary>Şoför hesap kapama: avanslar − (onaylı harcamalar + harcırah). Tutarlar TL.</summary>
public class Mahsup : Kayit<int>, IDonemKilitli
{
    public short SoforId { get; set; }
    public int? SeferId { get; set; }
    public DateOnly Tarih { get; set; }
    public decimal AvansToplam { get; set; }
    public decimal HarcamaToplam { get; set; }
    public decimal HarcirahToplam { get; set; }
    /// <summary>Pozitif: şoför şirkete borçlu. Negatif: şirket şoföre borçlu.</summary>
    public decimal Fark { get; set; }
    public MahsupSonuc Sonuc { get; set; }
    public byte? KasaHesapId { get; set; }
    public string? Aciklama { get; set; }

    public Sofor? Sofor { get; set; }
    public Sefer? Sefer { get; set; }
}

public class Yakit : Kayit<int>, IDonemKilitli
{
    public short AracId { get; set; }
    public int? SeferId { get; set; }
    public short? SoforId { get; set; }
    public DateTime AlimZamani { get; set; }
    public int Km { get; set; }
    public decimal Litre { get; set; }
    public decimal BirimFiyat { get; set; }
    /// <summary>KDV dahil toplam, kendi para biriminde.</summary>
    public decimal Tutar { get; set; }
    public byte KdvOrani { get; set; } = 20;
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal Kur { get; set; } = 1m;
    /// <summary>Depo tamamen dolduruldu mu (L/100km hesabı depo-dolu yöntemiyle yapılır).</summary>
    public bool FullDepo { get; set; } = true;
    public int? IstasyonCariId { get; set; }
    public string? IstasyonAdi { get; set; }
    public OdemeSekli OdemeSekli { get; set; } = OdemeSekli.Kart;
    public byte? KasaHesapId { get; set; }
    public string? KartNo { get; set; }
    public string? FisNo { get; set; }
    public int? IceAktarimId { get; set; }
    public int? MahsupId { get; set; }
    public bool Iptal { get; set; }

    public DateOnly Tarih => DateOnly.FromDateTime(AlimZamani);

    public Arac? Arac { get; set; }
    public Sefer? Sefer { get; set; }
    public Sofor? Sofor { get; set; }
}

public class HgsGecis : IOlusturmaBilgisi
{
    public int Id { get; set; }
    public short AracId { get; set; }
    public DateTime GecisZamani { get; set; }
    public string? GirisYeri { get; set; }
    public string? CikisYeri { get; set; }
    public decimal Tutar { get; set; }
    public int? SeferId { get; set; }
    public int? IceAktarimId { get; set; }
    public short? OlusturanId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public Arac? Arac { get; set; }
    public Sefer? Sefer { get; set; }
}

public class IceAktarim : IOlusturmaBilgisi
{
    public int Id { get; set; }
    public IceAktarimTur Tur { get; set; }
    public short? SablonId { get; set; }
    public string DosyaAdi { get; set; } = "";
    /// <summary>Ekstreyi gönderen firma (akaryakıt şirketi / HGS bankası). Toplam tutar bu cariye alacak yazılır.</summary>
    public int? CariId { get; set; }
    public int ToplamSatir { get; set; }
    public int BasariliSatir { get; set; }
    public int HataliSatir { get; set; }
    public string? Hatalar { get; set; }
    public short? OlusturanId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}

/// <summary>Excel ekstre kolon eşlemesi (Opet, Shell, PO, HGS ...).</summary>
public class IceAktarimSablon
{
    public short Id { get; set; }
    public IceAktarimTur Tur { get; set; }
    public string Ad { get; set; } = "";
    /// <summary>Başlık satırı (1 tabanlı). Veri bir alt satırdan başlar.</summary>
    public byte BaslikSatiri { get; set; } = 1;
    /// <summary>{"Plaka":"Plaka","Tarih":"İşlem Tarihi",...} — alan adı → Excel başlığı.</summary>
    public string KolonEsleme { get; set; } = "{}";
    public bool Aktif { get; set; } = true;
}
