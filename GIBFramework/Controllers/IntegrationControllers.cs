using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using GIBFramework.Models.Integrations;
using GIBFramework.Models.Messaging;
using GIBFramework.Services.Integrations;
using GIBFramework.Services.Invoices;
using GIBFramework.Services.Messaging;

namespace GIBFramework.Controllers;

public sealed record SendInvoiceRequest(IReadOnlyList<MessageChannel> Channels, [StringLength(200)] string? Email, [StringLength(30)] string? Phone);

[ApiController]
[Route("api/v1/invoices")]
[Authorize(Policy = Policies.InvoiceRead)]
public sealed class InvoiceDeliveryController(
    InvoiceDocumentService documents,
    NotificationService notifications,
    IAuthorizationService authorization,
    ITenantContext context) : ControllerBase
{
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var invoice = await documents.LoadAsync(context.RequireTenant(), id, ct) ?? throw new NotFoundException("Fatura bulunamadı.");
        return File(await documents.RenderPdfAsync(invoice, ct), "application/pdf", InvoiceDocumentService.FileBaseName(invoice) + ".pdf");
    }

    [HttpGet("{id:guid}/share-link")]
    public async Task<IActionResult> ShareLink(Guid id, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        _ = await documents.LoadAsync(tenantId, id, ct) ?? throw new NotFoundException("Fatura bulunamadı.");
        return Ok(new { url = documents.PublicLink(tenantId, id), expiresInDays = 400 });
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendInvoiceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!(await authorization.AuthorizeAsync(User, Policies.InvoiceCreate)).Succeeded && !(await authorization.AuthorizeAsync(User, Policies.InvoiceTransmit)).Succeeded)
        {
            return Forbid();
        }

        var tenantId = context.RequireTenant();
        var invoice = await documents.LoadAsync(tenantId, id, ct) ?? throw new NotFoundException("Fatura bulunamadı.");
        if (invoice.DocumentNumber is null)
        {
            throw new DomainException("INVOICE_NOT_ISSUED", "Fatura henüz numara almadı; düzenlendikten sonra gönderilebilir.");
        }

        if (request.Channels is not { Count: > 0 })
        {
            throw new ValidationFailedException("SEND_CHANNEL", "En az bir gönderim kanalı seçin.", []);
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? invoice.Customer.Email : request.Email.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? invoice.Customer.Phone : request.Phone.Trim();
        var values = EventRouter.InvoiceValues(invoice, documents.PublicLink(tenantId, id), documents.PanelLink(id));
        values["kullanici.ad"] = invoice.Customer.Title;
        var result = await notifications.NotifyAsync(new NotificationRequest(
            tenantId,
            TemplateKeys.InvoiceIssued,
            [new NotificationRecipient(invoice.Customer.Title, email, phone)],
            values,
            context.RequireUser(),
            "Invoice",
            id.ToString(),
            [new MessageAttachmentRef("InvoicePdf", id), new MessageAttachmentRef("InvoiceHtml", id), new MessageAttachmentRef("InvoiceXml", id)],
            Channels: request.Channels,
            Force: true), ct);
        return Ok(result);
    }
}

[ApiController]
[Route("api/v1/integrations")]
[Authorize(Policy = Policies.IntegrationManage)]
public sealed class IntegrationsController(IntegrationService integrations) : ControllerBase
{
    [HttpGet("catalog")]
    public IActionResult Catalog() => Ok(new
    {
        kinds = IntegrationCatalog.All,
        events = InvoiceEvents.Catalog.Select(e => new { code = e.Code, label = e.Label }),
        signatureHeader = IntegrationService.SignatureHeader,
    });

    [HttpGet]
    public async Task<IReadOnlyList<IntegrationView>> List(CancellationToken ct) => await integrations.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<IntegrationView> Get(Guid id, CancellationToken ct) => await integrations.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveIntegrationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await integrations.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IntegrationView> Update(Guid id, [FromBody] SaveIntegrationRequest request, CancellationToken ct) => await integrations.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await integrations.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/rotate-secret")]
    public async Task<IntegrationView> Rotate(Guid id, CancellationToken ct) => await integrations.RotateSecretAsync(id, ct);

