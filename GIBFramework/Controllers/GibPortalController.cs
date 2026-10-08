using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GIBFramework.DTOs;
using GIBFramework.Mapper;
using GIBFramework.Services.GibPortal;

namespace GIBFramework.Controllers;

public sealed record GibPortalConnectRequest(
    GibPortalEnvironment Environment,
    [Required, StringLength(50)] string UserCode,
    [Required, StringLength(100)] string Password);

public sealed record GibPortalSignRequest([StringLength(6)] string? SmsCode);

[ApiController]
[Route("api/v1/gib-portal")]
[Authorize(Policy = Policies.GibPortal)]
public sealed class GibPortalController(GibPortalService portal) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(portal.Status());

    [HttpPost("connect")]
    [EnableRateLimiting(AuthController.LoginRateLimitPolicy)]
    public async Task<IActionResult> Connect([FromBody] GibPortalConnectRequest request, CancellationToken ct) =>
        Ok(await portal.ConnectAsync(request.Environment, request.UserCode, request.Password, ct));

    [HttpPost("test-user")]
    [EnableRateLimiting(AuthController.LoginRateLimitPolicy)]
    public async Task<IActionResult> TestUser(CancellationToken ct) => Ok(await portal.SuggestTestUserAsync(ct));

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        await portal.DisconnectAsync(ct);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(await portal.GetUserInfoAsync(ct));

    [HttpGet("documents")]
    public async Task<IActionResult> Documents([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok((await portal.ListDocumentsAsync(from, to, ct)).Select(r => new
        {
            r.Ettn,
            r.DocumentNumber,
            r.RecipientTaxId,
            r.RecipientTitle,
            r.DocumentDate,
            r.ApprovalStatus,
            r.IsApproved,
            raw = r.Raw,
        }));

    [HttpGet("documents/issued-to-me")]
    public async Task<IActionResult> IssuedToMe([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(await portal.ListIssuedToMeAsync(from, to, ct));

    [HttpGet("documents/{ettn}/html")]
    public async Task<IActionResult> DocumentHtml(string ettn, [FromQuery] bool approved, CancellationToken ct) =>
        Content(await portal.GetDocumentHtmlAsync(ettn, approved, ct), "text/html; charset=utf-8");

    [HttpGet("recipients/{taxId}")]
    public async Task<IActionResult> Recipient(string taxId, CancellationToken ct)
    {
        if (!TaxIdentifier.TryParse(taxId, out _, out var error))
        {
            throw new ValidationFailedException(error.Code, error.Message, []);
        }

        return Ok(await portal.LookupRecipientAsync(taxId, ct));
    }

    [HttpPost("invoices/{id:guid}/draft")]
    public async Task<IActionResult> Draft(Guid id, CancellationToken ct) => Ok(await portal.CreateDraftAsync(id, ct));

    [HttpGet("invoices/{id:guid}/html")]
    public async Task<IActionResult> Html(Guid id, CancellationToken ct) =>
        Content(await portal.GetHtmlAsync(id, ct), "text/html; charset=utf-8");

    [HttpPost("invoices/{id:guid}/delete-draft")]
    public async Task<InvoiceView> DeleteDraft(Guid id, [FromBody] ReasonRequest request, CancellationToken ct) =>
        (await portal.DeleteDraftAsync(id, request.Reason, ct)).ToView(maskPersonalData: false);

    [HttpPost("invoices/{id:guid}/sms")]
    [Authorize(Policy = Policies.InvoiceSign)]
    public async Task<IActionResult> Sms(Guid id, CancellationToken ct) => Ok(await portal.SendSmsAsync(id, ct));

    [HttpPost("invoices/{id:guid}/sign")]
    [Authorize(Policy = Policies.InvoiceSign)]
    public async Task<InvoiceView> Sign(Guid id, [FromBody] GibPortalSignRequest request, CancellationToken ct) =>
        (await portal.SignAsync(id, request.SmsCode, ct)).ToView(maskPersonalData: false);

    [HttpPost("invoices/{id:guid}/cancel")]
    [Authorize(Policy = Policies.InvoiceCancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ReasonRequest request, CancellationToken ct) =>
        Ok(new { message = await portal.RequestCancellationAsync(id, request.Reason, ct) });
}
