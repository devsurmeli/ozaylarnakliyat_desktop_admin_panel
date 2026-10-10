# Ozaylar Filo — Nakliye Filo ve Sefer Yönetimi

Yaklaşık 20 araçlık (çekici/kamyon + dorse), yurtiçi ve yurtdışı sefer yapan nakliye firması için masaüstü uygulaması.
Tek bilgisayara (sunucu) kurulur; aynı ağdaki diğer bilgisayarlar programı kendi üzerlerinde çalıştırıp ortak veritabanını kullanır.

## Modüller

| Modül | İçerik |
|---|---|
| Sefer | Sefer kartı, durum akışı (Planlandı → Yolda → Teslim → Kapatıldı → Faturalandı), çakışma kontrolü (araç/dorse/şoför), km sapma uyarısı, süresi dolmuş evrak engeli, taşeron seferi, yurtdışı/döviz navlun, ara duraklar, sefer kârlılığı |
| Şoför hesabı | Avans (kasadan), şoför nakit harcamaları + onay, harcırah (ülke tarifesi), mahsup (iade/ödeme/devir), şoför ekstresi |
| Gider | Masraf (maliyet merkezi: sefer/araç/dorse/genel), yakıt (L/100km, sapma alarmı), HGS/OGS, trafik cezası (+şoföre rücu), akaryakıt/HGS Excel ekstre aktarımı |
| Finans | Cari (borç/alacak, ekstre, FIFO yaşlandırma, risk limiti), kasa/banka (TL+döviz, virman), fatura kaydı (KDV + 2/10 tevkifat; fatura **kesilmez**, takip edilir), çek/senet portföyü, kredi/leasing taksit planı |
| Filo | Araç, dorse, çekici-dorse eşleşme geçmişi, şoför, evrak/muayene/sigorta hatırlatma, bakım planı (km/gün), bakım/onarım (parça/işçilik), lastik takibi |
| Rapor | Sefer kârlılığı, araç kârlılığı + km başı maliyet (amortisman ve kredi faizi dahil), aylık gelir-gider, şoför performansı, nakit akış tahmini, yakıt tüketimi, yaşlandırma, faturalanmamış seferler — Excel/PDF |
| Yönetim | Kullanıcı, rol/izin (ör. operasyon navlunu göremez), dönem kilidi, denetim kaydı (kim, ne zaman, neyi değiştirdi) |

## Mimari

```
[WPF istemci] ─┐
[WPF istemci] ─┼── TCP 1433 (şifreli) ──► SQL Server 2025 Express  (sunucu bilgisayar)
[WPF istemci] ─┘                            Ozaylar.Filo.Sunucu (Windows servisi: migration, yedek, TCMB kurları)
```

- **2 katman:** istemciler doğrudan SQL Server'a bağlanır. İş kuralları ortak `Application` katmanındadır (her istemci aynı DLL).
- **Güvenlik:** istemciler yalnızca okuma/yazma yetkili `filo_app` SQL login'iyle bağlanır (DDL yok); bağlantı bilgisi istemcide DPAPI ile şifreli. Uygulama kullanıcıları ayrı (PBKDF2, 5 hatalı girişte 15 dk kilit), rol/izin sunucu tarafında da denetlenir.
- **Şema:** EF Core 10 Code-First. Migration'ları yalnızca sunucu servisi uygular (önce otomatik yedek); istemci açılışta şema sürümünü kontrol eder.
- **Eşzamanlılık:** her güncellenebilir tabloda `rowversion`; aynı kaydı iki kişi değiştirirse ikincisi uyarılır.

### Veritabanı tasarımı (az yer kaplama)
- `Turkish_100_CI_AS_SC_UTF8` collation + **`varchar`** (hiç `nvarchar` yok — testle doğrulanır), Türkçe İ/ı arama doğru çalışır.
- Küçük anahtarlar (`tinyint`/`smallint`/`int`, GUID yok), `date` / `datetime2(0)`, `decimal(18,2)`.
- Hareket tablolarında **PAGE**, diğerlerinde **ROW** sıkıştırma (`Sikistirma.Uygula`).
- Tek **cari defteri** ve tek **kasa defteri**: her belge (fatura, avans, masraf…) defterlere satır üretir; bakiye saklanmaz, hesaplanır. Defter satırları değişmez, iptal **ters kayıtla** yapılır. Dönem kilidi.
- Dosya ekleri ayrı veritabanında (`OzaylarFilo_Ekler`), görseller yüklemeden önce küçültülür.

## Proje yapısı

```
src/
  Ozaylar.Filo.Domain           Varlıklar, enum'lar, hesaplamalar (tevkifat, harcırah, L/100km, amortisman, taksit, yaşlandırma)
  Ozaylar.Filo.Application      İş kuralları (servisler), izinler, doğrulayıcılar
  Ozaylar.Filo.Infrastructure   EF Core DbContext + migration'lar, Excel/PDF, yedekleme, TCMB, DPAPI bağlantı deposu
  Ozaylar.Filo.Desktop          WPF (WPF-UI Fluent) istemci
  Ozaylar.Filo.Sunucu           Windows servisi + kurulum komutları
tests/                          Birim + gerçek SQL Server üzerinde entegrasyon testleri
installer/                      Sunucu hazırlık betiği, Inno Setup
build/paketle.ps1               Yayın paketleri (Velopack istemci + sunucu)
docs/KURULUM.md                 Kurulum, yedekten dönüş, güncelleme
```

## Geliştirme

Gereksinimler: .NET 10 SDK, yerel SQL Server (Windows kimlik doğrulaması).

```powershell
dotnet build Ozaylar.Filo.slnx
dotnet test  Ozaylar.Filo.slnx          # entegrasyon testleri geçici bir veritabanı oluşturup siler (OZAYLAR_TEST_SQL ile sunucu değiştirilebilir)

# Yerel deneme veritabanı + örnek veri
dotnet run --project src/Ozaylar.Filo.Sunucu -- kur --sql localhost --windows --veritabani OzaylarFiloDeneme
dotnet run --project src/Ozaylar.Filo.Sunucu -- ornek-veri
dotnet run --project src/Ozaylar.Filo.Desktop        # ilk giriş: admin / admin (şifre değiştirme zorunlu)

# Model değişikliğinden sonra
dotnet ef migrations add <Ad> --project src/Ozaylar.Filo.Infrastructure --context FiloDbContext --output-dir Veri/Migrations
#   yeni tablo eklendiyse migration'ın Up() sonuna: Sikistirma.Uygula(migrationBuilder);
```

## Bilinen sınırlar / sonraki fazlar
- **Faz 2:** e-Fatura/e-Arşiv entegratörü, muhasebe programına aktarım, banka ekstresi eşleştirme, hasar/kaza, e-posta hatırlatma, genel giderlerin araçlara dağıtımı.
- **Faz 3:** U-ETDS bildirimi, GPS entegrasyonu, şoför mobil uygulaması.
- Çek/senet şimdilik yalnızca TL.
