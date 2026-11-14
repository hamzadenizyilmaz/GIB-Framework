using GIBFramework.Models.Audit;
using Microsoft.AspNetCore.Routing;

namespace GIBFramework.Middleware;

public sealed class OperationAuditMiddleware(RequestDelegate next, ILogger<OperationAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http, ITenantContext tenant, IAuditTrail audit)
    {
        if (!http.Request.Path.StartsWithSegments("/api") || http.GetEndpoint() is not RouteEndpoint route
            || http.User.Identity?.IsAuthenticated != true)
        {
            await next(http);
            return;
        }

        var scope = tenant.TenantId ?? Guid.Empty;
        var outcome = 500;
        try
        {
            await next(http);
            outcome = http.Response.StatusCode;
        }
        catch (Exception ex)
        {
            outcome = ex switch
            {
                OperationCanceledException => 499,
                ForbiddenOperationException => 403,
                NotFoundException => 404,
                ConflictException => 409,
                DomainException or ValidationFailedException => 422,
                ProviderUnavailableException => 503,
                _ => 500,
            };
            throw;
        }
        finally
        {
            try
            {
                var id = http.Request.RouteValues.Values.Select(v => v?.ToString()).FirstOrDefault(v => Guid.TryParse(v, out _));
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var action = route.RoutePattern.RawText?.StartsWith("api/v1/audit", StringComparison.OrdinalIgnoreCase) == true
                    ? "AUDIT_ACCESSED" : "API_REQUEST";
                await audit.AppendActorAsync(scope, new AuditEntry(action, "Endpoint", id,
                    outcome < 400 ? "Success" : "Failure",
                    new { method = http.Request.Method, route = route.RoutePattern.RawText, status = outcome }), deadline.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Denetim kaydı yazılamadı. İz kimliği {TraceId}", http.TraceIdentifier);
            }
        }
    }
}
