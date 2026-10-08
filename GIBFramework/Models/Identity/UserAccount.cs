namespace GIBFramework.Models.Identity;

public sealed class UserAccount
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public string UserCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockoutUntil { get; set; }

    public string? TotpSecretProtected { get; set; }

    public bool TotpEnabled { get; set; }

    public long? TotpLastStep { get; set; }

    public Guid SecurityStamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public bool IsLockedOut(DateTimeOffset now) => LockoutUntil is { } until && until > now;
}

public sealed record UserView(
    Guid Id,
    Guid? TenantId,
    string UserCode,
    string DisplayName,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool MustChangePassword,
    bool TotpEnabled,
    bool IsLockedOut,
    DateTimeOffset? LastLoginAt)
{
    public static UserView From(UserAccount u, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(u);
        return new(u.Id, u.TenantId, u.UserCode, u.DisplayName, u.Email, u.Phone, u.Roles, u.IsActive, u.MustChangePassword, u.TotpEnabled, u.IsLockedOut(now), u.LastLoginAt);
    }
}
