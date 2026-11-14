<p align="center">
  <a href="https://www.bilhost.com/panel/aff.php?aff=37">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="assets/branding/bilhost-logo-dark.svg">
      <img src="assets/branding/bilhost-logo.svg" alt="Bilhost" height="90">
    </picture>
  </a>
</p>

<h2 align="center">Projemiz <a href="https://www.bilhost.com/panel/aff.php?aff=37">Bilhost</a> tarafından desteklenmektedir.</h2>

<p align="center"><a href="https://www.bilhost.com/panel/aff.php?aff=37"><b>www.bilhost.com</b></a></p>

<br>

<div align="center">

# GIB Framework

Gelir İdaresi Başkanlığı e-Fatura ve e-Arşiv süreçleri için çok amaçlı faturalama platformu.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React-19-149ECA?logo=react&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2019%2B-CC2927?logo=microsoftsqlserver&logoColor=white)
![Sürüm](https://img.shields.io/badge/s%C3%BCr%C3%BCm-6.0.0-0778E6)

[Proje sitesi](https://framework.hamzadenizyilmaz.com.tr) &nbsp;|&nbsp;
[API dokümantasyonu](#rest-api) &nbsp;|&nbsp;
[Kurulum kılavuzu](install.md) &nbsp;|&nbsp;
[Değişiklik günlüğü](CHANGELOG.md) &nbsp;|&nbsp;
[Güvenlik](SECURITY.md)

</div>

## İçindekiler

1. [Genel bakış](#genel-bakış)
2. [Özellikler](#özellikler)
3. [Mimari](#mimari)
4. [Teknoloji yığını](#teknoloji-yığını)
5. [Kurulum](#kurulum)
6. [Demo hesapları](#demo-hesapları)
7. [Roller ve yetkiler](#roller-ve-yetkiler)
8. [Fatura mimarisi](#fatura-mimarisi)
9. [E-posta ve SMS bildirimleri](#e-posta-ve-sms-bildirimleri)
10. [Pazaryeri ve e-ticaret entegrasyonları](#pazaryeri-ve-e-ticaret-entegrasyonları)
11. [REST API](#rest-api)
12. [Güvenlik](#güvenlik)
13. [Canlı ortama alma](#canlı-ortama-alma)
14. [Veritabanı](#veritabanı)
15. [Proje yapısı](#proje-yapısı)
16. [Lisans ve iletişim](#lisans-ve-iletişim)

## Genel bakış

GIB Framework; fatura hazırlama, kural doğrulama, onay, mali mühürle imzalama, GİB'e iletim ve arşivleme adımlarını tek bir
uygulamada toplar. Her firma kendi verisine SQL Server Row-Level Security ile izole edilmiş olarak erişir. Pazaryerleri,
e-ticaret altyapıları ve hosting faturalama sistemlerinden gelen siparişler otomatik olarak faturaya dönüştürülür; düzenlenen
fatura müşteriye e-posta ve SMS ile, kaynak platforma ise PDF bağlantısı olarak geri bildirilir.

## Özellikler

### Belge yönetimi

| Alan | Kapsam |
|---|---|
| e-Fatura ve e-Arşiv | Cari karttaki e-Fatura durumuna göre belge seçimi, taslak, doğrulama, onay, imza ve gönderim |
| UBL-TR | UBL-TR 1.2 belge üretimi, XAdES-BES imza, GİB karekodu |
| Kanıt kasası | Hash zincirli belge arşivi ve okuma sırasında boyut ve SHA-256 doğrulaması |
| PDF fatura | A4 görünüm, firma logosu, karekod, ürün kodu, KDV, banka ve iade alanı |
| Paylaşım bağlantısı | Dış paylaşıma uygun faturalar için süreli müşteri PDF adresi. Diğer faturalar için yetki gerektiren panel adresi. |
| Nihai tüketici | Kimlik numarası bulunmayan alıcılar için `11111111111` ile düzenleme |

### GİB ve mevzuat

| Alan | Kapsam |
|---|---|
| GİB e-Arşiv Portal | Test kullanıcısı, ETTN ile taslak mutabakatı, SMS imzası, imzalı belge arşivi ve belge listesi |
| Kural motoru | Sürümlenen ve yürürlük tarihli KDV, tevkifat ve istisna kuralları; mevzuat kaynağı değişiklik takibi |
| Referans veriler | 81 il, TKGM ilçe ve mahalle verisi, 860 birimlik GİB vergi dairesi listesi |

### İletişim ve entegrasyon

| Alan | Kapsam |
|---|---|
| E-posta | STARTTLS, SSL ve SMTP AUTH destekli yerleşik istemci; firma markalı HTML şablonlar; PDF ve UBL-TR XML ekleri |
| SMS | Netgsm REST v2 ile gönderim, gönderici başlığı listesi ve bakiye sorgusu |
| Pazaryerleri | Trendyol, Hepsiburada ve n11 sipariş aktarımı, başarısız kayıtları yeniden işleme ve fatura bağlantısı iletme |
| E-ticaret ve hosting | WooCommerce, Shopify, WHMCS ve WISECP için HMAC imzalı webhook kabulü |
| Özel uygulamalar | Genel sipariş şeması ile gelen istekler ve imzalı giden webhook bildirimleri |

### Yönetim

| Alan | Kapsam |
|---|---|
| Yetkilendirme | 14 rollü seviye hiyerarşisi ve politika tabanlı yetki modeli |
| Kimlik doğrulama | JWT, TOTP iki adımlı doğrulama, hesap kilitleme, firma bazlı oturum ve hareketsizlik politikası |
| IP Erişimi | Firma ve platform kapsamlarında IP veya CIDR izin ve engel listeleri |
| Panel | React 19 ve Bootstrap 5; açık ve koyu tema, vurgu rengi ve yoğunluk seçenekleri, mobil uyumlu arayüz |
| Denetim | Kullanıcı, rol, işlem, tarih ve sonuç filtreli hash zincirli kayıtlar ve belge bütünlük denetimi |

## Mimari

```mermaid
flowchart LR
    subgraph Kaynaklar["Sipariş kaynakları"]
        MP[Trendyol / Hepsiburada / n11]
        EC[WooCommerce / Shopify]
        HB[WHMCS / WISECP]
        CA[Özel uygulama]
    end
    subgraph Uygulama["ASP.NET Core 10"]
        API[REST API]
        HK[Webhook uç noktası]
        INV[Fatura servisi]
        OB[(Olay kuyruğu)]
        WK[Arka plan işçileri]
    end
    UI[Panel] --> API --> INV
    EC & HB & CA --> HK --> INV
    WK -- periyodik sorgu --> MP
    INV --> DB[(SQL Server, RLS)]
    INV --> EV[(Kanıt kasası)]
    INV -- aynı işlem --> OB --> WK
    WK --> SMTP[SMTP]
    WK --> SMS[Netgsm]
    WK -- fatura bağlantısı, webhook --> Kaynaklar
    INV <--> GIB[GİB e-Arşiv Portal]
```

Fatura durum değişiklikleri, durum güncellemesiyle aynı veritabanı işlemi içinde olay kuyruğuna yazılır (transactional outbox).
Bildirim, webhook ve pazaryeri çağrıları arka plan işçileri tarafından artan bekleme süreleriyle yeniden denenir; böylece dış
sistemlerdeki kesintiler fatura işlemini etkilemez.

## Teknoloji yığını

| Katman | Teknoloji |
|---|---|
| Uygulama | ASP.NET Core 10 LTS, ADO.NET (Microsoft.Data.SqlClient) |
| Veritabanı | SQL Server 2019 ve üzeri, Row-Level Security, tetikleyici tabanlı değişmezlik |
| İmza | XAdES, Windows sertifika deposu ve GİB Portal imza akışı |
| Panel | React 19, Vite 8, Bootstrap 5, Bootstrap Icons |
| API dokümantasyonu | Swashbuckle (OpenAPI 3), ReDoc |
| Barındırma | IIS (ASP.NET Core Module V2), Plesk, bağımsız win-x64 yayın |

## Kurulum

Kurulum için .NET 10 SDK 10.0.404, Node.js 24 ve SQL Server 2019 veya üzeri kullanın. [Kurulum kılavuzundaki](install.md) SQL kurulumunu ve yerel gizli ayarları tamamladıktan sonra uygulamayı başlatın.

```bash
dotnet run --project GIBFramework/GIBFramework.csproj --launch-profile http
```

[Panel](http://localhost:5097/app/#/login) ve [hazırlık denetimi](http://localhost:5097/health/ready) aynı sunucudadır. Panel derlemesi otomatik çalışır. Sunucu genelindeki ve firma bazındaki kurallar **Ayarlar → Güvenlik → IP Erişimi** sayfasından yönetilir. Başlangıçtaki platform kuralları sunucuda `IpAccess:Allow` ve `IpAccess:Deny` ile verilebilir.

## Demo hesapları

[Demo giriş ekranı](https://framework.hamzadenizyilmaz.com.tr/app/#/login)

| Kullanıcı kodu | Yayımlanan demo parolası | Rol |
|---|---|---|
| `yonetici` | `GibDemo2026` | Genel Müdür / CEO |
| `firmaadmin` | `GibDemo2026` | Firma yöneticisi |
| `onay` | `GibDemo2026` | Muhasebe müdürü |
| `muhasebe` | `GibDemo2026` | Muhasebeci, fatura hazırlayan |
| `imza` | `GibDemo2026` | İmza yetkilisi, entegrasyon yöneticisi |
| `denetci` | `GibDemo2026` | Arşiv denetçisi |

**Canlı demo durumu:** Son kontrolde site `yonetici` için bu parolayla girişi reddetti. Tablo, v6.0.0 sürümünde yayımlanan demo bilgilerini gösterir.

Kendi demo kurulumunuzda `Development` veya `Demo` ortamını kullanın. `Demo:Enabled` ayarını etkinleştirip `Demo:Password` değerini belirlediğinizde uygulama eksik demo hesaplarını oluşturur. Var olan hesapların parolaları korunur. [Demo kurulum adımları](install.md#isteğe-bağlı-demo-ve-panel-geliştirme)

Boş veritabanında ilk platform yöneticisi `Bootstrap:AdminUserCode` ve `Bootstrap:AdminPassword` ayarlarıyla oluşturulur. Yerel yapılandırmada kullanıcı kodu `admin` ve ilk girişte şifre değişikliği etkindir.

## Roller ve yetkiler

Her rolün bir seviyesi vardır. Kullanıcılar yalnızca kendi seviyelerinin altındaki rolleri atayabilir ve yalnızca daha düşük
seviyedeki kullanıcıları yönetebilir. Firma sahibi (CEO) ataması platform yöneticisi tarafından yapılır.

| Seviye | Rol | Sorumluluk |
|---:|---|---|
| 100 | Platform yöneticisi | Firmalar, firma sahipleri ve duyurular |
| 90 | Genel Müdür / CEO | Firmadaki tüm işlemler, rol atama, API anahtarları |
| 80 | Firma yöneticisi | Kullanıcılar, firma profili, ayarlar, iletişim ve entegrasyonlar |
| 70 | Muhasebe müdürü | Fatura oluşturma, onay, iptal, GİB e-Arşiv Portal |
| 60 | Güvenlik sorumlusu | Denetim izi, güvenlik politikası, sistem durumu |
| 60 | Uyum sorumlusu | Mevzuat kuralları, vergi daireleri, denetim izi |
| 50 | Onay yetkilisi | Başkasının hazırladığı faturanın onayı veya reddi |
| 50 | İmza yetkilisi | Mali mühür veya GİB imzası ve gönderim |
| 50 | Entegrasyon yöneticisi | Gönderim, durum sorgulama, entegrasyon ayarları |
| 40 | Muhasebeci | Fatura, cari ve ürün kayıtları, gelen faturalar |
| 30 | Fatura hazırlayan | Taslak fatura, cari ve ürün kayıtları |
| 20 | Arşiv denetçisi | Fatura ve denetim izi görüntüleme |
| 10 | Salt okunur denetçi | Yalnızca görüntüleme |
| 10 | API istemcisi | Üçüncü taraf uygulamalar için fatura okuma ve oluşturma |

Seçili yetki politikaları:

| Politika | Roller |
|---|---|
| API anahtarı yönetimi | CEO |
| E-posta, SMS ve şablonlar | CEO, firma yöneticisi |
| Entegrasyonlar | CEO, firma yöneticisi, entegrasyon yöneticisi |
| Güvenlik politikası | Platform yöneticisi, CEO, firma yöneticisi, güvenlik sorumlusu |
| IP erişim kuralları | Platform yöneticisi site genelinde, CEO kendi firmasında |
| Fatura onayı | Platform yöneticisi seçili firmada, CEO, muhasebe müdürü ve onay yetkilisi |
| Kullanıcı ve rol yönetimi | Platform yöneticisi, CEO, firma yöneticisi |
| Fatura imzalama | CEO, imza yetkilisi |

Tam liste panelde **Ayarlar > Roller ve yetkiler** sayfasında yer alır.

## Fatura mimarisi

```mermaid
stateDiagram-v2
    state "Taslak" as Draft
    state "Doğrulanıyor" as Validating
    state "Onay bekliyor" as Awaiting
    state "Onaylandı" as Approved
    state "İmzalandı" as Signed
    state "Gönderildi" as Sent
    state "İptal edildi" as Cancelled
    [*] --> Draft
    Draft --> Validating: Doğrulamaya gönder
    Validating --> Awaiting: Kurallar sağlandı
    Validating --> Draft: Kural hatası
    Awaiting --> Approved: Onay
    Awaiting --> Draft: Ret
    Approved --> Signed: XAdES imza
    Signed --> Sent: GİB iletimi
    Sent --> Cancelled: İptal
```

| Konu | Davranış |
|---|---|
| Belge türü | Alıcı e-Fatura mükellefi ise e-Fatura, değilse e-Arşiv |
| Numaralandırma | Taslak oluşturulurken geçici numara, imza anında seri ve yıl bazlı belge numarası |
| Saat dilimi | Tüm belge tarihleri Europe/Istanbul |
| Dört göz ilkesi | Faturayı hazırlayan onaylayamaz; `Workflow:SelfApprovalRoles` listesindeki roller hariç |
| Nihai tüketici | Kimlik numarası girilmezse `11111111111` kullanılır ve bu numara için cari kart açılmaz |
| Değişmezlik | İmzalanmış belge ve denetim kayıtları veritabanı tetikleyicileriyle değişikliğe ve silmeye kapalıdır |

## E-posta ve SMS bildirimleri

İletişim ayarları panelde **Ayarlar > İletişim** bölümünden yapılır. Tüm gönderimler kuyruk üzerinden, artan bekleme süreleriyle
yeniden denenerek yapılır ve **Gönderim kayıtları** sayfasından izlenir, yeniden gönderilir veya iptal edilir.

| Şablon | Alıcı | Kanal |
|---|---|---|
| Fatura düzenlendi | Müşteri | E-posta (PDF ve XML ekli), SMS |
| Fatura iptal edildi | Müşteri | E-posta, SMS |
| Onay bekleyen fatura | Onay yetkilileri | E-posta, SMS |
| Fatura reddedildi veya gönderilemedi | Hazırlayan kullanıcı | E-posta, SMS |
| Hesap oluşturuldu, şifre sıfırlandı | Kullanıcı | E-posta, SMS |
| Şifre sıfırlama talebi | Firma yöneticileri | E-posta |
| Hesap kilitlendi | Kullanıcı | E-posta, SMS |
| API anahtarı oluşturuldu | CEO | E-posta |

- Şablonlar `{{fatura.no}}`, `{{musteri.unvan}}`, `{{fatura.link}}` gibi değişkenlerle düzenlenir ve canlı önizlenir.
- Geçici şifreler veritabanında açık metin olarak tutulmaz; gönderim anında çözülerek içeriğe eklenir.
- SMTP için STARTTLS (587), SSL (465) ve şifrelemesiz bağlantı desteklenir; Gmail, Microsoft 365 ve Yandex için hazır ayarlar bulunur.
- Netgsm için abone numarası ve API alt kullanıcısı kullanılır; gönderici başlıkları ve bakiye panelden sorgulanır.

## Pazaryeri ve e-ticaret entegrasyonları

Entegrasyonlar panelde **Ayarlar > Entegrasyonlar** sayfasından kod yazmadan yapılandırılır. Tüm kimlik bilgileri ve imza
anahtarları ASP.NET Core Data Protection ile şifrelenerek saklanır.

| Platform | Sipariş alımı | Geri bildirim |
|---|---|---|
| Trendyol | Sipariş paketleri (Order V2) belirlenen aralıkla sorgulanır | Fatura bağlantısı `seller-invoice-links` servisine iletilir |
| Hepsiburada | Faturası yüklenmemiş paketler sorgulanır | Fatura bağlantısı ilgili pakete eklenir |
| n11 | Sipariş paketleri REST servisi üzerinden sorgulanır | Fatura bağlantısı `SaveLinkSellerInvoice` ile kaydedilir |
| WooCommerce | `order.updated` webhook, `X-WC-Webhook-Signature` doğrulaması | Siparişe müşteriye görünen fatura notu |
| Shopify | `orders/paid` webhook, `X-Shopify-Hmac-Sha256` doğrulaması | İmzalı webhook |
| WHMCS | `InvoicePaid` kancası (hazır PHP kanca dosyası) | İmzalı webhook |
| WISECP | Fatura durum değişikliği kancası (hazır PHP kanca dosyası) | İmzalı webhook |
| Özel uygulama | Genel sipariş şeması, `X-GibFramework-Signature` doğrulaması | İmzalı webhook |

Pazaryerlerine iletilen bağlantı HTTPS üzerinden sunulan ve `.pdf` uzantısıyla biten kalıcı PDF adresidir. Aynı sipariş ikinci kez
faturalanmaz.

Özel uygulama isteği:

```http
POST /api/v1/hooks/{entegrasyonId}
Content-Type: application/json
X-GibFramework-Signature: sha256=<HMAC-SHA256(imza anahtarı, istek gövdesi)>

{
  "externalId": "SIPARIS-1001",
  "currency": "TRY",
  "customer": { "taxId": "11111111111", "name": "Ayşe Yılmaz", "email": "ayse@example.com", "city": "İstanbul" },
  "lines": [ { "name": "Web hosting", "quantity": 1, "unitPrice": 1000, "vatRate": 20 } ]
}
```

Giden webhook gövdesi ve PHP, Node.js ve C# imza doğrulama örnekleri panelde **Ayarlar > Geliştirici dokümanı** sayfasında yer alır.

## REST API

| Konu | Ayrıntı |
|---|---|
| Dokümantasyon | `Swagger:Enabled` etkin olduğunda `/swagger`, `/redoc`, `/swagger/v1/swagger.json` |
| Kimlik doğrulama | `X-Api-Key` başlığı ile firma API anahtarı veya `Authorization: Bearer` ile JWT |
| Kullanıcı işlemleri | Hesap, fatura onayı ve imzası ile GİB portalı Bearer oturumu gerektirir |
| Tekrarlanabilirlik | Fatura oluşturma isteklerinde `Idempotency-Key` başlığı zorunludur |
| Hata biçimi | RFC 7807 (`application/problem+json`), her yanıtta `X-Correlation-Id` |
| Sayfalama | `GET /api/v1/invoices/page?take=50&skip=0` |
| Özet ve görevler | `GET /api/v1/invoices/overview` ve `GET /api/v1/invoices/tasks/{task}` |
| Sağlık | `/health` süreç canlılığını, `/health/ready` hazırlığı bildirir |

```bash
curl -H "X-Api-Key: gfk_..." "https://framework.hamzadenizyilmaz.com.tr/api/v1/invoices/page?take=50"
```

## Güvenlik

| Alan | Uygulama |
|---|---|
| HTTP başlıkları | Content-Security-Policy, Strict-Transport-Security, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy, Cross-Origin-Opener/Resource/Embedder-Policy |
| Kimlik | JWT ve güvenlik damgası, TOTP iki adımlı doğrulama, hesap kilitleme, zorunlu şifre değişikliği |
| Firma politikası | 2FA zorunluluğu, oturum süresi, hareketsizlik süresi, API anahtarı ömrü |
| Veri izolasyonu | SQL Server Row-Level Security ile firma bazlı satır filtresi |
| Denetim | Hash zincirli denetim izi ve kanıt kasası |
| Sırların korunması | SMTP, Netgsm ve entegrasyon sırları Data Protection ile şifreli; anahtarlar sunucuda makine kapsamında korunur |
| Webhook | HMAC-SHA256 imza, zaman damgası, istek boyutu ve hız sınırı; canlı ortamda iç ağ adreslerine gönderim engellenir |

Güvenlik açığı bildirimi için [SECURITY.md](SECURITY.md) dosyasına bakın.

## Canlı ortama alma

### Plesk (Windows Server 2019-2022-2025)

Yayın bağımsız `win-x64` olarak derlenir. IIS için .NET 10 Hosting Bundle ile gelen ASP.NET Core Module V2 gerekir.

```bash
dotnet publish GIBFramework/GIBFramework.csproj -c Release -r win-x64 --self-contained true -o publish/httpdocs
```

`publish/httpdocs/` uygulama dosyalarını içerir. Plesk site köküne bu dizinin içeriğini kopyalayın. SQL betiklerini [database](database/) dizininden hedef veritabanına uygulayın. Üretim bağlantı dizesini ve JWT anahtarını dağıtım ortamından verin. Adımlar [install.md](install.md) dosyasındadır.

### Ortamlar

| Ortam | Kullanım |
|---|---|
| `Development` | Yerel geliştirme |
| `Demo` | Demo sitesi: demo hesaplar, test imza sertifikası, GİB test portalı |
| `Production` | Sertifika, UBL ve hazırlık koşulları tamamlandığında GİB e-Arşiv Portal üzerinden desteklenen e-Arşiv SATIS belgeleri |

IIS/Plesk uygulama havuzuna .NET 10 Hosting Bundle ile gelen ASP.NET Core Module V2 gerekir. Uygulama havuzunda `ASPNETCORE_ENVIRONMENT=Production` tanımlayın ve gizli değerleri dağıtım ortamından verin. Üretim ayarlarını tamamladıktan sonra HTTPS üzerinden `/health` ve `/health/ready` uçlarını doğrulayın. Sistem ekranı hazırlık nedenini ve denetim süresini gösterir.

v6.0.0 SQL şema 5 ile çalışır. API anahtarıyla çalışan istemciler `/api/v1/auth/permissions` ve atanmış makine işlemlerini kullanabilir. Hesap, fatura onayı, imza ve portal işlemleri için kullanıcı oturumu gerekir. Portal kayıtlarının dış bağlantıları doğrulanmış düzenleme kanıtı gerektirir. IIS ve ters proxy erişim günlüklerinde `/f/` yollarındaki bağlantı belirteçlerini ve hassas sorgu dizelerini kaydetmeyin.

Mevcut şema 4 veritabanında `database/06_Sema5.sql` dosyasını `ALTER ANY SECURITY POLICY` yetkisi olan veritabanı yöneticisi çalıştırır. Betik eksik firma satır güvenliği politikasını aynı işlemde kurar. Kurulumdan sonra `dbo.SchemaInfo` tablosundaki son sürüm 5 ve `dbo.TenantIsolation` politikası etkin olmalıdır.

### Yapılandırma

Varsayılanlar `appsettings.json` dosyasındadır. Ortama özel değişkenlerde bölüm ayırıcı olarak çift alt çizgi kullanın. SQL için güvenilir TLS sertifikası, uygulama havuzu kimliği ve şema 5 gerekir. Kanıt ve Data Protection dizinlerine yalnızca uygulama havuzu kimliği ve yetkili operatörler erişebilmelidir.

| Anahtar | Açıklama |
|---|---|
| `ConnectionStrings:GibFramework` | SQL Server bağlantı dizesi |
| `Jwt:Issuer`, `Jwt:SigningKey` | Yerel JWT issuer ve en az 32 baytlık gizli anahtar |
| `Bootstrap:AdminPassword` | İlk platform yöneticisi şifresi |
| `Compliance:SeedPath` | Mevzuat kaynağı, yürürlükteki onaylı belge yönlendirme kuralları ve vergi kataloğu dizini |
| `App:PublicUrl` | E-posta, paylaşım ve pazaryeri bağlantılarında kullanılan genel adres |
| `Signing:Mode`, `Signing:TrustedRootSha256` | `WindowsStore` ve Windows güven deposundaki GİB zinciri köklerinin SHA-256 parmak izleri. İptal doğrulaması için güncel CRL/OCSP verisi gerekir. |
| `Providers:PortalOnly`, `GibPortal:AllowProduction` | Canlı portal yayını için ikisini de `true` yapın. Portal oturumu ve SMS isteği için tek IIS işçi süreci kullanın. |
| `Ubl:RequireSchemaValidation`, `Ubl:SchemaVersion`, `Ubl:SchemaDirectory`, `Ubl:SchemaSha256` | Onaylı UBL-TR XSD paketi. Dizin özetinde her satır `göreli/yol.xsd:SHA256` biçimindedir. Yollar ordinal sıralanır, özetler büyük harftir, satırlar LF ile birleştirilir ve UTF-8 metne SHA-256 uygulanır. |
| `Ubl:SchematronPath`, `Ubl:SchematronSha256` | SVRL üreten bağımsız XSLT 1.0 şematron çıktısı ve dosyanın SHA-256 özeti |
| `ReverseProxy:KnownProxies` | Tek hop için güvenilen proxy IP adresleri. Varsayılan loopback adresleri korunur. |
| `OutboundNetwork:WebhookHosts`, `OutboundNetwork:SmtpHosts` | Firma tarafından seçilebilecek tam hedef adları. Ağ güvenlik duvarında da yalnızca izinli çıkışları açın. |
| `Messaging:Smtp`, `Messaging:Sms` | Platform geneli varsayılan SMTP ve Netgsm ayarları |
| `Swagger:Enabled` | API dokümantasyonunun canlı ortamda yayınlanması |

## Veritabanı

| Dosya | İçerik |
|---|---|
| `00_Veritabani.sql` | Yerel `GIBFramework` veritabanını oluşturma ve seçme |
| `01_Tablolar.sql` | Tablolar ve indeksler |
| `02_Tetikleyiciler.sql` | Değişiklik ve silme engelleri |
| `03_SatirGuvenligi.sql` | Temel firma satır güvenliği politikası |
| `04_BaslangicVerileri.sql` | Şema sürümü, iller, duyurular |
| `05_VergiDaireleri.sql` | GİB vergi daireleri (860 birim) |
| `06_Sema5.sql` | Şema 4 → 5 yükseltmesi, eksik RLS kuralları ve kuyruk indeksleri |
| `GIBFramework_TamKurulum.sql` | Yerel temiz kurulum için birleşik betik |
| `GIBFramework_Plesk.sql` | Plesk'te seçili boş veritabanına temiz kurulum |
| `Build-Kurulum.ps1` | Birleşik SQL dosyalarını numaralı kaynaklardan üretir |

Numaralı kurulumda betikleri sırasıyla aynı hedef veritabanında çalıştırın. Yerel temiz kurulumda `00` ile başlayın. Sağlayıcının açtığı boş veritabanında `01` ile başlayın. Bir hata oluştuğunda yürütmeyi durdurun. `sqlcmd` ile çalıştırırken `-b` kullanın. Şema 5 yükseltmesi tek işlemde uygulanır. Uygulama başlangıcında şema sürümü doğrulanır.

## Proje yapısı

```text
GIBFramework/
  Base/             Ortak tipler, sabitler, servis kayıtları
  Contracts/        Servis sözleşmeleri
  Controllers/      REST uç noktaları
  DAL/              ADO.NET depoları, şema doğrulama, başlangıç verileri
  DTOs/             İstek ve yanıt modelleri
  Infrastructure/   Arka plan işçileri, imza sağlayıcıları, GİB portal istemcisi, kanıt kasası, Swagger
  Mapper/           Model dönüşümleri
  Middleware/       Güvenlik başlıkları, kiracı bağlamı, hata işleme
  Models/           Alan modelleri
  Options/          Yapılandırma sınıfları
  Services/         Fatura, imza, UBL, PDF, mesajlaşma ve entegrasyon servisleri
  ClientApp/        React panel
database/           SQL kurulum scriptleri
```

## Lisans ve iletişim

Telif hakkı 2019-2026 Hamza Deniz Yılmaz. Tüm hakları saklıdır.

Depo: [github.com/hamzadenizyilmaz/GIB-Framework](https://github.com/hamzadenizyilmaz/GIB-Framework)
