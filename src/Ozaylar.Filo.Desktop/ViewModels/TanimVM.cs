using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed class LokasyonListeVM(IDisaAktarici d, IKayitServisi<Lokasyon> servis, Secimler s) : BasitKayitListeVM<Lokasyon>(d, servis, s)
{
    public override string Baslik => "Lokasyonlar (yükleme / boşaltma noktaları)";
    protected override string KayitAdi => "Lokasyon";
    protected override IQueryable<Lokasyon> Sorgu(IQueryable<Lokasyon> q) => q.Include(x => x.Ulke).Include(x => x.Il).Include(x => x.Cari).OrderBy(x => x.Ad);
    protected override object Id(Lokasyon s) => s.Id;
    protected override Lokasyon YeniModel() => new() { UlkeId = 1 };

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Ad", nameof(Lokasyon.Ad)), new("Ülke", "Ulke.Ad"), new("İl", "Il.Ad"), new("İlçe", nameof(Lokasyon.Ilce)),
        new("Cari", "Cari.Unvan"), new("Adres", nameof(Lokasyon.Adres)), new("Aktif", "Aktif?bool"),
    ];

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(Lokasyon m, bool yeni) =>
    [
        new() { Etiket = "Ad", Yol = nameof(Lokasyon.Ad), Zorunlu = true, MaksUzunluk = 100, Ipucu = "Örn. Gebze Depo, Mersin Liman, München Lager" },
        new() { Etiket = "Ülke", Yol = nameof(Lokasyon.UlkeId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await Secimler.UlkelerAsync() },
        new() { Etiket = "İl", Yol = nameof(Lokasyon.IlId), Tip = AlanTipi.Secim, Secenekler = await Secimler.IllerAsync(), Gorunur = o => ((Lokasyon)o).UlkeId == 1 },
        new() { Etiket = "İlçe / şehir", Yol = nameof(Lokasyon.Ilce), MaksUzunluk = 50 },
        new() { Etiket = "Cari (firma)", Yol = nameof(Lokasyon.CariId), Tip = AlanTipi.Secim, Secenekler = await Secimler.CarilerAsync() },
        new() { Etiket = "Adres", Yol = nameof(Lokasyon.Adres), Tip = AlanTipi.CokSatir, MaksUzunluk = 300 },
        new() { Etiket = "Aktif", Yol = nameof(Lokasyon.Aktif), Tip = AlanTipi.Onay },
    ];
}

