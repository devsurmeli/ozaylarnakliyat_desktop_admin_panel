using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Kontroller;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.ViewModels;

/// <summary>Detay sayfalarına kısayollar.</summary>
public static class Detay
{
    public static Task Sofor(short id) { Gezgin.Ac(new SoforDetayVM(id)); return Task.CompletedTask; }
    public static Task Arac(short id) { Gezgin.Ac(new AracDetayVM(id)); return Task.CompletedTask; }
    public static Task Cari(int id) { Gezgin.Ac(new CariDetayVM(id)); return Task.CompletedTask; }
    public static Task Sefer(int id) { Gezgin.Ac(new SeferDetayVM(id)); return Task.CompletedTask; }

    internal static DateOnly Bugun => DateOnly.FromDateTime(DateTime.Today);
    internal static DateTime AyBasi => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    internal static IReadOnlyList<Kolon> SeferKolonlari { get; } =
    [
        new("Sefer No", nameof(SeferSatir.SeferNo), Genislik: 110), Kolon.Tarih("Tarih", nameof(SeferSatir.PlanlananTarih)), new("Durum", nameof(SeferSatir.DurumAd)),
        new("Müşteri", nameof(SeferSatir.Musteri), Genislik: 180), new("Araç", nameof(SeferSatir.Arac)), new("Şoför", nameof(SeferSatir.Soforler)),
        new("Çıkış", nameof(SeferSatir.Cikis)), new("Varış", nameof(SeferSatir.Varis)), Kolon.Sayi("Km", nameof(SeferSatir.Km), true),
        Kolon.Para("Navlun (TL)", nameof(SeferSatir.NavlunTL)),
    ];

    internal static IReadOnlyList<Kolon> MasrafKolonlari { get; } =
    [
        Kolon.Tarih("Tarih", nameof(MasrafSatir.Tarih)), new("Tür", nameof(MasrafSatir.Tur)), new("Sefer", nameof(MasrafSatir.Sefer)), new("Plaka", nameof(MasrafSatir.Plaka)),
        new("Şoför", nameof(MasrafSatir.Sofor)), Kolon.Para("Tutar", nameof(MasrafSatir.Tutar), false), new("PB", nameof(MasrafSatir.PB)),
        Kolon.Para("TL", nameof(MasrafSatir.TutarTL)), new("Ödeme", nameof(MasrafSatir.OdemeAd)), new("Açıklama", nameof(MasrafSatir.Aciklama), Genislik: 180),
        new("Onaylı", "Onayli?bool"), new("İptal", "Iptal?bool"),
    ];

    internal static IReadOnlyList<Kolon> DefterKolonlari(bool soforKolonu) =>
    [
        Kolon.Tarih("Tarih", nameof(SoforDefterSatir.Tarih)),
        .. soforKolonu ? new[] { new Kolon("Şoför", nameof(SoforDefterSatir.Sofor), Genislik: 150) } : [],
        new("Araç", nameof(SoforDefterSatir.Plaka)), new("Sefer", nameof(SoforDefterSatir.SeferNo)), new("İşlem", nameof(SoforDefterSatir.Islem)),
        new("Açıklama", nameof(SoforDefterSatir.Aciklama), Genislik: 260), Kolon.Para("Verilen", nameof(SoforDefterSatir.Verilen)), Kolon.Para("Alınan", nameof(SoforDefterSatir.Alinan)),
        .. soforKolonu ? [] : new[] { Kolon.Para("Bakiye", nameof(SoforDefterSatir.Bakiye), false) },
        new("Döviz", nameof(SoforDefterSatir.Doviz)),
    ];

    /// <summary>Evrak sekmesi: hedefe filtrelenmiş evrak listesi.</summary>
    internal static IListeVM Evraklar(EvrakHedef tip, short id, string ad)
    {
        var vm = App.Al<EvrakListeVM>();
        vm.HedefFiltre = (tip, id, ad);
        return vm;
    }
}

// =====================================================================================================================
// Şoför
// =====================================================================================================================

public sealed class SoforDetayVM : DetayVM
{
    private readonly short _id;
    private readonly SoforHesapServisi _servis = App.Al<SoforHesapServisi>();
    private readonly Secimler _s = App.Al<Secimler>();
    private SoforOzet? _o;

