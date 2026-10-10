using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
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

/// <summary>Kredi işlemleri (liste, takvim ve detay ekranları ortak kullanır). Kaydedilirse true döner.</summary>
public static class KrediIslemleri
{
    private static KrediServisi Servis => App.Al<KrediServisi>();
    private static Secimler S => App.Al<Secimler>();
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.Today);

    private sealed class KrediForm
    {
        public Kredi K { get; set; } = new();
        public List<short> Araclar { get; set; } = [];
        public List<short> Dorseler { get; set; } = [];
    }

    /// <summary>Yeni kredi (id null) veya düzenleme. Yeni kayıtta kaydedilen kredinin Id'si döner.</summary>
    public static async Task<short?> FormAsync(short? id)
    {
        var k = id is { } i ? await Servis.GetirAsync(i) ?? throw new DomainException("Kredi bulunamadı.") : new Kredi
        {
            KullandirimTarihi = Bugun, IlkVade = Bugun.AddMonths(1), VadeSayisi = 36, FaizTipi = FaizTipi.Aylik,
        };
        var m = new KrediForm
        {
            K = k,
            Araclar = k.Araclar.Where(a => a.AracId is not null).Select(a => a.AracId!.Value).ToList(),
            Dorseler = k.Araclar.Where(a => a.DorseId is not null).Select(a => a.DorseId!.Value).ToList(),
        };
        var odemeVar = id is not null && (await Servis.DetayAsync(id.Value)).Odemeler.Any(o => !o.Iptal);
        const string P = nameof(KrediForm.K) + ".";
        var alanlar = new List<FormAlani>
        {
            FormAlani.Bolum("Sözleşme"),
            new() { Etiket = "Kredi / sözleşme no", Yol = P + nameof(Kredi.KrediNo), Zorunlu = true, MaksUzunluk = 40 },
            new()
            {
                Etiket = "Kredi türü", Yol = P + nameof(Kredi.Tur), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = EnumMetin.Liste<KrediTuru>(),
                Degisince = _ =>
                {
                    if (k.Tur == KrediTuru.Leasing) k.BsmvOrani = 0;
                    if (k.Tur is KrediTuru.Rotatif or KrediTuru.KrediKarti) { k.Yontem = PlanYontemi.Manuel; k.Amac = KrediAmaci.Isletme; }
                    return Task.CompletedTask;
                },
            },
            new() { Etiket = "Banka / leasing şirketi", Yol = P + nameof(Kredi.BankaCariId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await S.CarilerAsync(CariTipi.Finans),
                    Ipucu = "Cari kartında 'Finans kurumu' tipi işaretli olmalı" },
            FormAlani.EnumSecim<ParaBirimi>("Para birimi", P + nameof(Kredi.ParaBirimi)),
            FormAlani.EnumSecim<KrediAmaci>("Amaç", P + nameof(Kredi.Amac)),

            FormAlani.Bolum("Kullandırım"),
            new() { Etiket = "Kullandırım tarihi", Yol = P + nameof(Kredi.KullandirimTarihi), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Kredi tutarı (anapara)", Yol = P + nameof(Kredi.Anapara), Tip = AlanTipi.Para, Zorunlu = true },
            new() { Etiket = "Yatırıldığı hesap", Yol = P + nameof(Kredi.KullandirimKasaHesapId), Tip = AlanTipi.Secim, Secenekler = await S.KasalarAsync(),
                    Ipucu = "Boş: para doğrudan satıcıya ödendi (leasing / taşıt kredisi). Doluysa hesaba giriş yazılır." },
            new() { Etiket = "Kullandırım kuru", Yol = P + nameof(Kredi.KullandirimKur), Tip = AlanTipi.Ondalik, Gorunur = _ => k.ParaBirimi != ParaBirimi.TRY },
            new() { Etiket = "Dosya masrafı", Yol = P + nameof(Kredi.DosyaMasrafi), Tip = AlanTipi.Para, Ipucu = "Kullandırımdan kesilen masraf (hesaba net tutar girer)" },

            FormAlani.Bolum("Faiz ve ödeme planı"),
            new() { Etiket = "Faiz oranı %", Yol = P + nameof(Kredi.FaizOrani), Tip = AlanTipi.Ondalik, Zorunlu = true, Ipucu = "Sözleşmedeki akdi faiz (vergiler hariç)" },
            FormAlani.EnumSecim<FaizTipi>("Faiz tipi", P + nameof(Kredi.FaizTipi)),
            new() { Etiket = "BSMV %", Yol = P + nameof(Kredi.BsmvOrani), Tip = AlanTipi.Ondalik, Ipucu = "Faiz üzerinden; ticari kredide genelde %5, leasingde 0" },
            new() { Etiket = "KKDF %", Yol = P + nameof(Kredi.KkdfOrani), Tip = AlanTipi.Ondalik, Ipucu = "Faiz üzerinden; TL ticari kredide genelde 0" },
            FormAlani.EnumSecim<PlanYontemi>("Plan yöntemi", P + nameof(Kredi.Yontem)),
            new() { Etiket = "Vade sayısı (ay)", Yol = P + nameof(Kredi.VadeSayisi), Tip = AlanTipi.Tamsayi, Gorunur = _ => k.Yontem != PlanYontemi.Manuel },
            new() { Etiket = "İlk taksit vadesi", Yol = P + nameof(Kredi.IlkVade), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Ödemesiz dönem (ay)", Yol = P + nameof(Kredi.OdemesizAy), Tip = AlanTipi.Tamsayi, Gorunur = _ => k.Yontem != PlanYontemi.Manuel,
                    Ipucu = "Bu aylarda yalnız faiz ödenir" },
            new() { Etiket = "Balon (son) ödeme", Yol = P + nameof(Kredi.BalonTutar), Tip = AlanTipi.Para, Gorunur = _ => k.Yontem == PlanYontemi.Balon },
            new() { Etiket = "Gecikme faizi artışı %", Yol = P + nameof(Kredi.GecikmeArtisYuzde), Tip = AlanTipi.Ondalik,
                    Ipucu = "Gecikme faizi = akdi faiz × (1 + bu oran); bankalarda genelde %30" },

            FormAlani.Bolum("Bağlı araçlar (maliyet paylaşımı — eşit pay)"),
            new() { Etiket = "Araçlar", Yol = nameof(KrediForm.Araclar), Tip = AlanTipi.CokluSecim, Secenekler = await S.AraclarAsync(false) },
            new() { Etiket = "Dorseler", Yol = nameof(KrediForm.Dorseler), Tip = AlanTipi.CokluSecim, Secenekler = await S.DorselerAsync(false) },
            new() { Etiket = "Açıklama", Yol = P + nameof(Kredi.Aciklama), Tip = AlanTipi.CokSatir, MaksUzunluk = 300 },
        };
        short? kaydedilen = null;
        var baslik = id is null ? "Yeni Kredi / Leasing" : odemeVar ? "Kredi Düzenle (ödemeli: yalnız açıklama ve araçlar değişir)" : "Kredi Düzenle";
        var ok = FormPenceresi.Ac(baslik, m, alanlar, async _ =>
        {
            var toplam = m.Araclar.Count + m.Dorseler.Count;
            var pay = toplam == 0 ? 0 : Math.Round(100m / toplam, 2);
            var bagli = m.Araclar.Select(a => new KrediArac { AracId = a, PayYuzde = pay })
                .Concat(m.Dorseler.Select(d => new KrediArac { DorseId = d, PayYuzde = pay })).ToList();
            var sonuc = await Servis.KaydetAsync(k, null, bagli);
            kaydedilen = sonuc.Id;
        }, 960);
        if (!ok) return null;
        AnaPencere.Basarili("Kredi kaydedildi.");
        return kaydedilen;
    }

    private sealed class OdeModel
    {
        public int? TaksitId { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public byte? KasaId { get; set; }
        public decimal Tutar { get; set; }
        public decimal Gecikme { get; set; }
        public decimal Kur { get; set; } = 1;
        public decimal Anapara { get; set; }
        public decimal Faiz { get; set; }
        public decimal Bsmv { get; set; }
        public decimal Kkdf { get; set; }
        public decimal KomisyonYuzde { get; set; }
        public YenidenPlanModu Mod { get; set; } = YenidenPlanModu.VadeyiKisalt;
        public string? Aciklama { get; set; }
    }

    private static async Task<decimal> KurAsync(ParaBirimi pb, DateOnly tarih) =>
        pb == ParaBirimi.TRY ? 1 : await App.Al<DovizServisi>().KurAsync(pb, tarih) ?? 1;

    public static async Task<bool> TaksitOdeAsync(short krediId, int? taksitId = null)
    {
        var d = await Servis.DetayAsync(krediId);
        var acik = d.Plan.Where(p => p.Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi && p.Versiyon == d.Kredi.AktifVersiyon).OrderBy(p => p.Vade).ToList();
        if (acik.Count == 0) { await Dialog.Bilgi(d.Kredi.Yontem == PlanYontemi.Manuel ? "Bu kredinin taksit planı yok; 'Serbest Ödeme' kullanın." : "Açık taksit yok."); return false; }
        var pb = d.Kredi.ParaBirimi;
        var m = new OdeModel { TaksitId = taksitId ?? acik[0].Id, Kur = await KurAsync(pb, Bugun) };
        async Task Hesapla()
        {
            if (m.TaksitId is not { } t) return;
            var o = await Servis.TaksitOnizleAsync(t, m.Tarih);
            m.Gecikme = o.GecikmeFaizi;
            m.Tutar = o.Toplam;
        }
        await Hesapla();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Taksit", Yol = nameof(OdeModel.TaksitId), Tip = AlanTipi.Secim, Zorunlu = true, Degisince = _ => Hesapla(),
                    Secenekler = acik.Select(p => new Secim(p.Id, $"{p.No}. taksit — {p.Vade:dd.MM.yyyy} — {p.Kalan:N2} {pb.Ad()}{(p.Gecikti ? " (gecikmiş)" : "")}")).ToList() },
            new() { Etiket = "Ödeme tarihi", Yol = nameof(OdeModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true, Degisince = _ => Hesapla() },
            new() { Etiket = "Ödenen hesap", Yol = nameof(OdeModel.KasaId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await S.KasalarAsync(),
                    Ipucu = "Kredi para birimindeki hesap veya TL hesap (kur ile)" },
            new() { Etiket = $"Gecikme faizi ({pb.Ad()})", Yol = nameof(OdeModel.Gecikme), Tip = AlanTipi.Para, Ipucu = "Geç ödemede önerilir; banka dekontuna göre düzeltin" },
            new() { Etiket = $"Ödenen toplam ({pb.Ad()})", Yol = nameof(OdeModel.Tutar), Tip = AlanTipi.Para, Zorunlu = true, Ipucu = "Eksik tutar kısmi ödeme olarak işlenir" },
            new() { Etiket = "Kur (TL)", Yol = nameof(OdeModel.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => pb != ParaBirimi.TRY },
        };
        var ok = FormPenceresi.Ac($"Taksit Öde — {d.Banka} {d.Kredi.KrediNo}", m, alanlar,
            _ => Servis.TaksitOdeAsync(m.TaksitId!.Value, m.Tarih, m.KasaId!.Value, m.Tutar, m.Gecikme, m.Kur));
        if (ok) AnaPencere.Basarili("Taksit ödemesi kaydedildi.");
        return ok;
    }

    public static async Task<bool> AraOdemeAsync(short krediId)
    {
        var d = await Servis.DetayAsync(krediId);
        var pb = d.Kredi.ParaBirimi;
        var m = new OdeModel { Kur = await KurAsync(pb, Bugun) };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(OdeModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Ödenen hesap", Yol = nameof(OdeModel.KasaId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await S.KasalarAsync() },
            new() { Etiket = $"Ödenecek anapara ({pb.Ad()})", Yol = nameof(OdeModel.Anapara), Tip = AlanTipi.Para, Zorunlu = true,
                    Ipucu = $"Kalan anapara: {d.Ozet.KalanAnapara:N2}. İşlemiş faiz otomatik eklenir." },
            FormAlani.EnumSecim<YenidenPlanModu>("Yeni plan", nameof(OdeModel.Mod)),
            new() { Etiket = "Erken ödeme komisyonu %", Yol = nameof(OdeModel.KomisyonYuzde), Tip = AlanTipi.Ondalik, Ipucu = "Ticari kredide genelde %0-2" },
            new() { Etiket = "Kur (TL)", Yol = nameof(OdeModel.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => pb != ParaBirimi.TRY },
        };
        var ok = FormPenceresi.Ac($"Ara (Kısmi) Ödeme — {d.Banka} {d.Kredi.KrediNo}", m, alanlar,
            _ => Servis.AraOdemeAsync(krediId, m.Tarih, m.KasaId!.Value, m.Anapara, m.Mod, m.KomisyonYuzde, m.Kur));
        if (ok) AnaPencere.Basarili("Ara ödeme kaydedildi; yeni ödeme planı oluşturuldu.");
        return ok;
    }

    public static async Task<bool> ErkenKapatAsync(short krediId)
    {
        var d = await Servis.DetayAsync(krediId);
        var pb = d.Kredi.ParaBirimi;
        var m = new OdeModel { Kur = await KurAsync(pb, Bugun) };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Kapama tarihi", Yol = nameof(OdeModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Ödenen hesap", Yol = nameof(OdeModel.KasaId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await S.KasalarAsync() },
            new() { Etiket = "Erken kapama komisyonu %", Yol = nameof(OdeModel.KomisyonYuzde), Tip = AlanTipi.Ondalik },
            new() { Etiket = "Kur (TL)", Yol = nameof(OdeModel.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => pb != ParaBirimi.TRY },
        };
        var ok = FormPenceresi.Ac($"Erken Kapama — {d.Banka} {d.Kredi.KrediNo}", m, alanlar, async _ =>
        {
            var o = await Servis.ErkenKapamaOnizleAsync(krediId, m.Tarih, m.KomisyonYuzde);
            var metin = $"Kalan anapara: {o.KalanAnapara:N2}\nİşlemiş faiz: {o.Faiz:N2}\nVergiler (BSMV/KKDF): {o.Vergi:N2}\nKomisyon: {o.Komisyon:N2}" +
                        (o.GecikenTaksitler > 0 ? $"\nGecikmiş taksitler: {o.GecikenTaksitler:N2}" : "") +
                        $"\n\nTOPLAM: {o.Toplam:N2} {pb.Ad()}\n\nKredi kapatılsın mı?";
            if (!await Dialog.Onay(metin, "Erken kapama tutarı", "Kapat")) throw new DomainException("Erken kapama yapılmadı.");
            await Servis.ErkenKapatAsync(krediId, m.Tarih, m.KasaId!.Value, m.KomisyonYuzde, m.Kur);
        });
        if (ok) AnaPencere.Basarili("Kredi kapatıldı.");
        return ok;
    }

    public static async Task<bool> SerbestOdemeAsync(short krediId)
    {
        var d = await Servis.DetayAsync(krediId);
        var pb = d.Kredi.ParaBirimi;
        var m = new OdeModel { Kur = await KurAsync(pb, Bugun) };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(OdeModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Ödenen hesap", Yol = nameof(OdeModel.KasaId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await S.KasalarAsync() },
            new() { Etiket = $"Anapara ({pb.Ad()})", Yol = nameof(OdeModel.Anapara), Tip = AlanTipi.Para, Ipucu = $"Kalan anapara: {d.Ozet.KalanAnapara:N2}" },
            new() { Etiket = "Faiz", Yol = nameof(OdeModel.Faiz), Tip = AlanTipi.Para },
            new() { Etiket = "BSMV", Yol = nameof(OdeModel.Bsmv), Tip = AlanTipi.Para },
            new() { Etiket = "KKDF", Yol = nameof(OdeModel.Kkdf), Tip = AlanTipi.Para },
            new() { Etiket = "Kur (TL)", Yol = nameof(OdeModel.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => pb != ParaBirimi.TRY },
            new() { Etiket = "Açıklama", Yol = nameof(OdeModel.Aciklama), MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac($"Serbest Ödeme — {d.Banka} {d.Kredi.KrediNo}", m, alanlar,
            _ => Servis.SerbestOdemeAsync(krediId, m.Tarih, m.KasaId!.Value, m.Anapara, m.Faiz, m.Bsmv, m.Kkdf, m.Kur, m.Aciklama));
        if (ok) AnaPencere.Basarili("Ödeme kaydedildi.");
        return ok;
    }

    /// <summary>Bankadan alınan ödeme planını (Excel/CSV) içe aktarır; kuruşu kuruşuna banka planı esas olur.</summary>
    public static async Task<bool> PlanExceldenAsync(short krediId)
    {
        var d = await Servis.DetayAsync(krediId);
        var dlg = new OpenFileDialog { Filter = "Excel / CSV|*.xlsx;*.xls;*.csv", Title = "Banka ödeme planı dosyası" };
        if (dlg.ShowDialog() != true) return false;
        var icerik = await File.ReadAllBytesAsync(dlg.FileName);
        List<KrediTaksit>? Dene(int baslik)
        {
            try { return KrediServisi.BankaPlaniAyristir(App.Al<IExcelOkuyucu>().Oku(new MemoryStream(icerik), dlg.FileName, baslik), d.Ozet.KalanAnapara); }
            catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or KeyNotFoundException) { return null; }
        }
        // Banka dosyalarında üstte logo/müşteri bilgisi olur: başlık satırı ilk 25 satırda otomatik aranır.
        var plan = Enumerable.Range(1, 25).Select(Dene).FirstOrDefault(p => p is { Count: > 0 });
        if (plan is null)
        {
            var satirNo = await Dialog.MetinIste("Başlık satırı otomatik bulunamadı. Vade / Anapara / Faiz yazan satırın numarası:", "Banka planı", "1");
            if (satirNo is null || !int.TryParse(satirNo, out var baslik) || baslik < 1) return false;
            plan = KrediServisi.BankaPlaniAyristir(App.Al<IExcelOkuyucu>().Oku(new MemoryStream(icerik), dlg.FileName, baslik), d.Ozet.KalanAnapara);
        }
        var pb = d.Kredi.ParaBirimi.Ad();
        var ozet = $"{plan.Count} taksit okundu ({plan[0].VadeTarihi:dd.MM.yyyy} – {plan[^1].VadeTarihi:dd.MM.yyyy})\n\n" +
                   $"Anapara: {plan.Sum(t => t.Anapara):N2} {pb}\nFaiz: {plan.Sum(t => t.Faiz):N2}\nBSMV+KKDF: {plan.Sum(t => t.Bsmv + t.Kkdf):N2}\n" +
                   $"Toplam geri ödeme: {plan.Sum(t => t.Toplam):N2} {pb}\n\n" +
                   (d.Odemeler.Any(o => !o.Iptal) ? "Ödemesi olan kredide açık taksitler yeni plan versiyonuyla değiştirilir." : "Mevcut plan bu planla değiştirilecek.") +
                   "\nDevam edilsin mi?";
        if (!await Dialog.Onay(ozet, "Banka planı", "Planı al")) return false;
        await Servis.PlanDegistirAsync(krediId, plan);
        AnaPencere.Basarili("Banka ödeme planı alındı.");
        return true;
    }

    /// <summary>Mevcut açık planı Excel şablonu olarak verir: bankanın rakamlarıyla düzeltilip "Planı Excel'den Al" ile geri yüklenebilir.</summary>
    public static async Task SablonAsync(short krediId)
    {
        var d = await Servis.DetayAsync(krediId);
        var acik = d.Plan.Where(p => p.Versiyon == d.Kredi.AktifVersiyon && p.Durum is TaksitDurum.Bekliyor or TaksitDurum.Kismi).ToList();
        var tablo = KolonOlusturucu.Tablo($"Banka Plani {d.Kredi.KrediNo}",
        [
            Kolon.Sayi("Taksit No", nameof(PlanSatir.No)), Kolon.Tarih("Vade Tarihi", nameof(PlanSatir.Vade)), Kolon.Para("Anapara", nameof(PlanSatir.Anapara), false),
            Kolon.Para("Faiz", nameof(PlanSatir.Faiz), false), Kolon.Para("BSMV", nameof(PlanSatir.Bsmv), false), Kolon.Para("KKDF", nameof(PlanSatir.Kkdf), false),
            Kolon.Para("Taksit Tutarı", nameof(PlanSatir.Toplam), false), Kolon.Para("Kalan Anapara", nameof(PlanSatir.KalanAnapara), false),
        ], acik, "Açık taksitler. Bankanın planına göre düzeltip 'Planı Excel'den Al' ile yükleyin.");
        var kaydet = new SaveFileDialog { FileName = $"Banka plani {d.Kredi.KrediNo}.xlsx", Filter = "Excel (*.xlsx)|*.xlsx" };
        if (kaydet.ShowDialog() != true) return;
        await File.WriteAllBytesAsync(kaydet.FileName, App.Al<IDisaAktarici>().Excel(tablo));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(kaydet.FileName) { UseShellExecute = true });
    }

    private sealed class FaizModel
    {
        public decimal Oran { get; set; }
        public FaizTipi Tip { get; set; }
    }

    /// <summary>Değişken faiz: yeni oranla kalan taksitler yeniden planlanır.</summary>
    public static async Task<bool> FaizDegistirAsync(short krediId)
    {
        var d = await Servis.DetayAsync(krediId);
        var m = new FaizModel { Oran = d.Kredi.FaizOrani, Tip = d.Kredi.FaizTipi };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Yeni faiz oranı %", Yol = nameof(FaizModel.Oran), Tip = AlanTipi.Ondalik, Zorunlu = true,
                    Ipucu = $"Şu anki oran %{d.Kredi.FaizOrani:0.##}; kalan anapara {d.Ozet.KalanAnapara:N2} kalan vadede yeniden taksitlendirilir" },
            FormAlani.EnumSecim<FaizTipi>("Faiz tipi", nameof(FaizModel.Tip)),
        };
        var ok = FormPenceresi.Ac($"Faiz Oranı Değiştir — {d.Banka} {d.Kredi.KrediNo}", m, alanlar, _ => Servis.FaizDegistirAsync(krediId, m.Oran, m.Tip));
        if (ok) AnaPencere.Basarili("Faiz oranı değişti; kalan taksitler yeniden planlandı.");
        return ok;
    }

    public static async Task YazdirAsync(short krediId)
    {
        var d = await Servis.DetayAsync(krediId);
        await Belge.AcAsync($"Ödeme Planı {d.Kredi.KrediNo}", () => App.Al<IBelgeUretici>().KrediOdemePlani(d));
    }

    public static async Task<bool> SilAsync(short krediId, string ad)
    {
        if (!await Dialog.Onay($"{ad} kredisi silinsin mi? (Kullandırım kaydı ters çevrilir.)", "Silme onayı", "Sil")) return false;
        await Servis.SilAsync(krediId);
        AnaPencere.Basarili("Kredi silindi.");
        return true;
    }

    public static Task DetayAc(short krediId)
    {
        Gezgin.Ac(new KrediDetayVM(krediId));
        return Task.CompletedTask;
    }
}

public sealed class KrediListeVM(IDisaAktarici d, KrediServisi servis, IKullaniciOturumu oturum) : ListeVM<KrediSatir>(d)
{
    public override string Baslik => "Krediler / Leasing";
    public override string? OnayFiltreMetni => "Kapananları da göster";
    public override bool YeniVar => oturum.Yetkili(Izinler.FinansmanYaz);
    public override bool DuzenleVar => oturum.Yetkili(Izinler.FinansmanYaz);
    public override bool SilVar => oturum.Yetkili(Izinler.FinansmanYaz);
    public override bool DetayVar => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Banka", nameof(KrediSatir.Banka), Genislik: 170), new("Kredi No", nameof(KrediSatir.KrediNo)), new("Tür", nameof(KrediSatir.TurAd)),
        new("PB", nameof(KrediSatir.PB)), Kolon.Para("Çekilen", nameof(KrediSatir.Anapara), false),
        Kolon.Para("Ödenen Anapara", nameof(KrediSatir.OdenenAnapara), false), Kolon.Para("Kalan Anapara", nameof(KrediSatir.KalanAnapara), false),
        Kolon.Para("Ödenen Faiz", nameof(KrediSatir.OdenenFaiz), false), Kolon.Para("Kalan Faiz", nameof(KrediSatir.KalanFaiz), false),
        new("Taksit", nameof(KrediSatir.TaksitMetni)), Kolon.Tarih("Sonraki Vade", nameof(KrediSatir.SonrakiVade)),
        Kolon.Para("Sonraki Tutar", nameof(KrediSatir.SonrakiTutar), false), new("İlerleme", nameof(KrediSatir.Ilerleme), KolonBicim.Yuzde, 70),
        new("Araçlar", nameof(KrediSatir.Araclar), Genislik: 140), new("Durum", nameof(KrediSatir.DurumAd)),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Taksit Öde", SymbolRegular.Payment24, async () => { if (Secili is { } k && await KrediIslemleri.TaksitOdeAsync(k.Id)) await YenileAsync(); }, true),
        E("Ara Ödeme", SymbolRegular.MoneyHand24, async () => { if (Secili is { } k && await KrediIslemleri.AraOdemeAsync(k.Id)) await YenileAsync(); }),
        E("Erken Kapat", SymbolRegular.LockClosed24, async () => { if (Secili is { } k && await KrediIslemleri.ErkenKapatAsync(k.Id)) await YenileAsync(); }),
        E("Serbest Ödeme (rotatif / kart)", SymbolRegular.Money24, async () => { if (Secili is { } k && await KrediIslemleri.SerbestOdemeAsync(k.Id)) await YenileAsync(); }),
        E("Planı Excel'den Al", SymbolRegular.DocumentTable24, async () => { if (Secili is { } k && await KrediIslemleri.PlanExceldenAsync(k.Id)) await YenileAsync(); }),
        E("Faiz Oranı Değiştir", SymbolRegular.DataTrending24, async () => { if (Secili is { } k && await KrediIslemleri.FaizDegistirAsync(k.Id)) await YenileAsync(); }),
        E("Ödeme Planı Yazdır", SymbolRegular.Print24, () => Secili is { } k ? KrediIslemleri.YazdirAsync(k.Id) : Task.CompletedTask),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } k ? EklerPenceresi.AcAsync(EkHedef.Finansman, k.Id, $"{k.Banka} {k.KrediNo}") : Task.CompletedTask),
        E("Banka Limitleri", SymbolRegular.BuildingBank24, () => { BankaLimitListeVM.Ac(); return Task.CompletedTask; }),
    ];

    protected override async Task<IEnumerable<KrediSatir>> YukleAsync() => await servis.ListeAsync(OnayFiltre);
    protected override Task DetayAsync(KrediSatir satir) => KrediIslemleri.DetayAc(satir.Id);

    protected override string? OzetHesapla(IReadOnlyList<KrediSatir> l) => l.Count == 0 ? null : "Kalan anapara — " + string.Join("   ",
        l.Where(k => k.Durum == KrediDurum.Aktif).GroupBy(k => k.PB).Select(g => $"{g.Key}: {g.Sum(k => k.KalanAnapara):N2} (faiz {g.Sum(k => k.KalanFaiz):N2})"))
        + (l.Sum(k => k.GecikenTaksit) is var g2 and > 0 ? $"   •   Geciken taksit: {g2}" : "");

    protected override async Task YeniAsync()
    {
        if (await KrediIslemleri.FormAsync(null) is { } id)
        {
            await YenileAsync();
            await KrediIslemleri.DetayAc(id);
        }
    }

    protected override async Task DuzenleAsync(KrediSatir satir)
    {
        if (await KrediIslemleri.FormAsync(satir.Id) is not null) await YenileAsync();
    }

    protected override async Task SilAsync(KrediSatir satir)
    {
        if (await KrediIslemleri.SilAsync(satir.Id, $"{satir.Banka} {satir.KrediNo}")) await YenileAsync();
    }
}

