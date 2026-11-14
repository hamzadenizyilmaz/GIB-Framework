# Güvenlik politikası

## Desteklenen sürümler

| Sürüm | Güvenlik güncellemesi |
|---|---|
| 6.0.0 | Güncel güvenlik düzeltmeleri |
| 5.x ve öncesi | 6.0.0'a yükseltin |

## Açık bildirimi

Güvenlik açıklarını herkese açık issue olarak paylaşmayın. Bildirimleri GitHub üzerindeki
[özel güvenlik bildirimi](https://github.com/hamzadenizyilmaz/GIB-Framework/security/advisories/new) formu ile ya da depo
sahibine doğrudan iletin.

Bildirimde aşağıdaki bilgilere yer verin:

- Etkilenen sürüm ve bileşen
- Sorunun koşulları ve olası etkisi
- Gerekli günlük veya ekran görüntüsünün kişisel verileri ve sırları maskelenmiş kopyası

## Süreç

| Aşama | Hedef süre |
|---|---|
| Bildirimin alındığının teyidi | 3 iş günü |
| İlk değerlendirme | 7 iş günü |
| Düzeltme ve sürüm yayını | Önem derecesine göre |

## Kapsam

- Kimlik doğrulama, yetkilendirme ve rol yükseltme
- Firma (kiracı) veri izolasyonu
- Webhook uç noktaları ve imza doğrulaması
- Pazaryeri ve e-ticaret entegrasyonları
- Fatura paylaşım bağlantıları
- E-posta ve SMS şablonlarında içerik enjeksiyonu
- İmza, kanıt kasası ve denetim izi bütünlüğü

## Yayın ve sır yönetimi

- Bağlantı parolasını ve JWT anahtarını dağıtım ortamından sağlayın. Ortama özel ayar dosyalarını, Data Protection anahtarlarını, günlükleri ve kanıt dosyalarını paylaşılan arşivlere eklemeyin.
- Data Protection ve kanıt dizinlerine uygulama havuzu kimliğinin ve yetkili operatörlerin erişimini tanımlayın. Sürüm değişiminde mevcut anahtar dizinini koruyun. Bir sır ifşa olduysa ilgili sırları değiştirin ve eski erişimi iptal edin.
- SQL şema yükseltmesini gerekli RLS yetkisi olan yönetici hesabıyla çalıştırın. Üretimde HTTPS, doğrulanmış SQL sertifika zinciri, bilinen proxy adresleri ve izinli giden bağlantı hedefleri kullanın.