    public SoforDetayVM(short id)
    {
        _id = id;
        Baslik = "Şoför";
        var sh = App.Al<IKullaniciOturumu>();
        Sekmeler.Add(DetaySekme.Liste("Günlük Hareket", () => new SekmeListeVM<SoforDefterSatir>("Şoför Hareketleri", Detay.DefterKolonlari(false),
            async l => await _servis.DefterAsync(l.BasTarihi, l.BitTarihi, _id))
        {
            Tarihli = true,
            Ozet = l => $"Verilen: {l.Where(x => !x.Iptal).Sum(x => x.Verilen):N2}   •   Alınan: {l.Where(x => !x.Iptal).Sum(x => x.Alinan):N2}   •   " +
                        $"Dönem sonu bakiye: {l.LastOrDefault()?.Bakiye ?? 0:N2} (+ şoför borçlu / − şoföre borçluyuz)",
        }.Aralik(Detay.AyBasi.AddMonths(-2), DateTime.Today)));
        Sekmeler.Add(DetaySekme.Liste("Açık Kalemler", () => new SekmeListeVM<AcikKalem>("Mahsup Bekleyen Kalemler",
        [
            new("Tür", nameof(AcikKalem.TurAd)), Kolon.Tarih("Tarih", nameof(AcikKalem.Tarih)), new("Sefer", nameof(AcikKalem.SeferNo)),
            new("Açıklama", nameof(AcikKalem.Aciklama), Genislik: 260), Kolon.Para("Tutar", nameof(AcikKalem.Tutar), false), new("PB", nameof(AcikKalem.PB)),
            Kolon.Para("TL", nameof(AcikKalem.TutarTL)), new("Onaylı", "Onayli?bool"),
        ], async _ => await _servis.AcikKalemlerAsync(_id))
        {
            EylemOlustur = _ => [Eylem.Yap("Mahsup Ekranı", SymbolRegular.ArrowSwap24, () => { Gezgin.Git(typeof(Sayfalar.SoforHesapSayfasi)); return Task.CompletedTask; }, true)],
        }));
        Sekmeler.Add(DetaySekme.Liste("Avans / Ödemeler", () => new SekmeListeVM<AvansSatir>("Verilen Paralar",
        [
            Kolon.Tarih("Tarih", nameof(AvansSatir.Tarih)), new("Sefer", nameof(AvansSatir.Sefer)), Kolon.Para("Tutar", nameof(AvansSatir.Tutar)),
            new("PB", nameof(AvansSatir.PB)), new("Kasa / Banka", nameof(AvansSatir.Kasa)), new("Açıklama", nameof(AvansSatir.Aciklama), Genislik: 220),
            new("Mahsuplu", "Mahsuplu?bool"), new("İptal", "Iptal?bool"),
        ], async l => await _servis.AvanslarAsync(l.BasTarihi, l.BitTarihi, _id))
        {
            Tarihli = true,
            EylemOlustur = l =>
            [
                Eylem.Yap("Avans Makbuzu", SymbolRegular.Print24, () => l.Secili is { } a
                    ? Belge.AcAsync($"Avans Makbuzu {a.Id}", () => App.Al<IBelgeUretici>().Makbuz(new MakbuzVeri(MakbuzTuru.Avans, a.Id.ToString(), a.Tarih, a.Sofor,
                        a.Tutar, a.ParaBirimi, a.Kasa ?? "", a.Aciklama, SeferNo: a.Sefer)))
                    : Task.CompletedTask, true),
                Eylem.Yap("İptal Et", SymbolRegular.Dismiss24, async () =>
                {
                    if (l.Secili is not { Iptal: false } a) return;
                    var neden = await Dialog.MetinIste("İptal nedeni:", "Avans iptali");
                    if (neden is null) return;
                    await _servis.AvansIptalAsync(a.Id, neden);
                    await YenileAsync();
                }),
            ],
        }.Aralik(DateTime.Today.AddMonths(-6), DateTime.Today)));
        Sekmeler.Add(DetaySekme.Liste("Harcamalar", () => new SekmeListeVM<MasrafSatir>("Harcamalar", Detay.MasrafKolonlari,
            async l => await App.Al<GiderServisi>().MasraflarAsync(new GiderFiltre(l.BasTarihi, l.BitTarihi, SoforId: _id)))
        { Tarihli = true }.Aralik(DateTime.Today.AddMonths(-3), DateTime.Today)));
        Sekmeler.Add(DetaySekme.Liste("Harcırahlar", () => new SekmeListeVM<HarcirahSatir>("Harcırahlar",
        [
            new("Sefer", nameof(HarcirahSatir.Sefer)), new("Ülke", nameof(HarcirahSatir.Ulke)), Kolon.Tarih("Başlangıç", nameof(HarcirahSatir.Baslangic)),
            Kolon.Tarih("Bitiş", nameof(HarcirahSatir.Bitis)), Kolon.Sayi("Gün", nameof(HarcirahSatir.Gun), true), Kolon.Para("Tutar", nameof(HarcirahSatir.Tutar)),
            new("PB", nameof(HarcirahSatir.PB)), new("Mahsuplu", "Mahsuplu?bool"), new("İptal", "Iptal?bool"),
        ], async l => await _servis.HarcirahlarAsync(l.BasTarihi, l.BitTarihi, _id)) { Tarihli = true }.Aralik(DateTime.Today.AddMonths(-6), DateTime.Today)));
        Sekmeler.Add(DetaySekme.Liste("Hakediş", () => new SekmeListeVM<TahakkukSatir>("Hakediş", HakedisListeVM.KolonListesi,
            async _ => await _servis.TahakkuklarAsync(null, _id))
        {
            Ozet = l => $"Net hakediş (iptal hariç): {l.Where(t => !t.Iptal).Sum(t => t.Net):N2} TL",
            EylemOlustur = l =>
            [
                Eylem.Yap("Ek / Kesinti", SymbolRegular.Add24, async () => { if (await SoforHesapFormlari.TahakkukAsync(_servis, _s, _id)) await YenileAsync(); }, true),
                Eylem.Yap("Hakediş Pusulası", SymbolRegular.Print24, async () =>
                {
                    var donem = l.Secili?.Donem ?? Detay.Bugun.Year * 100 + Detay.Bugun.Month;
                    await PusulaAsync(donem);
                }),
            ],
        }));
        Sekmeler.Add(DetaySekme.Liste("Zimmet", () => new SekmeListeVM<ZimmetSatir>("Zimmetler", ZimmetListeVM.KolonListesi,
            async l => await _servis.ZimmetlerAsync(_id, null, l.OnayFiltre))
        {
            Onay = "İade edilenleri de göster",
            EylemOlustur = l =>
            [
                Eylem.Yap("Zimmet Ver", SymbolRegular.Add24, async () => { if (await SoforHesapFormlari.ZimmetAsync(_s, soforId: _id)) await YenileAsync(); }, true),
                Eylem.Yap("İade Al", SymbolRegular.ArrowUndo24, async () => { if (l.Secili is { } z && await SoforHesapFormlari.ZimmetKapatAsync(_servis, z)) await YenileAsync(); }),
            ],
        }));
        Sekmeler.Add(DetaySekme.Liste("Araç Geçmişi", () => new SekmeListeVM<AtamaSatir>("Araç Geçmişi",
        [
            new("Araç", nameof(AtamaSatir.Plaka)), Kolon.Tarih("Başlangıç", nameof(AtamaSatir.Baslangic)), Kolon.Tarih("Bitiş", nameof(AtamaSatir.Bitis)),
            new("Halen", "Aktif?bool"),
        ], async _ => await _servis.AtamalarAsync(_id, null))
        {
            EylemOlustur = _ => [Eylem.Yap("Araca Ata", SymbolRegular.VehicleTruckProfile24, async () =>
            {
                if (await SoforHesapFormlari.AracAtaAsync(_servis, _s, _id, Baslik)) await YenileAsync();
            }, true)],
        }));
        if (sh.Yetkili(Izinler.SeferGor))
            Sekmeler.Add(DetaySekme.Liste("Seferler", () => new SekmeListeVM<SeferSatir>("Seferler", Detay.SeferKolonlari,
                async l => await App.Al<SeferServisi>().ListeAsync(new SeferFiltre(l.BasTarihi, l.BitTarihi, SoforId: _id)))
            { Tarihli = true, DetayIslem = s => Detay.Sefer(s.Id) }.Aralik(DateTime.Today.AddMonths(-3), DateTime.Today.AddDays(30))));
        Sekmeler.Add(DetaySekme.Liste("Evraklar", () => Detay.Evraklar(EvrakHedef.Sofor, _id, Baslik)));
    }

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        Eylem.Yap("Para Ver", SymbolRegular.ArrowUpload24, async () => { if (await SoforHesapFormlari.AvansAsync(_servis, _s, _id)) await YenileAsync(); }, true),
        Eylem.Yap("Para Al", SymbolRegular.ArrowDownload24, async () => { if (await SoforHesapFormlari.ParaAlAsync(_servis, _s, _id)) await YenileAsync(); }, true),
        Eylem.Yap("Harcama Gir", SymbolRegular.ReceiptMoney24, async () =>
        {
            var m = new Masraf { Tarih = Detay.Bugun, Merkez = MasrafMerkezi.Sefer, OdemeSekli = OdemeSekli.SoforNakit, SoforId = _id };
            if (await MasrafFormu.AcAsync(m, App.Al<GiderServisi>(), _s, App.Al<IFiloDbContextFactory>())) await YenileAsync();
        }),
        Eylem.Yap("Harcırah Yaz", SymbolRegular.CalendarClock24, async () =>
        {
            var h = new Harcirah { SoforId = _id, Baslangic = Detay.Bugun, Bitis = Detay.Bugun };
            if (await SoforHesapFormlari.HarcirahAsync(_servis, _s, h)) await YenileAsync();
        }),
        Eylem.Yap("Maaş / Hakediş Öde", SymbolRegular.Wallet24, async () =>
        {
            if (await SoforHesapFormlari.AvansAsync(_servis, _s, _id, tur: AvansTuru.MaasOdemesi)) await YenileAsync();
        }),
        Eylem.Yap("Hakediş Ek / Kesinti", SymbolRegular.Calculator24, async () => { if (await SoforHesapFormlari.TahakkukAsync(_servis, _s, _id)) await YenileAsync(); }),
        Eylem.Yap("Araca Ata", SymbolRegular.VehicleTruckProfile24, async () => { if (await SoforHesapFormlari.AracAtaAsync(_servis, _s, _id, Baslik)) await YenileAsync(); }),
        Eylem.Yap("Zimmet Ver", SymbolRegular.Key24, async () => { if (await SoforHesapFormlari.ZimmetAsync(_s, soforId: _id)) await YenileAsync(); }),
        Eylem.Yap("Cari Ekstre", SymbolRegular.DocumentText24, () => _o is null ? Task.CompletedTask : EkstrePenceresi.CariAsync(_o.CariId, _o.AdSoyad)),
        Eylem.Yap("Şoför Kartı", SymbolRegular.Person24, async () =>
        {
            var vm = App.Al<SoforListeVM>();
            if (await vm.KartAcAsync(_id)) await YenileAsync();
        }),
        Eylem.Yap("Ekler", SymbolRegular.Attach24, () => EklerPenceresi.AcAsync(EkHedef.Sofor, _id, Baslik)),
    ];

    public override IReadOnlyList<Eylem> Belgeler =>
    [
        Eylem.Yap("Şoför Ekstresi (bu ay)", SymbolRegular.DocumentPdf24, () => EkstreAsync(DateOnly.FromDateTime(Detay.AyBasi), Detay.Bugun)),
        Eylem.Yap("Şoför Ekstresi (geçen ay)", SymbolRegular.DocumentPdf24, () =>
            EkstreAsync(DateOnly.FromDateTime(Detay.AyBasi.AddMonths(-1)), DateOnly.FromDateTime(Detay.AyBasi.AddDays(-1)))),
        Eylem.Yap("Hakediş Pusulası (bu ay)", SymbolRegular.DocumentPdf24, () => PusulaAsync(Detay.Bugun.Year * 100 + Detay.Bugun.Month)),
        Eylem.Yap("Hakediş Pusulası (geçen ay)", SymbolRegular.DocumentPdf24, () =>
        {
            var g = Detay.Bugun.AddMonths(-1);
            return PusulaAsync(g.Year * 100 + g.Month);
        }),
        Eylem.Yap("Zimmet Tutanağı", SymbolRegular.DocumentPdf24, async () =>
        {
            var z = await App.Al<IKayitServisi<Zimmet>>().ListeAsync(q => q.Where(x => x.SoforId == _id && x.Durum == ZimmetDurum.Zimmette).OrderBy(x => x.VerilisTarihi));
            if (z.Count == 0) { await Dialog.Bilgi("Şoförün üzerinde zimmet yok."); return; }
            await Belge.AcAsync($"Zimmet Tutanağı {Baslik}", () => App.Al<IBelgeUretici>().ZimmetTutanagi(Baslik, z));
        }),
    ];

    private async Task EkstreAsync(DateOnly bas, DateOnly bit)
    {
        var satirlar = await _servis.DefterAsync(bas, bit, _id);
        await Belge.AcAsync($"Şoför Ekstresi {Baslik} {bas:yyyy-MM}", () => App.Al<IBelgeUretici>().SoforEkstresi(Baslik, bas, bit, satirlar));
    }

    private async Task PusulaAsync(int donem)
    {
        var satirlar = (await _servis.TahakkuklarAsync(donem, _id)).Where(t => !t.Iptal).ToList();
        if (satirlar.Count == 0) { await Dialog.Bilgi($"{donem % 100:D2}.{donem / 100} döneminde hakediş satırı yok. Önce Hakediş ekranından hesaplayın."); return; }
        var bakiye = (await _servis.OzetAsync(_id)).Bakiye;
        await Belge.AcAsync($"Hakediş {Baslik} {donem}", () => App.Al<IBelgeUretici>().HakedisPusulasi(Baslik, donem, satirlar, bakiye));
    }

    protected override async Task YukleAsync()
    {
        var o = _o = await _servis.OzetAsync(_id);
        Baslik = o.AdSoyad;
        AltBaslik = string.Join(" · ", new[] { o.Plaka is null ? "Araç atanmamış" : $"Araç: {o.Plaka}", o.Telefon }.Where(x => !string.IsNullOrWhiteSpace(x)));
        (Rozet, RozetRenk) = o.LimitAsildi ? ("Bakiye limiti aşıldı", "kirmizi") : o.OnayBekleyen > 0 ? ($"{o.OnayBekleyen} harcama onay bekliyor", "turuncu") : (null, null);
        Kpiler.Clear();
        Kpiler.Add(new Kpi("Bakiye", Para(Math.Abs(o.Bakiye)), o.Bakiye > 0 ? "Şoför borçlu (elinde para var)" : o.Bakiye < 0 ? "Şoföre borçluyuz" : "Hesap kapalı",
            o.LimitAsildi ? "kirmizi" : o.Bakiye > 0 ? "turuncu" : o.Bakiye < 0 ? "yesil" : null));
        Kpiler.Add(new Kpi("Mahsup edilmemiş avans", Para(o.AcikAvans)));
        Kpiler.Add(new Kpi("Onay bekleyen harcama", o.OnayBekleyen.ToString(), Renk: o.OnayBekleyen > 0 ? "turuncu" : null));
        Kpiler.Add(new Kpi("Bu ay sefer", o.BuAySefer.ToString()));
        Kpiler.Add(new Kpi("Bu ay hakediş", Para(o.BuAyHakedis), "Maaş + prim − kesinti"));
        Kpiler.Add(new Kpi("Zimmet", o.Zimmet.ToString(), "kart / ekipman"));
        if (o.BakiyeLimiti is > 0) Kpiler.Add(new Kpi("Bakiye limiti", Para(o.BakiyeLimiti.Value)));
    }
}

