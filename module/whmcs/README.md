<<<<<<< Updated upstream
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
=======
# GIB Framework · WHMCS eklenti modülü (2.3.2)

Ödenen WHMCS faturalarını GIB Framework paneline **imzalı webhook** ile aktarır ve fatura durumlarını
(onay, imza, belge numarası, iptal) panelden geri alır. Panelin hazır `includes/hooks/gibframework.php`
kanca dosyasının yerini alır; ek olarak kuyruk, yeniden deneme, döviz çevirme, durum takibi ve yönetim ekranı sunar.

## Akış

```
WHMCS fatura "Ödendi"
  └─ InvoicePaid kancası → kuyruk (mod_gibframework_queue) → hemen gönderim
       POST https://PANEL/api/v1/hooks/{entegrasyon-id}
       X-GibFramework-Signature: sha256=HMAC_SHA256(imza anahtarı, gövde)
  └─ başarısızsa WHMCS cron'u (AfterCronJob) artan bekleme ile yeniden dener

GIB Framework panel → fatura olayı (invoice.created … invoice.issued)
  └─ POST https://WHMCS/modules/addons/gibframework/callback.php
       X-GibFramework-Timestamp + X-GibFramework-Signature: sha256=HMAC(anahtar, timestamp + "." + gövde)
  └─ modül ekranındaki durum / belge no güncellenir
```

Her fatura `externalId = whmcs-{WHMCS fatura ID}` ile gönderilir. Panel bu kimlikle idempotenttir:
aynı fatura iki kez gönderilse de (modül + eski kanca dosyası, yeniden deneme) panelde tek taslak oluşur.
2.3.1 ile panel, kaynak tutarını hesaplanan fatura toplamıyla karşılaştırır. Modül, panelin sıra numarasını
kullanarak geç gelen veya tekrarlanan durum bildirimlerinin son durumun üzerine yazmasını önler.

## Gereksinimler

- WHMCS 8.0+, PHP 8.1+, cURL eklentisi
- WHMCS sunucusunun panel adresine HTTPS (443) ile erişebilmesi
- Durum bildirimleri için panelin WHMCS adresine HTTPS ile erişebilmesi

## Kurulum

1. `module/whmcs/gibframework` klasörünü WHMCS'te `modules/addons/gibframework` olarak yükleyin.
2. **WHMCS › Sistem Ayarları › Eklenti Modülleri › GIB Framework › Etkinleştir.**
3. GIB Framework panelinde **Ayarlar › Entegrasyonlar › WHMCS** sayfasında **Etkin**'i açıp kaydedin.
   **Kurulum** kutusundan şu iki değeri kopyalayın:
   - **Gelen adres (webhook URL)** → `https://…/api/v1/hooks/xxxxxxxx-xxxx-…`
   - **İmza anahtarı · X-GibFramework-Signature** → `gfs_…`
4. WHMCS'te modülün **Yapılandır** ekranına bu iki değeri girin, **Otomatik aktarım**'ı açın,
   yönetici rolünüze erişim izni verin ve kaydedin.
5. **Eklentiler › GIB Framework › Kurulum ve bağlantı › Bağlantıyı test et.** Test imzalı bir `ping` gönderir;
   panel imzayı doğrular, fatura oluşturmaz.
6. (Önerilir) Aynı sayfadaki **Bildirim adresi**ni (`…/modules/addons/gibframework/callback.php`) paneldeki
   WHMCS entegrasyonunun **Bildirim adresi (webhook URL)** alanına yazın, fatura olaylarını seçip kaydedin ve
   **Giden bildirimi dene**'ye basın. Modül ekranındaki "Son panel bildirimi" saati güncellenmelidir.
7. Panelden indirilen `includes/hooks/gibframework.php` dosyası kuruluysa silin (modül onun yerini alır).

## Modül ayarları

| Ayar | Açıklama |
| --- | --- |
| Otomatik aktarım | Kapalıyken hiçbir fatura gönderilmez, kuyruk korunur. |
| Gelen adres (webhook URL) | Paneldeki WHMCS entegrasyonunun gelen adresi. Biçim: `https://HOST/api/v1/hooks/{uuid}` |
| İmza anahtarı | Paneldeki `gfs_` ile başlayan anahtar. Panelde yenilenirse buraya da girilmelidir. |
| TCKN / VKN müşteri özel alanı | Özel alan adı (varsayılan `TC Kimlik / Vergi No`) veya ID'si. Boşsa WHMCS Tax ID, o da boşsa `11111111111`. |
| Vergi dairesi özel alanı | İsteğe bağlı; kurumsal alıcının vergi dairesi. |
| Belge türü | Hesap türü kimlik numarasından belirlenir: VKN (10 hane) → şirket, TCKN (11 hane) → şahıs. Belge türünü panel seçer: alıcı ve firmanız e-Fatura kayıtlıysa e-Fatura, değilse e-Arşiv. |
| Ödeme anında gönder | Açıkken fatura ödendiği istekte gönderilir; kapalıyken yalnızca cron'da. |
| Geçmiş faturalar için başlangıç tarihi | `YYYY-AA-GG`. Doluysa bu tarihten sonra ödenmiş eski faturalar da cron ile kuyruğa alınır (çalışma başına 250). |
| Satış sitesi adı / adresi | Fatura notuna eklenir. Boşsa WHMCS firma adı / sistem adresi. |
| Döviz → TL kurları | `USD=41.25 EUR=48.10`. Döviz faturaları bu kurla TL'ye çevrilir, orijinal tutar ve kur nota yazılır. |

