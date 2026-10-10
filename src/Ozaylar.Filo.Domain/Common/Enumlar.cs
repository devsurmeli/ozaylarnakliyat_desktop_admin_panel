using System.ComponentModel;

namespace Ozaylar.Filo.Domain.Common;

// Tüm enum'lar tinyint olarak saklanır. Değerler veritabanında kalıcıdır: mevcut değerleri DEĞİŞTİRMEYİN, sadece ekleyin.

public enum ParaBirimi : byte
{
    [Description("TL")] TRY = 1,
    [Description("USD")] USD = 2,
    [Description("EUR")] EUR = 3,
    [Description("GBP")] GBP = 4,
}

public enum AracTipi : byte
{
    [Description("Çekici")] Cekici = 1,
    [Description("Kamyon")] Kamyon = 2,
    [Description("Kamyonet")] Kamyonet = 3,
    [Description("Binek")] Binek = 4,
    [Description("Diğer")] Diger = 9,
}

public enum Mulkiyet : byte
{
    [Description("Özmal")] Ozmal = 1,
    [Description("Kiralık")] Kiralik = 2,
    [Description("Leasing")] Leasing = 3,
}

public enum DorseTipi : byte
{
    [Description("Tenteli")] Tenteli = 1,
    [Description("Frigorifik")] Frigo = 2,
    [Description("Damperli")] Damper = 3,
    [Description("Lowbed")] Lowbed = 4,
    [Description("Silobas")] Silobas = 5,
    [Description("Konteyner Taşıyıcı")] Konteyner = 6,
    [Description("Açık Kasa")] Acik = 7,
    [Description("Tanker")] Tanker = 8,
    [Description("Diğer")] Diger = 9,
}

[Flags]
public enum CariTipi : byte
{
    [Description("Müşteri")] Musteri = 1,
    [Description("Tedarikçi")] Tedarikci = 2,
    [Description("Personel")] Personel = 4,
    [Description("Taşeron")] Taseron = 8,
    [Description("Banka/Finans")] Finans = 16,
}

public enum SeferKapsam : byte
{
    [Description("Yurtiçi")] Yurtici = 1,
    [Description("Yurtdışı")] Yurtdisi = 2,
}

public enum SeferDurum : byte
{
    [Description("Planlandı")] Planlandi = 1,
    [Description("Yüklemede")] Yuklemede = 2,
    [Description("Yolda")] Yolda = 3,
    [Description("Teslim Edildi")] TeslimEdildi = 4,
    [Description("Kapatıldı")] Kapatildi = 5,
    [Description("Faturalandı")] Faturalandi = 6,
    [Description("İptal")] Iptal = 9,
}

public enum DurakTipi : byte
{
    [Description("Yükleme")] Yukleme = 1,
    [Description("Boşaltma")] Bosaltma = 2,
    [Description("Gümrük")] Gumruk = 3,
    [Description("Ara Nokta")] Ara = 4,
}

public enum MasrafMerkezi : byte
{
    [Description("Sefer")] Sefer = 1,
    [Description("Araç")] Arac = 2,
    [Description("Dorse")] Dorse = 3,
    [Description("Genel")] Genel = 4,
}

public enum MasrafGrubu : byte
{
    [Description("Yakıt")] Yakit = 1,
    [Description("Otoyol/Köprü")] YolKopru = 2,
    [Description("Konaklama")] Konaklama = 3,
    [Description("Yemek")] Yemek = 4,
    [Description("Bakım/Onarım")] Bakim = 5,
    [Description("Lastik")] Lastik = 6,
    [Description("Sigorta")] Sigorta = 7,
    [Description("Vergi/Harç")] Vergi = 8,
    [Description("Ceza")] Ceza = 9,
    [Description("Personel")] Personel = 10,
    [Description("Gümrük/Sınır")] Gumruk = 11,
    [Description("Feribot/Liman")] Feribot = 12,
    [Description("Yükleme/Boşaltma")] YuklemeBosaltma = 13,
    [Description("Otopark/Kantar")] Otopark = 14,
    [Description("Genel Gider")] Genel = 15,
    [Description("Diğer")] Diger = 99,
}

public enum OdemeSekli : byte
{
    [Description("Şoför Nakit")] SoforNakit = 1,
    [Description("Kasa/Banka")] Kasa = 2,
    [Description("Cari (Veresiye)")] Cari = 3,
    [Description("Akaryakıt/HGS Kartı")] Kart = 4,
}

