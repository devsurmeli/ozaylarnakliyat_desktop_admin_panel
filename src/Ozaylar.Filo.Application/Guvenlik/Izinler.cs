using System.Reflection;

namespace Ozaylar.Filo.Application.Guvenlik;

/// <summary>
/// İzin kodları. Veritabanındaki Izin tablosu bu sabitlerden seed edilir; yeni izin eklemek için buraya sabit ekleyin.
/// Kural: "Modul.Gor" listeleme/görüntüleme, "Modul.Yaz" ekleme/düzenleme, "Modul.Sil" silme/iptal.
/// </summary>
public static class Izinler
{
    [IzinBilgi("Tanımlar", "Tanımları görüntüle")] public const string TanimGor = "Tanim.Gor";
    [IzinBilgi("Tanımlar", "Tanımları düzenle")] public const string TanimYaz = "Tanim.Yaz";

    [IzinBilgi("Filo", "Araç/dorse/şoför görüntüle")] public const string FiloGor = "Filo.Gor";
    [IzinBilgi("Filo", "Araç/dorse/şoför düzenle")] public const string FiloYaz = "Filo.Yaz";

    [IzinBilgi("Sefer", "Seferleri görüntüle")] public const string SeferGor = "Sefer.Gor";
    [IzinBilgi("Sefer", "Sefer aç/düzenle")] public const string SeferYaz = "Sefer.Yaz";
    [IzinBilgi("Sefer", "Navlun/fiyat bilgilerini gör")] public const string SeferFiyatGor = "Sefer.FiyatGor";
    [IzinBilgi("Sefer", "Kapatılmış seferi yeniden aç")] public const string SeferYenidenAc = "Sefer.YenidenAc";

    [IzinBilgi("Finans", "Cari/kasa/fatura görüntüle")] public const string FinansGor = "Finans.Gor";
    [IzinBilgi("Finans", "Cari/kasa/fatura işlemleri")] public const string FinansYaz = "Finans.Yaz";
    [IzinBilgi("Finans", "Finansal kayıt iptali")] public const string FinansIptal = "Finans.Iptal";

    [IzinBilgi("Şoför Hesap", "Avans/harcırah/mahsup görüntüle")] public const string SoforHesapGor = "SoforHesap.Gor";
    [IzinBilgi("Şoför Hesap", "Avans/harcırah/harcama gir")] public const string SoforHesapYaz = "SoforHesap.Yaz";
    [IzinBilgi("Şoför Hesap", "Harcama onayı ve mahsup")] public const string SoforHesapOnay = "SoforHesap.Onay";

    [IzinBilgi("Gider", "Masraf/yakıt/HGS/ceza görüntüle")] public const string GiderGor = "Gider.Gor";
    [IzinBilgi("Gider", "Masraf/yakıt/HGS/ceza gir")] public const string GiderYaz = "Gider.Yaz";
    [IzinBilgi("Gider", "Ekstre içe aktar")] public const string IceAktar = "Gider.IceAktar";

    [IzinBilgi("Bakım/Evrak", "Evrak/bakım/lastik görüntüle")] public const string BakimGor = "Bakim.Gor";
    [IzinBilgi("Bakım/Evrak", "Evrak/bakım/lastik düzenle")] public const string BakimYaz = "Bakim.Yaz";

    [IzinBilgi("Finansman", "Kredi/leasing görüntüle")] public const string FinansmanGor = "Finansman.Gor";
    [IzinBilgi("Finansman", "Kredi/leasing düzenle")] public const string FinansmanYaz = "Finansman.Yaz";

    [IzinBilgi("Rapor", "Operasyon raporları")] public const string RaporOperasyon = "Rapor.Operasyon";
    [IzinBilgi("Rapor", "Kârlılık ve finans raporları")] public const string RaporFinans = "Rapor.Finans";

    [IzinBilgi("Yönetim", "Kullanıcı ve rol yönetimi")] public const string KullaniciYonet = "Yonetim.Kullanici";
    [IzinBilgi("Yönetim", "Sistem ayarları, dönem kilidi")] public const string AyarYonet = "Yonetim.Ayar";
    [IzinBilgi("Yönetim", "Denetim kaydı (audit) görüntüle")] public const string AuditGor = "Yonetim.Audit";

    public static IReadOnlyList<(string Kod, string Grup, string Ad)> Tumu { get; } =
        typeof(Izinler).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => ((string)f.GetRawConstantValue()!, f.GetCustomAttribute<IzinBilgiAttribute>()!))
            .Select(x => (x.Item1, x.Item2.Grup, x.Item2.Ad))
            .ToList();
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class IzinBilgiAttribute(string grup, string ad) : Attribute
{
    public string Grup { get; } = grup;
    public string Ad { get; } = ad;
}

public interface IKullaniciOturumu
{
    short? KullaniciId { get; }
    string? AdSoyad { get; }
    bool GirisYapildi { get; }
    bool Yetkili(string izinKodu);
    void Baslat(short kullaniciId, string adSoyad, IEnumerable<string> izinler);
    void Kapat();
}

public sealed class KullaniciOturumu : IKullaniciOturumu
{
    private HashSet<string> _izinler = new(StringComparer.Ordinal);

    public short? KullaniciId { get; private set; }
    public string? AdSoyad { get; private set; }
    public bool GirisYapildi => KullaniciId is not null;

    public bool Yetkili(string izinKodu) => _izinler.Contains(izinKodu);

    public void Baslat(short kullaniciId, string adSoyad, IEnumerable<string> izinler)
    {
        KullaniciId = kullaniciId;
        AdSoyad = adSoyad;
        _izinler = new HashSet<string>(izinler, StringComparer.Ordinal);
    }

    public void Kapat()
    {
        KullaniciId = null;
        AdSoyad = null;
        _izinler.Clear();
    }
}

public class YetkiYokException(string izin) : Exception($"Bu işlem için yetkiniz yok ({izin}).");

public static class OturumUzantilari
{
    public static void YetkiGerekli(this IKullaniciOturumu oturum, string izin)
    {
        if (!oturum.Yetkili(izin)) throw new YetkiYokException(izin);
    }
}
