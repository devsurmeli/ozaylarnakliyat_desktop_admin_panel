using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ozaylar.Filo.Application.Abstractions;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Altyapi;

public sealed record Eylem(string Metin, SymbolRegular Ikon, IAsyncRelayCommand Komut);

public interface IListeVM : INotifyPropertyChanged
{
    string Baslik { get; }
    IReadOnlyList<Kolon> Kolonlar { get; }
    ICollectionView Gorunum { get; }
    object? SeciliNesne { get; set; }
    string? Arama { get; set; }
    bool Mesgul { get; }
    bool TarihFiltresi { get; }
    DateTime? BasTarih { get; set; }
    DateTime? BitTarih { get; set; }
    string? OnayFiltreMetni { get; }
    bool OnayFiltre { get; set; }
    bool YeniVar { get; }
    bool DuzenleVar { get; }
    bool SilVar { get; }
    string SilMetni { get; }
    string? AltBilgi { get; }
    IReadOnlyList<Eylem> Eylemler { get; }
    IAsyncRelayCommand YenileKomut { get; }
    IAsyncRelayCommand YeniKomut { get; }
    IAsyncRelayCommand DuzenleKomut { get; }
    IAsyncRelayCommand SilKomut { get; }
    IAsyncRelayCommand ExcelKomut { get; }
    IAsyncRelayCommand PdfKomut { get; }
    Task IlkYuklemeAsync();
}

/// <summary>
/// Liste ekranlarının ortak davranışı: yükleme, metin arama, tarih filtresi, yeni/düzenle/sil, ek eylemler, Excel/PDF.
/// Türetilen sınıf yalnızca kolonları ve veri/işlem metotlarını tanımlar.
/// </summary>
public abstract partial class ListeVM<T> : ObservableObject, IListeVM where T : class
{
    private readonly ObservableCollection<T> _satirlar = [];
    private bool _yuklendi;
    private readonly IDisaAktarici _disa;

    protected ListeVM(IDisaAktarici disa)
    {
        _disa = disa;
        Gorunum = CollectionViewSource.GetDefaultView(_satirlar);
        Gorunum.Filter = o => Filtre((T)o);
        var bugun = DateTime.Today;
        _basTarih = new DateTime(bugun.Year, bugun.Month, 1).AddMonths(-1);
        _bitTarih = bugun;
    }

    public abstract string Baslik { get; }
    public abstract IReadOnlyList<Kolon> Kolonlar { get; }
    public ICollectionView Gorunum { get; }
    public IReadOnlyCollection<T> Satirlar => _satirlar;

    [ObservableProperty] private object? _seciliNesne;
    [ObservableProperty] private string? _arama;
    [ObservableProperty] private bool _mesgul;
    [ObservableProperty] private DateTime? _basTarih;
    [ObservableProperty] private DateTime? _bitTarih;
    [ObservableProperty] private bool _onayFiltre;
    [ObservableProperty] private string? _altBilgi;

    public T? Secili => SeciliNesne as T;
    public virtual bool TarihFiltresi => false;
    public virtual string? OnayFiltreMetni => null;
    public virtual bool YeniVar => false;
    public virtual bool DuzenleVar => false;
    public virtual bool SilVar => false;
    public virtual string SilMetni => "Sil";
    public virtual IReadOnlyList<Eylem> Eylemler => [];

    protected DateOnly Bas => DateOnly.FromDateTime(BasTarih ?? DateTime.Today.AddYears(-1));
    protected DateOnly Bit => DateOnly.FromDateTime(BitTarih ?? DateTime.Today);

    protected abstract Task<IEnumerable<T>> YukleAsync();
    protected virtual Task YeniAsync() => Task.CompletedTask;
    protected virtual Task DuzenleAsync(T satir) => Task.CompletedTask;
    protected virtual Task SilAsync(T satir) => Task.CompletedTask;
    protected virtual string? OzetHesapla(IReadOnlyList<T> gorunen) => null;

    partial void OnAramaChanged(string? value) { Gorunum.Refresh(); OzetGuncelle(); }
    partial void OnOnayFiltreChanged(bool value) => _ = YenileAsync();
    partial void OnBasTarihChanged(DateTime? value) { if (_yuklendi) _ = YenileAsync(); }
    partial void OnBitTarihChanged(DateTime? value) { if (_yuklendi) _ = YenileAsync(); }

