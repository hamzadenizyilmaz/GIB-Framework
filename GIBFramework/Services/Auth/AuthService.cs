using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using GIBFramework.DAL.Identity;
using GIBFramework.DAL.Tenants;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Identity;
using GIBFramework.Services.Qr;

namespace GIBFramework.Services.Auth;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
    TotpRequired,
}

public sealed record LoginResult(LoginOutcome Outcome, IssuedToken? Token, UserView? User, DateTimeOffset? LockedUntil);

public sealed record TotpSetup(string Secret, string OtpAuthUri, byte[] QrPng);

public sealed class AuthService(
    IUserRepository users,
    ITenantRepository tenants,
    TokenService tokens,
    IPasswordHasher<UserAccount> hasher,
    IDataProtectionProvider dataProtection,
    QrCodeService qr,
    IAuditTrail audit,
    SecurityPolicyService policies,
    Messaging.UserNotifier notifier,
    ITenantContext context,
    IClock clock)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly UserAccount Dummy = new() { UserCode = "dummy" };

    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<UserAccount>().HashPassword(Dummy, Guid.NewGuid().ToString()));
    private readonly IDataProtector _totpProtector = dataProtection.CreateProtector("GIBFramework.Totp.v1");

    public async Task<LoginResult> LoginAsync(string userCode, string password, string? totpCode, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var user = await users.FindByCodeAsync(userCode, ct);
        if (user is null || !user.IsActive)
        {
            _ = hasher.VerifyHashedPassword(Dummy, DummyHash.Value, password ?? string.Empty);
            await AuditAsync(null, "AUTH_LOGIN_FAILED", userCode, "Failure", "Kullanıcı yok veya pasif", ct);
            return new LoginResult(LoginOutcome.InvalidCredentials, null, null, null);
        }

        if (user.IsLockedOut(now))
        {
            await AuditAsync(user, "AUTH_LOGIN_LOCKED", user.UserCode, "Failure", "Hesap kilitli", ct);
            return new LoginResult(LoginOutcome.LockedOut, null, null, user.LockoutUntil);
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? string.Empty);
        if (verification == PasswordVerificationResult.Failed)
        {
            return await FailAsync(user, "Hatalı şifre", ct);
        }

        if (user.TotpEnabled)
        {
            if (string.IsNullOrWhiteSpace(totpCode))
            {
                return new LoginResult(LoginOutcome.TotpRequired, null, null, null);
            }

            var step = Totp.Verify(UnprotectSecret(user), totpCode, now, user.TotpLastStep);
            if (step is null)
            {
                return await FailAsync(user, "Hatalı doğrulama kodu", ct);
            }

            user.TotpLastStep = step;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password!);
        }

        if (user.TenantId is { } tenantId && (await tenants.GetAsync(tenantId, ct))?.IsActive != true)
        {
            await AuditAsync(user, "AUTH_LOGIN_FAILED", user.UserCode, "Failure", "Firma pasif", ct);
            return new LoginResult(LoginOutcome.InvalidCredentials, null, null, null);
        }

        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await users.UpdateAsync(user, ct);
        await AuditAsync(user, "AUTH_LOGIN_SUCCESS", user.UserCode, "Success", null, ct);
        TimeSpan? lifetime = null;
        if (user.TenantId is { } policyTenant && (await policies.GetAsync(policyTenant, ct)).SessionMinutes is { } minutes)
        {
            lifetime = TimeSpan.FromMinutes(minutes);
        }

        return new LoginResult(LoginOutcome.Success, tokens.IssueForUser(user, user.TotpEnabled, lifetime), UserView.From(user, now), null);
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await CurrentUserAsync(ct);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            throw new ForbiddenOperationException("PASSWORD_WRONG", "Mevcut şifre hatalı.");
        }

        SetPassword(user, newPassword, mustChange: false);
        await users.UpdateAsync(user, ct);
        await AuditAsync(user, "AUTH_PASSWORD_CHANGED", user.UserCode, "Success", null, ct);
    }

    public async Task LogoutEverywhereAsync(CancellationToken ct)
    {
        var user = await CurrentUserAsync(ct);
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = clock.UtcNow;
        await users.UpdateAsync(user, ct);
        await AuditAsync(user, "AUTH_LOGOUT", user.UserCode, "Success", null, ct);
    }

    public async Task<TotpSetup> BeginTotpSetupAsync(CancellationToken ct)
    {
        var user = await CurrentUserAsync(ct);
        if (user.TotpEnabled)
        {
            throw new DomainException("TOTP_ALREADY_ENABLED", "İki adımlı doğrulama zaten etkin.");
        }

        var secret = Totp.NewSecret();
        user.TotpSecretProtected = _totpProtector.Protect(Convert.ToBase64String(secret));
        user.UpdatedAt = clock.UtcNow;
        await users.UpdateAsync(user, ct);
        var uri = Totp.OtpAuthUri("GIB Framework", user.UserCode, secret);
        return new TotpSetup(Totp.ToBase32(secret), uri, qr.RenderPng(uri, 5));
    }

    public async Task EnableTotpAsync(string code, CancellationToken ct)
    {
        var user = await CurrentUserAsync(ct);
        if (user.TotpSecretProtected is null)
        {
            throw new DomainException("TOTP_NOT_STARTED", "Önce iki adımlı doğrulama kurulumunu başlatın.");
        }

        var step = Totp.Verify(UnprotectSecret(user), code, clock.UtcNow, null)
            ?? throw new ValidationFailedException("TOTP_INVALID", "Doğrulama kodu hatalı.", []);
        user.TotpEnabled = true;
        user.TotpLastStep = step;
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = clock.UtcNow;
        await users.UpdateAsync(user, ct);
        await AuditAsync(user, "AUTH_TOTP_ENABLED", user.UserCode, "Success", null, ct);
    }

    public async Task<UserView> MeAsync(CancellationToken ct) => UserView.From(await CurrentUserAsync(ct), clock.UtcNow);

    public void SetPassword(UserAccount user, string password, bool mustChange)
    {
        ArgumentNullException.ThrowIfNull(user);
        var errors = PasswordPolicy.Validate(password, user.UserCode);
        if (errors.Count > 0)
        {
            throw new ValidationFailedException("PASSWORD_POLICY", "Şifre politikaya uymuyor.", errors);
        }

        user.PasswordHash = hasher.HashPassword(user, password);
        user.MustChangePassword = mustChange;
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = clock.UtcNow;
    }

    private async Task<LoginResult> FailAsync(UserAccount user, string reason, CancellationToken ct)
    {
        var now = clock.UtcNow;
        user.FailedLoginCount++;
        var locked = false;
        if (user.FailedLoginCount >= MaxFailedAttempts)
        {
            user.LockoutUntil = now.Add(LockoutDuration);
            user.FailedLoginCount = 0;
            locked = true;
        }

        user.UpdatedAt = now;
        await users.UpdateAsync(user, ct);
        if (locked)
        {
            await notifier.LockedAsync(user, user.LockoutUntil!.Value, ct);
        }
        await AuditAsync(user, "AUTH_LOGIN_FAILED", user.UserCode, "Failure", reason, ct);
        return user.LockoutUntil is { } until && until > now
            ? new LoginResult(LoginOutcome.LockedOut, null, null, until)
            : new LoginResult(LoginOutcome.InvalidCredentials, null, null, null);
    }

    private async Task<UserAccount> CurrentUserAsync(CancellationToken ct) =>
        await users.FindByCodeAsync(context.RequireUser(), ct) ?? throw new NotFoundException("Kullanıcı hesabı bulunamadı (yerel hesap değil).");

    private byte[] UnprotectSecret(UserAccount user) => Convert.FromBase64String(_totpProtector.Unprotect(user.TotpSecretProtected!));

    private Task AuditAsync(UserAccount? user, string action, string userCode, string result, string? reason, CancellationToken ct) =>
        audit.AppendSystemAsync(
            user?.TenantId ?? Guid.Empty,
            new AuditEntry(action, "User", UserRepository.Normalize(userCode), result, new { ip = context.Ip }, reason),
            null,
            ct);
}