public sealed class GuzergahListeVM(IDisaAktarici d, IKayitServisi<Guzergah> servis, Secimler s) : BasitKayitListeVM<Guzergah>(d, servis, s)
{
    public override string Baslik => "Güzergahlar";
    protected override string KayitAdi => "Güzergah";
    protected override IQueryable<Guzergah> Sorgu(IQueryable<Guzergah> q) => q.Include(x => x.CikisLokasyon).Include(x => x.VarisLokasyon).OrderBy(x => x.Ad);
    protected override object Id(Guzergah s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Ad", nameof(Guzergah.Ad)), new("Çıkış", "CikisLokasyon.Ad"), new("Varış", "VarisLokasyon.Ad"),
        Kolon.Sayi("Std. Km", nameof(Guzergah.StandartKm)), Kolon.Sayi("Std. Gün", nameof(Guzergah.StandartGun)),
        Kolon.Para("Std. Navlun", nameof(Guzergah.StandartNavlun), false), new("PB", nameof(Guzergah.NavlunParaBirimi)),
        Kolon.Para("Std. Yol Masrafı", nameof(Guzergah.StandartYolMasrafi), false), new("Aktif", "Aktif?bool"),
    ];

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(Guzergah m, bool yeni)
    {
        var lok = await Secimler.LokasyonlarAsync();
        return
        [
            new() { Etiket = "Çıkış", Yol = nameof(Guzergah.CikisLokasyonId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = lok, Degisince = AdOner(lok, m) },
            new() { Etiket = "Varış", Yol = nameof(Guzergah.VarisLokasyonId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = lok, Degisince = AdOner(lok, m) },
            new() { Etiket = "Ad", Yol = nameof(Guzergah.Ad), Zorunlu = true, MaksUzunluk = 150 },
            new() { Etiket = "Standart km", Yol = nameof(Guzergah.StandartKm), Tip = AlanTipi.Tamsayi, Zorunlu = true, Ipucu = "Gidiş-dönüş toplam km; sefer km sapma uyarısı bu değere göre" },
            new() { Etiket = "Standart gün", Yol = nameof(Guzergah.StandartGun), Tip = AlanTipi.Tamsayi },
            new() { Etiket = "Standart navlun", Yol = nameof(Guzergah.StandartNavlun), Tip = AlanTipi.Para },
            FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(Guzergah.NavlunParaBirimi)),
            new() { Etiket = "Tahmini yol masrafı (TL)", Yol = nameof(Guzergah.StandartYolMasrafi), Tip = AlanTipi.Para },
            new() { Etiket = "Aktif", Yol = nameof(Guzergah.Aktif), Tip = AlanTipi.Onay },
        ];
    }

    private static Func<FormPenceresi, Task> AdOner(List<Secim> lok, Guzergah m) => _ =>
    {
        if (string.IsNullOrWhiteSpace(m.Ad) && m.CikisLokasyonId > 0 && m.VarisLokasyonId > 0)
            m.Ad = $"{lok.First(l => l.Id == m.CikisLokasyonId).Ad} → {lok.First(l => l.Id == m.VarisLokasyonId).Ad}";
        return Task.CompletedTask;
    };
}

public sealed class MasrafTuruListeVM(IDisaAktarici d, IKayitServisi<MasrafTuru> servis, Secimler s) : BasitKayitListeVM<MasrafTuru>(d, servis, s)
{
    public override string Baslik => "Masraf Türleri";
    protected override string KayitAdi => "Masraf Türü";
    public override bool SilVar => false;
    protected override IQueryable<MasrafTuru> Sorgu(IQueryable<MasrafTuru> q) => q.OrderBy(x => x.Grup).ThenBy(x => x.Ad);
    protected override object Id(MasrafTuru s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Ad", nameof(MasrafTuru.Ad)), new("Grup", nameof(MasrafTuru.Grup)), Kolon.Sayi("KDV %", nameof(MasrafTuru.VarsayilanKdv)),
        new("Şoför Harcaması", "SoforHarcamasi?bool"), new("Aktif", "Aktif?bool"),
    ];

    protected override Task<IReadOnlyList<FormAlani>> AlanlarAsync(MasrafTuru m, bool yeni) => Task.FromResult<IReadOnlyList<FormAlani>>(
    [
        new() { Etiket = "Ad", Yol = nameof(MasrafTuru.Ad), Zorunlu = true, MaksUzunluk = 60 },
        FormAlani.EnumSecim<MasrafGrubu>("Grup (raporlama)", nameof(MasrafTuru.Grup)),
        new() { Etiket = "Varsayılan KDV %", Yol = nameof(MasrafTuru.VarsayilanKdv), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Şoför yolda yapabilir", Yol = nameof(MasrafTuru.SoforHarcamasi), Tip = AlanTipi.Onay },
        new() { Etiket = "Aktif", Yol = nameof(MasrafTuru.Aktif), Tip = AlanTipi.Onay },
    ]);
}

public sealed class EvrakTuruListeVM(IDisaAktarici d, IKayitServisi<EvrakTuru> servis, Secimler s) : BasitKayitListeVM<EvrakTuru>(d, servis, s)
{
    public override string Baslik => "Evrak Türleri";
    protected override string KayitAdi => "Evrak Türü";
    public override bool SilVar => false;
    protected override IQueryable<EvrakTuru> Sorgu(IQueryable<EvrakTuru> q) => q.OrderBy(x => x.Ad);
    protected override object Id(EvrakTuru s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Ad", nameof(EvrakTuru.Ad)), new("Kime Ait", nameof(EvrakTuru.HedefTip)), Kolon.Sayi("Uyarı (gün)", nameof(EvrakTuru.UyariGun)),
        Kolon.Sayi("Periyot (ay)", nameof(EvrakTuru.PeriyotAy)), new("Zorunlu", "Zorunlu?bool"), new("Sefer Engeller", "SeferEngeller?bool"), new("Aktif", "Aktif?bool"),
    ];

