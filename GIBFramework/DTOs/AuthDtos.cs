using System.ComponentModel.DataAnnotations;

namespace GIBFramework.DTOs;

public sealed record LoginRequest(
    [Required, StringLength(50)] string UserCode,
    [Required, StringLength(200)] string Password,
    [StringLength(6)] string? TotpCode);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, Models.Identity.UserView User);

public sealed record PermissionsResponse(
    string UserId,
    string? DisplayName,
    Guid? TenantId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Policies,
    bool MustChangePassword,
    bool RequireMakerChecker,
    IReadOnlyList<string> SelfApprovalRoles,
    bool MfaRequired = false,
    int IdleLogoutMinutes = 0);

public sealed record ChangePasswordRequest(
    [Required, StringLength(200)] string CurrentPassword,
    [Required, StringLength(200, MinimumLength = 10)] string NewPassword);

public sealed record TotpCodeRequest([Required, StringLength(6, MinimumLength = 6)] string Code);

public sealed record CreateUserRequest(
    [Required, StringLength(50, MinimumLength = 3)] string UserCode,
    [Required, StringLength(200)] string DisplayName,
    [EmailAddress, StringLength(200)] string? Email,
    Guid? TenantId,
    [Required] IReadOnlyList<string> Roles,
    [Required, StringLength(200, MinimumLength = 10)] string InitialPassword,
    [StringLength(20)] string? Phone = null,
    IReadOnlyList<Models.Messaging.MessageChannel>? SendCredentials = null);

public sealed record UpdateUserRequest(
    IReadOnlyList<string>? Roles,
    bool? IsActive,
    [StringLength(200)] string? DisplayName = null,
    [StringLength(200)] string? Email = null,
    [StringLength(20)] string? Phone = null);

public sealed record ResetPasswordRequest(
    [Required, StringLength(200, MinimumLength = 10)] string TemporaryPassword,
    IReadOnlyList<Models.Messaging.MessageChannel>? Send = null);
