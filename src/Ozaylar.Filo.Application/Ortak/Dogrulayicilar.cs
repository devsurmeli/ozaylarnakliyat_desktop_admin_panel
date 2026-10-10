using FluentValidation;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Application.Ortak;

public static class Kontrol
{
    /// <summary>T.C. kimlik no algoritma kontrolü.</summary>
    public static bool TcknGecerli(string? tc)
    {
        if (tc is null || tc.Length != 11 || !tc.All(char.IsDigit) || tc[0] == '0') return false;
        var d = tc.Select(c => c - '0').ToArray();
        var tek = d[0] + d[2] + d[4] + d[6] + d[8];
        var cift = d[1] + d[3] + d[5] + d[7];
        var d10 = ((tek * 7 - cift) % 10 + 10) % 10;
        var d11 = (d.Take(10).Sum()) % 10;
        return d[9] == d10 && d[10] == d11;
    }

    /// <summary>Vergi kimlik no (10 hane) algoritma kontrolü.</summary>
    public static bool VknGecerli(string? vkn)
    {
        if (vkn is null || vkn.Length != 10 || !vkn.All(char.IsDigit)) return false;
        var toplam = 0;
        for (var i = 0; i < 9; i++)
        {
            var tmp = (vkn[i] - '0' + (9 - i)) % 10;
            var deger = tmp == 0 ? 0 : (tmp * (1 << (9 - i))) % 9;
            if (tmp != 0 && deger == 0) deger = 9;
            toplam += deger;
        }
        return (10 - toplam % 10) % 10 == vkn[9] - '0';
    }

    /// <summary>IBAN mod-97 kontrolü (boşluksuz).</summary>
    public static bool IbanGecerli(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return true;
        var s = iban.Replace(" ", "").ToUpperInvariant();
        if (s.Length is < 15 or > 34) return false;
        var r = s[4..] + s[..4];
        var mod = 0;
        foreach (var c in r)
        {
            var v = char.IsDigit(c) ? c - '0' : char.IsLetter(c) ? c - 'A' + 10 : -1;
            if (v < 0) return false;
            mod = v > 9 ? (mod * 100 + v) % 97 : (mod * 10 + v) % 97;
        }
        return mod == 1;
    }
}

public sealed class AracDogrulayici : AbstractValidator<Arac>
{
    public AracDogrulayici()
    {
        RuleFor(x => x.Plaka).NotEmpty().WithMessage("Plaka zorunludur.").MaximumLength(15);
        RuleFor(x => x.ModelYili).InclusiveBetween((short)1950, (short)2100).When(x => x.ModelYili is not null).WithMessage("Model yılı geçersiz.");
        RuleFor(x => x.GuncelKm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FaydaliOmurAy).GreaterThan((short)0).When(x => x.AlisBedeli is > 0)
            .WithMessage("Amortisman için alış bedeli girildiyse faydalı ömür (ay) da girilmelidir.");
        RuleFor(x => x.AlisTarihi).NotNull().When(x => x.AlisBedeli is > 0).WithMessage("Alış bedeli girildiyse alış tarihi zorunludur.");
        RuleFor(x => x.BeklenenTuketim).InclusiveBetween(5m, 80m).When(x => x.BeklenenTuketim is not null).WithMessage("Beklenen tüketim 5-80 L/100km arasında olmalıdır.");
    }
}

public sealed class DorseDogrulayici : AbstractValidator<Dorse>
{
    public DorseDogrulayici()
    {
        RuleFor(x => x.Plaka).NotEmpty().WithMessage("Plaka zorunludur.").MaximumLength(15);
        RuleFor(x => x.FaydaliOmurAy).GreaterThan((short)0).When(x => x.AlisBedeli is > 0)
            .WithMessage("Alış bedeli girildiyse faydalı ömür (ay) da girilmelidir.");
        RuleFor(x => x.AlisTarihi).NotNull().When(x => x.AlisBedeli is > 0).WithMessage("Alış bedeli girildiyse alış tarihi zorunludur.");
    }
}

public sealed class SoforDogrulayici : AbstractValidator<Sofor>
{
    public SoforDogrulayici()
    {
        RuleFor(x => x.TCKN).Must(Kontrol.TcknGecerli).WithMessage("T.C. kimlik numarası geçersiz.");
        RuleFor(x => x.Ad).NotEmpty().WithMessage("Ad zorunludur.").MaximumLength(50);
        RuleFor(x => x.Soyad).NotEmpty().WithMessage("Soyad zorunludur.").MaximumLength(50);
        RuleFor(x => x.IBAN).Must(Kontrol.IbanGecerli).WithMessage("IBAN geçersiz.");
        RuleFor(x => x.IstenCikis).GreaterThanOrEqualTo(x => x.IseGiris).When(x => x.IstenCikis is not null && x.IseGiris is not null)
            .WithMessage("İşten çıkış tarihi işe giriş tarihinden önce olamaz.");
    }
}

