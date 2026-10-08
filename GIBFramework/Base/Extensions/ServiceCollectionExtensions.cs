using GIBFramework.DAL.Messaging;
using GIBFramework.Services.Integrations;
using GIBFramework.Services.Messaging;
using Microsoft.AspNetCore.Authentication;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using GIBFramework.Infrastructure.Swagger;
using GIBFramework.DAL;
using GIBFramework.DAL.Announcements;
using GIBFramework.DAL.Catalog;
using GIBFramework.Services.Locations;
using GIBFramework.DAL.Audit;
using GIBFramework.Controllers;
using GIBFramework.DAL.Compliance;
using GIBFramework.DAL.Identity;
using GIBFramework.DAL.Incoming;
using GIBFramework.DAL.Invoices;
using GIBFramework.DAL.LegalSources;
using GIBFramework.DAL.Sequences;
using GIBFramework.DAL.Tax;
using GIBFramework.DAL.TaxOffices;
using GIBFramework.DAL.Tenants;
using GIBFramework.Infrastructure.Background;
using GIBFramework.Infrastructure.GibPortal;
using GIBFramework.Infrastructure.Providers;
using GIBFramework.Infrastructure.Signing;
using GIBFramework.Infrastructure.Storage;
using GIBFramework.Infrastructure.Tenancy;
using GIBFramework.Models.Identity;
using GIBFramework.Services.Auth;
using GIBFramework.Services.Compliance;
using GIBFramework.Services.GibPortal;
using GIBFramework.Services.Incoming;
using GIBFramework.Services.Invoices;
using GIBFramework.Services.Qr;
using GIBFramework.Services.Signing;
using GIBFramework.Services.Tax;
using GIBFramework.Services.TaxOffices;
using GIBFramework.Services.Ubl;

namespace GIBFramework.Base.Extensions;

public static class ServiceCollectionExtensions
{
    public const string DemoEnvironment = "Demo";

    public const string AuthSchemeSelector = "GibFramework";

