using GIBFramework.Models.Integrations;

namespace GIBFramework.Services.Integrations;

public sealed record IntegrationField(string Key, string Label, string Type, bool Required, string? Default, string? Help, IReadOnlyList<string>? Options = null);

public sealed record IntegrationKindInfo(
    IntegrationKind Kind,
    string Label,
    string Category,
    string Icon,
    string Description,
    bool Inbound,
    string SignatureHeader,
    IReadOnlyList<IntegrationField> Fields,
    IReadOnlyList<string> Steps);

public static class IntegrationCatalog
{
    public const string SigningSecret = "signingSecret";
    public const string ConsumerKey = "consumerKey";
    public const string ConsumerSecret = "consumerSecret";
    public const string WebhookUrl = "webhookUrl";
    public const string AutoSubmit = "autoSubmit";
    public const string SaveCustomer = "saveCustomer";

    private static readonly IntegrationField Outbound = new(WebhookUrl, "Bildirim adresi (webhook URL)", "url", false, null,
        "Seçilen fatura olayları bu adrese imzalı JSON olarak POST edilir. Boş bırakılırsa olay gönderilmez.");

    private static readonly IntegrationField Auto = new(AutoSubmit, "Gelen siparişi otomatik onaya gönder", "bool", false, "true",
        "Açıksa sipariş taslak olarak oluşturulup doğrulamadan geçirilir ve onay kuyruğuna düşer.");

    private static readonly IntegrationField Save = new(SaveCustomer, "Alıcıyı cari kartlara kaydet", "bool", false, "true", null);

    private static readonly IntegrationField Vat = new("defaultVatRate", "Vergisi belirtilmeyen satırlarda KDV oranı (%)", "select", false, "20", null, ["0", "1", "10", "20"]);

    private static readonly IntegrationField SendLink = new("sendInvoiceLink", "Fatura düzenlenince PDF fatura linkini pazaryerine gönder", "bool", false, "true", null);

    private static readonly IntegrationField Poll = new("pollMinutes", "Sipariş kontrol aralığı (dakika)", "select", false, "15", null, ["5", "10", "15", "30", "60"]);

    private static readonly IntegrationField Marketplace = new("autoSubmit", "Gelen siparişi otomatik onaya gönder", "bool", false, "true", null);

