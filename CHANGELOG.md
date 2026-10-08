# Değişiklik günlüğü

Bu dosya [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) biçimini ve [Anlamsal Sürümleme](https://semver.org/lang/tr/)
kurallarını izler.

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
