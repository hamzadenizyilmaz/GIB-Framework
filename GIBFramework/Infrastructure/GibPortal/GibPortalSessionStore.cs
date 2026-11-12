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

    public void SavePendingSms(GibPortalSession session, Guid invoiceId, Guid ettn, string oid) =>
        cache.Set(SmsKey(session, invoiceId, ettn), _protector.Protect(oid), TimeSpan.FromMinutes(5));

    public string? GetPendingSms(GibPortalSession session, Guid invoiceId, Guid ettn) =>
        cache.TryGetValue<string>(SmsKey(session, invoiceId, ettn), out var oid) && oid is not null ? _protector.Unprotect(oid) : null;

    public void RemovePendingSms(GibPortalSession session, Guid invoiceId, Guid ettn) =>
        cache.Remove(SmsKey(session, invoiceId, ettn));

    private static string Key(Guid tenantId, string userId) => $"gibportal:{tenantId:N}:{userId.ToLowerInvariant()}";

    private static string SmsKey(GibPortalSession session, Guid invoiceId, Guid ettn) =>
        $"gibsms:{session.TenantId:N}:{session.OwnerUserId.ToLowerInvariant()}:{session.Environment}:{session.ConnectedAt.UtcTicks}:{invoiceId:N}:{ettn:N}";

    private sealed record Entry(GibPortalSession Session, string ProtectedToken);
}
