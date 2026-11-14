# Değişiklik günlüğü

Bu dosya [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) biçimini ve [Anlamsal Sürümleme](https://semver.org/lang/tr/)
kurallarını izler.

## **[6.0.0] - 2026-11-14**

Bu ana sürüm, fatura kesen kullanıcıların günlük çalışma ekranlarını ve uygulamanın yönetim tarafını birlikte güncelliyor. Üst menü ve belge ayrıntısı yeniden düzenlendi. Fatura önizlemesiyle PDF çıktısı daha ayrıntılı hale geldi. Firma yöneticileri kendi IP kurallarını ayarlayabiliyor ve onay bekleyen kendi faturalarını sonuçlandırabiliyor. GİB taslak mutabakatı, belge arşivi ve pazaryeri siparişlerinde operatörün takip edebileceği daha ayrıntılı durumlar bulunuyor.

### Panel tasarımı ve gezinme

- Masaüstü üst çubuğunda marka, ana menü ve işlem alanı yeniden hizalandı. **Panel**, **Faturalar**, **Kayıtlar**, **GİB**, **Mevzuat** ve **Ayarlar** marka alanına yaklaştırıldı. Arama, test kullanıcı işlemi ve hesap menüsü sağda kendi alanında kalıyor. Dar ekranlarda menü tek düğmeyle açılıyor.
- Üst çubuktan girilen arama, sorguyu **Faturalar** listesine taşıyor. Liste üzerinden belge türü, durum ve tarih aralığı daraltılabiliyor.
- **Genel bakış** ekranındaki aylık belge sayısı, ciro, onay bekleyenler ve gönderilen faturalar aynı özet alanında izlenebiliyor. Fatura hacmi para birimine göre inceleniyor. **Aksiyon merkezi** kullanıcının yetkisine göre onay, imza, gönderim ve taslak işlerini gösteriyor.
- Ayarlar merkezinde firma, iletişim, ekip, entegrasyon, veri, güvenlik, kişisel görünüm ve sistem sayfaları kendi başlıkları altında düzenlendi. **IP Erişimi**, **Güvenlik politikası** üzerinden de ulaşılabilen ayrı bir ayar sayfası oldu.
- Fatura ayrıntısında belge numarası, e-belge türü, alıcı, durum ve ödenecek tutar üst bölümde yer alıyor. **İş akışı** altında onay, imza ve gönderim işlemleri bulunuyor. **Belge ve doğrulama** alanından önizleme, PDF, UBL, karekod ve kanıt doğrulama işlemlerine erişiliyor. İşlemler belge durumuna ve yetkiye göre gösteriliyor.
- Arayüz açık, koyu ve sistem temalarıyla kullanılabiliyor. **Görünüm** ayarlarında vurgu rengi, tablo yoğunluğu ve sayfa genişliği seçilebiliyor.
- Masaüstünde hesap menüsü tema, güvenlik ve oturum işlemlerini bir araya getiriyor. Platform yöneticisi firma seçicisini kullanarak ilgili firmanın çalışma alanına geçebiliyor.
- Ana menü açılır listeleri dışarı tıklayınca veya klavyede **Esc** tuşuna basılınca kapanıyor. Dar ekranda menü kapatıldığında odak açma düğmesine dönüyor.
- Giriş ekranındaki fotoğraflı arka plan korunurken ana panelin marka, gezinme ve içerik bölgeleri farklı ekran genişliklerinde test edildi. 1366 px kontrolde menü ile arama kutusu birbirine taşmadı.

### Fatura paneli, PDF ve paylaşım

- Geçerli bir VKN girildiğinde cari kayıt sorgusu otomatik başlar. Cari kaydı e-Fatura mükellefi olarak işaretlenmiş alıcı için e-Fatura seçilir ve kullanıcıya bildirilir. Taslak kaydı, devam eden VKN sorgusunun sonucunu bekler. Nihai tüketici ve e-Arşiv akışları desteklenir.
- Firma profilinden web sitesi, faks, ticaret sicil ve MERSİS numarası düzenlenebilir. Fatura satırına ürün kodu girilebilir veya ürün kartından alınabilir. Ürün kodu UBL-TR içinde satıcı ürün kimliği olarak yazılır. Firma bilgileri fatura kaydına kopyalanır.
- A4 PDF ve panel önizlemesinde satıcı ve alıcı blokları, belge künyesi, ürün kodu, KDV oranı ve tutarı, KDV matrahı, tutarın yazıyla karşılığı, banka hesapları ve internet satışları için iade alanı gösterilir. PDF metin eşlemesi Türkçe karakter arama ve çıkarımını iyileştirir.
- A4 çıktıda satıcı ve alıcı bilgileri sol tarafta, belge türü ortada, karekod ve belge künyesi sağda konumlanır. Kalemler çizgili tabloda gösterilir. Tutar özeti, notlar, banka ve iade alanları tablonun altında yer alır.
- İnternet satışı için fatura referansı, mağaza adı, satış adresi, ödeme şekli, ödeme aracısı ve gönderim bilgileri fatura formunda girilebilir. Seçilen firma hesapları banka adı, şube, IBAN ve para birimi sütunlarıyla belgeye alınır.
- Firmanın tanımladığı not şablonları ve banka hesaplarından otomatik eklenecek olanlar taslak açılırken seçilir. Faturayı hazırlayan kişi seçimleri belge bazında değiştirebilir ve serbest metin ekleyebilir.
- Kayıtlı bir ürün seçildiğinde ürün kodu, ad, açıklama, birim, fiyat ve vergi bilgileri fatura satırına gelir. Ürün kodu formda ayrıca düzenlenebilir ve belge ayrıntısında ayrı sütunda görülür.
- PDF künyesinde senaryo, fatura tipi, belge numarası ve düzenleme saati ayrı alanlardadır. Taslak çıktısında taslak numarası ve düzenlenme durumunu belirten ibare kullanılır. Çok kalemli belgelerde kalem tablosu yeni sayfada başlıkla devam eder.
- Türk lirası tutarlar yazıyla gösterilir. Banka hesabı satırları belge notlarından tabloya alınır. İnternet üzerinden satışa ait e-Arşiv satış belgelerinde ad, soyad, ürün, miktar, fiyat, tutar, adres ve imza alanları olan iade tablosu bulunur.
- **Faturayı görüntüle** belgeyi panelde açar. Doğrulanmış bir GİB HTML belgesi arşivde varsa bütünlüğü kontrol edilerek kısıtlı bir önizleme içinde gösterilir.
- **URL Kopyala** fatura detayında kullanılabilir. Dış paylaşıma uygun belgelerde tam belge yetkisi olan kullanıcı, o faturanın süreli PDF bağlantısını kopyalar. Diğer belgelerde bağlantı oturum açmayı ve fatura okuma yetkisini gerektirir. Dış bağlantı istekleri belge durumu ve imzalı XML kanıtı üzerinden yeniden doğrulanır.
- Panelin oluşturduğu müşteri paylaşım bağlantısının varsayılan ömrü 400 gündür. Kopyalama izni olmadığında bağlantı seçilebilir bir alan içinde gösterilir. İç bağlantı üzerinden gelen kullanıcı girişten sonra ilgili fatura sayfasına yönlendirilir.
- Tam belge yetkisiyle bağlantı kopyalama ve **Müşteriye gönder** farklı işlemlerdir. E-posta veya SMS gönderimini başlatmak için fatura oluşturma ya da iletim yetkisi aranır.

### Onay, kullanıcılar ve IP erişimi

- Platform yöneticisi seçtiği firmada onay bekleyen faturaları onaylayabilir. Genel Müdür kendi oluşturduğu faturayı da onaylayabilir. `Workflow:RequireMakerChecker` açıkken istisna rolleri `Workflow:SelfApprovalRoles` ile belirlenir. Onay kuyruğu ile onay uç noktası aynı kuralı kullanır.
- Kullanıcı kaldırma işlemi hesabı pasifleştirir, oturumları geçersiz kılar ve `USER_REMOVED` olayı oluşturur. Hesap geçmişi ve belge ilişkileri korunur.
- **Ayarlar → Güvenlik → IP Erişimi** sayfasında izin ve engel kuralları satır bazında eklenebilir ve kaldırılabilir. IP adresi ve CIDR desteklenir. Genel Müdür kendi firmasının giriş ve kimlik doğrulamalı API kurallarını yönetir. Platform yöneticisi bütün siteyi ve dış belge bağlantılarını kapsayan kuralları yönetir. Yetkisiz site ziyaretleri HTTP 403 yanıtı alır.
- IP Erişimi sayfası mevcut istemci IP'sini, denetimin etkinlik durumunu ve her adresin izin ya da engel durumunu gösterir. Engel kuralları önceliklidir. İzin listesi doluysa erişim listedeki adreslerle sınırlanır. Kullanıcının kendi bağlantısını engelleyecek kayıt reddedilir.
- Site genelindeki IP kuralına takılan tarayıcı isteği resimli 403 sayfası alır. Firma kuralına takılan giriş ve kimlik doğrulamalı API çağrılarında firma erişim hatası gösterilir. Firma kuralları başka firmaların erişimini değiştirmez.
- **Kullanıcılar** sayfasında yetkili kişinin yönetebildiği hesaplarda **Sil** işlemi görünür. Kaldırılan hesabın güvenlik damgası yenilenir ve daha önce aldığı JWT ile işlem yapması engellenir.
- Giriş formu, oturum açılana kadar duyurular tarafından engellenmez. Oturum gerektiren bir fatura veya ayar bağlantısı üzerinden giriş tamamlandığında ilgili sayfaya dönülür. Demo firma hesapları üretimde giriş ve JWT doğrulamasında engellenir.

### Denetim izi ve belge bütünlüğü

- Denetim izi kullanıcı kodu, işlem anındaki rol, işlem kodu, kayıt kimliği, tarih aralığı ve sonuçla süzülebilir. Kayıt ayrıntısında IP ve iz kimliği görülebilir.
- Kimlik doğrulamalı API istekleri, denetim ekranına erişim, IP kuralı değişiklikleri ve dış belge erişimleri izlenir. Fatura onay olayında işlemin kişinin kendi faturası için yapılıp yapılmadığı kaydedilir.
- Denetim ekranında bir kaydın ayrıntısından işlem verileri, kayıt kimliği, IP adresi ve iz kimliği incelenebilir. Liste geçmişe doğru sayfalanır. **Hash zincirini doğrula** işlemi kayıtların birbirine bağlı özetlerini denetler.
- Fatura onayı, PDF indirme, belge önizleme ve bağlantı oluşturma ayrı işlem kodlarıyla izlenebilir. Denetim araması, örneğin belirli bir yöneticinin yalnızca başarılı onaylarını tarih aralığı içinde bulmak için birden fazla filtreyi birlikte kullanabilir.
- Kanıt kasası dosya okunurken manifestonun belge ve firma kimliğini, dosya boyutunu ve SHA-256 özetini kontrol eder. Belge ayrıntısındaki **Kanıt doğrula** işlemi manifestoların birbirine bağlı zincirini de denetler.
- Fatura listeleme ve sayfalama, ayrıntı görünümündeki kişisel verileri maskeleme yaklaşımıyla uyumlu hale getirildi. PDF, UBL ve karekod uç noktaları tam belge yetkisiyle korunur.

### GİB Portal ve üretim hazırlığı

- Portal taslağı oluşturma yanıtındaki ETTN saklanır. Mutabakatta ETTN, alıcı ve tarih dikkate alınır. Önceki gönderim belirsizse yeniden kontrol aynı taslağı arar.
- Portal listesinde kullanılan ETTN, alıcı kimliği ve belge tarihi alanlarının alternatif adları da okunur. Tarih, desteklenen biçimlerden birine uyduğunda belge günüyle karşılaştırılır. Birden fazla aday varsa operatöre bir eşleşme uyarısı gösterilir.
- Daha önce gönderilmiş taslak için **Taslağı tekrar kontrol et** adımı sunulur. Aynı GİB hesabı ve ortamı kullanılır. Belge hâlâ doğrulanamıyorsa **Portal belgeleri** ekranına geçilebilir.
- Portal ZIP indirmesi akış üzerinden 32 MiB ile sınırlanır. Arşivden okunan XML ve HTML dosyalarının her biri için 20 MiB sınırı uygulanır. Arşiv indirilemezse GİB onay bilgisi korunur ve imzalı kanıtın alınması mutabakat aşamasında kalır.
- Üretim hazırlığı yürürlükteki onaylı e-Fatura ve e-Arşiv yönlendirme kurallarını, GİB portal izinlerini, sertifika güvenini ve UBL doğrulama paketini denetler. Bir koşul eksikse `/health/ready` HTTP 503 döner ve sistem sayfası nedenini gösterir.
- **Sistem durumu** ekranındaki hazırlık sonucu SQL erişimi, veritabanı başlangıcı ve canlı e-belge koşullarının nerede takıldığını gösterir. Operatör kontrolü uygulamanın HTTP isteklerine yanıt verip vermediğini gösteren `/health` ucundan ayrıdır.

### Entegrasyonlar ve performans

- Trendyol fatura bağlantısı gönderiminde `409` yalnızca ilgili paket üzerindeki URL aynıysa başarılı sayılır.
- Trendyol gönderimi çakıştığında sipariş paketi sağlayıcıdan yeniden sorgulanır. Paket ya da URL eşleşmezse sonuç başarısız olarak kaydedilir ve teslimat geçmişinde incelenebilir.
- Pazaryeri siparişlerinde `Retry-After` bekleme zamanı saklanır. Başarısız siparişler şifreli aktarım verisiyle tekrar denenebilir. Uzun harici kimlikler kısaltılmadan önce tam kimlikten anahtar türetilir. Mikro ihracat manuel incelemede kalır.
- Sağlayıcı yanıtları sınırlı okunur. Teslimat ekranı ham istek ve yanıt gövdeleri yerine durum, HTTP kodu, deneme sayısı ve ilgili kayıt bilgilerini gösterir. Aktarım yüklerinin korunması için Data Protection anahtarları kullanılır.
- **Entegrasyonlar → Kayıtlar** ekranındaki yeniden işleme düğmesi, mevcut sipariş kaydı üzerinden çalışır. Sonraki deneme zamanı kayıtta görülebilir. Mikro ihracat kayıtları inceleme durumunda kalır.
- WHMCS modülü ödenmiş faturaların satır ve müşteri bilgilerini mevcut aktarım kuyruğu üzerinden işler. Müşteri belgesinde kaynak etiketi yerine fatura referansı kullanılır.
- Entegrasyon, geliştirici dokümanı ve mesajlaşma ekranlarının JavaScript paketleri ilk panel yüküne eklenmek yerine sayfa açıldığında indirilir. Üretim varlıkları sıkıştırılır ve dosya adına bağlı önbellekle sunulur. Giriş ekranının arka plan görseli yaklaşık 2,46 MB'tan 228 KB'a indirildi.
- Uygulama kökü ve `/app/` aynı paneli sunar. Panelin giriş HTML'i yeniden istenir. Adı içeriğe göre üretilen JS ve CSS dosyaları uzun süre önbellekte tutulabilir.

### Kurulum ve yükseltme

- Uygulama sürümü 6.0.0, REST yolu `/api/v1`, SQL şema sürümü 5'tir. Şema 5 zaten kuruluysa şema değişikliği gerekmez. Şema 4 için `06_Sema5.sql` kullanılır.
- Temiz Windows/SQL Server kurulumu ve Plesk için ayrı birleşik SQL betikleri bulunur. Bunlar `Build-Kurulum.ps1` ile üretilebilir ve karşılaştırılabilir.
- SQL bağlantı bilgileri, JWT imza anahtarı ve ilk yönetici parolası yerel kullanıcı sırlarından veya sunucu ortamından sağlanır. Kurulum ve manuel yayın komutları [install.md](install.md) dosyasındadır.
- Sürüm geçişinde SQL yedeğiyle birlikte kanıt kasası, Data Protection anahtarları ve `App_Data` içindeki IP erişim kuralları korunur. Anahtarlar mevcut fatura bağlantıları ve şifreli aktarım yüklerinin okunması için gereklidir.
- Temiz kaynakta Release derlemesi, yerel ve Plesk SQL kurulumları, şema 4 → 5 yükseltmesi ve 41 oturumlu panel rotası kontrol edildi. Canlı GİB imza, WHMCS ve pazaryeri kabul testleri ilgili üretim ortamlarında yapılır.

#### Yükseltme sonrası ilk kontroller

1. `/health/ready` sonucunu ve **Ayarlar → Sistem durumu** ekranını kontrol edin. Üretimde yürürlükteki onaylı kurallar ve UBL doğrulama paketi gerekir.
2. Platform yöneticisi ve firma yöneticisi oturumlarıyla **IP Erişimi** sayfasındaki kapsamı ve eski kuralların korunduğunu doğrulayın.
3. Firma yetkili bir hesabıyla onay kuyruğunu açın. Örnek bir faturada önizlemeyi, PDF çıktısını, belgeye uygun **URL Kopyala** davranışını ve denetim kayıtlarını kontrol edin.
4. Üretime bağlı entegrasyonlarda bekleyen siparişleri ve giden teslimatları gözden geçirin. GİB ve müşteri teslimatının kabulünü ilgili ortamlarda tamamlayın.

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
