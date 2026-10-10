using System.Collections;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed record RaporTanimi(string Ad, string Aciklama, string Izin, IReadOnlyList<Kolon> Kolonlar, Func<DateOnly, DateOnly, Task<IEnumerable>> Getir, Func<IEnumerable, string?>? Ozet = null)
{
    public override string ToString() => Ad;
}

public sealed partial class RaporVM : ObservableObject
{
    private readonly IDisaAktarici _disa;
    public IReadOnlyList<RaporTanimi> Raporlar { get; }

    [ObservableProperty] private RaporTanimi? _secili;
    [ObservableProperty] private DateTime? _basTarih = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _bitTarih = DateTime.Today;
    [ObservableProperty] private IEnumerable? _veri;
    [ObservableProperty] private string? _ozet;
    [ObservableProperty] private bool _mesgul;

    public event Action<IReadOnlyList<Kolon>>? KolonlarDegisti;

    public RaporVM(RaporServisi rapor, GiderServisi gider, FinansServisi finans, FaturaServisi fatura, IKullaniciOturumu oturum, IDisaAktarici disa)
    {
        _disa = disa;
        static string Tl(decimal d) => $"{d:N2} TL";
        var tumu = new List<RaporTanimi>
        {
            new("Sefer kârlılığı", "Her sefer için navlun − sefer giderleri (yakıt, masraf, HGS, harcırah, taşeron, ceza). KDV hariç, TL.", Izinler.RaporFinans,
            [
                new("Sefer", nameof(SeferKarlilikSatir.SeferNo)), Kolon.Tarih("Tarih", nameof(SeferKarlilikSatir.Tarih)), new("Müşteri", nameof(SeferKarlilikSatir.Musteri)),
                new("Plaka", nameof(SeferKarlilikSatir.Plaka)), new("Güzergah", nameof(SeferKarlilikSatir.Guzergah)), Kolon.Para("Gelir", nameof(SeferKarlilikSatir.Gelir)),
                Kolon.Para("Gider", nameof(SeferKarlilikSatir.Gider)), Kolon.Para("Kâr", nameof(SeferKarlilikSatir.Kar)), new("Marj %", nameof(SeferKarlilikSatir.Marj), Application.Abstractions.KolonBicim.Yuzde),
                Kolon.Sayi("Km", nameof(SeferKarlilikSatir.Km), true), new("Km Maliyet", nameof(SeferKarlilikSatir.KmBasiMaliyet), Application.Abstractions.KolonBicim.Para), new("Taşeron", "Taseron?bool"),
            ],
            async (b, e) => await rapor.SeferKarlilikAsync(b, e),
            v => { var l = v.Cast<SeferKarlilikSatir>().ToList(); return $"Gelir {Tl(l.Sum(x => x.Gelir))}  •  Gider {Tl(l.Sum(x => x.Gider))}  •  Kâr {Tl(l.Sum(x => x.Kar))}"; }),

            new("Araç kârlılığı / km başı maliyet", "Araç bazında gelir, değişken giderler ve sabit giderler (sigorta, MTV, personel, amortisman, kredi faizi).", Izinler.RaporFinans,
            [
                new("Plaka", nameof(AracKarlilikSatir.Plaka)), Kolon.Sayi("Sefer", nameof(AracKarlilikSatir.SeferSayisi), true), Kolon.Sayi("Km", nameof(AracKarlilikSatir.Km), true),
                Kolon.Para("Gelir", nameof(AracKarlilikSatir.Gelir)), Kolon.Para("Yakıt", nameof(AracKarlilikSatir.Yakit)), Kolon.Para("Yol/Köprü", nameof(AracKarlilikSatir.YolKopru)),
                Kolon.Para("Harcırah", nameof(AracKarlilikSatir.Harcirah)), Kolon.Para("Bakım", nameof(AracKarlilikSatir.Bakim)), Kolon.Para("Sigorta", nameof(AracKarlilikSatir.Sigorta)),
                Kolon.Para("Vergi/Harç", nameof(AracKarlilikSatir.VergiHarc)), Kolon.Para("Personel", nameof(AracKarlilikSatir.Personel)), Kolon.Para("Diğer", nameof(AracKarlilikSatir.DigerMasraf)),
                Kolon.Para("Ceza", nameof(AracKarlilikSatir.Ceza)), Kolon.Para("Amortisman", nameof(AracKarlilikSatir.Amortisman)), Kolon.Para("Kredi Faizi", nameof(AracKarlilikSatir.FinansmanFaizi)),
                Kolon.Para("Toplam Gider", nameof(AracKarlilikSatir.ToplamGider)), Kolon.Para("Kâr", nameof(AracKarlilikSatir.Kar)), new("Marj %", nameof(AracKarlilikSatir.Marj), Application.Abstractions.KolonBicim.Yuzde),
                new("Km Maliyet", nameof(AracKarlilikSatir.KmBasiMaliyet), Application.Abstractions.KolonBicim.Para), new("Km Gelir", nameof(AracKarlilikSatir.KmBasiGelir), Application.Abstractions.KolonBicim.Para),
                new("Boş Km %", nameof(AracKarlilikSatir.BosKmOrani), Application.Abstractions.KolonBicim.Yuzde),
            ],
            async (b, e) => await rapor.AracKarlilikAsync(b, e),
            v => { var l = v.Cast<AracKarlilikSatir>().ToList(); return $"Gelir {Tl(l.Sum(x => x.Gelir))}  •  Gider {Tl(l.Sum(x => x.ToplamGider))}  •  Kâr {Tl(l.Sum(x => x.Kar))}"; }),

            new("Aylık gelir-gider", "Bitiş tarihinin yılı için aylık özet (sefer tarihine göre). KDV hariç, TL.", Izinler.RaporFinans,
            [
                new("Dönem", nameof(AylikSatir.Donem)), Kolon.Sayi("Sefer", nameof(AylikSatir.SeferSayisi), true), Kolon.Para("Gelir", nameof(AylikSatir.Gelir)),
                Kolon.Para("Gider", nameof(AylikSatir.Gider)), Kolon.Para("Taşeron", nameof(AylikSatir.TaseronGider)), Kolon.Para("Kâr", nameof(AylikSatir.Kar)),
            ],
            async (_, e) => await rapor.AylikAsync(e.Year)),

            new("Şoför performansı", "Birincil şoför olduğu seferlerin sayısı, km, gelir, sefer kârı, yakıt tüketimi, ceza ve harcırah.", Izinler.RaporFinans,
            [
                new("Şoför", nameof(SoforPerformansSatir.AdSoyad)), Kolon.Sayi("Sefer", nameof(SoforPerformansSatir.SeferSayisi), true), Kolon.Sayi("Km", nameof(SoforPerformansSatir.Km), true),
                Kolon.Para("Gelir", nameof(SoforPerformansSatir.Gelir)), Kolon.Para("Sefer Kârı", nameof(SoforPerformansSatir.SeferKari)),
                new("L/100km", nameof(SoforPerformansSatir.LitreYuzKm), Application.Abstractions.KolonBicim.Sayi), Kolon.Sayi("Ceza Adedi", nameof(SoforPerformansSatir.CezaSayisi), true),
                Kolon.Para("Ceza", nameof(SoforPerformansSatir.CezaTutari)), Kolon.Para("Harcırah", nameof(SoforPerformansSatir.Harcirah)),
            ],
            async (b, e) => await rapor.SoforPerformansAsync(b, e)),

            new("Nakit akış tahmini", "Bitiş tarihine kadar vadesi gelen müşteri alacakları, cari borçlar, çekler ve kredi taksitleri.", Izinler.RaporFinans,
            [
                Kolon.Tarih("Vade", nameof(NakitAkisSatir.Vade)), new("Tür", nameof(NakitAkisSatir.Tur)), new("Karşı Taraf", nameof(NakitAkisSatir.Karsi)),
                new("Açıklama", nameof(NakitAkisSatir.Aciklama), Genislik: 260), Kolon.Para("Giriş", nameof(NakitAkisSatir.Giris)), Kolon.Para("Çıkış", nameof(NakitAkisSatir.Cikis)),
            ],
            async (_, e) => await rapor.NakitAkisAsync(e),
            v => { var l = v.Cast<NakitAkisSatir>().ToList(); return $"Giriş {Tl(l.Sum(x => x.Giris))}  •  Çıkış {Tl(l.Sum(x => x.Cikis))}  •  Net {Tl(l.Sum(x => x.Giris - x.Cikis))}"; }),

            new("Yakıt tüketimi (L/100km)", "Depo-dolu yöntemiyle araç bazında tüketim ve beklenen tüketime göre sapma.", Izinler.RaporOperasyon,
            [
                new("Plaka", nameof(TuketimRaporSatir.Plaka)), Kolon.Sayi("Km", nameof(TuketimRaporSatir.Km), true), Kolon.Sayi("Litre", nameof(TuketimRaporSatir.Litre), true),
                new("L/100km", nameof(TuketimRaporSatir.LitreYuzKm), Application.Abstractions.KolonBicim.Sayi), new("Beklenen", nameof(TuketimRaporSatir.Beklenen), Application.Abstractions.KolonBicim.Sayi),
                new("Sapma %", nameof(TuketimRaporSatir.SapmaYuzde), Application.Abstractions.KolonBicim.Yuzde), new("Alarm", "Alarm?bool"),
            ],
            async (b, e) => await gider.TuketimRaporuAsync(b, e)),

            new("Alacak yaşlandırma", "Müşteri alacaklarının vadesine göre dağılımı (FIFO). Tarih aralığından bağımsızdır.", Izinler.FinansGor,
            [
                new("Cari", nameof(YaslandirmaGorunum.Unvan), Genislik: 260), Kolon.Para("Vadesi Gelmemiş", nameof(YaslandirmaGorunum.VadesiGelmemis)),
                Kolon.Para("0-30", nameof(YaslandirmaGorunum.Gun0_30)), Kolon.Para("31-60", nameof(YaslandirmaGorunum.Gun31_60)), Kolon.Para("61-90", nameof(YaslandirmaGorunum.Gun61_90)),
                Kolon.Para("90+", nameof(YaslandirmaGorunum.Gun90Ustu)), Kolon.Para("Toplam", nameof(YaslandirmaGorunum.Toplam)),
            ],
            async (_, _) => (await finans.YaslandirmaAsync()).Select(y => new YaslandirmaGorunum(y.CariId, y.Unvan, y.Sonuc.VadesiGelmemis, y.Sonuc.Gun0_30, y.Sonuc.Gun31_60, y.Sonuc.Gun61_90, y.Sonuc.Gun90Ustu, y.Sonuc.Toplam)).ToList()),

            new("Faturalanmamış seferler", "Teslim edilmiş/kapatılmış ama satış faturası kaydı olmayan seferler.", Izinler.FinansGor,
            [
                new("Sefer", nameof(FaturalanmamisSefer.SeferNo)), Kolon.Tarih("Tarih", nameof(FaturalanmamisSefer.Tarih)), new("Müşteri", nameof(FaturalanmamisSefer.Musteri)),
                new("Güzergah", nameof(FaturalanmamisSefer.Guzergah)), new("Plaka", nameof(FaturalanmamisSefer.Plaka)), Kolon.Para("Navlun", nameof(FaturalanmamisSefer.Navlun)), new("PB", nameof(FaturalanmamisSefer.PB)),
            ],
            async (_, _) => await fatura.FaturalanmamisSeferlerAsync()),
        };
        Raporlar = tumu.Where(r => oturum.Yetkili(r.Izin)).ToList();
        _secili = Raporlar.FirstOrDefault();
    }

