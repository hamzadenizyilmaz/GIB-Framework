using GIBFramework.Base.Extensions;
using GIBFramework.Infrastructure.Tenancy;

namespace GIBFramework.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestTenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenant);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.GetTenantId();
            if (context.User.IsInRole(Roles.PlatformSuperAdmin)
                && Guid.TryParse(context.Request.Headers[GibFrameworkHeaders.TenantId].ToString(), out var overrideId))
            {
                tenantId = overrideId;
            }

            tenant.SetUser(
                tenantId,
                context.User.GetUserId(),
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                context.Items[CorrelationIdMiddleware.ItemKey] as string,
                [.. context.User.FindAll(GibFrameworkClaims.Role).Select(c => c.Value)]);
        }

        await next(context);
    }
}