// =====================================================================================================================
// Araç
// =====================================================================================================================

public sealed class AracDetayVM : DetayVM
{
    private readonly short _id;
    private readonly DetayServisi _servis = App.Al<DetayServisi>();
    private AracOzet? _o;

    public AracDetayVM(short id)
    {
        _id = id;
        Baslik = "Araç";
        var ot = App.Al<IKullaniciOturumu>();
        Sekmeler.Add(new DetaySekme("Özet", GrafikOlustur));
        if (ot.Yetkili(Izinler.RaporFinans))
            Sekmeler.Add(DetaySekme.Liste("Araç Ekstresi", () => new SekmeListeVM<AracEkstreSatir>("Araç Gelir / Gider Ekstresi",
            [
                Kolon.Tarih("Tarih", nameof(AracEkstreSatir.Tarih)), new("Tür", nameof(AracEkstreSatir.Tur)), new("Belge", nameof(AracEkstreSatir.Belge)),
                new("Açıklama", nameof(AracEkstreSatir.Aciklama), Genislik: 280), Kolon.Para("Gelir", nameof(AracEkstreSatir.Gelir)),
                Kolon.Para("Gider", nameof(AracEkstreSatir.Gider)), Kolon.Para("Kümülatif Kâr", nameof(AracEkstreSatir.Bakiye), false),
            ], async l => await _servis.AracEkstreAsync(_id, l.BasTarihi, l.BitTarihi))
            { Tarihli = true }.Aralik(Detay.AyBasi, DateTime.Today)));
        if (ot.Yetkili(Izinler.SeferGor))
            Sekmeler.Add(DetaySekme.Liste("Seferler", () => new SekmeListeVM<SeferSatir>("Seferler", Detay.SeferKolonlari,
                async l => await App.Al<SeferServisi>().ListeAsync(new SeferFiltre(l.BasTarihi, l.BitTarihi, AracId: _id)))
            { Tarihli = true, DetayIslem = s => Detay.Sefer(s.Id) }.Aralik(DateTime.Today.AddMonths(-3), DateTime.Today.AddDays(30))));
        if (ot.Yetkili(Izinler.GiderGor))
        {
            Sekmeler.Add(DetaySekme.Liste("Masraflar", () => new SekmeListeVM<MasrafSatir>("Masraflar", Detay.MasrafKolonlari,
                async l => await App.Al<GiderServisi>().MasraflarAsync(new GiderFiltre(l.BasTarihi, l.BitTarihi, AracId: _id)))
            { Tarihli = true, Ozet = l => $"Toplam: {l.Where(x => !x.Iptal).Sum(x => x.TutarTL):N2} TL" }.Aralik(DateTime.Today.AddMonths(-3), DateTime.Today)));
            Sekmeler.Add(DetaySekme.Liste("Yakıt", () => new SekmeListeVM<YakitSatir>("Yakıt",
            [
                Kolon.TarihSaat("Zaman", nameof(YakitSatir.Zaman)), new("Sefer", nameof(YakitSatir.Sefer)), new("Şoför", nameof(YakitSatir.Sofor)),
                Kolon.Sayi("Km", nameof(YakitSatir.Km)), Kolon.Sayi("Litre", nameof(YakitSatir.Litre), true), Kolon.Para("Birim", nameof(YakitSatir.BirimFiyat), false),
                Kolon.Para("Tutar", nameof(YakitSatir.Tutar)), new("Full", "FullDepo?bool"), new("İstasyon", nameof(YakitSatir.Istasyon)), new("Ödeme", nameof(YakitSatir.OdemeAd)),
            ], async l => await App.Al<GiderServisi>().YakitlarAsync(new GiderFiltre(l.BasTarihi, l.BitTarihi, AracId: _id)))
            { Tarihli = true }.Aralik(DateTime.Today.AddMonths(-3), DateTime.Today)));
        }
        if (ot.Yetkili(Izinler.BakimGor))
            Sekmeler.Add(DetaySekme.Liste("Bakım", () => new SekmeListeVM<BakimSatir>("Bakım / Onarım",
            [
                Kolon.Tarih("Tarih", nameof(BakimSatir.Tarih)), new("Tür", nameof(BakimSatir.TurAd)), Kolon.Sayi("Km", nameof(BakimSatir.Km)),
                new("Servis", nameof(BakimSatir.Servis)), new("Açıklama", nameof(BakimSatir.Aciklama), Genislik: 260), Kolon.Para("Tutar", nameof(BakimSatir.Tutar)),
                new("İptal", "Iptal?bool"),
            ], async l => await App.Al<BakimServisi>().ListeAsync(l.BasTarihi, l.BitTarihi, _id)) { Tarihli = true }.Aralik(DateTime.Today.AddYears(-1), DateTime.Today)));
        if (ot.Yetkili(Izinler.FinansmanGor))
            Sekmeler.Add(DetaySekme.Liste("Kredi / Leasing", () => new SekmeListeVM<KrediSatir>("Krediler",
            [
                new("Banka", nameof(KrediSatir.Banka), Genislik: 170), new("Kredi No", nameof(KrediSatir.KrediNo)), new("Tür", nameof(KrediSatir.TurAd)), new("PB", nameof(KrediSatir.PB)),
                Kolon.Para("Çekilen", nameof(KrediSatir.Anapara), false), Kolon.Para("Kalan Anapara", nameof(KrediSatir.KalanAnapara), false),
                new("Taksit", nameof(KrediSatir.TaksitMetni)), Kolon.Tarih("Sonraki Vade", nameof(KrediSatir.SonrakiVade)), new("Durum", nameof(KrediSatir.DurumAd)),
            ], async _ =>
            {
                var plaka = _o?.Baslik.Plaka ?? "";
                return (await App.Al<KrediServisi>().ListeAsync(true)).Where(k => k.Araclar is { } a && a.Split(", ").Contains(plaka));
            }) { DetayIslem = k => KrediIslemleri.DetayAc(k.Id) }));
        if (ot.Yetkili(Izinler.SoforHesapGor))
            Sekmeler.Add(DetaySekme.Liste("Şoför Hareketleri", () => new SekmeListeVM<SoforDefterSatir>("Bu araçla ilgili şoför hareketleri", Detay.DefterKolonlari(true),
                async l => await App.Al<SoforHesapServisi>().DefterAsync(l.BasTarihi, l.BitTarihi, null, _id))
            { Tarihli = true }.Aralik(Detay.AyBasi, DateTime.Today)));
        Sekmeler.Add(DetaySekme.Liste("Şoför Geçmişi", () => new SekmeListeVM<AtamaSatir>("Şoför Geçmişi",
        [
            new("Şoför", nameof(AtamaSatir.Sofor)), Kolon.Tarih("Başlangıç", nameof(AtamaSatir.Baslangic)), Kolon.Tarih("Bitiş", nameof(AtamaSatir.Bitis)), new("Halen", "Aktif?bool"),
        ], async _ => await App.Al<SoforHesapServisi>().AtamalarAsync(null, _id))));
        Sekmeler.Add(DetaySekme.Liste("Km Geçmişi", () => new SekmeListeVM<KmSatir>("Km Okumaları",
        [
            Kolon.TarihSaat("Zaman", nameof(KmSatir.Zaman)), Kolon.Sayi("Km", nameof(KmSatir.Km)), new("Kaynak", nameof(KmSatir.KaynakAd)),
            Kolon.Sayi("Fark", nameof(KmSatir.Fark)), new("Anormal (geri)", "Anormal?bool"),
        ], async _ => await _servis.KmGecmisiAsync(_id))));
        Sekmeler.Add(DetaySekme.Liste("Evraklar", () => Detay.Evraklar(EvrakHedef.Arac, _id, Baslik)));
    }

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        Eylem.Yap("Araç Kartı", SymbolRegular.Edit24, async () => { if (await App.Al<AracListeVM>().KartAcAsync(_id)) await YenileAsync(); }, true),
        Eylem.Yap("Zimmet Ver", SymbolRegular.Key24, async () => { if (await SoforHesapFormlari.ZimmetAsync(App.Al<Secimler>(), aracId: _id)) await YenileAsync(); }),
        Eylem.Yap("Ekler", SymbolRegular.Attach24, () => EklerPenceresi.AcAsync(EkHedef.Arac, _id, Baslik)),
    ];

    protected override async Task YukleAsync()
    {
        var yilBas = new DateOnly(Detay.Bugun.Year, 1, 1);
        var o = _o = await _servis.AracOzetAsync(_id, yilBas, Detay.Bugun);
        var b = o.Baslik;
        Baslik = b.Plaka;
        AltBaslik = string.Join(" · ", new[] { b.MarkaModel, b.Yil?.ToString(), b.Sofor is null ? "Şoför yok" : $"Şoför: {b.Sofor}",
            b.Dorse is null ? null : $"Dorse: {b.Dorse}", $"{b.Km:N0} km" }.Where(x => !string.IsNullOrWhiteSpace(x)));
        Rozet = b.Durum;
        RozetRenk = b.Durum.StartsWith("Seferde") ? "mavi" : b.Aktif ? "yesil" : null;
        Kpiler.Clear();
        if (o.Donem is { } d)
        {
            Kpiler.Add(new Kpi("Gelir (bu yıl)", Para0(d.Gelir), $"{d.SeferSayisi} sefer"));
            Kpiler.Add(new Kpi("Gider (bu yıl)", Para0(d.ToplamGider), $"Sabit {d.SabitGider:N0} · değişken {d.DegiskenGider:N0}"));
            Kpiler.Add(new Kpi("Net kâr", Para0(d.Kar), d.Marj is { } m ? $"Marj %{m:0.#}" : null, d.Kar < 0 ? "kirmizi" : "yesil"));
            Kpiler.Add(new Kpi("Km", $"{d.Km:N0}", d.BosKmOrani is { } bk ? $"Boş km %{bk:0.#}" : null));
            Kpiler.Add(new Kpi("Km başı maliyet", d.KmBasiMaliyet is { } km ? $"{km:N2} ₺" : "—", d.KmBasiGelir is { } kg ? $"Km başı gelir {kg:N2} ₺" : null));
        }
        else
        {
            Kpiler.Add(new Kpi("Güncel km", $"{b.Km:N0}"));
        }
        if (o.LitreYuzKm is { } l) Kpiler.Add(new Kpi("Ortalama tüketim", $"{l:N1} L/100km"));
        if (o.KalanKrediTL > 0) Kpiler.Add(new Kpi("Kalan kredi (TL)", Para0(o.KalanKrediTL), "Araç payına düşen", "turuncu"));
    }

    private object GrafikOlustur()
    {
        if (_o is null || _o.Trend.Count == 0) return Grafik.Bos("Kârlılık verisi için 'Finans raporları' yetkisi gerekir.");
        var e = _o.Trend.Select(t => t.Donem).ToList();
        var kartlar = new List<System.Windows.FrameworkElement>
        {
            Grafik.Kart("Son 12 ay gelir / gider / kâr (TL)", Grafik.GelirGiderKar(e, _o.Trend.Select(t => t.Gelir), _o.Trend.Select(t => t.Gider), _o.Trend.Select(t => t.Kar)), 300),
        };
        if (_o.GiderDagilimi.Count > 0) kartlar.Add(Grafik.Kart("Bu yıl gider dağılımı", Grafik.Pasta(_o.GiderDagilimi), 300));
        return Grafik.Izgara(2, [.. kartlar]);
    }
}

