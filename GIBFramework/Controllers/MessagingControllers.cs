using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.DAL.Messaging;
using GIBFramework.Models.Messaging;
using GIBFramework.Services.Messaging;

namespace GIBFramework.Controllers;

public sealed record MessagingTestRequest(MessageChannel Channel, [Required, StringLength(320)] string To);

public sealed record TemplatePreviewRequest([Required, StringLength(60)] string Key, MessageChannel Channel, [StringLength(300)] string? Subject, [Required, StringLength(20000)] string Body);

[ApiController]
[Route("api/v1/messaging")]
[Authorize(Policy = Policies.MessagingManage)]
public sealed class MessagingController(
    MessagingSettingsService settings,
    NotificationService notifications,
    MessageDeliveryService delivery,
    MessageRepository messages,
    TenantLogoRepository logos,
    NetgsmClient netgsm,
    ITenantContext context,
    IClock clock) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<MessagingSettingsView> Settings(CancellationToken ct) => await settings.ViewAsync(ct);

    [HttpPut("settings/smtp")]
    public async Task<MessagingSettingsView> SaveSmtp([FromBody] SaveSmtpRequest request, CancellationToken ct) => await settings.SaveSmtpAsync(request, ct);

    [HttpPut("settings/sms")]
    public async Task<MessagingSettingsView> SaveSms([FromBody] SaveSmsRequest request, CancellationToken ct) => await settings.SaveSmsAsync(request, ct);

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] MessagingTestRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = context.RequireTenant();
        var recipient = request.Channel == MessageChannel.Email
            ? new NotificationRecipient(request.To.Trim(), request.To.Trim(), null)
            : new NotificationRecipient(request.To.Trim(), null, request.To.Trim());
        var result = await notifications.NotifyAsync(new NotificationRequest(tenantId, TemplateKeys.Test, [recipient], new Dictionary<string, string?>(),
            context.RequireUser(), "Test", null, Channels: [request.Channel], Force: true, Hold: true), ct);
        if (result.MessageIds.Count == 0)
        {
            throw new ValidationFailedException("MESSAGING_TEST", "Deneme mesajı oluşturulamadı.", result.Skipped);
        }

        var message = await messages.GetAsync(tenantId, result.MessageIds[0], ct) ?? throw new NotFoundException("Mesaj bulunamadı.");
        message.Attempts = int.MaxValue;
        await delivery.DeliverAsync(message, ct);
        var final = await messages.GetAsync(tenantId, message.Id, ct);
        return Ok(final is null ? null : MessageView.From(final));
    }

    [HttpGet("netgsm/headers")]
    public async Task<IActionResult> Headers(CancellationToken ct)
    {
        var credentials = settings.StoredSms(await settings.GetAsync(context.RequireTenant(), ct))
            ?? throw new ValidationFailedException("NETGSM_NOT_CONFIGURED", "Önce Netgsm abone numarası ve şifresini kaydedin.", []);
        return Ok(await netgsm.HeadersAsync(credentials, ct));
    }

    [HttpGet("netgsm/balance")]
    public async Task<IActionResult> Balance(CancellationToken ct)
    {
        var credentials = settings.StoredSms(await settings.GetAsync(context.RequireTenant(), ct))
            ?? throw new ValidationFailedException("NETGSM_NOT_CONFIGURED", "Önce Netgsm abone numarası ve şifresini kaydedin.", []);
        return Ok(await netgsm.BalanceAsync(credentials, ct));
    }

    [HttpGet("templates")]
    public async Task<IReadOnlyList<TemplateView>> Templates(CancellationToken ct) => await settings.TemplatesAsync(ct);

    [HttpPut("templates")]
    public async Task<IActionResult> SaveTemplate([FromBody] SaveTemplateRequest request, CancellationToken ct)
    {
        await settings.SaveTemplateAsync(request, ct);
        return NoContent();
    }

    [HttpDelete("templates/{key}/{channel}")]
    public async Task<IActionResult> ResetTemplate(string key, MessageChannel channel, CancellationToken ct)
    {
        await settings.ResetTemplateAsync(key, channel, ct);
        return NoContent();
    }

    [HttpPost("templates/preview")]
    public async Task<IActionResult> Preview([FromBody] TemplatePreviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = context.RequireTenant();
        var def = TemplateCatalog.Find(request.Key) ?? throw new NotFoundException("Şablon bulunamadı.");
        var config = await settings.GetAsync(tenantId, ct);
        var (values, brand) = await notifications.ContextAsync(tenantId, config, ct);
        foreach (var v in def.Variables.Where(v => !values.ContainsKey(v.Name) || string.IsNullOrEmpty(values[v.Name])))
        {
            values[v.Name] = v.Sample;
        }

        var unknown = TemplateRenderer.UnknownVariables((request.Subject ?? string.Empty) + " " + request.Body, def.Variables.Select(v => v.Name));
        if (request.Channel == MessageChannel.Sms)
        {
            var text = TemplateRenderer.Render(request.Body, values, html: false);
            return Ok(new { text, length = text.Length, segments = SmsSegments(text), unknown });
        }

        var html = TemplateRenderer.Layout(TemplateRenderer.Render(request.Body, values, html: true), brand);
        if (brand.HasLogo && await logos.GetAsync(tenantId, ct) is { } logo)
        {
            html = html.Replace("cid:" + TemplateRenderer.LogoContentId, $"data:{logo.ContentType};base64,{Convert.ToBase64String(logo.Data)}", StringComparison.Ordinal);
        }

        return Ok(new { subject = TemplateRenderer.Render(request.Subject ?? def.Label, values, html: false), html, text = TemplateRenderer.ToPlainText(html), unknown });
    }

    [HttpGet("messages")]
    public async Task<IActionResult> Messages(
        [FromQuery] MessageChannel? channel,
        [FromQuery] MessageStatus? status,
        [FromQuery] string? q,
        [FromQuery] int take = 200,
        CancellationToken ct = default) =>
        Ok((await messages.ListAsync(context.RequireTenant(), new MessageQuery(channel, status, q, take), ct)).Select(MessageView.From));

    [HttpGet("messages/stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) =>
        Ok(await messages.StatsAsync(context.RequireTenant(), clock.UtcNow.AddDays(-30), ct));

    [HttpGet("messages/{id:guid}")]
    public async Task<IActionResult> Message(Guid id, CancellationToken ct)
    {
        var m = await messages.GetAsync(context.RequireTenant(), id, ct) ?? throw new NotFoundException("Mesaj bulunamadı.");
        var body = m.Body;
        if (m.Channel == MessageChannel.Email && await logos.GetAsync(context.RequireTenant(), ct) is { } logo)
        {
            body = body.Replace("cid:" + TemplateRenderer.LogoContentId, $"data:{logo.ContentType};base64,{Convert.ToBase64String(logo.Data)}", StringComparison.Ordinal);
        }

        return Ok(new { message = MessageView.From(m), body, attachments = m.Extras.Attachments.Where(a => a.Kind != "Logo").Select(a => a.Kind) });
    }

    [HttpPost("messages/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) =>
        await messages.RequeueAsync(context.RequireTenant(), id, clock.UtcNow, ct) ? NoContent() : Conflict();

    [HttpPost("messages/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        await messages.CancelAsync(context.RequireTenant(), id, ct) ? NoContent() : Conflict();

    private static int SmsSegments(string text)
    {
        var turkish = text.Count(c => "çğıişÇĞİŞ".Contains(c, StringComparison.Ordinal));
        var units = text.Length + turkish;
        return units <= 155 ? 1 : (int)Math.Ceiling(units / 150.0);
    }
}

[ApiController]
[Route("api/v1/tenants")]
public sealed class TenantLogoController(TenantLogoRepository logos, ITenantContext context, IAuditTrail audit, IClock clock) : ControllerBase
{
    private const int MaxBytes = 512 * 1024;

    [HttpGet("{tenantId:guid}/logo")]
    [AllowAnonymous]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken ct)
    {
        var logo = await logos.GetAsync(tenantId, ct);
        if (logo is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = $"\"{logo.Sha256}\"";
        return File(logo.Data, logo.ContentType);
    }

    [HttpGet("current/logo-info")]
    [Authorize]
    public async Task<IActionResult> Info(CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        var info = await logos.InfoAsync(tenantId, ct);
        return Ok(new { hasLogo = info is not null, sha256 = info?.Sha256, updatedAt = info?.UpdatedAt, url = info is null ? null : $"/api/v1/tenants/{tenantId}/logo?v={info.Value.Sha256[..12]}" });
    }

    [HttpPut("current/logo")]
    [Authorize(Policy = Policies.CompanyManage)]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationFailedException("LOGO_EMPTY", "Bir görsel dosyası seçin.", []);
        }

        if (file.Length > MaxBytes)
        {
            throw new ValidationFailedException("LOGO_SIZE", "Logo en fazla 512 KB olabilir.", []);
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var data = ms.ToArray();
        var type = Sniff(data) ?? throw new ValidationFailedException("LOGO_TYPE", "Yalnızca PNG, JPEG veya WebP yükleyebilirsiniz.", []);
        var tenantId = context.RequireTenant();
        await logos.SaveAsync(tenantId, type, data, context.RequireUser(), clock.UtcNow, ct);
        await audit.AppendSystemAsync(tenantId, new Models.Audit.AuditEntry("TENANT_LOGO_UPDATED", "Tenant", tenantId.ToString(), "Success",
            new { type, bytes = data.Length, by = context.UserId }), null, ct);
        return await Info(ct);
    }

    [HttpDelete("current/logo")]
    [Authorize(Policy = Policies.CompanyManage)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        await logos.DeleteAsync(tenantId, ct);
        await audit.AppendSystemAsync(tenantId, new Models.Audit.AuditEntry("TENANT_LOGO_DELETED", "Tenant", tenantId.ToString(), "Success", new { by = context.UserId }), null, ct);
        return NoContent();
    }

    private static string? Sniff(byte[] d)
    {
        if (d.Length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47)
        {
            return "image/png";
        }

        if (d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return d.Length > 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P'
            ? "image/webp"
            : null;
    }
}
