using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed class AracListeVM(IDisaAktarici d, AracServisi servis, Secimler s) : KayitListeVM<AracSatir, Arac>(d, servis, s)
{
    public override string Baslik => "Araçlar";
    protected override string KayitAdi => "Araç";
    public override string? OnayFiltreMetni => "Pasifleri de göster";
    protected override double FormGenislik => 900;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Plaka", nameof(AracSatir.Plaka), Genislik: 110), new("Tip", nameof(AracSatir.Tip)), new("Marka", nameof(AracSatir.Marka)),
        new("Model", nameof(AracSatir.Model)), new("Yıl", nameof(AracSatir.ModelYili)), new("Mülkiyet", nameof(AracSatir.Mulkiyet)),
        new("Bağlı Dorse", nameof(AracSatir.Dorse)), Kolon.Sayi("Güncel Km", nameof(AracSatir.GuncelKm)),
        Kolon.Sayi("Süresi Dolan Evrak", nameof(AracSatir.SuresiDolanEvrak)), new("Aktif", "Aktif?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Dorse Bağla", SymbolRegular.Link24, DorseBagla),
        E("Evraklar", SymbolRegular.DocumentText24, () => Ekranlar.EvraklarAsync(EvrakHedef.Arac, (Secili?.Id, Secili?.Plaka))),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } a ? EklerPenceresi.AcAsync(EkHedef.Arac, a.Id, a.Plaka) : Task.CompletedTask),
    ];

    protected override object Id(AracSatir s) => s.Id;
    public override bool DetayVar => true;
    protected override Task DetayAsync(AracSatir satir) => Detay.Arac(satir.Id);
    protected override async Task<IEnumerable<AracSatir>> YukleAsync() => await servis.SatirlarAsync(!OnayFiltre);

    protected override Task<IReadOnlyList<FormAlani>> AlanlarAsync(Arac m, bool yeni) => Task.FromResult<IReadOnlyList<FormAlani>>(
    [
        FormAlani.Bolum("Araç Bilgileri"),
        new() { Etiket = "Plaka", Yol = nameof(Arac.Plaka), Zorunlu = true, MaksUzunluk = 15 },
        FormAlani.EnumSecim<AracTipi>("Araç tipi", nameof(Arac.AracTipi)),
        new() { Etiket = "Marka", Yol = nameof(Arac.Marka), MaksUzunluk = 40 },
        new() { Etiket = "Model", Yol = nameof(Arac.Model), MaksUzunluk = 60 },
        new() { Etiket = "Model yılı", Yol = nameof(Arac.ModelYili), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Şasi no", Yol = nameof(Arac.SaseNo), MaksUzunluk = 20 },
        new() { Etiket = "Motor no", Yol = nameof(Arac.MotorNo), MaksUzunluk = 30 },
        new() { Etiket = "HGS etiket no", Yol = nameof(Arac.HgsEtiketNo), MaksUzunluk = 30, Ipucu = "HGS ekstresi plaka yerine etiket no ile eşleşebilir" },
        new() { Etiket = "Güncel km", Yol = nameof(Arac.GuncelKm), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Depo (litre)", Yol = nameof(Arac.YakitDepoLitre), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Beklenen tüketim (L/100km)", Yol = nameof(Arac.BeklenenTuketim), Tip = AlanTipi.Ondalik, Ipucu = "Yakıt sapma alarmı bu değere göre hesaplanır" },
        new() { Etiket = "Aktif", Yol = nameof(Arac.Aktif), Tip = AlanTipi.Onay },
        FormAlani.Bolum("Mülkiyet ve Amortisman"),
        FormAlani.EnumSecim<Mulkiyet>("Mülkiyet", nameof(Arac.Mulkiyet)),
        new() { Etiket = "Alış tarihi", Yol = nameof(Arac.AlisTarihi), Tip = AlanTipi.Tarih },
        new() { Etiket = "Alış bedeli (TL, KDV hariç)", Yol = nameof(Arac.AlisBedeli), Tip = AlanTipi.Para },
        new() { Etiket = "Faydalı ömür (ay)", Yol = nameof(Arac.FaydaliOmurAy), Tip = AlanTipi.Tamsayi, Ipucu = "Örn. 120 ay; araç kârlılığında aylık amortisman olarak gider yazılır" },
        new() { Etiket = "Hurda değeri", Yol = nameof(Arac.HurdaDegeri), Tip = AlanTipi.Para },
        new() { Etiket = "Satış tarihi", Yol = nameof(Arac.SatisTarihi), Tip = AlanTipi.Tarih },
        new() { Etiket = "Notlar", Yol = nameof(Arac.Notlar), Tip = AlanTipi.CokSatir, MaksUzunluk = 500 },
    ]);

    private async Task DorseBagla()
    {
        if (Secili is not { } a) return;
        var m = new DorseBaglaModel { Tarih = DateOnly.FromDateTime(DateTime.Today) };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Dorse", Yol = nameof(DorseBaglaModel.DorseId), Tip = AlanTipi.Secim, Secenekler = await Secimler.DorselerAsync(), Ipucu = "Boş bırakılırsa aracın dorsesi ayrılır" },
            new() { Etiket = "Tarih", Yol = nameof(DorseBaglaModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
        };
        if (FormPenceresi.Ac($"{a.Plaka} — Dorse Bağla", m, alanlar, _ => servis.DorseBaglaAsync(a.Id, m.DorseId, m.Tarih)))
            await YenileAsync();
    }

    private sealed class DorseBaglaModel
    {
        public short? DorseId { get; set; }
        public DateOnly Tarih { get; set; }
    }
}

public sealed class DorseListeVM(IDisaAktarici d, IKayitServisi<Dorse> servis, Secimler s) : BasitKayitListeVM<Dorse>(d, servis, s)
{
    public override string Baslik => "Dorseler";
    protected override string KayitAdi => "Dorse";
    public override string? OnayFiltreMetni => "Pasifleri de göster";
    protected override IQueryable<Dorse> Sorgu(IQueryable<Dorse> q) => q.Where(x => OnayFiltre || x.Aktif).OrderBy(x => x.Plaka);
    protected override object Id(Dorse s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Plaka", nameof(Dorse.Plaka), Genislik: 110), new("Tip", nameof(Dorse.DorseTipi)), new("Marka", nameof(Dorse.Marka)),
        new("Yıl", nameof(Dorse.ModelYili)), Kolon.Sayi("Kapasite (ton)", nameof(Dorse.KapasiteTon)), new("Mülkiyet", nameof(Dorse.Mulkiyet)), new("Aktif", "Aktif?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Evraklar", SymbolRegular.DocumentText24, () => Ekranlar.EvraklarAsync(EvrakHedef.Dorse, (Secili?.Id, Secili?.Plaka))),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } x ? EklerPenceresi.AcAsync(EkHedef.Dorse, x.Id, x.Plaka) : Task.CompletedTask),
    ];

    protected override Task<IReadOnlyList<FormAlani>> AlanlarAsync(Dorse m, bool yeni) => Task.FromResult<IReadOnlyList<FormAlani>>(
    [
        new() { Etiket = "Plaka", Yol = nameof(Dorse.Plaka), Zorunlu = true, MaksUzunluk = 15 },
        FormAlani.EnumSecim<DorseTipi>("Dorse tipi", nameof(Dorse.DorseTipi)),
        new() { Etiket = "Marka", Yol = nameof(Dorse.Marka), MaksUzunluk = 40 },
        new() { Etiket = "Model yılı", Yol = nameof(Dorse.ModelYili), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Şasi no", Yol = nameof(Dorse.SaseNo), MaksUzunluk = 20 },
        new() { Etiket = "Kapasite (ton)", Yol = nameof(Dorse.KapasiteTon), Tip = AlanTipi.Ondalik },
        new() { Etiket = "Hacim (m³)", Yol = nameof(Dorse.HacimM3), Tip = AlanTipi.Ondalik },
        FormAlani.EnumSecim<Mulkiyet>("Mülkiyet", nameof(Dorse.Mulkiyet)),
        new() { Etiket = "Alış tarihi", Yol = nameof(Dorse.AlisTarihi), Tip = AlanTipi.Tarih },
        new() { Etiket = "Alış bedeli", Yol = nameof(Dorse.AlisBedeli), Tip = AlanTipi.Para },
        new() { Etiket = "Faydalı ömür (ay)", Yol = nameof(Dorse.FaydaliOmurAy), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Hurda değeri", Yol = nameof(Dorse.HurdaDegeri), Tip = AlanTipi.Para },
        new() { Etiket = "Aktif", Yol = nameof(Dorse.Aktif), Tip = AlanTipi.Onay },
        new() { Etiket = "Notlar", Yol = nameof(Dorse.Notlar), Tip = AlanTipi.CokSatir, MaksUzunluk = 500 },
    ]);
}

public sealed class SoforListeVM(IDisaAktarici d, IKayitServisi<Sofor> servis, Secimler s) : BasitKayitListeVM<Sofor>(d, servis, s)
{
    public override string Baslik => "Şoförler";
    protected override string KayitAdi => "Şoför";
    public override string? OnayFiltreMetni => "İşten ayrılanları da göster";
    protected override double FormGenislik => 900;
    protected override IQueryable<Sofor> Sorgu(IQueryable<Sofor> q) => q.Include(x => x.VarsayilanArac).Where(x => OnayFiltre || x.Aktif).OrderBy(x => x.Ad).ThenBy(x => x.Soyad);
    protected override object Id(Sofor s) => s.Id;
    public override bool DetayVar => true;
    protected override Task DetayAsync(Sofor satir) => Detay.Sofor(satir.Id);

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Ad", nameof(Sofor.Ad)), new("Soyad", nameof(Sofor.Soyad)), new("TCKN", nameof(Sofor.TCKN)), new("Telefon", nameof(Sofor.Telefon)),
        new("Ehliyet", nameof(Sofor.EhliyetSinifi)), new("Varsayılan Araç", "VarsayilanArac.Plaka"), Kolon.Tarih("İşe Giriş", nameof(Sofor.IseGiris)), new("Aktif", "Aktif?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Evraklar", SymbolRegular.DocumentText24, () => Ekranlar.EvraklarAsync(EvrakHedef.Sofor, (Secili?.Id, Secili?.AdSoyad))),
        E("Hesap Ekstresi", SymbolRegular.ReceiptMoney24, () => Secili is { } x ? EkstrePenceresi.CariAsync(x.CariId, x.AdSoyad) : Task.CompletedTask),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } x ? EklerPenceresi.AcAsync(EkHedef.Sofor, x.Id, x.AdSoyad) : Task.CompletedTask),
    ];

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(Sofor m, bool yeni) =>
    [
        FormAlani.Bolum("Kimlik ve İletişim"),
        new() { Etiket = "T.C. kimlik no", Yol = nameof(Sofor.TCKN), Zorunlu = true, MaksUzunluk = 11 },
        new() { Etiket = "Ad", Yol = nameof(Sofor.Ad), Zorunlu = true, MaksUzunluk = 50 },
        new() { Etiket = "Soyad", Yol = nameof(Sofor.Soyad), Zorunlu = true, MaksUzunluk = 50 },
        new() { Etiket = "Doğum tarihi", Yol = nameof(Sofor.DogumTarihi), Tip = AlanTipi.Tarih },
        new() { Etiket = "Telefon", Yol = nameof(Sofor.Telefon), MaksUzunluk = 20 },
        new() { Etiket = "E-posta", Yol = nameof(Sofor.Eposta), MaksUzunluk = 100 },
        new() { Etiket = "IBAN", Yol = nameof(Sofor.IBAN), MaksUzunluk = 34 },
        new() { Etiket = "Adres", Yol = nameof(Sofor.Adres), Tip = AlanTipi.CokSatir, MaksUzunluk = 300 },
        FormAlani.Bolum("Çalışma"),
        new() { Etiket = "Ehliyet sınıfı", Yol = nameof(Sofor.EhliyetSinifi), MaksUzunluk = 10, Ipucu = "Örn. CE" },
        new() { Etiket = "Ehliyet no", Yol = nameof(Sofor.EhliyetNo), MaksUzunluk = 20 },
        new() { Etiket = "Varsayılan araç", Yol = nameof(Sofor.VarsayilanAracId), Tip = AlanTipi.Secim, Secenekler = await Secimler.AraclarAsync() },
        new() { Etiket = "İşe giriş", Yol = nameof(Sofor.IseGiris), Tip = AlanTipi.Tarih },
        new() { Etiket = "İşten çıkış", Yol = nameof(Sofor.IstenCikis), Tip = AlanTipi.Tarih },
        new() { Etiket = "Aktif", Yol = nameof(Sofor.Aktif), Tip = AlanTipi.Onay },
        FormAlani.Bolum("Hakediş (iç takip)"),
        new() { Etiket = "Aylık maaş (TL)", Yol = nameof(Sofor.MaasTutari), Tip = AlanTipi.Para, Ipucu = "Hakediş hesabında her ay önerilir" },
        FormAlani.EnumSecim<PrimTipi>("Prim tipi", nameof(Sofor.PrimTipi)),
        new() { Etiket = "Prim tutarı", Yol = nameof(Sofor.PrimTutari), Tip = AlanTipi.Para, Gorunur = _ => m.PrimTipi != PrimTipi.Yok, Ipucu = "Sefer başı TL veya km başı TL" },
        new() { Etiket = "Bakiye uyarı limiti", Yol = nameof(Sofor.BakiyeLimiti), Tip = AlanTipi.Para, Ipucu = "Şoförün elindeki para bu tutarı aşınca uyarı verilir" },
        new() { Etiket = "Notlar", Yol = nameof(Sofor.Notlar), Tip = AlanTipi.CokSatir, MaksUzunluk = 500 },
    ];
}

