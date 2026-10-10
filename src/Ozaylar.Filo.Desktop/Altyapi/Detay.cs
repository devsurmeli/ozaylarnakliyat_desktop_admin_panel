using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Desktop.Kontroller;
using Ozaylar.Filo.Desktop.Sayfalar;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Altyapi;

/// <summary>Detay sayfasının üst şeridindeki gösterge kartı. Renk: kirmizi/turuncu/yesil/null.</summary>
public sealed record Kpi(string Baslik, string Deger, string? Alt = null, string? Renk = null, double? Ilerleme = null);

/// <summary>Detay sayfasında bir sekme. İçerik ilk seçildiğinde oluşturulur (IListeVM → liste görünümü, FrameworkElement → kendisi).</summary>
public sealed partial class DetaySekme(string baslik, Func<object> olustur) : ObservableObject
{
    private object? _icerik;
    private object? _gorunum;

    public string Baslik { get; } = baslik;
    public bool Olusturuldu => _icerik is not null;
    public object Icerik => _icerik ??= olustur();

    /// <summary>Sekmede gösterilecek görsel öğe (bir kez oluşturulur, sekme değişiminde korunur).</summary>
    public object Gorunum => _gorunum ??= Icerik switch
    {
        IListeVM vm => new ListeGorunumu { DataContext = vm },
        _ => Icerik,
    };

    /// <summary>Yeniden yüklemede: liste sekmesi yenilenir, diğer içerik yeniden oluşturulur.</summary>
    public async Task YenileAsync()
    {
        if (_icerik is IListeVM vm) await vm.YenileKomut.ExecuteAsync(null);
        else if (_icerik is not null)
        {
            _icerik = null;
            _gorunum = null;
            OnPropertyChanged(nameof(Gorunum));
        }
    }

    public static DetaySekme Liste(string baslik, Func<IListeVM> vm) => new(baslik, vm);

    /// <summary>Sekme başlığının erişilebilirlik (UI Automation) adı.</summary>
    public override string ToString() => Baslik;
}

/// <summary>
/// 360° detay sayfası modeli: başlık, rozet, KPI kartları, sekmeler, eylemler.
/// Türetilen sınıf <see cref="YukleAsync"/> içinde başlık/KPI'ları doldurur; sekmeleri yapıcıda bir kez ekler.
/// </summary>
public abstract partial class DetayVM : ObservableObject
{
    [ObservableProperty] private string _baslik = "";
    [ObservableProperty] private string? _altBaslik;
    [ObservableProperty] private string? _rozet;
    [ObservableProperty] private string? _rozetRenk;
    [ObservableProperty] private bool _mesgul;
    [ObservableProperty] private DetaySekme? _seciliSekme;
    private bool _yuklendi;

    public ObservableCollection<Kpi> Kpiler { get; } = [];
    public ObservableCollection<DetaySekme> Sekmeler { get; } = [];

    /// <summary>Başlık şeridindeki eylemler (Birincil olanlar buton, diğerleri "Diğer" menüsü).</summary>
    public virtual IReadOnlyList<Eylem> Eylemler => [];
    /// <summary>"Yazdır" menüsündeki belgeler.</summary>
    public virtual IReadOnlyList<Eylem> Belgeler => [];

    private IReadOnlyList<Eylem>? _eylemOnbellek;
    private IReadOnlyList<Eylem> EylemlerSabit => _eylemOnbellek ??= Eylemler;
    public IReadOnlyList<Eylem> BirincilEylemler => EylemlerSabit.Where(e => e.Birincil).ToList();
    public IReadOnlyList<Eylem> DigerEylemler => EylemlerSabit.Where(e => !e.Birincil).ToList();

    protected abstract Task YukleAsync();

    public bool Yuklendi => _yuklendi;

    public async Task IlkYuklemeAsync()
    {
        if (_yuklendi) return;
        _yuklendi = true;
        await YenileAsync();
        SeciliSekme ??= Sekmeler.FirstOrDefault();
    }

