using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Kontroller;
using Ozaylar.Filo.Desktop.Sayfalar;
using Ozaylar.Filo.Domain.Common;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.ViewModels;

/// <summary>Patron paneli: bu ayın özeti, 12 ay trend, kredi yükü, uyarılar, yoldaki araçlar, şoför bakiyeleri, araç kârlılığı.</summary>
public sealed class PanelVM(PanelServisi servis, UyariServisi uyari, IKullaniciOturumu oturum) : DetayVM
{
    private PanelOzeti? _o;
    private List<Uyari> _uyarilar = [];
    private bool _sekmeler;

    /// <summary>Başlıktaki zil için: kritik + uyarı sayısı.</summary>
    public event Action<int, int>? UyarilarDegisti;

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        .. oturum.Yetkili(Izinler.SeferYaz) ? new[] { Eylem.Yap("Yeni Sefer", SymbolRegular.Add24, async () =>
        {
            Gezgin.Git(typeof(SeferlerSayfasi));
            await App.Al<SeferListeVM>().YeniKomut.ExecuteAsync(null);
        }, true) } : [],
        .. oturum.Yetkili(Izinler.SoforHesapYaz) ? new[] { Eylem.Yap("Şoföre Para Ver", SymbolRegular.ArrowUpload24, async () =>
        {
            if (await SoforHesapFormlari.AvansAsync(App.Al<SoforHesapServisi>(), App.Al<Secimler>())) await YenileAsync();
        }, true) } : [],
    ];

    protected override async Task YukleAsync()
    {
        var o = _o = await servis.OzetAsync();
        _uyarilar = await uyari.ListeAsync();
        var saat = DateTime.Now.Hour;
        Baslik = $"{(saat < 12 ? "Günaydın" : saat < 18 ? "İyi günler" : "İyi akşamlar")}, {oturum.AdSoyad.Split(' ')[0]}";
        AltBaslik = string.Join("  ·  ", new[]
        {
            DateTime.Now.ToString("dd MMMM yyyy, dddd", Kultur.Tr),
            o.Yoldakiler.Count > 0 ? $"{o.Yoldakiler.Count} araç yolda" : "Yolda araç yok",
            o.Kredi is { GecikenTaksit: > 0 } kg ? $"{kg.GecikenTaksit} gecikmiş taksit" : null,
            o.FaturalanmamisSefer > 0 ? $"{o.FaturalanmamisSefer} faturalanmamış sefer" : null,
        }.Where(x => x is not null));
        var kritik = _uyarilar.Count(u => u.Seviye == UyariSeviye.Kritik);
        (Rozet, RozetRenk) = kritik > 0 ? ($"{kritik} kritik uyarı", "kirmizi") : _uyarilar.Count > 0 ? ($"{_uyarilar.Count} uyarı", "turuncu") : ("Her şey yolunda", "yesil");
        UyarilarDegisti?.Invoke(kritik, _uyarilar.Count);

        Kpiler.Clear();
        if (o.Kasalar is not null)
            Kpiler.Add(new Kpi("Kasa + banka (TL)", Para0(o.Kasalar.Where(k => k.ParaBirimi == ParaBirimi.TRY).Sum(k => k.Bakiye)),
                string.Join("  ", o.Kasalar.Where(k => k.ParaBirimi != ParaBirimi.TRY).GroupBy(k => k.PB).Select(g => $"{g.Sum(k => k.Bakiye):N0} {g.Key}"))));
        if (o.BuAyGelir is { } gelir) Kpiler.Add(new Kpi("Bu ay gelir", Para0(gelir), $"{o.BuAySeferSayisi} sefer"));
        else Kpiler.Add(new Kpi("Bu ay sefer", o.BuAySeferSayisi.ToString("N0")));
        if (o.BuAyGider is { } gider) Kpiler.Add(new Kpi("Bu ay gider", Para0(gider)));
        if (o.BuAyKar is { } kar) Kpiler.Add(new Kpi("Bu ay kâr", Para0(kar), "KDV hariç", kar < 0 ? "kirmizi" : "yesil"));
        if (o.AcikAlacak is { } al) Kpiler.Add(new Kpi("Açık alacak", Para0(al), o.VadesiGecenAlacak > 0 ? $"Vadesi geçen {o.VadesiGecenAlacak:N0} ₺" : null, o.VadesiGecenAlacak > 0 ? "turuncu" : null));
        if (o.AcikBorc is { } bo) Kpiler.Add(new Kpi("Açık borç (cari)", Para0(bo), o.Odenecek30Gun > 0 ? $"30 günde ödenecek {o.Odenecek30Gun:N0} ₺" : null));
        if (o.Kredi is { } kr)
        {
            Kpiler.Add(new Kpi("Kredi borcu", Para0(kr.KalanAnaparaTL), $"Bu ay taksit {kr.BuAyOdenecekTL:N0} ₺"));
            if (kr.GecikenTaksit > 0) Kpiler.Add(new Kpi("Gecikmiş taksit", Para0(kr.GecikenTutarTL), $"{kr.GecikenTaksit} taksit", "kirmizi"));
        }
        if (o.SoforBakiyeleri is { Count: > 0 } sb)
            Kpiler.Add(new Kpi("Şoförlerdeki para", Para0(sb.Where(s => s.Bakiye > 0).Sum(s => s.Bakiye)),
                $"Şoförlere borç {-sb.Where(s => s.Bakiye < 0).Sum(s => s.Bakiye):N0} ₺"));
        if (o.FaturalanmamisSefer > 0) Kpiler.Add(new Kpi("Faturalanmamış sefer", o.FaturalanmamisSefer.ToString(), Renk: "turuncu"));
        if (o.OnayBekleyenHarcama > 0) Kpiler.Add(new Kpi("Onay bekleyen harcama", o.OnayBekleyenHarcama.ToString(), "Şoför nakit harcamaları", "turuncu"));

        if (_sekmeler) return;
        _sekmeler = true;
        Sekmeler.Add(new DetaySekme("Genel Bakış", GenelOlustur));
        Sekmeler.Add(DetaySekme.Liste("Uyarılar", () => new SekmeListeVM<Uyari>("Uyarılar",
        [
            new("Seviye", nameof(Uyari.SeviyeAd), Genislik: 80), new("Konu", nameof(Uyari.Kategori), Genislik: 90), new("Uyarı", nameof(Uyari.Metin), Genislik: 640),
        ], _ => Task.FromResult<IEnumerable<Uyari>>(_uyarilar)) { DetayIslem = UyariAc }));
        Sekmeler.Add(DetaySekme.Liste("Yoldaki Araçlar", () => new SekmeListeVM<YoldakiArac>("Yoldaki Araçlar",
        [
            new("Sefer", nameof(YoldakiArac.SeferNo)), new("Plaka", nameof(YoldakiArac.Plaka)), new("Şoför", nameof(YoldakiArac.Sofor), Genislik: 160),
            new("Güzergah", nameof(YoldakiArac.Guzergah), Genislik: 260), new("Durum", nameof(YoldakiArac.DurumAd)), Kolon.TarihSaat("Çıkış", nameof(YoldakiArac.Cikis)),
        ], _ => Task.FromResult<IEnumerable<YoldakiArac>>(_o?.Yoldakiler ?? [])) { DetayIslem = y => Detay.Sefer(y.SeferId) }));
        if (o.SoforBakiyeleri is not null)
            Sekmeler.Add(DetaySekme.Liste("Şoför Bakiyeleri", () => new SekmeListeVM<SoforBakiyeSatir>("Şoför Bakiyeleri",
            [
                new("Şoför", nameof(SoforBakiyeSatir.AdSoyad), Genislik: 180), Kolon.Para("Bakiye", nameof(SoforBakiyeSatir.Bakiye)), new("Durum", nameof(SoforBakiyeSatir.Durum)),
                Kolon.Sayi("Açık Avans", nameof(SoforBakiyeSatir.AcikAvans)), Kolon.Sayi("Onay Bekleyen", nameof(SoforBakiyeSatir.OnayBekleyen)),
            ], _ => Task.FromResult<IEnumerable<SoforBakiyeSatir>>(_o?.SoforBakiyeleri ?? [])) { DetayIslem = s => Detay.Sofor(s.SoforId) }));
        if (o.AracKarlari is not null)
            Sekmeler.Add(DetaySekme.Liste("Araç Kârlılığı (bu ay)", () => new SekmeListeVM<AracKarSatir>("Araç Kârlılığı",
            [
                new("Plaka", nameof(AracKarSatir.Plaka)), Kolon.Para("Gelir", nameof(AracKarSatir.Gelir)), Kolon.Para("Kâr", nameof(AracKarSatir.Kar)),
                Kolon.Para("Km Başı Maliyet", nameof(AracKarSatir.KmMaliyet), false),
            ], _ => Task.FromResult<IEnumerable<AracKarSatir>>(_o?.AracKarlari ?? [])) { DetayIslem = a => Detay.Arac(a.AracId) }));
        Sekmeler.Add(DetaySekme.Liste("Evrak / Bakım", () => new SekmeListeVM<EvrakUyari>("Süresi yaklaşan / dolan evraklar",
        [
            new("Evrak", nameof(EvrakUyari.Tur)), new("Kime", nameof(EvrakUyari.Hedef)), new("Belge No", nameof(EvrakUyari.BelgeNo)),
            Kolon.Tarih("Bitiş", nameof(EvrakUyari.Bitis)), new("Durum", nameof(EvrakUyari.Durum)), new("Sefere engel", "SeferEngeller?bool"),
        ], _ => Task.FromResult<IEnumerable<EvrakUyari>>(_o?.Evraklar ?? []))));
        if (o.Kasalar is not null)
            Sekmeler.Add(DetaySekme.Liste("Kasa / Banka", () => new SekmeListeVM<KasaBakiyeSatir>("Kasa ve Banka Bakiyeleri",
            [
                new("Hesap", nameof(KasaBakiyeSatir.Ad), Genislik: 240), new("Tip", nameof(KasaBakiyeSatir.TipAd)), new("PB", nameof(KasaBakiyeSatir.PB)),
                Kolon.Para("Bakiye", nameof(KasaBakiyeSatir.Bakiye), false),
            ], _ => Task.FromResult<IEnumerable<KasaBakiyeSatir>>(_o?.Kasalar ?? []))));
    }

    private object GenelOlustur()
    {
        if (_o is null) return Grafik.Bos();
        var kartlar = new List<System.Windows.FrameworkElement>();
        if (_o.Trend is { Count: > 0 } t)
            kartlar.Add(Grafik.Kart("Son 12 ay gelir / gider / kâr (TL)", Grafik.GelirGiderKar(t.Select(x => x.Donem).ToList(),
                t.Select(x => x.Gelir), t.Select(x => x.Gider), t.Select(x => x.Kar)), 280));
        if (_o.Kredi is { AylikYuk.Count: > 0 } k)
            kartlar.Add(Grafik.Kart("Önümüzdeki 12 ay kredi taksitleri (TL)", Grafik.Yigilmis(k.AylikYuk.Select(a => a.Ay.ToString("MMM yy", Kultur.Tr)).ToList(),
                ("Anapara", k.AylikYuk.Select(a => a.AnaparaTL), Grafik.Ana), ("Faiz + vergi", k.AylikYuk.Select(a => a.FaizTL), Grafik.Ikincil)), 280));
        if (_o.AracKarlari is { Count: > 0 } ak)
        {
            var sirali = ak.OrderByDescending(a => a.Kar).ToList();
            var secim = sirali.Count <= 10 ? sirali : [.. sirali.Take(5), .. sirali.TakeLast(5)];
            kartlar.Add(Grafik.Kart("Bu ay araç kârı — en iyi / en kötü (TL)", Grafik.Yigilmis(secim.Select(a => a.Plaka).ToList(),
                ("Kâr", secim.Select(a => Math.Max(0, a.Kar)), Grafik.Yesil), ("Zarar", secim.Select(a => Math.Min(0, a.Kar)), Grafik.Kirmizi)), 280));
        }
        if (_o.Kredi is { Bankalar.Count: > 0 } kb)
            kartlar.Add(Grafik.Kart("Kredi borcu — banka dağılımı", Grafik.Pasta(kb.Bankalar.Select(b => (b.Banka, b.KalanAnaparaTL))), 280));
        return kartlar.Count == 0 ? Grafik.Bos("Grafikler için finans rapor yetkisi gerekir.") : Grafik.Izgara(2, [.. kartlar]);
    }

    private static Task UyariAc(Uyari u)
    {
        switch (u.Hedef)
        {
            case UyariHedef.Kredi when u.HedefId is { } k: return KrediIslemleri.DetayAc((short)k);
            case UyariHedef.Sofor when u.HedefId is { } s: return Detay.Sofor((short)s);
            case UyariHedef.Cari when u.HedefId is { } c: return Detay.Cari(c);
            case UyariHedef.Sefer when u.HedefId is { } sf: return Detay.Sefer(sf);
            case UyariHedef.Evrak: Gezgin.Git(typeof(EvraklarSayfasi)); break;
            case UyariHedef.Bakim: Gezgin.Git(typeof(BakimPlanlariSayfasi)); break;
            case UyariHedef.Masraf: Gezgin.Git(typeof(SoforHesapSayfasi)); break;
            case UyariHedef.Ayarlar: Gezgin.Git(typeof(AyarlarMerkeziSayfasi)); break;
        }
        return Task.CompletedTask;
    }

    /// <summary>Başlıktaki zile tıklanınca: uyarılar sekmesi.</summary>
    public void UyarilariGoster() => SeciliSekme = Sekmeler.FirstOrDefault(s => s.Baslik == "Uyarılar") ?? SeciliSekme;
}
