using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Domain.Entities;

public class Kullanici : SilinebilirKayit<short>
{
    public string KullaniciAdi { get; set; } = "";
    public string SifreHash { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string? Eposta { get; set; }
    public bool Aktif { get; set; } = true;
    public bool SifreDegistirmeli { get; set; }
    public DateTime? SonGiris { get; set; }
    public byte BasarisizGiris { get; set; }
    public DateTime? KilitBitis { get; set; }

    public List<KullaniciRol> Roller { get; set; } = [];
}

public class Rol
{
    public byte Id { get; set; }
    public string Ad { get; set; } = "";
    public string? Aciklama { get; set; }
    /// <summary>Sistem rolleri (Yönetici) silinemez.</summary>
    public bool Sistem { get; set; }

    public List<RolIzin> Izinler { get; set; } = [];
}

/// <summary>İzin kodları koddaki sabitlerden seed edilir (bkz. Application.Guvenlik.Izinler).</summary>
public class Izin
{
    public short Id { get; set; }
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public string Grup { get; set; } = "";
}

public class RolIzin
{
    public byte RolId { get; set; }
    public short IzinId { get; set; }
    public Rol? Rol { get; set; }
    public Izin? Izin { get; set; }
}

public class KullaniciRol
{
    public short KullaniciId { get; set; }
    public byte RolId { get; set; }
    public Kullanici? Kullanici { get; set; }
    public Rol? Rol { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Tarih { get; set; }
    public short? KullaniciId { get; set; }
    public string Tablo { get; set; } = "";
    public string KayitId { get; set; } = "";
    public AuditIslem Islem { get; set; }
    /// <summary>Yalnızca değişen alanlar: {"Alan":[eski,yeni]}</summary>
    public string? Degisiklik { get; set; }
}