public sealed record EvrakSatiri(int Id, string Tur, string Kime, string? BelgeNo, string? Kurum, DateOnly? Duzenlenme, DateOnly? Bitis, decimal? Tutar, int? KalanGun)
{
    public string Durum => KalanGun switch { null => "", < 0 => $"{-KalanGun} gün geçti", 0 => "Bugün", _ => $"{KalanGun} gün" };
}

public sealed class EvrakListeVM(IDisaAktarici d, EvrakServisi servis, Secimler s, IFiloDbContextFactory f) : KayitListeVM<EvrakSatiri, Evrak>(d, servis, s)
{
    public (EvrakHedef Tip, short Id, string Ad)? HedefFiltre { get; set; }
    public override string Baslik => HedefFiltre is { } h ? $"Evraklar — {h.Ad}" : "Evraklar / Muayene / Sigorta";
    protected override string KayitAdi => "Evrak";
    public override string? OnayFiltreMetni => HedefFiltre is null ? "Yalnızca yaklaşan/süresi dolanlar" : null;
    protected override object Id(EvrakSatiri s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Evrak", nameof(EvrakSatiri.Tur)), new("Araç / Dorse / Şoför", nameof(EvrakSatiri.Kime)), new("Belge No", nameof(EvrakSatiri.BelgeNo)),
        new("Kurum", nameof(EvrakSatiri.Kurum)), Kolon.Tarih("Düzenlenme", nameof(EvrakSatiri.Duzenlenme)), Kolon.Tarih("Bitiş", nameof(EvrakSatiri.Bitis)),
        new("Kalan", nameof(EvrakSatiri.Durum)), Kolon.Para("Tutar", nameof(EvrakSatiri.Tutar), false),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Yenile (yeni dönem)", SymbolRegular.ArrowSync24, Yenileme),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } x ? EklerPenceresi.AcAsync(EkHedef.Evrak, x.Id, $"{x.Tur} - {x.Kime}") : Task.CompletedTask),
    ];

