using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;
using Ozaylar.Filo.Desktop.Pencereler;
using Ozaylar.Filo.Domain.Common;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed partial class IceAktarmaVM(IceAktarmaServisi servis, Secimler secimler) : ObservableObject
{
    public IReadOnlyList<SecimOgesi<IceAktarimTur>> Turler { get; } = EnumMetin.Liste<IceAktarimTur>();
    public ObservableCollection<IceAktarimSablon> Sablonlar { get; } = [];
    public ObservableCollection<Secim> Cariler { get; } = [];
    public ObservableCollection<OnizlemeSatir> Satirlar { get; } = [];
    public ObservableCollection<IceAktarim> Gecmis { get; } = [];

    [ObservableProperty] private IceAktarimTur _tur = IceAktarimTur.Yakit;
    [ObservableProperty] private IceAktarimSablon? _sablon;
    [ObservableProperty] private int? _cariId;
    [ObservableProperty] private string? _dosya;
    [ObservableProperty] private string? _ozet;
    [ObservableProperty] private bool _mesgul;
    private IceAktarimOnizleme? _onizleme;

    partial void OnTurChanged(IceAktarimTur value) => _ = SablonlariYukleAsync();

    public async Task YukleAsync()
    {
        await SablonlariYukleAsync();
        Cariler.Clear();
        foreach (var c in await secimler.CarilerAsync(CariTipi.Tedarikci)) Cariler.Add(c);
        await GecmisYukleAsync();
    }

    private async Task SablonlariYukleAsync()
    {
        Sablonlar.Clear();
        foreach (var s in await servis.SablonlarAsync(Tur)) Sablonlar.Add(s);
        Sablon = Sablonlar.FirstOrDefault();
        Satirlar.Clear();
        _onizleme = null;
        Ozet = null;
    }

    private async Task GecmisYukleAsync()
    {
        Gecmis.Clear();
        foreach (var g in await servis.GecmisAsync()) Gecmis.Add(g);
    }

    [RelayCommand]
    private async Task DosyaSecAsync()
    {
        if (Sablon is null) { await Dialog.Bilgi("Önce bir şablon seçin (Tanımlar > Ekstre Şablonları)."); return; }
        var dlg = new OpenFileDialog { Filter = "Excel / CSV|*.xlsx;*.csv;*.txt" };
        if (dlg.ShowDialog() != true) return;
        Dosya = dlg.FileName;
        Mesgul = true;
        try
        {
            await using var akis = File.OpenRead(dlg.FileName);
            _onizleme = await servis.OnizleAsync(akis, dlg.FileName, Sablon.Id);
            Satirlar.Clear();
            foreach (var s in _onizleme.Satirlar) Satirlar.Add(s);
            Ozet = $"{_onizleme.Satirlar.Count} satır okundu  •  {_onizleme.Hazir} satır aktarılacak  •  " +
                   $"{_onizleme.Satirlar.Count(s => s.Hata is not null)} hatalı  •  {_onizleme.Satirlar.Count(s => s.Mukerrer)} mükerrer  •  Toplam: {_onizleme.ToplamTutar:N2} TL";
        }
        catch (Exception ex)
        {
            await Dialog.HataGoster(ex);
        }
        finally
        {
            Mesgul = false;
        }
    }

    [RelayCommand]
    private async Task AktarAsync()
    {
        if (_onizleme is null || _onizleme.Hazir == 0) { await Dialog.Bilgi("Aktarılacak satır yok."); return; }
        var cariMetin = CariId is null ? "Cari seçilmedi: tutar hiçbir cariye borç yazılmayacak." : $"{_onizleme.ToplamTutar:N2} TL seçilen cariye borç (alacak) olarak yazılacak.";
        if (!await Dialog.Onay($"{_onizleme.Hazir} satır aktarılacak.\n{cariMetin}\n\nDevam edilsin mi?", "Ekstre aktarımı", "Aktar")) return;
        if (await Dialog.Calistir(() => servis.AktarAsync(_onizleme, CariId)))
        {
            AnaPencere.Basarili($"{_onizleme.Hazir} satır aktarıldı.");
            Satirlar.Clear();
            _onizleme = null;
            Ozet = null;
            Dosya = null;
            await GecmisYukleAsync();
        }
    }
}