public sealed class CariDogrulayici : AbstractValidator<Cari>
{
    public CariDogrulayici()
    {
        RuleFor(x => x.Unvan).NotEmpty().WithMessage("Unvan zorunludur.").MaximumLength(200);
        RuleFor(x => x.CariTipi).NotEqual((Domain.Common.CariTipi)0).WithMessage("En az bir cari tipi seçilmelidir.");
        RuleFor(x => x.VergiNo)
            .Must(v => v!.Length == 10 ? Kontrol.VknGecerli(v) : Kontrol.TcknGecerli(v))
            .When(x => !string.IsNullOrEmpty(x.VergiNo) && (x.UlkeId is null or 1))
            .WithMessage("Vergi no (10 hane VKN) veya T.C. kimlik no (11 hane) geçersiz.");
        RuleFor(x => x.IBAN).Must(Kontrol.IbanGecerli).WithMessage("IBAN geçersiz.");
        RuleFor(x => x.VadeGun).InclusiveBetween((short)0, (short)365);
        RuleFor(x => x.Eposta).EmailAddress().When(x => !string.IsNullOrEmpty(x.Eposta)).WithMessage("E-posta geçersiz.");
    }
}

public sealed class LokasyonDogrulayici : AbstractValidator<Lokasyon>
{
    public LokasyonDogrulayici()
    {
        RuleFor(x => x.Ad).NotEmpty().WithMessage("Lokasyon adı zorunludur.");
        RuleFor(x => x.UlkeId).GreaterThan((short)0).WithMessage("Ülke seçilmelidir.");
    }
}

public sealed class GuzergahDogrulayici : AbstractValidator<Guzergah>
{
    public GuzergahDogrulayici()
    {
        RuleFor(x => x.Ad).NotEmpty().WithMessage("Güzergah adı zorunludur.");
        RuleFor(x => x.CikisLokasyonId).GreaterThan(0).WithMessage("Çıkış lokasyonu seçilmelidir.");
        RuleFor(x => x.VarisLokasyonId).GreaterThan(0).WithMessage("Varış lokasyonu seçilmelidir.");
        RuleFor(x => x.StandartKm).GreaterThan(0).WithMessage("Standart km sıfırdan büyük olmalıdır.");
    }
}

public sealed class KasaHesapDogrulayici : AbstractValidator<KasaHesap>
{
    public KasaHesapDogrulayici()
    {
        RuleFor(x => x.Ad).NotEmpty().WithMessage("Hesap adı zorunludur.");
        RuleFor(x => x.IBAN).Must(Kontrol.IbanGecerli).WithMessage("IBAN geçersiz.");
    }
}

public sealed class EvrakDogrulayici : AbstractValidator<Evrak>
{
    public EvrakDogrulayici()
    {
        RuleFor(x => x.EvrakTuruId).GreaterThan((short)0).WithMessage("Evrak türü seçilmelidir.");
        RuleFor(x => x.BitisTarihi).GreaterThanOrEqualTo(x => x.DuzenlenmeTarihi)
            .When(x => x.BitisTarihi is not null && x.DuzenlenmeTarihi is not null).WithMessage("Bitiş tarihi düzenlenme tarihinden önce olamaz.");
        RuleFor(x => x).Must(x => new object?[] { x.AracId, x.DorseId, x.SoforId }.Count(v => v is not null) <= 1)
            .WithMessage("Evrak yalnızca bir araç, dorse veya şoföre bağlanabilir.");
    }
}

public sealed class MasrafTuruDogrulayici : AbstractValidator<MasrafTuru>
{
    public MasrafTuruDogrulayici()
    {
        RuleFor(x => x.Ad).NotEmpty().WithMessage("Masraf türü adı zorunludur.");
        RuleFor(x => x.VarsayilanKdv).LessThanOrEqualTo((byte)100);
    }
}

public sealed class EvrakTuruDogrulayici : AbstractValidator<EvrakTuru>
{
    public EvrakTuruDogrulayici()
    {
        RuleFor(x => x.Ad).NotEmpty().WithMessage("Evrak türü adı zorunludur.");
        RuleFor(x => x.HedefTip).NotEqual((Domain.Common.EvrakHedef)0).WithMessage("Evrakın kime ait olduğu seçilmelidir.");
    }
}

public sealed class HarcirahTarifeDogrulayici : AbstractValidator<HarcirahTarife>
{
    public HarcirahTarifeDogrulayici()
    {
        RuleFor(x => x.GunlukTutar).GreaterThanOrEqualTo(0);
    }
}

public sealed class BakimPlaniDogrulayici : AbstractValidator<BakimPlani>
{
    public BakimPlaniDogrulayici()
    {
        RuleFor(x => x.BakimAdi).NotEmpty().WithMessage("Bakım adı zorunludur.");
        RuleFor(x => x).Must(x => (x.AracId is null) != (x.DorseId is null)).WithMessage("Bakım planı bir araç veya dorseye bağlanmalıdır.");
        RuleFor(x => x).Must(x => x.PeriyotKm is > 0 || x.PeriyotGun is > 0).WithMessage("Km veya gün periyodundan en az biri girilmelidir.");
    }
}

public sealed class LastikDogrulayici : AbstractValidator<Lastik>
{
    public LastikDogrulayici()
    {
        RuleFor(x => x.SeriNo).NotEmpty().WithMessage("Seri no zorunludur.");
        RuleFor(x => x.Ebat).NotEmpty().WithMessage("Ebat zorunludur.");
    }
}
