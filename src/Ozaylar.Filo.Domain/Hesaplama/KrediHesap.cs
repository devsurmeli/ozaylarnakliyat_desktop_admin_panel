using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Domain.Hesaplama;

public sealed record KrediPlanParametre(
    decimal Anapara,
    decimal AylikOran,
    decimal BsmvYuzde,
    decimal KkdfYuzde,
    short VadeSayisi,
    DateOnly IlkVade,
    byte OdemesizAy = 0,
    PlanYontemi Yontem = PlanYontemi.EsitTaksit,
    decimal? BalonTutar = null);

public sealed record KrediPlanSatiri(short No, DateOnly Vade, decimal Anapara, decimal Faiz, decimal Bsmv, decimal Kkdf, decimal Toplam, decimal KalanAnapara);

public sealed record OdemeDagilimi(decimal Gecikme, decimal Bsmv, decimal Kkdf, decimal Faiz, decimal Anapara)
{
    public decimal Toplam => Gecikme + Bsmv + Kkdf + Faiz + Anapara;
}

/// <summary>
/// Türk bankası ödeme planı mantığı: faiz kalan anapara üzerinden, BSMV ve KKDF faiz üzerinden;
/// eşit taksitte efektif oran r' = r × (1 + BSMV + KKDF) ile annüite hesaplanır. Son taksit yuvarlama farkını kapatır.
/// </summary>
public static class KrediHesap
{
    private static decimal Y(decimal d) => Para.Yuvarla(d);

    public static (decimal Faiz, decimal Bsmv, decimal Kkdf) FaizVergi(decimal kalan, decimal aylikOran, decimal bsmvYuzde, decimal kkdfYuzde, decimal gunOrani = 1m)
    {
        var faiz = Y(kalan * aylikOran * gunOrani);
        return (faiz, Y(faiz * bsmvYuzde / 100m), Y(faiz * kkdfYuzde / 100m));
    }

    public static List<KrediPlanSatiri> PlanOlustur(KrediPlanParametre p)
    {
        if (p.Anapara <= 0) throw new DomainException("Anapara sıfırdan büyük olmalıdır.");
        if (p.VadeSayisi <= 0) throw new DomainException("Vade sayısı sıfırdan büyük olmalıdır.");
        if (p.OdemesizAy >= p.VadeSayisi) throw new DomainException("Ödemesiz dönem vade sayısından küçük olmalıdır.");
        if (p.Yontem == PlanYontemi.Manuel) throw new DomainException("Manuel planda ödeme planı otomatik oluşturulmaz; banka planını girin veya içe aktarın.");
        var balon = p.Yontem == PlanYontemi.Balon ? p.BalonTutar ?? 0 : 0;
        if (balon < 0 || balon >= p.Anapara) throw new DomainException("Balon tutarı anaparadan küçük olmalıdır.");

        var r = p.AylikOran;
        var vergiCarpan = 1 + (p.BsmvYuzde + p.KkdfYuzde) / 100m;
        var rEf = r * vergiCarpan;
        var odemeli = p.VadeSayisi - p.OdemesizAy;
        var liste = new List<KrediPlanSatiri>(p.VadeSayisi);
        var kalan = p.Anapara;

        decimal taksit = 0;
        if (p.Yontem is PlanYontemi.EsitTaksit or PlanYontemi.Balon)
        {
            var amortize = p.Anapara - (balon == 0 ? 0 : balon / (decimal)Math.Pow((double)(1 + rEf), odemeli));
            taksit = rEf == 0 ? Y(amortize / odemeli) : Y(amortize * rEf / (1 - (decimal)Math.Pow((double)(1 + rEf), -odemeli)));
        }
        var esitAnapara = Y(p.Anapara / odemeli);

        for (short i = 1; i <= p.VadeSayisi; i++)
        {
            var (faiz, bsmv, kkdf) = FaizVergi(kalan, r, p.BsmvYuzde, p.KkdfYuzde);
            decimal ana;
            if (i <= p.OdemesizAy) ana = 0;
            else if (i == p.VadeSayisi) ana = kalan;
            else if (p.Yontem == PlanYontemi.EsitAnapara) ana = Math.Min(esitAnapara, kalan);
            else ana = Math.Min(Math.Max(0, taksit - faiz - bsmv - kkdf), kalan);
            kalan -= ana;
            liste.Add(new KrediPlanSatiri(i, p.IlkVade.AddMonths(i - 1), ana, faiz, bsmv, kkdf, ana + faiz + bsmv + kkdf, kalan));
        }
        return liste;
    }

    /// <summary>
    /// Ödeme tutarını banka sırasıyla dağıtır: gecikme faizi → BSMV → KKDF → faiz → anapara.
    /// <paramref name="borclar"/> taksitin henüz ödenmemiş bileşenleridir.
    /// </summary>
    public static OdemeDagilimi Dagit(decimal tutar, decimal gecikme, decimal bsmv, decimal kkdf, decimal faiz, decimal anapara)
    {
        if (tutar < 0) throw new DomainException("Ödeme tutarı negatif olamaz.");
        decimal Al(ref decimal kalanTutar, decimal borc)
        {
            var x = Math.Min(kalanTutar, Math.Max(0, borc));
            kalanTutar -= x;
            return x;
        }
        var k = tutar;
        var g = Al(ref k, gecikme);
        var b = Al(ref k, bsmv);
        var kk = Al(ref k, kkdf);
        var f = Al(ref k, faiz);
        var a = Al(ref k, anapara);
        if (k > 0.009m) throw new DomainException($"Ödeme tutarı borçtan {k:N2} fazla.");
        return new OdemeDagilimi(g, b, kk, f, a);
    }

