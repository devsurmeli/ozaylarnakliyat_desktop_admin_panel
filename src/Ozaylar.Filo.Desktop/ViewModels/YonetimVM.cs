using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Ozaylar.Filo.Infrastructure.Veri;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed class KullaniciListeVM(IDisaAktarici d, KullaniciServisi servis, Secimler s) : ListeVM<KullaniciSatir>(d)
{
    public override string Baslik => "Kullanıcılar";
    public override bool YeniVar => true;
    public override bool DuzenleVar => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Kullanıcı Adı", nameof(KullaniciSatir.KullaniciAdi)), new("Ad Soyad", nameof(KullaniciSatir.AdSoyad)), new("E-posta", nameof(KullaniciSatir.Eposta)),
        new("Roller", nameof(KullaniciSatir.Roller), Genislik: 260), Kolon.TarihSaat("Son Giriş (UTC)", nameof(KullaniciSatir.SonGiris)),
        new("Aktif", "Aktif?bool"), new("Kilitli", "Kilitli?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Kilidi Aç", SymbolRegular.LockOpen24, async () =>
        {
            if (Secili is not { } k) return;
            await servis.KilitAcAsync(k.Id);
            await YenileAsync();
        }),
    ];

    protected override async Task<IEnumerable<KullaniciSatir>> YukleAsync() => await servis.ListeAsync();

    public sealed class KullaniciModel
    {
        public Kullanici K { get; init; } = new();
        public List<byte> Roller { get; init; } = [];
        public string? GeciciSifre { get; set; }
    }

    protected override Task YeniAsync() => FormAsync(new KullaniciModel());

    protected override async Task DuzenleAsync(KullaniciSatir satir)
    {
        var (k, roller) = await servis.GetirAsync(satir.Id);
        await FormAsync(new KullaniciModel { K = k, Roller = roller });
    }

    private async Task FormAsync(KullaniciModel m)
    {
        var yeni = m.K.Id == 0;
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Kullanıcı adı", Yol = "K.KullaniciAdi", Zorunlu = true, MaksUzunluk = 50 },
            new() { Etiket = "Ad soyad", Yol = "K.AdSoyad", Zorunlu = true, MaksUzunluk = 100 },
            new() { Etiket = "E-posta", Yol = "K.Eposta", MaksUzunluk = 100 },
            new() { Etiket = "Aktif", Yol = "K.Aktif", Tip = AlanTipi.Onay },
            new() { Etiket = yeni ? "Geçici şifre" : "Yeni geçici şifre (sıfırlamak için)", Yol = nameof(KullaniciModel.GeciciSifre), Tip = AlanTipi.Sifre, Zorunlu = yeni, Ipucu = "Kullanıcı ilk girişte değiştirmek zorunda kalır" },
            new() { Etiket = "Roller", Yol = nameof(KullaniciModel.Roller), Tip = AlanTipi.CokluSecim, Zorunlu = true, Secenekler = await s.RollerAsync() },
        };
        if (FormPenceresi.Ac(yeni ? "Yeni Kullanıcı" : $"Kullanıcı — {m.K.KullaniciAdi}", m, alanlar, _ => servis.KaydetAsync(m.K, m.Roller, m.GeciciSifre)))
            await YenileAsync();
    }
}

public sealed record RolSatir(byte Id, string Ad, string? Aciklama, bool Sistem);

public sealed class RolListeVM(IDisaAktarici d, RolServisi servis, Secimler s) : ListeVM<RolSatir>(d)
{
    public override string Baslik => "Roller ve Yetkiler";
    public override bool YeniVar => true;
    public override bool DuzenleVar => true;
    public override bool SilVar => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
        [new("Rol", nameof(RolSatir.Ad)), new("Açıklama", nameof(RolSatir.Aciklama), Genislik: 400), new("Sistem", "Sistem?bool")];

    protected override async Task<IEnumerable<RolSatir>> YukleAsync() => (await servis.RollerAsync()).Select(r => new RolSatir(r.Id, r.Ad, r.Aciklama, r.Sistem));

    public sealed class RolModel
    {
        public string? Ad { get; set; }
        public string? Aciklama { get; set; }
        public List<short> Izinler { get; init; } = [];
    }

    protected override Task YeniAsync() => FormAsync(0, new RolModel());

    protected override async Task DuzenleAsync(RolSatir r)
    {
        if (r.Sistem) { await Dialog.Bilgi("Yönetici rolü her zaman tüm yetkilere sahiptir ve değiştirilemez."); return; }
        var izinler = await servis.IzinlerAsync(r.Id);
        await FormAsync(r.Id, new RolModel { Ad = r.Ad, Aciklama = r.Aciklama, Izinler = izinler.Where(i => i.Secili).Select(i => i.Id).ToList() });
    }

    private async Task FormAsync(byte id, RolModel m)
    {
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Rol adı", Yol = nameof(RolModel.Ad), Zorunlu = true, MaksUzunluk = 50 },
            new() { Etiket = "Açıklama", Yol = nameof(RolModel.Aciklama), MaksUzunluk = 200 },
            new() { Etiket = "Yetkiler", Yol = nameof(RolModel.Izinler), Tip = AlanTipi.CokluSecim, Secenekler = await s.IzinlerAsync() },
        };
        if (FormPenceresi.Ac(id == 0 ? "Yeni Rol" : $"Rol — {m.Ad}", m, alanlar, _ => servis.KaydetAsync(id, m.Ad!, m.Aciklama, m.Izinler), 640))
            await YenileAsync();
    }

    protected override async Task SilAsync(RolSatir r)
    {
        if (!await Dialog.Onay($"'{r.Ad}' rolü silinsin mi?")) return;
        await servis.SilAsync(r.Id);
        await YenileAsync();
    }
}

