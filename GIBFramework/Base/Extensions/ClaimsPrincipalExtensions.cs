using System.Security.Claims;

namespace GIBFramework.Base.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.FindFirstValue(GibFrameworkClaims.Subject)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.Identity?.Name
            ?? throw new ForbiddenOperationException("AUTH_NO_SUBJECT", "Kullanıcı kimliği (sub) bulunamadı.");
    }

    public static Guid? GetTenantId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Guid.TryParse(user.FindFirstValue(GibFrameworkClaims.TenantId), out var id) ? id : null;
    }
}
