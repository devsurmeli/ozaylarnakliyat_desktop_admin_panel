using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Veri;

/// <summary>Sabit başlangıç verileri (migration ile gelir). Kullanıcı/izin/harcırah gibi çalışma zamanı verileri VeritabaniHazirlik'tedir.</summary>
internal static class Seed
{
    public const byte RolYonetici = 1;

    public static void Uygula(ModelBuilder mb)
    {
        mb.Entity<Il>().HasData(Iller.Select((ad, i) => new Il { Id = (byte)(i + 1), Ad = ad }));
        mb.Entity<Ulke>().HasData(Ulkeler.Select((u, i) => new Ulke { Id = (short)(i + 1), Kod = u.Kod, Ad = u.Ad }));

        short m = 1;
        mb.Entity<MasrafTuru>().HasData(
            new MasrafTuru { Id = m++, Ad = "Motorin (Fiş)", Grup = MasrafGrubu.Yakit, VarsayilanKdv = 20, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "AdBlue", Grup = MasrafGrubu.Yakit, VarsayilanKdv = 20, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Otoyol / Köprü", Grup = MasrafGrubu.YolKopru, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Feribot / Ro-Ro", Grup = MasrafGrubu.Feribot, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Yemek", Grup = MasrafGrubu.Yemek, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Konaklama", Grup = MasrafGrubu.Konaklama, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Otopark", Grup = MasrafGrubu.Otopark, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Kantar", Grup = MasrafGrubu.Otopark, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Yükleme / Boşaltma Ücreti", Grup = MasrafGrubu.YuklemeBosaltma, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Gümrük / Sınır Masrafı", Grup = MasrafGrubu.Gumruk, VarsayilanKdv = 0, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Yol Tamiri / Lastikçi", Grup = MasrafGrubu.Bakim, VarsayilanKdv = 20, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Yıkama", Grup = MasrafGrubu.Diger, VarsayilanKdv = 20, SoforHarcamasi = true },
            new MasrafTuru { Id = m++, Ad = "Trafik Sigortası", Grup = MasrafGrubu.Sigorta, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "Kasko", Grup = MasrafGrubu.Sigorta, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "Taşımacı Mali Sorumluluk Sig.", Grup = MasrafGrubu.Sigorta, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "MTV", Grup = MasrafGrubu.Vergi, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "Muayene Ücreti", Grup = MasrafGrubu.Vergi, VarsayilanKdv = 20 },
            new MasrafTuru { Id = m++, Ad = "Takograf Kalibrasyon", Grup = MasrafGrubu.Bakim, VarsayilanKdv = 20 },
            new MasrafTuru { Id = m++, Ad = "Belge / Harç", Grup = MasrafGrubu.Vergi, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "Şoför Maaşı", Grup = MasrafGrubu.Personel, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "SGK Primi", Grup = MasrafGrubu.Personel, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "Sefer Primi", Grup = MasrafGrubu.Personel, VarsayilanKdv = 0 },
            new MasrafTuru { Id = m++, Ad = "Ofis Kira", Grup = MasrafGrubu.Genel, VarsayilanKdv = 20 },
            new MasrafTuru { Id = m++, Ad = "Muhasebe Ücreti", Grup = MasrafGrubu.Genel, VarsayilanKdv = 20 },
            new MasrafTuru { Id = m++, Ad = "Araç Takip (GPS)", Grup = MasrafGrubu.Genel, VarsayilanKdv = 20 },
            new MasrafTuru { Id = m++, Ad = "Diğer", Grup = MasrafGrubu.Diger, VarsayilanKdv = 20, SoforHarcamasi = true });

        short e = 1;
        const EvrakHedef AD = EvrakHedef.Arac | EvrakHedef.Dorse;
        mb.Entity<EvrakTuru>().HasData(
            new EvrakTuru { Id = e++, Ad = "Araç Muayenesi", HedefTip = AD, UyariGun = 30, PeriyotAy = 12, Zorunlu = true, SeferEngeller = true },
            new EvrakTuru { Id = e++, Ad = "Egzoz Emisyon", HedefTip = EvrakHedef.Arac, UyariGun = 30, PeriyotAy = 12, Zorunlu = true },
            new EvrakTuru { Id = e++, Ad = "Trafik Sigortası", HedefTip = AD, UyariGun = 30, PeriyotAy = 12, Zorunlu = true, SeferEngeller = true },
            new EvrakTuru { Id = e++, Ad = "Kasko", HedefTip = AD, UyariGun = 30, PeriyotAy = 12 },
            new EvrakTuru { Id = e++, Ad = "Taşımacı Mali Sorumluluk Sig.", HedefTip = EvrakHedef.Arac | EvrakHedef.Firma, UyariGun = 30, PeriyotAy = 12 },
            new EvrakTuru { Id = e++, Ad = "Takograf Kalibrasyonu", HedefTip = EvrakHedef.Arac, UyariGun = 30, PeriyotAy = 24, Zorunlu = true },
            new EvrakTuru { Id = e++, Ad = "Ruhsat", HedefTip = AD, UyariGun = 0 },
            new EvrakTuru { Id = e++, Ad = "ATP Belgesi (Frigo)", HedefTip = EvrakHedef.Dorse, UyariGun = 60, PeriyotAy = 72 },
            new EvrakTuru { Id = e++, Ad = "K Yetki Belgesi", HedefTip = EvrakHedef.Firma, UyariGun = 60, Zorunlu = true },
            new EvrakTuru { Id = e++, Ad = "Taşıt Kartı", HedefTip = EvrakHedef.Arac, UyariGun = 30 },
            new EvrakTuru { Id = e++, Ad = "Ehliyet", HedefTip = EvrakHedef.Sofor, UyariGun = 60, Zorunlu = true, SeferEngeller = true },
            new EvrakTuru { Id = e++, Ad = "SRC Belgesi", HedefTip = EvrakHedef.Sofor, UyariGun = 60, PeriyotAy = 60, Zorunlu = true, SeferEngeller = true },
            new EvrakTuru { Id = e++, Ad = "Psikoteknik", HedefTip = EvrakHedef.Sofor, UyariGun = 60, PeriyotAy = 60, Zorunlu = true, SeferEngeller = true },
            new EvrakTuru { Id = e++, Ad = "Sağlık Raporu", HedefTip = EvrakHedef.Sofor, UyariGun = 30, PeriyotAy = 12 },
            new EvrakTuru { Id = e++, Ad = "Takograf Şoför Kartı", HedefTip = EvrakHedef.Sofor, UyariGun = 60, PeriyotAy = 60, Zorunlu = true },
            new EvrakTuru { Id = e++, Ad = "Pasaport", HedefTip = EvrakHedef.Sofor, UyariGun = 90 },
            new EvrakTuru { Id = e++, Ad = "Vize", HedefTip = EvrakHedef.Sofor, UyariGun = 30 },
            new EvrakTuru { Id = e++, Ad = "Uluslararası Yeşil Kart Sig.", HedefTip = AD, UyariGun = 30, PeriyotAy = 12 },
            new EvrakTuru { Id = e++, Ad = "TIR Karnesi", HedefTip = EvrakHedef.Arac, UyariGun = 15 });

        mb.Entity<Rol>().HasData(
            new Rol { Id = RolYonetici, Ad = "Yönetici", Aciklama = "Tüm yetkiler", Sistem = true },
            new Rol { Id = 2, Ad = "Muhasebe", Aciklama = "Fatura, cari, kasa, mahsup, raporlar" },
            new Rol { Id = 3, Ad = "Operasyon", Aciklama = "Sefer, araç-şoför ataması, evrak (fiyat göremez)" },
            new Rol { Id = 4, Ad = "Kasiyer", Aciklama = "Avans, masraf ve fiş girişi" });

        mb.Entity<Ayar>().HasData(
            new Ayar { Anahtar = "Firma.Unvan", Deger = "Ozaylar Nakliyat" },
            new Ayar { Anahtar = "Firma.VergiNo", Deger = "" },
            new Ayar { Anahtar = "Sefer.KmSapmaYuzde", Deger = "15" },
            new Ayar { Anahtar = "Yakit.SapmaUyariYuzde", Deger = "10" },
            new Ayar { Anahtar = "Fatura.VarsayilanKdv", Deger = "20" },
            new Ayar { Anahtar = "Fatura.TasimaTevkifatPay", Deger = "2" },
            new Ayar { Anahtar = "Masraf.FissizLimit", Deger = "1000" },
            new Ayar { Anahtar = "Yedek.SonBasarili", Deger = "" },
            new Ayar { Anahtar = FiloDbContext.AyarKapanisTarihi, Deger = "" });

        static string J(Dictionary<string, string> d) => JsonSerializer.Serialize(d);
        mb.Entity<IceAktarimSablon>().HasData(
            new IceAktarimSablon
            {
                Id = 1, Tur = IceAktarimTur.Yakit, Ad = "Genel Akaryakıt Ekstresi", BaslikSatiri = 1,
                KolonEsleme = J(new()
                {
                    ["Plaka"] = "Plaka", ["Tarih"] = "İşlem Tarihi", ["Litre"] = "Miktar", ["BirimFiyat"] = "Birim Fiyat",
                    ["Tutar"] = "Tutar", ["Km"] = "Km", ["Istasyon"] = "İstasyon", ["KartNo"] = "Kart No", ["FisNo"] = "Fiş No",
                }),
            },
            new IceAktarimSablon
            {
                Id = 2, Tur = IceAktarimTur.Hgs, Ad = "Genel HGS Ekstresi", BaslikSatiri = 1,
                KolonEsleme = J(new()
                {
                    ["Plaka"] = "Plaka", ["EtiketNo"] = "Etiket No", ["Tarih"] = "Geçiş Tarihi", ["Giris"] = "Giriş", ["Cikis"] = "Çıkış", ["Tutar"] = "Tutar",
                }),
            });
    }

