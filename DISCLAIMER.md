# Sorumluluk Reddi Beyanı

> **ÖNEMLİ UYARI:** GIB Framework, T.C. Gelir İdaresi Başkanlığına (GİB) ait veya GİB tarafından onaylanmış resmî bir ürün değildir. Proje, herhangi bir garanti verilmeden sunulur. Gerçek mükellef verileriyle veya canlı ortamda kullanılmadan önce teknik, hukuki ve mali uygunluk kontrolleri yapılmalıdır.

Bu belge, [GIB Framework](https://github.com/hamzadenizyilmaz/GIB-Framework) projesinin kullanımı, kurulumu, değiştirilmesi, dağıtılması ve üçüncü taraf sistemlerle bütünleştirilmesiyle ilgili önemli uyarıları ve sorumluluk sınırlarını açıklar. Belge, yürürlükteki mevzuatın ve depodaki [GNU Genel Kamu Lisansı sürüm 3 (GPL-3.0)](./LICENSE) hükümlerinin yerine geçmez.

---

## 1. Bağımsız Proje ve Resmî Kurumlarla İlişki

GIB Framework, **Hamza Deniz Yılmaz tarafından geliştirilen bağımsız bir açık kaynak yazılım projesidir.**

- **T.C. Gelir İdaresi Başkanlığı (GİB)** veya **T.C. Hazine ve Maliye Bakanlığı** tarafından geliştirilmiş, yayımlanmış, sertifikalandırılmış, onaylanmış ya da resmî olarak desteklenmiş değildir.
- Projede geçen “GİB”, “e-Fatura”, “e-Arşiv”, “UBL-TR” ve benzeri adlar, ilgili kurumları, belge türlerini ve teknik süreçleri tanımlamak amacıyla kullanılmaktadır. Bu adların kullanılması resmî yetki, ortaklık veya kurumsal temsil anlamına gelmez.
- Yazılımın indirilmesi, kurulması veya kullanılması, kullanıcıya **GİB adına işlem yapma, özel entegratör olarak faaliyet gösterme veya mevzuatın öngördüğü yetki ve izinlere sahip olma hakkı vermez.**
- Projeye destek veren üçüncü taraf kuruluşların desteği, GİB'in veya başka bir kamu kurumunun onayı olarak yorumlanamaz.

## 2. Teknik Kapsam, Portal ve API Entegrasyonları

GIB Framework; .NET/ASP.NET Core, React ve SQL Server tabanlı, e-belge iş akışları ile çeşitli haricî sistem entegrasyonlarını hedefleyen bir platformdur. Proje; fatura hazırlama, doğrulama, onaylama, imzalama, gönderme, sorgulama, arşivleme, PDF/XML üretimi ve REST API gibi işlevler içerebilir.

- **Bir özelliğin kaynak kodda veya belgelerde bulunması, onun tüm senaryolarda çalıştığını, test edildiğini, yasal olarak kullanılabileceğini ya da üretime hazır olduğunu göstermez.**
- GİB portalı, test ortamı, iletişim yöntemleri, oturum açma mekanizmaları, servis adresleri, yanıt biçimleri veya ilgili teknik kurallar önceden bildirim yapılmaksızın değişebilir. Bu değişiklikler entegrasyonları bozabilir veya geçici/kalıcı olarak kullanılamaz hâle getirebilir.
- GİB veya başka bir hizmet sağlayıcının arayüzüne teknik olarak erişilebilmesi, o arayüzün her kullanım biçimi için resmî izin verildiği anlamına gelmez. **İlgili erişim ve kullanım şartlarını inceleme, gerekli yetki ve izinleri temin etme sorumluluğu kullanıcıya aittir.**
- e-Fatura, e-Arşiv ve diğer e-belge süreçlerinin tabi olduğu farklı teknik ve hukuki koşullar birbirinin yerine geçmez. Desteklenen bir e-Arşiv işlem akışı, diğer bütün e-belge türleri için canlı gönderim yetkisi veya uygunluğu anlamına gelmez.
- API dokümanları, örnek istekler ve sistem yanıtları resmî GİB API dokümantasyonu ya da kesintisiz hizmet taahhüdü değildir.

## 3. Yazılım “Olduğu Gibi” Sunulur; Garanti Verilmez

Yazılım, **GPL-3.0 lisansının 15, 16 ve 17. bölümleriyle uyumlu olarak, uygulanabilir hukukun izin verdiği ölçüde “OLDUĞU GİBİ” (AS IS) ve “MEVCUT OLDUĞU ŞEKLİYLE”** sunulmaktadır.

Proje sahibi ve katkıda bulunanlar, kanunen aksinin zorunlu olduğu hâller dışında, aşağıdaki konularda açık veya zımni garanti vermez:

- Hatasız, kesintisiz, güvenli veya belirli bir amaca uygun çalışma;
- Ticari elverişlilik, doğruluk, eksiksizlik ve üçüncü taraf haklarının ihlal edilmemesi;
- Üretilen fatura, UBL-TR XML, PDF, elektronik imza, karekod veya diğer belgelerin her zaman yürürlükteki standartlara ve mevzuata uygunluğu;
- Vergi hesaplarının, belge türü seçiminin, iş akışlarının, durum bildirimlerinin veya gönderim sonuçlarının doğruluğu;
- GİB ve üçüncü taraf servislerle sürekli uyumluluk;
- Verilerin kayıpsız saklanması, aktarılması, arşivlenmesi veya geri yüklenmesi;
- Her güvenlik açığının tespit edilmesi veya her sorun için düzeltme, destek ya da güncelleme sağlanması.

## 4. Vergisel, Mali ve Hukuki Yükümlülükler

**Yazılımın kullanımına bağlı vergisel ve mali işlemlerin doğrulanması, yetkilerin sağlanması ve yasal yükümlülüklerin yerine getirilmesi ilgili mükellef, işletme ve/veya yetkili kullanıcıların sorumluluğundadır.**

Bunlara, sınırlı olmamak üzere, aşağıdakiler dahildir:

- Düzenlenen veya aktarılan belgelerde alıcı, satıcı, VKN/TCKN, tutar, tarih, açıklama ve diğer bilgilerin doğruluğu;
- KDV, ÖTV, tevkifat, istisna, stopaj ve diğer vergi kurallarının doğru uygulanması;
- Belge numaralandırması, elektronik imza/mali mühür, gönderim, iptal, itiraz ve düzeltme süreçleri;
- E-belgelerin mevzuata uygun şekilde saklanması, ibraz edilmesi ve gerekiyorsa raporlanması;
- Vergi Usul Kanunu, ilgili genel tebliğler, GİB teknik kılavuzları ve diğer güncel düzenlemelere uyum;
- Muhasebe kayıtlarının, resmî GİB durumlarının ve alıcıya yapılan bildirimlerin karşılaştırılması ve mutabakatı.

**Canlı kullanımdan önce yetkili bir serbest muhasebeci mali müşavir (SMMM), yeminli mali müşavir (YMM) ve gerektiğinde hukuk danışmanı ile uygunluk değerlendirmesi yapılması kuvvetle önerilir.** Yazılım, profesyonel mali veya hukuki danışmanlık hizmetinin yerini almaz.

## 5. Test Ortamı, Canlı Kullanım ve İşlem Onayı

- Geliştirme ve ilk doğrulama işlemleri **izole test ortamında, sentetik verilerle** gerçekleştirilmelidir.
- Üretim ortamına geçmeden önce ilgili GİB erişim yetkileri, imza sertifikaları, UBL-TR doğrulamaları, veritabanı yapısı, yedekleme, erişim kontrolleri ve dış sistem izinleri bağımsız biçimde kontrol edilmelidir.
- Kullanıcılar; taslak, onay, imza ve gönderim aşamalarının sonuçlarını yalnızca uygulama ekranındaki durumla değil, **yetkili resmî sistemdeki kayıtlar ve belgelerle** de doğrulamalıdır.
- Yanlış yapılandırma, otomatik iş akışı veya kullanıcı hatası nedeniyle taslak yerine gerçek belge oluşturulması, istenmeyen gönderim, mükerrer işlem, hatalı iptal ya da yanlış alıcıya bildirim gibi risklere karşı gerekli denetimler kurulmalıdır.
- README, örnek yapılandırmalar, demo hesapları veya sağlık kontrolü uç noktaları tek başına üretim için uygunluk onayı sayılmaz. **Kamuya açık demo ortamlarına gerçek müşteri bilgisi, şifre, token, mali mühür bilgisi veya başka hassas veri girilmemelidir.**

## 6. Kişisel Verilerin Korunması ve Bilgi Güvenliği

GIB Framework; firma, müşteri, kullanıcı, fatura, işlem geçmişi, erişim, entegrasyon ve benzeri verileri yapılandırmaya bağlı olarak **veritabanında, dosya sisteminde, günlüklerde veya entegre haricî hizmetlerde işleyebilir**. Bu nedenle yazılımın hiçbir veriyi kaydetmediği veya üçüncü taraflara iletmediği varsayılamaz.

- Kullanıcı ve sistem işletmecisi, kendi somut faaliyetleri bakımından **6698 sayılı Kişisel Verilerin Korunması Kanunu (KVKK)** ile uygulanabilir diğer veri koruma mevzuatından doğan yükümlülüklerini değerlendirmelidir.
- Veri sorumlusu, veri işleyen ve diğer tarafların hukuki rolleri; kurulum, barındırma, hizmet ilişkisi ve fiilî veri işleme faaliyetlerine göre belirlenir. Bu belge, söz konusu rolleri kendiliğinden belirlemez veya değiştirmez.
- VKN/TCKN, kişisel iletişim bilgileri, faturalar, API anahtarları, JWT ve oturum belirteçleri, SMTP/SMS kimlik bilgileri, özel anahtarlar ve imza sertifikaları korunmalıdır.
- Güvenli yapılandırma, HTTPS/TLS, güçlü kimlik doğrulama, en az yetki, ağ kısıtlamaları, anahtar yönetimi, erişim günlüğü, sırların maskelemesi, yedekleme, saklama ve imha politikaları kurulumun sorumluları tarafından sağlanmalıdır.
- E-posta, SMS, pazaryeri, e-ticaret, webhook ve paylaşım bağlantısı işlevleri veri aktarımına yol açabilir. İlgili aktarımın hukuki dayanağı, alıcıları, saklama koşulları ve güvenliği ayrıca değerlendirilmelidir.
- Çok firmalı kullanımda veri izolasyonu, erişim kontrolü ve yetki ayrımı bağımsız güvenlik testleriyle doğrulanmalıdır. Projede bu amaçla kontrollerin bulunması, sıfır veri sızıntısı garantisi vermez.

Güvenlik açığı bildirimleri için [SECURITY.md](./SECURITY.md) dosyasına başvurun; hassas güvenlik bilgilerini herkese açık issue kayıtlarında paylaşmayın.

## 7. Üçüncü Taraf Servisler ve Bağımlılıklar

Yazılım, yapılandırmaya göre GİB sistemleri, SQL Server, SMTP sağlayıcıları, SMS servisleri, pazaryerleri, e-ticaret platformları, hosting/faturalama sistemleri ve diğer API'lerle iletişim kurabilir.

- Bu sağlayıcıların hizmet kesintileri, fiyatlandırmaları, kota/rate-limit uygulamaları, erişim kuralları, API değişiklikleri ve veri işleme koşulları proje sahibinin kontrolü dışındadır.
- Örneğin Trendyol, Hepsiburada, n11, WooCommerce, Shopify, WHMCS, WISECP veya Netgsm ile bağlantı desteği, bu kuruluşlarca resmî onay ya da iş ortaklığı bulunduğu anlamına gelmez.
- Kullanıcı, kullandığı her hizmete ilişkin sözleşmeleri, lisansları, API kullanım koşullarını ve gerekli izinleri ayrıca yerine getirmelidir.
- Açık kaynak ve ticari bağımlılıkların güvenlik güncellemeleri, lisans koşulları ve uyumluluk denetimleri işletim ve dağıtım süreçlerinde takip edilmelidir.

## 8. Sorumluluğun Sınırlandırılması

**Uygulanabilir hukukun izin verdiği azami ölçüde**, proje sahibi ve katkıda bulunanlar; yazılımın kullanımı veya kullanılamaması nedeniyle oluşabilecek doğrudan, dolaylı, özel, arızi, sonuçsal veya ekonomik zararlardan sorumlu tutulmaz. Bu kapsamda özellikle aşağıdaki riskler yer alabilir:

- Eksik, hatalı, mükerrer veya mevzuata aykırı e-belge düzenlenmesi; vergi cezaları, faizler ve düzeltme masrafları;
- Yanlış kişiye belge ya da bildirim gönderilmesi, gizlilik ihlali ve yetkisiz erişim;
- GİB portalı, API, imza, mali mühür, oturum veya token işlemlerinin başarısız olması;
- Veritabanı bozulması, veri kaybı, arşiv eksikliği, yedekten dönememe veya hizmet kesintisi;
- Üçüncü taraf altyapı, modül, bağımlılık, entegrasyon ve hizmet sağlayıcılardan kaynaklanan sorunlar;
- Kâr, gelir, müşteri, itibar veya iş fırsatı kaybı.

**Bu madde, yürürlükteki emredici hukuk hükümleri uyarınca sınırlandırılması veya kaldırılması mümkün olmayan sorumlulukları ortadan kaldırmaz.** Her olayda sorumluluğun kapsamı somut koşullara ve uygulanabilir hukuka göre belirlenir.

## 9. Lisans, Telif Hakları ve Yeniden Dağıtım

GIB Framework deposunun [LICENSE](./LICENSE) dosyasında **GNU General Public License, Version 3 (GPL-3.0)** metni yer almaktadır. Yazılımın kopyalanması, değiştirilmesi ve yeniden dağıtılması bakımından ilgili lisansın koşulları geçerlidir.

- Bu sorumluluk reddi beyanı **GPL-3.0 lisansının yerine geçmez, lisansın tanıdığı hakları kaldırmaz veya ek lisans kısıtlaması getirmez.**
- Yazılımı değiştiren veya yeniden dağıtanlar, uygulanabilir lisans koşullarına ve üçüncü taraf bileşenlerin lisanslarına uymalıdır.
- GİB'e, diğer kurumlara, ticari markalara ve üçüncü taraflara ait adlar veya işaretler üzerindeki haklar ilgili hak sahiplerine aittir.

Proje sahibi: **Hamza Deniz Yılmaz**  
Depo: https://github.com/hamzadenizyilmaz/GIB-Framework

## 10. Belgenin Güncellenmesi ve Uygulanacak Hukuk

Bu beyan, proje kapsamının, kullanılan servislerin veya mevzuatın değişmesine bağlı olarak güncellenebilir. Güncel metin için proje deposuna başvurulmalıdır. Değişiklikler geçmiş kullanıma otomatik olarak yeni yükümlülükler yüklediği şeklinde yorumlanmamalıdır.

Bu belgenin yorumlanmasında, uygulanabildiği ölçüde **Türkiye Cumhuriyeti hukuku** esas alınır. Emredici kanunlar, kanunlar ihtilafı kuralları ve yetkili mahkemeye ilişkin zorunlu hükümler saklıdır.

Bu beyan yalnızca genel bilgilendirme ve risk açıklaması amacı taşır; tek başına her kullanıcıyla ayrı bir sözleşme kurulduğu veya kanuni haklardan feragat edildiği anlamına gelmez.

---

## Kısa Özet

**GIB Framework resmî bir GİB yazılımı değildir.** Yazılım, **GPL-3.0 kapsamında ve garanti verilmeksizin** sunulur. Canlı kullanıma geçmeden önce mevzuat, güvenlik, veri koruma, imza, entegrasyon ve fatura işlemleri bağımsız olarak doğrulanmalıdır. Uygulanabilir hukuk çerçevesinde, kendi kullanım ve işletim kararlarından doğan yükümlülükler ilgili kullanıcılara ve işletmecilere aittir.

**İlgili belgeler:** [README.md](./README.md) · [LICENSE](./LICENSE) · [SECURITY.md](./SECURITY.md) · [install.md](./install.md)

> **Hukuki not:** Bu metin genel amaçlı bir taslaktır. Kamuya açık yayımlama ve özellikle ticari/canlı kullanım öncesinde Türkiye'de bilişim, sözleşme ve vergi hukuku alanında yetkin bir hukukçunun incelemesi önerilir.
