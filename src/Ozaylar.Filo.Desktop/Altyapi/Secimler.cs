using Microsoft.EntityFrameworkCore;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Domain.Common;

namespace Ozaylar.Filo.Desktop.Altyapi;

public sealed record GrupluSecim(int Id, string Ad, string Grup)
{
    public override string ToString() => Ad;
}

/// <summary>Form ve filtrelerdeki açılır listeler için hafif sorgular (yalnızca Id + görünen ad).</summary>
public sealed class Secimler(IFiloDbContextFactory factory)
{
    private async Task<List<Secim>> Q(Func<IFiloDbContext, IQueryable<Secim>> sorgu)
    {
        await using var db = factory.Olustur();
        return await sorgu(db).ToListAsync();
    }

    public Task<List<Secim>> AraclarAsync(bool sadeceAktif = true) =>
        Q(db => db.Araclar.Where(a => !sadeceAktif || a.Aktif).OrderBy(a => a.Plaka).Select(a => new Secim(a.Id, a.Plaka + (a.Marka != null ? " - " + a.Marka : ""))));

    public Task<List<Secim>> DorselerAsync(bool sadeceAktif = true) =>
        Q(db => db.Dorseler.Where(a => !sadeceAktif || a.Aktif).OrderBy(a => a.Plaka).Select(a => new Secim(a.Id, a.Plaka)));

    public Task<List<Secim>> SoforlerAsync(bool sadeceAktif = true) =>
        Q(db => db.Soforler.Where(s => !sadeceAktif || s.Aktif).OrderBy(s => s.Ad).ThenBy(s => s.Soyad).Select(s => new Secim(s.Id, s.Ad + " " + s.Soyad)));

    public Task<List<Secim>> CarilerAsync(CariTipi? tip = null) =>
        Q(db => db.Cariler.Where(c => c.Aktif && (tip == null || (c.CariTipi & tip.Value) != 0)).OrderBy(c => c.Unvan).Select(c => new Secim(c.Id, c.Unvan)));

    public Task<List<Secim>> LokasyonlarAsync() =>
        Q(db => db.Lokasyonlar.Where(l => l.Aktif).OrderBy(l => l.Ad)
            .Select(l => new Secim(l.Id, l.Ad + (l.Il != null ? " (" + l.Il.Ad + ")" : l.Ulke!.Kod != "TR" ? " (" + l.Ulke.Ad + ")" : ""))));

    public Task<List<Secim>> GuzergahlarAsync() =>
        Q(db => db.Guzergahlar.Where(g => g.Aktif).OrderBy(g => g.Ad).Select(g => new Secim(g.Id, g.Ad)));

    public Task<List<Secim>> UlkelerAsync() => Q(db => db.Ulkeler.OrderBy(u => u.Id == 1 ? 0 : 1).ThenBy(u => u.Ad).Select(u => new Secim(u.Id, u.Ad)));
    public Task<List<Secim>> IllerAsync() => Q(db => db.Iller.OrderBy(i => i.Ad).Select(i => new Secim(i.Id, i.Ad)));

    public Task<List<Secim>> MasrafTurleriAsync(bool sadeceSofor = false) =>
        Q(db => db.MasrafTurleri.Where(m => m.Aktif && (!sadeceSofor || m.SoforHarcamasi)).OrderBy(m => m.Ad).Select(m => new Secim(m.Id, m.Ad)));

    public Task<List<Secim>> EvrakTurleriAsync() => Q(db => db.EvrakTurleri.Where(e => e.Aktif).OrderBy(e => e.Ad).Select(e => new Secim(e.Id, e.Ad)));

    public Task<List<Secim>> KasalarAsync(ParaBirimi? pb = null) =>
        Q(db => db.KasaHesaplari.Where(k => k.Aktif && (pb == null || k.ParaBirimi == pb)).OrderBy(k => k.HesapTipi).ThenBy(k => k.Ad)
            .Select(k => new Secim(k.Id, k.Ad + (k.ParaBirimi != ParaBirimi.TRY ? " (" + k.ParaBirimi.ToString() + ")" : ""))));

    /// <summary>Açık (kapatılmamış) seferler + son 60 günün seferleri.</summary>
    public Task<List<Secim>> SeferlerAsync(short? aracId = null)
    {
        var sinir = DateOnly.FromDateTime(DateTime.Today.AddDays(-60));
        return Q(db => db.Seferler.Where(s => s.Durum != SeferDurum.Iptal && (s.Durum < SeferDurum.Kapatildi || s.PlanlananTarih >= sinir))
            .Where(s => aracId == null || s.AracId == aracId)
            .OrderByDescending(s => s.PlanlananTarih)
            .Select(s => new Secim(s.Id, s.SeferNo + " " + (s.Arac != null ? s.Arac.Plaka : s.TaseronPlaka) + " " + s.CikisLokasyon!.Ad + "→" + s.VarisLokasyon!.Ad)));
    }

    public Task<List<Secim>> RollerAsync() => Q(db => db.Roller.OrderBy(r => r.Id).Select(r => new Secim(r.Id, r.Ad)));

    public async Task<List<GrupluSecim>> IzinlerAsync()
    {
        await using var db = factory.Olustur();
        return await db.Izinler.OrderBy(i => i.Grup).ThenBy(i => i.Kod).Select(i => new GrupluSecim(i.Id, i.Ad, i.Grup)).ToListAsync();
    }
}