public sealed class KrediTakvimVM : ListeVM<TakvimSatir>
{
    private readonly KrediServisi _servis;

    public KrediTakvimVM(IDisaAktarici d, KrediServisi servis) : base(d)
    {
        _servis = servis;
        BasTarih = DateTime.Today.AddMonths(-3);
        BitTarih = DateTime.Today.AddMonths(3);
    }

    public override string Baslik => "Taksit Takvimi";
    public override bool TarihFiltresi => true;
    public override string? OnayFiltreMetni => "Ödenmişleri de göster";
    public override bool DetayVar => true;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.Tarih("Vade", nameof(TakvimSatir.Vade)), new("Banka", nameof(TakvimSatir.Banka), Genislik: 200), new("Kredi No", nameof(TakvimSatir.KrediNo)),
        Kolon.Sayi("Taksit", nameof(TakvimSatir.No)), Kolon.Para("Tutar", nameof(TakvimSatir.Tutar), false), new("PB", nameof(TakvimSatir.PB)),
        Kolon.Para("TL Karşılığı", nameof(TakvimSatir.TutarTL)), new("Gecikti", "Gecikti?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Öde", SymbolRegular.Payment24, async () => { if (Secili is { } t && await KrediIslemleri.TaksitOdeAsync(t.KrediId, t.TaksitId)) await YenileAsync(); }, true),
    ];

    protected override async Task<IEnumerable<TakvimSatir>> YukleAsync() => await _servis.TakvimAsync(Bas, Bit, OnayFiltre);
    protected override Task DetayAsync(TakvimSatir satir) => KrediIslemleri.DetayAc(satir.KrediId);
    protected override string? OzetHesapla(IReadOnlyList<TakvimSatir> l) =>
        $"Toplam (TL): {l.Sum(t => t.TutarTL):N2}   •   Gecikmiş: {l.Where(t => t.Gecikti).Sum(t => t.TutarTL):N2} ({l.Count(t => t.Gecikti)} taksit)";
}