Paneldeki **Gelen siparişi otomatik onaya gönder** ve **Alıcıyı cari kartlara kaydet** seçenekleri panel tarafında
uygulanır. **Vergisi belirtilmeyen satırlarda KDV oranı** modülde kullanılmaz (modül her satırın oranını gönderir).
Paneldeki **TCKN / VKN müşteri özel alanı adı** yalnızca hazır kanca dosyası içindir.

## Gönderilen JSON

```json
{
  "event": "invoice.paid",
  "externalId": "whmcs-1042",
  "orderNumber": "TR-SNDC-1-13",
  "currency": "TRY",
  "pricesIncludeTax": false,
  "customer": {
    "taxId": "11111111111", "name": "Ayşe Yılmaz", "company": "", "taxOffice": "",
    "email": "ayse@example.com", "phone": "+90.5321234567",
    "address": "Atatürk Cad. No:1", "district": "Kadıköy", "city": "İstanbul", "postalCode": "34710", "country": "TR"
  },
  "lines": [
    { "name": "Web hosting (1 yıl)", "quantity": 1, "unitPrice": 1000, "discount": 0, "vatRate": 20 }
  ],
  "notes": ["Satış sitesi: …", "Site adresi: …", "Ödeme tarihi: 2026-11-15", "Ödeme yöntemi: …"]
}
```

- KDV dahil WHMCS kurulumlarında `pricesIncludeTax: true` gönderilir; panel net fiyatı kendisi hesaplar.
- Negatif (indirim / kredi) satırları aynı KDV oranındaki hizmet satırlarına `discount` olarak dağıtılır.
- Desteklenen KDV oranları: 0, 1, 8, 10, 18, 20. İkinci vergi (`tax2`) içeren faturalar incelemeye düşer.

## Resmi Fatura bağlantısı (2.1)

Panel faturayı düzenleyip (`invoice.issued`) bildirim adresine gönderdiğinde modül faturanın uzun ömürlü
(≈10 yıl) görüntüleme bağlantısını saklar ve **Resmi Fatura** düğmesi olarak gösterir:

- Müşteri paneli › Faturalarım listesinde her faturanın yanında,
- Müşterinin fatura görüntüleme sayfasında (`viewinvoice.php`) üstte,
- Yönetici › fatura düzenleme sayfasında (belge durumu ve belge numarasıyla birlikte),
- Modül ekranındaki belge kayıtları tablosunda.

Bunun için paneldeki **Bildirim adresi**nin `callback.php` olarak girilmiş, **Fatura düzenlendi (invoice.issued)** olayının
seçilmiş ve WHMCS alan adının platform yöneticisi tarafından **Ayarlar › Güvenlik › Dış bağlantı izinleri** listesine eklenmiş olması gerekir.
İptal / red durumunda bağlantı kaldırılır.

## Yönetim ekranı

**Eklentiler › GIB Framework** — ekran WHMCS arayüzünün üzerinde tam sayfa açılır ve GIB Framework panelinin
açık temasını (Manrope / DM Sans, Bootstrap Icons, panel ikonları) kullanır. Sağ üstteki **WHMCS** düğmesi yönetim
paneline döner. Tüm ölçüler px'tir (WHMCS yönetim paneli kök yazı boyutunu 10px yaptığından rem kullanılmaz).

Bağlantı testi 403 ile başarısız olursa modül, WHMCS sunucusunun çıkış IP'sini panelin `/cdn-cgi/trace` adresinden
okuyup uyarıda gösterir; bu IP'yi izin listesine eklemeniz yeterlidir.