    protected override Task<IReadOnlyList<FormAlani>> AlanlarAsync(EvrakTuru m, bool yeni) => Task.FromResult<IReadOnlyList<FormAlani>>(
    [
        new() { Etiket = "Ad", Yol = nameof(EvrakTuru.Ad), Zorunlu = true, MaksUzunluk = 60 },
        new() { Etiket = "Kime ait", Yol = nameof(EvrakTuru.HedefTip), Tip = AlanTipi.Bayraklar, Secenekler = EnumMetin.Liste<EvrakHedef>() },
        new() { Etiket = "Uyarı (gün önce)", Yol = nameof(EvrakTuru.UyariGun), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Yenileme periyodu (ay)", Yol = nameof(EvrakTuru.PeriyotAy), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Zorunlu", Yol = nameof(EvrakTuru.Zorunlu), Tip = AlanTipi.Onay, Ipucu = "Süresi dolmuşsa sefer kaydında uyarı verilir" },
        new() { Etiket = "Süresi dolmuşsa sefer engellensin", Yol = nameof(EvrakTuru.SeferEngeller), Tip = AlanTipi.Onay },
        new() { Etiket = "Aktif", Yol = nameof(EvrakTuru.Aktif), Tip = AlanTipi.Onay },
    ]);
}

public sealed class HarcirahTarifeListeVM(IDisaAktarici d, IKayitServisi<HarcirahTarife> servis, Secimler s) : BasitKayitListeVM<HarcirahTarife>(d, servis, s)
{
    public override string Baslik => "Harcırah Tarifeleri";
    protected override string KayitAdi => "Harcırah Tarifesi";
    protected override IQueryable<HarcirahTarife> Sorgu(IQueryable<HarcirahTarife> q) => q.Include(x => x.Ulke).OrderBy(x => x.UlkeId).ThenByDescending(x => x.GecerlilikBaslangic);
    protected override object Id(HarcirahTarife s) => s.Id;
    protected override HarcirahTarife YeniModel() => new() { GecerlilikBaslangic = DateOnly.FromDateTime(DateTime.Today) };

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Ülke (boş = yurtiçi)", "Ulke.Ad"), Kolon.Para("Günlük Tutar", nameof(HarcirahTarife.GunlukTutar), false), new("PB", nameof(HarcirahTarife.ParaBirimi)),
        Kolon.Tarih("Geçerlilik Başlangıcı", nameof(HarcirahTarife.GecerlilikBaslangic)), new("Açıklama", nameof(HarcirahTarife.Aciklama)),
    ];

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(HarcirahTarife m, bool yeni) =>
    [
        new() { Etiket = "Ülke (boş = yurtiçi)", Yol = nameof(HarcirahTarife.UlkeId), Tip = AlanTipi.Secim, Secenekler = (await Secimler.UlkelerAsync()).Where(u => u.Id != 1).ToList() },
        new() { Etiket = "Günlük tutar", Yol = nameof(HarcirahTarife.GunlukTutar), Tip = AlanTipi.Para, Zorunlu = true },
        FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(HarcirahTarife.ParaBirimi)),
        new() { Etiket = "Geçerlilik başlangıcı", Yol = nameof(HarcirahTarife.GecerlilikBaslangic), Tip = AlanTipi.Tarih, Zorunlu = true },
        new() { Etiket = "Açıklama", Yol = nameof(HarcirahTarife.Aciklama), MaksUzunluk = 200, Ipucu = "Vergi/SGK istisna tutarlarını mali müşavirinizle teyit edin" },
    ];
}

