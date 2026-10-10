using Ozaylar.Filo.Desktop.ViewModels;

namespace Ozaylar.Filo.Desktop.Sayfalar;

// Navigasyon sayfa tipiyle çalıştığı için her liste ekranı ortak ListeSayfasi'ndan türetilmiş ayrı bir tiptir.
public sealed class SeferlerSayfasi(SeferListeVM vm) : ListeSayfasi(vm);
public sealed class MasraflarSayfasi(MasrafListeVM vm) : ListeSayfasi(vm);
public sealed class YakitSayfasi(YakitListeVM vm) : ListeSayfasi(vm);
public sealed class HgsSayfasi(HgsListeVM vm) : ListeSayfasi(vm);
public sealed class CezalarSayfasi(CezaListeVM vm) : ListeSayfasi(vm);
public sealed class AvanslarSayfasi(AvansListeVM vm) : ListeSayfasi(vm);
public sealed class HarcirahlarSayfasi(HarcirahListeVM vm) : ListeSayfasi(vm);
public sealed class MahsuplarSayfasi(MahsupListeVM vm) : ListeSayfasi(vm);
public sealed class SoforGunlukSayfasi(SoforGunlukVM vm) : ListeSayfasi(vm);
public sealed class HakedisSayfasi(HakedisListeVM vm) : ListeSayfasi(vm);
public sealed class ZimmetlerSayfasi(ZimmetListeVM vm) : ListeSayfasi(vm);
public sealed class CarilerSayfasi(CariListeVM vm) : ListeSayfasi(vm);
public sealed class KasaSayfasi(KasaListeVM vm) : ListeSayfasi(vm);
public sealed class FaturalarSayfasi(FaturaListeVM vm) : ListeSayfasi(vm);
public sealed class CekSenetSayfasi(CekSenetListeVM vm) : ListeSayfasi(vm);
public sealed class KredilerSayfasi(KrediListeVM vm) : ListeSayfasi(vm);
public sealed class TaksitlerSayfasi(KrediTakvimVM vm) : ListeSayfasi(vm);
public sealed class AraclarSayfasi(AracListeVM vm) : ListeSayfasi(vm);
public sealed class DorselerSayfasi(DorseListeVM vm) : ListeSayfasi(vm);
public sealed class SoforlerSayfasi(SoforListeVM vm) : ListeSayfasi(vm);
public sealed class EvraklarSayfasi(EvrakListeVM vm) : ListeSayfasi(vm);
public sealed class BakimlarSayfasi(BakimListeVM vm) : ListeSayfasi(vm);
public sealed class BakimPlanlariSayfasi(BakimPlaniListeVM vm) : ListeSayfasi(vm);
public sealed class LastiklerSayfasi(LastikListeVM vm) : ListeSayfasi(vm);
public sealed class LokasyonlarSayfasi(LokasyonListeVM vm) : ListeSayfasi(vm);
public sealed class GuzergahlarSayfasi(GuzergahListeVM vm) : ListeSayfasi(vm);
public sealed class MasrafTurleriSayfasi(MasrafTuruListeVM vm) : ListeSayfasi(vm);
public sealed class EvrakTurleriSayfasi(EvrakTuruListeVM vm) : ListeSayfasi(vm);
public sealed class HarcirahTarifeleriSayfasi(HarcirahTarifeListeVM vm) : ListeSayfasi(vm);
public sealed class SablonlarSayfasi(SablonListeVM vm) : ListeSayfasi(vm);
public sealed class KullanicilarSayfasi(KullaniciListeVM vm) : ListeSayfasi(vm);
public sealed class RollerSayfasi(RolListeVM vm) : ListeSayfasi(vm);
public sealed class AuditSayfasi(AuditListeVM vm) : ListeSayfasi(vm);

// 360° / pano sayfaları: ortak DetaySayfasi + menüden açılan model.
public sealed class PanelSayfasi : DetaySayfasi
{
    public PanelSayfasi(PanelVM vm) => DataContext = vm;
}

public sealed class KrediOzetSayfasi : DetaySayfasi
{
    public KrediOzetSayfasi(KrediOzetVM vm) => DataContext = vm;
}

public sealed class AyarlarMerkeziSayfasi : DetaySayfasi
{
    public AyarlarMerkeziSayfasi(AyarlarMerkeziVM vm) => DataContext = vm;
}
