using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Application.Ortak;
using Ozaylar.Filo.Desktop.Pencereler;

namespace Ozaylar.Filo.Desktop.Altyapi;

/// <summary>
/// Ana kayıt (CRUD) listeleri: satır tipi (liste görünümü) ile düzenlenen kayıt tipi ayrı olabilir.
/// Form alanlarını tanımlamak yeterlidir; yeni/düzenle/sil ve eşzamanlılık otomatik.
/// </summary>
public abstract class KayitListeVM<TSatir, T>(IDisaAktarici disa, IKayitServisi<T> servis, Secimler secimler) : ListeVM<TSatir>(disa)
    where TSatir : class where T : class, new()
{
    protected IKayitServisi<T> Servis { get; } = servis;
    protected Secimler Secimler { get; } = secimler;

    public override bool YeniVar => Servis.YazmaYetkisi;
    public override bool DuzenleVar => true;
    public override bool SilVar => Servis.YazmaYetkisi;

    protected abstract object Id(TSatir satir);
    protected abstract string KayitAdi { get; }
    protected abstract Task<IReadOnlyList<FormAlani>> AlanlarAsync(T model, bool yeni);
    protected virtual T YeniModel() => new();
    protected virtual double FormGenislik => 0;

    protected override async Task YeniAsync()
    {
        var m = YeniModel();
        if (await FormAcAsync(m, true)) await YenileAsync();
    }

    protected override async Task DuzenleAsync(TSatir satir)
    {
        var m = await Servis.GetirAsync(Id(satir));
        if (m is null)
        {
            await Dialog.Hata("Kayıt bulunamadı; silinmiş olabilir.");
            await YenileAsync();
            return;
        }
        if (await FormAcAsync(m, false)) await YenileAsync();
    }

    protected virtual async Task<bool> FormAcAsync(T m, bool yeni)
    {
        var alanlar = await AlanlarAsync(m, yeni);
        if (!Servis.YazmaYetkisi)
        {
            new FormPenceresi(KayitAdi, m, alanlar, _ => Task.CompletedTask, salt: true, genislik: FormGenislik).ShowDialog();
            return false;
        }
        var ok = FormPenceresi.Ac(yeni ? $"Yeni {KayitAdi}" : $"{KayitAdi} Düzenle", m, alanlar, async _ => await KaydetAsync(m), FormGenislik);
        if (ok) AnaPencere.Basarili($"{KayitAdi} kaydedildi.");
        return ok;
    }

    protected virtual Task KaydetAsync(T m) => Servis.KaydetAsync(m);

    protected override async Task SilAsync(TSatir satir)
    {
        if (!await Dialog.Onay($"Seçili {KayitAdi.ToLower(Kultur.Tr)} silinsin mi?", "Silme onayı", "Sil")) return;
        await Servis.SilAsync(Id(satir));
        AnaPencere.Basarili($"{KayitAdi} silindi.");
        await YenileAsync();
    }
}

/// <summary>Satır tipi kayıt tipiyle aynı olan basit listeler.</summary>
public abstract class BasitKayitListeVM<T>(IDisaAktarici disa, IKayitServisi<T> servis, Secimler secimler)
    : KayitListeVM<T, T>(disa, servis, secimler) where T : class, new()
{
    protected virtual IQueryable<T> Sorgu(IQueryable<T> q) => q;
    protected override async Task<IEnumerable<T>> YukleAsync() => await Servis.ListeAsync(Sorgu);
}