- **Faturalar:** kuyrukta / aktarılan / taslak-onay / imza bekleyen / iletimde / kesinleşen / inceleme sekmeleri,
  arama (WHMCS fatura ID, fatura no, belge no, taslak no), panelde belgeyi açma bağlantısı.
  - **Şimdi gönder:** cron'u beklemeden kuyruğu çalıştırır.
  - **Kuyruğa ekle:** ödenmiş bir faturayı ID ile elle kuyruğa ekler.
  - **Şimdi dene:** hata almış faturayı hemen yeniden denetir.
  - **Yeniden gönder:** panelde reddedilen / iptal edilen faturanın eşleşmesini sıfırlar.
- **Kurulum ve bağlantı:** adım adım kurulum, ayar durumu, bağlantı testi, bildirim adresi ve istek geçmişi (son 1000).

## Hata kodları

| Kod | Anlamı | Ne yapılmalı |
| --- | --- | --- |
| `CONFIG_WEBHOOK_URL_MISSING` / `CONFIG_WEBHOOK_URL` | Gelen adres boş / hatalı | Paneldeki gelen adresi modül ayarlarına girin |
| `CONFIG_SIGNING_SECRET_MISSING` / `CONFIG_SIGNING_SECRET` | İmza anahtarı boş / hatalı | `gfs_…` anahtarını girin |
| `SIGNATURE_REJECTED` (HTTP 401) | Anahtar paneldekiyle aynı değil | Panelde anahtar yenilendiyse modüle de girin |
| `PANEL_IP_DENIED` (HTTP 403) | Panelin **site geneli (Platform)** IP kuralları WHMCS sunucusunu engelliyor (firma IP listesi webhook'lara uygulanmaz) | Süper yönetici ile Ayarlar › Güvenlik › IP erişimi ("Site genelinde geçerli kurallar") listesine WHMCS IP'sini ekleyin; ekrandaki IP'yi kullanın |
| `CLOUDFLARE_BLOCKED` (HTTP 403) | Panelin önündeki Cloudflare (Bot Fight Mode / WAF) isteği engelledi | Cloudflare › Security › WAF › Custom rules: `URI Path starts with /api/v1/hooks/` → **Skip** (Bot Fight Mode / Super Bot Fight Mode / WAF managed rules) veya WHMCS IP'si için IP Access Rule → Allow |
| `HTTP_403` | Başka bir güvenlik katmanı reddetti | Sunucu güvenlik duvarı / ters vekil kurallarını kontrol edin |
| `INTEGRATION_DISABLED` (HTTP 404) | Panel entegrasyonu kapalı / silinmiş | Panelde **Etkin** yapıp kaydedin |
| `FRAMEWORK_CONNECTION` | Panele bağlanılamadı | DNS, SSL, güvenlik duvarı |
| `FRAMEWORK_REJECTED` (HTTP 400/422) | Panel faturayı kabul etmedi | Satırdaki ayrıntıya bakın, düzeltip **Şimdi dene** |
| `RATE_LIMITED` (HTTP 429) | İstek sınırı | Otomatik devam eder |
| `INVOICE_EXCHANGE_RATE` | Döviz kuru tanımlı değil | **Döviz → TL kurları** ayarını doldurun |
| `INVOICE_VAT_RATE`, `INVOICE_SECOND_TAX`, `INVOICE_TOTAL_MISMATCH`, `INVOICE_TAX_RECONCILIATION`, `INVOICE_DISCOUNT_ALLOCATION` | Fatura tutarları otomatik eşlenemedi | Faturayı panelde elle düzenleyin |
| `CUSTOMER_TAX_ID`, `CUSTOMER_BILLING_DETAILS` | Müşteri bilgisi eksik / hatalı | WHMCS müşteri kartını düzeltin |

Kuyruğu etkileyen hatalar (adres, imza, bağlantı) tüm aktarımı duraklatır; faturaya özgü hatalar yalnızca o faturayı
5 dk → 6 saat arası artan beklemeyle yeniden dener.

## 1.x sürümünden yükseltme

1.x sürümü panelin REST API'sini (API anahtarı, operatör hesabı, GİB Portal kimlikleri) kullanıyordu. 2.0 bunların
yerine paneldeki WHMCS entegrasyonunun imzalı webhook'unu kullanır:

- Dosyaları üzerine yazın ve `modules/addons/gibframework/lib/FrameworkClient.php` dosyasını silin.
- Yükseltmede eski ayarlar (`frameworkUrl`, `credentialsFile`, `portalEnvironment`, `taxFieldId`) silinir; yapılandırma
  hatası yüzünden bekleyen faturalar sıfırlanır ve yeni akışla hemen denenir. Daha önce aktarılmış faturaların eşleşmesi korunur.
- Kimlik bilgisi JSON dosyası artık gerekmez; sunucudan silebilirsiniz.
- Onay ve imza artık panelde yapılır (otomatik onaya gönderme panel ayarıdır).
>>>>>>> Stashed changes
