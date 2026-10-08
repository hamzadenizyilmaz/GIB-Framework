using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace GIBFramework.Infrastructure.GibPortal;

public sealed record GibPortalSession(
    Guid TenantId,
    string OwnerUserId,
    GibPortalEnvironment Environment,
    string PortalUserCodeMasked,
    string PortalTaxId,
    string PortalTitle,
    DateTimeOffset ConnectedAt);

public sealed class GibPortalSessionStore(IMemoryCache cache, IDataProtectionProvider dataProtection, GibPortalOptions options)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("GIBFramework.GibPortal.Token.v1");
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new(StringComparer.Ordinal);

    public void Save(GibPortalSession session, string token)
    {
        ArgumentNullException.ThrowIfNull(session);
        cache.Set(Key(session.TenantId, session.OwnerUserId), new Entry(session, _protector.Protect(token)), new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(options.SessionIdleMinutes),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(8),
        });
    }

    public (GibPortalSession Session, string Token)? Get(Guid tenantId, string userId) =>
        cache.TryGetValue<Entry>(Key(tenantId, userId), out var entry) && entry is not null
            ? (entry.Session, _protector.Unprotect(entry.ProtectedToken))
            : null;

    public void Remove(Guid tenantId, string userId) => cache.Remove(Key(tenantId, userId));

    public void SavePendingSms(Guid invoiceId, string oid) =>
        cache.Set("gibsms:" + invoiceId.ToString("N"), _protector.Protect(oid), TimeSpan.FromMinutes(5));

    public string? TakePendingSms(Guid invoiceId)
    {
        var key = "gibsms:" + invoiceId.ToString("N");
        if (!cache.TryGetValue<string>(key, out var protectedOid) || protectedOid is null)
        {
            return null;
        }

        cache.Remove(key);
        return _protector.Unprotect(protectedOid);
    }

    public SemaphoreSlim AccountLock(GibPortalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _accountLocks.GetOrAdd($"{session.Environment}:{session.PortalTaxId}", _ => new SemaphoreSlim(1, 1));
    }

    private static string Key(Guid tenantId, string userId) => $"gibportal:{tenantId:N}:{userId.ToLowerInvariant()}";

    private sealed record Entry(GibPortalSession Session, string ProtectedToken);
}