    [RelayCommand]
    public async Task YenileAsync()
    {
        if (Mesgul) return;
        Mesgul = true;
        try
        {
            await YukleAsync();
            foreach (var s in Sekmeler.Where(s => s.Olusturuldu)) await s.YenileAsync();
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

    public IAsyncRelayCommand YenileKomut => YenileCommand;

    protected static string Para(decimal d, string pb = "₺") => $"{d:N2} {pb}";
    protected static string Para0(decimal d, string pb = "₺") => $"{d:N0} {pb}";
}

/// <summary>Detay sekmelerinde kullanılan hafif liste: kolonlar + yükleme fonksiyonu + isteğe bağlı eylemler.</summary>
public sealed class SekmeListeVM<T>(string baslik, IReadOnlyList<Kolon> kolonlar, Func<SekmeListeVM<T>, Task<IEnumerable<T>>> yukle)
    : ListeVM<T>(App.Al<IDisaAktarici>()) where T : class
{
    public override string Baslik => baslik;
    public override IReadOnlyList<Kolon> Kolonlar => kolonlar;
    public bool Tarihli { get; init; }
    public override bool TarihFiltresi => Tarihli;
    public string? Onay { get; init; }
    public override string? OnayFiltreMetni => Onay;
    public Func<T, Task>? DetayIslem { get; init; }
    public override bool DetayVar => DetayIslem is not null;
    public Func<SekmeListeVM<T>, IReadOnlyList<Eylem>>? EylemOlustur { get; init; }
    public Func<IReadOnlyList<T>, string?>? Ozet { get; init; }
    public DateOnly BasTarihi => Bas;
    public DateOnly BitTarihi => Bit;

    private IReadOnlyList<Eylem>? _eylemler;
    public override IReadOnlyList<Eylem> Eylemler => _eylemler ??= EylemOlustur?.Invoke(this) ?? [];

    protected override Task<IEnumerable<T>> YukleAsync() => yukle(this);
    protected override Task DetayAsync(T satir) => DetayIslem?.Invoke(satir) ?? Task.CompletedTask;
    protected override string? OzetHesapla(IReadOnlyList<T> gorunen) => Ozet?.Invoke(gorunen);

    /// <summary>Tarih aralığını ayarlar (yüklemeden önce).</summary>
    public SekmeListeVM<T> Aralik(DateTime bas, DateTime bit)
    {
        BasTarih = bas;
        BitTarih = bit;
        return this;
    }
}

/// <summary>Uygulama içi gezinme: detay sayfalarına git, geri dön (Alt+Sol / fare geri tuşu).</summary>
public static class Gezgin
{
    private static NavigationView? _nav;
    private static readonly List<(Type Tip, object? Veri)> _gecmis = [];
    private static (Type Tip, object? Veri)? _mevcut;
    private static bool _geriGidiyor;

    public static event Action? Degisti;
    public static bool GeriVar => _gecmis.Count > 0;

    public static void Bagla(NavigationView nav)
    {
        _nav = nav;
        nav.Navigated += (_, e) =>
        {
            var sayfa = e.Page as FrameworkElement;
            if (sayfa is null) return;
            var yeni = (sayfa.GetType(), sayfa is DetaySayfasi ? sayfa.DataContext : null);
            if (!_geriGidiyor && _mevcut is { } m && !(m.Tip == yeni.Item1 && ReferenceEquals(m.Veri, yeni.Item2)))
            {
                _gecmis.Add(m);
                if (_gecmis.Count > 30) _gecmis.RemoveAt(0);
            }
            _geriGidiyor = false;
            _mevcut = yeni;
            Degisti?.Invoke();
        };
    }

    /// <summary>Bir detay sayfası açar.</summary>
    public static void Ac(DetayVM vm)
    {
        if (_nav is null) return;
        _nav.Navigate(typeof(DetaySayfasi), vm);
    }

    public static void Git(Type sayfa) => _nav?.Navigate(sayfa);

    public static void Geri()
    {
        if (_nav is null || _gecmis.Count == 0) return;
        var (tip, veri) = _gecmis[^1];
        _gecmis.RemoveAt(_gecmis.Count - 1);
        _geriGidiyor = true;
        _nav.Navigate(tip, veri);
    }
}
