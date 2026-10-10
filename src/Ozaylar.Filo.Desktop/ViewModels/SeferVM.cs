using System.Collections.ObjectModel;
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

/// <summary>Sefer formu modeli: sefer + çoklu şoför + taşeron anahtarı.</summary>
public sealed class SeferFormModel
{
    public Sefer S { get; init; } = new();
    public short? Sofor1Id { get; set; }
    public short? Sofor2Id { get; set; }
    public bool Taseron { get; set; }
}

public sealed class SeferListeVM(
    IDisaAktarici d, SeferServisi servis, Secimler secimler, IKullaniciOturumu oturum, DovizServisi doviz,
    IFiloDbContextFactory f, GiderServisi gider, SoforHesapServisi soforHesap) : ListeVM<SeferSatir>(d)
{
    public override string Baslik => "Seferler";
    public override bool TarihFiltresi => true;
    public override string? OnayFiltreMetni => "Tarihten bağımsız tüm açık seferler";
    public override bool YeniVar => oturum.Yetkili(Izinler.SeferYaz);
    public override bool DuzenleVar => true;
    public override bool SilVar => oturum.Yetkili(Izinler.SeferYaz);

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        new("Sefer No", nameof(SeferSatir.SeferNo), Genislik: 110), Kolon.Tarih("Tarih", nameof(SeferSatir.PlanlananTarih)),
        new("Durum", nameof(SeferSatir.DurumAd)), new("Kapsam", nameof(SeferSatir.KapsamAd)), new("Müşteri", nameof(SeferSatir.Musteri), Genislik: 200),
        new("Araç", nameof(SeferSatir.Arac)), new("Dorse", nameof(SeferSatir.Dorse)), new("Şoför", nameof(SeferSatir.Soforler)),
        new("Çıkış", nameof(SeferSatir.Cikis)), new("Varış", nameof(SeferSatir.Varis)), new("Yük", nameof(SeferSatir.YukTanimi)),
        Kolon.Sayi("Km", nameof(SeferSatir.Km), true), Kolon.Para("Navlun (TL)", nameof(SeferSatir.NavlunTL)), new("Taşeron", "Taseron?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Yola Çıktı", SymbolRegular.Play24, () => DurumAsync(SeferDurum.Yolda)),
        E("Teslim Edildi", SymbolRegular.CheckboxChecked24, () => DurumAsync(SeferDurum.TeslimEdildi)),
        E("Kapat", SymbolRegular.LockClosed24, KapatAsync),
        E("Yeniden Aç", SymbolRegular.ArrowUndo24, () => DurumAsync(SeferDurum.TeslimEdildi, "Seçili sefer yeniden açılsın mı? (Taşeron kaydı ters çevrilir)")),
        E("İptal", SymbolRegular.Dismiss24, () => DurumAsync(SeferDurum.Iptal, "Seçili sefer iptal edilsin mi?")),
        E("Masraf Ekle", SymbolRegular.ReceiptMoney24, MasrafEkleAsync),
        E("Avans Ver", SymbolRegular.MoneyHand24, AvansAsync),
        E("Harcırah", SymbolRegular.CalendarClock24, HarcirahAsync),
        E("Duraklar", SymbolRegular.Location24, DuraklarAsync),
        E("Kârlılık", SymbolRegular.DataBarVertical24, KarlilikAsync),
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } s ? EklerPenceresi.AcAsync(EkHedef.Sefer, s.Id, s.SeferNo) : Task.CompletedTask),
    ];

    protected override async Task<IEnumerable<SeferSatir>> YukleAsync() =>
        await servis.ListeAsync(OnayFiltre ? new SeferFiltre(SadeceAcik: true) : new SeferFiltre(Bas, Bit));

    protected override string? OzetHesapla(IReadOnlyList<SeferSatir> l) =>
        servis.FiyatGorebilir ? $"Toplam navlun: {l.Sum(s => s.NavlunTL ?? 0):N2} TL   •   Toplam km: {l.Sum(s => s.Km ?? 0):N0}" : $"Toplam km: {l.Sum(s => s.Km ?? 0):N0}";

    protected override async Task YeniAsync()
    {
        var m = new SeferFormModel { S = new Sefer { PlanlananTarih = DateOnly.FromDateTime(DateTime.Today) } };
        if (await FormAcAsync(m, true)) await YenileAsync();
    }

    protected override async Task DuzenleAsync(SeferSatir satir)
    {
        var s = await servis.GetirAsync(satir.Id);
        if (s is null) return;
        var soforler = s.Soforler.OrderByDescending(x => x.Birincil).Select(x => x.SoforId).ToList();
        var m = new SeferFormModel { S = s, Sofor1Id = soforler.ElementAtOrDefault(0) is var a and > 0 ? a : null, Sofor2Id = soforler.ElementAtOrDefault(1) is var b and > 0 ? b : null, Taseron = s.TaseronCariId is not null };
        if (await FormAcAsync(m, false)) await YenileAsync();
    }

    private async Task<bool> FormAcAsync(SeferFormModel m, bool yeni)
    {
        var duzenlenebilir = yeni || m.S.Durum is < SeferDurum.Kapatildi;
        var fiyat = servis.FiyatGorebilir;
        var guzergahlar = await secimler.GuzergahlarAsync();
        var lok = await secimler.LokasyonlarAsync();
        var alanlar = new List<FormAlani>
        {
            FormAlani.Bolum(yeni ? "Yeni sefer" : $"{m.S.SeferNo} — {m.S.Durum.Ad()}"),
            FormAlani.EnumSecim<SeferKapsam>("Kapsam", "S.Kapsam"),
            new() { Etiket = "Planlanan tarih", Yol = "S.PlanlananTarih", Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Müşteri", Yol = "S.MusteriCariId", Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = await secimler.CarilerAsync(CariTipi.Musteri) },
            new()
            {
                Etiket = "Güzergah", Yol = "S.GuzergahId", Tip = AlanTipi.Secim, Secenekler = guzergahlar, Ipucu = "Seçilince çıkış/varış ve standart navlun doldurulur",
                Degisince = async _ =>
                {
                    if (m.S.GuzergahId is not { } gid) return;
                    await using var db = f.Olustur();
                    var g = await db.Guzergahlar.AsNoTracking().SingleAsync(x => x.Id == gid);
                    m.S.CikisLokasyonId = g.CikisLokasyonId;
                    m.S.VarisLokasyonId = g.VarisLokasyonId;
                    if (fiyat && m.S.NavlunTutar == 0 && g.StandartNavlun is > 0)
                    {
                        m.S.NavlunTutar = g.StandartNavlun.Value;
                        m.S.NavlunParaBirimi = g.NavlunParaBirimi;
                        m.S.NavlunKur = await doviz.KurAsync(g.NavlunParaBirimi, m.S.PlanlananTarih) ?? 1;
                    }
                },
            },
            new() { Etiket = "Çıkış", Yol = "S.CikisLokasyonId", Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = lok },
            new() { Etiket = "Varış", Yol = "S.VarisLokasyonId", Tip = AlanTipi.Secim, Zorunlu = true, Secenekler = lok },
            new() { Etiket = "Yük tanımı", Yol = "S.YukTanimi", MaksUzunluk = 200 },
            new() { Etiket = "Tonaj", Yol = "S.Tonaj", Tip = AlanTipi.Ondalik },
            new() { Etiket = "CMR no", Yol = "S.CmrNo", MaksUzunluk = 30, Gorunur = _ => m.S.Kapsam == SeferKapsam.Yurtdisi },
            new() { Etiket = "İrsaliye no", Yol = "S.IrsaliyeNo", MaksUzunluk = 30 },

            FormAlani.Bolum("Araç ve şoför"),
            new() { Etiket = "Taşeron (dış araç) seferi", Yol = nameof(SeferFormModel.Taseron), Tip = AlanTipi.Onay },
            new()
            {
                Etiket = "Araç", Yol = "S.AracId", Tip = AlanTipi.Secim, Secenekler = await secimler.AraclarAsync(), Gorunur = _ => !m.Taseron,
                Degisince = async _ =>
                {
                    if (m.S.AracId is not { } aid) return;
                    var (dorse, sofor) = await servis.AracVarsayilanlariAsync(aid);
                    m.S.DorseId ??= dorse;
                    m.Sofor1Id ??= sofor;
                },
            },
            new() { Etiket = "Dorse", Yol = "S.DorseId", Tip = AlanTipi.Secim, Secenekler = await secimler.DorselerAsync(), Gorunur = _ => !m.Taseron },
            new() { Etiket = "Şoför", Yol = nameof(SeferFormModel.Sofor1Id), Tip = AlanTipi.Secim, Secenekler = await secimler.SoforlerAsync(), Gorunur = _ => !m.Taseron },
            new() { Etiket = "2. şoför", Yol = nameof(SeferFormModel.Sofor2Id), Tip = AlanTipi.Secim, Secenekler = await secimler.SoforlerAsync(), Gorunur = _ => !m.Taseron },
            new() { Etiket = "Taşeron firma", Yol = "S.TaseronCariId", Tip = AlanTipi.Secim, Secenekler = await secimler.CarilerAsync(CariTipi.Taseron), Gorunur = _ => m.Taseron },
            new() { Etiket = "Taşeron plaka", Yol = "S.TaseronPlaka", MaksUzunluk = 15, Gorunur = _ => m.Taseron },
        };
        if (fiyat)
        {
            alanlar.AddRange(
            [
                new() { Etiket = "Taşerona ödenecek", Yol = "S.TaseronBedel", Tip = AlanTipi.Para, Gorunur = _ => m.Taseron },
                new() { Etiket = "Taşeron para birimi", Yol = "S.TaseronParaBirimi", Tip = AlanTipi.Secim, Secenekler = EnumMetin.Liste<ParaBirimi>(), Gorunur = _ => m.Taseron },
                new() { Etiket = "Taşeron kuru", Yol = "S.TaseronKur", Tip = AlanTipi.Ondalik, Gorunur = _ => m.Taseron && m.S.TaseronParaBirimi != ParaBirimi.TRY },
                FormAlani.Bolum("Navlun (gelir)"),
                new() { Etiket = "Navlun tutarı (KDV hariç)", Yol = "S.NavlunTutar", Tip = AlanTipi.Para },
                new()
                {
                    Etiket = "Para birimi", Yol = "S.NavlunParaBirimi", Tip = AlanTipi.Secim, Secenekler = EnumMetin.Liste<ParaBirimi>(),
                    Degisince = async _ =>
                    {
                        m.S.NavlunKur = await doviz.KurAsync(m.S.NavlunParaBirimi, m.S.PlanlananTarih) ?? (m.S.NavlunParaBirimi == ParaBirimi.TRY ? 1 : 0);
                        if (m.S.NavlunParaBirimi != ParaBirimi.TRY) { m.S.NavlunKdvOrani = 0; m.S.NavlunTevkifatPay = 0; }
                    },
                },
                new() { Etiket = "Kur (TCMB)", Yol = "S.NavlunKur", Tip = AlanTipi.Ondalik, Gorunur = _ => m.S.NavlunParaBirimi != ParaBirimi.TRY, Ipucu = "TCMB döviz satış kuru otomatik gelir; değiştirilebilir" },
                new() { Etiket = "KDV %", Yol = "S.NavlunKdvOrani", Tip = AlanTipi.Tamsayi },
                new() { Etiket = "Tevkifat (x/10)", Yol = "S.NavlunTevkifatPay", Tip = AlanTipi.Tamsayi, Ipucu = "Yurtiçi yük taşımacılığında genellikle 2/10 — mali müşavirinizle teyit edin" },
            ]);
        }
        alanlar.AddRange(
        [
            FormAlani.Bolum("Gerçekleşen"),
            new() { Etiket = "Çıkış zamanı", Yol = "S.CikisTarihi", Tip = AlanTipi.TarihSaat },
            new() { Etiket = "Teslim zamanı", Yol = "S.TeslimTarihi", Tip = AlanTipi.TarihSaat },
            new() { Etiket = "Dönüş zamanı", Yol = "S.DonusTarihi", Tip = AlanTipi.TarihSaat },
            new() { Etiket = "Çıkış km", Yol = "S.CikisKm", Tip = AlanTipi.Tamsayi, Gorunur = _ => !m.Taseron },
            new() { Etiket = "Dönüş km", Yol = "S.DonusKm", Tip = AlanTipi.Tamsayi, Gorunur = _ => !m.Taseron },
            new() { Etiket = "Boş (yüksüz) km", Yol = "S.BosKm", Tip = AlanTipi.Tamsayi, Gorunur = _ => !m.Taseron },
            new() { Etiket = "Notlar", Yol = "S.Notlar", Tip = AlanTipi.CokSatir, MaksUzunluk = 500 },
        ]);

        if (!duzenlenebilir || !oturum.Yetkili(Izinler.SeferYaz))
        {
            new FormPenceresi($"Sefer {m.S.SeferNo}", m, alanlar, _ => Task.CompletedTask, salt: true, genislik: 980).ShowDialog();
            return false;
        }
        var ok = FormPenceresi.Ac(yeni ? "Yeni Sefer" : $"Sefer {m.S.SeferNo}", m, alanlar, async onay =>
        {
            if (m.Taseron) { m.S.AracId = null; m.S.DorseId = null; }
            else { m.S.TaseronCariId = null; }
            var soforler = new[] { m.Sofor1Id, m.Sofor2Id }.Where(x => x is not null && !m.Taseron).Select(x => x!.Value).Distinct().ToList();
            var duraklar = yeni ? [] : await DuraklariGetirAsync(m.S.Id);
            await servis.KaydetAsync(m.S, soforler, duraklar, onay);
        }, 980);
        if (ok) AnaPencere.Basarili(yeni ? $"Sefer {m.S.SeferNo} açıldı." : "Sefer kaydedildi.");
        return ok;
    }

    private async Task<List<SeferDurak>> DuraklariGetirAsync(int seferId)
    {
        await using var db = f.Olustur();
        return await db.SeferDuraklari.AsNoTracking().Where(x => x.SeferId == seferId).OrderBy(x => x.Sira).ToListAsync();
    }

    private async Task DurumAsync(SeferDurum durum, string? onayMetni = null)
    {
        if (Secili is not { } s) return;
        if (onayMetni is not null && !await Dialog.Onay(onayMetni)) return;
        if (await Dialog.Calistir(() => servis.DurumDegistirAsync(s.Id, durum)))
        {
            AnaPencere.Basarili($"{s.SeferNo}: {durum.Ad()}");
            await YenileAsync();
        }
    }

    private sealed class KapatModel
    {
        public int? CikisKm { get; set; }
        public int? DonusKm { get; set; }
        public DateTime? TeslimTarihi { get; set; }
        public DateTime? DonusTarihi { get; set; }
    }

    /// <summary>Kapatma: km/teslim eksikse önce hızlıca girdirir.</summary>
    private async Task KapatAsync()
    {
        if (Secili is not { } satir) return;
        var s = await servis.GetirAsync(satir.Id);
        if (s is null) return;
        var eksik = (s.AracId is not null && (s.CikisKm is null || s.DonusKm is null)) || s.TeslimTarihi is null;
        if (eksik)
        {
            var m = new KapatModel { CikisKm = s.CikisKm, DonusKm = s.DonusKm, TeslimTarihi = s.TeslimTarihi ?? DateTime.Now, DonusTarihi = s.DonusTarihi ?? DateTime.Now };
            var alanlar = new List<FormAlani>
            {
                new() { Etiket = "Çıkış km", Yol = nameof(KapatModel.CikisKm), Tip = AlanTipi.Tamsayi, Zorunlu = s.AracId is not null },
                new() { Etiket = "Dönüş km", Yol = nameof(KapatModel.DonusKm), Tip = AlanTipi.Tamsayi, Zorunlu = s.AracId is not null },
                new() { Etiket = "Teslim zamanı", Yol = nameof(KapatModel.TeslimTarihi), Tip = AlanTipi.TarihSaat, Zorunlu = true },
                new() { Etiket = "Dönüş zamanı", Yol = nameof(KapatModel.DonusTarihi), Tip = AlanTipi.TarihSaat },
            };
            if (!FormPenceresi.Ac($"{s.SeferNo} — Kapat", m, alanlar, async onay =>
                {
                    s.CikisKm = m.CikisKm;
                    s.DonusKm = m.DonusKm;
                    s.TeslimTarihi = m.TeslimTarihi;
                    s.DonusTarihi = m.DonusTarihi;
                    if (s.Durum < SeferDurum.TeslimEdildi)
                    {
                        await servis.KaydetAsync(s, s.Soforler.OrderByDescending(x => x.Birincil).Select(x => x.SoforId).ToList(), await DuraklariGetirAsync(s.Id), onay);
                        await servis.DurumDegistirAsync(s.Id, SeferDurum.TeslimEdildi);
                    }
                    else
                    {
                        // Teslim edilmiş sefer formdan düzenlenebilir (kapatılmadıkça)
                        await servis.KaydetAsync(s, s.Soforler.OrderByDescending(x => x.Birincil).Select(x => x.SoforId).ToList(), await DuraklariGetirAsync(s.Id), onay);
                    }
                    await servis.DurumDegistirAsync(s.Id, SeferDurum.Kapatildi);
                })) return;
        }
        else if (!await Dialog.Calistir(() => servis.DurumDegistirAsync(s.Id, SeferDurum.Kapatildi))) return;
        AnaPencere.Basarili($"{s.SeferNo} kapatıldı.");
        await YenileAsync();
    }

    private async Task MasrafEkleAsync()
    {
        if (Secili is not { } s) return;
        var m = new Masraf
        {
            Tarih = DateOnly.FromDateTime(DateTime.Today), Merkez = MasrafMerkezi.Sefer, SeferId = s.Id, OdemeSekli = OdemeSekli.SoforNakit,
        };
        if (await MasrafFormu.AcAsync(m, gider, secimler, f)) AnaPencere.Basarili("Masraf eklendi.");
    }

    private async Task AvansAsync()
    {
        if (Secili is not { } s) return;
        var sofor = await IlkSoforAsync(s.Id);
        await SoforHesapFormlari.AvansAsync(soforHesap, secimler, sofor, s.Id);
    }

    private async Task HarcirahAsync()
    {
        if (Secili is not { } s) return;
        var sofor = await IlkSoforAsync(s.Id);
        if (sofor is null) { await Dialog.Bilgi("Seferde şoför yok."); return; }
        var oneri = await soforHesap.SeferdenHarcirahOnerAsync(s.Id, sofor.Value);
        await SoforHesapFormlari.HarcirahAsync(soforHesap, secimler, oneri);
    }

    private async Task<short?> IlkSoforAsync(int seferId)
    {
        await using var db = f.Olustur();
        return await db.SeferSoforleri.Where(x => x.SeferId == seferId).OrderByDescending(x => x.Birincil).Select(x => (short?)x.SoforId).FirstOrDefaultAsync();
    }

    private async Task KarlilikAsync()
    {
        if (Secili is not { } s) return;
        var k = await servis.KarlilikAsync(s.Id);
        var satirlar = string.Join("\n", k.Giderler.Select(g => $"   {g.Kalem,-28} {g.TutarTL,14:N2}"));
        await Dialog.Bilgi(
            $"Gelir (navlun, KDV hariç): {k.GelirTL:N2} TL\n\nGiderler (KDV hariç):\n{(satirlar.Length == 0 ? "   (gider kaydı yok)" : satirlar)}\n" +
            $"   {"TOPLAM GİDER",-28} {k.GiderTL,14:N2}\n\nKÂR: {k.KarTL:N2} TL" + (k.KarMarji is null ? "" : $"  (marj %{k.KarMarji:N1})") +
            (k.KmBasiMaliyet is null ? "" : $"\nKm başı maliyet: {k.KmBasiMaliyet:N2} TL ({k.Km:N0} km)"),
            $"Sefer Kârlılığı — {s.SeferNo}");
    }

    protected override async Task SilAsync(SeferSatir s)
    {
        if (!await Dialog.Onay($"{s.SeferNo} silinsin mi? (Yalnızca planlanmış veya iptal edilmiş seferler silinebilir)", "Sefer sil", "Sil")) return;
        await servis.SilAsync(s.Id);
        await YenileAsync();
    }

    // -------------------- Duraklar --------------------

    public sealed class DurakSatiri
    {
        public int LokasyonId { get; set; }
        public DurakTipi DurakTipi { get; set; } = DurakTipi.Ara;
        public DateTime? PlanlananTarih { get; set; }
        public string? Aciklama { get; set; }
    }

    private async Task DuraklarAsync()
    {
        if (Secili is not { } satir) return;
        var s = await servis.GetirAsync(satir.Id);
        if (s is null) return;
        var lok = await secimler.LokasyonlarAsync();
        var liste = new ObservableCollection<DurakSatiri>(s.Duraklar.OrderBy(d => d.Sira)
            .Select(d => new DurakSatiri { LokasyonId = d.LokasyonId, DurakTipi = d.DurakTipi, PlanlananTarih = d.PlanlananTarih, Aciklama = d.Aciklama }));
        var duzenlenebilir = s.Durum < SeferDurum.Kapatildi && oturum.Yetkili(Izinler.SeferYaz);
        if (!DurakPenceresi.Ac($"{s.SeferNo} — Ara Duraklar", liste, lok, duzenlenebilir)) return;
        var soforler = s.Soforler.OrderByDescending(x => x.Birincil).Select(x => x.SoforId).ToList();
        var duraklar = liste.Where(x => x.LokasyonId > 0).Select(x => new SeferDurak { LokasyonId = x.LokasyonId, DurakTipi = x.DurakTipi, PlanlananTarih = x.PlanlananTarih, Aciklama = x.Aciklama }).ToList();
        if (await Dialog.Calistir(onay => servis.KaydetAsync(s, soforler, duraklar, onay))) AnaPencere.Basarili("Duraklar kaydedildi.");
    }
}