public sealed class AuditListeVM(IDisaAktarici d, AuditServisi servis) : ListeVM<AuditSatir>(d)
{
    public override string Baslik => "Denetim Kaydı (kim, ne zaman, neyi değiştirdi)";
    public override bool TarihFiltresi => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.TarihSaat("Zaman (UTC)", nameof(AuditSatir.Tarih)), new("Kullanıcı", nameof(AuditSatir.Kullanici)), new("Tablo", nameof(AuditSatir.Tablo)),
        new("Kayıt", nameof(AuditSatir.KayitId)), new("İşlem", nameof(AuditSatir.Islem)), new("Değişiklik [eski, yeni]", nameof(AuditSatir.Degisiklik), Genislik: 600),
    ];

    protected override async Task<IEnumerable<AuditSatir>> YukleAsync() =>
        await servis.ListeAsync(Bas.ToDateTime(TimeOnly.MinValue), Bit.ToDateTime(TimeOnly.MaxValue), null, null, null);
}

/// <summary>Sistem ayarları, dönem kilidi, bağlantı ve kişisel şifre.</summary>
public sealed partial class AyarlarVM(AyarServisi ayar, IKullaniciOturumu oturum, BaglantiBilgisi baglanti) : ObservableObject
{
    public bool Yonetici => oturum.Yetkili(Izinler.AyarYonet);
    public string Sunucu => $"{baglanti.Sunucu} / {baglanti.Veritabani} ({(baglanti.WindowsKimlik ? "Windows kimlik" : baglanti.Kullanici)})";
    public string Surum => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "";

    [ObservableProperty] private string? _sonYedek;
    [ObservableProperty] private string? _kapanis;

    public sealed class AyarModel
    {
        public string? Unvan { get; set; }
        public string? VergiNo { get; set; }
        public decimal KmSapma { get; set; }
        public decimal YakitSapma { get; set; }
        public decimal FissizLimit { get; set; }
        public DateOnly? KapanisTarihi { get; set; }
    }

    public async Task YukleAsync()
    {
        var y = await ayar.SonYedekAsync();
        SonYedek = y is null ? "Kayıt yok" : y.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        var k = await ayar.KapanisTarihiAsync();
        Kapanis = k is null ? "Dönem kilidi yok" : $"{k:dd.MM.yyyy} ve öncesi kilitli";
    }

    [RelayCommand]
    private async Task DuzenleAsync()
    {
        var t = await ayar.TumuAsync();
        decimal D(string a, decimal v) => decimal.TryParse(t.GetValueOrDefault(a), NumberStyles.Number, CultureInfo.InvariantCulture, out var x) ? x : v;
        var m = new AyarModel
        {
            Unvan = t.GetValueOrDefault("Firma.Unvan"), VergiNo = t.GetValueOrDefault("Firma.VergiNo"),
            KmSapma = D("Sefer.KmSapmaYuzde", 15), YakitSapma = D("Yakit.SapmaUyariYuzde", 10), FissizLimit = D("Masraf.FissizLimit", 1000),
            KapanisTarihi = await ayar.KapanisTarihiAsync(),
        };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Firma ünvanı", Yol = nameof(AyarModel.Unvan), Zorunlu = true, Ipucu = "Rapor ve PDF başlıklarında görünür" },
            new() { Etiket = "Vergi no", Yol = nameof(AyarModel.VergiNo) },
            new() { Etiket = "Sefer km sapma uyarısı %", Yol = nameof(AyarModel.KmSapma), Tip = AlanTipi.Ondalik },
            new() { Etiket = "Yakıt tüketim sapma alarmı %", Yol = nameof(AyarModel.YakitSapma), Tip = AlanTipi.Ondalik },
            new() { Etiket = "Fişsiz harcama limiti (TL)", Yol = nameof(AyarModel.FissizLimit), Tip = AlanTipi.Para },
            new() { Etiket = "Dönem kapanış tarihi", Yol = nameof(AyarModel.KapanisTarihi), Tip = AlanTipi.Tarih, Ipucu = "Bu tarih ve öncesine finansal kayıt girilemez/değiştirilemez (muhasebe dönemi kapandığında)" },
        };
        if (!FormPenceresi.Ac("Sistem Ayarları", m, alanlar, async _ =>
            {
                string S(decimal v) => v.ToString(CultureInfo.InvariantCulture);
                await ayar.YazAsync("Firma.Unvan", m.Unvan);
                await ayar.YazAsync("Firma.VergiNo", m.VergiNo);
                await ayar.YazAsync("Sefer.KmSapmaYuzde", S(m.KmSapma));
                await ayar.YazAsync("Yakit.SapmaUyariYuzde", S(m.YakitSapma));
                await ayar.YazAsync("Masraf.FissizLimit", S(m.FissizLimit));
                await ayar.KapanisTarihiYazAsync(m.KapanisTarihi);
            })) return;
        await YukleAsync();
        AnaPencere.Basarili("Ayarlar kaydedildi.");
    }

    [RelayCommand]
    private async Task SifreAsync()
    {
        if (await SifreDegistir.AcAsync(zorunlu: false)) AnaPencere.Basarili("Şifreniz değiştirildi.");
    }

    [RelayCommand]
    private async Task BaglantiAsync()
    {
        if (KurulumPenceresi.Ac(baglanti) is not null)
            await Dialog.Bilgi("Bağlantı ayarları kaydedildi. Değişikliğin geçerli olması için programı yeniden başlatın.");
    }
}
