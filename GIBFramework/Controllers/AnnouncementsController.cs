using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.DAL.Announcements;
using GIBFramework.DAL.Tenants;
using GIBFramework.DTOs;
using GIBFramework.Models.Announcements;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/announcements")]
[Authorize(Policy = Policies.PlatformAdmin)]
public sealed class AnnouncementsController(
    IAnnouncementRepository announcements,
    ITenantRepository tenants,
    ITenantContext context,
    IClock clock,
    ILogger<AnnouncementsController> logger) : ControllerBase
{
    [HttpGet("active")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true)]
    public async Task<ActionResult<IReadOnlyList<Announcement>>> Active(CancellationToken ct)
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        var items = await announcements.ListActiveAsync(clock.UtcNow, authenticated, authenticated ? context.TenantId : null, ct);
        return Ok(items);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Announcement>>> List(CancellationToken ct) => Ok(await announcements.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Announcement>> Get(Guid id, CancellationToken ct) =>
        await announcements.GetAsync(id, ct) is { } a ? Ok(a) : NotFound();

    [HttpPost]
    public async Task<ActionResult<Announcement>> Create([FromBody] AnnouncementRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var announcement = await BuildAsync(request, Guid.NewGuid(), context.RequireUser(), now, now, ct);
        await announcements.CreateAsync(announcement, ct);
        logger.LogInformation("Duyuru oluşturuldu {AnnouncementId} ({User})", announcement.Id, announcement.CreatedBy);
        return CreatedAtAction(nameof(Get), new { id = announcement.Id }, announcement);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Announcement>> Update(Guid id, [FromBody] AnnouncementRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await announcements.GetAsync(id, ct) is not { } existing)
        {
            return NotFound();
        }

        var updated = await BuildAsync(request, id, existing.CreatedBy, existing.CreatedAt, clock.UtcNow, ct);
        return await announcements.UpdateAsync(updated, ct) ? Ok(updated) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await announcements.DeleteAsync(id, ct))
        {
            return NotFound();
        }

        logger.LogInformation("Duyuru silindi {AnnouncementId} ({User})", id, context.UserId);
        return NoContent();
    }

    private async Task<Announcement> BuildAsync(
        AnnouncementRequest r, Guid id, string createdBy, DateTimeOffset createdAt, DateTimeOffset updatedAt, CancellationToken ct)
    {
        var errors = new List<string>();
        var tenantId = r.Audience == AnnouncementAudience.Tenant ? r.TenantId : null;
        if (r.Audience == AnnouncementAudience.Tenant)
        {
            if (tenantId is null)
            {
                errors.Add("Firma duyurusu için firma seçilmelidir.");
            }
            else if (await tenants.GetAsync(tenantId.Value, ct) is null)
            {
                errors.Add("Seçilen firma bulunamadı.");
            }
        }

        var startsAt = r.StartsAt ?? updatedAt;
        if (r.EndsAt is { } endsAt && endsAt <= startsAt)
        {
            errors.Add("Bitiş zamanı başlangıçtan sonra olmalıdır.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("ANNOUNCEMENT_INVALID", "Duyuru geçersiz.", errors);
        }

        return new Announcement(id, r.Title.Trim(), r.Message.Trim(), r.Level, r.Audience, tenantId, startsAt, r.EndsAt, r.IsActive, createdBy, createdAt, updatedAt);
    }
}
