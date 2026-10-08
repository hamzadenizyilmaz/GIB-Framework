using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.DAL.LegalSources;
using GIBFramework.Services.Compliance;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/compliance")]
[Authorize]
public sealed class ComplianceController(ComplianceEngine engine) : ControllerBase
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
            reviewStatus = r.Review.Status,
            reviewNotes = r.Review.Notes,
            approvedBy = r.Review.ApprovedBy,
            legalBasis = r.LegalBasis.Select(b => new { key = b.Key, title = b.Source.Title, uri = b.Source.Uri }),
            parameters = r.Parameters.Values,
        }),
    });

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
