using System.Text;
using GIBFramework.DAL.Messaging;
using GIBFramework.Models.Messaging;

namespace GIBFramework.Services.Messaging;

public sealed partial class MessageDeliveryService(
    MessageRepository messages,
    MessagingSettingsService settings,
    TenantLogoRepository logos,
    InvoiceDocumentService documents,
    SmtpMailer smtp,
    NetgsmClient netgsm,
    MessagingOptions options,
    IClock clock,
    ILogger<MessageDeliveryService> logger)
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(3), TimeSpan.FromHours(6),
    ];

    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var batch = await messages.ClaimDueAsync(Math.Clamp(options.BatchSize, 1, 100), clock.UtcNow, TimeSpan.FromMinutes(5), ct);
        foreach (var message in batch)
        {
            await DeliverAsync(message, ct);
        }

        return batch.Count;
    }

    public async Task DeliverAsync(OutboundMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            var config = await settings.GetAsync(message.TenantId, ct);
            var providerId = message.Channel == MessageChannel.Email
                ? await SendEmailAsync(message, config, ct)
                : await SendSmsAsync(message, config, ct);
            await messages.MarkSentAsync(message.Id, providerId, clock.UtcNow, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var transient = ex switch
            {
                SmtpException s => s.Transient,
                NetgsmException n => n.Transient,
                PermanentDeliveryException => false,
                _ => true,
            };
            DateTimeOffset? retry = transient && message.Attempts < Math.Max(1, options.MaxAttempts)
                ? clock.UtcNow.Add(Backoff[Math.Clamp(message.Attempts - 1, 0, Backoff.Length - 1)])
                : null;
            Log.DeliveryFailed(logger, message.Id, message.Channel, ex.Message);
            await messages.MarkFailedAsync(message.Id, ex.Message, retry, CancellationToken.None);
        }
    }

    private async Task<string> SendEmailAsync(OutboundMessage message, MessagingSettings config, CancellationToken ct)
    {
        var endpoint = settings.ResolveSmtp(config) ?? throw new PermanentDeliveryException("E-posta (SMTP) ayarları yapılandırılmamış veya kapalı.");
        var secrets = Secrets(message);
        var html = TemplateRenderer.FillSecrets(message.Body, secrets, html: true);
        var subject = TemplateRenderer.FillSecrets(message.Subject ?? string.Empty, secrets, html: false);
        var attachments = new List<MailAttachment>();
        foreach (var a in message.Extras.Attachments)
        {
            switch (a.Kind)
            {
                case "Logo" when await logos.GetAsync(a.RefId, ct) is { } logo:
                    attachments.Add(new MailAttachment("logo" + Extension(logo.ContentType), logo.ContentType, logo.Data, TemplateRenderer.LogoContentId));
                    break;
                case "InvoicePdf" or "InvoiceHtml" or "InvoiceXml" when message.TenantId is { } tenantId && await documents.LoadAsync(tenantId, a.RefId, ct) is { } invoice:
                    var name = InvoiceDocumentService.FileBaseName(invoice);
                    if (a.Kind == "InvoicePdf")
                    {
                        attachments.Add(new MailAttachment(name + ".pdf", "application/pdf", await documents.RenderPdfAsync(invoice, ct)));
                    }
                    else if (a.Kind == "InvoiceHtml")
                    {
                        attachments.Add(new MailAttachment(name + ".html", "text/html; charset=utf-8",
                            Encoding.UTF8.GetBytes(await documents.RenderHtmlAsync(invoice, publicView: false, token: null, ct))));
                    }
                    else if (await documents.SignedXmlAsync(invoice, ct) is { } xml)
                    {
                        attachments.Add(new MailAttachment(name + ".xml", "application/xml", xml));
                    }

                    break;
            }
        }

        if (!html.Contains("cid:" + TemplateRenderer.LogoContentId, StringComparison.Ordinal))
        {
            attachments.RemoveAll(x => x.ContentId is not null);
        }

        return await smtp.SendAsync(endpoint, new MailMessageData(message.Recipient, message.RecipientName, subject, html, TemplateRenderer.ToPlainText(html), attachments), clock.UtcNow, ct);
    }

    private async Task<string> SendSmsAsync(OutboundMessage message, MessagingSettings config, CancellationToken ct)
    {
        var credentials = settings.ResolveSms(config) ?? throw new PermanentDeliveryException("SMS (Netgsm) ayarları yapılandırılmamış veya kapalı.");
        var text = TemplateRenderer.FillSecrets(message.Body, Secrets(message), html: false);
        return await netgsm.SendAsync(credentials, message.Recipient, text, ct);
    }

    private Dictionary<string, string> Secrets(OutboundMessage message) =>
        message.Extras.Secrets.ToDictionary(s => s.Key, s => settings.Unprotect(s.Value) ?? "(geçersiz)", StringComparer.Ordinal);

    private static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => ".img",
    };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Mesaj {MessageId} ({Channel}) gönderilemedi: {Error}")]
        public static partial void DeliveryFailed(ILogger logger, Guid messageId, MessageChannel channel, string error);
    }
}

public sealed class PermanentDeliveryException(string message) : Exception(message);
