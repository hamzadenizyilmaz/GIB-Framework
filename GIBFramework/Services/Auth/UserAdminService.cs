using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using GIBFramework.Base.Extensions;
using GIBFramework.DAL;
using GIBFramework.DAL.Identity;
using GIBFramework.DAL.Tenants;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Identity;
using GIBFramework.Models.Tenancy;

namespace GIBFramework.Services.Auth;

public sealed record CreateUserCommand(string UserCode, string DisplayName, string? Email, Guid? TenantId, IReadOnlyList<string> Roles, string InitialPassword, string? Phone = null);

public sealed record UserChanges(IReadOnlyList<string>? Roles, bool? IsActive, string? DisplayName, string? Email, string? Phone);

public sealed partial class UserAdminService(
    IUserRepository users,
    DAL.Tenants.ITenantRepository tenants,
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
        GuardRoles(command.Roles, tenantId);
        if (tenantId is { } tenant && await tenants.GetAsync(tenant, ct) is not { IsActive: true })
        {
            throw new NotFoundException("Etkin firma bulunamadı.");
        }
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

    public async Task<IReadOnlyList<UserView>> ListAsync(Guid? tenantId, CancellationToken ct)
    {
        Guid? scope = IsPlatformAdmin ? tenantId : ResolveTenantScope(tenantId);
        var now = clock.UtcNow;
        return [.. (await users.ListAsync(scope, ct)).Select(u => UserView.From(u, now))];
    }

    public async Task<UserView> UpdateAsync(Guid id, UserChanges changes, CancellationToken ct, bool removeAccess = false)
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
            GuardRoles(roles, user.TenantId, user.Roles);
            user.Roles = [.. roles.Distinct()];
        }

        if (isActive is { } active)
        {
            user.IsActive = active;
        }

        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = clock.UtcNow;
        await users.UpdateAsync(user, ct);
        await AuditAsync(user, removeAccess ? "USER_REMOVED" : "USER_UPDATED", new { user.Roles, user.IsActive }, ct);
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
        if (requested == Guid.Empty)
        {
            throw new ValidationFailedException("USER_TENANT", "Geçerli bir firma seçin.", []);
        }

        if (IsPlatformAdmin)
        {
            return requested;
        }

        var own = context.RequireTenant();
        return requested is null || requested == own
            ? own
            : throw new ForbiddenOperationException("USER_TENANT_SCOPE", "Yalnızca kendi firmanızda kullanıcı yönetebilirsiniz.");
    }

    private void GuardRoles(IReadOnlyList<string> roles, Guid? tenantId, IReadOnlyList<string>? currentRolesOfTarget = null)
    {
        var unknown = roles.Except(Roles.All).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationFailedException("USER_ROLES", "Tanımsız rol.", unknown);
        }

        var platformRole = roles.Contains(Roles.PlatformSuperAdmin);
        if (roles.Count == 0 || (platformRole ? tenantId is not null || roles.Any(r => r != Roles.PlatformSuperAdmin) : tenantId is null))
        {
            throw new ValidationFailedException("USER_ROLE_SCOPE", "Platform yöneticisi tek başına ve firmasız atanır. Diğer roller için firma zorunludur.", []);
        }

        if (IsPlatformAdmin)
        {
            return;
        }

        if (platformRole)
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
        return await users.GetAsync(id, IsPlatformAdmin ? null : context.RequireTenant(), ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");
    }

    private Task AuditAsync(UserAccount user, string action, object? data, CancellationToken ct) =>
        audit.AppendActorAsync(user.TenantId ?? Guid.Empty, new AuditEntry(action, "User", user.UserCode, "Success", data), ct);

    [GeneratedRegex("^[A-Za-z0-9._-]{3,50}$")]
    private static partial Regex UserCodePattern();
}

