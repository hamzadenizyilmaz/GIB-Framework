# GIB Framework WHMCS modülü 1.2.0

WHMCS 9.0.0+, 64 bit PHP 8.2+, cURL ve GIB Framework 6.0.0 `/api/v1` API’si.

1. `gibframework` klasörünü WHMCS `modules/addons/` dizinine kopyalayın. Kurulu bir modülü güncelliyorsanız yeni dosyaları kopyalayıp **Addons → GIB Framework** ekranını açın.
2. İlgili firma için `ApiClient` rolüyle API anahtarı oluşturup WHMCS cron sürecine `GIBFRAMEWORK_API_KEY` ortam değişkeni olarak verin.
3. Aynı firmanın `GibPortal` ve `InvoiceApprove` yetkilerine sahip kullanıcısıyla GİB Portal’a bağlanın. Bu kullanıcının geçerli panel JWT’sini cron’a `GIBFRAMEWORK_PORTAL_BEARER` olarak verin. Firma politikası izin veriyorsa `GIBFRAMEWORK_PORTAL_USER` ve `GIBFRAMEWORK_PORTAL_PASSWORD` ile cron JWT alabilir. MFA gereken hesaplarda MFA ile alınmış JWT kullanın. JWT süresi dolmadan yenileyin. Bağlantı testi için aynı değişkenleri WHMCS web sürecine de tanımlayın.
4. **System Settings → Addon Modules** altında panelin HTTPS kök adresini, Portal ortamını, gerekiyorsa VKN/TCKN özel alan ID’sini ve dövizli faturalar için `USD=<kur> EUR=<kur>` biçiminde TL kurlarını girin. Satış sitesi adı ve adresi boşsa WHMCS firma adı ve sistem adresi kullanılır.
5. Eski bağımsız `includes/hooks/gibframework.php` kancası kullanılıyorsa kapatın ve yalnız o kancayla gönderilmiş geçmiş faturaları karşılaştırın. Mevcut modül kuyruğu ve belge eşleştirmeleri yükseltmede korunur.
6. **API ve istekler → Bağlantıyı test et** ile doğrulayın, otomatik aktarımı açın ve WHMCS cron’unun çalıştığını kontrol edin.

## Fatura akışı

- Cron, toplamı sıfırdan büyük `Paid` faturaları işler. GİB Portal bağlantısı geri geldiğinde kuyruk devam eder. Faturanın aktarımı aynı idempotency anahtarıyla tekrar denenebilir.
- Modül faturayı taslak olarak oluşturur ve doğrulamaya gönderir. Onaydan sonra uygun e-Arşiv belgesi için GİB Portal taslağı açılır ve belge `Signing` durumunda imza bekler. e-Fatura panelde `Approved` durumunda imza bekler.
- Satış sitesi adı ve adresi ile WHMCS fatura numarası ve mevcut ödeme/fatura tarihleri yeni faturaların notlarına eklenir. Önceden aktarılmış belgelere geriye dönük yazılmaz.
- Vergi, tutar veya uyum doğrulaması başarısızsa fatura incelemeye alınır. Paneldeki taslağı inceleyip doğrulama ve onay akışına tekrar alın. İmzalama, SMS onayı ve nihai iletim paneldeki yetkili kullanıcıya aittir.
- Geçmiş kayıtların tutarı WHMCS faturasından, alıcı bilgisi güncel WHMCS müşteri profilinden okunur. Profil değişikliği ve tarihsel döviz kuru gerektiren kayıtları aktarım öncesinde kontrol edin.

## Yönetim ekranı

**Faturalar** bölümünde ödeme ve belge durumları, belge numarası ve son sorgulama tarihi görünür. **API ve istekler** bölümünde bağlantı testi ve son isteklerin HTTP sonucu, gecikmesi, zaman bilgisi ve hata kodu bulunur. İstek ve yanıt gövdeleriyle kimlik bilgileri günlüğe yazılmaz.

Bootstrap 5.3.9 ve Türkçe karakterleri içeren Inter ile Instrument Sans fontları modülle birlikte gelir. [WHMCS Addon Module belgeleri](https://developers.whmcs.com/addon-modules) · [Bootstrap 5](https://getbootstrap.com/docs/5.3/getting-started/download/)
