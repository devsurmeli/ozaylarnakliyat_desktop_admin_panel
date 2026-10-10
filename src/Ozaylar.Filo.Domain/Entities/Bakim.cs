using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public class BakimPlani : SilinebilirKayit<short>
{
    public short? AracId { get; set; }
    public short? DorseId { get; set; }
    public string BakimAdi { get; set; } = "";
    public int? PeriyotKm { get; set; }
    public short? PeriyotGun { get; set; }
    public int? SonKm { get; set; }
    public DateOnly? SonTarih { get; set; }
    public bool Aktif { get; set; } = true;

    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }

    public int? SonrakiKm => PeriyotKm is not null && SonKm is not null ? SonKm + PeriyotKm : null;
    public DateOnly? SonrakiTarih => PeriyotGun is not null && SonTarih is not null ? SonTarih.Value.AddDays(PeriyotGun.Value) : null;
}

public class Bakim : Kayit<int>, IDonemKilitli
{
    public short? AracId { get; set; }
    public short? DorseId { get; set; }
    public BakimTuru BakimTuru { get; set; } = BakimTuru.Periyodik;
    public short? BakimPlaniId { get; set; }
    public DateOnly Tarih { get; set; }
    public int? Km { get; set; }
    public int? ServisCariId { get; set; }
    public string? ServisAdi { get; set; }
    public string? Aciklama { get; set; }
    /// <summary>KDV dahil toplam (kalemlerden).</summary>
    public decimal ToplamTutar { get; set; }
    public byte KdvOrani { get; set; } = 20;
    public OdemeSekli OdemeSekli { get; set; } = OdemeSekli.Cari;
    public byte? KasaHesapId { get; set; }
    public string? BelgeNo { get; set; }
    public DateOnly? GarantiBitis { get; set; }
    public bool Iptal { get; set; }

    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
    public Cari? ServisCari { get; set; }
    public List<BakimKalem> Kalemler { get; set; } = [];
}

public class BakimKalem
{
    public int Id { get; set; }
    public int BakimId { get; set; }
    public BakimKalemTipi KalemTipi { get; set; } = BakimKalemTipi.Parca;
    public string Aciklama { get; set; } = "";
    public decimal Miktar { get; set; } = 1m;
    public decimal BirimFiyat { get; set; }
    public decimal Tutar { get; set; }
}

public class Lastik : SilinebilirKayit<int>
{
    public string SeriNo { get; set; } = "";
    public string? Marka { get; set; }
    public string? Desen { get; set; }
    public string Ebat { get; set; } = "";
    public string? DotKodu { get; set; }
    public DateOnly? AlisTarihi { get; set; }
    public decimal? AlisBedeli { get; set; }
    public int? TedarikciCariId { get; set; }
    public LastikDurum Durum { get; set; } = LastikDurum.Depoda;
    public byte KaplamaSayisi { get; set; }
    public DateOnly? HurdaTarihi { get; set; }

    public List<LastikHareket> Hareketler { get; set; } = [];
}

public class LastikHareket : Kayit<int>
{
    public int LastikId { get; set; }
    public short? AracId { get; set; }
    public short? DorseId { get; set; }
    /// <summary>Aks/konum kodu, ör. "1-Sol", "2-Sağ-İç".</summary>
    public string Pozisyon { get; set; } = "";
    public DateOnly TakmaTarihi { get; set; }
    public int TakmaKm { get; set; }
    public DateOnly? SokmeTarihi { get; set; }
    public int? SokmeKm { get; set; }
    public decimal? DisDerinligiMm { get; set; }
    public string? Aciklama { get; set; }

    public Lastik? Lastik { get; set; }
    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
}

public class AracFinansman : SilinebilirKayit<short>
{
    public short? AracId { get; set; }
    public short? DorseId { get; set; }
    public FinansmanTipi Tip { get; set; } = FinansmanTipi.Kredi;
    public int FinansKurumuCariId { get; set; }
    public string? SozlesmeNo { get; set; }
    public DateOnly BaslangicTarihi { get; set; }
    public decimal AnaparaTutar { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    /// <summary>Aylık faiz oranı (%), ör. 3.25</summary>
    public decimal AylikFaizOrani { get; set; }
    public short VadeSayisi { get; set; }
    public string? Aciklama { get; set; }
    public bool Aktif { get; set; } = true;

    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
    public Cari? FinansKurumu { get; set; }
    public List<FinansmanTaksit> Taksitler { get; set; } = [];
}

public class FinansmanTaksit : Kayit<int>
{
    public short AracFinansmanId { get; set; }
    public short TaksitNo { get; set; }
    public DateOnly VadeTarihi { get; set; }
    public decimal Anapara { get; set; }
    public decimal Faiz { get; set; }
    public decimal Toplam { get; set; }
    public DateOnly? OdemeTarihi { get; set; }
    public byte? OdemeKasaHesapId { get; set; }

    public AracFinansman? AracFinansman { get; set; }
}

public class Evrak : SilinebilirKayit<int>
{
    public short EvrakTuruId { get; set; }
    /// <summary>En fazla biri dolu. Hepsi boşsa firma evrakı (K belgesi vb.).</summary>
    public short? AracId { get; set; }
    public short? DorseId { get; set; }
    public short? SoforId { get; set; }
    public string? BelgeNo { get; set; }
    /// <summary>Sigorta şirketi, muayene istasyonu, veren kurum.</summary>
    public string? Kurum { get; set; }
    public DateOnly? DuzenlenmeTarihi { get; set; }
    public DateOnly? BitisTarihi { get; set; }
    public decimal? Tutar { get; set; }
    public string? Aciklama { get; set; }

    public EvrakTuru? EvrakTuru { get; set; }
    public Arac? Arac { get; set; }
    public Dorse? Dorse { get; set; }
    public Sofor? Sofor { get; set; }
}

/// <summary>Dosya eki üst bilgisi. İçerik ayrı veritabanındadır (OzaylarFilo_Ekler.EkIcerik).</summary>
public class Ek : IOlusturmaBilgisi, ISoftDelete
{
    public int Id { get; set; }
    public EkHedef HedefTip { get; set; }
    public int HedefId { get; set; }
    public string DosyaAdi { get; set; } = "";
    public string IcerikTipi { get; set; } = "application/octet-stream";
    public int Boyut { get; set; }
    public byte[] Sha256 { get; set; } = [];
    public int EkIcerikId { get; set; }
    public string? Aciklama { get; set; }
    public bool IsDeleted { get; set; }
    public short? OlusturanId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}

public class EkIcerik
{
    public int Id { get; set; }
    public byte[] Icerik { get; set; } = [];
}
