using GIBFramework.Base.Extensions;
using GIBFramework.DAL.Tenants;
using GIBFramework.Infrastructure.Tenancy;

namespace GIBFramework.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestTenantContext tenant, ITenantRepository tenants)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenant);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.GetTenantId();
            if (context.User.IsInRole(Roles.PlatformSuperAdmin)
                && context.Request.Headers.TryGetValue(GibFrameworkHeaders.TenantId, out var value))
            {
                if (!Guid.TryParse(value.ToString(), out var overrideId) || overrideId == Guid.Empty)
                {
                    throw new ValidationFailedException("TENANT_OVERRIDE", "Firma kimliği geçersiz.", []);
                }
                if (await tenants.GetAsync(overrideId, context.RequestAborted) is not { IsActive: true })
                {
                    throw new NotFoundException("Etkin firma bulunamadı.");
                }
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
