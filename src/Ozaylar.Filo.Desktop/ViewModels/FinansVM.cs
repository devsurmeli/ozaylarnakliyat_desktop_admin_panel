using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed class CariListeVM(IDisaAktarici d, CariServisi servis, Secimler s, FinansServisi finans, IKullaniciOturumu oturum, DovizServisi doviz)
    : KayitListeVM<CariBakiyeSatir, Cari>(d, servis, s)
{
    public override string Baslik => "Cariler";
    protected override string KayitAdi => "Cari";
    public override string? OnayFiltreMetni => "Yalnızca bakiyesi olanlar";
    protected override double FormGenislik => 900;
    protected override object Id(CariBakiyeSatir s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Kod", nameof(CariBakiyeSatir.Kod)), new("Ünvan", nameof(CariBakiyeSatir.Unvan), Genislik: 280), new("Tip", nameof(CariBakiyeSatir.Tip)),
        new("Telefon", nameof(CariBakiyeSatir.Telefon)), Kolon.Para("Borç", nameof(CariBakiyeSatir.Borc)), Kolon.Para("Alacak", nameof(CariBakiyeSatir.Alacak)),
        Kolon.Para("Bakiye", nameof(CariBakiyeSatir.Bakiye)), new("Durum", nameof(CariBakiyeSatir.Durum)), new("Risk Aşıldı", "RiskAsildi?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Ekstre", SymbolRegular.DocumentText24, () => Secili is { } c ? EkstrePenceresi.CariAsync(c.Id, c.Unvan) : Task.CompletedTask),
        E("Tahsilat", SymbolRegular.ArrowDownload24, () => TahsilatOdemeAsync(true)),
        E("Ödeme", SymbolRegular.ArrowUpload24, () => TahsilatOdemeAsync(false)),
        E("Açılış / Düzeltme", SymbolRegular.Edit24, ManuelAsync),
        E("Yaşlandırma", SymbolRegular.CalendarClock24, YaslandirmaAsync),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } c ? EklerPenceresi.AcAsync(EkHedef.Cari, c.Id, c.Unvan) : Task.CompletedTask),
    ];

    protected override async Task<IEnumerable<CariBakiyeSatir>> YukleAsync() => await finans.CariListesiAsync(sadeceBakiyeli: OnayFiltre);
    protected override string? OzetHesapla(IReadOnlyList<CariBakiyeSatir> l) =>
        $"Alacaklarımız: {l.Where(c => c.Bakiye > 0).Sum(c => c.Bakiye):N2}   •   Borçlarımız: {-l.Where(c => c.Bakiye < 0).Sum(c => c.Bakiye):N2}";

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(Cari m, bool yeni) =>
    [
        new() { Etiket = "Kod (boş = otomatik)", Yol = nameof(Cari.Kod), MaksUzunluk = 20 },
        new() { Etiket = "Ünvan", Yol = nameof(Cari.Unvan), Zorunlu = true, MaksUzunluk = 200 },
        new() { Etiket = "Cari tipi", Yol = nameof(Cari.CariTipi), Tip = AlanTipi.Bayraklar, Secenekler = EnumMetin.Liste<CariTipi>().Where(x => x.Deger != CariTipi.Personel).ToList() },
        new() { Etiket = "Vergi dairesi", Yol = nameof(Cari.VergiDairesi), MaksUzunluk = 60 },
        new() { Etiket = "VKN / TCKN", Yol = nameof(Cari.VergiNo), MaksUzunluk = 11 },
        new() { Etiket = "e-Fatura mükellefi", Yol = nameof(Cari.EFaturaMukellefi), Tip = AlanTipi.Onay },
        new() { Etiket = "Yetkili", Yol = nameof(Cari.Yetkili), MaksUzunluk = 100 },
        new() { Etiket = "Telefon", Yol = nameof(Cari.Telefon), MaksUzunluk = 20 },
        new() { Etiket = "E-posta", Yol = nameof(Cari.Eposta), MaksUzunluk = 100 },
        new() { Etiket = "Ülke", Yol = nameof(Cari.UlkeId), Tip = AlanTipi.Secim, Secenekler = await Secimler.UlkelerAsync() },
        new() { Etiket = "İl", Yol = nameof(Cari.IlId), Tip = AlanTipi.Secim, Secenekler = await Secimler.IllerAsync() },
        new() { Etiket = "Vade (gün)", Yol = nameof(Cari.VadeGun), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Risk limiti (TL)", Yol = nameof(Cari.RiskLimiti), Tip = AlanTipi.Para, Ipucu = "Aşılırsa sefer açarken uyarı verilir" },
        FormAlani.EnumSecim<ParaBirimi>("Çalışma para birimi", nameof(Cari.ParaBirimi)),
        new() { Etiket = "IBAN", Yol = nameof(Cari.IBAN), MaksUzunluk = 34 },
        new() { Etiket = "Aktif", Yol = nameof(Cari.Aktif), Tip = AlanTipi.Onay },
        new() { Etiket = "Adres", Yol = nameof(Cari.Adres), Tip = AlanTipi.CokSatir, MaksUzunluk = 400 },
        new() { Etiket = "Notlar", Yol = nameof(Cari.Notlar), Tip = AlanTipi.CokSatir, MaksUzunluk = 500 },
    ];

    private sealed class TOModel
    {
        public byte? KasaId { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public decimal Tutar { get; set; }
        public decimal Kur { get; set; } = 1;
        public string? Aciklama { get; set; }
    }

    private async Task TahsilatOdemeAsync(bool tahsilat)
    {
        if (Secili is not { } c) return;
        var m = new TOModel { Tutar = Math.Abs(c.Bakiye) };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Kasa / banka", Yol = nameof(TOModel.KasaId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await Secimler.KasalarAsync() },
            new() { Etiket = "Tarih", Yol = nameof(TOModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Tutar (kasa para biriminde)", Yol = nameof(TOModel.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
            new() { Etiket = "Kur (dövizli kasa)", Yol = nameof(TOModel.Kur), Tip = AlanTipi.Ondalik },
            new() { Etiket = "Açıklama", Yol = nameof(TOModel.Aciklama), MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac($"{(tahsilat ? "Tahsilat" : "Ödeme")} — {c.Unvan}", m, alanlar, _ =>
            tahsilat
                ? finans.TahsilatAsync(new TahsilatOdeme(c.Id, m.KasaId!.Value, m.Tarih, m.Tutar, m.Aciklama, m.Kur))
                : finans.OdemeAsync(new TahsilatOdeme(c.Id, m.KasaId!.Value, m.Tarih, m.Tutar, m.Aciklama, m.Kur)));
        if (!ok) return;
        AnaPencere.Basarili(tahsilat ? "Tahsilat kaydedildi." : "Ödeme kaydedildi.");
        await YenileAsync();
    }

    private sealed class ManuelModel
    {
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public bool Acilis { get; set; } = true;
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        public ParaBirimi PB { get; set; } = ParaBirimi.TRY;
        public decimal Kur { get; set; } = 1;
        public DateOnly? Vade { get; set; }
        public string? Aciklama { get; set; } = "Açılış bakiyesi";
    }

    private async Task ManuelAsync()
    {
        if (Secili is not { } c) return;
        var m = new ManuelModel();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(ManuelModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Açılış bakiyesi", Yol = nameof(ManuelModel.Acilis), Tip = AlanTipi.Onay, Ipucu = "İşaretli değilse düzeltme kaydıdır" },
            new() { Etiket = "Borç (cari bize borçlu)", Yol = nameof(ManuelModel.Borc), Tip = AlanTipi.Para },
            new() { Etiket = "Alacak (biz cariye borçluyuz)", Yol = nameof(ManuelModel.Alacak), Tip = AlanTipi.Para },
            FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(ManuelModel.PB)),
            new() { Etiket = "Kur", Yol = nameof(ManuelModel.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => m.PB != ParaBirimi.TRY },
            new() { Etiket = "Vade", Yol = nameof(ManuelModel.Vade), Tip = AlanTipi.Tarih },
            new() { Etiket = "Açıklama", Yol = nameof(ManuelModel.Aciklama), Zorunlu = true, MaksUzunluk = 200 },
        };
        if (!FormPenceresi.Ac($"Açılış / Düzeltme — {c.Unvan}", m, alanlar, _ =>
                finans.CariManuelKayitAsync(c.Id, m.Tarih, m.Borc, m.Alacak, m.Aciklama!, m.Acilis, m.PB, m.Kur, m.Vade))) return;
        await YenileAsync();
    }

    private async Task YaslandirmaAsync()
    {
        var y = await finans.YaslandirmaAsync();
        var vm = new YaslandirmaVM(App.Al<IDisaAktarici>(), y);
        new ListePenceresi(vm, 1100, 600).ShowDialog();
    }
}

public sealed record YaslandirmaGorunum(int CariId, string Unvan, decimal VadesiGelmemis, decimal Gun0_30, decimal Gun31_60, decimal Gun61_90, decimal Gun90Ustu, decimal Toplam);

public sealed class YaslandirmaVM(IDisaAktarici d, List<YaslandirmaSatir> veri) : ListeVM<YaslandirmaGorunum>(d)
{
    public override string Baslik => "Alacak Yaşlandırma (FIFO)";
    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Cari", nameof(YaslandirmaGorunum.Unvan), Genislik: 280), Kolon.Para("Vadesi Gelmemiş", nameof(YaslandirmaGorunum.VadesiGelmemis)),
        Kolon.Para("0-30 gün", nameof(YaslandirmaGorunum.Gun0_30)), Kolon.Para("31-60 gün", nameof(YaslandirmaGorunum.Gun31_60)),
        Kolon.Para("61-90 gün", nameof(YaslandirmaGorunum.Gun61_90)), Kolon.Para("90+ gün", nameof(YaslandirmaGorunum.Gun90Ustu)), Kolon.Para("Toplam", nameof(YaslandirmaGorunum.Toplam)),
    ];

    protected override Task<IEnumerable<YaslandirmaGorunum>> YukleAsync() => Task.FromResult(veri.Select(y => new YaslandirmaGorunum(y.CariId, y.Unvan,
        y.Sonuc.VadesiGelmemis, y.Sonuc.Gun0_30, y.Sonuc.Gun31_60, y.Sonuc.Gun61_90, y.Sonuc.Gun90Ustu, y.Sonuc.Toplam)));
}

public sealed class KasaListeVM(IDisaAktarici d, IKayitServisi<KasaHesap> servis, Secimler s, FinansServisi finans) : KayitListeVM<KasaBakiyeSatir, KasaHesap>(d, servis, s)
{
    public override string Baslik => "Kasa ve Banka Hesapları";
    protected override string KayitAdi => "Kasa/Banka Hesabı";
    public override string? OnayFiltreMetni => "Pasifleri de göster";
    protected override object Id(KasaBakiyeSatir s) => s.Id;
    protected override KasaHesap YeniModel() => new() { AcilisTarihi = DateOnly.FromDateTime(DateTime.Today) };

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Hesap", nameof(KasaBakiyeSatir.Ad), Genislik: 260), new("Tip", nameof(KasaBakiyeSatir.TipAd)), new("PB", nameof(KasaBakiyeSatir.PB)),
        Kolon.Para("Bakiye", nameof(KasaBakiyeSatir.Bakiye), false), new("Aktif", "Aktif?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Ekstre", SymbolRegular.DocumentText24, () => Secili is { } k ? EkstrePenceresi.KasaAsync(k.Id, k.Ad) : Task.CompletedTask),
        E("Virman", SymbolRegular.ArrowSwap24, VirmanAsync),
    ];

    protected override async Task<IEnumerable<KasaBakiyeSatir>> YukleAsync() => await finans.KasaListesiAsync(!OnayFiltre);

    protected override Task<IReadOnlyList<FormAlani>> AlanlarAsync(KasaHesap m, bool yeni) => Task.FromResult<IReadOnlyList<FormAlani>>(
    [
        new() { Etiket = "Hesap adı", Yol = nameof(KasaHesap.Ad), Zorunlu = true, MaksUzunluk = 60 },
        FormAlani.EnumSecim<KasaHesapTipi>("Tip", nameof(KasaHesap.HesapTipi)),
        new() { Etiket = "Banka", Yol = nameof(KasaHesap.BankaAdi), MaksUzunluk = 60 },
        new() { Etiket = "Şube", Yol = nameof(KasaHesap.Sube), MaksUzunluk = 60 },
        new() { Etiket = "IBAN", Yol = nameof(KasaHesap.IBAN), MaksUzunluk = 34 },
        new() { Etiket = "Para birimi", Yol = nameof(KasaHesap.ParaBirimi), Tip = AlanTipi.Secim, Secenekler = EnumMetin.Liste<ParaBirimi>(), SaltOkunur = !yeni },
        new() { Etiket = "Açılış bakiyesi", Yol = nameof(KasaHesap.AcilisBakiye), Tip = AlanTipi.Para },
        new() { Etiket = "Açılış tarihi", Yol = nameof(KasaHesap.AcilisTarihi), Tip = AlanTipi.Tarih, Zorunlu = true },
        new() { Etiket = "Eksi bakiyeye izin ver", Yol = nameof(KasaHesap.EksiBakiyeIzin), Tip = AlanTipi.Onay, Ipucu = "Kredi kartı / KMH hesapları için" },
        new() { Etiket = "Aktif", Yol = nameof(KasaHesap.Aktif), Tip = AlanTipi.Onay },
    ]);

    private sealed class VirmanModel
    {
        public byte? Kaynak { get; set; }
        public byte? Hedef { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public decimal Tutar { get; set; }
        public decimal? HedefTutar { get; set; }
        public string? Aciklama { get; set; }
    }

    private async Task VirmanAsync()
    {
        var m = new VirmanModel { Kaynak = Secili?.Id };
        var kasalar = await Secimler.KasalarAsync();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Kaynak hesap", Yol = nameof(VirmanModel.Kaynak), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = kasalar },
            new() { Etiket = "Hedef hesap", Yol = nameof(VirmanModel.Hedef), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = kasalar },
            new() { Etiket = "Tarih", Yol = nameof(VirmanModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Tutar", Yol = nameof(VirmanModel.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
            new() { Etiket = "Hedef tutar (farklı PB)", Yol = nameof(VirmanModel.HedefTutar), Tip = AlanTipi.Para, Ipucu = "Örn. EUR hesaptan TL hesaba bozdurmada TL karşılığı" },
            new() { Etiket = "Açıklama", Yol = nameof(VirmanModel.Aciklama), MaksUzunluk = 200 },
        };
        if (!FormPenceresi.Ac("Virman", m, alanlar, _ => finans.VirmanAsync(m.Kaynak!.Value, m.Hedef!.Value, m.Tarih, m.Tutar, m.HedefTutar, m.Aciklama))) return;
        AnaPencere.Basarili("Virman yapıldı.");
        await YenileAsync();
    }
}

public sealed class FaturaListeVM(IDisaAktarici d, FaturaServisi servis, Secimler s, IKullaniciOturumu oturum, DovizServisi doviz) : ListeVM<FaturaSatir>(d)
{
    public override string Baslik => "Faturalar";
    public override bool TarihFiltresi => true;
    public override bool YeniVar => oturum.Yetkili(Izinler.FinansYaz);
    public override bool DuzenleVar => true;
    public override bool SilVar => oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "İptal Et";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Tip", nameof(FaturaSatir.TipAd)), new("Fatura No", nameof(FaturaSatir.FaturaNo)), Kolon.Tarih("Tarih", nameof(FaturaSatir.Tarih)),
        Kolon.Tarih("Vade", nameof(FaturaSatir.Vade)), new("Cari", nameof(FaturaSatir.Cari), Genislik: 240),
        Kolon.Para("Ara Toplam", nameof(FaturaSatir.AraToplam)), Kolon.Para("KDV", nameof(FaturaSatir.Kdv)), Kolon.Para("Tevkifat", nameof(FaturaSatir.Tevkifat)),
        Kolon.Para("Genel Toplam", nameof(FaturaSatir.GenelToplam)), new("PB", nameof(FaturaSatir.PB)), new("Durum", nameof(FaturaSatir.DurumAd)), new("e-Belge No", nameof(FaturaSatir.EBelgeNo)),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Faturalanmamış Seferler", SymbolRegular.VehicleTruckProfile24, FaturalanmamisAsync),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } f ? EklerPenceresi.AcAsync(EkHedef.Fatura, f.Id, f.FaturaNo) : Task.CompletedTask),
    ];

    protected override async Task<IEnumerable<FaturaSatir>> YukleAsync() => await servis.ListeAsync(Bas, Bit);
    protected override string? OzetHesapla(IReadOnlyList<FaturaSatir> l)
    {
        var aktif = l.Where(f => f.Durum == FaturaDurum.Kayitli && f.ParaBirimi == ParaBirimi.TRY).ToList();
        return $"Satış: {aktif.Where(f => f.Tip == FaturaTipi.Satis).Sum(f => f.AraToplam):N2}   •   Alış: {aktif.Where(f => f.Tip == FaturaTipi.Alis).Sum(f => f.AraToplam):N2} (TL, KDV hariç)";
    }

    protected override async Task YeniAsync()
    {
        if (FaturaPenceresi.Ac(new Fatura { Tarih = DateOnly.FromDateTime(DateTime.Today) }, servis, s, doviz, await s.CarilerAsync())) await YenileAsync();
    }

    protected override async Task DuzenleAsync(FaturaSatir satir)
    {
        var f = await servis.GetirAsync(satir.Id);
        if (f is not null && FaturaPenceresi.Ac(f, servis, s, doviz, await s.CarilerAsync())) await YenileAsync();
    }

    protected override async Task SilAsync(FaturaSatir f)
    {
        if (f.Durum == FaturaDurum.Iptal) return;
        var neden = await Dialog.MetinIste($"{f.FaturaNo} numaralı fatura iptal edilecek (cari kaydı ters çevrilir, seferler tekrar faturalanabilir). İptal nedeni:", "Fatura iptali");
        if (neden is null) return;
        await servis.IptalAsync(f.Id, neden);
        await YenileAsync();
    }

    private async Task FaturalanmamisAsync()
    {
        var liste = await servis.FaturalanmamisSeferlerAsync();
        var secilen = SecimPenceresi.Sec("Faturalanmamış Seferler (aynı müşteriye ait olanları seçin)", liste,
        [
            new("Sefer", nameof(FaturalanmamisSefer.SeferNo)), Kolon.Tarih("Tarih", nameof(FaturalanmamisSefer.Tarih)), new("Müşteri", nameof(FaturalanmamisSefer.Musteri)),
            new("Güzergah", nameof(FaturalanmamisSefer.Guzergah)), new("Plaka", nameof(FaturalanmamisSefer.Plaka)),
            Kolon.Para("Navlun", nameof(FaturalanmamisSefer.Navlun)), new("PB", nameof(FaturalanmamisSefer.PB)),
        ]);
        if (secilen.Count == 0) return;
        if (secilen.Select(x => x.Musteri).Distinct().Count() > 1) { await Dialog.Hata("Tek faturada yalnızca aynı müşterinin seferleri olabilir."); return; }
        if (secilen.Select(x => x.ParaBirimi).Distinct().Count() > 1) { await Dialog.Hata("Seçilen seferlerin para birimleri farklı."); return; }
        var cariId = await MusteriIdAsync(secilen[0].SeferId);
        var f = new Fatura
        {
            Tarih = DateOnly.FromDateTime(DateTime.Today), CariId = cariId, ParaBirimi = secilen[0].ParaBirimi, Kur = secilen[0].Kur,
            Kalemler = await servis.SeferKalemleriAsync(secilen.Select(x => x.SeferId)),
        };
        if (FaturaPenceresi.Ac(f, servis, s, doviz, await s.CarilerAsync())) await YenileAsync();
    }

    private static async Task<int> MusteriIdAsync(int seferId) => (await App.Al<SeferServisi>().GetirAsync(seferId))!.MusteriCariId;
}

public sealed class CekSenetListeVM(IDisaAktarici d, CekSenetServisi servis, Secimler s, IKullaniciOturumu oturum) : ListeVM<CekSenetSatir>(d)
{
    public override string Baslik => "Çek / Senet Portföyü";
    public override string? OnayFiltreMetni => "Kapananları da göster";
    public override bool YeniVar => oturum.Yetkili(Izinler.FinansYaz);

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Portföy No", nameof(CekSenetSatir.PortfoyNo)), new("Yön", nameof(CekSenetSatir.YonAd)), new("Tip", nameof(CekSenetSatir.TipAd)),
        new("Seri No", nameof(CekSenetSatir.SeriNo)), new("Banka", nameof(CekSenetSatir.Banka)), new("Cari", nameof(CekSenetSatir.Cari), Genislik: 220),
        Kolon.Para("Tutar", nameof(CekSenetSatir.Tutar)), Kolon.Tarih("Vade", nameof(CekSenetSatir.Vade)), new("Durum", nameof(CekSenetSatir.DurumAd)),
    ];

    public override IReadOnlyList<Eylem> Eylemler => [E("Durum Değiştir", SymbolRegular.ArrowSync24, DurumAsync)];

    protected override async Task<IEnumerable<CekSenetSatir>> YukleAsync() => await servis.ListeAsync(null, !OnayFiltre);
    protected override string? OzetHesapla(IReadOnlyList<CekSenetSatir> l) =>
        $"Portföydeki alınan: {l.Where(c => c.Yon == CekSenetYon.Alinan && c.Durum is CekSenetDurum.Portfoyde or CekSenetDurum.TahsileVerildi).Sum(c => c.Tutar):N2}   •   " +
        $"Ödenecek verilen: {l.Where(c => c.Yon == CekSenetYon.Verilen && c.Durum == CekSenetDurum.CiroEdildi).Sum(c => c.Tutar):N2}";

    protected override async Task YeniAsync()
    {
        var c = new CekSenet { IslemTarihi = DateOnly.FromDateTime(DateTime.Today), VadeTarihi = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)) };
        var alanlar = new List<FormAlani>
        {
            FormAlani.EnumSecim<CekSenetYon>("Yön", nameof(CekSenet.Yon)),
            FormAlani.EnumSecim<CekSenetTipi>("Tip", nameof(CekSenet.Tip)),
            new() { Etiket = "Cari (alınan: kimden / verilen: kime)", Yol = nameof(CekSenet.CariId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.CarilerAsync() },
            new() { Etiket = "Seri no", Yol = nameof(CekSenet.SeriNo), MaksUzunluk = 30 },
            new() { Etiket = "Banka", Yol = nameof(CekSenet.Banka), MaksUzunluk = 60 },
            new() { Etiket = "Şube", Yol = nameof(CekSenet.Sube), MaksUzunluk = 60 },
            new() { Etiket = "Keşideci", Yol = nameof(CekSenet.Kesideci), MaksUzunluk = 150 },
            new() { Etiket = "Tutar (TL)", Yol = nameof(CekSenet.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
            new() { Etiket = "İşlem tarihi", Yol = nameof(CekSenet.IslemTarihi), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Vade", Yol = nameof(CekSenet.VadeTarihi), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Açıklama", Yol = nameof(CekSenet.Aciklama), MaksUzunluk = 300 },
        };
        if (FormPenceresi.Ac("Yeni Çek / Senet", c, alanlar, _ => servis.KaydetAsync(c))) await YenileAsync();
    }

    private sealed class DurumModel
    {
        public CekSenetDurum Durum { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public byte? KasaId { get; set; }
        public int? CiroCariId { get; set; }
        public string? Aciklama { get; set; }
    }

    private async Task DurumAsync()
    {
        if (Secili is not { } c) return;
        var gecisler = c.Yon == CekSenetYon.Alinan
            ? [CekSenetDurum.TahsileVerildi, CekSenetDurum.TahsilEdildi, CekSenetDurum.CiroEdildi, CekSenetDurum.Karsiliksiz, CekSenetDurum.Iade]
            : new[] { CekSenetDurum.Odendi, CekSenetDurum.Iade };
        var m = new DurumModel { Durum = gecisler[0] };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Yeni durum", Yol = nameof(DurumModel.Durum), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = EnumMetin.Liste<CekSenetDurum>().Where(x => gecisler.Contains(x.Deger)).ToList() },
            new() { Etiket = "Tarih", Yol = nameof(DurumModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Kasa / banka", Yol = nameof(DurumModel.KasaId), Tip = AlanTipi.Secim, Secenekler = await s.KasalarAsync(ParaBirimi.TRY), Gorunur = _ => m.Durum is CekSenetDurum.TahsilEdildi or CekSenetDurum.Odendi },
            new() { Etiket = "Ciro edilen cari", Yol = nameof(DurumModel.CiroCariId), Tip = AlanTipi.Secim, Secenekler = await s.CarilerAsync(), Gorunur = _ => m.Durum == CekSenetDurum.CiroEdildi },
            new() { Etiket = "Açıklama", Yol = nameof(DurumModel.Aciklama), MaksUzunluk = 200 },
        };
        if (FormPenceresi.Ac($"{c.PortfoyNo} — Durum Değiştir", m, alanlar, _ => servis.DurumDegistirAsync(c.Id, m.Durum, m.Tarih, m.KasaId, m.CiroCariId, m.Aciklama)))
            await YenileAsync();
    }
}

public sealed class FinansmanListeVM(IDisaAktarici d, FinansmanServisi servis, Secimler s, IKullaniciOturumu oturum) : ListeVM<FinansmanSatir>(d)
{
    public override string Baslik => "Kredi / Leasing Sözleşmeleri";
    public override bool YeniVar => oturum.Yetkili(Izinler.FinansmanYaz);
    public override bool DuzenleVar => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Plaka", nameof(FinansmanSatir.Plaka)), new("Tip", nameof(FinansmanSatir.TipAd)), new("Kurum", nameof(FinansmanSatir.Kurum), Genislik: 200),
        new("Sözleşme", nameof(FinansmanSatir.SozlesmeNo)), Kolon.Para("Anapara", nameof(FinansmanSatir.Anapara)), new("PB", nameof(FinansmanSatir.ParaBirimi)),
        Kolon.Sayi("Vade", nameof(FinansmanSatir.VadeSayisi)), Kolon.Sayi("Ödenen", nameof(FinansmanSatir.OdenenTaksit)),
        Kolon.Para("Kalan Borç", nameof(FinansmanSatir.KalanBorc)), Kolon.Tarih("Sonraki Vade", nameof(FinansmanSatir.SonrakiVade)), new("Aktif", "Aktif?bool"),
    ];

    protected override async Task<IEnumerable<FinansmanSatir>> YukleAsync() => await servis.ListeAsync();

    protected override async Task YeniAsync()
    {
        if (await FormAsync(new AracFinansman { BaslangicTarihi = DateOnly.FromDateTime(DateTime.Today), VadeSayisi = 36 })) await YenileAsync();
    }

    protected override async Task DuzenleAsync(FinansmanSatir satir)
    {
        var f = await servis.GetirAsync(satir.Id);
        if (f is not null && await FormAsync(f)) await YenileAsync();
    }

    private async Task<bool> FormAsync(AracFinansman f)
    {
        var yeni = f.Id == 0;
        var alanlar = new List<FormAlani>
        {
            FormAlani.EnumSecim<FinansmanTipi>("Tip", nameof(AracFinansman.Tip)),
            new() { Etiket = "Araç", Yol = nameof(AracFinansman.AracId), Tip = AlanTipi.Secim, Secenekler = await s.AraclarAsync(false) },
            new() { Etiket = "Dorse", Yol = nameof(AracFinansman.DorseId), Tip = AlanTipi.Secim, Secenekler = await s.DorselerAsync(false) },
            new() { Etiket = "Banka / leasing şirketi", Yol = nameof(AracFinansman.FinansKurumuCariId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.CarilerAsync(CariTipi.Finans) },
            new() { Etiket = "Sözleşme no", Yol = nameof(AracFinansman.SozlesmeNo), MaksUzunluk = 40 },
            new() { Etiket = "Başlangıç", Yol = nameof(AracFinansman.BaslangicTarihi), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Anapara", Yol = nameof(AracFinansman.AnaparaTutar), Tip = AlanTipi.Para, Zorunlu = true },
            FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(AracFinansman.ParaBirimi)),
            new() { Etiket = "Aylık faiz %", Yol = nameof(AracFinansman.AylikFaizOrani), Tip = AlanTipi.Ondalik, Ipucu = "BSMV/KKDF dahil efektif aylık oran" },
            new() { Etiket = "Vade sayısı (ay)", Yol = nameof(AracFinansman.VadeSayisi), Tip = AlanTipi.Tamsayi, Zorunlu = true },
            new() { Etiket = "Aktif", Yol = nameof(AracFinansman.Aktif), Tip = AlanTipi.Onay },
            new() { Etiket = "Açıklama", Yol = nameof(AracFinansman.Aciklama), Tip = AlanTipi.CokSatir, MaksUzunluk = 300 },
        };
        return FormPenceresi.Ac(yeni ? "Yeni Kredi / Leasing" : "Kredi / Leasing Düzenle", f, alanlar, async _ =>
        {
            // Plan: eşit taksit önerisi (ilk vade başlangıçtan bir ay sonra). Banka planı farklıysa Taksit Takvimi'nden tek tek düzenlenebilir.
            var plan = yeni || f.Taksitler.All(t => t.OdemeTarihi is null)
                ? FinansmanServisi.PlanOner(f, f.BaslangicTarihi.AddMonths(1))
                : f.Taksitler;
            await servis.KaydetAsync(f, plan);
        }, 900);
    }
}

public sealed class TaksitListeVM(IDisaAktarici d, FinansmanServisi servis, Secimler s, IKullaniciOturumu oturum) : ListeVM<TaksitSatir>(d)
{
    public override string Baslik => "Taksit Takvimi";
    public override bool TarihFiltresi => true;
    public override string? OnayFiltreMetni => "Ödenmişleri de göster";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.Tarih("Vade", nameof(TaksitSatir.Vade)), new("Plaka", nameof(TaksitSatir.Plaka)), new("Kurum", nameof(TaksitSatir.Kurum), Genislik: 200),
        Kolon.Sayi("Taksit", nameof(TaksitSatir.TaksitNo)), Kolon.Para("Anapara", nameof(TaksitSatir.Anapara)), Kolon.Para("Faiz", nameof(TaksitSatir.Faiz)),
        Kolon.Para("Toplam", nameof(TaksitSatir.Toplam)), new("PB", nameof(TaksitSatir.ParaBirimi)), Kolon.Tarih("Ödeme", nameof(TaksitSatir.OdemeTarihi)),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Öde", SymbolRegular.Payment24, OdeAsync),
        E("Ödemeyi Geri Al", SymbolRegular.ArrowUndo24, async () =>
        {
            if (Secili is not { Odendi: true } t || !await Dialog.Onay("Taksit ödemesi geri alınsın mı? (Kasa kaydı ters çevrilir)")) return;
            await servis.TaksitOdemeIptalAsync(t.Id);
            await YenileAsync();
        }),
    ];

    protected override async Task<IEnumerable<TaksitSatir>> YukleAsync()
    {
        var bit = BitTarih is { } b && b > DateTime.Today ? Bit : DateOnly.FromDateTime(DateTime.Today.AddYears(1));
        return await servis.TaksitlerAsync(Bas, bit, !OnayFiltre);
    }

    protected override string? OzetHesapla(IReadOnlyList<TaksitSatir> l) =>
        $"Ödenecek: {l.Where(t => !t.Odendi).Sum(t => t.Toplam):N2}   •   Faiz: {l.Where(t => !t.Odendi).Sum(t => t.Faiz):N2}";

    private sealed class OdeModel
    {
        public byte? KasaId { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }

    private async Task OdeAsync()
    {
        if (Secili is not { } t) return;
        if (t.Odendi) { await Dialog.Bilgi("Taksit zaten ödenmiş."); return; }
        var m = new OdeModel();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Kasa / banka", Yol = nameof(OdeModel.KasaId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.KasalarAsync(t.ParaBirimi) },
            new() { Etiket = "Ödeme tarihi", Yol = nameof(OdeModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
        };
        if (FormPenceresi.Ac($"Taksit Öde — {t.Kurum} #{t.TaksitNo} ({t.Toplam:N2})", m, alanlar, _ => servis.TaksitOdeAsync(t.Id, m.KasaId!.Value, m.Tarih)))
            await YenileAsync();
    }
}
