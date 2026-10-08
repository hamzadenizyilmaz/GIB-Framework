using Microsoft.Extensions.Caching.Memory;
using GIBFramework.DAL.Identity;

namespace GIBFramework.Services.Auth;

public sealed class SecurityStampCache(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    public async Task<Guid?> GetAsync(string userCode, Func<Task<Guid?>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        return await cache.GetOrCreateAsync(Key(userCode), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = Lifetime;
            return await load();
        });
    }

    public void Invalidate(string userCode) => cache.Remove(Key(userCode));

    private static string Key(string userCode) => "sstamp:" + UserRepository.Normalize(userCode);
}
