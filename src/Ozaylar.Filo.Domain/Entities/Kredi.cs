using System.ComponentModel;
using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public enum KrediTuru : byte
{
    [Description("Taşıt Kredisi")] TasitKredisi = 1,
    [Description("Leasing")] Leasing = 2,
    [Description("Ticari / Spot Kredi")] Ticari = 3,
    [Description("Rotatif / KMH")] Rotatif = 4,
    [Description("Kredi Kartı Borcu")] KrediKarti = 5,
    [Description("Dövize Endeksli")] DovizeEndeksli = 6,
}

public enum FaizTipi : byte
{
    [Description("Aylık %")] Aylik = 1,
    [Description("Yıllık %")] Yillik = 2,
}

public enum PlanYontemi : byte
{
    [Description("Eşit Taksit")] EsitTaksit = 1,
    [Description("Eşit Anapara")] EsitAnapara = 2,
    [Description("Balon Ödemeli")] Balon = 3,
    [Description("Manuel / Banka Planı")] Manuel = 4,
}

public enum KrediAmaci : byte
{
    [Description("Araç Finansmanı")] Arac = 1,
    [Description("İşletme")] Isletme = 2,
}

public enum KrediDurum : byte
{
    [Description("Aktif")] Aktif = 1,
    [Description("Kapandı")] Kapandi = 2,
}

public enum TaksitDurum : byte
{
    [Description("Bekliyor")] Bekliyor = 1,
    [Description("Kısmi Ödendi")] Kismi = 2,
    [Description("Ödendi")] Odendi = 3,
    [Description("Kapatıldı (erken)")] Kapatildi = 4,
    [Description("Plan Değişti")] PlanDegisti = 5,
}

public enum KrediOdemeTuru : byte
{
    [Description("Taksit")] Taksit = 1,
    [Description("Ara (Kısmi) Ödeme")] KismiOdeme = 2,
    [Description("Erken Kapama")] ErkenKapama = 3,
    [Description("Serbest Ödeme")] Serbest = 4,
}

public enum YenidenPlanModu : byte
{
    [Description("Vadeyi kısalt (taksit aynı)")] VadeyiKisalt = 1,
    [Description("Taksiti düşür (vade aynı)")] TaksitiDusur = 2,
}

/// <summary>
/// Kredi / leasing / rotatif sözleşmesi. Tutarlar kredinin para biriminde.
/// Kalan anapara = Anapara − ödemelerdeki anapara toplamı (plan değil, gerçekleşen ödemeler esas).
/// </summary>
public class Kredi : SilinebilirKayit<short>
{
    public string KrediNo { get; set; } = "";
    public KrediTuru Tur { get; set; } = KrediTuru.TasitKredisi;
    public int BankaCariId { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public DateOnly KullandirimTarihi { get; set; }
    /// <summary>Kredi tutarının yattığı hesap. Leasing/araç finansmanında boş olabilir (para doğrudan satıcıya gider).</summary>
    public byte? KullandirimKasaHesapId { get; set; }
    public decimal KullandirimKur { get; set; } = 1m;
    public decimal Anapara { get; set; }
    public decimal FaizOrani { get; set; }
    public FaizTipi FaizTipi { get; set; } = FaizTipi.Aylik;
    public decimal BsmvOrani { get; set; } = 5m;
    public decimal KkdfOrani { get; set; }
    public decimal DosyaMasrafi { get; set; }
    public short VadeSayisi { get; set; }
    public DateOnly IlkVade { get; set; }
    public byte OdemesizAy { get; set; }
    public PlanYontemi Yontem { get; set; } = PlanYontemi.EsitTaksit;
    public decimal? BalonTutar { get; set; }
    /// <summary>Gecikme faizi: akdi faiz oranının yüzde kaç fazlası (bankalarda genelde %30).</summary>
    public decimal GecikmeArtisYuzde { get; set; } = 30m;
    public KrediAmaci Amac { get; set; } = KrediAmaci.Arac;
    public KrediDurum Durum { get; set; } = KrediDurum.Aktif;
    public byte AktifVersiyon { get; set; } = 1;
    public DateOnly? KapanisTarihi { get; set; }
    public string? Aciklama { get; set; }

    public Cari? Banka { get; set; }
    public List<KrediTaksit> Taksitler { get; set; } = [];
    public List<KrediArac> Araclar { get; set; } = [];

    /// <summary>Aylık faiz oranı (ondalık, ör. 0,025).</summary>
    public decimal AylikOran => FaizTipi == FaizTipi.Aylik ? FaizOrani / 100m : FaizOrani / 1200m;
}

public class KrediTaksit : Kayit<int>
{
    public short KrediId { get; set; }
    public byte Versiyon { get; set; } = 1;
    public short TaksitNo { get; set; }
    public DateOnly VadeTarihi { get; set; }
    public decimal Anapara { get; set; }
    public decimal Faiz { get; set; }
    public decimal Bsmv { get; set; }
    public decimal Kkdf { get; set; }
    public decimal Toplam { get; set; }
    public decimal KalanAnapara { get; set; }
    public decimal OdenenTutar { get; set; }
    public TaksitDurum Durum { get; set; } = TaksitDurum.Bekliyor;

    public Kredi? Kredi { get; set; }
    public decimal KalanTutar => Math.Max(0, Toplam - OdenenTutar);
}

/// <summary>Krediye yapılan her ödeme; anapara/faiz/vergi ayrımı ile. Raporlar faiz giderini buradan alır.</summary>
public class KrediOdeme : IOlusturmaBilgisi, IDonemKilitli
{
    public int Id { get; set; }
    public short KrediId { get; set; }
    public int? KrediTaksitId { get; set; }
    public DateOnly Tarih { get; set; }
    public KrediOdemeTuru Tur { get; set; } = KrediOdemeTuru.Taksit;
    public byte KasaHesapId { get; set; }
    /// <summary>Kredi para biriminde toplam ödeme.</summary>
    public decimal Tutar { get; set; }
    /// <summary>Kredi PB → TL kuru (ödeme günü).</summary>
    public decimal Kur { get; set; } = 1m;
    /// <summary>Kasadan çıkan tutar (kasanın para biriminde).</summary>
    public decimal KasaTutar { get; set; }
    public decimal Anapara { get; set; }
    public decimal Faiz { get; set; }
    public decimal Bsmv { get; set; }
    public decimal Kkdf { get; set; }
    public decimal GecikmeFaizi { get; set; }
    public decimal Komisyon { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }
    public short? OlusturanId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public Kredi? Kredi { get; set; }
    /// <summary>Faiz + vergiler + gecikme + komisyon (finansman gideri).</summary>
    public decimal FinansmanGideri => Faiz + Bsmv + Kkdf + GecikmeFaizi + Komisyon;
}

/// <summary>Kredinin finanse ettiği araç/dorse ve payı (bir kredi birden çok aracı finanse edebilir).</summary>
public class KrediArac
{
    public int Id { get; set; }
    public short KrediId { get; set; }
    public short? AracId { get; set; }
    public short? DorseId { get; set; }
    public decimal PayYuzde { get; set; } = 100m;

    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
}

public class BankaLimit : Kayit<short>
{
    public int BankaCariId { get; set; }
    public decimal LimitTutar { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public string? Teminat { get; set; }
    public DateOnly? GecerlilikBitis { get; set; }

    public Cari? Banka { get; set; }
}
