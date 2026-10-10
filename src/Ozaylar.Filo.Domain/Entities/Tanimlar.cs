using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public class Ulke
{
    public short Id { get; set; }
    /// <summary>ISO 3166 alpha-2</summary>
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
}

public class Il
{
    /// <summary>Plaka kodu (1-81).</summary>
    public byte Id { get; set; }
    public string Ad { get; set; } = "";
}

public class Lokasyon : SilinebilirKayit<int>
{
    public string Ad { get; set; } = "";
    public short UlkeId { get; set; }
    public byte? IlId { get; set; }
    public string? Ilce { get; set; }
    public string? Adres { get; set; }
    public int? CariId { get; set; }
    public bool Aktif { get; set; } = true;

    public Ulke? Ulke { get; set; }
    public Il? Il { get; set; }
    public Cari? Cari { get; set; }
}

public class Guzergah : SilinebilirKayit<short>
{
    public string Ad { get; set; } = "";
    public int CikisLokasyonId { get; set; }
    public int VarisLokasyonId { get; set; }
    public int StandartKm { get; set; }
    public byte? StandartGun { get; set; }
    public decimal? StandartNavlun { get; set; }
    public ParaBirimi NavlunParaBirimi { get; set; } = ParaBirimi.TRY;
    /// <summary>Tahmini otoyol/köprü/feribot masrafı (TL).</summary>
    public decimal? StandartYolMasrafi { get; set; }
    public bool Aktif { get; set; } = true;

    public Lokasyon? CikisLokasyon { get; set; }
    public Lokasyon? VarisLokasyon { get; set; }
}

public class MasrafTuru
{
    public short Id { get; set; }
    public string Ad { get; set; } = "";
    public MasrafGrubu Grup { get; set; }
    public byte VarsayilanKdv { get; set; }
    /// <summary>Şoförün yolda yapabileceği harcama mı (mahsup ekranında listelenir).</summary>
    public bool SoforHarcamasi { get; set; }
    public bool Aktif { get; set; } = true;
}

public class EvrakTuru
{
    public short Id { get; set; }
    public string Ad { get; set; } = "";
    public EvrakHedef HedefTip { get; set; }
    /// <summary>Bitişe kaç gün kala uyarı başlasın.</summary>
    public short UyariGun { get; set; } = 30;
    /// <summary>Yenileme periyodu (ay). Yeni evrak girilirken bitiş tarihi önerisi için.</summary>
    public short? PeriyotAy { get; set; }
    public bool Zorunlu { get; set; }
    /// <summary>Süresi dolmuşsa sefer ataması engellenir.</summary>
    public bool SeferEngeller { get; set; }
    public bool Aktif { get; set; } = true;
}

public class DovizKur
{
    public int Id { get; set; }
    public DateOnly Tarih { get; set; }
    public ParaBirimi ParaBirimi { get; set; }
    public decimal Alis { get; set; }
    public decimal Satis { get; set; }
}

public class HarcirahTarife : Kayit<short>
{
    /// <summary>Null = yurtiçi.</summary>
    public short? UlkeId { get; set; }
    public decimal GunlukTutar { get; set; }
    public ParaBirimi ParaBirimi { get; set; } = ParaBirimi.TRY;
    public DateOnly GecerlilikBaslangic { get; set; }
    public string? Aciklama { get; set; }

    public Ulke? Ulke { get; set; }
}

public class Ayar
{
    public string Anahtar { get; set; } = "";
    public string? Deger { get; set; }
}
