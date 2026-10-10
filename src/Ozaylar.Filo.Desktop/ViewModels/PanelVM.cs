using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ozaylar.Filo.Application.Guvenlik;
using Ozaylar.Filo.Application.Servisler;
using Ozaylar.Filo.Desktop.Altyapi;

namespace Ozaylar.Filo.Desktop.ViewModels;

public sealed record PanelKarti(string Baslik, string Deger, string? Alt = null, bool Uyari = false);

public sealed partial class PanelVM(PanelServisi servis, IKullaniciOturumu oturum) : ObservableObject
{
    public ObservableCollection<PanelKarti> Kartlar { get; } = [];
    public ObservableCollection<YoldakiArac> Yoldakiler { get; } = [];
    public ObservableCollection<EvrakUyari> Evraklar { get; } = [];
    public ObservableCollection<YaklasanBakim> Bakimlar { get; } = [];
    public ObservableCollection<KasaBakiyeSatir> Kasalar { get; } = [];

    [ObservableProperty] private bool _mesgul;
    [ObservableProperty] private string? _yedekUyari;
    [ObservableProperty] private string _hosgeldin = "";

    [RelayCommand]
    public async Task YenileAsync()
    {
        if (Mesgul) return;
        Mesgul = true;
        try
        {
            var o = await servis.OzetAsync();
            Hosgeldin = $"Merhaba {oturum.AdSoyad} — {DateTime.Now:dd MMMM yyyy, dddd}";
            Kartlar.Clear();
            void K(string b, string d, string? a = null, bool u = false) => Kartlar.Add(new PanelKarti(b, d, a, u));
            if (o.Kasalar is not null)
                K("Kasa + Banka (TL)", $"{o.Kasalar.Where(k => k.ParaBirimi == Domain.Common.ParaBirimi.TRY).Sum(k => k.Bakiye):N0} ₺",
                    string.Join("  ", o.Kasalar.Where(k => k.ParaBirimi != Domain.Common.ParaBirimi.TRY).GroupBy(k => k.PB).Select(g => $"{g.Sum(k => k.Bakiye):N0} {g.Key}")));
            if (o.AcikAlacak is not null) K("Açık alacak", $"{o.AcikAlacak:N0} ₺", o.VadesiGecenAlacak > 0 ? $"Vadesi geçen: {o.VadesiGecenAlacak:N0} ₺" : null, o.VadesiGecenAlacak > 0);
            if (o.AcikBorc is not null) K("Açık borç", $"{o.AcikBorc:N0} ₺", o.Odenecek30Gun > 0 ? $"30 gün içinde taksit/çek: {o.Odenecek30Gun:N0} ₺" : null);
            K("Bu ay sefer", o.BuAySeferSayisi.ToString("N0"), o.BuAyCiro is null ? null : $"Ciro: {o.BuAyCiro:N0} ₺");
            if (o.BuAyKar is not null) K("Bu ay sefer kârı", $"{o.BuAyKar:N0} ₺", "Navlun − sefer giderleri (KDV hariç)", o.BuAyKar < 0);
            K("Faturalanmamış sefer", o.FaturalanmamisSefer.ToString(), null, o.FaturalanmamisSefer > 0);
            K("Onay bekleyen harcama", o.OnayBekleyenHarcama.ToString(), "Şoför nakit harcamaları", o.OnayBekleyenHarcama > 0);
            K("Evrak uyarısı", o.Evraklar.Count.ToString(), $"{o.Evraklar.Count(e => e.SuresiDoldu)} adedinin süresi dolmuş", o.Evraklar.Any(e => e.SuresiDoldu));

            Yenile(Yoldakiler, o.Yoldakiler);
            Yenile(Evraklar, o.Evraklar);
            Yenile(Bakimlar, o.Bakimlar);
            Yenile(Kasalar, o.Kasalar ?? []);

            YedekUyari = o.SonYedek is null ? "Henüz başarılı bir otomatik yedek kaydı yok. Sunucu servisinin çalıştığını kontrol edin."
                : DateTime.UtcNow - o.SonYedek > TimeSpan.FromHours(26) ? $"Son başarılı yedek {o.SonYedek.Value.ToLocalTime():dd.MM.yyyy HH:mm} — 24 saatten eski! Sunucu servisini kontrol edin."
                : null;
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

    private static void Yenile<T>(ObservableCollection<T> hedef, IEnumerable<T> kaynak)
    {
        hedef.Clear();
        foreach (var x in kaynak) hedef.Add(x);
    }
}
