namespace Ozaylar.Filo.Application.Abstractions;

public interface ISifreHasher
{
    string Hashle(string sifre);
    /// <returns>Doğru mu, ve hash yeniden üretilmeli mi (algoritma güncellendiyse).</returns>
    (bool Dogru, bool YenidenHashle) Dogrula(string hash, string sifre);
}
