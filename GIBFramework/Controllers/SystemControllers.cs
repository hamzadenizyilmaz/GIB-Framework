using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.Base.Extensions;
using GIBFramework.DAL;
using GIBFramework.Infrastructure.Swagger;
using GIBFramework.Services.Auth;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/settings/security")]
[Authorize]
public sealed class SecurityPolicyController(SecurityPolicyService policies, ITenantContext context, JwtOptions jwt) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var policy = await policies.GetAsync(context.RequireTenant(), ct);
        return Ok(new
        {
            policy,
            platform = new
            {
                defaultSessionMinutes = jwt.AccessTokenMinutes,
                passwordMinLength = PasswordPolicy.MinLength,
                passwordRules = new[]
                {
                    $"En az {PasswordPolicy.MinLength} karakter",
                    "En az bir harf ve bir rakam",
                    "Kullanıcı kodunu içeremez",
                },
                maxFailedAttempts = AuthService.MaxFailedAttempts,
                lockoutMinutes = (int)AuthService.LockoutDuration.TotalMinutes,
            },
            limits = new
            {
                minSessionMinutes = SecurityPolicyService.MinSessionMinutes,
                maxSessionMinutes = SecurityPolicyService.MaxSessionMinutes,
                minIdleMinutes = SecurityPolicyService.MinIdleMinutes,
                maxIdleMinutes = SecurityPolicyService.MaxIdleMinutes,
                maxApiKeyDays = SecurityPolicyService.MaxApiKeyDays,
            },
        });
    }

    [HttpPut]
    [Authorize(Policy = Policies.SecurityManage)]
    public async Task<IActionResult> Save([FromBody] SecurityPolicyRequest request, CancellationToken ct) =>
        Ok(await policies.SaveAsync(request, User.IsInRole(Roles.PlatformSuperAdmin) || User.HasClaim(TokenService.AuthMethodClaim, "mfa"), ct));
}

[ApiController]
[Route("api/v1/system")]
[Authorize(Policy = Policies.SystemRead)]
public sealed class SystemController(
    SqlConnectionFactory connections,
    ITenantContext context,
    IHostEnvironment environment,
    JwtOptions jwt,
    GibPortalOptions portal,
    IConfiguration configuration,
    IClock clock) : ControllerBase
{
    private static readonly DateTimeOffset StartedAt = new(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var tenantId = context.TenantId;
        await using var c = await connections.OpenSystemAsync(ct);
        await using var info = c.Command("""
            SELECT DB_NAME() AS Name,
                   CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) AS Version,
                   CAST(SERVERPROPERTY('Edition') AS nvarchar(100)) AS Edition,
                   (SELECT MAX(Version) FROM dbo.SchemaInfo) AS SchemaVersion
            """);
        string? name = null, version = null, edition = null;
        int? schema = null;
        await using (var r = await info.ExecuteReaderAsync(ct))
        {
            if (await r.ReadAsync(ct))
            {
                name = r.IsDBNull(0) ? null : r.GetString(0);
                version = r.IsDBNull(1) ? null : r.GetString(1);
                edition = r.IsDBNull(2) ? null : r.GetString(2);
                schema = r.IsDBNull(3) ? null : r.GetInt32(3);
            }
        }

        var filter = tenantId is null ? string.Empty : " WHERE TenantId = @t";
        await using var counts = c.Command($"""
            SELECT
              (SELECT COUNT(*) FROM dbo.Invoices{filter}) AS Invoices,
              (SELECT COUNT(*) FROM dbo.Customers{filter}) AS Customers,
              (SELECT COUNT(*) FROM dbo.Products{filter}) AS Products,
              (SELECT COUNT(*) FROM dbo.Users{filter}) AS Users,
              (SELECT COUNT(*) FROM dbo.ApiKeys{filter}{(tenantId is null ? " WHERE" : " AND")} RevokedAt IS NULL) AS ApiKeys,
              (SELECT COUNT(*) FROM dbo.AuditEvents{filter}) AS AuditEvents,
              (SELECT COUNT(*) FROM dbo.Tenants) AS Tenants
            """);
        if (tenantId is { } t)
        {
            counts.With("@t", t);
        }

        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var r = await counts.ExecuteReaderAsync(ct))
        {
            if (await r.ReadAsync(ct))
            {
                for (var i = 0; i < r.FieldCount; i++)
                {
                    totals[char.ToLowerInvariant(r.GetName(i)[0]) + r.GetName(i)[1..]] = r.GetInt32(i);
                }
            }
        }

        var now = clock.UtcNow;
        var assembly = Assembly.GetExecutingAssembly();
        return Ok(new
        {
            application = new
            {
                name = "GIB Framework",
                version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
                    ?? assembly.GetName().Version?.ToString(),
                environment = environment.EnvironmentName,
                startedAt = StartedAt,
                uptimeSeconds = (long)(now - StartedAt).TotalSeconds,
                serverTime = now,
                timeZone = "Europe/Istanbul",
                runtime = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                machine = Environment.MachineName,
                memoryMb = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024,
            },
            database = new { name, version, edition, schemaVersion = schema, requiredSchemaVersion = SchemaVerifier.RequiredVersion },
            totals,
            scope = tenantId is null ? "platform" : "tenant",
            security = new
            {
                https = Request.IsHttps,
                hsts = !environment.IsDevelopment(),
                sessionMinutes = jwt.AccessTokenMinutes,
                swaggerEnabled = configuration.GetSection(SwaggerOptions.Section).Get<SwaggerOptions>()?.Enabled ?? environment.IsDevelopment(),
                gibPortalEnabled = portal.Enabled,
                gibPortalProduction = portal.AllowProduction,
                headers = new[]
                {
                    "Content-Security-Policy", "Strict-Transport-Security", "X-Content-Type-Options", "X-Frame-Options", "Referrer-Policy",
                    "Permissions-Policy", "Cross-Origin-Opener-Policy", "Cross-Origin-Resource-Policy", "Cross-Origin-Embedder-Policy",
                    "Origin-Agent-Cluster", "X-Permitted-Cross-Domain-Policies", "X-DNS-Prefetch-Control", "Cache-Control (API: no-store)",
                },
            },
        });
    }
}