    [HttpGet("{id:guid}/setup")]
    public async Task<IntegrationSetup> Setup(Guid id, CancellationToken ct) => await integrations.SetupAsync(id, ct);

    [HttpGet("{id:guid}/deliveries")]
    public async Task<IReadOnlyList<IntegrationDelivery>> Deliveries(Guid id, CancellationToken ct) => await integrations.DeliveriesAsync(id, ct);

    [HttpPost("{id:guid}/deliveries/{deliveryId:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, Guid deliveryId, CancellationToken ct) =>
        await integrations.RetryAsync(id, deliveryId, ct) ? NoContent() : Conflict();

    [HttpPost("{id:guid}/sync")]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct) => Ok(await integrations.SyncAsync(id, ct));

    [HttpPost("{id:guid}/test")]
    public async Task<IntegrationDelivery> Test(Guid id, CancellationToken ct) => await integrations.TestAsync(id, ct);
}

[ApiController]
[Route("api/v1/hooks")]
[AllowAnonymous]
public sealed class HooksController(IntegrationService integrations) : ControllerBase
{
    public const string RateLimitPolicy = "hooks";

    [HttpPost("{id:guid}")]
    [EnableRateLimiting(RateLimitPolicy)]
    [RequestSizeLimit(1024 * 1024)]
    [RawBody]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var result = await integrations.ReceiveAsync(id, ms.ToArray(), Request.Headers, ct);
        return StatusCode(result.StatusCode, result.Body);
    }
}

[ApiController]
[Route("f")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PublicInvoiceController(InvoiceDocumentService documents) : ControllerBase
{
    public const string RateLimitPolicy = "public";

    [HttpGet("{token}")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> View(string token, CancellationToken ct)
    {
        var invoice = await ResolveAsync(token, ct);
        if (invoice is null)
        {
            return new ContentResult { Content = NotFoundPage, ContentType = "text/html; charset=utf-8", StatusCode = StatusCodes.Status404NotFound };
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return Content(await documents.RenderHtmlAsync(invoice, publicView: true, token, ct), "text/html; charset=utf-8", Encoding.UTF8);
    }

    [HttpGet("{token}/fatura.pdf")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Pdf(string token, CancellationToken ct)
    {
        var invoice = await ResolveAsync(token, ct);
        if (invoice is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = $"inline; filename=\"{InvoiceDocumentService.FileBaseName(invoice)}.pdf\"";
        return File(await documents.RenderPdfAsync(invoice, ct), "application/pdf");
    }

    [HttpGet("{token}/xml")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Xml(string token, CancellationToken ct)
    {
        var invoice = await ResolveAsync(token, ct);
        if (invoice is null || await documents.SignedXmlAsync(invoice, ct) is not { } xml)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(xml, "application/xml", InvoiceDocumentService.FileBaseName(invoice) + ".xml");
    }

    private async Task<Invoice?> ResolveAsync(string token, CancellationToken ct) =>
        token.Length is > 20 and < 1000 && documents.ReadToken(token) is { } ids ? await documents.LoadAsync(ids.TenantId, ids.InvoiceId, ct) : null;

    private const string NotFoundPage = """
        <!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Fatura bulunamadı</title>
        <style>body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#f4f7fb;font-family:'Segoe UI',Arial,sans-serif;color:#1f2933}
        .c{background:#fff;padding:36px 40px;border-radius:14px;box-shadow:0 4px 24px rgba(15,40,80,.08);max-width:420px;text-align:center}h1{font-size:20px;margin:0 0 8px}p{color:#6b7684;margin:0}</style></head>
        <body><div class="c"><h1>Fatura bulunamadı</h1><p>Bağlantı geçersiz veya süresi dolmuş. Faturayı düzenleyen firmadan yeni bağlantı isteyin.</p></div></body></html>
        """;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RawBodyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ValueProviderFactories.RemoveType<FormValueProviderFactory>();
        context.ValueProviderFactories.RemoveType<FormFileValueProviderFactory>();
        context.ValueProviderFactories.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
