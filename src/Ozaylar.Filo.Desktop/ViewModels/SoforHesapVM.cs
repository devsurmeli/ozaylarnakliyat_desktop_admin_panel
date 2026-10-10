using System.Collections.ObjectModel;
using System.ComponentModel;
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
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.ViewModels;

public static class SoforHesapFormlari
{
    /// <summary>Şoföre para ver: sefer avansı, harçlık, yol parası veya maaş/hakediş ödemesi; kasadan nakit veya bankadan EFT.</summary>
    public static Task<bool> AvansAsync(SoforHesapServisi servis, Secimler s, short? soforId = null, int? seferId = null, AvansTuru tur = AvansTuru.Avans) =>
        TekrarliAsync(() => TekAvansAsync(servis, s, soforId, seferId, tur));

    /// <summary>"Kaydet ve yeni" seçildikçe formu yeniden açar; en az bir kayıt olduysa true.</summary>
    private static async Task<bool> TekrarliAsync(Func<Task<bool>> form)
    {
        var kaydedildi = false;
        do
        {
            if (!await form()) break;
            kaydedildi = true;
        } while (FormPenceresi.SonYeniIstendi);
        return kaydedildi;
    }

    private static async Task<bool> TekAvansAsync(SoforHesapServisi servis, Secimler s, short? soforId, int? seferId, AvansTuru tur)
    {
        var a = new Avans { SoforId = soforId ?? 0, SeferId = seferId, Tarih = DateOnly.FromDateTime(DateTime.Today), Tur = tur };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Şoför", Yol = nameof(Avans.SoforId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.SoforlerAsync() },
            FormAlani.EnumSecim<AvansTuru>("Ne için", nameof(Avans.Tur)),
            new() { Etiket = "Sefer", Yol = nameof(Avans.SeferId), Tip = AlanTipi.Secim, Secenekler = await s.SeferlerAsync(), Gorunur = _ => a.Tur != AvansTuru.MaasOdemesi },
            new() { Etiket = "Araç (boş = otomatik)", Yol = nameof(Avans.AracId), Tip = AlanTipi.Secim, Secenekler = await s.AraclarAsync(),
                    Ipucu = "Boşsa seferin aracı veya şoförün o tarihteki aracı yazılır" },
            new() { Etiket = "Tarih", Yol = nameof(Avans.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Kasa (nakit) / banka (EFT)", Yol = nameof(Avans.KasaHesapId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.KasalarAsync() },
            new() { Etiket = "Tutar", Yol = nameof(Avans.Tutar), Tip = AlanTipi.Para, Zorunlu = true, Ipucu = "Kasanın para biriminde (EUR kasadan verilen avans EUR'dur)" },
            new() { Etiket = "Kur (dövizli kasa)", Yol = nameof(Avans.Kur), Tip = AlanTipi.Ondalik },
            new() { Etiket = "Açıklama", Yol = nameof(Avans.Aciklama), MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac("Şoföre Para Ver", a, alanlar, _ => servis.AvansVerAsync(a), yeni: true);
        if (ok) AnaPencere.Basarili($"{a.Tutar:N2} verildi ({a.Tur.Ad()}).");
        return ok;
    }

    private sealed class ParaAlModel
    {
        public short? SoforId { get; set; }
        public SoforHesapServisi.ParaAlTuru Tur { get; set; } = SoforHesapServisi.ParaAlTuru.Iade;
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public decimal Tutar { get; set; }
        public ParaBirimi PB { get; set; } = ParaBirimi.TRY;
        public decimal Kur { get; set; } = 1;
        public byte? KasaId { get; set; }
        public int? MusteriId { get; set; }
        public int? SeferId { get; set; }
        public short? AracId { get; set; }
        public string? Aciklama { get; set; }
    }

    /// <summary>Şoförden para al: elindeki avansın iadesi veya müşteriden tahsil ettiği navlun.</summary>
    public static Task<bool> ParaAlAsync(SoforHesapServisi servis, Secimler s, short? soforId = null, int? seferId = null) =>
        TekrarliAsync(() => TekParaAlAsync(servis, s, soforId, seferId));

    private static async Task<bool> TekParaAlAsync(SoforHesapServisi servis, Secimler s, short? soforId, int? seferId)
    {
        var m = new ParaAlModel { SoforId = soforId, SeferId = seferId };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Şoför", Yol = nameof(ParaAlModel.SoforId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.SoforlerAsync() },
            new() { Etiket = "İşlem", Yol = nameof(ParaAlModel.Tur), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = new[]
            {
                new SecimOgesi<SoforHesapServisi.ParaAlTuru>(SoforHesapServisi.ParaAlTuru.Iade, "Şoför para iade etti"),
                new SecimOgesi<SoforHesapServisi.ParaAlTuru>(SoforHesapServisi.ParaAlTuru.MusteriTahsilati, "Şoför müşteriden tahsilat yaptı"),
            } },
            new() { Etiket = "Müşteri", Yol = nameof(ParaAlModel.MusteriId), Tip = AlanTipi.Secim, Secenekler = await s.CarilerAsync(CariTipi.Musteri),
                    Gorunur = _ => m.Tur == SoforHesapServisi.ParaAlTuru.MusteriTahsilati },
            new() { Etiket = "Tarih", Yol = nameof(ParaAlModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Tutar", Yol = nameof(ParaAlModel.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
            FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(ParaAlModel.PB)),
            new() { Etiket = "Kur", Yol = nameof(ParaAlModel.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => m.PB != ParaBirimi.TRY },
            new() { Etiket = "Teslim alınan kasa / banka", Yol = nameof(ParaAlModel.KasaId), Tip = AlanTipi.Secim, Secenekler = await s.KasalarAsync(),
                    Ipucu = "Müşteri tahsilatında boş bırakılırsa para şoförde kalır (şoför borçlanır)" },
            new() { Etiket = "Sefer", Yol = nameof(ParaAlModel.SeferId), Tip = AlanTipi.Secim, Secenekler = await s.SeferlerAsync() },
            new() { Etiket = "Araç (boş = otomatik)", Yol = nameof(ParaAlModel.AracId), Tip = AlanTipi.Secim, Secenekler = await s.AraclarAsync() },
            new() { Etiket = "Açıklama", Yol = nameof(ParaAlModel.Aciklama), MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac("Şoförden Para Al", m, alanlar, _ => servis.ParaAlAsync(m.SoforId!.Value, m.Tur, m.Tarih, m.Tutar, m.KasaId,
            m.MusteriId, m.SeferId, m.AracId, m.Aciklama, m.PB, m.Kur), yeni: true);
        if (ok) AnaPencere.Basarili("Kaydedildi.");
        return ok;
    }

    private sealed class AtamaModel
    {
        public short? AracId { get; set; }
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }

    public static async Task<bool> AracAtaAsync(SoforHesapServisi servis, Secimler s, short soforId, string ad)
    {
        var m = new AtamaModel();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Araç", Yol = nameof(AtamaModel.AracId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.AraclarAsync() },
            new() { Etiket = "Başlangıç tarihi", Yol = nameof(AtamaModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
        };
        var ok = FormPenceresi.Ac($"Araca Ata — {ad}", m, alanlar, _ => servis.AracAtaAsync(soforId, m.AracId!.Value, m.Tarih));
        if (ok) AnaPencere.Basarili("Atama yapıldı; önceki atama kapatıldı.");
        return ok;
    }

    public static async Task<bool> ZimmetAsync(Secimler s, Zimmet? z = null, short? soforId = null, short? aracId = null)
    {
        var yeni = z is null;
        z ??= new Zimmet { SoforId = soforId, AracId = aracId, VerilisTarihi = DateOnly.FromDateTime(DateTime.Today) };
        var alanlar = new List<FormAlani>
        {
            FormAlani.EnumSecim<ZimmetTuru>("Tür", nameof(Zimmet.Tur)),
            new() { Etiket = "Ad / tanım", Yol = nameof(Zimmet.Ad), Zorunlu = true, MaksUzunluk = 100, Ipucu = "Ör. Shell taşıt kartı, HGS etiketi, telefon" },
            new() { Etiket = "Seri / kart no", Yol = nameof(Zimmet.SeriNo), MaksUzunluk = 40 },
            new() { Etiket = "Şoför", Yol = nameof(Zimmet.SoforId), Tip = AlanTipi.Secim, Secenekler = await s.SoforlerAsync() },
            new() { Etiket = "Araç", Yol = nameof(Zimmet.AracId), Tip = AlanTipi.Secim, Secenekler = await s.AraclarAsync() },
            new() { Etiket = "Veriliş tarihi", Yol = nameof(Zimmet.VerilisTarihi), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Değer (TL)", Yol = nameof(Zimmet.Deger), Tip = AlanTipi.Para },
            new() { Etiket = "Açıklama", Yol = nameof(Zimmet.Aciklama), MaksUzunluk = 200 },
        };
        var servis = App.Al<IKayitServisi<Zimmet>>();
        var ok = FormPenceresi.Ac(yeni ? "Zimmet Ver" : "Zimmet Düzenle", z, alanlar, async _ =>
        {
            if (z.SoforId is null && z.AracId is null) throw new DomainException("Şoför veya araçtan en az biri seçilmelidir.");
            await servis.KaydetAsync(z);
        });
        if (ok) AnaPencere.Basarili("Zimmet kaydedildi.");
        return ok;
    }

    private sealed class ZimmetKapatModel
    {
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public ZimmetDurum Durum { get; set; } = ZimmetDurum.IadeEdildi;
        public string? Aciklama { get; set; }
    }

    public static async Task<bool> ZimmetKapatAsync(SoforHesapServisi servis, ZimmetSatir z)
    {
        if (z.Durum != ZimmetDurum.Zimmette) { await Dialog.Bilgi("Bu zimmet zaten kapatılmış."); return false; }
        var m = new ZimmetKapatModel();
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(ZimmetKapatModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Sonuç", Yol = nameof(ZimmetKapatModel.Durum), Tip = AlanTipi.Secim, Zorunlu = true,
                    Secenekler = EnumMetin.Liste<ZimmetDurum>().Where(x => x.Deger != ZimmetDurum.Zimmette).ToList() },
            new() { Etiket = "Açıklama", Yol = nameof(ZimmetKapatModel.Aciklama), MaksUzunluk = 100 },
        };
        return FormPenceresi.Ac($"Zimmet İade — {z.Ad}", m, alanlar, _ => servis.ZimmetKapatAsync(z.Id, m.Tarih, m.Durum, m.Aciklama));
    }

    private sealed class TahakkukModel
    {
        public short? SoforId { get; set; }
        public DateOnly Donem { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public TahakkukTuru Tur { get; set; } = TahakkukTuru.Ek;
        public decimal Tutar { get; set; }
        public int? SeferId { get; set; }
        public string? Aciklama { get; set; }
    }

    /// <summary>Elle hakediş satırı: ek ödeme (prim, ikramiye) veya kesinti (ceza, hasar, avans kesintisi).</summary>
    public static Task<bool> TahakkukAsync(SoforHesapServisi servis, Secimler s, short? soforId = null, TahakkukTuru tur = TahakkukTuru.Ek) =>
        TekrarliAsync(() => TekTahakkukAsync(servis, s, soforId, tur));

    private static async Task<bool> TekTahakkukAsync(SoforHesapServisi servis, Secimler s, short? soforId, TahakkukTuru tur)
    {
        var m = new TahakkukModel { SoforId = soforId, Tur = tur };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Şoför", Yol = nameof(TahakkukModel.SoforId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.SoforlerAsync() },
            new() { Etiket = "Dönem (ayın herhangi bir günü)", Yol = nameof(TahakkukModel.Donem), Tip = AlanTipi.Tarih, Zorunlu = true },
            FormAlani.EnumSecim<TahakkukTuru>("Tür", nameof(TahakkukModel.Tur)),
            new() { Etiket = "Tutar (TL)", Yol = nameof(TahakkukModel.Tutar), Tip = AlanTipi.Para, Zorunlu = true },
            new() { Etiket = "Sefer", Yol = nameof(TahakkukModel.SeferId), Tip = AlanTipi.Secim, Secenekler = await s.SeferlerAsync() },
            new() { Etiket = "Açıklama", Yol = nameof(TahakkukModel.Aciklama), Zorunlu = true, MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac("Hakediş — Ek / Kesinti", m, alanlar, _ => servis.TahakkukYazAsync(m.Donem.Year * 100 + m.Donem.Month,
            [new PersonelTahakkuk { SoforId = m.SoforId!.Value, Tur = m.Tur, Tutar = m.Tutar, SeferId = m.SeferId, Aciklama = m.Aciklama, Tarih = m.Donem }]), yeni: true);
        if (ok) AnaPencere.Basarili("Hakediş satırı yazıldı.");
        return ok;
    }

    public static async Task<bool> HarcirahAsync(SoforHesapServisi servis, Secimler s, Harcirah? oneri = null)
    {
        var h = oneri ?? new Harcirah { Baslangic = DateOnly.FromDateTime(DateTime.Today), Bitis = DateOnly.FromDateTime(DateTime.Today) };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Şoför", Yol = nameof(Harcirah.SoforId), Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await s.SoforlerAsync() },
            new() { Etiket = "Sefer", Yol = nameof(Harcirah.SeferId), Tip = AlanTipi.Secim, Secenekler = await s.SeferlerAsync() },
            new()
            {
                Etiket = "Ülke (boş = yurtiçi)", Yol = nameof(Harcirah.UlkeId), Tip = AlanTipi.Secim, Secenekler = await s.UlkelerAsync(),
                Degisince = async _ =>
                {
                    var t = await servis.TarifeBulAsync(h.UlkeId, h.Baslangic);
                    if (t is not null) { h.GunlukTutar = t.GunlukTutar; h.ParaBirimi = t.ParaBirimi; }
                },
            },
            new() { Etiket = "Başlangıç", Yol = nameof(Harcirah.Baslangic), Tip = AlanTipi.Tarih, Zorunlu = true, Degisince = GunHesapla(h) },
            new() { Etiket = "Bitiş", Yol = nameof(Harcirah.Bitis), Tip = AlanTipi.Tarih, Zorunlu = true, Degisince = GunHesapla(h) },
            new() { Etiket = "Gün sayısı", Yol = nameof(Harcirah.GunSayisi), Tip = AlanTipi.Ondalik, Ipucu = "Yarım gün için 0,5 girilebilir" },
            new() { Etiket = "Günlük tutar (0 = tarifeden)", Yol = nameof(Harcirah.GunlukTutar), Tip = AlanTipi.Para },
            FormAlani.EnumSecim<ParaBirimi>("Para birimi", nameof(Harcirah.ParaBirimi)),
            new() { Etiket = "Kur", Yol = nameof(Harcirah.Kur), Tip = AlanTipi.Ondalik, Gorunur = _ => h.ParaBirimi != ParaBirimi.TRY },
            new() { Etiket = "Açıklama", Yol = nameof(Harcirah.Aciklama), MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac(h.Id == 0 ? "Harcırah" : "Harcırah Düzenle", h, alanlar, async _ =>
        {
            if (h.ParaBirimi != ParaBirimi.TRY && h.Kur <= 1) h.Kur = await App.Al<DovizServisi>().KurAsync(h.ParaBirimi, h.Bitis) ?? h.Kur;
            await servis.HarcirahKaydetAsync(h);
        });
        if (ok) AnaPencere.Basarili($"Harcırah yazıldı: {h.Tutar:N2} {h.ParaBirimi.Ad()}");
        return ok;
    }

    private static Func<FormPenceresi, Task> GunHesapla(Harcirah h) => _ =>
    {
        if (h.Bitis >= h.Baslangic) h.GunSayisi = h.Bitis.DayNumber - h.Baslangic.DayNumber + 1;
        return Task.CompletedTask;
    };
}

public sealed partial class AcikKalemSecim(AcikKalem kalem) : ObservableObject
{
    public AcikKalem Kalem { get; } = kalem;
    [ObservableProperty] private bool _secili = kalem.Onayli;
}

/// <summary>Şoför hesap ekranı: bakiyeler, açık kalemler, onay ve mahsup.</summary>
public sealed partial class SoforHesapVM(SoforHesapServisi servis, Secimler secimler, IKullaniciOturumu oturum, GiderServisi gider, IFiloDbContextFactory f) : ObservableObject
{
    public ObservableCollection<SoforBakiyeSatir> Soforler { get; } = [];
    public ObservableCollection<AcikKalemSecim> Kalemler { get; } = [];

    [ObservableProperty] private SoforBakiyeSatir? _seciliSofor;
    [ObservableProperty] private bool _mesgul;
    [ObservableProperty] private string _onizleme = "";
    [ObservableProperty] private decimal _fark;

    public bool YazmaYetkisi => oturum.Yetkili(Izinler.SoforHesapYaz);
    public bool OnayYetkisi => oturum.Yetkili(Izinler.SoforHesapOnay);

    partial void OnSeciliSoforChanged(SoforBakiyeSatir? value) => _ = KalemleriYukleAsync();

    [RelayCommand]
    public async Task YenileAsync()
    {
        Mesgul = true;
        try
        {
            var secili = SeciliSofor?.SoforId;
            Soforler.Clear();
            foreach (var s in await servis.BakiyelerAsync()) Soforler.Add(s);
            SeciliSofor = Soforler.FirstOrDefault(s => s.SoforId == secili) ?? Soforler.FirstOrDefault();
        }
        catch (Exception ex) { await Dialog.HataGoster(ex); }
        finally { Mesgul = false; }
    }

    private async Task KalemleriYukleAsync()
    {
        foreach (var k in Kalemler) k.PropertyChanged -= KalemDegisti;
        Kalemler.Clear();
        if (SeciliSofor is null) { OnizlemeGuncelle(); return; }
        try
        {
            foreach (var k in await servis.AcikKalemlerAsync(SeciliSofor.SoforId))
            {
                var s = new AcikKalemSecim(k);
                s.PropertyChanged += KalemDegisti;
                Kalemler.Add(s);
            }
        }
        catch (Exception ex) { await Dialog.HataGoster(ex); }
        OnizlemeGuncelle();
    }

    private void KalemDegisti(object? sender, PropertyChangedEventArgs e) => OnizlemeGuncelle();

    private void OnizlemeGuncelle()
    {
        var o = SoforHesapServisi.Onizle(Kalemler.Where(k => k.Secili).Select(k => k.Kalem));
        Fark = o.Fark;
        Onizleme = $"Avans: {o.AvansToplam:N2}   −   Harcama: {o.HarcamaToplam:N2}   −   Harcırah: {o.HarcirahToplam:N2}   =   " +
                   (o.Fark > 0 ? $"Şoför {o.Fark:N2} TL iade etmeli" : o.Fark < 0 ? $"Şoföre {-o.Fark:N2} TL ödenmeli" : "Fark yok");
    }

    [RelayCommand]
    private async Task AvansAsync()
    {
        if (await SoforHesapFormlari.AvansAsync(servis, secimler, SeciliSofor?.SoforId)) await YenileVeKalemlerAsync();
    }

    [RelayCommand]
    private async Task HarcamaAsync()
    {
        var m = new Masraf { Tarih = DateOnly.FromDateTime(DateTime.Today), Merkez = MasrafMerkezi.Sefer, OdemeSekli = OdemeSekli.SoforNakit, SoforId = SeciliSofor?.SoforId };
        if (await MasrafFormu.AcAsync(m, gider, secimler, f)) await YenileVeKalemlerAsync();
    }

    [RelayCommand]
    private async Task HarcirahAsync()
    {
        var h = new Harcirah { SoforId = SeciliSofor?.SoforId ?? 0, Baslangic = DateOnly.FromDateTime(DateTime.Today), Bitis = DateOnly.FromDateTime(DateTime.Today) };
        if (await SoforHesapFormlari.HarcirahAsync(servis, secimler, h)) await YenileVeKalemlerAsync();
    }

    [RelayCommand]
    private async Task OnaylaAsync()
    {
        var ids = Kalemler.Where(k => k.Secili && k.Kalem.Tur == AcikKalemTuru.Masraf && !k.Kalem.Onayli).Select(k => k.Kalem.Id).ToList();
        if (ids.Count == 0) { await Dialog.Bilgi("Onay bekleyen seçili harcama yok. Onaylanacak harcamaları işaretleyin."); return; }
        if (await Dialog.Calistir(() => servis.HarcamaOnaylaAsync(ids, true)))
        {
            AnaPencere.Basarili($"{ids.Count} harcama onaylandı.");
            await YenileVeKalemlerAsync();
        }
    }

    private sealed class MahsupModel
    {
        public DateOnly Tarih { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public MahsupSonuc Sonuc { get; set; }
        public byte? KasaId { get; set; }
        public string? Aciklama { get; set; }
    }

    [RelayCommand]
    private async Task MahsupAsync()
    {
        if (SeciliSofor is not { } sofor) return;
        var secili = Kalemler.Where(k => k.Secili).Select(k => k.Kalem).ToList();
        if (secili.Count == 0) { await Dialog.Bilgi("Mahsuba alınacak kalemleri işaretleyin."); return; }
        var fark = SoforHesapServisi.Onizle(secili).Fark;
        var m = new MahsupModel { Sonuc = fark > 0 ? MahsupSonuc.IadeAlindi : fark < 0 ? MahsupSonuc.SoforeOdendi : MahsupSonuc.FarkYok };
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Tarih", Yol = nameof(MahsupModel.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new()
            {
                Etiket = fark == 0 ? "Sonuç" : fark > 0 ? $"Şoför {fark:N2} TL borçlu" : $"Şoföre {-fark:N2} TL borçluyuz", Yol = nameof(MahsupModel.Sonuc), Tip = AlanTipi.Secim,
                Secenekler = EnumMetin.Liste<MahsupSonuc>().Where(x => fark == 0 ? x.Deger == MahsupSonuc.FarkYok
                    : x.Deger == MahsupSonuc.Devredildi || x.Deger == (fark > 0 ? MahsupSonuc.IadeAlindi : MahsupSonuc.SoforeOdendi)).ToList(),
            },
            new() { Etiket = "Kasa (TL)", Yol = nameof(MahsupModel.KasaId), Tip = AlanTipi.Secim, Secenekler = await secimler.KasalarAsync(ParaBirimi.TRY), Gorunur = _ => m.Sonuc is MahsupSonuc.IadeAlindi or MahsupSonuc.SoforeOdendi },
            new() { Etiket = "Açıklama", Yol = nameof(MahsupModel.Aciklama), MaksUzunluk = 200 },
        };
        var ok = FormPenceresi.Ac($"Mahsup — {sofor.AdSoyad}", m, alanlar, _ =>
            servis.MahsupYapAsync(sofor.SoforId, null, m.Tarih, secili.Select(k => (k.Tur, k.Id)).ToList(), m.Sonuc, m.KasaId, m.Aciklama));
        if (!ok) return;
        AnaPencere.Basarili("Mahsup yapıldı.");
        await YenileVeKalemlerAsync();
    }

    [RelayCommand]
    private Task EkstreAsync() => SeciliSofor is { } s ? EkstrePenceresi.CariAsync(s.CariId, s.AdSoyad) : Task.CompletedTask;

    [RelayCommand]
    private void TumunuSec(bool? sec)
    {
        foreach (var k in Kalemler) k.Secili = sec == true && k.Kalem.Onayli;
    }

    private async Task YenileVeKalemlerAsync()
    {
        await YenileAsync();
        await KalemleriYukleAsync();
    }
}

public sealed class AvansListeVM(IDisaAktarici d, SoforHesapServisi servis, Secimler s, IKullaniciOturumu oturum) : ListeVM<AvansSatir>(d)
{
    public override string Baslik => "Avanslar";
    public override bool TarihFiltresi => true;
    public override bool YeniVar => oturum.Yetkili(Izinler.SoforHesapYaz);
    public override bool SilVar => oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "İptal Et";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.Tarih("Tarih", nameof(AvansSatir.Tarih)), new("Şoför", nameof(AvansSatir.Sofor)), new("Sefer", nameof(AvansSatir.Sefer)),
        Kolon.Para("Tutar", nameof(AvansSatir.Tutar)), new("PB", nameof(AvansSatir.PB)), new("Kasa", nameof(AvansSatir.Kasa)),
        new("Açıklama", nameof(AvansSatir.Aciklama), Genislik: 220), new("Mahsuplu", "Mahsuplu?bool"), new("İptal", "Iptal?bool"),
    ];

    protected override async Task<IEnumerable<AvansSatir>> YukleAsync() => await servis.AvanslarAsync(Bas, Bit);

    protected override async Task YeniAsync()
    {
        if (await SoforHesapFormlari.AvansAsync(servis, s)) await YenileAsync();
    }

    protected override async Task SilAsync(AvansSatir a)
    {
        var neden = await Dialog.MetinIste("İptal nedeni:", "Avans iptali");
        if (neden is null) return;
        await servis.AvansIptalAsync(a.Id, neden);
        await YenileAsync();
    }
}

public sealed class HarcirahListeVM(IDisaAktarici d, SoforHesapServisi servis, Secimler s, IKullaniciOturumu oturum) : ListeVM<HarcirahSatir>(d)
{
    public override string Baslik => "Harcırahlar";
    public override bool TarihFiltresi => true;
    public override bool YeniVar => oturum.Yetkili(Izinler.SoforHesapYaz);
    public override bool SilVar => oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "İptal Et";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Şoför", nameof(HarcirahSatir.Sofor)), new("Sefer", nameof(HarcirahSatir.Sefer)), new("Ülke", nameof(HarcirahSatir.Ulke)),
        Kolon.Tarih("Başlangıç", nameof(HarcirahSatir.Baslangic)), Kolon.Tarih("Bitiş", nameof(HarcirahSatir.Bitis)),
        Kolon.Sayi("Gün", nameof(HarcirahSatir.Gun), true), Kolon.Para("Günlük", nameof(HarcirahSatir.Gunluk), false),
        Kolon.Para("Tutar", nameof(HarcirahSatir.Tutar)), new("PB", nameof(HarcirahSatir.PB)), new("Mahsuplu", "Mahsuplu?bool"), new("İptal", "Iptal?bool"),
    ];

    protected override async Task<IEnumerable<HarcirahSatir>> YukleAsync() => await servis.HarcirahlarAsync(Bas, Bit);

    protected override async Task YeniAsync()
    {
        if (await SoforHesapFormlari.HarcirahAsync(servis, s)) await YenileAsync();
    }

    protected override async Task SilAsync(HarcirahSatir h)
    {
        var neden = await Dialog.MetinIste("İptal nedeni:", "Harcırah iptali");
        if (neden is null) return;
        await servis.HarcirahIptalAsync(h.Id, neden);
        await YenileAsync();
    }
}

public sealed record MahsupSatir(int Id, DateOnly Tarih, string Sofor, string? Sefer, decimal Avans, decimal Harcama, decimal Harcirah, decimal Fark, string Sonuc, string? Aciklama);

public sealed class MahsupListeVM(IDisaAktarici d, SoforHesapServisi servis, IKullaniciOturumu oturum) : ListeVM<MahsupSatir>(d)
{
    public override string Baslik => "Mahsup Geçmişi";
    public override bool TarihFiltresi => true;
    public override bool SilVar => oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "Mahsubu İptal Et";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.Tarih("Tarih", nameof(MahsupSatir.Tarih)), new("Şoför", nameof(MahsupSatir.Sofor)), new("Sefer", nameof(MahsupSatir.Sefer)),
        Kolon.Para("Avans", nameof(MahsupSatir.Avans)), Kolon.Para("Harcama", nameof(MahsupSatir.Harcama)), Kolon.Para("Harcırah", nameof(MahsupSatir.Harcirah)),
        Kolon.Para("Fark", nameof(MahsupSatir.Fark)), new("Sonuç", nameof(MahsupSatir.Sonuc)), new("Açıklama", nameof(MahsupSatir.Aciklama)),
    ];

    protected override async Task<IEnumerable<MahsupSatir>> YukleAsync() =>
        (await servis.MahsuplarAsync(null, Bas, Bit)).Select(m => new MahsupSatir(m.Id, m.Tarih, m.Sofor!.AdSoyad, m.Sefer?.SeferNo,
            m.AvansToplam, m.HarcamaToplam, m.HarcirahToplam, m.Fark, m.Sonuc.Ad(), m.Aciklama));

    protected override async Task SilAsync(MahsupSatir m)
    {
        var neden = await Dialog.MetinIste("Mahsup iptal edilecek; kalemler tekrar açık duruma gelir. İptal nedeni:", "Mahsup iptali");
        if (neden is null) return;
        await servis.MahsupIptalAsync(m.Id, neden);
        await YenileAsync();
    }
}
