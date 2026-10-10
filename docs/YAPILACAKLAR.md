# Yapılacaklar / Bilinen Eksikler

Yapılanlar işaretlenir; yeni eksikler alta eklenir.

## Kullanım kolaylığı
- [x] Formlarda **"Kaydet ve yeni"** (Ctrl+Shift+S) — masraf, yakıt, şoföre para ver/al, hakediş satırı ve tüm tanım listeleri.
- [x] Yeni kayıt formlarında son seçilen değerleri hatırlama (kasa, araç, şoför, masraf türü...; kullanıcı başına).
- [x] Panel ve Finansman Özeti kartlarına tıklayınca ilgili ekran; kredi detayında "Sonraki taksit / Gecikmiş" kartı taksit ödemeyi açar.
- [x] Grafikten detaya inme: araç kârı çubuğu → araç detayı, banka pastası → finansman özeti.
- [x] Detay sayfalarında konum çubuğu (ör. *Finansman Özeti › Akbank AK-TK-2025-0415*), önceki sayfaya tıklayınca geri.
- [x] Uyarı rozeti 5 dakikada bir kendiliğinden yenilenir.
- [x] İçerik alanındaki ince çerçeve çizgisi kaldırıldı (kendi sayfa çerçevesi + geçiş animasyonu).

## Kredi
- [x] Banka planı Excel içe aktarmada başlık satırı otomatik bulunur (ilk 25 satır).
- [x] "Plan Şablonu (Excel)": açık plan düzenlenebilir Excel olarak verilir, bankanın rakamlarıyla düzeltilip geri yüklenir.
- [x] Değişken faiz: "Faiz Oranı Değiştir" — kalan anapara yeni oranla kalan vadede yeniden planlanır.
- [x] v1'den aktarılan kredi ödemesi iptal edilince eski kasa kaydı da ters çevrilir, anapara borcu bankaya geri yazılır.
- [ ] Gerçek banka dosyalarıyla (Ziraat, İş, Akbank, Garanti...) deneme — örnek dosya bekleniyor.

## Performans
- [x] Araç detay özeti 12 aylık trendi eşzamanlı hesaplar.

## Örnek veri
- [x] Deneme veritabanında 12 aya yayılmış 66 sefer (trend grafikleri gerçekçi).

## Ortam
- [ ] Smart App Control (Windows 11) yeni derlenen imzasız dosyaları zaman zaman engelliyor. Kalıcı çözüm:
  ya Smart App Control'ü kapatmak (Windows Güvenliği › Uygulama ve tarayıcı denetimi) ya da kod imzalama sertifikası.
