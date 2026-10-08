using System.Globalization;
using GIBFramework.DAL.Identity;
using GIBFramework.Infrastructure.Background;
using GIBFramework.Services.Integrations;
using GIBFramework.Models.Identity;
using GIBFramework.Models.Messaging;

namespace GIBFramework.Services.Messaging;

public sealed class UserNotifier(NotificationService notifications, IUserRepository users)
{
    public Task<NotificationResult> CredentialsAsync(UserAccount user, string temporaryPassword, bool welcome, IReadOnlyCollection<MessageChannel> channels, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        return notifications.TryNotifyAsync(new NotificationRequest(
            user.TenantId,
            welcome ? TemplateKeys.UserWelcome : TemplateKeys.UserPasswordReset,
            [Recipient(user)],
            UserValues(user),
            actor,
            "User",
            user.Id.ToString(),
            Secrets: new Dictionary<string, string> { [TemplateCatalog.SecretPassword] = temporaryPassword },
            Channels: channels,
            Force: true), ct);
    }

    public async Task ResetRequestedAsync(UserAccount requester, string? ip, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requester);
        var rank = Roles.MaxRank(requester.Roles);
        var managers = (await users.ListAsync(requester.TenantId, ct))
            .Where(u => u.IsActive && u.Id != requester.Id && u.Roles.Any(r => Policies.Map[Policies.UserManage].Contains(r)) && Roles.MaxRank(u.Roles) > rank)
            .ToList();
        foreach (var manager in managers)
        {
            var values = UserValues(manager);
            values["talep.kullanici"] = requester.UserCode;
            values["talep.ip"] = ip ?? "-";
            await notifications.TryNotifyAsync(new NotificationRequest(requester.TenantId, TemplateKeys.UserPasswordResetRequested, [Recipient(manager)], values,
                "system", "User", requester.Id.ToString()), ct);
        }
    }

    public Task LockedAsync(UserAccount user, DateTimeOffset until, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        var values = UserValues(user);
        values["kilit.bitis"] = TurkeyTime.ToTurkey(until).ToString("HH:mm", CultureInfo.InvariantCulture);
        return notifications.TryNotifyAsync(new NotificationRequest(user.TenantId, TemplateKeys.UserLocked, [Recipient(user)], values, "system", "User", user.Id.ToString()), ct);
    }

    public async Task ApiKeyCreatedAsync(Guid tenantId, string name, IReadOnlyList<string> roles, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var owners = (await users.ListAsync(tenantId, ct)).Where(u => u.IsActive && u.Roles.Contains(Roles.TenantOwner)).ToList();
        var labels = string.Join(", ", roles.Select(r => Roles.Catalog.FirstOrDefault(c => c.Code == r)?.Label ?? r));
        foreach (var owner in owners)
        {
            var values = UserValues(owner);
            values["anahtar.ad"] = name;
            values["anahtar.yetkiler"] = labels;
            values["islem_yapan"] = actor;
            await notifications.TryNotifyAsync(new NotificationRequest(tenantId, TemplateKeys.ApiKeyCreated, [Recipient(owner)], values, "system", "ApiKey", name), ct);
        }
    }

    private static NotificationRecipient Recipient(UserAccount user) => new(user.DisplayName, user.Email, user.Phone);

    private static Dictionary<string, string?> UserValues(UserAccount user) => new(StringComparer.Ordinal)
    {
        ["kullanici.ad"] = user.DisplayName,
        ["kullanici.kod"] = user.UserCode,
    };
}

public sealed class MessagingWorker(IServiceScopeFactory scopes, MessagingOptions options, ILogger<MessagingWorker> logger) : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(Math.Clamp(options.WorkerIntervalSeconds, 2, 300));

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        await services.GetRequiredService<EventRouter>().ProcessPendingAsync(ct);
        await services.GetRequiredService<MessageDeliveryService>().SendDueAsync(ct);
        await services.GetRequiredService<IntegrationService>().SendDueAsync(ct);
        await services.GetRequiredService<IntegrationService>().SyncDueAsync(ct);
    }
}
