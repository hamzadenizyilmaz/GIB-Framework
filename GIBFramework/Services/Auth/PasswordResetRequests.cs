using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GIBFramework.Base.Extensions;
using GIBFramework.Controllers;
using GIBFramework.DAL;
using GIBFramework.DAL.Identity;
using GIBFramework.Models.Audit;

namespace GIBFramework.Services.Auth;

public sealed record PasswordResetRequestInfo(
    Guid Id,
    Guid UserId,
    Guid? TenantId,
    string UserCode,
    string? DisplayName,
    string? Ip,
    DateTimeOffset RequestedAt,
    string Status,
    string? HandledBy,
    DateTimeOffset? HandledAt);

public sealed record ForgotPasswordRequest([Required, StringLength(50, MinimumLength = 3)] string UserCode);

public sealed record CloseResetRequest([Required, RegularExpression("^(Done|Dismissed)$")] string Status);

public sealed class PasswordResetRequestService(
    SqlConnectionFactory connections,
    IUserRepository users,
    IAuditTrail audit,
    ITenantContext context,
    IClock clock,
    IHttpContextAccessor http,
    Messaging.UserNotifier notifier)
{
    public async Task SubmitAsync(string userCode, CancellationToken ct)
    {
        var user = await users.FindByCodeAsync(UserRepository.Normalize(userCode), ct);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var now = clock.UtcNow;
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            IF NOT EXISTS (SELECT 1 FROM dbo.PasswordResetRequests WHERE UserId = @u AND Status = 'Open' AND RequestedAt > DATEADD(HOUR, -1, @at))
                INSERT dbo.PasswordResetRequests (Id, UserId, TenantId, UserCode, Ip, RequestedAt, Status)
                VALUES (@id, @u, @t, @code, @ip, @at, 'Open');
            """);
        var inserted = await cmd.With("@id", Guid.CreateVersion7(now)).With("@u", user.Id).With("@t", user.TenantId).With("@code", user.UserCode)
            .With("@ip", http.HttpContext?.Connection.RemoteIpAddress?.ToString()).With("@at", now).ExecuteNonQueryAsync(ct);
        if (inserted > 0)
        {
            await notifier.ResetRequestedAsync(user, http.HttpContext?.Connection.RemoteIpAddress?.ToString(), ct);
        }
        await audit.AppendSystemAsync(user.TenantId ?? Guid.Empty, new AuditEntry("PASSWORD_RESET_REQUESTED", "User", user.UserCode, "Success",
            new { ip = http.HttpContext?.Connection.RemoteIpAddress?.ToString() }), null, ct);
    }

    public async Task<IReadOnlyList<PasswordResetRequestInfo>> ListAsync(bool platform, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT TOP (200) r.*, u.DisplayName FROM dbo.PasswordResetRequests r JOIN dbo.Users u ON u.Id = r.UserId
             WHERE (@platform = 1 OR r.TenantId = @t)
             ORDER BY CASE WHEN r.Status = 'Open' THEN 0 ELSE 1 END, r.RequestedAt DESC
            """);
        cmd.With("@platform", platform).With("@t", platform ? null : context.RequireTenant());
        var list = new List<PasswordResetRequestInfo>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new PasswordResetRequestInfo(
                r.Get<Guid>("Id"), r.Get<Guid>("UserId"), r.IsDBNull(r.GetOrdinal("TenantId")) ? null : r.Get<Guid>("TenantId"),
                r.Get<string>("UserCode"), r.GetNullableString("DisplayName"), r.GetNullableString("Ip"), r.Get<DateTimeOffset>("RequestedAt"),
                r.Get<string>("Status"), r.GetNullableString("HandledBy"), r.GetNullableDateTimeOffset("HandledAt")));
        }

        return list;
    }

    public async Task<bool> CloseAsync(Guid id, string status, bool platform, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.PasswordResetRequests SET Status = @s, HandledBy = @by, HandledAt = @at
             WHERE Id = @id AND Status = 'Open' AND (@platform = 1 OR TenantId = @t)
            """);
        cmd.With("@s", status).With("@by", context.RequireUser()).With("@at", clock.UtcNow).With("@id", id)
            .With("@platform", platform).With("@t", platform ? null : context.RequireTenant());
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }
}

[ApiController]
[Route("api/v1/auth/password-reset-requests")]
[AllowAnonymous]
public sealed class ForgotPasswordController(PasswordResetRequestService service) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(AuthController.LoginRateLimitPolicy)]
    public async Task<IActionResult> Submit([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await service.SubmitAsync(request.UserCode, ct);
        return Accepted(new { message = "Talebiniz alındı. Yöneticiniz şifrenizi sıfırladığında size geçici şifre iletilecektir." });
    }
}

[ApiController]
[Route("api/v1/users/password-reset-requests")]
[Authorize(Policy = Policies.UserManage)]
public sealed class PasswordResetRequestsController(PasswordResetRequestService service) : ControllerBase
{
    private bool Platform => User.IsInRole(Roles.PlatformSuperAdmin);

    [HttpGet]
    public async Task<IReadOnlyList<PasswordResetRequestInfo>> List(CancellationToken ct) => await service.ListAsync(Platform, ct);

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseResetRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await service.CloseAsync(id, request.Status, Platform, ct) ? NoContent() : NotFound();
    }
}

[ApiController]
[Route("api/v1/roles")]
[Authorize]
public sealed class RolesController(ITenantContext context) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        var platform = User.IsInRole(Roles.PlatformSuperAdmin);
        var actorRank = Roles.MaxRank(context.Roles);
        return Ok(new
        {
            roles = Roles.Catalog.Select(r => new
            {
                r.Code,
                r.Label,
                r.Group,
                r.Rank,
                r.Description,
                policies = Policies.Map.Where(p => p.Value.Contains(r.Code)).Select(p => p.Key).ToArray(),
                assignable = platform || (r.Code != Roles.PlatformSuperAdmin && r.Rank < actorRank),
            }),
            policies = Policies.Catalog,
        });
    }
}
