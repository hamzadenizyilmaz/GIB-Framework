# Değişiklik günlüğü

Bu dosya [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) biçimini ve [Anlamsal Sürümleme](https://semver.org/lang/tr/)
kurallarını izler.

## [5.5.1] - 2026-11-13

### Derleme ve yayın

- ASP.NET Core 10 LTS, SDK 10.0.404 ve Node.js 24 ile bağımsız Windows x64 yayın. JWT ve PKCS paketleri güncellendi. Panel React 19.3.1, Vite 8.3.8 ve React eklentisi 6.3.0 kullanır. QR üretimi QRCoder 1.8.1'e taşındı.
- Npm kurulumu paket manifesti veya kilit dosyası değiştiğinde ve gerekli modüller eksildiğinde yenilenir. IIS ortamı dağıtımdan alınır. Ortama özel JSON dosyaları yayın çıktısına kopyalanmaz.
- `/health` canlılık, `/health/ready` SQL şeması ve üretim yetenekleri için hazırlık sunar. Sistem ekranı hazırlık nedenini ve süreyi gösterir. OpenAPI sürümü `/api/v1` olarak sürer.

### Kimlik ve firma sınırları

- Hesap, kullanıcı yönetimi, fatura onayı, yerel imza ve GİB portal işlemleri Bearer oturumu gerektirir. Swagger işlem bazında geçerli kimlik şemalarını gösterir. API anahtarı doğrulaması etkin firmayı ve güncel azami anahtar ömrünü denetler.
- Yerel JWT oturumları kullanıcı ve güvenlik damgasıyla doğrulanır. Geliştirme tokenı açık yapılandırma gerektirir. Kullanıcı `rowversion` denetimi eşzamanlı TOTP kullanımını ve hesap güncellemelerinin birbirini ezmesini önler.
- Platform yöneticisi rolü firmasız ve tek başına atanır. Firma bağlamı etkin firmayla doğrulanır. Kullanıcı listesinde platform geneli ve firma filtresi sunulur. Yönlendirilmiş HTTP başlıkları tanımlı proxy adreslerinden kabul edilir.

### Fatura ve GİB

- Dış bağlantı oluşturma ve müşteriye gönderim için fatura oluşturma veya iletim yetkisi gerekir. Dış HTML, PDF ve XML erişiminde düzenleme durumu ve imzalı XML kanıtı yeniden denetlenir. Eski portal bağlantıları da yeni kanıt koşullarına tabidir.
- Portal taslak ve imza istekleri için kalıcı işlem işaretleri ve firma bazında SQL kilitleri eklendi. SMS isteği oturum, kullanıcı, firma, ortam ve ETTN ile ilişkilendirilir. GİB onayından sonra imzalı XML, sertifika zinciri, belge kimliği ve tutar doğrulanır. Eksik veya uyuşmayan kanıt mutabakat bekler.
- Kanıt kasası belge bazında kilit, geçici dizine yazma ve atomik taşıma kullanır. Yerel imza kesildiğinde aynı belge üzerinden işlem toparlanabilir. Gelen belge hakkında eşzamanlı kararlar arasından tek karar kaydedilir.
- UBL XSD ve şematron özetleri yapılandırmada sabitlenebilir. Resmî UBL-TR imza şemasının iç DTD'si özeti doğrulanan yerel paketten yüklenir.

### SQL, kuyruk ve entegrasyonlar

- Şema 5, firma satır güvenliğini 17 tabloya genişletir. Yükseltme eksik politikayı işlem içinde kurar. Temiz kurulum tekrar çalıştırıldığında etkin politikayı korur. Birleşik SQL dosyaları numaralı kaynaklardan üretilir.
- Olay, mesaj ve webhook kuyruğunda atomik sahiplenme, kira kimliği ve eski işçinin sonucu ezmesini engelleyen koşullar kullanılır. Olayın ürettiği alt kayıtlar benzersiz anahtarla korunur. Belirsiz teslimatlar incelemeye düşer. Olay ve giden webhook işçileri için filtreli indeksler eklendi.
- Doğrulanmamış gelen webhook gövdesi teslimat günlüğüne alınmaz. WooCommerce ping'i sipariş ilerlemesini değiştirmez. Firma webhook ve SMTP hedefleri izin listesi ve bağlantı anındaki adres denetiminden geçer. SMTP kimlik doğrulaması TLS gerektirir. DATA kabulünden sonraki QUIT zaman aşımı kabul sonucunu bozmaz.
- Netgsm bakiyesi JSON ve XML yanıtlarını işler. Giden webhook ve SMS denemelerinde `Retry-After` dikkate alınır. Trendyol yoğun zaman pencereleri bölünür. Sipariş ilerlemesi bütün sayfalar işlendiğinde kaydedilir. Mikro ihracat kayıtları otomatik fatura yerine incelemeye alınır.

### Panel

- Eski 401 yanıtı yeni oturumu silmez. Geçici ağ hatalarında oturum korunur ve yeniden deneme sunulur. Firma geçişlerinde eski istekler ekrana uygulanmaz. Yetkisiz rota 403 ekranında gösterilir.
- Filtreli boş sayfada fatura toplamı doğru hesaplanır. Özet tutarlar SQL'den gelir. Görev listeleri ayrı sayfalanır. Vergi ve firma ayarları yüklenemezse taslak oluşturma ekranı yeniden deneme sunar.
- Modal klavye dolaşımı, iç içe dialog odağı ve kapanınca odağın geri dönmesi düzeltildi. İşlem düğmeleri React Strict Mode'da tekrar kullanılabilir. İndirme adı `filename*` alanını önceliklendirir.

### Yükseltme

- Mevcut şema 4 veritabanına `database/06_Sema5.sql` uygulanır. `ALTER ANY SECURITY POLICY` yetkisi gerekir. Yerel kabulde temiz kurulum, yükseltme ve tekrar çalıştırma doğrulanmıştır.
- Canlı GİB için sunucu sertifika zinciri, onaylı UBL ve uyumlu şematron çıktısı gerekir. Gerçek sağlayıcı ve barındırma kabulü ilgili ortamlarda yapılır. Trendyol `409` yanıtının uzak kayıtla otomatik mutabakatı bu sürümün açık işidir.

## [5.2.1] - 2026-10-08

### Eklenenler

- Trendyol, Hepsiburada ve n11 için sipariş alma, otomatik faturalama ve fatura bağlantısı iletimi.
- Firma logosu ve karekod içeren PDF fatura; e-posta eki, panelden indirme ve `/f/{token}/fatura.pdf` adresi.
- Kimlik numarası bulunmayan alıcılar için `11111111111` ile düzenleme ve fatura formunda "TCKN yok" seçeneği.
- Plesk (Windows Server 2019) yayın paketi, `Demo` ortamı, tek dosyalı `appsettings.json` yapılandırması ve `GIBFramework_Plesk.sql`.
- STARTTLS, SSL ve SMTP AUTH destekli e-posta altyapısı; firma markalı HTML şablonlar, PDF ve UBL-TR XML ekleri.
- Netgsm REST v2 ile SMS gönderimi, gönderici başlığı listesi ve bakiye sorgusu.
- Müşteri ve kullanıcı bildirim şablonları; değişkenler, canlı önizleme ve varsayılana dönme.
- Gönderim kayıtları: kuyruk durumu, hata ayrıntısı, yeniden gönderme ve iptal.
- WooCommerce, Shopify, WHMCS, WISECP ve özel uygulama entegrasyonları; imzalı gelen siparişlerden otomatik fatura, imzalı giden webhook bildirimleri.
- Faturanın müşteriye gönderimi ve süre sınırlı, imzalı görüntüleme bağlantısı.
- Firma logosu yönetimi.
- Kullanıcı telefon numarası, profil düzenleme ve giriş bilgilerinin e-posta veya SMS ile iletimi.
- Güvenlik politikası, görünüm, imza sertifikası ve sistem durumu sayfaları.
- Olay kuyruğu (transactional outbox) ve arka plan mesaj işçisi.
- Veritabanı şeması sürüm 4.

### Değişenler

- Fatura durum geçmişi zaman çizelgesi olarak yeniden tasarlandı.
- Vergi daireleri listesine anlık arama, il ve tür süzgeçleri eklendi.
- Roller ve yetkiler sayfası sadeleştirildi.

## [5.2.0]

### Eklenenler

- Yeni giriş ekranı, ana sayfa, üst menü ve alt bilgi şeridi.
- Seviyeli rol hiyerarşisi ve politika tabanlı yetki listesi; API anahtarlarının yalnızca CEO tarafından oluşturulması.
- Ayrı ayarlar sayfaları, geliştirici dokümanı ve TKGM adres verisi sayfası.
- Kapsamlı HTTP güvenlik başlıkları.