    protected override async Task<IEnumerable<EvrakSatiri>> YukleAsync()
    {
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        await using var db = f.Olustur();
        var q = db.Evraklar.AsNoTracking();
        if (HedefFiltre is { } h)
            q = h.Tip switch
            {
                EvrakHedef.Arac => q.Where(e => e.AracId == h.Id),
                EvrakHedef.Dorse => q.Where(e => e.DorseId == h.Id),
                _ => q.Where(e => e.SoforId == h.Id),
            };
        if (OnayFiltre)
        {
            var uyarilar = (await servis.UyarilarAsync()).Select(u => u.EvrakId).ToHashSet();
            q = q.Where(e => uyarilar.Contains(e.Id));
        }
        var liste = await q.OrderBy(e => e.BitisTarihi)
            .Select(e => new
            {
                e.Id, Tur = e.EvrakTuru!.Ad,
                Kime = e.AracId != null ? e.Arac!.Plaka : e.DorseId != null ? e.Dorse!.Plaka : e.SoforId != null ? e.Sofor!.Ad + " " + e.Sofor.Soyad : "Firma",
                e.BelgeNo, e.Kurum, e.DuzenlenmeTarihi, e.BitisTarihi, e.Tutar,
            }).ToListAsync();
        return liste.Select(e => new EvrakSatiri(e.Id, e.Tur, e.Kime, e.BelgeNo, e.Kurum, e.DuzenlenmeTarihi, e.BitisTarihi, e.Tutar,
            e.BitisTarihi is { } b ? b.DayNumber - bugun.DayNumber : null));
    }