    public async Task IlkYuklemeAsync()
    {
        if (_yuklendi) return;
        _yuklendi = true;
        await YenileAsync();
    }

    [RelayCommand]
    public async Task YenileAsync()
    {
        if (Mesgul) return;
        Mesgul = true;
        try
        {
            var seciliEski = Secili;
            var veri = (await YukleAsync()).ToList();
            _satirlar.Clear();
            foreach (var s in veri) _satirlar.Add(s);
            if (seciliEski is not null) SeciliNesne = _satirlar.FirstOrDefault(s => s.Equals(seciliEski));
            OzetGuncelle();
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
    private async Task Yeni()
    {
        Serilog.Log.Debug("{Ekran}: yeni kayıt", Baslik);
        try { await YeniAsync(); }
        catch (Exception ex) { await Dialog.HataGoster(ex); }
    }

    [RelayCommand]
    private async Task Duzenle()
    {
        if (Secili is not { } s) return;
        try { await DuzenleAsync(s); }
        catch (Exception ex) { await Dialog.HataGoster(ex); }
    }

    [RelayCommand]
    private async Task Sil()
    {
        if (Secili is not { } s) return;
        try { await SilAsync(s); }
        catch (Exception ex) { await Dialog.HataGoster(ex); }
    }

    [RelayCommand]
    private Task Excel() => DisaAktarAsync(false);

    [RelayCommand]
    private Task Pdf() => DisaAktarAsync(true);

    protected virtual string[] DisaAltBilgi() => TarihFiltresi ? [$"Dönem: {Bas:dd.MM.yyyy} - {Bit:dd.MM.yyyy}"] : [];

    private async Task DisaAktarAsync(bool pdf)
    {
        try
        {
            var tablo = KolonOlusturucu.Tablo(Baslik, Kolonlar, Gorunum, DisaAltBilgi());
            var dlg = new SaveFileDialog
            {
                FileName = $"{Baslik} {DateTime.Now:yyyy-MM-dd}".Replace('/', '-'),
                Filter = pdf ? "PDF (*.pdf)|*.pdf" : "Excel (*.xlsx)|*.xlsx",
            };
            if (dlg.ShowDialog() != true) return;
            var veri = await Task.Run(() => pdf ? _disa.Pdf(tablo) : _disa.Excel(tablo));
            await File.WriteAllBytesAsync(dlg.FileName, veri);
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (IOException ex)
        {
            await Dialog.Hata("Dosya yazılamadı (açık olabilir): " + ex.Message);
        }
        catch (Exception ex)
        {
            await Dialog.HataGoster(ex);
        }
    }

    private bool Filtre(T satir)
    {
        if (string.IsNullOrWhiteSpace(Arama)) return true;
        var arananlar = Arama.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var metin = string.Join(" ", typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => p.GetValue(satir) as string));
        return arananlar.All(a => metin.Contains(a, StringComparison.CurrentCultureIgnoreCase)
                                  || Kultur.Tr.CompareInfo.IndexOf(metin, a, System.Globalization.CompareOptions.IgnoreCase) >= 0);
    }

    private void OzetGuncelle()
    {
        var gorunen = Gorunum.Cast<T>().ToList();
        var ozet = OzetHesapla(gorunen);
        AltBilgi = $"{gorunen.Count} kayıt" + (ozet is null ? "" : "   •   " + ozet);
    }

    // XAML bu adlara bağlanır: public olmalı (WPF binding açık arayüz üyelerini göremez).
    public IAsyncRelayCommand YenileKomut => YenileCommand;
    public IAsyncRelayCommand YeniKomut => YeniCommand;
    public IAsyncRelayCommand DuzenleKomut => DuzenleCommand;
    public IAsyncRelayCommand SilKomut => SilCommand;
    public IAsyncRelayCommand ExcelKomut => ExcelCommand;
    public IAsyncRelayCommand PdfKomut => PdfCommand;

    protected static Eylem E(string metin, SymbolRegular ikon, Func<Task> islem) =>
        new(metin, ikon, new AsyncRelayCommand(async () =>
        {
            try { await islem(); }
            catch (Exception ex) { await Dialog.HataGoster(ex); }
        }));
}
