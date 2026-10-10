using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public class Cari : SilinebilirKayit<int>
{
    public string Kod { get; set; } = "";
    public string Unvan { get; set; } = "";
    public CariTipi CariTipi { get; set; } = CariTipi.Musteri;
    public string? VergiDairesi { get; set; }
    /// <summary>VKN (10) veya TCKN (11).</summary>
    public string? VergiNo { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Adres { get; set; }
    public byte? IlId { get; set; }
    public short? UlkeId { get; set; }
    public string? Yetkili { get; set; }
    public short VadeGun { get; set; }
    public decimal? RiskLimiti { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public bool EFaturaMukellefi { get; set; }
    public string? IBAN { get; set; }
    public bool Aktif { get; set; } = true;
    public string? Notlar { get; set; }

    public Il? Il { get; set; }
    public Ulke? Ulke { get; set; }
}

/// <summary>
/// Cari defter. Borc/Alacak her zaman TL'dir; dövizli işlemde döviz tutarı ayrıca saklanır.
/// Borç = cari bize borçlandı (ör. satış faturası, şoföre avans). Alacak = biz cariye borçlandık / cari ödedi.
/// </summary>
public class CariHareket : DefterKaydi
{
    public int CariId { get; set; }
    public DateOnly? VadeTarihi { get; set; }
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal Kur { get; set; } = 1m;
    public decimal? DovizBorc { get; set; }
    public decimal? DovizAlacak { get; set; }

    public Cari? Cari { get; set; }
}

public class KasaHesap : SilinebilirKayit<byte>
{
    public string Ad { get; set; } = "";
    public KasaHesapTipi HesapTipi { get; set; } = KasaHesapTipi.Kasa;
    public string? BankaAdi { get; set; }
    public string? Sube { get; set; }
    public string? IBAN { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal AcilisBakiye { get; set; }
    public DateOnly AcilisTarihi { get; set; }
    /// <summary>Kredi kartı / KMH gibi eksiye düşebilen hesaplar.</summary>
    public bool EksiBakiyeIzin { get; set; }
    public bool Aktif { get; set; } = true;
}

/// <summary>Kasa/banka defteri. Giris/Cikis hesabın kendi para birimindedir; Kur TL karşılığı içindir.</summary>
public class KasaHareket : DefterKaydi
{
    public byte KasaHesapId { get; set; }
    public decimal Giris { get; set; }
    public decimal Cikis { get; set; }
    public decimal Kur { get; set; } = 1m;
    public int? CariId { get; set; }
    /// <summary>Virmanda karşı hesaptaki hareket.</summary>
    public int? VirmanKarsiHareketId { get; set; }

    public KasaHesap? KasaHesap { get; set; }
    public Cari? Cari { get; set; }
}

/// <summary>Fatura kaydı. Bu sistem faturayı KESMEZ; harici programda kesilen faturanın takibini yapar.</summary>
public class Fatura : Kayit<int>, IDonemKilitli
{
    public FaturaTipi FaturaTipi { get; set; } = FaturaTipi.Satis;
    public string FaturaNo { get; set; } = "";
    public DateOnly Tarih { get; set; }
    public DateOnly? VadeTarihi { get; set; }
    public int CariId { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public decimal Kur { get; set; } = 1m;
    public decimal AraToplam { get; set; }
    public decimal KdvToplam { get; set; }
    public decimal TevkifatToplam { get; set; }
    /// <summary>AraToplam + KDV - Tevkifat (cariye yansıyan tutar, fatura para biriminde).</summary>
    public decimal GenelToplam { get; set; }
    public string? EBelgeNo { get; set; }
    public string? Aciklama { get; set; }
    public FaturaDurum Durum { get; set; } = FaturaDurum.Kayitli;

    public Cari? Cari { get; set; }
    public List<FaturaKalem> Kalemler { get; set; } = [];
}

public class FaturaKalem
{
    public int Id { get; set; }
    public int FaturaId { get; set; }
    public byte SiraNo { get; set; }
    public int? SeferId { get; set; }
    public string Aciklama { get; set; } = "";
    public decimal Miktar { get; set; } = 1m;
    public string? Birim { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IskontoOrani { get; set; }
    public byte KdvOrani { get; set; }
    /// <summary>Tevkifat payı (x/10). Yük taşımacılığında 2.</summary>
    public byte TevkifatPay { get; set; }
    public decimal Tutar { get; set; }
    public decimal KdvTutar { get; set; }
    public decimal TevkifatTutar { get; set; }

    public Fatura? Fatura { get; set; }
    public Sefer? Sefer { get; set; }
}

public class CekSenet : Kayit<int>
{
    public CekSenetTipi Tip { get; set; } = CekSenetTipi.Cek;
    public CekSenetYon Yon { get; set; } = CekSenetYon.Alinan;
    public string PortfoyNo { get; set; } = "";
    public string? SeriNo { get; set; }
    public string? Banka { get; set; }
    public string? Sube { get; set; }
    public string? Kesideci { get; set; }
    /// <summary>Alınan: kimden alındı; Verilen: kime verildi.</summary>
    public int CariId { get; set; }
    public decimal Tutar { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public DateOnly IslemTarihi { get; set; }
    public DateOnly VadeTarihi { get; set; }
    public CekSenetDurum Durum { get; set; } = CekSenetDurum.Portfoyde;
    public string? Aciklama { get; set; }

    public Cari? Cari { get; set; }
    public List<CekSenetHareket> Hareketler { get; set; } = [];
}

public class CekSenetHareket : IOlusturmaBilgisi
{
    public int Id { get; set; }
    public int CekSenetId { get; set; }
    public DateOnly Tarih { get; set; }
    public CekSenetDurum YeniDurum { get; set; }
    public int? CariId { get; set; }
    public byte? KasaHesapId { get; set; }
    public string? Aciklama { get; set; }
    public short? OlusturanId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}