    public static IServiceCollection AddGibFramework(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var database = Bind<DatabaseOptions>(configuration, DatabaseOptions.Section);
        database.ConnectionString = configuration.GetConnectionString("GibFramework") ?? string.Empty;
        services.AddSingleton(database);
        var jwt = services.AddOptionsInstance<JwtOptions>(configuration, JwtOptions.Section);
        var compliance = services.AddOptionsInstance<ComplianceOptions>(configuration, ComplianceOptions.Section);
        services.AddOptionsInstance<WorkflowOptions>(configuration, WorkflowOptions.Section);
        var signing = services.AddOptionsInstance<SigningOptions>(configuration, SigningOptions.Section);
        var vault = services.AddOptionsInstance<EvidenceVaultOptions>(configuration, EvidenceVaultOptions.Section);
        services.AddOptionsInstance<ProviderOptions>(configuration, ProviderOptions.Section);
        services.AddOptionsInstance<UblOptions>(configuration, UblOptions.Section);
        var workers = services.AddOptionsInstance<WorkerOptions>(configuration, WorkerOptions.Section);
        services.AddOptionsInstance<BootstrapOptions>(configuration, BootstrapOptions.Section);
        services.AddOptionsInstance<DemoOptions>(configuration, DemoOptions.Section);
        services.AddOptionsInstance<GibPortalOptions>(configuration, GibPortalOptions.Section);
        var security = services.AddOptionsInstance<SecurityOptions>(configuration, SecurityOptions.Section);

        var demoHost = environment.IsEnvironment(DemoEnvironment);
        if (!environment.IsDevelopment()
            && (jwt.DevTokenEnabled || jwt.SigningKey.Contains("DEV-ONLY", StringComparison.Ordinal) || jwt.SigningKey.Length < 32))
        {
            throw new InvalidOperationException(
                "Geliştirme dışı ortamlarda Jwt:DevTokenEnabled kapalı olmalı ve Jwt:SigningKey gizli yönetiminden gelen en az 32 karakterlik bir anahtar olmalıdır.");
        }

        if (!environment.IsDevelopment() && !demoHost && (compliance.AllowUnapprovedRules || signing.Mode == SigningMode.DevSelfSigned))
        {
            throw new InvalidOperationException(
                "Üretim ortamında AllowUnapprovedRules ve DevSelfSigned imza modu kapalı olmalıdır (demo sunucusu için ASPNETCORE_ENVIRONMENT=Demo kullanın).");
        }

        var keysDir = new DirectoryInfo(Path.GetFullPath(security.DataProtectionKeysPath, environment.ContentRootPath));
        var dataProtection = services.AddDataProtection().SetApplicationName("GIB Framework").PersistKeysToFileSystem(keysDir);
        if (OperatingSystem.IsWindows())
        {
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }

        services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<SecurityStampCache>();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(AuthController.LoginRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = security.LoginRateLimitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy(HooksController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy(PublicInvoiceController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        var seedRoot = Path.Combine(AppContext.BaseDirectory, compliance.SeedPath);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(RuleEvaluatorRegistry.CreateDefault());
        services.AddSingleton(sp => JsonRuleSetLoader.LoadFromDirectory(seedRoot, sp.GetRequiredService<RuleEvaluatorRegistry>()));
        services.AddSingleton(_ => TaxCatalogLoader.LoadFromDirectory(seedRoot));
        services.AddSingleton<ComplianceEngine>();
        services.AddSingleton<TaxEngine>();
        services.AddSingleton<UblInvoiceWriter>();
        services.AddSingleton<UblValidator>();
        services.AddSingleton<QrCodeService>();
        services.AddSingleton<TokenService>();

        services.AddSingleton<ISigningCertificateProvider>(sp => signing.Mode switch
        {
            SigningMode.DevSelfSigned => new DevSelfSignedCertificateProvider(sp.GetRequiredService<IClock>()),
            SigningMode.WindowsStore => new WindowsStoreCertificateProvider(signing),
            _ => new Pkcs11CertificateProvider(),
        });
        services.AddSingleton<IXmlSigner, XadesSigner>();

        services.AddSingleton<IEvidenceVault>(sp => new FileSystemEvidenceVault(
            Path.GetFullPath(vault.RootPath, environment.ContentRootPath), sp.GetRequiredService<IClock>()));
        services.AddSingleton<SandboxEDocumentProvider>();
        services.AddSingleton<IEDocumentProvider>(sp => sp.GetRequiredService<SandboxEDocumentProvider>());
        services.AddSingleton<IEDocumentProvider, GibDirectProvider>();
        services.AddSingleton<ProviderRegistry>();
        services.AddSingleton<IDocumentNumberAllocator, SqlDocumentNumberAllocator>();
        services.AddSingleton<SchemaVerifier>();

        services.AddScoped<RequestTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<RequestTenantContext>());
        services.AddScoped<SqlConnectionFactory>();
        services.AddScoped<IAuditTrail, SqlAuditTrail>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddSingleton<IInvoiceRepository, InvoiceRepository>();
        services.AddSingleton<IIncomingInvoiceRepository, IncomingInvoiceRepository>();
        services.AddScoped<ITaxOfficeRepository, TaxOfficeRepository>();
        services.AddScoped<ILegalSourceCheckRepository, LegalSourceCheckRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
        var locationOptions = services.AddOptionsInstance<LocationOptions>(configuration, LocationOptions.Section);
        services.AddHttpClient(TkgmClient.HttpClientName, c => c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; GIBFramework/1.0)"));
        services.AddSingleton<TkgmClient>();
        services.AddSingleton<LocationSyncState>();
        services.AddScoped<LocationRepository>();
        services.AddScoped<LocationService>();
        if (locationOptions.SyncEnabled && !string.IsNullOrWhiteSpace(database.ConnectionString))
        {
            services.AddHostedService<LocationSyncWorker>();
        }
        services.AddScoped<InvoiceService>();
        services.AddScoped<IncomingInvoiceService>();
        services.AddScoped<TaxOfficeImportService>();
        var taxOfficeOptions = services.AddOptionsInstance<TaxOfficeOptions>(configuration, TaxOfficeOptions.Section);
        services.AddHttpClient(TaxOfficeBootstrapWorker.HttpClientName, c => c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; GIBFramework/1.0)"));
        if (taxOfficeOptions.AutoLoad && !string.IsNullOrWhiteSpace(database.ConnectionString))
        {
            services.AddHostedService<TaxOfficeBootstrapWorker>();
        }
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<UserBootstrapper>();
        services.AddScoped<DemoDataSeeder>();

        services.AddHttpClient(GibPortalClient.HttpClientName, c =>
        {
            c.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr,en-US;q=0.9,en;q=0.8");
            c.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; GIBFramework/1.0)");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
        services.AddSingleton<GibPortalClient>();
        services.AddSingleton<GibPortalSessionStore>();
        services.AddScoped<GibPortalService>();

        services.AddHttpClient(nameof(LegalSourceWatcher), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(60);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("GIBFramework-LegalSourceWatcher/1.0");
        });
        if (workers.DispatcherEnabled)
        {
            services.AddHostedService<DispatcherWorker>();
        }

        if (workers.ReconciliationEnabled)
        {
            services.AddHostedService<ReconciliationWorker>();
        }

        if (workers.LegalSourceWatcherEnabled)
        {
            services.AddHostedService<LegalSourceWatcher>();
        }

        if (workers.CertificateMonitorEnabled)
        {
            services.AddHostedService<CertificateExpiryMonitor>();
        }

        services.AddScoped<ApiKeyRepository>();
        services.AddScoped<ApiKeyService>();
        services.AddScoped<SecurityPolicyService>();
        services.AddScoped<PasswordResetRequestService>();

        services.AddOptionsInstance<AppOptions>(configuration, AppOptions.Section);
        var messaging = services.AddOptionsInstance<MessagingOptions>(configuration, MessagingOptions.Section);
        services.AddHttpClient(NetgsmClient.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("GIBFramework/5.2");
        });
        services.AddHttpClient(IntegrationService.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("GIBFramework-Webhook/5.2");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddOptionsInstance<MarketplaceOptions>(configuration, MarketplaceOptions.Section);
        services.AddHttpClient(MarketplaceClientBase.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(40));
        services.AddSingleton<IMarketplaceClient, TrendyolClient>();
        services.AddSingleton<IMarketplaceClient, HepsiburadaClient>();
        services.AddSingleton<IMarketplaceClient, N11Client>();
        services.AddSingleton<NetgsmClient>();
        services.AddSingleton<SmtpMailer>();
        services.AddScoped<OutboxEventRepository>();
        services.AddScoped<MessageRepository>();
        services.AddScoped<TenantLogoRepository>();
        services.AddScoped<IntegrationRepository>();
        services.AddScoped<MessagingSettingsService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<MessageDeliveryService>();
        services.AddScoped<InvoiceDocumentService>();
        services.AddScoped<UserNotifier>();
        services.AddScoped<IntegrationService>();
        services.AddScoped<EventRouter>();
        if (messaging.WorkerEnabled && !string.IsNullOrWhiteSpace(database.ConnectionString))
        {
            services.AddHostedService<MessagingWorker>();
        }
        services.AddAuthentication(o =>
            {
                o.DefaultScheme = AuthSchemeSelector;
                o.DefaultChallengeScheme = AuthSchemeSelector;
            })
            .AddPolicyScheme(AuthSchemeSelector, "JWT veya API anahtarı", o => o.ForwardDefaultSelector = ctx =>
                ctx.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName) ? ApiKeyAuthenticationHandler.SchemeName : JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
            .AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = GibFrameworkClaims.Name,
                RoleClaimType = GibFrameworkClaims.Role,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            };

            if (!string.IsNullOrWhiteSpace(jwt.SigningKey))
            {
                o.TokenValidationParameters.ValidateIssuer = true;
                o.TokenValidationParameters.ValidIssuer = jwt.Issuer;
                o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
            }
            else if (!string.IsNullOrWhiteSpace(jwt.Authority))
            {
                o.Authority = jwt.Authority;
                o.RequireHttpsMetadata = !environment.IsDevelopment();
            }
            else
            {
                throw new InvalidOperationException("Jwt:SigningKey (yerel hesaplar) veya Jwt:Authority (harici OIDC) yapılandırılmalıdır.");
            }

            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var stamp = ctx.Principal?.FindFirst(TokenService.SecurityStampClaim)?.Value;
                    var userCode = ctx.Principal?.FindFirst(GibFrameworkClaims.Subject)?.Value;
                    if (stamp is null || userCode is null)
                    {
                        return;
                    }

                    var services = ctx.HttpContext.RequestServices;
                    var current = await services.GetRequiredService<SecurityStampCache>().GetAsync(
                        userCode,
                        () => services.GetRequiredService<IUserRepository>().GetSecurityStampAsync(userCode, ctx.HttpContext.RequestAborted));
                    if (current?.ToString("N") != stamp)
                    {
                        ctx.Fail("Oturum geçersiz kılındı.");
                    }
                },
            };
        });

        services.AddAuthorization(o =>
        {
            foreach (var (policy, roles) in Policies.Map)
            {
                o.AddPolicy(policy, p => p.RequireAuthenticatedUser().RequireRole(roles));
            }
        });

        services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        });
        services.AddGibFrameworkSwagger();

        return services;
    }

    public static async Task InitializeGibFrameworkAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.GetRequiredService<ComplianceEngine>();
        _ = services.GetRequiredService<TaxEngine>();
        _ = services.GetRequiredService<ProviderRegistry>();
        if (services.GetRequiredService<SigningOptions>().Mode == SigningMode.Pkcs11)
        {
            _ = services.GetRequiredService<ISigningCertificateProvider>().GetSigningCertificate();
        }

        if (!string.IsNullOrWhiteSpace(services.GetRequiredService<DatabaseOptions>().ConnectionString))
        {
            await services.GetRequiredService<SchemaVerifier>().VerifyAsync(ct);
            await using var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<RequestTenantContext>().UseSystem();
            await scope.ServiceProvider.GetRequiredService<UserBootstrapper>().EnsureAdminAsync(ct);
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().EnsureAsync(ct);
        }
    }

    private static T AddOptionsInstance<T>(this IServiceCollection services, IConfiguration configuration, string section)
        where T : class, new()
    {
        var value = Bind<T>(configuration, section);
        services.AddSingleton(value);
        return value;
    }

    private static T Bind<T>(IConfiguration configuration, string section)
        where T : class, new() => configuration.GetSection(section).Get<T>() ?? new T();
}
