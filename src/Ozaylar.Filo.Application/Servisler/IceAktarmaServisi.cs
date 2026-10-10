using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Servisler;

/// <summary>Excel/CSV okuyucu (Infrastructure: ClosedXML). Başlık satırındaki metinlere göre satırları döndürür.</summary>
public interface IExcelOkuyucu
{
    List<Dictionary<string, string?>> Oku(Stream dosya, string dosyaAdi, int baslikSatiri);
}

public sealed class OnizlemeSatir
{
    public int SatirNo { get; init; }
    public string? Plaka { get; init; }
    public short? AracId { get; set; }
    public DateTime? Zaman { get; init; }
    public decimal? Litre { get; init; }
    public decimal? BirimFiyat { get; init; }
    public decimal? Tutar { get; init; }
    public int? Km { get; init; }
    public string? Yer { get; init; }
    public string? Yer2 { get; init; }
    public string? KartNo { get; init; }
    public string? FisNo { get; init; }
    public string? Hata { get; set; }
    public bool Mukerrer { get; set; }
    public bool Aktarilacak => Hata is null && !Mukerrer;
    public string Durum => Hata ?? (Mukerrer ? "Daha önce aktarılmış" : "Hazır");
}

public sealed record IceAktarimOnizleme(IceAktarimTur Tur, short SablonId, string DosyaAdi, List<OnizlemeSatir> Satirlar)
{
    public int Hazir => Satirlar.Count(s => s.Aktarilacak);
    public decimal ToplamTutar => Satirlar.Where(s => s.Aktarilacak).Sum(s => s.Tutar ?? 0);
}

public sealed class IceAktarmaServisi(IFiloDbContextFactory factory, IKullaniciOturumu oturum, IExcelOkuyucu okuyucu)
{
    public static readonly string[] YakitAlanlari = ["Plaka", "Tarih", "Saat", "Litre", "BirimFiyat", "Tutar", "Km", "Istasyon", "KartNo", "FisNo"];
    public static readonly string[] HgsAlanlari = ["Plaka", "EtiketNo", "Tarih", "Saat", "Giris", "Cikis", "Tutar"];

    public async Task<List<IceAktarimSablon>> SablonlarAsync(IceAktarimTur tur, CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.IceAktarimSablonlari.AsNoTracking().Where(s => s.Tur == tur && s.Aktif).OrderBy(s => s.Ad).ToListAsync(ct);
    }