    partial void OnSeciliChanged(RaporTanimi? value)
    {
        Veri = null;
        Ozet = value?.Aciklama;
        if (value is not null) KolonlarDegisti?.Invoke(value.Kolonlar);
    }

    [RelayCommand]
    private async Task CalistirAsync()
    {
        if (Secili is not { } r) return;
        Mesgul = true;
        try
        {
            var bas = DateOnly.FromDateTime(BasTarih ?? DateTime.Today.AddYears(-1));
            var bit = DateOnly.FromDateTime(BitTarih ?? DateTime.Today);
            var v = await r.Getir(bas, bit);
            Veri = v;
            Ozet = r.Ozet?.Invoke(v) ?? $"{v.Cast<object>().Count()} satır";
        }
        catch (Exception ex)
        {
            await Dialog.HataGoster(ex);
        }
        finally
        {
            Mesgul = false;
        }
    }

    [RelayCommand]
    private async Task DisaAsync(string tur)
    {
        if (Secili is not { } r || Veri is null) return;
        var pdf = tur == "pdf";
        var tablo = KolonOlusturucu.Tablo(r.Ad, r.Kolonlar, Veri, $"Dönem: {BasTarih:dd.MM.yyyy} - {BitTarih:dd.MM.yyyy}", Ozet ?? "");
        var dlg = new SaveFileDialog { FileName = $"{r.Ad.Replace('/', '-')} {DateTime.Now:yyyy-MM-dd}", Filter = pdf ? "PDF|*.pdf" : "Excel|*.xlsx" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            await File.WriteAllBytesAsync(dlg.FileName, await Task.Run(() => pdf ? _disa.Pdf(tablo) : _disa.Excel(tablo)));
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await Dialog.HataGoster(ex);
        }
    }
}
