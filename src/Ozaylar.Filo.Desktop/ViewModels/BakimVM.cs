using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
using Button = Wpf.Ui.Controls.Button;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed class BakimListeVM(IDisaAktarici d, BakimServisi servis, Secimler s, IKullaniciOturumu oturum, IFiloDbContextFactory f) : ListeVM<BakimSatir>(d)
{
    public override string Baslik => "Bakım / Onarım Kayıtları";
    public override bool TarihFiltresi => true;
    public override bool YeniVar => oturum.Yetkili(Izinler.BakimYaz);
    public override bool DuzenleVar => true;
    public override bool SilVar => oturum.Yetkili(Izinler.FinansIptal);
    public override string SilMetni => "İptal Et";

    public override IReadOnlyList<Kolon> Kolonlar { get; } =
    [
        Kolon.Tarih("Tarih", nameof(BakimSatir.Tarih)), new("Plaka", nameof(BakimSatir.Plaka)), new("Tür", nameof(BakimSatir.TurAd)),
        Kolon.Sayi("Km", nameof(BakimSatir.Km)), new("Servis", nameof(BakimSatir.Servis)), new("Açıklama", nameof(BakimSatir.Aciklama), Genislik: 260),
        Kolon.Para("Tutar", nameof(BakimSatir.Tutar)), new("Ödeme", nameof(BakimSatir.Odeme)), new("İptal", "Iptal?bool"),
    ];

    public override IReadOnlyList<Eylem> Eylemler =>
    [
        E("Ekler", SymbolRegular.Attach24, () => Secili is { } b ? EklerPenceresi.AcAsync(EkHedef.Bakim, b.Id, $"{b.Plaka} {b.Tarih:dd.MM.yyyy}") : Task.CompletedTask),
    ];

    protected override async Task<IEnumerable<BakimSatir>> YukleAsync() => await servis.ListeAsync(Bas, Bit);
    protected override string? OzetHesapla(IReadOnlyList<BakimSatir> l) => $"Toplam: {l.Where(b => !b.Iptal).Sum(b => b.Tutar):N2} TL";

    protected override async Task YeniAsync()
    {
        if (await FormAsync(new Bakim { Tarih = DateOnly.FromDateTime(DateTime.Today) })) await YenileAsync();
    }

    protected override async Task DuzenleAsync(BakimSatir satir)
    {
        var b = await servis.GetirAsync(satir.Id);
        if (b is not null && await FormAsync(b)) await YenileAsync();
    }

    protected override async Task SilAsync(BakimSatir b)
    {
        var neden = await Dialog.MetinIste("İptal nedeni:", "Bakım kaydı iptali");
        if (neden is null) return;
        await servis.IptalAsync(b.Id, neden);
        await YenileAsync();
    }

    private async Task<bool> FormAsync(Bakim b)
    {
        await using var db = f.Olustur();
        var planlar = await db.BakimPlanlari.AsNoTracking().Where(p => p.Aktif)
            .Select(p => new Secim(p.Id, (p.Arac != null ? p.Arac.Plaka : p.Dorse!.Plaka) + " — " + p.BakimAdi)).ToListAsync();
        var kalemler = new ObservableCollection<BakimKalem>(b.Kalemler);
        var alanlar = new List<FormAlani>
        {
            new() { Etiket = "Araç", Yol = nameof(Bakim.AracId), Tip = AlanTipi.Secim, Secenekler = await s.AraclarAsync() },
            new() { Etiket = "Dorse", Yol = nameof(Bakim.DorseId), Tip = AlanTipi.Secim, Secenekler = await s.DorselerAsync() },
            FormAlani.EnumSecim<BakimTuru>("Bakım türü", nameof(Bakim.BakimTuru)),
            new() { Etiket = "Bakım planı", Yol = nameof(Bakim.BakimPlaniId), Tip = AlanTipi.Secim, Secenekler = planlar, Ipucu = "Seçilirse planın son bakım km/tarihi güncellenir" },
            new() { Etiket = "Tarih", Yol = nameof(Bakim.Tarih), Tip = AlanTipi.Tarih, Zorunlu = true },
            new() { Etiket = "Km", Yol = nameof(Bakim.Km), Tip = AlanTipi.Tamsayi },
            new() { Etiket = "Servis (cari)", Yol = nameof(Bakim.ServisCariId), Tip = AlanTipi.Secim, Secenekler = await s.CarilerAsync(CariTipi.Tedarikci) },
            new() { Etiket = "Servis adı (carisiz)", Yol = nameof(Bakim.ServisAdi), MaksUzunluk = 100 },
            new() { Etiket = "Toplam (KDV dahil)", Yol = nameof(Bakim.ToplamTutar), Tip = AlanTipi.Para, Ipucu = "Kalem girilirse kalemlerden hesaplanır" },
            new() { Etiket = "KDV %", Yol = nameof(Bakim.KdvOrani), Tip = AlanTipi.Tamsayi },
            new() { Etiket = "Ödeme", Yol = nameof(Bakim.OdemeSekli), Tip = AlanTipi.Secim, Secenekler = EnumMetin.Liste<OdemeSekli>().Where(x => x.Deger != OdemeSekli.SoforNakit).ToList() },
            new() { Etiket = "Kasa / banka", Yol = nameof(Bakim.KasaHesapId), Tip = AlanTipi.Secim, Secenekler = await s.KasalarAsync(ParaBirimi.TRY), Gorunur = _ => b.OdemeSekli == OdemeSekli.Kasa },
            new() { Etiket = "Fatura / belge no", Yol = nameof(Bakim.BelgeNo), MaksUzunluk = 30 },
            new() { Etiket = "Garanti bitiş", Yol = nameof(Bakim.GarantiBitis), Tip = AlanTipi.Tarih },
            new() { Etiket = "Açıklama", Yol = nameof(Bakim.Aciklama), Tip = AlanTipi.CokSatir, MaksUzunluk = 500 },
        };
        if (!oturum.Yetkili(Izinler.BakimYaz) || b.Iptal)
        {
            new FormPenceresi("Bakım", b, alanlar, _ => Task.CompletedTask, salt: true, genislik: 900).ShowDialog();
            return false;
        }
        if (await Dialog.Onay("Parça / işçilik kalemlerini ayrı ayrı girmek ister misiniz?\n(Hayır derseniz yalnızca toplam tutar girilir.)", "Bakım kalemleri", "Kalem gir", "Yalnızca toplam"))
        {
            if (!KalemPenceresi.Ac(kalemler)) return false;
            b.Kalemler = kalemler.Where(k => !string.IsNullOrWhiteSpace(k.Aciklama)).ToList();
            b.ToplamTutar = b.Kalemler.Sum(k => Math.Round(k.Miktar * k.BirimFiyat, 2));
        }
        return FormPenceresi.Ac(b.Id == 0 ? "Yeni Bakım / Onarım" : "Bakım Düzenle", b, alanlar, _ => servis.KaydetAsync(b), 900);
    }
}