/// <summary>Ekstre şablonu: alan → Excel başlığı eşlemesi düzenlenebilir alanlar olarak gösterilir.</summary>
public sealed class SablonListeVM(IDisaAktarici d, IceAktarmaServisi servis, IFiloDbContextFactory f) : ListeVM<IceAktarimSablon>(d)
{
    public override string Baslik => "Ekstre Şablonları";
    public override bool YeniVar => true;
    public override bool DuzenleVar => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Ad", nameof(IceAktarimSablon.Ad)), new("Tür", nameof(IceAktarimSablon.Tur)), Kolon.Sayi("Başlık Satırı", nameof(IceAktarimSablon.BaslikSatiri)),
        new("Kolon Eşleme", nameof(IceAktarimSablon.KolonEsleme), Genislik: 600), new("Aktif", "Aktif?bool"),
    ];

    protected override async Task<IEnumerable<IceAktarimSablon>> YukleAsync()
    {
        await using var db = f.Olustur();
        return await db.IceAktarimSablonlari.AsNoTracking().OrderBy(s => s.Tur).ThenBy(s => s.Ad).ToListAsync();
    }

    protected override async Task YeniAsync()
    {
        var tur = await Dialog.Onay("Hangi tür ekstre için şablon oluşturulsun?", "Yeni şablon", "Akaryakıt", "HGS/OGS") ? IceAktarimTur.Yakit : IceAktarimTur.Hgs;
        await AcAsync(new IceAktarimSablon { Tur = tur, Ad = tur == IceAktarimTur.Yakit ? "Yeni akaryakıt şablonu" : "Yeni HGS şablonu" });
    }

    protected override Task DuzenleAsync(IceAktarimSablon s) => AcAsync(s);

    private async Task AcAsync(IceAktarimSablon s)
    {
        var esleme = JsonSerializer.Deserialize<Dictionary<string, string>>(s.KolonEsleme) ?? [];
        var alanAdlari = s.Tur == IceAktarimTur.Yakit ? IceAktarmaServisi.YakitAlanlari : IceAktarmaServisi.HgsAlanlari;
        var m = new SablonModel { Ad = s.Ad, BaslikSatiri = s.BaslikSatiri, Aktif = s.Aktif };
        foreach (var a in alanAdlari) m.Esleme[a] = esleme.GetValueOrDefault(a);
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Şablon adı", Yol = nameof(SablonModel.Ad), Zorunlu = true },
            new() { Etiket = "Başlık satırı no", Yol = nameof(SablonModel.BaslikSatiri), Tip = AlanTipi.Tamsayi, Zorunlu = true },
            new() { Etiket = "Aktif", Yol = nameof(SablonModel.Aktif), Tip = AlanTipi.Onay },
            FormAlani.Bolum("Excel'deki kolon başlıkları (birebir yazın)"),
        };
        alanlar.AddRange(alanAdlari.Select(a => new FormAlani { Etiket = a, Yol = $"Esleme[{a}]" }));
        if (!FormPenceresi.Ac("Ekstre Şablonu", m, alanlar, async _ =>
            {
                s.Ad = m.Ad!;
                s.BaslikSatiri = m.BaslikSatiri;
                s.Aktif = m.Aktif;
                s.KolonEsleme = JsonSerializer.Serialize(m.Esleme.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value!.Trim()));
                await servis.SablonKaydetAsync(s);
            })) return;
        await YenileAsync();
    }

    public sealed class SablonModel
    {
        public string? Ad { get; set; }
        public byte BaslikSatiri { get; set; } = 1;
        public bool Aktif { get; set; } = true;
        public Dictionary<string, string?> Esleme { get; } = [];
    }
}