public sealed record KrediAracSatir(string Plaka, decimal Pay);

/// <summary>Kredi 360°: KPI, ödeme planı, ödemeler, grafik, bağlı araçlar.</summary>
public sealed class KrediDetayVM : DetayVM
{
    private readonly short _id;
    private readonly KrediServisi _servis = App.Al<KrediServisi>();
    private KrediDetay? _d;

    public KrediDetayVM(short id)
    {
        _id = id;
        Baslik = "Kredi";
        Sekmeler.Add(DetaySekme.Liste("Ödeme Planı", () => new SekmeListeVM<PlanSatir>("Ödeme Planı",
        [
            Kolon.Sayi("No", nameof(PlanSatir.No)), Kolon.Tarih("Vade", nameof(PlanSatir.Vade)), Kolon.Para("Anapara", nameof(PlanSatir.Anapara)),
            Kolon.Para("Faiz", nameof(PlanSatir.Faiz)), Kolon.Para("BSMV", nameof(PlanSatir.Bsmv)), Kolon.Para("KKDF", nameof(PlanSatir.Kkdf)),
            Kolon.Para("Taksit", nameof(PlanSatir.Toplam)), Kolon.Para("Ödenen", nameof(PlanSatir.Odenen)), Kolon.Para("Kalan", nameof(PlanSatir.Kalan)),
            Kolon.Para("Kalan Anapara", nameof(PlanSatir.KalanAnapara), false), new("Durum", nameof(PlanSatir.DurumAd)), Kolon.Sayi("Plan v.", nameof(PlanSatir.Versiyon)),
        ], _ => Task.FromResult<IEnumerable<PlanSatir>>(_d?.Plan ?? []))
        {
            EylemOlustur = l =>
            [
                Eylem.Yap("Bu Taksiti Öde", SymbolRegular.Payment24, async () =>
                {
                    if (l.Secili is { Durum: TaksitDurum.Bekliyor or TaksitDurum.Kismi } t && await KrediIslemleri.TaksitOdeAsync(_id, t.Id)) await YenileAsync();
                }, true),
            ],
        }));
        Sekmeler.Add(DetaySekme.Liste("Ödemeler", () => new SekmeListeVM<OdemeSatir>("Ödemeler",
        [
            Kolon.Tarih("Tarih", nameof(OdemeSatir.Tarih)), new("Tür", nameof(OdemeSatir.TurAd)), Kolon.Sayi("Taksit", nameof(OdemeSatir.TaksitNo)),
            new("Hesap", nameof(OdemeSatir.Kasa)), Kolon.Para("Tutar", nameof(OdemeSatir.Tutar)), Kolon.Para("Anapara", nameof(OdemeSatir.Anapara)),
            Kolon.Para("Faiz", nameof(OdemeSatir.Faiz)), Kolon.Para("Vergi", nameof(OdemeSatir.Vergi)), Kolon.Para("Gecikme", nameof(OdemeSatir.Gecikme)),
            Kolon.Para("Komisyon", nameof(OdemeSatir.Komisyon)), new("Kur", nameof(OdemeSatir.Kur), KolonBicim.Sayi, 70),
            Kolon.Para("Hesaptan Çıkan", nameof(OdemeSatir.KasaTutar), false), new("Açıklama", nameof(OdemeSatir.Aciklama), Genislik: 180), new("İptal", "Iptal?bool"),
        ], _ => Task.FromResult<IEnumerable<OdemeSatir>>(_d?.Odemeler.Reverse() ?? []))
        {
            EylemOlustur = l =>
            [
                Eylem.Yap("Ödemeyi İptal Et", SymbolRegular.ArrowUndo24, async () =>
                {
                    if (l.Secili is not { Iptal: false } o) return;
                    var neden = await Dialog.MetinIste("Yalnızca son ödeme iptal edilebilir (kasa ve banka kayıtları ters çevrilir). İptal nedeni:", "Ödeme iptali");
                    if (neden is null) return;
                    await _servis.OdemeIptalAsync(o.Id, neden);
                    AnaPencere.Basarili("Ödeme iptal edildi.");
                    await YenileAsync();
                }),
            ],
        }));
        Sekmeler.Add(new DetaySekme("Grafik", GrafikOlustur));
        Sekmeler.Add(DetaySekme.Liste("Bağlı Araçlar", () => new SekmeListeVM<KrediAracSatir>("Bağlı Araçlar",
            [new("Plaka", nameof(KrediAracSatir.Plaka)), new("Pay %", nameof(KrediAracSatir.Pay), KolonBicim.Sayi, 80)],
            _ => Task.FromResult<IEnumerable<KrediAracSatir>>(_d?.Araclar.Select(a => new KrediAracSatir(a.Plaka, a.Pay)).ToList() ?? []))));
    }

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        Eylem.Yap("Taksit Öde", SymbolRegular.Payment24, async () => { if (await KrediIslemleri.TaksitOdeAsync(_id)) await YenileAsync(); }, true),
        Eylem.Yap("Ara Ödeme", SymbolRegular.MoneyHand24, async () => { if (await KrediIslemleri.AraOdemeAsync(_id)) await YenileAsync(); }, true),
        Eylem.Yap("Erken Kapat", SymbolRegular.LockClosed24, async () => { if (await KrediIslemleri.ErkenKapatAsync(_id)) await YenileAsync(); }),
        Eylem.Yap("Serbest Ödeme", SymbolRegular.Money24, async () => { if (await KrediIslemleri.SerbestOdemeAsync(_id)) await YenileAsync(); }),
        Eylem.Yap("Planı Excel'den Al", SymbolRegular.DocumentTable24, async () => { if (await KrediIslemleri.PlanExceldenAsync(_id)) await YenileAsync(); }),
        Eylem.Yap("Plan Şablonu (Excel)", SymbolRegular.ArrowExportLtr24, () => KrediIslemleri.SablonAsync(_id)),
        Eylem.Yap("Faiz Oranı Değiştir", SymbolRegular.DataTrending24, async () => { if (await KrediIslemleri.FaizDegistirAsync(_id)) await YenileAsync(); }),
        Eylem.Yap("Düzenle", SymbolRegular.Edit24, async () => { if (await KrediIslemleri.FormAsync(_id) is not null) await YenileAsync(); }),
        Eylem.Yap("Ekler", SymbolRegular.Attach24, () => EklerPenceresi.AcAsync(EkHedef.Finansman, _id, Baslik)),
        Eylem.Yap("Sil", SymbolRegular.Delete24, async () => { if (await KrediIslemleri.SilAsync(_id, Baslik)) Gezgin.Geri(); }),
    ];

    public override IReadOnlyList<Eylem> Belgeler =>
    [
        Eylem.Yap("Ödeme Planı (PDF)", SymbolRegular.DocumentPdf24, () => KrediIslemleri.YazdirAsync(_id)),
    ];

    protected override async Task YukleAsync()
    {
        var d = _d = await _servis.DetayAsync(_id);
        var k = d.Kredi;
        var o = d.Ozet;
        var pb = k.ParaBirimi.Ad();
        Baslik = $"{d.Banka} — {k.KrediNo}";
        AltBaslik = $"{k.Tur.Ad()} · {pb} · Kullandırım {k.KullandirimTarihi:dd.MM.yyyy} · " +
                    (k.Yontem == PlanYontemi.Manuel ? "Serbest ödemeli" : $"{k.VadeSayisi} ay {k.Yontem.Ad().ToLower(Kultur.Tr)}") +
                    $" · Faiz %{k.FaizOrani:#,##0.##} {k.FaizTipi.Ad().Replace(" %", "").ToLower(Kultur.Tr)}" +
                    (k.BsmvOrani + k.KkdfOrani > 0 ? $" (+BSMV %{k.BsmvOrani:0.##}, KKDF %{k.KkdfOrani:0.##})" : "") +
                    (d.Araclar.Count > 0 ? " · " + string.Join(", ", d.Araclar.Select(a => a.Plaka)) : "");
        (Rozet, RozetRenk) = k.Durum == KrediDurum.Kapandi ? ("Kapandı", null) : o.Geciken > 0 ? ($"{o.Geciken} taksit gecikmede", "kirmizi") : ("Aktif", "yesil");
        Kpiler.Clear();
        Kpiler.Add(new Kpi("Kredi tutarı", Para(o.Anapara, pb), $"Dosya masrafı {k.DosyaMasrafi:N2}"));
        Kpiler.Add(new Kpi("Ödenen anapara", Para(o.OdenenAnapara, pb), $"%{o.Ilerleme:0.#} tamamlandı", Ilerleme: (double)o.Ilerleme));
        Kpiler.Add(new Kpi("Kalan anapara", Para(o.KalanAnapara, pb), Renk: o.KalanAnapara > 0 ? "turuncu" : "yesil"));
        Kpiler.Add(new Kpi("Ödenen faiz + vergi", Para(o.OdenenFaiz, pb)));
        Kpiler.Add(new Kpi("Kalan faiz + vergi", Para(o.KalanFaiz, pb), "Plana göre"));
        Kpiler.Add(new Kpi("Sonraki taksit", o.SonrakiTutar is { } st ? Para(st, pb) : "—",
            o.SonrakiVade is { } sv ? $"{sv:dd.MM.yyyy} ({sv.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber} gün)" : null,
            o.SonrakiVade is { } sv2 && sv2.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber <= 7 ? "turuncu" : null, Tikla: o.SonrakiTutar is null ? null : TaksitOdeTikla));
        Kpiler.Add(new Kpi("Taksit", $"{o.OdenenTaksit} / {o.ToplamTaksit}", "ödendi"));
        if (o.Geciken > 0) Kpiler.Add(new Kpi("Gecikmiş", Para(o.GecikenTutar, pb), $"{o.Geciken} taksit", "kirmizi", Tikla: TaksitOdeTikla));
    }

    /// <summary>KPI kartından taksit ödeme formu.</summary>
    private async void TaksitOdeTikla()
    {
        try
        {
            if (await KrediIslemleri.TaksitOdeAsync(_id)) await YenileAsync();
        }
        catch (Exception ex)
        {
            await Dialog.HataGoster(ex);
        }
    }

    private object GrafikOlustur()
    {
        if (_d is null || _d.Plan.Count == 0) return Grafik.Bos("Ödeme planı yok.");
        var plan = _d.Plan.Where(p => p.Durum != TaksitDurum.PlanDegisti).OrderBy(p => p.Vade).ToList();
        var etiketler = plan.Select(p => p.Vade.ToString("MM.yy")).ToList();
        return Grafik.Izgara(1,
            Grafik.Kart("Kalan anapara", Grafik.Cizgi(etiketler, ("Kalan anapara", plan.Select(p => p.KalanAnapara), Grafik.Ana)), 260),
            Grafik.Kart("Taksit dağılımı (anapara / faiz + vergi)", Grafik.Yigilmis(etiketler,
                ("Anapara", plan.Select(p => p.Anapara), Grafik.Ana), ("Faiz + vergi", plan.Select(p => p.Faiz + p.Bsmv + p.Kkdf), Grafik.Ikincil)), 260));
    }
}

