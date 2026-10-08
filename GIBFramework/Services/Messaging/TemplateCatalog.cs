using GIBFramework.Models.Messaging;

namespace GIBFramework.Services.Messaging;

public sealed record TemplateVariable(string Name, string Description, string Sample);

public sealed record TemplateDefinition(
    string Key,
    string Label,
    string Audience,
    string Description,
    IReadOnlyList<TemplateVariable> Variables,
    string? EmailSubject,
    string? EmailBody,
    bool EmailEnabled,
    string? SmsBody,
    bool SmsEnabled,
    IReadOnlyList<string> SecretVariables)
{
    public bool Supports(MessageChannel channel) => channel == MessageChannel.Email ? EmailBody is not null : SmsBody is not null;
}

public static class TemplateKeys
{
    public const string InvoiceIssued = "invoice.issued";
    public const string InvoiceCancelled = "invoice.cancelled";
    public const string InvoiceAwaitingApproval = "invoice.awaiting_approval";
    public const string InvoiceRejected = "invoice.rejected";
    public const string InvoiceFailed = "invoice.failed";
    public const string UserWelcome = "user.welcome";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserPasswordResetRequested = "user.password_reset_requested";
    public const string UserLocked = "user.locked";
    public const string ApiKeyCreated = "security.api_key_created";
    public const string Test = "system.test";
}

public static class TemplateCatalog
{
    public const string SecretPassword = "gecici_sifre";

    private static readonly TemplateVariable[] Company =
    [
        new("firma.unvan", "Firma unvanı", "Demo Yazılım A.Ş."),
        new("firma.vkn", "Firma VKN/TCKN", "1234567890"),
        new("firma.eposta", "Firma e-posta adresi", "info@demo.com.tr"),
        new("firma.telefon", "Firma telefonu", "0212 000 00 00"),
        new("firma.adres", "Firma adresi", "Kadıköy / İstanbul"),
        new("panel.link", "Panel adresi", "https://framework.hamzadenizyilmaz.com.tr"),
    ];

    private static readonly TemplateVariable[] Invoice =
    [
        new("fatura.no", "Belge numarası (taslakta taslak numarası)", "DEA2026000000001"),
        new("fatura.tur", "Belge türü", "e-Arşiv Fatura"),
        new("fatura.tarih", "Düzenleme tarihi", "08.10.2026"),
        new("fatura.tutar", "Ödenecek tutar", "1.200,00 ₺"),
        new("fatura.ettn", "ETTN", "f320bba6-db44-47ed-aabb-168a4ce67dc3"),
        new("fatura.link", "Müşterinin faturayı görüntüleyeceği bağlantı", "https://framework.hamzadenizyilmaz.com.tr/f/…"),
        new("fatura.panel_link", "Faturanın panel bağlantısı", "https://framework.hamzadenizyilmaz.com.tr/#/invoices/…"),
        new("musteri.unvan", "Alıcı unvanı / adı", "Ayşe Yılmaz"),
        new("musteri.vkn", "Alıcı VKN/TCKN", "11111111111"),
    ];

    private static readonly TemplateVariable[] User =
    [
        new("kullanici.ad", "Kullanıcının adı", "Ayşe Yılmaz"),
        new("kullanici.kod", "Kullanıcı kodu", "ayse"),
    ];

    private static TemplateVariable[] Vars(params TemplateVariable[][] groups) => [.. groups.SelectMany(g => g)];

    private static string P(string text) => $"<p style=\"margin:0 0 14px\">{text}</p>";

    private static string Button(string link, string label) =>
        $"<p style=\"margin:22px 0\"><a href=\"{{{{{link}}}}}\" style=\"display:inline-block;background:{{{{marka.renk}}}};color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:24px;font-weight:600\">{label}</a></p>";

    private static string Facts(params (string Label, string Var)[] rows) =>
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;border-collapse:collapse;margin:6px 0 16px;font-size:14px\">"
        + string.Concat(rows.Select(r => $"<tr><td style=\"padding:8px 0;color:#6b7684;border-bottom:1px solid #eef1f5;width:42%\">{r.Label}</td><td style=\"padding:8px 0;border-bottom:1px solid #eef1f5;font-weight:600\">{{{{{r.Var}}}}}</td></tr>"))
        + "</table>";

