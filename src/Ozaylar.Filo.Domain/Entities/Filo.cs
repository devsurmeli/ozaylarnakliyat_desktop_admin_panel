using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public class Arac : SilinebilirKayit<short>
{
    public string Plaka { get; set; } = "";
    public AracTipi AracTipi { get; set; } = AracTipi.Cekici;
    public string? Marka { get; set; }
    public string? Model { get; set; }
    public short? ModelYili { get; set; }
    public string? SaseNo { get; set; }
    public string? MotorNo { get; set; }
    public Mulkiyet Mulkiyet { get; set; } = Mulkiyet.Ozmal;
    public DateOnly? AlisTarihi { get; set; }
    public decimal? AlisBedeli { get; set; }
    public short? FaydaliOmurAy { get; set; }
    public decimal? HurdaDegeri { get; set; }
    public short? YakitDepoLitre { get; set; }
    /// <summary>Beklenen tüketim (L/100km). Sapma alarmı bu değere göre hesaplanır.</summary>
    public decimal? BeklenenTuketim { get; set; }
    public string? HgsEtiketNo { get; set; }
    public int GuncelKm { get; set; }
    public bool Aktif { get; set; } = true;
    public DateOnly? SatisTarihi { get; set; }
    public string? Notlar { get; set; }
}

public class Dorse : SilinebilirKayit<short>
{
    public string Plaka { get; set; } = "";
    public DorseTipi DorseTipi { get; set; } = DorseTipi.Tenteli;
    public string? Marka { get; set; }
    public short? ModelYili { get; set; }
    public string? SaseNo { get; set; }
    public decimal? KapasiteTon { get; set; }
    public decimal? HacimM3 { get; set; }
    public Mulkiyet Mulkiyet { get; set; } = Mulkiyet.Ozmal;
    public DateOnly? AlisTarihi { get; set; }
    public decimal? AlisBedeli { get; set; }
    public short? FaydaliOmurAy { get; set; }
    public decimal? HurdaDegeri { get; set; }
    public bool Aktif { get; set; } = true;
    public string? Notlar { get; set; }
}

/// <summary>Çekici-dorse bağlantı geçmişi. Bitis null = halen bağlı.</summary>
public class AracDorseEslesme : Kayit<int>
{
    public short AracId { get; set; }
    public short DorseId { get; set; }
    public DateOnly Baslangic { get; set; }
    public DateOnly? Bitis { get; set; }

    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
}

public class Sofor : SilinebilirKayit<short>
{
    public string TCKN { get; set; } = "";
    public string Ad { get; set; } = "";
    public string Soyad { get; set; } = "";
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public DateOnly? DogumTarihi { get; set; }
    public string? EhliyetSinifi { get; set; }
    public string? EhliyetNo { get; set; }
    public string? IBAN { get; set; }
    public string? Adres { get; set; }
    public DateOnly? IseGiris { get; set; }
    public DateOnly? IstenCikis { get; set; }
    /// <summary>Şoförün personel carisi: avans/harcama/harcırah bakiyesi bu cari üzerinden akar.</summary>
    public int CariId { get; set; }
    public short? VarsayilanAracId { get; set; }
    public bool Aktif { get; set; } = true;
    public string? Notlar { get; set; }

    public Cari? Cari { get; set; }
    public Arac? VarsayilanArac { get; set; }

    public string AdSoyad => $"{Ad} {Soyad}";
}