/// <summary>Finansman paneli: tüm kredilerin özeti, vade yükü, banka/döviz riski.</summary>
public sealed class KrediOzetVM(KrediServisi servis, IKullaniciOturumu oturum) : DetayVM
{
    private KrediGenelOzet? _o;
    private bool _sekmeler;

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        Eylem.Yap("Yeni Kredi", SymbolRegular.Add24, async () =>
        {
            if (!oturum.Yetkili(Izinler.FinansmanYaz)) return;
            if (await KrediIslemleri.FormAsync(null) is { } id) await KrediIslemleri.DetayAc(id);
        }, true),
        Eylem.Yap("Banka Limitleri", SymbolRegular.BuildingBank24, async () => { BankaLimitListeVM.Ac(); await YenileAsync(); }),
    ];

    protected override async Task YukleAsync()
    {
        var o = _o = await servis.GenelOzetAsync();
        Baslik = "Finansman Özeti";
        AltBaslik = "Tüm aktif kredi / leasing / rotatif borçlar — döviz tutarlar bugünkü TCMB kuruyla TL'ye çevrilmiştir";
        Rozet = o.GecikenTaksit > 0 ? $"{o.GecikenTaksit} gecikmiş taksit" : null;
        RozetRenk = "kirmizi";
        Kpiler.Clear();
        Kpiler.Add(new Kpi("Toplam kalan anapara", Para0(o.KalanAnaparaTL), $"{o.Bankalar.Sum(b => b.KrediSayisi)} aktif kredi", Tikla: () => Gezgin.Git(typeof(Sayfalar.KredilerSayfasi))));
        Kpiler.Add(new Kpi("Bu ay ödenecek", Para0(o.BuAyOdenecekTL), Renk: "turuncu", Tikla: () => Gezgin.Git(typeof(Sayfalar.TaksitlerSayfasi))));
        Kpiler.Add(new Kpi("0–30 gün", Para0(o.Gun30TL), Tikla: () => Gezgin.Git(typeof(Sayfalar.TaksitlerSayfasi))));
        Kpiler.Add(new Kpi("31–60 gün", Para0(o.Gun60TL), Tikla: () => Gezgin.Git(typeof(Sayfalar.TaksitlerSayfasi))));
        Kpiler.Add(new Kpi("61–90 gün", Para0(o.Gun90TL), Tikla: () => Gezgin.Git(typeof(Sayfalar.TaksitlerSayfasi))));
        Kpiler.Add(new Kpi("Gecikmiş", Para0(o.GecikenTutarTL), $"{o.GecikenTaksit} taksit", o.GecikenTaksit > 0 ? "kirmizi" : "yesil", Tikla: () => Gezgin.Git(typeof(Sayfalar.TaksitlerSayfasi))));
        if (_sekmeler) return;
        _sekmeler = true;
        Sekmeler.Add(DetaySekme.Liste("Krediler", () => new SekmeListeVM<KrediSatir>("Krediler",
        [
            new("Banka", nameof(KrediSatir.Banka), Genislik: 170), new("Kredi No", nameof(KrediSatir.KrediNo)), new("Tür", nameof(KrediSatir.TurAd)), new("PB", nameof(KrediSatir.PB)),
            Kolon.Para("Kalan Anapara", nameof(KrediSatir.KalanAnapara), false), Kolon.Para("Kalan Faiz", nameof(KrediSatir.KalanFaiz), false),
            new("Taksit", nameof(KrediSatir.TaksitMetni)), Kolon.Tarih("Sonraki Vade", nameof(KrediSatir.SonrakiVade)),
            Kolon.Para("Sonraki Tutar", nameof(KrediSatir.SonrakiTutar), false), new("Durum", nameof(KrediSatir.DurumAd)),
        ], async _ => await servis.ListeAsync()) { DetayIslem = k => KrediIslemleri.DetayAc(k.Id) }));
        Sekmeler.Add(new DetaySekme("Aylık Taksit Yükü", () => _o is null ? Grafik.Bos() : Grafik.Kart("Önümüzdeki 12 ay (TL)", Grafik.Yigilmis(
            _o.AylikYuk.Select(a => a.Ay.ToString("MMM yy", Kultur.Tr)).ToList(),
            ("Anapara", _o.AylikYuk.Select(a => a.AnaparaTL), Grafik.Ana), ("Faiz + vergi", _o.AylikYuk.Select(a => a.FaizTL), Grafik.Ikincil)), 380)));
        Sekmeler.Add(new DetaySekme("Banka Riski", () => _o is null ? Grafik.Bos() : Grafik.Izgara(2,
            Grafik.Kart("Kalan anapara (banka bazında)", Grafik.Pasta(_o.Bankalar.Select(b => (b.Banka, b.KalanAnaparaTL))), 320),
            Grafik.Kart("Döviz bazında risk", Grafik.Pasta(_o.Dovizler.Select(x => ($"{x.PB} ({x.Kalan:N0})", x.KalanTL))), 320))));
        Sekmeler.Add(DetaySekme.Liste("Banka Limitleri", () => new SekmeListeVM<BankaRiskSatir>("Banka Riski",
        [
            new("Banka", nameof(BankaRiskSatir.Banka), Genislik: 220), Kolon.Sayi("Kredi", nameof(BankaRiskSatir.KrediSayisi)),
            Kolon.Para("Kalan Anapara (TL)", nameof(BankaRiskSatir.KalanAnaparaTL)), Kolon.Para("Bu Ay Taksit (TL)", nameof(BankaRiskSatir.BuAyTaksitTL)),
            Kolon.Para("Limit (TL)", nameof(BankaRiskSatir.LimitTL)), Kolon.Para("Boş Limit (TL)", nameof(BankaRiskSatir.BosLimitTL)),
        ], _ => Task.FromResult<IEnumerable<BankaRiskSatir>>(_o?.Bankalar ?? []))));
        Sekmeler.Add(DetaySekme.Liste("Yaklaşan Taksitler", () => new SekmeListeVM<TakvimSatir>("Yaklaşan Taksitler (90 gün)",
        [
            Kolon.Tarih("Vade", nameof(TakvimSatir.Vade)), new("Banka", nameof(TakvimSatir.Banka), Genislik: 200), new("Kredi No", nameof(TakvimSatir.KrediNo)),
            Kolon.Sayi("Taksit", nameof(TakvimSatir.No)), Kolon.Para("Tutar", nameof(TakvimSatir.Tutar), false), new("PB", nameof(TakvimSatir.PB)),
            Kolon.Para("TL Karşılığı", nameof(TakvimSatir.TutarTL)), new("Gecikti", "Gecikti?bool"),
        ], async _ => await servis.TakvimAsync(DateOnly.FromDateTime(DateTime.Today.AddYears(-5)), DateOnly.FromDateTime(DateTime.Today.AddDays(90))))
        {
            DetayIslem = t => KrediIslemleri.DetayAc(t.KrediId),
            EylemOlustur = l => [Eylem.Yap("Öde", SymbolRegular.Payment24, async () =>
            {
                if (l.Secili is { } t && await KrediIslemleri.TaksitOdeAsync(t.KrediId, t.TaksitId)) await YenileAsync();
            }, true)],
        }));
    }
}

