using System.Globalization;
using GIBFramework.DAL.Messaging;
using GIBFramework.DAL.Tenants;
using GIBFramework.Models.Messaging;

namespace GIBFramework.Services.Messaging;

public sealed record NotificationRequest(
    Guid? TenantId,
    string TemplateKey,
    IReadOnlyList<NotificationRecipient> Recipients,
    IReadOnlyDictionary<string, string?> Values,
    string CreatedBy,
    string? EntityType = null,
    string? EntityId = null,
    IReadOnlyList<MessageAttachmentRef>? Attachments = null,
    IReadOnlyDictionary<string, string>? Secrets = null,
    IReadOnlyCollection<MessageChannel>? Channels = null,
    bool Force = false,
    bool Hold = false);

public sealed record NotificationResult(int Queued, IReadOnlyList<string> Skipped)
{
    public IReadOnlyList<Guid> MessageIds { get; init; } = [];
}

public sealed partial class NotificationService(
    MessagingSettingsService settings,
    ITenantRepository tenants,
    TenantLogoRepository logos,
    MessageRepository messages,
    AppOptions app,
    IClock clock,
    ILogger<NotificationService> logger)
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<NotificationResult> TryNotifyAsync(NotificationRequest request, CancellationToken ct)
    {
        try
        {
            return await NotifyAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.NotifyFailed(logger, request.TemplateKey, ex);
            return new NotificationResult(0, [ex.Message]);
        }
    }

    public async Task<NotificationResult> NotifyAsync(NotificationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var def = TemplateCatalog.Find(request.TemplateKey) ?? throw new NotFoundException($"Şablon bulunamadı: {request.TemplateKey}");
        var config = await settings.GetAsync(request.TenantId, ct);
        var (values, brand) = await ContextAsync(request.TenantId, config, ct);
        foreach (var (k, v) in request.Values)
        {
            values[k] = v;
        }

        var secretNames = def.SecretVariables;
        var protectedSecrets = (request.Secrets ?? new Dictionary<string, string>())
            .Where(s => secretNames.Contains(s.Key))
            .ToDictionary(s => s.Key, s => settings.ProtectSecret(s.Value), StringComparer.Ordinal);

        var skipped = new List<string>();
        var ids = new List<Guid>();
        var queued = 0;
        var now = clock.UtcNow;
        foreach (var channel in new[] { MessageChannel.Email, MessageChannel.Sms })
        {
            if (!def.Supports(channel) || (request.Channels is not null && !request.Channels.Contains(channel)))
            {
                continue;
            }

            var label = channel == MessageChannel.Email ? "E-posta" : "SMS";
            var template = MessagingSettingsService.Effective(config, def, channel);
            if (!template.Enabled && !request.Force)
            {
                skipped.Add($"{label} şablonu kapalı.");
                continue;
            }

            if (channel == MessageChannel.Email ? !settings.SmtpConfigured(config) : !settings.SmsConfigured(config))
            {
                skipped.Add($"{label} gönderimi yapılandırılmamış.");
                continue;
            }

            foreach (var recipient in request.Recipients)
            {
                var address = channel == MessageChannel.Email
                    ? (MessagingSettingsService.IsEmail(recipient.Email) ? recipient.Email!.Trim() : null)
                    : NetgsmClient.NormalizePhone(recipient.Phone);
                if (address is null)
                {
                    skipped.Add($"{recipient.Name}: {(channel == MessageChannel.Email ? "geçerli e-posta adresi yok" : "geçerli cep telefonu yok")}.");
                    continue;
                }

                var scoped = new Dictionary<string, string?>(values, StringComparer.Ordinal);
                scoped.TryAdd("kullanici.ad", recipient.Name);
                string? subject = null;
                string body;
                if (channel == MessageChannel.Email)
                {
                    subject = TemplateRenderer.Render(template.Subject ?? def.Label, scoped, html: false, keep: secretNames.ToList());
                    body = TemplateRenderer.Layout(TemplateRenderer.Render(template.Body, scoped, html: true, keep: secretNames.ToList()), brand);
                }
                else
                {
                    body = TemplateRenderer.Render(template.Body, scoped, html: false, keep: secretNames.ToList());
                }

                var extras = new MessageExtras { Secrets = protectedSecrets };
                if (channel == MessageChannel.Email)
                {
                    if (brand.HasLogo && request.TenantId is not null)
                    {
                        extras.Attachments.Add(new MessageAttachmentRef("Logo", request.TenantId.Value));
                    }

                    extras.Attachments.AddRange(request.Attachments ?? []);
                }

                var id = Guid.CreateVersion7(now);
                await messages.InsertAsync(new OutboundMessage
                {
                    Id = id,
                    TenantId = request.TenantId,
                    Channel = channel,
                    Recipient = address,
                    RecipientName = recipient.Name,
                    Subject = subject,
                    Body = body,
                    TemplateKey = def.Key,
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    Extras = extras,
                    Status = MessageStatus.Queued,
                    NextAttemptAt = request.Hold ? now.AddMinutes(10) : now,
                    CreatedBy = request.CreatedBy,
                    CreatedAt = now,
                }, ct);
                ids.Add(id);
                queued++;
            }
        }

        return new NotificationResult(queued, skipped) { MessageIds = ids };
    }

    public async Task<(Dictionary<string, string?> Values, EmailBranding Brand)> ContextAsync(Guid? tenantId, MessagingSettings config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["panel.link"] = app.BaseUrl,
            ["marka.renk"] = TemplateRenderer.SafeColor(config.BrandColor),
            ["tarih"] = clock.TurkeyNow.ToString("dd.MM.yyyy HH:mm", Tr),
        };
        var companyName = app.ProductName;
        string? lines = null;
        var hasLogo = false;
        if (tenantId is { } t && await tenants.GetAsync(t, ct) is { } tenant)
        {
            var p = tenant.Profile;
            companyName = string.IsNullOrWhiteSpace(p.Title) ? tenant.Name : p.Title;
            values["firma.unvan"] = companyName;
            values["firma.vkn"] = p.TaxId;
            values["firma.eposta"] = p.Email;
            values["firma.telefon"] = p.Phone;
            values["firma.adres"] = p.FullAddress;
            lines = string.Join("\n", new[]
            {
                p.FullAddress,
                string.IsNullOrWhiteSpace(p.TaxOffice) ? $"VKN/TCKN: {p.TaxId}" : $"{p.TaxOffice} V.D. · {p.TaxId}",
                string.Join(" · ", new[] { p.Phone, p.Email }.Where(x => !string.IsNullOrWhiteSpace(x))),
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            hasLogo = await logos.InfoAsync(t, ct) is not null;
        }
        else
        {
            values["firma.unvan"] = companyName;
        }

        return (values, new EmailBranding(companyName, lines, config.EmailFooter, TemplateRenderer.SafeColor(config.BrandColor), hasLogo));
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Bildirim kuyruğa alınamadı ({Template}).")]
        public static partial void NotifyFailed(ILogger logger, string template, Exception exception);
    }
}