    public async Task<short> SablonKaydetAsync(IceAktarimSablon s, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.IceAktar);
        if (string.IsNullOrWhiteSpace(s.Ad)) throw new DomainException("Şablon adı zorunludur.");
        _ = JsonSerializer.Deserialize<Dictionary<string, string>>(s.KolonEsleme);
        await using var db = factory.Olustur();
        if (s.Id == 0) db.IceAktarimSablonlari.Add(s);
        else await db.BirlestirAsync(s, ct);
        await db.KaydetAsync(ct);
        return s.Id;
    }

    public async Task<IceAktarimOnizleme> OnizleAsync(Stream dosya, string dosyaAdi, short sablonId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.IceAktar);
        await using var db = factory.Olustur();
        var sablon = await db.IceAktarimSablonlari.AsNoTracking().SingleAsync(s => s.Id == sablonId, ct);
        var esleme = JsonSerializer.Deserialize<Dictionary<string, string>>(sablon.KolonEsleme) ?? [];
        var satirlar = okuyucu.Oku(dosya, dosyaAdi, sablon.BaslikSatiri);
        if (satirlar.Count == 0) throw new DomainException("Dosyada veri satırı bulunamadı.");
        var basliklar = satirlar[0].Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eksik = esleme.Where(e => !basliklar.Contains(e.Value)).Select(e => $"{e.Key} → '{e.Value}'").ToList();
        var zorunlu = sablon.Tur == IceAktarimTur.Yakit ? new[] { "Tarih", "Litre" } : ["Tarih", "Tutar"];
        foreach (var z in zorunlu)
            if (!esleme.ContainsKey(z) || !basliklar.Contains(esleme[z]))
                throw new DomainException($"Zorunlu '{z}' kolonu dosyada bulunamadı. Eksik eşlemeler: {string.Join(", ", eksik)}. Dosyadaki başlıklar: {string.Join(", ", basliklar)}");

        string? Al(Dictionary<string, string?> r, string alan) =>
            esleme.TryGetValue(alan, out var b) && r.TryGetValue(b, out var v) ? Metin.Temizle(v) : null;

        var araclar = await db.Araclar.AsNoTracking().Select(a => new { a.Id, a.Plaka, a.HgsEtiketNo }).ToListAsync(ct);
        var plakaIdx = araclar.ToDictionary(a => a.Plaka.Replace(" ", ""), a => a.Id, StringComparer.OrdinalIgnoreCase);
        var etiketIdx = araclar.Where(a => a.HgsEtiketNo != null).ToDictionary(a => a.HgsEtiketNo!.Replace(" ", ""), a => a.Id, StringComparer.OrdinalIgnoreCase);

        var sonuc = new List<OnizlemeSatir>();
        var no = sablon.BaslikSatiri;
        foreach (var r in satirlar)
        {
            no++;
            if (r.Values.All(string.IsNullOrWhiteSpace)) continue;
            var zaman = TarihCoz(Al(r, "Tarih"), Al(r, "Saat"));
            var plaka = Al(r, "Plaka");
            var s = new OnizlemeSatir
            {
                SatirNo = no, Plaka = plaka, Zaman = zaman,
                Litre = SayiCoz(Al(r, "Litre")), BirimFiyat = SayiCoz(Al(r, "BirimFiyat")), Tutar = SayiCoz(Al(r, "Tutar")),
                Km = (int?)SayiCoz(Al(r, "Km")), Yer = Al(r, "Istasyon") ?? Al(r, "Giris"), Yer2 = Al(r, "Cikis"),
                KartNo = Al(r, "KartNo"), FisNo = Al(r, "FisNo"),
            };
            if (plaka is not null && plakaIdx.TryGetValue(Metin.Plaka(plaka).Replace(" ", ""), out var aid)) s.AracId = aid;
            else if (Al(r, "EtiketNo") is { } et && etiketIdx.TryGetValue(et.Replace(" ", ""), out var eid)) s.AracId = eid;

            if (s.AracId is null) s.Hata = $"Araç bulunamadı ({plaka ?? Al(r, "EtiketNo") ?? "plaka yok"})";
            else if (zaman is null) s.Hata = "Tarih okunamadı";
            else if (sablon.Tur == IceAktarimTur.Yakit && s.Litre is not > 0) s.Hata = "Litre okunamadı";
            else if (sablon.Tur == IceAktarimTur.Yakit && s.Tutar is not > 0 && s.BirimFiyat is not > 0) s.Hata = "Tutar/birim fiyat okunamadı";
            else if (sablon.Tur == IceAktarimTur.Hgs && s.Tutar is not > 0) s.Hata = "Tutar okunamadı";
            sonuc.Add(s);
        }

        // Mükerrer kontrolü (daha önce aktarılmış satırlar).
        var gecerli = sonuc.Where(s => s.Hata is null).ToList();
        if (gecerli.Count > 0)
        {
            var min = gecerli.Min(s => s.Zaman!.Value);
            var max = gecerli.Max(s => s.Zaman!.Value);
            if (sablon.Tur == IceAktarimTur.Yakit)
            {
                var mevcut = (await db.Yakitlar.AsNoTracking().Where(y => !y.Iptal && y.AlimZamani >= min && y.AlimZamani <= max)
                    .Select(y => new { y.AracId, y.AlimZamani, y.Litre }).ToListAsync(ct)).ToHashSet();
                foreach (var s in gecerli) s.Mukerrer = mevcut.Contains(new { AracId = s.AracId!.Value, AlimZamani = s.Zaman!.Value, Litre = Math.Round(s.Litre!.Value, 2) });
            }
            else
            {
                var mevcut = (await db.HgsGecisleri.AsNoTracking().Where(h => h.GecisZamani >= min && h.GecisZamani <= max)
                    .Select(h => new { h.AracId, h.GecisZamani, h.Tutar }).ToListAsync(ct)).ToHashSet();
                foreach (var s in gecerli) s.Mukerrer = mevcut.Contains(new { AracId = s.AracId!.Value, GecisZamani = s.Zaman!.Value, Tutar = Math.Round(s.Tutar!.Value, 2) });
            }
            // Dosya içi tekrar
            foreach (var g in gecerli.Where(s => !s.Mukerrer).GroupBy(s => (s.AracId, s.Zaman, s.Litre, s.Tutar)).Where(g => g.Count() > 1))
                foreach (var s in g.Skip(1)) s.Mukerrer = true;
        }
        return new IceAktarimOnizleme(sablon.Tur, sablonId, dosyaAdi, sonuc);
    }

    /// <summary>Hazır satırları aktarır. Ekstreyi gönderen cari (akaryakıt şirketi/banka) verilirse toplam tutar o cariye tek satır alacak yazılır.</summary>
    public async Task<IceAktarim> AktarAsync(IceAktarimOnizleme onizleme, int? cariId, CancellationToken ct = default)
    {
        oturum.YetkiGerekli(Izinler.IceAktar);
        var hazir = onizleme.Satirlar.Where(s => s.Aktarilacak).ToList();
        if (hazir.Count == 0) throw new DomainException("Aktarılacak satır yok.");
        await using var db = factory.Olustur();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var kayit = new IceAktarim
        {
            Tur = onizleme.Tur, SablonId = onizleme.SablonId, DosyaAdi = Path.GetFileName(onizleme.DosyaAdi), CariId = cariId,
            ToplamSatir = onizleme.Satirlar.Count, BasariliSatir = hazir.Count, HataliSatir = onizleme.Satirlar.Count - hazir.Count,
            Hatalar = string.Join("\n", onizleme.Satirlar.Where(s => !s.Aktarilacak).Select(s => $"Satır {s.SatirNo}: {s.Durum}")),
        };
        db.IceAktarimlar.Add(kayit);
        await db.KaydetAsync(ct);

        decimal toplam = 0;
        foreach (var s in hazir)
        {
            var aracId = s.AracId!.Value;
            var seferId = await GiderServisi.SeferBulAsync(db, aracId, s.Zaman!.Value, ct);
            if (onizleme.Tur == IceAktarimTur.Yakit)
            {
                var tutar = s.Tutar is > 0 ? s.Tutar.Value : Math.Round(s.Litre!.Value * s.BirimFiyat!.Value, 2);
                var y = new Yakit
                {
                    AracId = aracId, SeferId = seferId, AlimZamani = s.Zaman.Value, Km = s.Km ?? 0, Litre = Math.Round(s.Litre!.Value, 2),
                    BirimFiyat = s.BirimFiyat is > 0 ? Math.Round(s.BirimFiyat.Value, 3) : Math.Round(tutar / s.Litre.Value, 3),
                    Tutar = tutar, FullDepo = true, IstasyonAdi = Kisalt(s.Yer, 100), KartNo = Kisalt(s.KartNo, 30), FisNo = Kisalt(s.FisNo, 30),
                    OdemeSekli = OdemeSekli.Kart, IceAktarimId = kayit.Id,
                };
                db.Yakitlar.Add(y);
                toplam += tutar;
                if (y.Km > 0)
                {
                    var arac = await db.Araclar.SingleAsync(a => a.Id == aracId, ct);
                    if (y.Km > arac.GuncelKm) arac.GuncelKm = y.Km;
                    AracKm.Kaydet(db, aracId, y.AlimZamani, y.Km, KmKaynak.Ekstre, null);
                }
            }
            else
            {
                db.HgsGecisleri.Add(new HgsGecis
                {
                    AracId = aracId, SeferId = seferId, GecisZamani = s.Zaman.Value, Tutar = Math.Round(s.Tutar!.Value, 2),
                    GirisYeri = Kisalt(s.Yer, 60), CikisYeri = Kisalt(s.Yer2, 60), IceAktarimId = kayit.Id,
                });
                toplam += s.Tutar.Value;
            }
        }
        if (cariId is not null && toplam > 0)
        {
            var tarih = DateOnly.FromDateTime(hazir.Max(s => s.Zaman!.Value));
            Defter.Cari(db, cariId.Value, tarih, 0, toplam, KaynakTip.IceAktarim, kayit.Id, $"{onizleme.Tur.Ad()} ekstresi: {kayit.DosyaAdi} ({hazir.Count} satır)");
        }
        await db.KaydetAsync(ct);
        await tx.CommitAsync(ct);
        return kayit;
    }

    public async Task<List<IceAktarim>> GecmisAsync(CancellationToken ct = default)
    {
        await using var db = factory.Olustur();
        return await db.IceAktarimlar.AsNoTracking().OrderByDescending(i => i.Id).Take(200).ToListAsync(ct);
    }

    private static readonly CultureInfo Tr = new("tr-TR");
    private static readonly string[] TarihBicimleri =
    [
        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "d.M.yyyy H:mm:ss", "d.M.yyyy H:mm", "d.M.yyyy",
        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
    ];

    public static DateTime? TarihCoz(string? tarih, string? saat)
    {
        if (tarih is null) return null;
        DateTime t;
        if (double.TryParse(tarih, NumberStyles.Float, CultureInfo.InvariantCulture, out var oa) && oa is > 20000 and < 80000)
            t = DateTime.FromOADate(oa);
        else if (!DateTime.TryParseExact(tarih, TarihBicimleri, Tr, DateTimeStyles.AllowWhiteSpaces, out t)
                 && !DateTime.TryParse(tarih, Tr, DateTimeStyles.AllowWhiteSpaces, out t))
            return null;
        if (saat is not null && t.TimeOfDay == TimeSpan.Zero)
        {
            if (double.TryParse(saat, NumberStyles.Float, CultureInfo.InvariantCulture, out var oas) && oas is >= 0 and < 1)
                t = t.Date + TimeSpan.FromDays(oas);
            else if (TimeSpan.TryParse(saat, Tr, out var ts))
                t = t.Date + ts;
        }
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second);
    }

    /// <summary>
    /// "1.234,56", "1,234.56", "₺ 1.234,56", "350,5" gibi biçimleri çözer. Türkçe ekstre varsayımı:
    /// virgülsüz ve tek noktadan sonra tam 3 hane varsa ("2.500") nokta binlik ayırıcıdır.
    /// </summary>
    public static decimal? SayiCoz(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = new string(s.Where(c => char.IsDigit(c) || c is ',' or '.' or '-').ToArray());
        if (t.Length == 0) return null;
        var sonVirgul = t.LastIndexOf(',');
        var sonNokta = t.LastIndexOf('.');
        if (sonVirgul > sonNokta) t = t.Replace(".", "").Replace(',', '.');
        else if (sonNokta > sonVirgul && sonVirgul >= 0) t = t.Replace(",", "");
        else if (sonNokta >= 0 && (t.Count(c => c == '.') > 1 || t.Length - sonNokta - 1 == 3)) t = t.Replace(".", "");
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string? Kisalt(string? s, int n) => s is { Length: > 0 } && s.Length > n ? s[..n] : s;
}
