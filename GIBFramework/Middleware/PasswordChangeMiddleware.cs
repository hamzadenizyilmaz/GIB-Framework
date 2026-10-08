using GIBFramework.Services.Auth;

namespace GIBFramework.Middleware;

public sealed class PasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SecurityPolicyService policies)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policies);
        var path = context.Request.Path;
        var guarded = path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/api/v1/auth", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/api/v1/announcements/active", StringComparison.OrdinalIgnoreCase);

        if (guarded && context.User.HasClaim(TokenService.PasswordChangeClaim, "true"))
        {
            await RejectAsync(context, "PASSWORD_CHANGE_REQUIRED", "Devam etmeden önce şifrenizi değiştirmelisiniz.");
            return;
        }

        if (guarded && await MfaMissingAsync(context, policies))
        {
            await RejectAsync(context, "MFA_REQUIRED", "Firma güvenlik politikası gereği iki adımlı doğrulamayı etkinleştirmelisiniz.");
            return;
        }

        await next(context);
    }

    public static async Task<bool> MfaMissingAsync(HttpContext context, SecurityPolicyService policies)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policies);
        var method = context.User.FindFirst(TokenService.AuthMethodClaim)?.Value;
        if (method is null || method == "mfa"
            || !Guid.TryParse(context.User.FindFirst(GibFrameworkClaims.TenantId)?.Value, out var tenantId))
        {
            return false;
        }

        return (await policies.GetAsync(tenantId, context.RequestAborted)).RequireTotp;
    }

    private static Task RejectAsync(HttpContext context, string code, string title)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(
            new { status = 403, code, title },
            JsonDefaults.Options,
            contentType: "application/problem+json");
    }
}