    private static readonly string[] Iller =
    [
        "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Amasya", "Ankara", "Antalya", "Artvin", "Aydın", "Balıkesir",
        "Bilecik", "Bingöl", "Bitlis", "Bolu", "Burdur", "Bursa", "Çanakkale", "Çankırı", "Çorum", "Denizli",
        "Diyarbakır", "Edirne", "Elazığ", "Erzincan", "Erzurum", "Eskişehir", "Gaziantep", "Giresun", "Gümüşhane", "Hakkari",
        "Hatay", "Isparta", "Mersin", "İstanbul", "İzmir", "Kars", "Kastamonu", "Kayseri", "Kırklareli", "Kırşehir",
        "Kocaeli", "Konya", "Kütahya", "Malatya", "Manisa", "Kahramanmaraş", "Mardin", "Muğla", "Muş", "Nevşehir",
        "Niğde", "Ordu", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop", "Sivas", "Tekirdağ", "Tokat",
        "Trabzon", "Tunceli", "Şanlıurfa", "Uşak", "Van", "Yozgat", "Zonguldak", "Aksaray", "Bayburt", "Karaman",
        "Kırıkkale", "Batman", "Şırnak", "Bartın", "Ardahan", "Iğdır", "Yalova", "Karabük", "Kilis", "Osmaniye", "Düzce",
    ];

