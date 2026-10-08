using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GIBFramework.DTOs;
using GIBFramework.Services.Auth;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    public const string LoginRateLimitPolicy = "login";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.UserCode, request.Password, request.TotpCode, ct);
        return result.Outcome switch
        {
            LoginOutcome.Success => Ok(new LoginResponse(result.Token!.AccessToken, "Bearer", result.Token.ExpiresAt, result.User!)),
            LoginOutcome.TotpRequired => Problem(StatusCodes.Status401Unauthorized, "TOTP_REQUIRED", "İki adımlı doğrulama kodu gerekli."),
            LoginOutcome.LockedOut => Problem(StatusCodes.Status423Locked, "ACCOUNT_LOCKED", $"Hesap geçici olarak kilitlendi. Tekrar deneme: {result.LockedUntil:HH:mm}."),
            _ => Problem(StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "Kullanıcı kodu veya şifre hatalı."),
        };
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(await auth.MeAsync(ct));

    [HttpGet("permissions")]
    [Authorize]
    [ProducesResponseType<PermissionsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Permissions(
        [FromServices] IAuthorizationService authorization,
        [FromServices] ITenantContext tenant,
        [FromServices] WorkflowOptions workflow,
        [FromServices] SecurityPolicyService securityPolicies)
    {
        var granted = new List<string>();
        foreach (var policy in Policies.Map.Keys)
        {
            if ((await authorization.AuthorizeAsync(User, policy)).Succeeded)
            {
                granted.Add(policy);
            }
        }

        var idle = Guid.TryParse(User.FindFirst(GibFrameworkClaims.TenantId)?.Value, out var ownTenant)
            ? (await securityPolicies.GetAsync(ownTenant, HttpContext.RequestAborted)).IdleLogoutMinutes
            : 0;

        return Ok(new PermissionsResponse(
            tenant.UserId ?? string.Empty,
            User.Identity?.Name,
            tenant.TenantId,
            [.. User.FindAll(GibFrameworkClaims.Role).Select(c => c.Value)],
            granted,
            User.HasClaim(TokenService.PasswordChangeClaim, "true"),
            workflow.RequireMakerChecker,
            [.. workflow.SelfApprovalRoles.Distinct(StringComparer.Ordinal)],
            await Middleware.PasswordChangeMiddleware.MfaMissingAsync(HttpContext, securityPolicies),
            idle));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request.CurrentPassword, request.NewPassword, ct);
        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutEverywhereAsync(ct);
        return NoContent();
    }

    [HttpPost("totp/setup")]
    [Authorize]
    public async Task<IActionResult> TotpSetup(CancellationToken ct)
    {
        var setup = await auth.BeginTotpSetupAsync(ct);
        return Ok(new { setup.Secret, setup.OtpAuthUri, qrPngBase64 = Convert.ToBase64String(setup.QrPng) });
    }

    [HttpPost("totp/enable")]
    [Authorize]
    public async Task<IActionResult> TotpEnable([FromBody] TotpCodeRequest request, CancellationToken ct)
    {
        await auth.EnableTotpAsync(request.Code, ct);
        return NoContent();
    }

    private ObjectResult Problem(int status, string code, string title)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Type = $"https://gibframework.local/errors/{code}" };
        problem.Extensions["code"] = code;
        return StatusCode(status, problem);
    }
}

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = Policies.UserManage)]
public sealed class UsersController(UserAdminService users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await users.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var (user, notification) = await users.CreateAsync(
            new CreateUserCommand(request.UserCode, request.DisplayName, request.Email, request.TenantId, request.Roles, request.InitialPassword, request.Phone),
            request.SendCredentials ?? [],
            ct);
        return StatusCode(StatusCodes.Status201Created, new { user, notification });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct) =>
        Ok(await users.UpdateAsync(id, new UserChanges(request.Roles, request.IsActive, request.DisplayName, request.Email, request.Phone), ct));

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct) =>
        Ok(new { notification = await users.ResetPasswordAsync(id, request.TemporaryPassword, request.Send ?? [], ct) });
}
