using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Domain.Hesaplama;

public static class Para
{
    public static decimal Yuvarla(decimal tutar) => Math.Round(tutar, 2, MidpointRounding.AwayFromZero);

    /// <summary>KDV dahil tutardan KDV hariç tutarı bulur.</summary>
    public static decimal KdvHaric(decimal kdvDahil, byte kdvOrani) =>
        kdvOrani == 0 ? kdvDahil : Yuvarla(kdvDahil / (1 + kdvOrani / 100m));

    public static decimal TL(decimal tutar, decimal kur) => Yuvarla(tutar * kur);
}

public static class FaturaHesap
{
    /// <summary>Kalem tutarlarını (net, KDV, tevkifat) hesaplar ve faturanın toplamlarını günceller.</summary>
    public static void Hesapla(Fatura fatura)
    {
        decimal ara = 0, kdv = 0, tevkifat = 0;
        byte sira = 1;
        foreach (var k in fatura.Kalemler)
        {
            k.SiraNo = sira++;
            KalemHesapla(k);
            ara += k.Tutar;
            kdv += k.KdvTutar;
            tevkifat += k.TevkifatTutar;
        }
        fatura.AraToplam = ara;
        fatura.KdvToplam = kdv;
        fatura.TevkifatToplam = tevkifat;
        fatura.GenelToplam = ara + kdv - tevkifat;
    }

    public static void KalemHesapla(FaturaKalem k)
    {
        if (k.TevkifatPay > 10) throw new DomainException("Tevkifat payı 0-10 arasında olmalıdır (x/10).");
        var brut = k.Miktar * k.BirimFiyat;
        k.Tutar = Para.Yuvarla(brut - brut * k.IskontoOrani / 100m);
        k.KdvTutar = Para.Yuvarla(k.Tutar * k.KdvOrani / 100m);
        k.TevkifatTutar = Para.Yuvarla(k.KdvTutar * k.TevkifatPay / 10m);
    }
}

public static class HarcirahHesap
{
    /// <summary>Başlangıç ve bitiş günü dahil gün sayısı.</summary>
    public static int GunSayisi(DateOnly baslangic, DateOnly bitis)
    {
        if (bitis < baslangic) throw new DomainException("Harcırah bitiş tarihi başlangıçtan önce olamaz.");
        return bitis.DayNumber - baslangic.DayNumber + 1;
    }

    public static decimal Tutar(decimal gunSayisi, decimal gunlukTutar) => Para.Yuvarla(gunSayisi * gunlukTutar);
}

public static class MahsupHesap
{
    /// <summary>Pozitif: şoför şirkete borçlu (iade etmeli). Negatif: şirket şoföre borçlu.</summary>
    public static decimal Fark(decimal avansToplam, decimal harcamaToplam, decimal harcirahToplam) =>
        avansToplam - (harcamaToplam + harcirahToplam);
}

public record YakitAlimi(DateTime Zaman, int Km, decimal Litre, bool FullDepo);

public record TuketimAraligi(DateTime Baslangic, DateTime Bitis, int Km, decimal Litre)
{
    public decimal LitreYuzKm => Km <= 0 ? 0 : Math.Round(Litre * 100m / Km, 2);
}

public static class YakitTuketimHesap
{
    /// <summary>
    /// Depo-dolu yöntemi: iki "full depo" alımı arasında katedilen km'de harcanan yakıt =
    /// ilk full'den SONRAKİ tüm alımların (son full dahil) litre toplamı.
    /// </summary>
    public static List<TuketimAraligi> Araliklar(IEnumerable<YakitAlimi> alimlar)
    {
        var sirali = alimlar.OrderBy(a => a.Zaman).ThenBy(a => a.Km).ToList();
        var sonuc = new List<TuketimAraligi>();
        YakitAlimi? oncekiFull = null;
        decimal biriken = 0;
        foreach (var a in sirali)
        {
            if (oncekiFull is null)
            {
                if (a.FullDepo) oncekiFull = a;
                continue;
            }
            biriken += a.Litre;
            if (!a.FullDepo) continue;
            var km = a.Km - oncekiFull.Km;
            if (km > 0) sonuc.Add(new TuketimAraligi(oncekiFull.Zaman, a.Zaman, km, biriken));
            oncekiFull = a;
            biriken = 0;
        }
        return sonuc;
    }