    /// <summary>Gecikme faizi: vadesi geçen tutar × akdi aylık oran × (1 + artış%) × gün/30, üzerine BSMV+KKDF.</summary>
    public static decimal GecikmeFaizi(decimal gecikenTutar, decimal aylikOran, decimal artisYuzde, int gun, decimal bsmvYuzde, decimal kkdfYuzde)
    {
        if (gun <= 0 || gecikenTutar <= 0) return 0;
        var faiz = gecikenTutar * aylikOran * (1 + artisYuzde / 100m) * gun / 30m;
        return Y(faiz * (1 + (bsmvYuzde + kkdfYuzde) / 100m));
    }

    /// <summary>Son vadeden bu yana işlemiş faiz (+vergiler); ara ödeme ve erken kapamada kullanılır.</summary>
    public static (decimal Faiz, decimal Bsmv, decimal Kkdf) IslemisFaiz(decimal kalanAnapara, decimal aylikOran, decimal bsmvYuzde, decimal kkdfYuzde, DateOnly sonVade, DateOnly tarih)
    {
        var gun = Math.Max(0, tarih.DayNumber - sonVade.DayNumber);
        return FaizVergi(kalanAnapara, aylikOran, bsmvYuzde, kkdfYuzde, gun / 30m);
    }

    /// <summary>
    /// Ara ödeme sonrası kalan anaparayla yeni plan: VadeyiKisalt → taksit tutarı korunur, vade sayısı azalır;
    /// TaksitiDusur → kalan vade sayısı korunur, taksit düşer.
    /// </summary>
    public static List<KrediPlanSatiri> YenidenPlanla(decimal kalanAnapara, decimal aylikOran, decimal bsmvYuzde, decimal kkdfYuzde,
        short kalanVade, DateOnly sonrakiVade, decimal mevcutTaksit, YenidenPlanModu mod, short baslangicNo)
    {
        if (kalanAnapara <= 0) return [];
        // Vadeyi kısalt: taksit tutarı aynen korunur, son taksit kalanı kapatır (daha küçük olur).
        var (f0, b0, k0) = FaizVergi(kalanAnapara, aylikOran, bsmvYuzde, kkdfYuzde);
        if (mod == YenidenPlanModu.VadeyiKisalt && mevcutTaksit > f0 + b0 + k0)
        {
            var liste = new List<KrediPlanSatiri>();
            var kalan = kalanAnapara;
            for (short i = 0; i < kalanVade && kalan > 0; i++)
            {
                var (faiz, bsmv, kkdf) = FaizVergi(kalan, aylikOran, bsmvYuzde, kkdfYuzde);
                var ana = i == kalanVade - 1 ? kalan : Math.Min(mevcutTaksit - faiz - bsmv - kkdf, kalan);
                kalan -= ana;
                liste.Add(new KrediPlanSatiri((short)(baslangicNo + i), sonrakiVade.AddMonths(i), ana, faiz, bsmv, kkdf, ana + faiz + bsmv + kkdf, kalan));
            }
            return liste;
        }
        var plan = PlanOlustur(new KrediPlanParametre(kalanAnapara, aylikOran, bsmvYuzde, kkdfYuzde, kalanVade, sonrakiVade));
        return plan.Select(s => s with { No = (short)(baslangicNo + s.No - 1) }).ToList();
    }
}

/// <summary>Tutarı Türkçe yazıya çevirir (makbuzlar için): 1250,50 → "BinİkiYüzElliTürkLirasıElliKuruş".</summary>
public static class TutarYazi
{
    private static readonly string[] Birler = ["", "Bir", "İki", "Üç", "Dört", "Beş", "Altı", "Yedi", "Sekiz", "Dokuz"];
    private static readonly string[] Onlar = ["", "On", "Yirmi", "Otuz", "Kırk", "Elli", "Altmış", "Yetmiş", "Seksen", "Doksan"];
    private static readonly string[] Basamaklar = ["", "Bin", "Milyon", "Milyar", "Trilyon"];

    public static string Yaz(decimal tutar, ParaBirimi pb = ParaBirimi.TRY)
    {
        var (birim, alt) = pb switch
        {
            ParaBirimi.USD => ("ABDDoları", "Sent"),
            ParaBirimi.EUR => ("Avro", "Sent"),
            ParaBirimi.GBP => ("İngilizSterlini", "Peni"),
            _ => ("TürkLirası", "Kuruş"),
        };
        tutar = Math.Round(Math.Abs(tutar), 2, MidpointRounding.AwayFromZero);
        var tam = (long)Math.Truncate(tutar);
        var kurus = (int)Math.Round((tutar - tam) * 100);
        var s = (tam == 0 ? "Sıfır" : Sayi(tam)) + birim;
        if (kurus > 0) s += Sayi(kurus) + alt;
        return s;
    }

    private static string Sayi(long n)
    {
        var parcalar = new List<string>();
        var i = 0;
        while (n > 0)
        {
            var grup = (int)(n % 1000);
            if (grup > 0)
            {
                var yazi = Uclu(grup);
                // "Bir Bin" değil "Bin"
                parcalar.Insert(0, i == 1 && grup == 1 ? "Bin" : yazi + Basamaklar[i]);
            }
            n /= 1000;
            i++;
        }
        return string.Concat(parcalar);
    }

    private static string Uclu(int n)
    {
        var y = n / 100;
        var o = n % 100 / 10;
        var b = n % 10;
        var s = y == 0 ? "" : (y == 1 ? "" : Birler[y]) + "Yüz";
        return s + Onlar[o] + Birler[b];
    }
}