    protected override Evrak YeniModel()
    {
        var e = new Evrak { DuzenlenmeTarihi = DateOnly.FromDateTime(DateTime.Today) };
        if (HedefFiltre is { } h)
        {
            if (h.Tip == EvrakHedef.Arac) e.AracId = h.Id;
            else if (h.Tip == EvrakHedef.Dorse) e.DorseId = h.Id;
            else e.SoforId = h.Id;
        }
        return e;
    }

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(Evrak m, bool yeni)
    {
        await using var db = f.Olustur();
        var turler = await db.EvrakTurleri.AsNoTracking().Where(t => t.Aktif).OrderBy(t => t.Ad).ToListAsync();
        return
        [
            new()
            {
                Etiket = "Evrak türü", Yol = nameof(Evrak.EvrakTuruId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = turler.Select(t => new Secim(t.Id, t.Ad)).ToList(),
                Degisince = p =>
                {
                    // Periyodu tanımlı türde bitiş tarihini öner.
                    var t = turler.FirstOrDefault(x => x.Id == m.EvrakTuruId);
                    if (t?.PeriyotAy is > 0 && m.DuzenlenmeTarihi is { } dz && m.BitisTarihi is null) m.BitisTarihi = dz.AddMonths(t.PeriyotAy.Value).AddDays(-1);
                    return Task.CompletedTask;
                },
            },
            new() { Etiket = "Araç", Yol = nameof(Evrak.AracId), Tip = AlanTipi.Secim, Secenekler = await Secimler.AraclarAsync(false), Ipucu = "Araç, dorse veya şoförden yalnızca biri seçilir; hiçbiri seçilmezse firma evrakı" },
            new() { Etiket = "Dorse", Yol = nameof(Evrak.DorseId), Tip = AlanTipi.Secim, Secenekler = await Secimler.DorselerAsync(false) },
            new() { Etiket = "Şoför", Yol = nameof(Evrak.SoforId), Tip = AlanTipi.Secim, Secenekler = await Secimler.SoforlerAsync(false) },
            new() { Etiket = "Belge / poliçe no", Yol = nameof(Evrak.BelgeNo), MaksUzunluk = 50 },
            new() { Etiket = "Kurum / şirket", Yol = nameof(Evrak.Kurum), MaksUzunluk = 100 },
            new() { Etiket = "Düzenlenme tarihi", Yol = nameof(Evrak.DuzenlenmeTarihi), Tip = AlanTipi.Tarih },
            new() { Etiket = "Bitiş tarihi", Yol = nameof(Evrak.BitisTarihi), Tip = AlanTipi.Tarih },
            new() { Etiket = "Tutar (prim/ücret)", Yol = nameof(Evrak.Tutar), Tip = AlanTipi.Para, Ipucu = "Bilgi amaçlı. Gider olarak işlemek için Masraf kaydı girin." },
            new() { Etiket = "Açıklama", Yol = nameof(Evrak.Aciklama), Tip = AlanTipi.CokSatir, MaksUzunluk = 300 },
        ];
    }

    /// <summary>Seçili evrakın türü/hedefiyle yeni dönem evrakı açar (eski evrak arşivde kalır).</summary>
    private async Task Yenileme()
    {
        if (Secili is not { } x) return;
        var eski = await Servis.GetirAsync(x.Id);
        if (eski is null) return;
        var yeni = new Evrak
        {
            EvrakTuruId = eski.EvrakTuruId, AracId = eski.AracId, DorseId = eski.DorseId, SoforId = eski.SoforId, Kurum = eski.Kurum,
            DuzenlenmeTarihi = eski.BitisTarihi?.AddDays(1) ?? DateOnly.FromDateTime(DateTime.Today),
        };
        await using (var db = f.Olustur())
        {
            var periyot = await db.EvrakTurleri.Where(t => t.Id == yeni.EvrakTuruId).Select(t => t.PeriyotAy).SingleAsync();
            if (periyot is > 0) yeni.BitisTarihi = yeni.DuzenlenmeTarihi!.Value.AddMonths(periyot.Value).AddDays(-1);
        }
        if (await FormAcAsync(yeni, true)) await YenileAsync();
    }
}

public sealed class BakimPlaniListeVM(IDisaAktarici d, IKayitServisi<BakimPlani> servis, Secimler s, BakimServisi bakim) : BasitKayitListeVM<BakimPlani>(d, servis, s)
{
    public override string Baslik => "Bakım Planları";
    protected override string KayitAdi => "Bakım Planı";
    protected override IQueryable<BakimPlani> Sorgu(IQueryable<BakimPlani> q) => q.Include(x => x.Arac).Include(x => x.Dorse).OrderBy(x => x.BakimAdi);
    protected override object Id(BakimPlani s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Araç", "Arac.Plaka"), new("Dorse", "Dorse.Plaka"), new("Bakım", nameof(BakimPlani.BakimAdi)),
        Kolon.Sayi("Periyot (km)", nameof(BakimPlani.PeriyotKm)), Kolon.Sayi("Periyot (gün)", nameof(BakimPlani.PeriyotGun)),
        Kolon.Sayi("Son Km", nameof(BakimPlani.SonKm)), Kolon.Tarih("Son Tarih", nameof(BakimPlani.SonTarih)),
        Kolon.Sayi("Sonraki Km", nameof(BakimPlani.SonrakiKm)), Kolon.Tarih("Sonraki Tarih", nameof(BakimPlani.SonrakiTarih)), new("Aktif", "Aktif?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler => [E("Yaklaşan Bakımlar", SymbolRegular.CalendarClock24, Yaklasanlar)];

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(BakimPlani m, bool yeni) =>
    [
        new() { Etiket = "Araç", Yol = nameof(BakimPlani.AracId), Tip = AlanTipi.Secim, Secenekler = await Secimler.AraclarAsync() },
        new() { Etiket = "Dorse", Yol = nameof(BakimPlani.DorseId), Tip = AlanTipi.Secim, Secenekler = await Secimler.DorselerAsync() },
        new() { Etiket = "Bakım adı", Yol = nameof(BakimPlani.BakimAdi), Zorunlu = true, MaksUzunluk = 100, Ipucu = "Örn. Motor yağı + filtreler" },
        new() { Etiket = "Periyot (km)", Yol = nameof(BakimPlani.PeriyotKm), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Periyot (gün)", Yol = nameof(BakimPlani.PeriyotGun), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Son bakım km", Yol = nameof(BakimPlani.SonKm), Tip = AlanTipi.Tamsayi },
        new() { Etiket = "Son bakım tarihi", Yol = nameof(BakimPlani.SonTarih), Tip = AlanTipi.Tarih },
        new() { Etiket = "Aktif", Yol = nameof(BakimPlani.Aktif), Tip = AlanTipi.Onay },
    ];

    private async Task Yaklasanlar()
    {
        var l = await bakim.YaklasanlarAsync();
        await Dialog.Bilgi(l.Count == 0 ? "Yaklaşan bakım yok." : string.Join("\n", l.Select(y => $"• {y.Plaka} — {y.BakimAdi}: {y.Durum}")), "Yaklaşan Bakımlar");
    }
}