    public static IReadOnlyList<TemplateDefinition> All { get; } =
    [
        new(
            TemplateKeys.InvoiceIssued,
            "Fatura düzenlendi",
            "Müşteri",
            "Fatura imzalanıp GİB'e iletildiğinde alıcıya gönderilir. E-postaya fatura PDF, HTML ve UBL-TR XML eklenir.",
            Vars(Company, Invoice),
            "{{firma.unvan}} – {{fatura.tur}} {{fatura.no}}",
            P("Sayın {{musteri.unvan}},")
                + P("{{firma.unvan}} tarafından adınıza <strong>{{fatura.tur}}</strong> düzenlenmiştir.")
                + Facts(("Belge no", "fatura.no"), ("Tarih", "fatura.tarih"), ("Tutar", "fatura.tutar"), ("ETTN", "fatura.ettn"))
                + Button("fatura.link", "Faturayı görüntüle")
                + P("Faturanın PDF, HTML ve UBL-TR XML dosyaları bu e-postanın ekindedir."),
            true,
            "{{firma.unvan}}: {{fatura.no}} numarali {{fatura.tutar}} tutarli {{fatura.tur}} duzenlendi. {{fatura.link}}",
            false,
            []),
        new(
            TemplateKeys.InvoiceCancelled,
            "Fatura iptal edildi",
            "Müşteri",
            "Düzenlenmiş fatura iptal edildiğinde alıcıya gönderilir.",
            Vars(Company, Invoice, [new("neden", "İptal nedeni", "Hatalı tutar")]),
            "{{firma.unvan}} – {{fatura.no}} numaralı fatura iptal edildi",
            P("Sayın {{musteri.unvan}},")
                + P("Adınıza düzenlenen <strong>{{fatura.no}}</strong> numaralı {{fatura.tur}} iptal edilmiştir.")
                + Facts(("Belge no", "fatura.no"), ("Tarih", "fatura.tarih"), ("Tutar", "fatura.tutar"), ("İptal nedeni", "neden")),
            true,
            "{{firma.unvan}}: {{fatura.no}} numarali faturaniz iptal edildi.",
            false,
            []),
        new(
            TemplateKeys.InvoiceAwaitingApproval,
            "Onay bekleyen fatura",
            "Kullanıcı (onaycılar)",
            "Fatura doğrulamadan geçip onaya düştüğünde onay yetkisi olan kullanıcılara gönderilir.",
            Vars(Company, Invoice, User, [new("olusturan", "Faturayı hazırlayan", "muhasebe")]),
            "Onay bekliyor: {{fatura.no}} – {{musteri.unvan}}",
            P("Merhaba {{kullanici.ad}},")
                + P("<strong>{{olusturan}}</strong> tarafından hazırlanan fatura onayınızı bekliyor.")
                + Facts(("Taslak no", "fatura.no"), ("Alıcı", "musteri.unvan"), ("Tutar", "fatura.tutar"))
                + Button("fatura.panel_link", "Faturayı incele"),
            true,
            "GIB Framework: {{fatura.no}} ({{fatura.tutar}}) onayinizi bekliyor. {{fatura.panel_link}}",
            false,
            []),
        new(
            TemplateKeys.InvoiceRejected,
            "Fatura onaylanmadı",
            "Kullanıcı (hazırlayan)",
            "Onaycı faturayı reddettiğinde faturayı hazırlayan kullanıcıya gönderilir.",
            Vars(Company, Invoice, User, [new("neden", "Red nedeni", "Birim fiyat hatalı"), new("islem_yapan", "Reddeden kullanıcı", "onay")]),
            "Onaylanmadı: {{fatura.no}}",
            P("Merhaba {{kullanici.ad}},")
                + P("<strong>{{fatura.no}}</strong> numaralı taslak <strong>{{islem_yapan}}</strong> tarafından onaylanmadı ve taslağa geri alındı.")
                + Facts(("Alıcı", "musteri.unvan"), ("Tutar", "fatura.tutar"), ("Neden", "neden"))
                + Button("fatura.panel_link", "Taslağı düzenle"),
            true,
            null,
            false,
            []),
        new(
            TemplateKeys.InvoiceFailed,
            "Fatura gönderilemedi",
            "Kullanıcı (hazırlayan)",
            "Fatura GİB / alıcı tarafından reddedildiğinde veya gönderim başarısız olduğunda gönderilir.",
            Vars(Company, Invoice, User, [new("neden", "Hata açıklaması", "Alıcı posta kutusu bulunamadı")]),
            "Gönderilemedi: {{fatura.no}}",
            P("Merhaba {{kullanici.ad}},")
                + P("<strong>{{fatura.no}}</strong> numaralı fatura işlenemedi.")
                + Facts(("Alıcı", "musteri.unvan"), ("Tutar", "fatura.tutar"), ("Açıklama", "neden"))
                + Button("fatura.panel_link", "Faturayı aç"),
            true,
            "GIB Framework: {{fatura.no}} numarali fatura gonderilemedi. {{fatura.panel_link}}",
            false,
            []),
        new(
            TemplateKeys.UserWelcome,
            "Hoş geldiniz (yeni kullanıcı)",
            "Kullanıcı",
            "Yeni kullanıcı oluşturulurken \"giriş bilgilerini gönder\" seçilirse gönderilir.",
            Vars(Company, User, [new(SecretPassword, "Geçici şifre (gönderim anında eklenir, veritabanında saklanmaz)", "Gecici2026x")]),
            "{{firma.unvan}} – GIB Framework hesabınız oluşturuldu",
            P("Merhaba {{kullanici.ad}},")
                + P("{{firma.unvan}} için GIB Framework hesabınız oluşturuldu.")
                + Facts(("Kullanıcı kodu", "kullanici.kod"), ("Geçici şifre", SecretPassword))
                + Button("panel.link", "Giriş yap")
                + P("İlk girişte yeni şifrenizi belirlemeniz istenecektir."),
            true,
            "GIB Framework hesabiniz olusturuldu. Kullanici kodu: {{kullanici.kod}} Gecici sifre: {{gecici_sifre}} {{panel.link}}",
            false,
            [SecretPassword]),
        new(
            TemplateKeys.UserPasswordReset,
            "Şifre sıfırlandı",
            "Kullanıcı",
            "Yönetici şifrenizi sıfırlarken \"yeni şifreyi gönder\" seçilirse gönderilir.",
            Vars(Company, User, [new(SecretPassword, "Geçici şifre (gönderim anında eklenir, veritabanında saklanmaz)", "Gecici2026x")]),
            "GIB Framework – şifreniz sıfırlandı",
            P("Merhaba {{kullanici.ad}},")
                + P("Şifreniz yöneticiniz tarafından sıfırlandı. Geçici şifrenizle giriş yapıp yeni şifrenizi belirleyin.")
                + Facts(("Kullanıcı kodu", "kullanici.kod"), ("Geçici şifre", SecretPassword))
                + Button("panel.link", "Giriş yap"),
            true,
            "GIB Framework sifreniz sifirlandi. Kullanici: {{kullanici.kod}} Gecici sifre: {{gecici_sifre}}",
            false,
            [SecretPassword]),
        new(
            TemplateKeys.UserPasswordResetRequested,
            "Şifre sıfırlama talebi",
            "Kullanıcı (yöneticiler)",
            "Bir kullanıcı \"Şifremi unuttum\" ile talep oluşturduğunda kullanıcı yönetimi yetkisi olan yöneticilere gönderilir.",
            Vars(Company, User, [new("talep.ip", "Talebin geldiği IP", "203.0.113.10"), new("talep.kullanici", "Talep eden kullanıcı", "muhasebe")]),
            "Şifre sıfırlama talebi: {{talep.kullanici}}",
            P("Merhaba {{kullanici.ad}},")
                + P("<strong>{{talep.kullanici}}</strong> kullanıcısı şifre sıfırlama talebinde bulundu (IP: {{talep.ip}}).")
                + Button("panel.link", "Talepleri görüntüle"),
            true,
            null,
            false,
            []),
        new(
            TemplateKeys.UserLocked,
            "Hesap kilitlendi",
            "Kullanıcı",
            "Art arda hatalı giriş denemesi nedeniyle hesap geçici olarak kilitlendiğinde gönderilir.",
            Vars(Company, User, [new("kilit.bitis", "Kilidin açılacağı saat", "14:45")]),
            "GIB Framework – hesabınız geçici olarak kilitlendi",
            P("Merhaba {{kullanici.ad}},")
                + P("Hesabınızda art arda hatalı giriş denemesi yapıldığı için hesabınız <strong>{{kilit.bitis}}</strong> saatine kadar kilitlendi.")
                + P("Bu denemeler size ait değilse yöneticinize haber verin."),
            true,
            "GIB Framework: hesabiniz {{kilit.bitis}} saatine kadar kilitlendi. Bu deneme size ait degilse yoneticinize bildirin.",
            false,
            []),
        new(
            TemplateKeys.ApiKeyCreated,
            "API anahtarı oluşturuldu",
            "Kullanıcı (CEO)",
            "Firmada yeni bir API anahtarı oluşturulduğunda Genel Müdür / CEO rolündeki kullanıcılara gönderilir.",
            Vars(Company, User, [new("anahtar.ad", "Uygulama adı", "E-ticaret"), new("anahtar.yetkiler", "Verilen yetkiler", "API istemcisi"), new("islem_yapan", "Oluşturan", "yonetici")]),
            "Yeni API anahtarı: {{anahtar.ad}}",
            P("Merhaba {{kullanici.ad}},")
                + P("<strong>{{islem_yapan}}</strong> tarafından yeni bir API anahtarı oluşturuldu.")
                + Facts(("Uygulama", "anahtar.ad"), ("Yetkiler", "anahtar.yetkiler")),
            true,
            null,
            false,
            []),
        new(
            TemplateKeys.Test,
            "Deneme mesajı",
            "Test",
            "Ayarlar sayfasındaki \"Deneme gönder\" düğmesiyle gönderilir.",
            Vars(Company, [new("tarih", "Gönderim zamanı", "08.10.2026 14:30")]),
            "{{firma.unvan}} – GIB Framework deneme e-postası",
            P("Bu bir deneme e-postasıdır.")
                + Facts(("Firma", "firma.unvan"), ("Gönderim zamanı", "tarih"))
                + P("E-posta ayarlarınız çalışıyor."),
            true,
            "GIB Framework deneme SMS'i. {{firma.unvan}} {{tarih}}",
            true,
            []),
    ];

    public static TemplateDefinition? Find(string key) => All.FirstOrDefault(t => t.Key == key);
}