/// <summary>Defter satırının hangi belgeden üretildiği.</summary>
public enum KaynakTip : byte
{
    [Description("Açılış")] Acilis = 1,
    [Description("Manuel")] Manuel = 2,
    [Description("Fatura")] Fatura = 3,
    [Description("Tahsilat")] Tahsilat = 4,
    [Description("Ödeme")] Odeme = 5,
    [Description("Virman")] Virman = 6,
    [Description("Avans")] Avans = 7,
    [Description("Masraf")] Masraf = 8,
    [Description("Harcırah")] Harcirah = 9,
    [Description("Mahsup")] Mahsup = 10,
    [Description("Yakıt")] Yakit = 11,
    [Description("HGS")] Hgs = 12,
    [Description("Bakım")] Bakim = 13,
    [Description("Ceza")] Ceza = 14,
    [Description("Çek/Senet")] CekSenet = 15,
    [Description("Finansman Taksiti")] FinansmanTaksit = 16,
    [Description("Taşeron Bedeli")] TaseronBedel = 17,
    [Description("Ters Kayıt")] TersKayit = 18,
    [Description("Ekstre Aktarımı")] IceAktarim = 19,
    [Description("Kredi Kullanımı")] KrediKullanim = 20,
    [Description("Kredi Ödemesi")] KrediOdeme = 21,
    [Description("Şoför Tahsilatı")] SoforTahsilat = 22,
    [Description("Hakediş")] Hakedis = 23,
}

public enum FaturaTipi : byte
{
    [Description("Satış")] Satis = 1,
    [Description("Alış")] Alis = 2,
    [Description("Satış İade")] SatisIade = 3,
    [Description("Alış İade")] AlisIade = 4,
}

public enum FaturaDurum : byte
{
    [Description("Kayıtlı")] Kayitli = 1,
    [Description("İptal")] Iptal = 9,
}

public enum KasaHesapTipi : byte
{
    [Description("Kasa")] Kasa = 1,
    [Description("Banka")] Banka = 2,
    [Description("Kredi Kartı")] KrediKarti = 3,
}

[Flags]
public enum EvrakHedef : byte
{
    [Description("Araç")] Arac = 1,
    [Description("Dorse")] Dorse = 2,
    [Description("Şoför")] Sofor = 4,
    [Description("Firma")] Firma = 8,
}

public enum BakimTuru : byte
{
    [Description("Periyodik Bakım")] Periyodik = 1,
    [Description("Arıza/Onarım")] Ariza = 2,
    [Description("Kaza/Hasar")] Kaza = 3,
    [Description("Muayene Hazırlık")] Muayene = 4,
    [Description("Diğer")] Diger = 9,
}

public enum BakimKalemTipi : byte
{
    [Description("Parça")] Parca = 1,
    [Description("İşçilik")] Iscilik = 2,
    [Description("Diğer")] Diger = 9,
}

public enum LastikDurum : byte
{
    [Description("Depoda")] Depoda = 1,
    [Description("Takılı")] Takili = 2,
    [Description("Kaplamada")] Kaplamada = 3,
    [Description("Hurda")] Hurda = 9,
}


public enum IceAktarimTur : byte
{
    [Description("Akaryakıt")] Yakit = 1,
    [Description("HGS/OGS")] Hgs = 2,
}

public enum CekSenetTipi : byte
{
    [Description("Çek")] Cek = 1,
    [Description("Senet")] Senet = 2,
}

public enum CekSenetYon : byte
{
    [Description("Alınan")] Alinan = 1,
    [Description("Verilen")] Verilen = 2,
}

public enum CekSenetDurum : byte
{
    [Description("Portföyde")] Portfoyde = 1,
    [Description("Ciro Edildi")] CiroEdildi = 2,
    [Description("Tahsile Verildi")] TahsileVerildi = 3,
    [Description("Tahsil Edildi")] TahsilEdildi = 4,
    [Description("Ödendi")] Odendi = 5,
    [Description("Karşılıksız")] Karsiliksiz = 6,
    [Description("İade")] Iade = 7,
}

public enum MahsupSonuc : byte
{
    [Description("Fark Yok")] FarkYok = 0,
    [Description("Şoförden İade Alındı")] IadeAlindi = 1,
    [Description("Şoföre Ödendi")] SoforeOdendi = 2,
    [Description("Devredildi")] Devredildi = 3,
}

public enum EkHedef : byte
{
    [Description("Araç")] Arac = 1,
    [Description("Dorse")] Dorse = 2,
    [Description("Şoför")] Sofor = 3,
    [Description("Cari")] Cari = 4,
    [Description("Sefer")] Sefer = 5,
    [Description("Masraf")] Masraf = 6,
    [Description("Yakıt")] Yakit = 7,
    [Description("Evrak")] Evrak = 8,
    [Description("Fatura")] Fatura = 9,
    [Description("Bakım")] Bakim = 10,
    [Description("Avans")] Avans = 11,
    [Description("Ceza")] Ceza = 12,
    [Description("Kredi")] Finansman = 13,
    [Description("Zimmet")] Zimmet = 14,
}

public enum AuditIslem : byte
{
    Ekle = 1,
    Guncelle = 2,
    Sil = 3,
}
