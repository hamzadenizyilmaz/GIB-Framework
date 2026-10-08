using Microsoft.Extensions.Caching.Memory;
using GIBFramework.DAL.Catalog;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Catalog;

namespace GIBFramework.Services.Auth;

public sealed record SecurityPolicyRequest(bool RequireTotp, int? SessionMinutes, int IdleLogoutMinutes, int? ApiKeyMaxDays);

public sealed class SecurityPolicyService(ITenantSettingsRepository settings, IMemoryCache cache, IAuditTrail audit, ITenantContext context, IClock clock)
{
    public const int MinSessionMinutes = 15;
    public const int MaxSessionMinutes = 1440;
    public const int MinIdleMinutes = 5;
    public const int MaxIdleMinutes = 480;
    public const int MaxApiKeyDays = 3650;

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    public async Task<SecurityPolicy> GetAsync(Guid tenantId, CancellationToken ct) =>
        await cache.GetOrCreateAsync(Key(tenantId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheLifetime;
            return (await settings.GetAsync(tenantId, ct)).Security ?? new SecurityPolicy();
        }) ?? new SecurityPolicy();

    public async Task<SecurityPolicy> SaveAsync(SecurityPolicyRequest request, bool actorUsedMfa, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        if (request.SessionMinutes is { } session && session is < MinSessionMinutes or > MaxSessionMinutes)
        {
            errors.Add($"Oturum süresi {MinSessionMinutes}-{MaxSessionMinutes} dakika olmalıdır.");
        }

        if (request.IdleLogoutMinutes != 0 && request.IdleLogoutMinutes is < MinIdleMinutes or > MaxIdleMinutes)
        {
            errors.Add($"Hareketsizlik süresi 0 (kapalı) veya {MinIdleMinutes}-{MaxIdleMinutes} dakika olmalıdır.");
        }

        if (request.ApiKeyMaxDays is { } days && days is < 1 or > MaxApiKeyDays)
        {
            errors.Add($"API anahtarı en uzun geçerlilik süresi 1-{MaxApiKeyDays} gün olmalıdır.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("SECURITY_POLICY_INVALID", "Güvenlik politikası kaydedilemedi.", errors);
        }

        if (request.RequireTotp && !actorUsedMfa)
        {
            throw new ForbiddenOperationException("SECURITY_POLICY_MFA_FIRST",
                "2FA zorunluluğunu açmak için önce kendi hesabınızda iki adımlı doğrulamayı etkinleştirip doğrulama koduyla giriş yapın.");
        }

        var tenantId = context.RequireTenant();
        var user = context.RequireUser();
        var now = clock.UtcNow;
        var current = await settings.GetAsync(tenantId, ct);
        current.Security = new SecurityPolicy
        {
            RequireTotp = request.RequireTotp,
            SessionMinutes = request.SessionMinutes,
            IdleLogoutMinutes = request.IdleLogoutMinutes,
            ApiKeyMaxDays = request.ApiKeyMaxDays,
            UpdatedAt = now,
            UpdatedBy = user,
        };
        await settings.SaveAsync(tenantId, current, user, now, ct);
        cache.Remove(Key(tenantId));
        await audit.AppendSystemAsync(tenantId, new AuditEntry("SECURITY_POLICY_UPDATED", "Tenant", tenantId.ToString(), "Success",
            new { by = user, request.RequireTotp, request.SessionMinutes, request.IdleLogoutMinutes, request.ApiKeyMaxDays }), null, ct);
        return current.Security;
    }

    private static string Key(Guid tenantId) => "secpol:" + tenantId.ToString("N");
}
