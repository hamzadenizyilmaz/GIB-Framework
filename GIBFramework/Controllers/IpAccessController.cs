using GIBFramework.Middleware;
using GIBFramework.Models.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/settings/ip-access")]
[Authorize(Policy = Policies.IpAccessManage, AuthenticationSchemes = "Bearer")]
public sealed class IpAccessController(IpAccessPolicy policy, IAuditTrail audit, ITenantContext tenant) : ControllerBase
{
    private Guid? Scope => User.IsInRole(Roles.PlatformSuperAdmin) ? null : tenant.RequireTenant();

    [HttpGet]
    public IActionResult Get()
    {
        var scope = Scope;
        return ResponseFor(scope, policy.GetSettings(scope));
    }

    [HttpPut]
    public async Task<IActionResult> Put(IpAccessSettings payload, CancellationToken ct)
    {
        var scope = Scope;
        IpAccessSettings saved;
        try
        {
            saved = policy.Save(scope, payload, HttpContext.Connection.RemoteIpAddress);
        }
        catch (InvalidOperationException error)
        {
            return Problem(title: error.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        await audit.AppendActorAsync(scope ?? Guid.Empty, new AuditEntry("IP_ACCESS_UPDATED", "Settings", "ip-access", Data: saved), ct);
        return ResponseFor(scope, saved);
    }

    private IActionResult ResponseFor(Guid? scope, IpAccessSettings settings) => Ok(new
    {
        settings.Enabled, settings.Allow, settings.Deny,
        scope = scope.HasValue ? "Tenant" : "Platform",
        clientIp = HttpContext.Connection.RemoteIpAddress is { IsIPv4MappedToIPv6: true } mapped
            ? mapped.MapToIPv4().ToString() : HttpContext.Connection.RemoteIpAddress?.ToString(),
    });
}
