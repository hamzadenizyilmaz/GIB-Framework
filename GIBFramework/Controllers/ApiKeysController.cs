using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.Services.Auth;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/api-keys")]
[Authorize(Policy = Policies.ApiKeyManage)]
public sealed class ApiKeysController(ApiKeyService keys) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ApiKeyInfo>> List(CancellationToken ct) => await keys.ListAsync(ct);

    [HttpGet("roles")]
    public IReadOnlyList<string> Roles() => ApiKeyService.AssignableRoles;

    [HttpPost]
    public async Task<ActionResult<CreatedApiKey>> Create([FromBody] CreateApiKeyRequest request, CancellationToken ct) =>
        Ok(await keys.CreateAsync(request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct) =>
        await keys.RevokeAsync(id, ct) ? NoContent() : NotFound();
}
