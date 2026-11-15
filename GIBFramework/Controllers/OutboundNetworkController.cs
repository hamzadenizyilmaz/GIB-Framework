using GIBFramework.Infrastructure;
using GIBFramework.Models.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIBFramework.Controllers;

/// <summary>Panelin bağlanabileceği webhook ve SMTP sunucuları (site geneli, yalnızca platform yöneticisi).</summary>
[ApiController]
[Route("api/v1/settings/outbound-network")]
[Authorize(Policy = Policies.PlatformAdmin, AuthenticationSchemes = "Bearer")]
public sealed class OutboundNetworkController(OutboundNetwork network, IAuditTrail audit) : ControllerBase
{
    [HttpGet]
    public OutboundNetworkSettings Get() => network.Settings;

    [HttpPut]
    [HttpPost("update")]
    public async Task<IActionResult> Put(OutboundNetworkSettings payload, CancellationToken ct)
    {
        OutboundNetworkSettings saved;
        try
        {
            saved = network.Save(payload);
        }
        catch (InvalidOperationException error)
        {
            return Problem(title: error.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        await audit.AppendActorAsync(Guid.Empty, new AuditEntry("OUTBOUND_NETWORK_UPDATED", "Settings", "outbound-network", Data: saved), ct);
        return Ok(saved);
    }
}