public sealed partial class UserBootstrapper(SqlConnectionFactory connections, IAuditTrail audit, IPasswordHasher<UserAccount> hasher, BootstrapOptions options, IClock clock, ILogger<UserBootstrapper> logger)
{
    public async Task EnsureAdminAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.AdminUserCode) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            return;
        }

        await TryCreateAsync(options.AdminUserCode, options.AdminPassword, options.AdminMustChangePassword, ct);
    }

    public async Task<bool> IsWebInstallAvailableAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenSystemAsync(ct);
        await using var command = connection.Command($"SELECT CASE WHEN {WebInstallClosed} THEN 0 ELSE 1 END");
        return await command.ExecuteScalarAsync(ct) is 1;
    }

    private const string WebInstallClosed = """
        EXISTS (SELECT 1 FROM dbo.Users)
        OR EXISTS (SELECT 1 FROM dbo.Tenants)
        OR EXISTS (SELECT 1 FROM dbo.AuditEvents WHERE TenantId = '00000000-0000-0000-0000-000000000000' AND Action = 'INSTALL_COMPLETED')
        """;

    public async Task<bool> TryCreateAsync(string userCode, string password, bool mustChangePassword, CancellationToken ct,
        Tenant? tenant = null, string? ownerCode = null, string? ownerPassword = null)
    {
        await using var connection = await connections.OpenSystemAsync(ct);
        var scope = await connections.BeginAsync(connection, ct);
        await using var transaction = scope.Transaction;
        await using var command = connection.Command("""
            DECLARE @lock int;
            EXEC @lock = sys.sp_getapplock @Resource = N'GIBFramework.FirstAdministrator', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000;
            IF @lock < 0 THROW 51140, N'Kurulum kilidi alınamadı.', 1;
            SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE TenantId IS NULL) THEN 1 ELSE 0 END;
            """, transaction);
        if (await command.ExecuteScalarAsync(ct) is 1)
            return false;

        if (tenant is not null)
        {
            await using var check = connection.Command($"SELECT CASE WHEN {WebInstallClosed} THEN 1 ELSE 0 END", transaction);
            if (await check.ExecuteScalarAsync(ct) is 1)
                return false;
        }

        var now = clock.UtcNow;
        var admin = new UserAccount
        {
            Id = Guid.CreateVersion7(now),
            UserCode = UserRepository.Normalize(userCode),
            DisplayName = "Platform Yöneticisi",
            Roles = [Roles.PlatformSuperAdmin],
            IsActive = true,
            MustChangePassword = mustChangePassword,
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        await UserRepository.InsertCoreAsync(admin, connection, transaction, ct);
        await audit.AppendSystemAsync(Guid.Empty, new AuditEntry("PLATFORM_ADMIN_CREATED", "User", admin.UserCode), scope, ct);
        if (tenant is not null)
        {
            await TenantRepository.CreateCoreAsync(tenant, connection, transaction, ct);
            var owner = new UserAccount
            {
                Id = Guid.CreateVersion7(now), TenantId = tenant.Id, UserCode = ownerCode!, DisplayName = "Firma Sahibi",
                Roles = [Roles.TenantOwner], IsActive = true, SecurityStamp = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now,
            };
            owner.PasswordHash = hasher.HashPassword(owner, ownerPassword!);
            await UserRepository.InsertCoreAsync(owner, connection, transaction, ct);
            await audit.AppendSystemAsync(tenant.Id, new AuditEntry("TENANT_CREATED", "Tenant", tenant.Id.ToString(), Data: new { tenant.Name }), scope, ct);
            await audit.AppendSystemAsync(tenant.Id, new AuditEntry("USER_CREATED", "User", owner.UserCode, Data: new { owner.Roles }), scope, ct);
            await audit.AppendSystemAsync(Guid.Empty, new AuditEntry("INSTALL_COMPLETED", "Tenant", tenant.Id.ToString()), scope, ct);
        }
        await transaction.CommitAsync(ct);
        Log.Created(logger, admin.UserCode);
        return true;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "İlk kurulum: platform yöneticisi '{UserCode}' oluşturuldu. Şifreyi değiştirin ve MFA'yı etkinleştirin.")]
        public static partial void Created(ILogger logger, string userCode);
    }
}
