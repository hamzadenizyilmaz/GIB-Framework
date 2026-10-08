using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using GIBFramework.DAL.Identity;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Identity;

namespace GIBFramework.Services.Auth;

public sealed record CreateUserCommand(string UserCode, string DisplayName, string? Email, Guid? TenantId, IReadOnlyList<string> Roles, string InitialPassword, string? Phone = null);

public sealed record UserChanges(IReadOnlyList<string>? Roles, bool? IsActive, string? DisplayName, string? Email, string? Phone);

public sealed partial class UserAdminService(
    IUserRepository users,
    AuthService auth,
    Messaging.UserNotifier notifier,
    IAuditTrail audit,
    ITenantContext context,
    IClock clock,
    Microsoft.AspNetCore.Http.IHttpContextAccessor http)
{
    public async Task<(UserView User, Messaging.NotificationResult? Notification)> CreateAsync(CreateUserCommand command, IReadOnlyCollection<Models.Messaging.MessageChannel> sendCredentials, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sendCredentials);
        ArgumentNullException.ThrowIfNull(command);
        var tenantId = ResolveTenantScope(command.TenantId);
        GuardRoles(command.Roles);
        if (!UserCodePattern().IsMatch(command.UserCode))
        {
            throw new ValidationFailedException("USER_CODE", "Kullanıcı kodu 3-50 karakter; harf, rakam, nokta, tire veya alt çizgi içerebilir.", []);
        }

        var now = clock.UtcNow;
        var user = new UserAccount
        {
            Id = Guid.CreateVersion7(now),
            TenantId = tenantId,
            UserCode = UserRepository.Normalize(command.UserCode),
            DisplayName = command.DisplayName.Trim(),
            Email = CleanEmail(command.Email),
            Phone = CleanPhone(command.Phone),
            Roles = [.. command.Roles.Distinct()],
            IsActive = true,
            CreatedAt = now,
        };
        auth.SetPassword(user, command.InitialPassword, mustChange: true);
        await users.InsertAsync(user, ct);
        await AuditAsync(user, "USER_CREATED", new { user.Roles }, ct);
        var notification = sendCredentials.Count > 0
            ? await notifier.CredentialsAsync(user, command.InitialPassword, welcome: true, sendCredentials, context.UserId ?? "system", ct)
            : null;
        return (UserView.From(user, now), notification);
    }

    public async Task<IReadOnlyList<UserView>> ListAsync(CancellationToken ct)
    {
        Guid? scope = IsPlatformAdmin ? null : context.RequireTenant();
        var now = clock.UtcNow;
        return [.. (await users.ListAsync(scope, ct)).Select(u => UserView.From(u, now))];
    }

    public async Task<UserView> UpdateAsync(Guid id, UserChanges changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var (roles, isActive) = (changes.Roles, changes.IsActive);
        var user = await LoadInScopeAsync(id, ct);
        GuardTarget(user.Roles);
        if (changes.DisplayName is { } name && !string.IsNullOrWhiteSpace(name))
        {
            user.DisplayName = name.Trim();
        }

        if (changes.Email is not null)
        {
            user.Email = CleanEmail(changes.Email);
        }

        if (changes.Phone is not null)
        {
            user.Phone = CleanPhone(changes.Phone);
        }
        if (string.Equals(user.UserCode, context.UserId, StringComparison.OrdinalIgnoreCase) && isActive == false)
        {
            throw new DomainException("USER_SELF_DEACTIVATE", "Kendi hesabınızı pasifleştiremezsiniz.");
        }

        if (roles is not null)
        {
            GuardRoles(roles, user.Roles);
            user.Roles = [.. roles.Distinct()];
        }

        if (isActive is { } active)
        {
            user.IsActive = active;
        }

        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = clock.UtcNow;
        await users.UpdateAsync(user, ct);
        await AuditAsync(user, "USER_UPDATED", new { user.Roles, user.IsActive, user.DisplayName, user.Email, user.Phone }, ct);
        return UserView.From(user, clock.UtcNow);
    }

    public async Task<Messaging.NotificationResult?> ResetPasswordAsync(Guid id, string temporaryPassword, IReadOnlyCollection<Models.Messaging.MessageChannel> send, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(send);
        var user = await LoadInScopeAsync(id, ct);
        GuardTarget(user.Roles);
        auth.SetPassword(user, temporaryPassword, mustChange: true);
        await users.UpdateAsync(user, ct);
        await AuditAsync(user, "USER_PASSWORD_RESET", new { notified = send }, ct);
        return send.Count > 0 ? await notifier.CredentialsAsync(user, temporaryPassword, welcome: false, send, context.UserId ?? "system", ct) : null;
    }

    private static string? CleanEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return Messaging.MessagingSettingsService.IsEmail(email.Trim())
            ? email.Trim()
            : throw new ValidationFailedException("USER_EMAIL", "Geçerli bir e-posta adresi girin.", []);
    }

    private static string? CleanPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        return Messaging.NetgsmClient.NormalizePhone(phone) is { } normalized && normalized.Length == 10 && normalized[0] == '5'
            ? normalized
            : throw new ValidationFailedException("USER_PHONE", "Geçerli bir cep telefonu girin (5XX XXX XX XX).", []);
    }

    private bool IsPlatformAdmin => http.HttpContext?.User.IsInRole(Roles.PlatformSuperAdmin) == true;

    private Guid? ResolveTenantScope(Guid? requested)
    {
        if (IsPlatformAdmin)
        {
            return requested;
        }

        var own = context.RequireTenant();
        return requested is null || requested == own
            ? own
            : throw new ForbiddenOperationException("USER_TENANT_SCOPE", "Yalnızca kendi firmanızda kullanıcı yönetebilirsiniz.");
    }

    private void GuardRoles(IReadOnlyList<string> roles, IReadOnlyList<string>? currentRolesOfTarget = null)
    {
        var unknown = roles.Except(Roles.All).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationFailedException("USER_ROLES", "Tanımsız rol.", unknown);
        }

        if (IsPlatformAdmin)
        {
            return;
        }

        if (roles.Contains(Roles.PlatformSuperAdmin))
        {
            throw new ForbiddenOperationException("USER_ROLE_ESCALATION", "Platform yöneticisi rolünü yalnızca platform yöneticisi verebilir.");
        }

        var actorRank = Roles.MaxRank(context.Roles);
        var tooHigh = roles.Where(r => Roles.Rank(r) >= actorRank).ToList();
        if (tooHigh.Count > 0)
        {
            throw new ForbiddenOperationException("USER_ROLE_ESCALATION",
                $"Yalnızca kendi yetki seviyenizin altındaki rolleri atayabilirsiniz: {string.Join(", ", tooHigh.Select(Label))}.");
        }

        GuardTarget(currentRolesOfTarget);
    }

    private void GuardTarget(IReadOnlyList<string>? targetRoles)
    {
        if (IsPlatformAdmin || targetRoles is null)
        {
            return;
        }

        if (Roles.MaxRank(targetRoles) >= Roles.MaxRank(context.Roles))
        {
            throw new ForbiddenOperationException("USER_RANK", "Kendi yetki seviyenizdeki veya üstündeki bir kullanıcıyı değiştiremezsiniz.");
        }
    }

    private static string Label(string role) => Roles.Catalog.FirstOrDefault(r => r.Code == role)?.Label ?? role;

    private async Task<UserAccount> LoadInScopeAsync(Guid id, CancellationToken ct)
    {
        var user = await users.GetAsync(id, ct) ?? throw new NotFoundException("Kullanıcı bulunamadı.");
        if (!IsPlatformAdmin && user.TenantId != context.RequireTenant())
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        return user;
    }

    private Task AuditAsync(UserAccount user, string action, object? data, CancellationToken ct) =>
        audit.AppendSystemAsync(user.TenantId ?? Guid.Empty, new AuditEntry(action, "User", user.UserCode, "Success", new { by = context.UserId, data }), null, ct);

    [GeneratedRegex("^[A-Za-z0-9._-]{3,50}$")]
    private static partial Regex UserCodePattern();
}

public sealed partial class UserBootstrapper(IUserRepository users, IPasswordHasher<UserAccount> hasher, BootstrapOptions options, IClock clock, ILogger<UserBootstrapper> logger)
{
    public async Task EnsureAdminAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.AdminUserCode) || string.IsNullOrWhiteSpace(options.AdminPassword) || await users.AnyAsync(ct))
        {
            return;
        }

        var now = clock.UtcNow;
        var admin = new UserAccount
        {
            Id = Guid.CreateVersion7(now),
            UserCode = UserRepository.Normalize(options.AdminUserCode),
            DisplayName = "Platform Yöneticisi",
            Roles = [Roles.PlatformSuperAdmin],
            IsActive = true,
            MustChangePassword = options.AdminMustChangePassword,
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        admin.PasswordHash = hasher.HashPassword(admin, options.AdminPassword);
        await users.InsertAsync(admin, ct);
        Log.Created(logger, admin.UserCode);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "İlk kurulum: platform yöneticisi '{UserCode}' oluşturuldu. Şifreyi değiştirin ve MFA'yı etkinleştirin.")]
        public static partial void Created(ILogger logger, string userCode);
    }
}
