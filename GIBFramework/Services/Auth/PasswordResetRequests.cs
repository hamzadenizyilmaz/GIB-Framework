using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GIBFramework.Base.Extensions;
using GIBFramework.Controllers;
using GIBFramework.DAL;
using GIBFramework.DAL.Identity;
using GIBFramework.Infrastructure.Background;
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
    Messaging.UserNotifier notifier,
    ILogger<PasswordResetRequestService> logger)
{
    public async Task SubmitAsync(string userCode, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            INSERT dbo.PasswordResetIntake (Id, UserCode, Ip, RequestedAt) VALUES (@id, @code, @ip, @at);
            """);
        await cmd.With("@id", Guid.CreateVersion7(now)).With("@code", UserRepository.Normalize(userCode))
            .With("@ip", http.HttpContext?.Connection.RemoteIpAddress?.ToString()).With("@at", now).ExecuteNonQueryAsync(ct);
    }

    public async Task DrainIntakeAsync(CancellationToken ct)
    {
        var accepted = new List<(string UserCode, string? Ip)>();
        await using (var c = await connections.OpenSystemAsync(ct))
        {
            await using var cmd = c.Command("""
                SET XACT_ABORT ON;
                SET NOCOUNT ON;
                BEGIN TRANSACTION;
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock @Resource = N'GIBFramework.PasswordResetIntake',
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;

                DECLARE @batch TABLE (Id uniqueidentifier PRIMARY KEY, UserCode nvarchar(50), Ip nvarchar(45), RequestedAt datetimeoffset);
                DECLARE @accepted TABLE (UserCode nvarchar(50), Ip nvarchar(45));
                IF @lockResult >= 0
                BEGIN
                    WITH batch AS (SELECT TOP (100) * FROM dbo.PasswordResetIntake ORDER BY Id)
                    DELETE FROM batch OUTPUT deleted.Id, deleted.UserCode, deleted.Ip, deleted.RequestedAt INTO @batch;

                    WITH candidates AS (
                        SELECT b.Id, u.Id AS UserId, u.TenantId, u.UserCode, b.Ip, b.RequestedAt,
                            ROW_NUMBER() OVER (PARTITION BY u.Id ORDER BY b.Id) AS Occurrence
                        FROM @batch b JOIN dbo.Users u ON u.UserCode = b.UserCode WHERE u.IsActive = 1
                    )
                    INSERT dbo.PasswordResetRequests (Id, UserId, TenantId, UserCode, Ip, RequestedAt, Status)
                    OUTPUT inserted.UserCode, inserted.Ip INTO @accepted
                    SELECT Id, UserId, TenantId, UserCode, Ip, RequestedAt, 'Open' FROM candidates AS c
                    WHERE Occurrence = 1 AND NOT EXISTS (
                        SELECT 1 FROM dbo.PasswordResetRequests r
                        WHERE r.UserId = c.UserId AND r.Status = 'Open' AND r.RequestedAt > DATEADD(HOUR, -1, @now)
                    );
                END;
                COMMIT TRANSACTION;
                SELECT UserCode, Ip FROM @accepted;
                """);
            await using var rows = await cmd.With("@now", clock.UtcNow).ExecuteReaderAsync(ct);
            while (await rows.ReadAsync(ct))
                accepted.Add((rows.Get<string>("UserCode"), rows.GetNullableString("Ip")));
        }

        foreach (var request in accepted)
        {
            try
            {
                var user = await users.FindByCodeAsync(request.UserCode, ct);
                if (user is not { IsActive: true }) continue;
                await audit.AppendSystemAsync(user.TenantId ?? Guid.Empty,
                    new AuditEntry("PASSWORD_RESET_REQUESTED", "User", user.UserCode, "Success", new { ip = request.Ip }), null, ct);
                await notifier.ResetRequestedAsync(user, request.Ip, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Parola sıfırlama isteğinin bildirimi veya denetim kaydı tamamlanamadı.");
            }
        }
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

public sealed class PasswordResetIntakeWorker(IServiceScopeFactory scopes, ILogger<PasswordResetIntakeWorker> logger)
    : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<PasswordResetRequestService>().DrainIntakeAsync(ct);
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