// =====================================================================================================================
// Cari
// =====================================================================================================================

public sealed class CariDetayVM : DetayVM
{
    private readonly int _id;
    private readonly DetayServisi _servis = App.Al<DetayServisi>();
    private CariOzet? _o;

    public CariDetayVM(int id)
    {
        _id = id;
        Baslik = "Cari";
        var finans = App.Al<FinansServisi>();
        Sekmeler.Add(DetaySekme.Liste("Ekstre", () => new SekmeListeVM<EkstreSatir>("Cari Ekstre",
        [
            Kolon.Tarih("Tarih", nameof(EkstreSatir.Tarih)), Kolon.Tarih("Vade", nameof(EkstreSatir.Vade)), new("İşlem", nameof(EkstreSatir.Islem)),
            new("Açıklama", nameof(EkstreSatir.Aciklama), Genislik: 300), Kolon.Para("Borç", nameof(EkstreSatir.Borc)), Kolon.Para("Alacak", nameof(EkstreSatir.Alacak)),
            Kolon.Para("Bakiye", nameof(EkstreSatir.Bakiye), false), new("Döviz", nameof(EkstreSatir.Doviz)), new("İptal", "Iptal?bool"),
        ], async l => (await finans.CariEkstreAsync(_id, l.BasTarihi, l.BitTarihi)).Satirlar) { Tarihli = true }.Aralik(DateTime.Today.AddMonths(-3), DateTime.Today)));
        Sekmeler.Add(DetaySekme.Liste("Faturalar", () => new SekmeListeVM<FaturaSatir>("Faturalar",
        [
            new("Tip", nameof(FaturaSatir.TipAd)), new("Fatura No", nameof(FaturaSatir.FaturaNo)), Kolon.Tarih("Tarih", nameof(FaturaSatir.Tarih)),
            Kolon.Tarih("Vade", nameof(FaturaSatir.Vade)), Kolon.Para("Ara Toplam", nameof(FaturaSatir.AraToplam)), Kolon.Para("KDV", nameof(FaturaSatir.Kdv)),
            Kolon.Para("Genel Toplam", nameof(FaturaSatir.GenelToplam)), new("PB", nameof(FaturaSatir.PB)), new("Durum", nameof(FaturaSatir.DurumAd)),
        ], async l => await App.Al<FaturaServisi>().ListeAsync(l.BasTarihi, l.BitTarihi, cariId: _id)) { Tarihli = true }.Aralik(DateTime.Today.AddYears(-1), DateTime.Today)));
        if (App.Al<IKullaniciOturumu>().Yetkili(Izinler.SeferGor))
            Sekmeler.Add(DetaySekme.Liste("Seferler", () => new SekmeListeVM<SeferSatir>("Seferler", Detay.SeferKolonlari,
                async l => await App.Al<SeferServisi>().ListeAsync(new SeferFiltre(l.BasTarihi, l.BitTarihi, MusteriId: _id)))
            { Tarihli = true, DetayIslem = s => Detay.Sefer(s.Id) }.Aralik(DateTime.Today.AddMonths(-3), DateTime.Today.AddDays(30))));
        Sekmeler.Add(new DetaySekme("Yaşlandırma", () => _o is null ? Grafik.Bos() : Grafik.Kart("Açık bakiyenin vadeye göre dağılımı (TL)", Grafik.Sutun(
            ["Vadesi gelmemiş", "0–30 gün", "31–60 gün", "61–90 gün", "90+ gün"],
            ("Tutar", new[] { _o.Yaslandirma.VadesiGelmemis, _o.Yaslandirma.Gun0_30, _o.Yaslandirma.Gun31_60, _o.Yaslandirma.Gun61_90, _o.Yaslandirma.Gun90Ustu }, Grafik.Ikincil)), 320)));
    }

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        Eylem.Yap("Tahsilat", SymbolRegular.ArrowDownload24, async () => { if (await App.Al<CariListeVM>().TahsilatOdemeIdAsync(_id, Baslik, true)) await YenileAsync(); }, true),
        Eylem.Yap("Ödeme", SymbolRegular.ArrowUpload24, async () => { if (await App.Al<CariListeVM>().TahsilatOdemeIdAsync(_id, Baslik, false)) await YenileAsync(); }, true),
        Eylem.Yap("Cari Kartı", SymbolRegular.Edit24, async () => { if (await App.Al<CariListeVM>().KartAcAsync(_id)) await YenileAsync(); }),
        Eylem.Yap("Ekler", SymbolRegular.Attach24, () => EklerPenceresi.AcAsync(EkHedef.Cari, _id, Baslik)),
    ];

    public override IReadOnlyList<Eylem> Belgeler =>
    [
        Eylem.Yap("Cari Mutabakat Mektubu", SymbolRegular.DocumentPdf24, async () =>
        {
            if (_o is null) return;
            await Belge.AcAsync($"Mutabakat {Baslik}", () => App.Al<IBelgeUretici>().CariMutabakat(_o, Detay.Bugun));
        }),
    ];

    protected override async Task YukleAsync()
    {
        var o = _o = await _servis.CariOzetAsync(_id);
        Baslik = o.Unvan;
        AltBaslik = string.Join(" · ", new[] { o.Tip.Ad(), o.Telefon }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var riskAsildi = o.RiskLimiti is > 0 && o.Bakiye > o.RiskLimiti;
        (Rozet, RozetRenk) = riskAsildi ? ("Risk limiti aşıldı", "kirmizi") : o.VadesiGecen > 0 ? ("Vadesi geçen alacak var", "turuncu") : (null, null);
        Kpiler.Clear();
        Kpiler.Add(new Kpi("Bakiye", Para(Math.Abs(o.Bakiye)), o.Bakiye > 0 ? "Cari bize borçlu" : o.Bakiye < 0 ? "Biz cariye borçluyuz" : "Kapalı",
            o.Bakiye > 0 ? "turuncu" : null));
        Kpiler.Add(new Kpi("Vadesi geçen", Para(o.VadesiGecen), Renk: o.VadesiGecen > 0 ? "kirmizi" : "yesil"));
        Kpiler.Add(new Kpi("Ort. tahsil süresi", o.OrtTahsilGun is { } g ? $"{g:N0} gün" : "—", "Son 90 gün satışa göre"));
        Kpiler.Add(new Kpi("Bu yıl ciro", Para0(o.BuYilCiro), "KDV hariç"));
        if (o.RiskLimiti is > 0) Kpiler.Add(new Kpi("Risk limiti", Para0(o.RiskLimiti.Value), Renk: riskAsildi ? "kirmizi" : null));
    }
}

