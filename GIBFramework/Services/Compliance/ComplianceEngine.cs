namespace GIBFramework.Services.Compliance;

public sealed record RuleTraceEntry(string RuleVersion, string Kind, string Outcome, IReadOnlyList<string> Notes);

public sealed class ComplianceEngine(RuleSet ruleSet, RuleEvaluatorRegistry registry, ComplianceOptions options, IClock clock)
{
    public RuleSet RuleSet => ruleSet;

    public ComplianceDecision Evaluate(ComplianceContext context) => EvaluateWithTrace(context).Decision;

    public (ComplianceDecision Decision, IReadOnlyList<RuleTraceEntry> Trace) EvaluateWithTrace(ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var findings = new List<ComplianceFinding>();
        var explanation = new List<string> { $"Belge tarihi: {Fmt.Date(context.DocumentDate)}. Bu tarihte yürürlükte olan kural versiyonları uygulandı." };
        var trace = new List<RuleTraceEntry>();
        var evaluated = new List<LegalRule>();
        var applied = new List<LegalRule>();

        var active = new List<LegalRule>();
        foreach (var rule in ruleSet.EffectiveOn(context.DocumentDate))
        {
            if (rule.IsApproved || options.AllowUnapprovedRules)
            {
                active.Add(rule);
            }
            else
            {
                trace.Add(new(rule.VersionTag, rule.Kind, "Excluded", [$"Kural onaylı değil ({rule.Review.Status}); üretim politikası gereği değerlendirilmedi."]));
            }
        }

        var documentType = EDocumentType.Undetermined;
        LegalRule? routingRule = null;
        var routingRules = active
            .Where(r => registry.Get(r.Kind).IsRouting)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Code, StringComparer.Ordinal);

        foreach (var rule in routingRules)
        {
            var outcome = registry.Get(rule.Kind).Evaluate(rule, context);
            evaluated.Add(rule);
            trace.Add(new(rule.VersionTag, rule.Kind, outcome.Applied ? "Applied" : "NotApplicable", outcome.Explanation));
            findings.AddRange(outcome.Findings);

            if (outcome.Applied && outcome.Routing is { } routing)
            {
                documentType = routing;
                routingRule = rule;
                applied.Add(rule);
                explanation.AddRange(outcome.Explanation);
                break;
            }
        }

        if (routingRule is null)
        {
            findings.Add(new("COMPLIANCE-NO-ROUTING-RULE", FindingSeverity.Error, true,
                $"{Fmt.Date(context.DocumentDate)} tarihi için belge türünü belirleyen yürürlükte (ve kullanılabilir) bir kural versiyonu bulunamadı. Başka bir dönemin kuralı uygulanmaz; manuel inceleme gerekir.",
                null));
            explanation.Add("Bu tarih için belge türünü belirleyen kural versiyonu bulunamadığından belge türü belirlenemedi.");
        }

        foreach (var rule in active.Where(r => !registry.Get(r.Kind).IsRouting).OrderBy(r => r.Code, StringComparer.Ordinal))
        {
            var outcome = registry.Get(rule.Kind).Evaluate(rule, context);
            evaluated.Add(rule);
            trace.Add(new(rule.VersionTag, rule.Kind, outcome.Applied ? "Applied" : "NotApplicable", outcome.Explanation));
            findings.AddRange(outcome.Findings);
            if (outcome.Applied)
            {
                applied.Add(rule);
                explanation.AddRange(outcome.Explanation);
            }
        }

        var unapproved = applied.Where(r => !r.IsApproved).Select(r => r.VersionTag).ToList();

        explanation.Add(documentType switch
        {
            EDocumentType.EFatura => "Sonuç: Belge e-Fatura olarak düzenlenmelidir.",
            EDocumentType.EArsiv => "Sonuç: Belge e-Arşiv Fatura olarak düzenlenmelidir.",
            EDocumentType.PaperInvoice => "Sonuç: Elektronik belge zorunluluğu yok; kâğıt fatura düzenlenebilir.",
            _ => "Sonuç: Belge türü belirlenemedi; manuel inceleme gerekir.",
        });

        var decision = new ComplianceDecision
        {
            DecisionId = Guid.CreateVersion7(clock.UtcNow),
            DecidedAt = clock.UtcNow,
            DocumentDate = context.DocumentDate,
            DocumentType = documentType,
            RoutingRuleVersion = routingRule?.VersionTag,
            LegalBasis = [.. applied.SelectMany(r => r.LegalBasis).Select(b => b.Key).Distinct(StringComparer.Ordinal)],
            RuleVersions = [.. evaluated.Select(r => r.VersionTag)],
            Findings = findings,
            Explanation = explanation,
            Validation = Classify(findings),
            UsesUnapprovedRules = unapproved.Count > 0,
            RuleSetHash = ruleSet.Hash,
        };

        return (decision, trace);
    }

    private static ValidationStatus Classify(IReadOnlyList<ComplianceFinding> findings)
    {
        if (findings.Any(f => f.Blocking))
        {
            return ValidationStatus.Failed;
        }

        if (findings.Any(f => f.Severity == FindingSeverity.Error))
        {
            return ValidationStatus.RequiresReview;
        }

        return findings.Any(f => f.Severity == FindingSeverity.Warning) ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
    }
}