public sealed record BankaLimitSatir(short Id, string Banka, decimal Limit, ParaBirimi ParaBirimi, string? Teminat, DateOnly? Bitis)
{
    public string PB => ParaBirimi.Ad();
}

public sealed class BankaLimitListeVM(IDisaAktarici d, IKayitServisi<BankaLimit> servis, Secimler s) : KayitListeVM<BankaLimitSatir, BankaLimit>(d, servis, s)
{
    public static void Ac() =>
        new ListePenceresi(new BankaLimitListeVM(App.Al<IDisaAktarici>(), App.Al<IKayitServisi<BankaLimit>>(), App.Al<Secimler>()), 900, 520).ShowDialog();

    public override string Baslik => "Banka Kredi Limitleri";
    protected override string KayitAdi => "Banka Limiti";
    protected override object Id(BankaLimitSatir s) => s.Id;

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Banka", nameof(BankaLimitSatir.Banka), Genislik: 240), Kolon.Para("Limit", nameof(BankaLimitSatir.Limit), false), new("PB", nameof(BankaLimitSatir.PB)),
        new("Teminat", nameof(BankaLimitSatir.Teminat), Genislik: 220), Kolon.Tarih("Geçerlilik", nameof(BankaLimitSatir.Bitis)),
    ];

    protected override async Task<IEnumerable<BankaLimitSatir>> YukleAsync() =>
        (await Servis.ListeAsync(q => q.Include(b => b.Banka))).Select(b => new BankaLimitSatir(b.Id, b.Banka?.Unvan ?? "", b.LimitTutar, b.ParaBirimi, b.Teminat, b.GecerlilikBitis));

    protected override async Task<IReadOnlyList<FormAlani>> AlanlarAsync(BankaLimit m, bool yeni) =>
    [
        new() { Etiket = "Banka", Yol = nameof(BankaLimit.BankaCariId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await Secimler.CarilerAsync(CariTipi.Finans) },
        new() { Etiket = "Limit tutarı", Yol = nameof(BankaLimit.LimitTutar), Tip = AlanTipi.Para, Zorunlu = true },
        FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(BankaLimit.ParaBirimi)),
        new() { Etiket = "Teminat", Yol = nameof(BankaLimit.Teminat), MaksUzunluk = 200, Ipucu = "Ör. ipotek, araç rehni, kefalet" },
        new() { Etiket = "Geçerlilik bitişi", Yol = nameof(BankaLimit.GecerlilikBitis), Tip = AlanTipi.Tarih },
    ];
}