// =====================================================================================================================
// Sefer
// =====================================================================================================================

public sealed record DurakGorunum(string Durak, string Tip, DateTime? Plan, DateTime? Gercek);

public sealed class SeferDetayVM : DetayVM
{
    private readonly int _id;
    private readonly DetayServisi _servis = App.Al<DetayServisi>();
    private SeferOzet? _o;

    public SeferDetayVM(int id)
    {
        _id = id;
        Baslik = "Sefer";
        var ot = App.Al<IKullaniciOturumu>();
        Sekmeler.Add(DetaySekme.Liste("Güzergah", () => new SekmeListeVM<DurakGorunum>("Duraklar",
        [
            new("Durak", nameof(DurakGorunum.Durak), Genislik: 260), new("Tip", nameof(DurakGorunum.Tip)),
            Kolon.TarihSaat("Planlanan", nameof(DurakGorunum.Plan)), Kolon.TarihSaat("Gerçekleşen", nameof(DurakGorunum.Gercek)),
        ], _ => Task.FromResult(Duraklar()))
        {
            EylemOlustur = _ => [Eylem.Yap("Durakları Düzenle", SymbolRegular.Location24, () => Calistir(SeferEylemi.Duraklar), true)],
        }));
        if (ot.Yetkili(Izinler.RaporFinans))
            Sekmeler.Add(new DetaySekme("Kârlılık", KarlilikOlustur));
        if (ot.Yetkili(Izinler.GiderGor))
            Sekmeler.Add(DetaySekme.Liste("Masraflar", () => new SekmeListeVM<MasrafSatir>("Sefer Masrafları", Detay.MasrafKolonlari,
                async _ => await App.Al<GiderServisi>().MasraflarAsync(new GiderFiltre(DateOnly.MinValue, DateOnly.MaxValue, SeferId: _id)))
            {
                Ozet = l => $"Toplam: {l.Where(x => !x.Iptal).Sum(x => x.TutarTL):N2} TL",
                EylemOlustur = _ => [Eylem.Yap("Masraf Ekle", SymbolRegular.Add24, () => Calistir(SeferEylemi.Masraf), true)],
            }));
        if (ot.Yetkili(Izinler.SoforHesapGor))
            Sekmeler.Add(DetaySekme.Liste("Şoför Hareketleri", () => new SekmeListeVM<SoforDefterSatir>("Sefere bağlı şoför hareketleri (avans, harcırah, mahsup...)",
                Detay.DefterKolonlari(true), async _ =>
                {
                    var tarih = _o?.Sefer.PlanlananTarih ?? Detay.Bugun;
                    var no = _o?.Sefer.SeferNo;
                    return (await App.Al<SoforHesapServisi>().DefterAsync(tarih.AddDays(-30), Detay.Bugun.AddDays(1))).Where(x => x.SeferNo == no);
                })
            {
                EylemOlustur = _ =>
                [
                    Eylem.Yap("Avans Ver", SymbolRegular.MoneyHand24, () => Calistir(SeferEylemi.Avans), true),
                    Eylem.Yap("Harcırah", SymbolRegular.CalendarClock24, () => Calistir(SeferEylemi.Harcirah)),
                ],
            }));
    }

