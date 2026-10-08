namespace GIBFramework.Base;

public sealed record RoleDefinition(string Code, string Label, string Group, int Rank, string Description);

public sealed record PolicyDefinition(string Code, string Label, string Group);

public static class Roles
{
    public const string Accountant = nameof(Accountant);
    public const string AccountingManager = nameof(AccountingManager);
    public const string ApiClient = nameof(ApiClient);
    public const string ArchiveAuditor = nameof(ArchiveAuditor);
    public const string CompanyAdmin = nameof(CompanyAdmin);
    public const string ComplianceOfficer = nameof(ComplianceOfficer);
    public const string IntegratorManager = nameof(IntegratorManager);
    public const string InvoiceApprover = nameof(InvoiceApprover);
    public const string InvoiceCreator = nameof(InvoiceCreator);
    public const string InvoiceSigner = nameof(InvoiceSigner);
    public const string PlatformSuperAdmin = nameof(PlatformSuperAdmin);
    public const string ReadOnlyAuditor = nameof(ReadOnlyAuditor);
    public const string SecurityOfficer = nameof(SecurityOfficer);
    public const string TenantOwner = nameof(TenantOwner);

    public static IReadOnlyList<RoleDefinition> Catalog { get; } =
    [
        new(PlatformSuperAdmin, "Platform yöneticisi", "Platform", 100, "Tüm firmalar, firma sahipleri ve duyurular."),
        new(TenantOwner, "Genel Müdür / CEO", "Yönetim", 90, "Firmadaki en üst yetki: rol atama, API anahtarları, tüm fatura işlemleri."),
        new(CompanyAdmin, "Firma yöneticisi", "Yönetim", 80, "Kullanıcılar, firma profili ve ayarlar."),
        new(AccountingManager, "Muhasebe müdürü", "Muhasebe", 70, "Fatura oluşturma ve onay (kendi faturası dahil), iptal, GİB portalı."),
        new(SecurityOfficer, "Güvenlik sorumlusu", "Denetim", 60, "Denetim izi ve güvenlik kayıtları."),
        new(ComplianceOfficer, "Uyum sorumlusu", "Denetim", 60, "Mevzuat kuralları, vergi daireleri, denetim izi."),
        new(InvoiceApprover, "Onay yetkilisi", "Fatura", 50, "Başkasının hazırladığı faturayı onaylar veya reddeder."),
        new(InvoiceSigner, "İmza yetkilisi", "Fatura", 50, "Mali mühür / GİB imzası ve gönderim."),
        new(IntegratorManager, "Entegrasyon yöneticisi", "Fatura", 50, "Gönderim, durum sorgulama ve yeniden deneme."),
        new(Accountant, "Muhasebeci", "Muhasebe", 40, "Fatura, cari ve ürün kayıtları, gelen faturalar."),
        new(InvoiceCreator, "Fatura hazırlayan", "Fatura", 30, "Taslak fatura, cari ve ürün kayıtları."),
        new(ArchiveAuditor, "Arşiv denetçisi", "Denetim", 20, "Faturaları ve denetim izini görüntüler."),
        new(ReadOnlyAuditor, "Salt okunur denetçi", "Denetim", 10, "Yalnızca görüntüleme."),
        new(ApiClient, "API istemcisi", "Entegrasyon", 10, "Üçüncü taraf uygulama: fatura okuma/oluşturma, gelen fatura."),
    ];

    public static IReadOnlyList<string> All { get; } = [.. Catalog.Select(r => r.Code)];

    public static int Rank(string role) => Catalog.FirstOrDefault(r => r.Code == role)?.Rank ?? 0;

    public static int MaxRank(IEnumerable<string> roles) => roles.Select(Rank).DefaultIfEmpty(0).Max();
}

public static class Policies
{
    public const string ApiKeyManage = nameof(ApiKeyManage);
    public const string AuditRead = nameof(AuditRead);
    public const string CatalogManage = nameof(CatalogManage);
    public const string CompanyManage = nameof(CompanyManage);
    public const string ComplianceManage = nameof(ComplianceManage);
    public const string GibPortal = nameof(GibPortal);
    public const string IncomingManage = nameof(IncomingManage);
    public const string IntegrationManage = nameof(IntegrationManage);
    public const string InvoiceApprove = nameof(InvoiceApprove);
    public const string InvoiceCancel = nameof(InvoiceCancel);
    public const string InvoiceCreate = nameof(InvoiceCreate);
    public const string InvoiceRead = nameof(InvoiceRead);
    public const string InvoiceSign = nameof(InvoiceSign);
    public const string InvoiceTransmit = nameof(InvoiceTransmit);
    public const string MessagingManage = nameof(MessagingManage);
    public const string PlatformAdmin = nameof(PlatformAdmin);
    public const string SecurityManage = nameof(SecurityManage);
    public const string SettingsManage = nameof(SettingsManage);
    public const string SystemRead = nameof(SystemRead);
    public const string TaxOfficeManage = nameof(TaxOfficeManage);
    public const string UserManage = nameof(UserManage);

