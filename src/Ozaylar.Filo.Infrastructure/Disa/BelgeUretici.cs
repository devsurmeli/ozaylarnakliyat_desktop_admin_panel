using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Domain.Hesaplama;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ozaylar.Filo.Infrastructure.Disa;

/// <summary>QuestPDF belge şablonları: ortak firma başlığı, alt bilgi, imza blokları.</summary>
public sealed class BelgeUretici(IFiloDbContextFactory factory) : IBelgeUretici
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private const string Mavi = "#1F4E79";

    static BelgeUretici() => QuestPDF.Settings.License = LicenseType.Community;

    private (string Unvan, string? VergiNo) Firma()
    {
        using var db = factory.Olustur();
        var a = db.Ayarlar.AsNoTracking().Where(x => x.Anahtar == "Firma.Unvan" || x.Anahtar == "Firma.VergiNo").ToDictionary(x => x.Anahtar, x => x.Deger);
        return (a.GetValueOrDefault("Firma.Unvan") ?? "Firma", a.GetValueOrDefault("Firma.VergiNo"));
    }

    private static string N2(decimal d) => d.ToString("N2", Tr);

    private void Baslik(IContainer c, string belgeAdi, string? sagUst = null)
    {
        var (unvan, vkn) = Firma();
        c.Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(x =>
                {
                    x.Item().Text(unvan).Bold().FontSize(13).FontColor(Mavi);
                    if (!string.IsNullOrWhiteSpace(vkn)) x.Item().Text($"VKN: {vkn}").FontSize(8);
                });
                r.ConstantItem(220).AlignRight().Column(x =>
                {
                    x.Item().AlignRight().Text(belgeAdi).Bold().FontSize(14);
                    if (sagUst is not null) x.Item().AlignRight().Text(sagUst).FontSize(9);
                });
            });
            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Mavi);
        });
    }

    private static void Alt(IContainer c) => c.Row(r =>
    {
        r.RelativeItem().Text($"Düzenlenme: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Darken1);
        r.RelativeItem().AlignRight().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Darken1));
            t.Span("Sayfa ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    });

    private static void Imzalar(IContainer c, params string[] basliklar) => c.PaddingTop(25).Row(r =>
    {
        foreach (var b in basliklar)
            r.RelativeItem().PaddingHorizontal(10).Column(x =>
            {
                x.Item().AlignCenter().Text(b).SemiBold();
                x.Item().PaddingTop(35).LineHorizontal(0.5f);
                x.Item().AlignCenter().Text("Ad Soyad / İmza").FontSize(7).FontColor(Colors.Grey.Darken1);
            });
    });

    private static void Satir(TableDescriptor t, string etiket, string deger)
    {
        t.Cell().PaddingVertical(3).Text(etiket).SemiBold();
        t.Cell().PaddingVertical(3).Text(deger);
    }

    // ------------------------------------------------------------------ Makbuz

    public byte[] Makbuz(MakbuzVeri v)
    {
        var (ad, karsiEtiket, imza1, imza2) = v.Tur switch
        {
            MakbuzTuru.Avans => ("AVANS MAKBUZU", "Teslim alan (şoför)", "Teslim Eden", "Teslim Alan"),
            MakbuzTuru.Tahsilat => ("TAHSİLAT MAKBUZU", "Ödeyen", "Ödeyen", "Tahsil Eden"),
            _ => ("TEDİYE MAKBUZU", "Ödenen", "Ödeyen", "Teslim Alan"),
        };
        return Document.Create(doc =>
        {
            foreach (var nusha in new[] { "ASIL", "SURET" })
                doc.Page(p =>
                {
                    p.Size(PageSizes.A5.Landscape());
                    p.Margin(25);
                    p.DefaultTextStyle(x => x.FontSize(10));
                    p.Header().Element(c => Baslik(c, ad, $"No: {v.No}   •   {v.Tarih:dd.MM.yyyy}   •   {nusha}"));
                    p.Content().PaddingTop(8).Column(col =>
                    {
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.ConstantColumn(140); cd.RelativeColumn(); });
                            Satir(t, karsiEtiket, v.KarsiTaraf);
                            Satir(t, "Tutar", $"{N2(v.Tutar)} {v.ParaBirimi.Ad()}");
                            Satir(t, "Yazıyla", TutarYazi.Yaz(v.Tutar, v.ParaBirimi));
                            Satir(t, "Ödeme şekli", v.OdemeSekli);
                            if (v.Plaka is not null) Satir(t, "Araç", v.Plaka);
                            if (v.SeferNo is not null) Satir(t, "Sefer", v.SeferNo);
                            if (!string.IsNullOrWhiteSpace(v.Aciklama)) Satir(t, "Açıklama", v.Aciklama);
                        });
                        col.Item().PaddingTop(6).Text($"Yukarıda yazılı {N2(v.Tutar)} {v.ParaBirimi.Ad()} {(v.Tur == MakbuzTuru.Tahsilat ? "tahsil edilmiştir" : "teslim edilmiştir")}.").Italic().FontSize(9);
                        col.Item().Element(c => Imzalar(c, imza1, imza2));
                    });
                    p.Footer().Element(Alt);
                });
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------ Sefer föyü

    public byte[] SeferFoyu(SeferOzet o, IReadOnlyList<MasrafSatir> masraflar, IReadOnlyList<AvansSatir> avanslar)
    {
        var s = o.Sefer;
        return Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(25);
            p.DefaultTextStyle(x => x.FontSize(9));
            p.Header().Element(c => Baslik(c, "SEFER FÖYÜ", $"{s.SeferNo}   •   {s.Durum.Ad()}"));
            p.Content().PaddingTop(6).Column(col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.ConstantColumn(90); cd.RelativeColumn(); cd.ConstantColumn(90); cd.RelativeColumn(); });
                    void S(string a, string? b) { t.Cell().PaddingVertical(2).Text(a).SemiBold(); t.Cell().PaddingVertical(2).Text(b ?? "-"); }
                    S("Müşteri", o.Musteri); S("Kapsam", s.Kapsam.Ad());
                    S("Güzergah", o.Guzergah); S("Planlanan", s.PlanlananTarih.ToString("dd.MM.yyyy"));
                    S("Araç", o.Plaka); S("Dorse", o.Dorse);
                    S("Şoför(ler)", o.Soforler); S("Yük", s.YukTanimi);
                    S("Çıkış", s.CikisTarihi?.ToString("dd.MM.yyyy HH:mm")); S("Teslim", s.TeslimTarihi?.ToString("dd.MM.yyyy HH:mm"));
                    S("Çıkış km", s.CikisKm?.ToString("N0", Tr)); S("Dönüş km", s.DonusKm?.ToString("N0", Tr));
                    S("CMR", s.CmrNo); S("İrsaliye", s.IrsaliyeNo);
                });
                if (o.Duraklar.Count > 0)
                {
                    col.Item().PaddingTop(8).Text("Duraklar").Bold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.ConstantColumn(20); cd.RelativeColumn(3); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        t.Header(h => { foreach (var x in new[] { "#", "Lokasyon", "Tip", "Planlanan", "Gerçekleşen" }) h.Cell().Background("#E8EEF7").Padding(2).Text(x).SemiBold(); });
                        var i = 1;
                        foreach (var d in o.Duraklar)
                        {
                            t.Cell().Padding(2).Text((i++).ToString()); t.Cell().Padding(2).Text(d.Durak); t.Cell().Padding(2).Text(d.Tip.Ad());
                            t.Cell().Padding(2).Text(d.Plan?.ToString("dd.MM HH:mm") ?? ""); t.Cell().Padding(2).Text(d.Gercek?.ToString("dd.MM HH:mm") ?? "");
                        }
                    });
                }
                col.Item().PaddingTop(8).Text("Şoföre verilen avanslar").Bold();
                Tablo(col, ["Tarih", "Şoför", "Tür", "Tutar"], avanslar.Where(a => !a.Iptal).Select(a => new[] { a.Tarih.ToString("dd.MM.yyyy"), a.Sofor, a.Aciklama ?? "", $"{N2(a.Tutar)} {a.PB}" }));
                col.Item().PaddingTop(8).Text("Masraflar").Bold();
                Tablo(col, ["Tarih", "Tür", "Ödeme", "Açıklama", "Tutar (TL)"], masraflar.Where(m => !m.Iptal).Select(m => new[] { m.Tarih.ToString("dd.MM.yyyy"), m.Tur, m.OdemeAd, m.Aciklama ?? "", N2(m.TutarTL) }));
                if (o.Karlilik is { } k)
                {
                    col.Item().PaddingTop(10).Background("#F3F6FA").Padding(6).Column(x =>
                    {
                        x.Item().Text("Kârlılık (KDV hariç, TL)").Bold();
                        x.Item().Text($"Navlun: {N2(k.GelirTL)}   •   Gider: {N2(k.GiderTL)}   •   Kâr: {N2(k.KarTL)}{(k.KarMarji is null ? "" : $"  (%{k.KarMarji:N1})")}");
                    });
                }
                col.Item().Element(c => Imzalar(c, "Hazırlayan", "Şoför", "Onaylayan"));
            });
            p.Footer().Element(Alt);
        })).GeneratePdf();
    }

    private static void Tablo(ColumnDescriptor col, string[] basliklar, IEnumerable<string[]> satirlar)
    {
        var liste = satirlar.ToList();
        if (liste.Count == 0)
        {
            col.Item().Text("— kayıt yok —").FontColor(Colors.Grey.Darken1);
            return;
        }
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { foreach (var _ in basliklar) cd.RelativeColumn(); });
            t.Header(h => { foreach (var b in basliklar) h.Cell().Background("#E8EEF7").Padding(2).Text(b).SemiBold(); });
            foreach (var s in liste)
                for (var i = 0; i < s.Length; i++)
                {
                    var c = t.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2);
                    if (i == s.Length - 1) c.AlignRight().Text(s[i]); else c.Text(s[i]);
                }
        });
    }

    // ------------------------------------------------------------------ Şoför ekstresi / hakediş / zimmet

    public byte[] SoforEkstresi(string sofor, DateOnly bas, DateOnly bit, IReadOnlyList<SoforDefterSatir> satirlar) =>
        Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.A4.Landscape());
            p.Margin(20);
            p.DefaultTextStyle(x => x.FontSize(8));
            p.Header().Element(c => Baslik(c, "ŞOFÖR HESAP EKSTRESİ", $"{sofor}   •   {bas:dd.MM.yyyy} - {bit:dd.MM.yyyy}"));
            p.Content().PaddingTop(6).Column(col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(55); cd.ConstantColumn(65); cd.ConstantColumn(80); cd.ConstantColumn(80); cd.RelativeColumn(); cd.ConstantColumn(70); cd.ConstantColumn(70); cd.ConstantColumn(75);
                    });
                    t.Header(h => { foreach (var b in new[] { "Tarih", "Araç", "Sefer", "İşlem", "Açıklama", "Verilen", "Alınan", "Bakiye" }) h.Cell().Background("#E8EEF7").Padding(2).Text(b).SemiBold(); });
                    foreach (var s in satirlar)
                    {
                        var renk = s.Iptal ? Colors.Grey.Medium : Colors.Black;
                        t.Cell().Padding(2).Text(s.Tarih.ToString("dd.MM.yyyy")).FontColor(renk);
                        t.Cell().Padding(2).Text(s.Plaka ?? "").FontColor(renk);
                        t.Cell().Padding(2).Text(s.SeferNo ?? "").FontColor(renk);
                        t.Cell().Padding(2).Text(s.Islem).FontColor(renk);
                        t.Cell().Padding(2).Text(s.Aciklama ?? "").FontColor(renk);
                        t.Cell().Padding(2).AlignRight().Text(s.Verilen == 0 ? "" : N2(s.Verilen)).FontColor(renk);
                        t.Cell().Padding(2).AlignRight().Text(s.Alinan == 0 ? "" : N2(s.Alinan)).FontColor(renk);
                        t.Cell().Padding(2).AlignRight().Text(s.Bakiye is null ? "" : N2(s.Bakiye.Value)).FontColor(renk);
                    }
                });
                var son = satirlar.LastOrDefault()?.Bakiye ?? 0;
                col.Item().PaddingTop(8).AlignRight().Text($"Toplam verilen: {N2(satirlar.Sum(s => s.Verilen))}   •   Toplam alınan: {N2(satirlar.Sum(s => s.Alinan))}   •   " +
                                                          $"Dönem sonu bakiye: {N2(son)} TL ({(son > 0 ? "şoför borçlu" : son < 0 ? "şoföre borçluyuz" : "kapalı")})").Bold();
                col.Item().Element(c => Imzalar(c, "Muhasebe", "Şoför"));
            });
            p.Footer().Element(Alt);
        })).GeneratePdf();

    public byte[] HakedisPusulasi(string sofor, int donem, IReadOnlyList<TahakkukSatir> satirlar, decimal guncelBakiye) =>
        Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.A5);
            p.Margin(20);
            p.DefaultTextStyle(x => x.FontSize(9));
            p.Header().Element(c => Baslik(c, "HAKEDİŞ PUSULASI", $"{sofor}   •   {donem % 100:D2}/{donem / 100}"));
            p.Content().PaddingTop(6).Column(col =>
            {
                var gecerli = satirlar.Where(s => !s.Iptal).ToList();
                Tablo(col, ["Tür", "Sefer", "Açıklama", "Tutar"], gecerli.Select(s => new[] { s.TurAd, s.SeferNo ?? "", s.Aciklama ?? "", (s.Net < 0 ? "-" : "") + N2(s.Tutar) }));
                col.Item().PaddingTop(6).AlignRight().Text($"Dönem hakedişi (net): {N2(gecerli.Sum(s => s.Net))} TL").Bold();
                col.Item().AlignRight().Text($"Güncel hesap bakiyesi: {N2(-guncelBakiye)} TL {(guncelBakiye < 0 ? "(şoföre ödenecek)" : guncelBakiye > 0 ? "(şoför borçlu)" : "")}");
                col.Item().PaddingTop(4).Text("Bu belge iç hakediş takibi içindir; resmi bordro yerine geçmez.").Italic().FontSize(7);
                col.Item().Element(c => Imzalar(c, "Muhasebe", "Şoför"));
            });
            p.Footer().Element(Alt);
        })).GeneratePdf();

    public byte[] ZimmetTutanagi(string sofor, IReadOnlyList<Zimmet> zimmetler) =>
        Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(30);
            p.DefaultTextStyle(x => x.FontSize(10));
            p.Header().Element(c => Baslik(c, "ZİMMET TESLİM TUTANAĞI", $"{DateTime.Today:dd.MM.yyyy}"));
            p.Content().PaddingTop(8).Column(col =>
            {
                col.Item().Text($"Aşağıda listelenen kart ve ekipmanlar {sofor} adlı personele zimmetlenmiştir. Personel, zimmetli malzemeyi " +
                                "özenle kullanmayı ve görev bitiminde/talep edildiğinde eksiksiz iade etmeyi kabul eder.");
                col.Item().PaddingTop(8);
                Tablo(col, ["Tür", "Ad", "Seri No", "Araç", "Veriliş", "Değer"],
                    zimmetler.Select(z => new[] { z.Tur.Ad(), z.Ad, z.SeriNo ?? "", z.Arac?.Plaka ?? "", z.VerilisTarihi.ToString("dd.MM.yyyy"), z.Deger is null ? "" : N2(z.Deger.Value) }));
                col.Item().Element(c => Imzalar(c, "Teslim Eden", "Teslim Alan"));
            });
            p.Footer().Element(Alt);
        })).GeneratePdf();

    // ------------------------------------------------------------------ Kredi planı

    public byte[] KrediOdemePlani(KrediDetay d) =>
        Document.Create(doc => doc.Page(p =>
        {
            var k = d.Kredi;
            p.Size(PageSizes.A4.Landscape());
            p.Margin(20);
            p.DefaultTextStyle(x => x.FontSize(8));
            p.Header().Element(c => Baslik(c, "KREDİ ÖDEME PLANI", $"{d.Banka}   •   {k.KrediNo}"));
            p.Content().PaddingTop(6).Column(col =>
            {
                col.Item().Text($"{k.Tur.Ad()}  •  Anapara {N2(k.Anapara)} {k.ParaBirimi.Ad()}  •  Faiz %{k.FaizOrani:N2} ({k.FaizTipi.Ad()})  •  BSMV %{k.BsmvOrani:N0}  •  KKDF %{k.KkdfOrani:N0}  •  " +
                                $"{k.VadeSayisi} ay  •  Kullandırım {k.KullandirimTarihi:dd.MM.yyyy}");
                col.Item().Text($"Ödenen anapara {N2(d.Ozet.OdenenAnapara)}  •  Kalan anapara {N2(d.Ozet.KalanAnapara)}  •  Ödenen faiz+vergi {N2(d.Ozet.OdenenFaiz)}  •  " +
                                $"Kalan faiz+vergi {N2(d.Ozet.KalanFaiz)}").Bold();
                col.Item().PaddingTop(6).Table(t =>
                {
                    var b = new[] { "No", "Vade", "Anapara", "Faiz", "BSMV", "KKDF", "Taksit", "Kalan Anapara", "Ödenen", "Durum" };
                    t.ColumnsDefinition(cd => { foreach (var _ in b) cd.RelativeColumn(); });
                    t.Header(h => { foreach (var x in b) h.Cell().Background("#E8EEF7").Padding(2).Text(x).SemiBold(); });
                    foreach (var s in d.Plan)
                    {
                        var renk = s.Gecikti ? Colors.Red.Darken2 : s.Durum == TaksitDurum.Odendi ? Colors.Green.Darken2 : s.Durum is TaksitDurum.PlanDegisti or TaksitDurum.Kapatildi ? Colors.Grey.Medium : Colors.Black;
                        foreach (var v in new[] { s.No.ToString(), s.Vade.ToString("dd.MM.yyyy"), N2(s.Anapara), N2(s.Faiz), N2(s.Bsmv), N2(s.Kkdf), N2(s.Toplam), N2(s.KalanAnapara), N2(s.Odenen), s.DurumAd })
                            t.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(v).FontColor(renk);
                    }
                });
            });
            p.Footer().Element(Alt);
        })).GeneratePdf();

    // ------------------------------------------------------------------ Mutabakat

    public byte[] CariMutabakat(CariOzet c, DateOnly tarih) =>
        Document.Create(doc => doc.Page(p =>
        {
            var (unvan, _) = Firma();
            p.Size(PageSizes.A4);
            p.Margin(35);
            p.DefaultTextStyle(x => x.FontSize(10));
            p.Header().Element(x => Baslik(x, "CARİ HESAP MUTABAKATI", tarih.ToString("dd.MM.yyyy")));
            p.Content().PaddingTop(12).Column(col =>
            {
                col.Item().Text($"Sayın {c.Unvan},").Bold();
                col.Item().PaddingTop(8).Text($"Kayıtlarımıza göre {tarih:dd.MM.yyyy} tarihi itibarıyla cari hesabınız " +
                    $"{N2(Math.Abs(c.Bakiye))} TL {(c.Bakiye > 0 ? "BORÇ (firmamıza borçlu)" : c.Bakiye < 0 ? "ALACAK (firmamızdan alacaklı)" : "SIFIR")} bakiye vermektedir.");
                col.Item().PaddingTop(4).Text($"Yazıyla: {TutarYazi.Yaz(Math.Abs(c.Bakiye))}").Italic();
                col.Item().PaddingTop(8).Text("Mutabık olup olmadığınızı bu formu imzalayıp kaşeleyerek 15 gün içinde bildirmenizi rica ederiz. " +
                                              "Süresi içinde bildirim yapılmazsa mutabık sayılacaksınız.");
                col.Item().PaddingTop(20).Row(r =>
                {
                    r.RelativeItem().Border(0.5f).Padding(8).Column(x =>
                    {
                        x.Item().Text("[   ] Mutabıkız").Bold();
                        x.Item().Text("[   ] Mutabık değiliz — kayıtlarımıza göre bakiye:");
                        x.Item().PaddingTop(18).LineHorizontal(0.5f);
                    });
                });
                col.Item().Element(x => Imzalar(x, unvan, c.Unvan));
            });
            p.Footer().Element(Alt);
        })).GeneratePdf();
}
