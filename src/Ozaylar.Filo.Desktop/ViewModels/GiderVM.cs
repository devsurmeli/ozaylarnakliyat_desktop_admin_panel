using Microsoft.EntityFrameworkCore;
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

/// <summary>Masraf formu: sefer listesinden, şoför hesabından ve masraf listesinden ortak kullanılır.</summary>
public static class MasrafFormu
{
    public static async Task<bool> AcAsync(Masraf m, GiderServisi gider, Secimler s, IFiloDbContextFactory f)
    {
        await using var db = f.Olustur();
        var turler = await db.MasrafTurleri.AsNoTracking().Where(t => t.Aktif).OrderBy(t => t.Ad).ToListAsync();
        var kur = App.Al<DovizServisi>();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(Masraf.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new()
            {
                Etiket = "Masraf türü", Yol = nameof(Masraf.MasrafTuruId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = turler.Select(t => new Secim(t.Id, t.Ad)).ToList(),
                Degisince = _ =>
                {
                    if (turler.FirstOrDefault(t => t.Id == m.MasrafTuruId) is { } t && m.Id == 0) m.KdvOrani = t.VarsayilanKdv;
                    return Task.CompletedTask;
                },
            },
            FormAlani.EnumSecim<MasrafMerkezi>("Maliyet merkezi", nameof(Masraf.Merkez)),
            new() { Etiket = "Sefer", Yol = nameof(Masraf.SeferId), Tip = AlanTipi.Secim, Secenekler = await s.SeferlerAsync(), Gorunur = _ => m.Merkez == MasrafMerkezi.Sefer },
            new() { Etiket = "Araç", Yol = nameof(Masraf.AracId), Tip = AlanTipi.Secim, Secenekler = await s.AraclarAsync(), Gorunur = _ => m.Merkez == MasrafMerkezi.Arac },
            new() { Etiket = "Dorse", Yol = nameof(Masraf.DorseId), Tip = AlanTipi.Secim, Secenekler = await s.DorselerAsync(), Gorunur = _ => m.Merkez == MasrafMerkezi.Dorse },
            new() { Etiket = "Tutar (KDV dahil)", Yol = nameof(Masraf.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
            new()
            {
                Etiket = "Para birimi", Yol = nameof(Masraf.ParaBirimi), Tip = AlanTipi.Secim, Secenekler = EnumMetin.Liste<ParaBirimi>(),
                Degisince = async _ => m.Kur = await kur.KurAsync(m.ParaBirimi, m.Tarih) ?? (m.ParaBirimi == ParaBirimi.TRY ? 1 : 0),
            },
            new() { Etiket = "Kur", Yol = nameof(Masraf.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => m.ParaBirimi != ParaBirimi.TRY },
            new() { Etiket = "İndirilebilir KDV %", Yol = nameof(Masraf.KdvOrani), Tip = AlanTipi.Tamsayi, Ipucu = "Faturasız/fişsiz harcamalarda 0" },
            FormAlani.EnumSecim<OdemeSekli>("Ödeme şekli", nameof(Masraf.OdemeSekli)),
            new() { Etiket = "Şoför", Yol = nameof(Masraf.SoforId), Tip = AlanTipi.Secim, Secenekler = await s.SoforlerAsync(), Gorunur = _ => m.OdemeSekli == OdemeSekli.SoforNakit, Ipucu = "Sefer masrafında boş bırakılırsa seferin şoförü alınır" },
            new() { Etiket = "Kasa / banka", Yol = nameof(Masraf.KasaHesapId), Tip = AlanTipi.Secim, Secenekler = await s.KasalarAsync(), Gorunur = _ => m.OdemeSekli == OdemeSekli.Kasa },
            new() { Etiket = "Tedarikçi / kart firması", Yol = nameof(Masraf.CariId), Tip = AlanTipi.Secim, Secenekler = await s.CarilerAsync(CariTipi.Tedarikci), Gorunur = _ => m.OdemeSekli is OdemeSekli.Cari or OdemeSekli.Kart },
            new() { Etiket = "Belge / fiş no", Yol = nameof(Masraf.BelgeNo), MaksUzunluk = 30 },
            new() { Etiket = "Açıklama", Yol = nameof(Masraf.Aciklama), MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac(m.Id == 0 ? "Yeni Masraf" : "Masraf Düzenle", m, alanlar, _ => gider.MasrafKaydetAsync(m), 900, yeni: m.Id == 0);
        if (ok && m.OdemeSekli == OdemeSekli.SoforNakit && !m.Onayli)
            await Dialog.Bilgi("Şoför harcaması onaya düştü. Muhasebe/yönetici onayladıktan sonra mahsuba alınabilir.");
        return ok;
    }
}

public sealed class MasrafListeVM(IDisaAktarici d, GiderServisi gider, Secimler s, IFiloDbContextFactory f, IKullaniciOturumu oturum) : ListeVM<MasrafSatir>(d)
{
    public override string Baslik => "Masraflar";
    public override bool TarihFiltresi => true;
    public override bool YeniVar => oturum.Yetkili(Izinler.GiderYaz);
    public override bool DuzenleVar => oturum.Yetkili(Izinler.GiderYaz);
    public override bool SilVar => oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "İptal Et";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.Tarih("Tarih", nameof(MasrafSatir.Tarih)), new("Tür", nameof(MasrafSatir.Tur)), new("Merkez", nameof(MasrafSatir.MerkezAd)),
        new("Sefer", nameof(MasrafSatir.Sefer)), new("Plaka", nameof(MasrafSatir.Plaka)), new("Şoför", nameof(MasrafSatir.Sofor)),
        Kolon.Para("Tutar", nameof(MasrafSatir.Tutar), false), new("PB", nameof(MasrafSatir.PB)), Kolon.Para("Tutar (TL)", nameof(MasrafSatir.TutarTL)),
        new("Ödeme", nameof(MasrafSatir.OdemeAd)), new("Açıklama", nameof(MasrafSatir.Aciklama), Genislik: 200),
        new("Onaylı", "Onayli?bool"), new("Mahsuplu", "Mahsuplu?bool"), new("İptal", "Iptal?bool"),
    ];

    protected override async Task<IEnumerable<MasrafSatir>> YukleAsync() => await gider.MasraflarAsync(new GiderFiltre(Bas, Bit));
    protected override string? OzetHesapla(IReadOnlyList<MasrafSatir> l) => $"Toplam: {l.Where(x => !x.Iptal).Sum(x => x.TutarTL):N2} TL";

    protected override async Task YeniAsync()
    {
        var kaydedildi = false;
        do
        {
            var m = new Masraf { Tarih = DateOnly.FromDateTime(DateTime.Today), Merkez = MasrafMerkezi.Arac, OdemeSekli = OdemeSekli.Kasa };
            if (!await MasrafFormu.AcAsync(m, gider, s, f)) break;
            kaydedildi = true;
        } while (FormPenceresi.SonYeniIstendi);
        if (kaydedildi) await YenileAsync();
    }

    protected override async Task DuzenleAsync(MasrafSatir satir)
    {
        if (satir.Iptal || satir.Mahsuplu) { await Dialog.Bilgi("İptal edilmiş veya mahsubu yapılmış masraf düzenlenemez."); return; }
        var m = await gider.MasrafGetirAsync(satir.Id);
        if (m is not null && await MasrafFormu.AcAsync(m, gider, s, f)) await YenileAsync();
    }

    protected override async Task SilAsync(MasrafSatir satir)
    {
        var neden = await Dialog.MetinIste("İptal nedeni:", "Masraf iptali");
        if (neden is null) return;
        await gider.MasrafIptalAsync(satir.Id, neden);
        await YenileAsync();
    }
}

public sealed class YakitListeVM(IDisaAktarici d, GiderServisi gider, Secimler s, DovizServisi kur, IKullaniciOturumu oturum) : ListeVM<YakitSatir>(d)
{
    public override string Baslik => "Yakıt Alımları";
    public override bool TarihFiltresi => true;
    public override bool YeniVar => oturum.Yetkili(Izinler.GiderYaz);
    public override bool DuzenleVar => oturum.Yetkili(Izinler.GiderYaz);
    public override bool SilVar => oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "İptal Et";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.TarihSaat("Zaman", nameof(YakitSatir.Zaman)), new("Plaka", nameof(YakitSatir.Plaka)), new("Sefer", nameof(YakitSatir.Sefer)),
        new("Şoför", nameof(YakitSatir.Sofor)), Kolon.Sayi("Km", nameof(YakitSatir.Km)), Kolon.Sayi("Litre", nameof(YakitSatir.Litre), true),
        new("Birim Fiyat", nameof(YakitSatir.BirimFiyat), KolonBicim.Sayi), Kolon.Para("Tutar", nameof(YakitSatir.Tutar)), new("Full", "FullDepo?bool"),
        new("İstasyon", nameof(YakitSatir.Istasyon)), new("Ödeme", nameof(YakitSatir.OdemeAd)), new("Ekstre", "Aktarim?bool"), new("İptal", "Iptal?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler => [E("Tüketim Raporu (L/100km)", SymbolRegular.GasPump24, TuketimAsync)];

    protected override async Task<IEnumerable<YakitSatir>> YukleAsync() => await gider.YakitlarAsync(new GiderFiltre(Bas, Bit));
    protected override string? OzetHesapla(IReadOnlyList<YakitSatir> l) =>
        $"Toplam: {l.Where(x => !x.Iptal).Sum(x => x.Litre):N2} L   •   {l.Where(x => !x.Iptal).Sum(x => x.Tutar):N2} TL";

    protected override async Task YeniAsync()
    {
        var kaydedildi = false;
        do
        {
            if (!await FormAsync(new Yakit { AlimZamani = DateTime.Now, OdemeSekli = OdemeSekli.Kart, FullDepo = true })) break;
            kaydedildi = true;
        } while (FormPenceresi.SonYeniIstendi);
        if (kaydedildi) await YenileAsync();
    }

    protected override async Task DuzenleAsync(YakitSatir satir)
    {
        if (satir.Iptal || satir.Aktarim) { await Dialog.Bilgi("İptal edilmiş veya ekstreden aktarılmış kayıt düzenlenemez."); return; }
        var y = await gider.YakitGetirAsync(satir.Id);
        if (y is not null && await FormAsync(y)) await YenileAsync();
    }

    private async Task<bool> FormAsync(Yakit y)
    {
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Araç", Yol = nameof(Yakit.AracId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.AraclarAsync() },
            new() { Etiket = "Alım zamanı", Yol = nameof(Yakit.AlimZamani), Tip = AlanTipi.TarihSaat, Zorunlu = true },
            new() { Etiket = "Km", Yol = nameof(Yakit.Km), Tip = AlanTipi.Tamsayi, Zorunlu = true },
            new() { Etiket = "Litre", Yol = nameof(Yakit.Litre), Tip = AlanTipi.Ondalik, Zorunlu = true },
            new() { Etiket = "Birim fiyat", Yol = nameof(Yakit.BirimFiyat), Tip = AlanTipi.Ondalik },
            new() { Etiket = "Tutar (KDV dahil)", Yol = nameof(Yakit.Tutar), Tip = AlanTipi.Para, Ipucu = "Boş bırakılırsa litre × birim fiyat" },
            new() { Etiket = "Depo dolduruldu (full)", Yol = nameof(Yakit.FullDepo), Tip = AlanTipi.Onay, Ipucu = "L/100km hesabı full depo alımları arasında yapılır" },
            new() { Etiket = "Sefer (boşsa otomatik)", Yol = nameof(Yakit.SeferId), Tip = AlanTipi.Secim, Secenekler = await s.SeferlerAsync() },
            FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(Yakit.ParaBirimi)),
            new() { Etiket = "Kur", Yol = nameof(Yakit.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => y.ParaBirimi != ParaBirimi.TRY },
            FormAlani.EnumSecim<OdemeSekli>("Ödeme şekli", nameof(Yakit.OdemeSekli)),
            new() { Etiket = "Şoför", Yol = nameof(Yakit.SoforId), Tip = AlanTipi.Secim, Secenekler = await s.SoforlerAsync() },
            new() { Etiket = "Kasa / banka", Yol = nameof(Yakit.KasaHesapId), Tip = AlanTipi.Secim, Secenekler = await s.KasalarAsync(), Gorunur = _ => y.OdemeSekli == OdemeSekli.Kasa },
            new() { Etiket = "İstasyon firması (cari)", Yol = nameof(Yakit.IstasyonCariId), Tip = AlanTipi.Secim, Secenekler = await s.CarilerAsync(CariTipi.Tedarikci), Gorunur = _ => y.OdemeSekli is OdemeSekli.Cari or OdemeSekli.Kart },
            new() { Etiket = "İstasyon", Yol = nameof(Yakit.IstasyonAdi), MaksUzunluk = 100 },
            new() { Etiket = "Fiş no", Yol = nameof(Yakit.FisNo), MaksUzunluk = 30 },
        };
        return FormPenceresi.Ac(y.Id == 0 ? "Yakıt Alımı" : "Yakıt Düzenle", y, alanlar, async onay =>
        {
            if (y.ParaBirimi != ParaBirimi.TRY && y.Kur <= 1) y.Kur = await kur.KurAsync(y.ParaBirimi, y.Tarih) ?? y.Kur;
            await gider.YakitKaydetAsync(y, onay);
        }, 900, yeni: y.Id == 0);
    }

    protected override async Task SilAsync(YakitSatir satir)
    {
        var neden = await Dialog.MetinIste("İptal nedeni:", "Yakıt kaydı iptali");
        if (neden is null) return;
        await gider.YakitIptalAsync(satir.Id, neden);
        await YenileAsync();
    }

    private async Task TuketimAsync()
    {
        var r = await gider.TuketimRaporuAsync(Bas, Bit);
        await Dialog.Bilgi(r.Count == 0 ? "Bu dönemde hesaplanabilir (iki full depo arası) yakıt verisi yok." :
            string.Join("\n", r.Select(x => $"{(x.Alarm ? "⚠ " : "   ")}{x.Plaka,-12} {x.LitreYuzKm?.ToString("N2") ?? "-",8} L/100km   ({x.Km:N0} km, {x.Litre:N0} L)" +
                                             (x.Beklenen is null ? "" : $"   beklenen {x.Beklenen:N1}, sapma %{x.SapmaYuzde:N1}"))),
            $"Yakıt Tüketimi {Bas:dd.MM.yyyy} - {Bit:dd.MM.yyyy}");
    }
}

public sealed class HgsListeVM(IDisaAktarici d, GiderServisi gider) : ListeVM<HgsSatir>(d)
{
    public override string Baslik => "HGS / OGS Geçişleri";
    public override bool TarihFiltresi => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.TarihSaat("Zaman", nameof(HgsSatir.Zaman)), new("Plaka", nameof(HgsSatir.Plaka)), new("Giriş", nameof(HgsSatir.Giris)),
        new("Çıkış", nameof(HgsSatir.Cikis)), Kolon.Para("Tutar", nameof(HgsSatir.Tutar)), new("Sefer", nameof(HgsSatir.Sefer)),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Seferlerle yeniden eşleştir", SymbolRegular.ArrowSync24, async () =>
        {
            var n = await gider.HgsYenidenEslestirAsync(Bas, Bit);
            AnaPencere.Basarili($"{n} geçiş sefere eşleştirildi.");
            await YenileAsync();
        }),
    ];

    protected override async Task<IEnumerable<HgsSatir>> YukleAsync() => await gider.HgsGecisleriAsync(new GiderFiltre(Bas, Bit));
    protected override string? OzetHesapla(IReadOnlyList<HgsSatir> l) =>
        $"Toplam: {l.Sum(x => x.Tutar):N2} TL   •   Seferle eşleşmeyen: {l.Count(x => x.Sefer is null)}";
}

public sealed class CezaListeVM(IDisaAktarici d, GiderServisi gider, Secimler s, IKullaniciOturumu oturum) : ListeVM<CezaSatir>(d)
{
    public override string Baslik => "Trafik Cezaları";
    public override bool TarihFiltresi => true;
    public override bool YeniVar => oturum.Yetkili(Izinler.GiderYaz);
    public override bool DuzenleVar => oturum.Yetkili(Izinler.GiderYaz);

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.Tarih("Tarih", nameof(CezaSatir.Tarih)), new("Plaka", nameof(CezaSatir.Plaka)), new("Şoför", nameof(CezaSatir.Sofor)),
        new("Ceza", nameof(CezaSatir.CezaTuru), Genislik: 220), Kolon.Para("Tutar", nameof(CezaSatir.Tutar)), Kolon.Para("İndirimli", nameof(CezaSatir.IndirimliTutar), false),
        Kolon.Tarih("Ödeme", nameof(CezaSatir.OdemeTarihi)), new("Şoföre Rücu", "RucuEdilsin?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler => [E("Öde", SymbolRegular.Payment24, OdeAsync)];

    protected override async Task<IEnumerable<CezaSatir>> YukleAsync() => await gider.CezalarAsync(Bas, Bit);

    protected override async Task YeniAsync()
    {
        if (await FormAsync(new Ceza { Tarih = DateOnly.FromDateTime(DateTime.Today) })) await YenileAsync();
    }

    protected override async Task DuzenleAsync(CezaSatir satir)
    {
        var c = await gider.CezaGetirAsync(satir.Id);
        if (c is not null && await FormAsync(c)) await YenileAsync();
    }

    private async Task<bool> FormAsync(Ceza c)
    {
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(Ceza.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Araç", Yol = nameof(Ceza.AracId), Tip = AlanTipi.Secim, Secenekler = await s.AraclarAsync() },
            new() { Etiket = "Şoför", Yol = nameof(Ceza.SoforId), Tip = AlanTipi.Secim, Secenekler = await s.SoforlerAsync() },
            new() { Etiket = "Sefer", Yol = nameof(Ceza.SeferId), Tip = AlanTipi.Secim, Secenekler = await s.SeferlerAsync() },
            new() { Etiket = "Ceza türü / madde", Yol = nameof(Ceza.CezaTuru), Zorunlu = true, MaksUzunluk = 100, Ipucu = "Örn. Hız ihlali, aşırı yükleme, takograf" },
            new() { Etiket = "Tutanak no", Yol = nameof(Ceza.TutanakNo), MaksUzunluk = 30 },
            new() { Etiket = "Tutar", Yol = nameof(Ceza.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
            new() { Etiket = "İndirimli tutar", Yol = nameof(Ceza.IndirimliTutar), Tip = AlanTipi.Para, Ipucu = "Erken ödeme indirimi" },
            new() { Etiket = "Tebliğ tarihi", Yol = nameof(Ceza.TebligTarihi), Tip = AlanTipi.Tarih },
            new() { Etiket = "Şoföre rücu edilsin", Yol = nameof(Ceza.RucuEdilsin), Tip = AlanTipi.Onay, Ipucu = "Seçilirse şoför carisine borç yazılır" },
            new() { Etiket = "Açıklama", Yol = nameof(Ceza.Aciklama), Tip = AlanTipi.CokSatir, MaksUzunluk = 300 },
        };
        return FormPenceresi.Ac(c.Id == 0 ? "Yeni Ceza" : "Ceza Düzenle", c, alanlar, _ => gider.CezaKaydetAsync(c));
    }

    private sealed class OdeModel
    {
        public byte? KasaId { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public decimal Tutar { get; set; }
    }

    private async Task OdeAsync()
    {
        if (Secili is not { } c) return;
        if (c.OdemeTarihi is not null) { await Dialog.Bilgi("Ceza zaten ödenmiş."); return; }
        var m = new OdeModel { Tutar = c.IndirimliTutar ?? c.Tutar };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Kasa / banka", Yol = nameof(OdeModel.KasaId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.KasalarAsync(ParaBirimi.TRY) },
            new() { Etiket = "Tarih", Yol = nameof(OdeModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Ödenen tutar", Yol = nameof(OdeModel.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
        };
        if (FormPenceresi.Ac($"Ceza Ödemesi — {c.CezaTuru}", m, alanlar, _ => gider.CezaOdeAsync(c.Id, m.KasaId!.Value, m.Tarih, m.Tutar)))
            await YenileAsync();
    }
}