/// <summary>Bakım parça/işçilik kalemleri düzenleyici.</summary>
public static class KalemPenceresi
{
    public static bool Ac(ObservableCollection<BakimKalem> kalemler)
    {
        var tablo = new System.Windows.Controls.DataGrid { ItemsSource = kalemler, AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column };
        tablo.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Tip", ItemsSource = EnumMetin.Liste<BakimKalemTipi>(), SelectedValuePath = "Deger", DisplayMemberPath = "Ad",
            SelectedValueBinding = new Binding(nameof(BakimKalem.KalemTipi)), Width = 110,
        });
        tablo.Columns.Add(new DataGridTextColumn { Header = "Açıklama", Binding = new Binding(nameof(BakimKalem.Aciklama)), Width = 320 });
        tablo.Columns.Add(new DataGridTextColumn { Header = "Miktar", Binding = new Binding(nameof(BakimKalem.Miktar)) { Converter = SayiDonusturucu.Ornek }, Width = 80 });
        tablo.Columns.Add(new DataGridTextColumn { Header = "Birim Fiyat (KDV dahil)", Binding = new Binding(nameof(BakimKalem.BirimFiyat)) { Converter = SayiDonusturucu.Ornek, ConverterParameter = "N2" }, Width = 160 });
        tablo.InitializingNewItem += (_, e) => ((BakimKalem)e.NewItem).Miktar = 1;

        var toplam = new System.Windows.Controls.TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
        void Hesapla() => toplam.Text = $"Toplam: {kalemler.Sum(k => k.Miktar * k.BirimFiyat):N2} TL";
        tablo.RowEditEnding += (_, _) => tablo.Dispatcher.BeginInvoke(Hesapla);
        Hesapla();

        var w = new FluentWindow
        {
            Title = "Parça / İşçilik", Width = 760, Height = 460, Owner = Dialog.Sahip, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ExtendsContentIntoTitleBar = true, WindowBackdropType = WindowBackdropType.Mica,
        };
        var tamam = new Button { Content = "Devam", Appearance = ControlAppearance.Primary, Margin = new Thickness(0, 0, 8, 0) };
        tamam.Click += (_, _) => { tablo.CommitEdit(DataGridEditingUnit.Row, true); w.DialogResult = true; };
        var iptal = new Button { Content = "Vazgeç", IsCancel = true };
        var alt = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        alt.Children.Add(tamam);
        alt.Children.Add(iptal);
        var ic = new DockPanel { Margin = new Thickness(16, 8, 16, 16) };
        DockPanel.SetDock(alt, Dock.Bottom);
        DockPanel.SetDock(toplam, Dock.Bottom);
        ic.Children.Add(alt);
        ic.Children.Add(toplam);
        ic.Children.Add(tablo);
        var kok = new DockPanel();
        var tb = new TitleBar { Title = "Parça / İşçilik", ShowMinimize = false };
        DockPanel.SetDock(tb, Dock.Top);
        kok.Children.Add(tb);
        kok.Children.Add(ic);
        w.Content = kok;
        return w.ShowDialog() == true;
    }
}
