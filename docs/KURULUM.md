# Ozaylar Filo — Kurulum ve İşletim Kılavuzu

## 1. Sunucu bilgisayar seçimi
Sunucu, programın veritabanını tutan ve **sürekli açık** kalacak bilgisayardır.

- Windows 10/11 Pro (veya Windows Server), SSD, en az 8 GB RAM.
- **Sabit IP:** modemde bu bilgisayara DHCP rezervasyonu yapın.
- **UPS** önerilir (elektrik kesintisinde veritabanı bozulmasın).
- Uyku/hazırda bekleme kapalı (kurulum betiği ayarlar). Windows Update "etkin saatler" iş saatlerini kapsasın.
- Ağ profili **Özel** olmalı (güvenlik duvarı kuralı yalnızca özel/etki alanı ağında açılır).

## 2. Sunucu kurulumu
1. SQL Server 2025 Express kurulum dosyasını (Microsoft'tan, `SQLEXPR_x64_*.exe`) `installer\prereq\` klasörüne koyup paketi üretin
   (`build\paketle.ps1 -Surum 1.0.0`) **veya** SQL Express'i önceden elle kurun:
   - Örnek adı `SQLEXPRESS`, **karma kimlik doğrulama** (SQL Server and Windows Authentication), collation `Turkish_100_CI_AS`.
2. `OzaylarFiloSunucuKurulum-x.y.z.exe`'yi **yönetici olarak** çalıştırın. Kurulum şunları yapar:
   - SQL Express (yoksa) + TCP 1433 sabit port + karma mod
   - Güvenlik duvarı kuralı (yalnızca özel ağ)
   - `C:\OzaylarFilo\Yedek` klasörü (SQL servis hesabına yazma izni)
   - `\\SUNUCU\OzaylarFiloGuncelleme` paylaşımı (istemci güncellemeleri)
   - Veritabanları (`OzaylarFilo`, `OzaylarFilo_Ekler`), başlangıç verileri, istemci login'i (`filo_app`)
   - **Ozaylar Filo Sunucu** Windows servisi (otomatik başlar, hata olursa yeniden başlar)
3. İstemci bağlantı bilgisi şu dosyaya yazılır: `C:\ProgramData\OzaylarFilo\istemci-baglanti-bilgisi.txt` (**gizli tutun**).

### Yedekleme ayarı (önemli)
`C:\Program Files\OzaylarFilo\Sunucu\appsettings.json` → `Yedek` bölümü:

| Alan | Açıklama |
|---|---|
| `YerelKlasor` | Yerel yedek klasörü (varsayılan `C:\OzaylarFilo\Yedek`) |
| `IkinciHedef` | **Mutlaka doldurun:** NAS paylaşımı, harici disk veya OneDrive/Google Drive klasörü |
| `SifrelemeParolasi` | İkinci kopya AES-256 ile şifrelenir. **Parolayı kaybetmeyin**; kaybedilirse yedek açılamaz |
| `TamYedekSaati` | Gece tam yedek saati (varsayılan 02:00) |
| `FarkYedekAraligiSaat` | Gün içi fark yedeği aralığı (08:00–20:00 arası, varsayılan 2 saat) |
| `GunlukSaklamaGun` / `AylikSaklamaAy` | 14 gün tüm yedekler + 12 ay boyunca her ayın ilk tam yedeği |

Değişiklikten sonra servisi yeniden başlatın: `Restart-Service OzaylarFiloSunucu`.
Programın **Panel** ekranı son başarılı yedek 24 saatten eskiyse uyarı gösterir.

Elle yedek: `"C:\Program Files\OzaylarFilo\Sunucu\Ozaylar.Filo.Sunucu.exe" yedek`

### Yedekten geri dönüş
1. Şifreli kopyayı çözün: `Ozaylar.Filo.Sunucu.exe coz <dosya.bak.ozyd> <hedef.bak> <parola>`
2. Servisi durdurun: `Stop-Service OzaylarFiloSunucu`
3. SQL Server Management Studio → Restore Database: önce son **TAM** yedek (`WITH NORECOVERY`), sonra aynı günün son **FARK** yedeği (`WITH RECOVERY`). `OzaylarFilo` ve `OzaylarFilo_Ekler` için ayrı ayrı.
4. Servisi başlatın. **Ayda bir** bu işlemi deneme veritabanına yaparak yedeklerin açıldığını doğrulayın.

## 3. İstemci kurulumu (diğer bilgisayarlar)
1. `\\SUNUCU\OzaylarFiloGuncelleme\OzaylarFilo-win-Setup.exe`'yi çalıştırın (yönetici gerekmez).
2. İlk açılışta **Sunucu Bağlantısı** penceresine `istemci-baglanti-bilgisi.txt`'deki bilgileri girin:
   sunucu adresi (`SUNUCU\SQLEXPRESS`), kullanıcı `filo_app` ve şifre; güncelleme klasörü `\\SUNUCU\OzaylarFiloGuncelleme`.
3. İlk giriş: `admin` / `admin` → şifre değiştirme zorunludur. Ardından **Yönetim > Kullanıcılar**'dan her çalışana kendi kullanıcısını açın.

## 4. Güncelleme yayınlama
1. Geliştirme bilgisayarında: `build\paketle.ps1 -Surum 1.0.1`
2. `artifacts\istemci` içeriğini `\\SUNUCU\OzaylarFiloGuncelleme` klasörüne kopyalayın → istemciler açılışta güncellemeyi indirir ve yeniden başlatmayı önerir.
3. Veritabanı değişikliği içeren sürümde önce sunucu paketini kurun: servis açılışta **otomatik yedek alıp** migration'ı uygular.
   Eski sürümdeki istemciler "programı güncelleyin" uyarısıyla güncellemeye yönlendirilir.

## 5. Windows "Akıllı Uygulama Denetimi" (Smart App Control)
Windows 11'de Smart App Control açıksa **imzasız** program dosyaları engellenebilir
(olay günlüğünde "Uygulama Denetimi ilkesi bu dosyayı engelledi").
- Üretimde **kod imzalama sertifikası** (OV/EV) alıp paketleri imzalayın (`paketle.ps1` içinde `--signParams` satırı).
- Smart App Control'ü kapatmak geri alınamaz (yeniden açmak için Windows sıfırlama gerekir); bu kararı BT sorumlusu vermelidir.

## 6. Lisanslar
- SQL Server 2025 Express: ücretsiz (veritabanı başına 50 GB, 1 soket/4 çekirdek, 1410 MB bellek) — 20 araç için fazlasıyla yeterli.
- WPF-UI, CommunityToolkit.Mvvm, ClosedXML, Velopack, Serilog: MIT.
- **QuestPDF (PDF raporlar):** Community lisansı yıllık brüt cirosu **1 milyon USD altındaki** firmalar içindir. Ciro bu sınırı aşıyorsa Professional lisans satın alın
  (`src/Ozaylar.Filo.Infrastructure/Disa/DisaAktarici.cs` içindeki `LicenseType` güncellenir).

## 7. Mali müşavirle teyit edilecekler
Programda parametre olarak tutulur; kesin değerleri mali müşavirinizle doğrulayın:
- Yük taşımacılığında KDV tevkifatı oranı (varsayılan **2/10**) ve hangi müşterilere uygulanacağı
- Yurtiçi/yurtdışı harcırah tutarları ve gelir vergisi/SGK istisna sınırları (**Tanımlar > Harcırah Tarifeleri**)
- K yetki belgesi geçerlilik süreleri, MTV dönemleri (**Tanımlar > Evrak Türleri**)
- U-ETDS bildiriminin şu an nasıl yapıldığı (Faz 3'te programa eklenebilir)
- Dönem kapanışı: muhasebe dönemi kapandığında **Ayarlar > Dönem kapanış tarihi** girilerek geçmiş kayıtlar kilitlenir