    public static IReadOnlyList<PolicyDefinition> Catalog { get; } =
    [
        new(InvoiceRead, "Faturaları görüntüleme", "Fatura"),
        new(InvoiceCreate, "Fatura oluşturma", "Fatura"),
        new(InvoiceApprove, "Fatura onaylama", "Fatura"),
        new(InvoiceSign, "Fatura imzalama", "Fatura"),
        new(InvoiceTransmit, "Gönderim ve durum sorgulama", "Fatura"),
        new(InvoiceCancel, "İptal ve itiraz", "Fatura"),
        new(IncomingManage, "Gelen faturalar", "Fatura"),
        new(GibPortal, "GİB e-Arşiv Portal", "GİB"),
        new(CatalogManage, "Cari ve ürün kayıtları", "Kayıtlar"),
        new(SettingsManage, "Fatura ayarları", "Ayarlar"),
        new(CompanyManage, "Firma profili ve numaralandırma", "Ayarlar"),
        new(UserManage, "Kullanıcı ve rol yönetimi", "Ayarlar"),
        new(ApiKeyManage, "API anahtarları", "Ayarlar"),
        new(IntegrationManage, "Entegrasyonlar (e-ticaret, WHMCS, WiseCP, webhook)", "Ayarlar"),
        new(MessagingManage, "E-posta / SMS ayarları ve şablonları", "Ayarlar"),
        new(SecurityManage, "Güvenlik politikası", "Ayarlar"),
        new(SystemRead, "Sistem durumu", "Ayarlar"),
        new(AuditRead, "Denetim izi", "Denetim"),
        new(ComplianceManage, "Mevzuat kaynak izleme", "Denetim"),
        new(TaxOfficeManage, "Vergi dairesi listesi", "Denetim"),
        new(PlatformAdmin, "Platform yönetimi", "Platform"),
    ];

    public static IReadOnlyDictionary<string, string[]> Map { get; } = new Dictionary<string, string[]>
    {
        [ApiKeyManage] = [Roles.TenantOwner],
        [AuditRead] = [Roles.TenantOwner, Roles.CompanyAdmin, Roles.SecurityOfficer, Roles.ComplianceOfficer, Roles.ArchiveAuditor, Roles.ReadOnlyAuditor],
        [CatalogManage] = [Roles.TenantOwner, Roles.CompanyAdmin, Roles.AccountingManager, Roles.Accountant, Roles.InvoiceCreator, Roles.ApiClient],
        [CompanyManage] = [Roles.TenantOwner, Roles.CompanyAdmin],
        [ComplianceManage] = [Roles.TenantOwner, Roles.ComplianceOfficer],
        [GibPortal] = [Roles.TenantOwner, Roles.AccountingManager, Roles.InvoiceSigner],
        [IncomingManage] = [Roles.TenantOwner, Roles.AccountingManager, Roles.Accountant, Roles.ApiClient],
        [IntegrationManage] = [Roles.TenantOwner, Roles.CompanyAdmin, Roles.IntegratorManager],
        [InvoiceApprove] = [Roles.TenantOwner, Roles.AccountingManager, Roles.InvoiceApprover],
        [InvoiceCancel] = [Roles.TenantOwner, Roles.CompanyAdmin, Roles.AccountingManager],
        [InvoiceCreate] = [Roles.TenantOwner, Roles.AccountingManager, Roles.Accountant, Roles.InvoiceCreator, Roles.ApiClient],
        [InvoiceRead] =
        [
            Roles.TenantOwner, Roles.CompanyAdmin, Roles.AccountingManager, Roles.SecurityOfficer, Roles.ComplianceOfficer, Roles.InvoiceApprover,
            Roles.InvoiceSigner, Roles.IntegratorManager, Roles.Accountant, Roles.InvoiceCreator, Roles.ArchiveAuditor, Roles.ReadOnlyAuditor, Roles.ApiClient,
        ],
        [InvoiceSign] = [Roles.TenantOwner, Roles.InvoiceSigner],
        [InvoiceTransmit] = [Roles.TenantOwner, Roles.InvoiceSigner, Roles.IntegratorManager],
        [MessagingManage] = [Roles.TenantOwner, Roles.CompanyAdmin],
        [PlatformAdmin] = [Roles.PlatformSuperAdmin],
        [SecurityManage] = [Roles.PlatformSuperAdmin, Roles.TenantOwner, Roles.CompanyAdmin, Roles.SecurityOfficer],
        [SettingsManage] = [Roles.TenantOwner, Roles.CompanyAdmin, Roles.AccountingManager],
        [SystemRead] = [Roles.PlatformSuperAdmin, Roles.TenantOwner, Roles.CompanyAdmin, Roles.SecurityOfficer],
        [TaxOfficeManage] = [Roles.PlatformSuperAdmin, Roles.ComplianceOfficer],
        [UserManage] = [Roles.PlatformSuperAdmin, Roles.TenantOwner, Roles.CompanyAdmin],
    };
}

public static class GibFrameworkClaims
{
    public const string TenantId = "tenant_id";
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
}

public static class GibFrameworkHeaders
{
    public const string CorrelationId = "X-Correlation-Id";
    public const string TenantId = "X-Tenant-Id";
    public const string IdempotencyKey = "Idempotency-Key";
}