    public static decimal? Ortalama(IEnumerable<TuketimAraligi> araliklar)
    {
        var list = araliklar.ToList();
        var km = list.Sum(a => a.Km);
        return km == 0 ? null : Math.Round(list.Sum(a => a.Litre) * 100m / km, 2);
    }

    /// <summary>Beklenen tüketime göre sapma yüzdesi (pozitif = fazla tüketim).</summary>
    public static decimal SapmaYuzde(decimal gercek, decimal beklenen) =>
        beklenen == 0 ? 0 : Math.Round((gercek - beklenen) * 100m / beklenen, 1);
}

public static class AmortismanHesap
{
    /// <summary>Doğrusal aylık amortisman. Alış tarihinden itibaren FaydaliOmurAy boyunca uygulanır.</summary>
    public static decimal AylikTutar(decimal alisBedeli, decimal? hurdaDegeri, short faydaliOmurAy) =>
        faydaliOmurAy <= 0 ? 0 : Para.Yuvarla((alisBedeli - (hurdaDegeri ?? 0)) / faydaliOmurAy);

    /// <summary>[donemBaslangic, donemBitis] aralığına düşen amortisman tutarı (ay bazında).</summary>
    public static decimal Donem(decimal? alisBedeli, decimal? hurdaDegeri, short? faydaliOmurAy, DateOnly? alisTarihi,
        DateOnly donemBaslangic, DateOnly donemBitis)
    {
        if (alisBedeli is null or <= 0 || faydaliOmurAy is null or <= 0 || alisTarihi is null) return 0;
        var aylik = AylikTutar(alisBedeli.Value, hurdaDegeri, faydaliOmurAy.Value);
        var ilkAy = AyIndeksi(alisTarihi.Value);
        var sonAy = ilkAy + faydaliOmurAy.Value - 1;
        var bas = Math.Max(ilkAy, AyIndeksi(donemBaslangic));
        var bit = Math.Min(sonAy, AyIndeksi(donemBitis));
        return bit < bas ? 0 : aylik * (bit - bas + 1);
    }

    private static int AyIndeksi(DateOnly t) => t.Year * 12 + t.Month - 1;
}


public record YaslandirmaSonuc(decimal VadesiGelmemis, decimal Gun0_30, decimal Gun31_60, decimal Gun61_90, decimal Gun90Ustu)
{
    public decimal Toplam => VadesiGelmemis + Gun0_30 + Gun31_60 + Gun61_90 + Gun90Ustu;
}

public static class Yaslandirma
{
    /// <summary>
    /// FIFO yaşlandırma: alacaklar (ödemeler) en eski borçları kapatır; kalan açık borçlar vadesine göre gruplanır.
    /// </summary>
    public static YaslandirmaSonuc Hesapla(IEnumerable<(DateOnly Vade, decimal Borc, decimal Alacak)> hareketler, DateOnly bugun)
    {
        var liste = hareketler.ToList();
        var odeme = liste.Sum(h => h.Alacak);
        decimal vg = 0, a = 0, b = 0, c = 0, d = 0;
        foreach (var h in liste.Where(h => h.Borc > 0).OrderBy(h => h.Vade))
        {
            var kapanan = Math.Min(odeme, h.Borc);
            odeme -= kapanan;
            var acik = h.Borc - kapanan;
            if (acik <= 0) continue;
            var gun = bugun.DayNumber - h.Vade.DayNumber;
            if (gun < 0) vg += acik;
            else if (gun <= 30) a += acik;
            else if (gun <= 60) b += acik;
            else if (gun <= 90) c += acik;
            else d += acik;
        }
        return new YaslandirmaSonuc(vg, a, b, c, d);
    }
}