    /// <summary>Çıkış → ara duraklar → varış zaman çizelgesi.</summary>
    private IEnumerable<DurakGorunum> Duraklar()
    {
        if (_o is null) return [];
        var s = _o.Sefer;
        var uclar = _o.Guzergah.Split(" → ");
        var liste = new List<DurakGorunum> { new(uclar[0], "Çıkış", s.PlanlananTarih.ToDateTime(TimeOnly.MinValue), s.CikisTarihi) };
        liste.AddRange(_o.Duraklar.Select(d => new DurakGorunum(d.Durak, d.Tip.Ad(), d.Plan, d.Gercek)));
        liste.Add(new DurakGorunum(uclar.Length > 1 ? uclar[^1] : "", "Varış / teslim", null, s.TeslimTarihi));
        if (s.DonusTarihi is not null) liste.Add(new DurakGorunum("Garaja dönüş", "Dönüş", null, s.DonusTarihi));
        return liste;
    }

    private async Task Calistir(SeferEylemi e)
    {
        await App.Al<SeferListeVM>().SeferIcinAsync(_id, _o?.Sefer.SeferNo ?? "", e);
        await YenileAsync();
    }

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        Eylem.Yap("Düzenle", SymbolRegular.Edit24, async () => { if (await App.Al<SeferListeVM>().DuzenleIdAsync(_id)) await YenileAsync(); }, true),
        Eylem.Yap("Yola Çıktı", SymbolRegular.Play24, () => Calistir(SeferEylemi.YolaCikti)),
        Eylem.Yap("Teslim Edildi", SymbolRegular.CheckboxChecked24, () => Calistir(SeferEylemi.TeslimEdildi)),
        Eylem.Yap("Kapat", SymbolRegular.LockClosed24, () => Calistir(SeferEylemi.Kapat)),
        Eylem.Yap("Yeniden Aç", SymbolRegular.ArrowUndo24, () => Calistir(SeferEylemi.YenidenAc)),
        Eylem.Yap("İptal Et", SymbolRegular.Dismiss24, () => Calistir(SeferEylemi.Iptal)),
        Eylem.Yap("Masraf Ekle", SymbolRegular.ReceiptMoney24, () => Calistir(SeferEylemi.Masraf)),
        Eylem.Yap("Avans Ver", SymbolRegular.MoneyHand24, () => Calistir(SeferEylemi.Avans)),
        Eylem.Yap("Harcırah", SymbolRegular.CalendarClock24, () => Calistir(SeferEylemi.Harcirah)),
        Eylem.Yap("Müşteri Detayı", SymbolRegular.People24, () => _o is null ? Task.CompletedTask : Detay.Cari(_o.Sefer.MusteriCariId)),
        Eylem.Yap("Araç Detayı", SymbolRegular.VehicleTruckProfile24, () => _o?.Sefer.AracId is { } a ? Detay.Arac(a) : Task.CompletedTask),
        Eylem.Yap("Ekler", SymbolRegular.Attach24, () => EklerPenceresi.AcAsync(EkHedef.Sefer, _id, Baslik)),
    ];

    public override IReadOnlyList<Eylem> Belgeler =>
    [
        Eylem.Yap("Sefer Föyü", SymbolRegular.DocumentPdf24, async () =>
        {
            if (_o is null) return;
            var masraflar = App.Al<IKullaniciOturumu>().Yetkili(Izinler.GiderGor)
                ? await App.Al<GiderServisi>().MasraflarAsync(new GiderFiltre(DateOnly.MinValue, DateOnly.MaxValue, SeferId: _id)) : [];
            var avanslar = App.Al<IKullaniciOturumu>().Yetkili(Izinler.SoforHesapGor)
                ? (await App.Al<SoforHesapServisi>().AvanslarAsync(_o.Sefer.PlanlananTarih.AddDays(-30), Detay.Bugun.AddDays(1))).Where(a => a.Sefer == _o.Sefer.SeferNo).ToList() : [];
            await Belge.AcAsync($"Sefer Föyü {_o.Sefer.SeferNo}", () => App.Al<IBelgeUretici>().SeferFoyu(_o, masraflar, avanslar));
        }),
    ];

    protected override async Task YukleAsync()
    {
        var o = _o = await _servis.SeferOzetAsync(_id);
        var s = o.Sefer;
        Baslik = $"{s.SeferNo} — {o.Musteri}";
        AltBaslik = $"{o.Guzergah} · {s.PlanlananTarih:dd.MM.yyyy} · {s.Kapsam.Ad()} · Araç: {o.Plaka ?? "-"}{(o.Dorse is null ? "" : " / " + o.Dorse)} · Şoför: {o.Soforler}" +
                    (s.YukTanimi is null ? "" : $" · Yük: {s.YukTanimi}");
        Rozet = s.Durum.Ad();
        RozetRenk = s.Durum switch { SeferDurum.Iptal => "kirmizi", SeferDurum.Kapatildi => "yesil", SeferDurum.Yolda or SeferDurum.Yuklemede => "mavi", _ => "turuncu" };
        Kpiler.Clear();
        if (o.Karlilik is { } k)
        {
            Kpiler.Add(new Kpi("Navlun (TL)", Para(k.GelirTL), s.NavlunParaBirimi == ParaBirimi.TRY ? "KDV hariç" : $"{s.NavlunTutar:N2} {s.NavlunParaBirimi.Ad()}"));
            Kpiler.Add(new Kpi("Gider", Para(k.GiderTL), $"{k.Giderler.Count} kalem"));
            Kpiler.Add(new Kpi("Kâr", Para(k.KarTL), k.KarMarji is { } m ? $"Marj %{m:0.#}" : null, k.KarTL < 0 ? "kirmizi" : "yesil"));
            if (k.KmBasiMaliyet is { } km) Kpiler.Add(new Kpi("Km başı maliyet", $"{km:N2} ₺", $"{k.Km:N0} km"));
        }
        else if (s.DonusKm - s.CikisKm is { } km2) Kpiler.Add(new Kpi("Km", $"{km2:N0}"));
        if (o.Sure is { } sure) Kpiler.Add(new Kpi("Süre", sure.TotalDays >= 1 ? $"{sure.TotalDays:0.#} gün" : $"{sure.TotalHours:0} saat"));
        Kpiler.Add(new Kpi("Fatura", o.FaturaNo ?? "Faturalanmadı", Renk: o.FaturaNo is null && s.Durum >= SeferDurum.TeslimEdildi ? "turuncu" : null));
    }

    private object KarlilikOlustur()
    {
        if (_o?.Karlilik is not { } k) return Grafik.Bos();
        if (k.Giderler.Count == 0) return Grafik.Bos("Bu sefere gider kaydı yok.");
        return Grafik.Izgara(2,
            Grafik.Kart("Gider dağılımı (TL)", Grafik.Pasta(k.Giderler.Select(g => (g.Kalem, g.TutarTL))), 300),
            Grafik.Kart("Gelir / gider / kâr (TL)", Grafik.Sutun(["Navlun", "Gider", "Kâr"], ("Tutar", new[] { k.GelirTL, k.GiderTL, k.KarTL }, Grafik.Ana)), 300));
    }
}

