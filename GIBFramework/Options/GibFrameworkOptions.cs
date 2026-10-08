namespace GIBFramework.Options;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = "gibframework-api";

    public string Issuer { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public bool DevTokenEnabled { get; set; }

    public int AccessTokenMinutes { get; set; } = 60;
}

public sealed class BootstrapOptions
{
    public const string Section = "Bootstrap";

    public string AdminUserCode { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;

    public bool AdminMustChangePassword { get; set; } = true;
}

public enum GibPortalEnvironment
{
    Test,

    Production,
}

public sealed class GibPortalOptions
{
    public const string Section = "GibPortal";

    public bool Enabled { get; set; } = true;

    public bool AllowProduction { get; set; }

    public string ProductionBaseUrl { get; set; } = "https://earsivportal.efatura.gov.tr";

    public string TestBaseUrl { get; set; } = "https://earsivportaltest.efatura.gov.tr";

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int SessionIdleMinutes { get; set; } = 20;
}

public sealed class SecurityOptions
{
    public const string Section = "Security";

    public string DataProtectionKeysPath { get; set; } = "keys";

    public int LoginRateLimitPerMinute { get; set; } = 10;
}

public sealed class ComplianceOptions
{
    public const string Section = "Compliance";

    public bool AllowUnapprovedRules { get; set; }

    public string SeedPath { get; set; } = "DAL/Seed";
}

public sealed class WorkflowOptions
{
    public const string Section = "Workflow";

    public bool RequireMakerChecker { get; set; } = true;

    public string[] SelfApprovalRoles { get; set; } = [];
}

public enum SigningMode
{
    DevSelfSigned,

    WindowsStore,

    Pkcs11,
}

public sealed class SigningOptions
{
    public const string Section = "Signing";

    public SigningMode Mode { get; set; } = SigningMode.WindowsStore;

    public string Thumbprint { get; set; } = string.Empty;

    public string StoreLocation { get; set; } = "LocalMachine";

    public int[] ExpiryAlertDays { get; set; } = [90, 60, 30, 15, 7, 1];
}

public sealed class EvidenceVaultOptions
{
    public const string Section = "EvidenceVault";

    public string RootPath { get; set; } = "evidence";
}

public sealed class ProviderOptions
{
    public const string Section = "Providers";

    public string Active { get; set; } = "Sandbox";

    public bool GibDirectEnabled { get; set; }

    public bool SpecialIntegratorMode { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 30;

    public string[] SandboxRegisteredTaxpayers { get; set; } = [];
}

public sealed class UblOptions
{
    public const string Section = "Ubl";

    public string SchemaDirectory { get; set; } = string.Empty;

    public bool RequireSchemaValidation { get; set; }
}

public sealed class WorkerOptions
{
    public const string Section = "Workers";

    public bool DispatcherEnabled { get; set; } = true;

    public int DispatcherIntervalSeconds { get; set; } = 15;

    public bool ReconciliationEnabled { get; set; } = true;

    public int ReconciliationIntervalMinutes { get; set; } = 15;

    public int InDoubtGraceMinutes { get; set; } = 60;

    public bool LegalSourceWatcherEnabled { get; set; }

    public int LegalSourceWatcherIntervalHours { get; set; } = 24;

    public bool CertificateMonitorEnabled { get; set; } = true;
}

public sealed class AppOptions
{
    public const string Section = "App";

    public string ProductName { get; set; } = "GIB Framework";

    public string PublicUrl { get; set; } = "https://framework.hamzadenizyilmaz.com.tr";

    public string BaseUrl => PublicUrl.TrimEnd('/');
}

public sealed class PlatformSmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Security { get; set; } = "StartTls";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "GIB Framework";

    public string ReplyTo { get; set; } = string.Empty;
}

public sealed class PlatformSmsOptions
{
    public string UserCode { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Header { get; set; } = string.Empty;
}

public sealed class MessagingOptions
{
    public const string Section = "Messaging";

    public bool WorkerEnabled { get; set; } = true;

    public int WorkerIntervalSeconds { get; set; } = 10;

    public int BatchSize { get; set; } = 20;

    public int MaxAttempts { get; set; } = 6;

    public int SmtpTimeoutSeconds { get; set; } = 30;

    public string NetgsmBaseUrl { get; set; } = "https://api.netgsm.com.tr";

    public PlatformSmtpOptions Smtp { get; set; } = new();

    public PlatformSmsOptions Sms { get; set; } = new();
}

public sealed class MarketplaceOptions
{
    public const string Section = "Marketplaces";

    public string TrendyolBaseUrl { get; set; } = string.Empty;

    public string HepsiburadaBaseUrl { get; set; } = string.Empty;

    public string N11RestUrl { get; set; } = "https://api.n11.com/rest";

    public string N11InvoiceSoapUrl { get; set; } = "https://api.n11.com/ws/sellerInvoiceService/";
}
