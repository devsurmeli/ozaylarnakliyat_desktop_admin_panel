using Microsoft.AspNetCore.Identity;
using Ozaylar.Filo.Application.Abstractions;
using Ozaylar.Filo.Domain.Entities;

namespace Ozaylar.Filo.Infrastructure.Guvenlik;

/// <summary>ASP.NET Core Identity PasswordHasher (PBKDF2-HMAC-SHA512, 100k iterasyon).</summary>
public sealed class SifreHasher : ISifreHasher
{
    private readonly PasswordHasher<Kullanici> _hasher = new();
    private static readonly Kullanici Bos = new();

    public string Hashle(string sifre) => _hasher.HashPassword(Bos, sifre);

    public (bool Dogru, bool YenidenHashle) Dogrula(string hash, string sifre) =>
        _hasher.VerifyHashedPassword(Bos, hash, sifre) switch
        {
            PasswordVerificationResult.Success => (true, false),
            PasswordVerificationResult.SuccessRehashNeeded => (true, true),
            _ => (false, false),
        };
}