    public static IReadOnlyList<IntegrationKindInfo> All { get; } =
    [
        new(IntegrationKind.Trendyol, "Trendyol", "Pazaryeri", "bag-heart",
            "Trendyol sipariş paketleri (Sipariş V2) düzenli aralıklarla çekilip faturalanır; fatura düzenlenince PDF fatura linki Trendyol'a gönderilir.",
            false, string.Empty,
            [
                new("sellerId", "Satıcı ID", "text", true, null, "Satıcı Paneli > Hesap Bilgilerim > Entegrasyon Bilgileri."),
                new("apiKey", "API Key", "secret", true, null, null),
                new("apiSecret", "API Secret", "secret", true, null, null),
                new("integratorName", "Entegratör adı (User-Agent)", "text", false, "SelfIntegration", "İstekler \"{Satıcı ID} - {Entegratör adı}\" User-Agent değeriyle gönderilir."),
                new("environment", "Ortam", "select", false, "prod", "prod: canlı, stage: test (IP yetkisi gerekir).", ["prod", "stage"]),
                new("statuses", "Faturalanacak paket durumları", "text", false, "Created", "Virgülle ayırın: Created, Picking, Invoiced, Shipped, Delivered."),
                new("initialDays", "İlk eşitlemede geriye dönük gün", "select", false, "3", null, ["1", "3", "7", "13"]),
                Poll, SendLink, Marketplace, Save, Vat, Outbound,
            ],
            [
                "Trendyol Satıcı Paneli > Hesap Bilgilerim > Entegrasyon Bilgileri sayfasından Satıcı ID, API Key ve API Secret değerlerini alın.",
                "Bilgileri kaydedip entegrasyonu etkinleştirin; siparişler seçilen aralıkla otomatik çekilir, \"Şimdi eşitle\" ile anında çekebilirsiniz.",
                "Fatura düzenlendiğinde PDF linki Trendyol'a iletilir; link 10 yıl erişilebilir kalır.",
            ]),
        new(IntegrationKind.Hepsiburada, "Hepsiburada", "Pazaryeri", "bag",
            "Faturası yüklenmemiş Hepsiburada paketleri çekilip faturalanır; fatura düzenlenince PDF fatura linki pakete eklenir.",
            false, string.Empty,
            [
                new("merchantId", "Merchant ID", "text", true, null, "Hepsiburada tarafından iletilen satıcı kimliği (GUID)."),
                new("username", "API kullanıcı adı", "text", false, null, "Boş bırakılırsa Merchant ID kullanılır."),
                new("password", "Servis anahtarı / API şifresi", "secret", true, null, "Satıcı Paneli > Bilgilerim > Entegrasyon > Entegratör Bilgileri > Servis Anahtarı."),
                new("userAgent", "Entegratör kullanıcı adı (User-Agent)", "text", true, null, "Hepsiburada isteklerinde zorunlu User-Agent değeri."),
                new("environment", "Ortam", "select", false, "prod", "prod: canlı, sit: test.", ["prod", "sit"]),
                Poll, SendLink, Marketplace, Save, Vat, Outbound,
            ],
            [
                "Hepsiburada Satıcı Paneli > Bilgilerim > Entegrasyon > Entegratör Bilgileri bölümünden servis anahtarını alın.",
                "Merchant ID, servis anahtarı ve entegratör kullanıcı adını kaydedip entegrasyonu etkinleştirin.",
                "Faturası yüklenmemiş paketler çekilir; fatura düzenlendiğinde PDF linki paket numarasına eklenir.",
            ]),
        new(IntegrationKind.N11, "n11", "Pazaryeri", "basket",
            "n11 sipariş paketleri (REST) düzenli aralıklarla çekilip faturalanır; fatura düzenlenince PDF fatura linki n11'e kaydedilir.",
            false, string.Empty,
            [
                new("appKey", "App Key", "secret", true, null, "n11 Mağaza Paneli > Hesabım > API Hesapları."),
                new("appSecret", "App Secret", "secret", true, null, null),
                new("status", "Faturalanacak paket durumu", "select", false, "Created", null, ["Created", "Picking", "Shipped", "Delivered"]),
                new("initialDays", "İlk eşitlemede geriye dönük gün", "select", false, "3", null, ["1", "3", "7", "14"]),
                Poll, SendLink, Marketplace, Save, Vat, Outbound,
            ],
            [
                "n11 Mağaza Paneli > Hesabım > API Hesapları bölümünden App Key ve App Secret oluşturun.",
                "Bilgileri kaydedip entegrasyonu etkinleştirin; seçilen durumdaki paketler otomatik çekilir.",
                "Fatura düzenlendiğinde HTTPS ve .pdf ile biten fatura linki n11'e kaydedilir.",
            ]),
        new(IntegrationKind.WooCommerce, "WooCommerce", "E-ticaret", "cart3",
            "WordPress / WooCommerce mağazanızdaki ödenen siparişler otomatik faturalanır; fatura düzenlenince siparişe müşteriye görünen not eklenir.",
            true, "X-WC-Webhook-Signature",
            [
                new("storeUrl", "Mağaza adresi", "url", true, null, "Örn. https://magazam.com"),
                new(ConsumerKey, "REST API Consumer key", "secret", false, null, "WooCommerce > Ayarlar > Gelişmiş > REST API (Okuma/Yazma). Siparişe fatura notu eklemek için gerekir."),
                new(ConsumerSecret, "REST API Consumer secret", "secret", false, null, null),
                new(SigningSecret, "Webhook gizli anahtarı", "generated", true, null, "WooCommerce webhook ayarındaki \"Gizli\" alanına yapıştırın."),
                new("triggerStatuses", "Faturalanacak sipariş durumları", "text", false, "processing,completed", "Virgülle ayırın (processing, completed …)."),
                new("taxIdMetaKey", "TCKN / VKN sipariş meta anahtarı", "text", false, null, "Ödeme sayfasına eklediğiniz TC kimlik / vergi no alanının meta anahtarı. Boşsa 11111111111 kullanılır."),
                new("taxOfficeMetaKey", "Vergi dairesi meta anahtarı", "text", false, null, null),
                new("addOrderNote", "Fatura düzenlenince siparişe müşteri notu ekle", "bool", false, "true", null),
                Auto, Save, Vat, Outbound,
            ],
            [
                "WooCommerce > Ayarlar > Gelişmiş > Webhook'lar > Webhook ekle.",
                "Konu: \"Sipariş güncellendi\", Teslimat URL'si: aşağıdaki gelen adres, Gizli: webhook gizli anahtarı, API sürümü: WP REST API v3.",
                "Durumu \"Etkin\" yapıp kaydedin; ilk ping isteği kayıtlara \"Yok sayıldı\" olarak düşer.",
                "Siparişe fatura notu eklemek için REST API anahtarı oluşturup Consumer key/secret alanlarını doldurun.",
            ]),
        new(IntegrationKind.Shopify, "Shopify", "E-ticaret", "bag-check",
            "Shopify mağazanızda ödemesi alınan siparişler otomatik faturalanır.",
            true, "X-Shopify-Hmac-Sha256",
            [
                new("shopDomain", "Mağaza alan adı", "text", false, null, "Örn. magazam.myshopify.com"),
                new(SigningSecret, "Webhook imzalama anahtarı", "secret", true, null, "Shopify Yönetici > Ayarlar > Bildirimler > Webhook'lar bölümündeki imza anahtarı (veya uygulamanızın client secret'ı)."),
                new("taxIdAttribute", "TCKN / VKN sipariş özelliği adı", "text", false, null, "Sepette toplanan not özelliğinin adı (note_attributes). Boşsa 11111111111 kullanılır."),
                Auto, Save, Vat, Outbound,
            ],
            [
                "Shopify Yönetici > Ayarlar > Bildirimler > Webhook'lar > Webhook oluştur.",
                "Olay: \"Sipariş ödemesi\" (orders/paid), Biçim: JSON, URL: aşağıdaki gelen adres.",
                "Sayfada gösterilen imza anahtarını \"Webhook imzalama anahtarı\" alanına kaydedin.",
            ]),
        new(IntegrationKind.Whmcs, "WHMCS", "Hosting faturalama", "hdd-network",
            "WHMCS'te ödenen faturalar (InvoicePaid) için e-Arşiv / e-Fatura otomatik oluşturulur.",
            true, "X-GibFramework-Signature",
            [
                new(SigningSecret, "İmza anahtarı", "generated", true, null, "Kanca dosyasına otomatik eklenir."),
                new("taxIdField", "TCKN / VKN müşteri özel alanı adı", "text", false, "TC Kimlik / Vergi No", "WHMCS müşteri özel alanının adı; yoksa WHMCS Tax ID alanı kullanılır."),
                Auto, Save, Vat, Outbound,
            ],
            [
                "Aşağıdaki kanca dosyasını indirip WHMCS kurulumunuzda includes/hooks/gibframework.php olarak kaydedin.",
                "WHMCS bir faturayı \"Ödendi\" yaptığında fatura satırları ve müşteri bilgileri imzalı olarak GIB Framework'e gönderilir.",
                "Gelen istekler bu sayfadaki kayıtlarda görünür.",
            ]),
        new(IntegrationKind.WiseCp, "WISECP", "Hosting faturalama", "hdd-stack",
            "WISECP'te fatura \"ödendi\" durumuna geçtiğinde e-Arşiv / e-Fatura otomatik oluşturulur.",
            true, "X-GibFramework-Signature",
            [
                new(SigningSecret, "İmza anahtarı", "generated", true, null, "Kanca dosyasına otomatik eklenir."),
                Auto, Save, Vat, Outbound,
            ],
            [
                "Aşağıdaki kanca dosyasını indirip WISECP kurulumunuzda coremio/hooks/gibframework.php olarak kaydedin.",
                "Kanca, action:invoice.status_changed olayında fatura \"paid\" durumuna geçtiğinde çalışır.",
                "Gelen istekler bu sayfadaki kayıtlarda görünür.",
            ]),
        new(IntegrationKind.Webhook, "Özel uygulama / API", "Geliştirici", "braces-asterisk",
            "Kendi yazılımınız, OpenCart, PrestaShop, İdeaSoft, Ticimax vb. platformlar için imzalı genel sipariş uç noktası ve fatura olayı bildirimleri.",
            true, "X-GibFramework-Signature",
            [
                new(SigningSecret, "İmza anahtarı", "generated", true, null, "Gelen isteklerin ve giden bildirimlerin HMAC-SHA256 imzası için kullanılır."),
                Auto, Save, Vat, Outbound,
            ],
            [
                "Siparişi aşağıdaki gelen adrese JSON olarak POST edin (şema Geliştirici dokümanında).",
                "İsteğe X-GibFramework-Signature: sha256=HMAC_SHA256(imza anahtarı, gövde) başlığını ekleyin.",
                "Fatura olaylarını almak için bildirim adresini girip olayları seçin.",
            ]),
    ];

    public static IntegrationKindInfo Get(IntegrationKind kind) => All.First(k => k.Kind == kind);
}
