using System.Globalization;
using GIBFramework.DAL.Identity;
using GIBFramework.DAL.Messaging;
using GIBFramework.Models.Identity;
using GIBFramework.Models.Messaging;
using GIBFramework.Services.Integrations;
using GIBFramework.Services.Invoices;

namespace GIBFramework.Services.Messaging;

public sealed partial class EventRouter(
    OutboxEventRepository events,
    InvoiceDocumentService documents,
    NotificationService notifications,
    IntegrationService integrations,
    IUserRepository users,
    IClock clock,
    ILogger<EventRouter> logger)
{
    private const int MaxAttempts = 5;
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<int> ProcessPendingAsync(CancellationToken ct)
    {
        var batch = await events.PendingAsync(50, ct);
        foreach (var evt in batch)
        {
            try
            {
                await HandleAsync(evt, ct);
                await events.CompleteAsync(evt.Id, null, clock.UtcNow, finished: true, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.EventFailed(logger, evt.Id, evt.EventType, ex);
                await events.CompleteAsync(evt.Id, ex.Message, clock.UtcNow, finished: evt.Attempts + 1 >= MaxAttempts, CancellationToken.None);
            }
        }

        return batch.Count;
    }

    public static Dictionary<string, string?> InvoiceValues(Invoice invoice, string publicLink, string panelLink) => new(StringComparer.Ordinal)
    {
        ["fatura.no"] = InvoiceDocumentService.FileBaseName(invoice),
        ["fatura.tur"] = InvoiceDocumentService.DocumentTypeLabel(invoice),
        ["fatura.tarih"] = invoice.IssueDate.ToString("dd.MM.yyyy", Tr),
        ["fatura.tutar"] = InvoiceDocumentService.Money(invoice.Totals.PayableAmount, invoice.Currency),
        ["fatura.ettn"] = invoice.Uuid.ToString(),
        ["fatura.link"] = publicLink,
        ["fatura.panel_link"] = panelLink,
        ["musteri.unvan"] = invoice.Customer.Title,
        ["musteri.vkn"] = invoice.Customer.TaxId,
        ["olusturan"] = invoice.CreatedBy,
    };

    private async Task HandleAsync(OutboxEvent evt, CancellationToken ct)
    {
        if (evt.EntityType != "Invoice" || evt.TenantId is not { } tenantId || !Guid.TryParse(evt.EntityId, out var invoiceId))
        {
            return;
        }

        var invoice = await documents.LoadAsync(tenantId, invoiceId, ct);
        if (invoice is null)
        {
            return;
        }

        var payload = JsonDefaults.Deserialize<InvoiceEventPayload>(evt.PayloadJson);
        var publicLink = documents.PublicLink(tenantId, invoice.Id);
        var panelLink = documents.PanelLink(invoice.Id);
        var values = InvoiceValues(invoice, publicLink, panelLink);
        values["neden"] = payload.Note ?? invoice.LastError;
        values["islem_yapan"] = payload.Actor;

        switch (evt.EventType)
        {
            case InvoiceEvents.AwaitingApproval:
                var approvers = await UsersWithPolicyAsync(tenantId, Policies.InvoiceApprove, ct);
                var others = approvers.Where(u => !string.Equals(u.UserCode, invoice.CreatedBy, StringComparison.OrdinalIgnoreCase)).ToList();
                await NotifyUsersAsync(tenantId, TemplateKeys.InvoiceAwaitingApproval, others.Count > 0 ? others : approvers, values, invoice, ct);
                break;
            case InvoiceEvents.Rejected:
                await NotifyUsersAsync(tenantId, TemplateKeys.InvoiceRejected, await CreatorAsync(invoice, ct), values, invoice, ct);
                break;
            case InvoiceEvents.Failed:
                values["neden"] = invoice.LastError ?? payload.Note;
                await NotifyUsersAsync(tenantId, TemplateKeys.InvoiceFailed, await CreatorAsync(invoice, ct), values, invoice, ct);
                break;
            case InvoiceEvents.Issued:
                await NotifyCustomerAsync(tenantId, TemplateKeys.InvoiceIssued, invoice, values,
                    [new MessageAttachmentRef("InvoicePdf", invoice.Id), new MessageAttachmentRef("InvoiceHtml", invoice.Id), new MessageAttachmentRef("InvoiceXml", invoice.Id)], ct);
                break;
            case InvoiceEvents.Cancelled:
                await NotifyCustomerAsync(tenantId, TemplateKeys.InvoiceCancelled, invoice, values, [], ct);
                break;
        }

        await integrations.RouteAsync(new OutboxEventContext(evt.EventType, invoice, payload.Note, publicLink, panelLink), ct);
    }

    private async Task NotifyCustomerAsync(Guid tenantId, string template, Invoice invoice, Dictionary<string, string?> values, IReadOnlyList<MessageAttachmentRef> attachments, CancellationToken ct)
    {
        var c = invoice.Customer;
        if (string.IsNullOrWhiteSpace(c.Email) && string.IsNullOrWhiteSpace(c.Phone))
        {
            return;
        }

        values["kullanici.ad"] = c.Title;
        await notifications.NotifyAsync(new NotificationRequest(tenantId, template, [new NotificationRecipient(c.Title, c.Email, c.Phone)], values, "system",
            "Invoice", invoice.Id.ToString(), attachments), ct);
    }

    private async Task NotifyUsersAsync(Guid tenantId, string template, IReadOnlyList<UserAccount> recipients, Dictionary<string, string?> values, Invoice invoice, CancellationToken ct)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        values.Remove("kullanici.ad");
        foreach (var user in recipients)
        {
            var scoped = new Dictionary<string, string?>(values, StringComparer.Ordinal)
            {
                ["kullanici.ad"] = user.DisplayName,
                ["kullanici.kod"] = user.UserCode,
            };
            await notifications.NotifyAsync(new NotificationRequest(tenantId, template, [new NotificationRecipient(user.DisplayName, user.Email, user.Phone)], scoped, "system",
                "Invoice", invoice.Id.ToString()), ct);
        }
    }

    private async Task<IReadOnlyList<UserAccount>> CreatorAsync(Invoice invoice, CancellationToken ct) =>
        await users.FindByCodeAsync(invoice.CreatedBy, ct) is { IsActive: true } u && u.TenantId == invoice.TenantId ? [u] : [];

    private async Task<IReadOnlyList<UserAccount>> UsersWithPolicyAsync(Guid tenantId, string policy, CancellationToken ct)
    {
        var roles = Policies.Map[policy];
        return [.. (await users.ListAsync(tenantId, ct)).Where(u => u.IsActive && u.Roles.Any(r => roles.Contains(r)))];
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Olay {EventId} ({EventType}) işlenemedi.")]
        public static partial void EventFailed(ILogger logger, long eventId, string eventType, Exception exception);
    }
}