// =====================================================================================================================
// Şoför listeleri: hakediş, zimmet, günlük kasa
// =====================================================================================================================

public sealed class HakedisListeVM : ListeVM<TahakkukSatir>
{
    private readonly SoforHesapServisi _servis;
    private readonly Secimler _s;
    private readonly IKullaniciOturumu _oturum;

    public HakedisListeVM(IDisaAktarici d, SoforHesapServisi servis, Secimler s, IKullaniciOturumu oturum) : base(d)
    {
        _servis = servis;
        _s = s;
        _oturum = oturum;
        BasTarih = Detay.AyBasi.AddMonths(-1);
        BitTarih = DateTime.Today;
    }

    public static IReadOnlyList<Kolon> KolonListesi { get; } =
    [
        new("Dönem", nameof(TahakkukSatir.DonemAd)), new("Şoför", nameof(TahakkukSatir.Sofor), Genislik: 160), new("Tür", nameof(TahakkukSatir.TurAd)),
        Kolon.Para("Tutar", nameof(TahakkukSatir.Tutar), false), Kolon.Para("Net", nameof(TahakkukSatir.Net)), new("Sefer", nameof(TahakkukSatir.SeferNo)),
        new("Araç", nameof(TahakkukSatir.Plaka)), new("Açıklama", nameof(TahakkukSatir.Aciklama), Genislik: 240), new("İptal", "Iptal?bool"),
    ];

    public override string Baslik => "Şoför Hakedişleri (maaş + prim − kesinti)";
    public override bool TarihFiltresi => true;
    public override IReadOnlyList<Kolon> Kolonlar => KolonListesi;
    public override bool YeniVar => _oturum.Yetkili(Izinler.SoforHesapOnay);
    public override bool SilVar => _oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "İptal Et";

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Dönem Hakedişini Hesapla", SymbolRegular.Calculator24, HesaplaAsync, true),
        E("Hakediş Pusulası", SymbolRegular.Print24, async () =>
        {
            if (Secili is not { } t) return;
            var tum = (await _servis.TahakkuklarAsync(t.Donem, null)).Where(x => x.Sofor == t.Sofor && !x.Iptal).ToList();
            var soforId = (await _s.SoforlerAsync(false)).FirstOrDefault(x => x.Ad == t.Sofor)?.Id;
            var bakiye = soforId is { } sid ? (await _servis.OzetAsync((short)sid)).Bakiye : 0;
            await Belge.AcAsync($"Hakediş {t.Sofor} {t.Donem}", () => App.Al<IBelgeUretici>().HakedisPusulasi(t.Sofor, t.Donem, tum, bakiye));
        }),
    ];

    protected override async Task<IEnumerable<TahakkukSatir>> YukleAsync()
    {
        var b = Bas.Year * 100 + Bas.Month;
        var e = Bit.Year * 100 + Bit.Month;
        return (await _servis.TahakkuklarAsync(null, null)).Where(t => t.Donem >= b && t.Donem <= e);
    }

    protected override string? OzetHesapla(IReadOnlyList<TahakkukSatir> l) =>
        $"Net hakediş: {l.Where(t => !t.Iptal).Sum(t => t.Net):N2} TL   •   Kesinti: {l.Where(t => !t.Iptal && t.Tur == TahakkukTuru.Kesinti).Sum(t => t.Tutar):N2} TL";

    protected override async Task YeniAsync()
    {
        if (await SoforHesapFormlari.TahakkukAsync(_servis, _s)) await YenileAsync();
    }

    protected override async Task SilAsync(TahakkukSatir t)
    {
        if (t.Iptal) return;
        var neden = await Dialog.MetinIste("Hakediş satırı iptal edilecek (şoför carisi ters kayıtla düzeltilir). İptal nedeni:", "Hakediş iptali");
        if (neden is null) return;
        await _servis.TahakkukIptalAsync(t.Id, neden);
        await YenileAsync();
    }

    private sealed class DonemModel
    {
        public DateOnly Donem { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
    }

    /// <summary>Dönem için maaş + sefer/km primi önerisini hesaplar; seçilenler tahakkuk olarak yazılır.</summary>
    private async Task HesaplaAsync()
    {
        var m = new DonemModel();
        if (!FormPenceresi.Ac("Hakediş dönemi", m, [new FormAlani { Etiket = "Dönem (ayın herhangi bir günü)", Yol = nameof(DonemModel.Donem), Tip = AlanTipi.Tarih, Zorunlu = true }],
                _ => Task.CompletedTask)) return;
        var donem = m.Donem.Year * 100 + m.Donem.Month;
        var oneriler = await _servis.HakedisOnerAsync(donem);
        if (oneriler.Count == 0)
        {
            await Dialog.Bilgi("Bu dönem için yeni hakediş önerisi yok. Şoför kartlarında maaş / prim tanımlı olmalı; yazılmış satırlar tekrar önerilmez.");
            return;
        }
        var secilen = SecimPenceresi.Sec($"{donem % 100:D2}.{donem / 100} hakediş önerisi — yazılacakları seçin", oneriler,
        [
            new("Şoför", nameof(HakedisOneri.Sofor), Genislik: 160), new("Tür", nameof(HakedisOneri.TurAd)), Kolon.Para("Tutar", nameof(HakedisOneri.Tutar)),
            new("Sefer", nameof(HakedisOneri.SeferNo)), new("Açıklama", nameof(HakedisOneri.Aciklama), Genislik: 260),
        ]);
        if (secilen.Count == 0) return;
        if (!await Dialog.Calistir(() => _servis.TahakkukYazAsync(donem, secilen.Select(o => new PersonelTahakkuk
            {
                SoforId = o.SoforId, Tur = o.Tur, Tutar = o.Tutar, SeferId = o.SeferId, AracId = o.AracId, Aciklama = o.Aciklama,
            }).ToList()))) return;
        AnaPencere.Basarili($"{secilen.Count} hakediş satırı yazıldı ({secilen.Sum(x => x.Tutar):N2} TL).");
        await YenileAsync();
    }
}

