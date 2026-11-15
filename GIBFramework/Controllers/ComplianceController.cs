using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.DAL.LegalSources;
using GIBFramework.Models.Audit;
using GIBFramework.Services.Compliance;

namespace GIBFramework.Controllers;

public sealed record RuleReviewRequest([System.ComponentModel.DataAnnotations.StringLength(1000)] string? Note);

[ApiController]
[Route("api/v1/compliance")]
[Authorize]
public sealed class ComplianceController(ComplianceEngine engine, ITenantContext tenant, IAuditTrail audit, IClock clock) : ControllerBase
{
    [HttpPost("evaluate")]
    [Authorize(Policy = Policies.InvoiceCreate)]
    public IActionResult Evaluate([FromBody] ComplianceContext context)
    {
        var (decision, trace) = engine.EvaluateWithTrace(context);
        return Ok(new { decision, trace });
    }

    [HttpGet("rules")]
    public IActionResult Rules([FromQuery] DateOnly? date) => Ok(new
    {
        ruleSetHash = engine.RuleSet.Hash,
        asOf = date,
        rules = (date is { } d ? engine.RuleSet.EffectiveOn(d) : engine.RuleSet.Rules).Select(r => new
        {
            rule = r.VersionTag,
            r.Code,
            r.Version,
            r.Kind,
            r.Title,
            r.Description,
            effectiveFrom = r.Period.From,
            effectiveTo = r.Period.To,
            r.Severity,
            r.Blocking,
            r.Priority,
            reviewStatus = engine.Reviews.Effective(r).Status,
            reviewNotes = r.Review.Notes,
            approvedBy = engine.Reviews.Effective(r).ApprovedBy,
            review = engine.Reviews.Effective(r),
            packageStatus = r.Review.Status,
            legalBasis = r.LegalBasis.Select(b => new { key = b.Key, title = b.Source.Title, uri = b.Source.Uri }),
            parameters = r.Parameters.Values,
        }),
    });

    [HttpPost("rules/{code}/{version:int}/review")]
    [Authorize(Policy = Policies.RuleApprove)]
    public async Task<IActionResult> Review(string code, int version, [FromBody] RuleReviewRequest? request, CancellationToken ct)
    {
        var rule = Find(code, version);
        var saved = engine.Reviews.Review(rule, tenant.RequireUser(), request?.Note, clock.UtcNow);
        await audit.AppendActorAsync(Guid.Empty, new AuditEntry("RULE_REVIEWED", "LegalRule", rule.VersionTag, Data: new { saved.Fingerprint, saved.ReviewNote }), ct);
        return Ok(engine.Reviews.Effective(rule));
    }

    [HttpPost("rules/{code}/{version:int}/approve")]
    [Authorize(Policy = Policies.RuleApprove)]
    public async Task<IActionResult> Approve(string code, int version, [FromBody] RuleReviewRequest? request, CancellationToken ct)
    {
        var rule = Find(code, version);
        var saved = engine.Reviews.Approve(rule, tenant.RequireUser(), request?.Note, clock.UtcNow);
        await audit.AppendActorAsync(Guid.Empty, new AuditEntry("RULE_APPROVED", "LegalRule", rule.VersionTag,
            Data: new { saved.Fingerprint, saved.ReviewedBy, saved.ApprovedBy, saved.ApprovalNote }), ct);
        return Ok(engine.Reviews.Effective(rule));
    }

    [HttpPost("rules/{code}/{version:int}/revoke")]
    [Authorize(Policy = Policies.RuleApprove)]
    public async Task<IActionResult> Revoke(string code, int version, CancellationToken ct)
    {
        var rule = Find(code, version);
        engine.Reviews.Revoke(rule);
        await audit.AppendActorAsync(Guid.Empty, new AuditEntry("RULE_REVIEW_REVOKED", "LegalRule", rule.VersionTag), ct);
        return Ok(engine.Reviews.Effective(rule));
    }

    private Models.Compliance.LegalRule Find(string code, int version) =>
        engine.RuleSet.Rules.FirstOrDefault(r => r.Code == code && r.Version == version) ?? throw new NotFoundException("Kural bulunamadı.");

    [HttpGet("sources")]
    public IActionResult Sources() => Ok(engine.RuleSet.Sources);

    [HttpGet("tax-definitions")]
    public IActionResult TaxDefinitions([FromServices] Models.Tax.TaxCatalog catalog, [FromQuery] DateOnly? date)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.Today);
        return Ok(new
        {
            asOf = d,
            reviewStatus = catalog.ReviewStatus,
            catalog.ReviewNotes,
            vatRates = catalog.VatRates.Where(r => r.IsEffectiveOn(d)).Select(r => r.Rate).OrderByDescending(r => r),
            withholdings = catalog.Withholdings.Where(w => w.IsEffectiveOn(d)).Select(w => new { w.Code, w.Name, ratio = $"{w.Numerator}/{w.Denominator}", w.Percent }),
            exemptions = catalog.Exemptions,
            taxTypes = catalog.TaxTypes,
        });
    }

    [HttpGet("source-checks")]
    [Authorize(Policy = Policies.ComplianceManage)]
    public async Task<IActionResult> SourceChecks([FromServices] ILegalSourceCheckRepository checks, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await checks.ListAsync(take, ct));
}
