using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.DAL.Invoices;
using GIBFramework.DTOs;
using GIBFramework.Mapper;
using GIBFramework.Services.Invoices;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize(Policy = Policies.InvoiceRead)]
public sealed class InvoicesController(InvoiceService service) : ControllerBase
{
    private static readonly string[] FullDataRoles =
    [
        Roles.TenantOwner, Roles.CompanyAdmin, Roles.AccountingManager, Roles.Accountant,
        Roles.InvoiceCreator, Roles.InvoiceApprover, Roles.InvoiceSigner, Roles.ApiClient,
    ];

    private bool Mask => !FullDataRoles.Any(User.IsInRole);

    [HttpPost]
    [Authorize(Policy = Policies.InvoiceCreate)]
    public async Task<ActionResult<InvoiceView>> Create(
        [FromBody] CreateInvoiceRequest request,
        [FromHeader(Name = GibFrameworkHeaders.IdempotencyKey)] string? idempotencyKey,
        CancellationToken ct)
    {
        var (invoice, created) = await service.CreateDraftAsync(request.ToDraft(), idempotencyKey, ct, new CustomerCardOptions(request.SaveCustomer, request.CustomerIsEFaturaRegistered));
        var view = invoice.ToView(Mask);
        return created ? CreatedAtAction(nameof(Get), new { id = invoice.Id }, view) : Ok(view);
    }

    [HttpGet]
    public async Task<IReadOnlyList<InvoiceSummary>> List(
        [FromQuery] DocumentStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? customerTaxId,
        [FromQuery] EDocumentType? documentType,
        [FromQuery] string? q,
        [FromQuery] int take = 50,
        [FromQuery] int skip = 0,
        CancellationToken ct = default) =>
        (await service.ListAsync(new InvoiceQuery(status, from, to, customerTaxId, take, skip, documentType, q), ct)).Items;

    [HttpGet("page")]
    public async Task<InvoicePage> Page(
        [FromQuery] DocumentStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? customerTaxId,
        [FromQuery] EDocumentType? documentType,
        [FromQuery] string? q,
        [FromQuery] int take = 50,
        [FromQuery] int skip = 0,
        CancellationToken ct = default) =>
        await service.ListAsync(new InvoiceQuery(status, from, to, customerTaxId, take, skip, documentType, q), ct);

    [HttpGet("{id:guid}")]
    public async Task<InvoiceView> Get(Guid id, CancellationToken ct) => (await service.GetAsync(id, ct)).ToView(Mask);

    [HttpGet("{id:guid}/decision")]
    public async Task<ActionResult<DecisionView>> Decision(Guid id, CancellationToken ct)
    {
        var view = (await service.GetAsync(id, ct)).ToView(Mask);
        return view.Decision is null ? NotFound() : view.Decision;
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        var (history, actions) = await service.GetHistoryAsync(id, ct);
        return Ok(new { history, actions });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Policies.InvoiceCreate)]
    public async Task<InvoiceView> Submit(Guid id, CancellationToken ct) => (await service.SubmitAsync(id, ct)).ToView(Mask);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.InvoiceApprove)]
    public async Task<InvoiceView> Approve(Guid id, CancellationToken ct) => (await service.ApproveAsync(id, ct)).ToView(Mask);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.InvoiceApprove)]
    public async Task<InvoiceView> Reject(Guid id, [FromBody] ReasonRequest request, CancellationToken ct) =>
        (await service.RejectAsync(id, request.Reason, ct)).ToView(Mask);

    [HttpPost("{id:guid}/sign")]
    [Authorize(Policy = Policies.InvoiceSign)]
    public async Task<InvoiceView> Sign(Guid id, CancellationToken ct) => (await service.SignAsync(id, ct)).ToView(Mask);

    [HttpPost("{id:guid}/transmit")]
    [Authorize(Policy = Policies.InvoiceTransmit)]
    public async Task<InvoiceView> Transmit(Guid id, CancellationToken ct) => (await service.TransmitAsync(id, ct)).ToView(Mask);

    [HttpPost("{id:guid}/refresh-status")]
    [Authorize(Policy = Policies.InvoiceTransmit)]
    public async Task<InvoiceView> RefreshStatus(Guid id, CancellationToken ct) => (await service.RefreshStatusAsync(id, ct)).ToView(Mask);

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = Policies.InvoiceTransmit)]
    public async Task<InvoiceView> Retry(Guid id, CancellationToken ct) => (await service.RetryAsync(id, ct)).ToView(Mask);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.InvoiceCancel)]
    public async Task<InvoiceView> Cancel(Guid id, [FromBody] ReasonRequest request, CancellationToken ct) =>
        (await service.CancelAsync(id, request.Reason, ct)).ToView(Mask);

    [HttpPost("{id:guid}/objection")]
    [Authorize(Policy = Policies.InvoiceCancel)]
    public async Task<InvoiceView> Objection(Guid id, [FromBody] ObjectionRequest request, CancellationToken ct) =>
        (await service.ObjectAsync(id, request.Method, request.ReferenceNumber, request.NotificationDate, request.Reason, ct)).ToView(Mask);

    [HttpGet("{id:guid}/ubl")]
    public async Task<IActionResult> Ubl(Guid id, CancellationToken ct) =>
        File(await service.GetSignedXmlAsync(id, ct), "application/xml", $"{id}.xml");

    [HttpGet("{id:guid}/qr")]
    public async Task<IActionResult> Qr(Guid id, CancellationToken ct) => File(await service.GetQrPngAsync(id, ct), "image/png");

    [HttpGet("{id:guid}/evidence")]
    [Authorize(Policy = Policies.AuditRead)]
    public async Task<IActionResult> Evidence(Guid id, CancellationToken ct) => Ok(await service.GetEvidenceAsync(id, ct));

    [HttpGet("{id:guid}/evidence/verify")]
    [Authorize(Policy = Policies.AuditRead)]
    public async Task<IActionResult> VerifyEvidence(Guid id, CancellationToken ct) => Ok(await service.VerifyEvidenceAsync(id, ct));
}