public sealed class ZimmetListeVM(IDisaAktarici d, SoforHesapServisi servis, Secimler s, IKullaniciOturumu oturum) : ListeVM<ZimmetSatir>(d)
{
    public static IReadOnlyList<Kolon> KolonListesi { get; } =
    [
        new("Tür", nameof(ZimmetSatir.TurAd)), new("Ad", nameof(ZimmetSatir.Ad), Genislik: 180), new("Seri / Kart No", nameof(ZimmetSatir.SeriNo)),
        new("Şoför", nameof(ZimmetSatir.Sofor)), new("Araç", nameof(ZimmetSatir.Plaka)), Kolon.Tarih("Veriliş", nameof(ZimmetSatir.Verilis)),
        Kolon.Tarih("İade", nameof(ZimmetSatir.Iade)), new("Durum", nameof(ZimmetSatir.DurumAd)), Kolon.Para("Değer", nameof(ZimmetSatir.Deger)),
        new("Açıklama", nameof(ZimmetSatir.Aciklama), Genislik: 200),
    ];

    public override string Baslik => "Zimmetler (yakıt kartı, HGS, telefon, ekipman)";
    public override IReadOnlyList<Kolon> Kolonlar => KolonListesi;
    public override string? OnayFiltreMetni => "İade edilenleri de göster";
    public override bool YeniVar => oturum.Yetkili(Izinler.SoforHesapYaz);
    public override bool DuzenleVar => oturum.Yetkili(Izinler.SoforHesapYaz);

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("İade Al", SymbolRegular.ArrowUndo24, async () => { if (Secili is { } z && await SoforHesapFormlari.ZimmetKapatAsync(servis, z)) await YenileAsync(); }, true),
    ];

    protected override async Task<IEnumerable<ZimmetSatir>> YukleAsync() => await servis.ZimmetlerAsync(null, null, OnayFiltre);

    protected override async Task YeniAsync()
    {
        if (await SoforHesapFormlari.ZimmetAsync(s)) await YenileAsync();
    }

    protected override async Task DuzenleAsync(ZimmetSatir satir)
    {
        var z = await App.Al<IKayitServisi<Zimmet>>().GetirAsync(satir.Id);
        if (z is not null && await SoforHesapFormlari.ZimmetAsync(s, z)) await YenileAsync();
    }
}

/// <summary>Şoför kasası — günlük: seçilen günlerde tüm şoförlere verilen / alınan paralar.</summary>
public sealed class SoforGunlukVM : ListeVM<SoforDefterSatir>
{
    private readonly SoforHesapServisi _servis;
    private readonly Secimler _s;
    private readonly IKullaniciOturumu _oturum;

    public SoforGunlukVM(IDisaAktarici d, SoforHesapServisi servis, Secimler s, IKullaniciOturumu oturum) : base(d)
    {
        _servis = servis;
        _s = s;
        _oturum = oturum;
        BasTarih = DateTime.Today;
        BitTarih = DateTime.Today;
    }

    public override string Baslik => "Şoför Kasası — Günlük Hareketler";
    public override bool TarihFiltresi => true;
    public override bool DetayVar => true;
    public override IReadOnlyList<Kolon> Kolonlar { get; } = Detay.DefterKolonlari(true);

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Para Ver", SymbolRegular.ArrowUpload24, async () => { if (await SoforHesapFormlari.AvansAsync(_servis, _s, Secili?.SoforId)) await YenileAsync(); }, true),
        E("Para Al", SymbolRegular.ArrowDownload24, async () => { if (await SoforHesapFormlari.ParaAlAsync(_servis, _s, Secili?.SoforId)) await YenileAsync(); }, true),
        E("İşlemi İptal Et", SymbolRegular.Dismiss24, IptalAsync),
    ];

    protected override async Task<IEnumerable<SoforDefterSatir>> YukleAsync() => await _servis.DefterAsync(Bas, Bit);
    protected override Task DetayAsync(SoforDefterSatir satir) => Detay.Sofor(satir.SoforId);

    protected override string? OzetHesapla(IReadOnlyList<SoforDefterSatir> l)
    {
        var g = l.Where(x => !x.Iptal).ToList();
        return $"Verilen: {g.Sum(x => x.Verilen):N2}   •   Alınan: {g.Sum(x => x.Alinan):N2}   •   Net: {g.Sum(x => x.Verilen - x.Alinan):N2} TL   •   " +
               $"{g.Select(x => x.SoforId).Distinct().Count()} şoför";
    }

    private async Task IptalAsync()
    {
        if (Secili is not { Iptal: false } s || s.KaynakId is not { } no) return;
        if (s.KaynakTip == KaynakTip.SoforTahsilat)
        {
            if (!_oturum.Yetkili(Izinler.FinansIptal)) { await Dialog.Hata("İptal yetkiniz yok."); return; }
            var neden = await Dialog.MetinIste("İşlem iptal edilecek (ters kayıt). İptal nedeni:", "İptal");
            if (neden is null) return;
            await _servis.ParaAlIptalAsync(no, neden);
        }
        else if (s.KaynakTip == KaynakTip.Avans)
        {
            var neden = await Dialog.MetinIste("Avans iptal edilecek. İptal nedeni:", "Avans iptali");
            if (neden is null) return;
            await _servis.AvansIptalAsync(no, neden);
        }
        else
        {
            await Dialog.Bilgi($"Bu satır '{s.Islem}' kaydından geliyor; ilgili ekrandan iptal edilmelidir.");
            return;
        }
        AnaPencere.Basarili("İşlem iptal edildi.");
        await YenileAsync();
    }
}