    private static readonly (string Kod, string Ad)[] Ulkeler =
    [
        ("TR", "Türkiye"), ("DE", "Almanya"), ("AT", "Avusturya"), ("BE", "Belçika"), ("BG", "Bulgaristan"),
        ("CZ", "Çekya"), ("DK", "Danimarka"), ("FR", "Fransa"), ("NL", "Hollanda"), ("GB", "Birleşik Krallık"),
        ("ES", "İspanya"), ("IT", "İtalya"), ("SE", "İsveç"), ("CH", "İsviçre"), ("HU", "Macaristan"),
        ("PL", "Polonya"), ("PT", "Portekiz"), ("RO", "Romanya"), ("SK", "Slovakya"), ("SI", "Slovenya"),
        ("GR", "Yunanistan"), ("HR", "Hırvatistan"), ("RS", "Sırbistan"), ("MK", "Kuzey Makedonya"), ("BA", "Bosna-Hersek"),
        ("AL", "Arnavutluk"), ("LT", "Litvanya"), ("LV", "Letonya"), ("EE", "Estonya"), ("LU", "Lüksemburg"),
        ("GE", "Gürcistan"), ("AZ", "Azerbaycan"), ("AM", "Ermenistan"), ("IR", "İran"), ("IQ", "Irak"),
        ("SY", "Suriye"), ("RU", "Rusya"), ("UA", "Ukrayna"), ("MD", "Moldova"), ("KZ", "Kazakistan"),
        ("UZ", "Özbekistan"), ("TM", "Türkmenistan"), ("KG", "Kırgızistan"), ("TJ", "Tacikistan"), ("SA", "Suudi Arabistan"),
        ("AE", "Birleşik Arap Emirlikleri"), ("JO", "Ürdün"), ("LB", "Lübnan"), ("IL", "İsrail"), ("EG", "Mısır"),
        ("NO", "Norveç"), ("FI", "Finlandiya"), ("BY", "Belarus"), ("KW", "Kuveyt"), ("QA", "Katar"),
    ];
}