public sealed class LastikListeVM(IDisaAktarici d, IKayitServisi<Lastik> servis, Secimler s, LastikServisi lastik) : KayitListeVM<LastikSatir, Lastik>(d, servis, s)
{
    public override string Baslik => "Lastikler";
    protected override string KayitAdi => "Lastik";
    public override string? OnayFiltreMetni => "Hurdaları da göster";
    protected override object Id(LastikSatir s) => s.Id;
    protected override async Task<IEnumerable<LastikSatir>> YukleAsync() => await lastik.ListeAsync(OnayFiltre);

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Seri No", nameof(LastikSatir.SeriNo)), new("Marka", nameof(LastikSatir.Marka)), new("Ebat", nameof(LastikSatir.Ebat)),
        new("Durum", nameof(LastikSatir.DurumAd)), new("Takılı Olduğu", nameof(LastikSatir.TakiliOlduguYer)), new("Pozisyon", nameof(LastikSatir.Pozisyon)),
        Kolon.Sayi("Toplam Km", nameof(LastikSatir.ToplamKm)), Kolon.Para("Alış Bedeli", nameof(LastikSatir.AlisBedeli), false),
        new("Km Maliyeti", nameof(LastikSatir.KmMaliyet), KolonBicim.Sayi), Kolon.Sayi("Kaplama", nameof(LastikSatir.KaplamaSayisi)),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Tak", SymbolRegular.ArrowDownload24, Tak),
        E("Sök", SymbolRegular.ArrowUpload24, Sok),
        E("Geçmiş", SymbolRegular.History24, Gecmis),
    ];

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(Lastik m, bool yeni) =>
    [
        new() { Etiket = "Seri no", Yol = nameof(Lastik.SeriNo), Zorunlu = true, MaksUzunluk = 30 },
        new() { Etiket = "Marka", Yol = nameof(Lastik.Marka), MaksUzunluk = 40 },
        new() { Etiket = "Desen", Yol = nameof(Lastik.Desen), MaksUzunluk = 40 },
        new() { Etiket = "Ebat", Yol = nameof(Lastik.Ebat), Zorunlu = true, MaksUzunluk = 20, Ipucu = "Örn. 315/80 R22.5" },
        new() { Etiket = "DOT", Yol = nameof(Lastik.DotKodu), MaksUzunluk = 10 },
        new() { Etiket = "Alış tarihi", Yol = nameof(Lastik.AlisTarihi), Tip = AlanTipi.Tarih },
        new() { Etiket = "Alış bedeli", Yol = nameof(Lastik.AlisBedeli), Tip = AlanTipi.Para },
        new() { Etiket = "Tedarikçi", Yol = nameof(Lastik.TedarikciCariId), Tip = AlanTipi.Secim, Secenekler = await Secimler.CarilerAsync(CariTipi.Tedarikci) },
    ];

    private sealed class TakModel
    {
        public short? AracId { get; set; }
        public short? DorseId { get; set; }
        public string? Pozisyon { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public int Km { get; set; }
        public decimal? Dis { get; set; }
        public LastikDurum YeniDurum { get; set; } = LastikDurum.Depoda;
        public string? Aciklama { get; set; }
    }

    private async Task Tak()
    {
        if (Secili is not { } l) return;
        var m = new TakModel();
        var araclar = await Secimler.AraclarAsync();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Araç", Yol = nameof(TakModel.AracId), Tip = AlanTipi.Secim, Secenekler = araclar },
            new() { Etiket = "Dorse", Yol = nameof(TakModel.DorseId), Tip = AlanTipi.Secim, Secenekler = await Secimler.DorselerAsync() },
            new() { Etiket = "Pozisyon", Yol = nameof(TakModel.Pozisyon), Zorunlu = true, Ipucu = "Örn. 1-Sol, 2-Sağ-Dış, Stepne" },
            new() { Etiket = "Tarih", Yol = nameof(TakModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Araç km", Yol = nameof(TakModel.Km), Tip = AlanTipi.Tamsayi, Zorunlu = true },
        };
        if (FormPenceresi.Ac($"Lastik Tak — {l.SeriNo}", m, alanlar, _ => lastik.TakAsync(l.Id, m.AracId, m.DorseId, m.Pozisyon!, m.Tarih, m.Km)))
            await YenileAsync();
    }

    private async Task Sok()
    {
        if (Secili is not { } l) return;
        var m = new TakModel();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(TakModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Araç km", Yol = nameof(TakModel.Km), Tip = AlanTipi.Tamsayi, Zorunlu = true },
            new() { Etiket = "Diş derinliği (mm)", Yol = nameof(TakModel.Dis), Tip = AlanTipi.Ondalik },
            new() { Etiket = "Sonraki durum", Yol = nameof(TakModel.YeniDurum), Tip = AlanTipi.Secim, Secenekler = EnumMetin.Liste<LastikDurum>().Where(x => x.Deger != LastikDurum.Takili).ToList() },
            new() { Etiket = "Açıklama", Yol = nameof(TakModel.Aciklama) },
        };
        if (FormPenceresi.Ac($"Lastik Sök — {l.SeriNo}", m, alanlar, _ => lastik.SokAsync(l.Id, m.Tarih, m.Km, m.Dis, m.YeniDurum, m.Aciklama)))
            await YenileAsync();
    }

    private async Task Gecmis()
    {
        if (Secili is not { } l) return;
        var g = await lastik.GecmisAsync(l.Id);
        await Dialog.Bilgi(g.Count == 0 ? "Hareket yok." : string.Join("\n", g.Select(h =>
            $"• {h.TakmaTarihi:dd.MM.yyyy} {(h.Arac?.Plaka ?? h.Dorse?.Plaka)} {h.Pozisyon}: {h.TakmaKm:N0} km" +
            (h.SokmeTarihi is null ? " → takılı" : $" → {h.SokmeTarihi:dd.MM.yyyy} {h.SokmeKm:N0} km ({h.SokmeKm - h.TakmaKm:N0} km){(h.DisDerinligiMm is null ? "" : $", diş {h.DisDerinligiMm} mm")}"))),
            $"Lastik Geçmişi — {l.SeriNo}");
    }
}
