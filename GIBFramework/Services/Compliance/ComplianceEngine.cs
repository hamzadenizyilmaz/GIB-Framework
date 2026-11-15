namespace GIBFramework.Services.Compliance;

public sealed record RuleTraceEntry(string RuleVersion, string Kind, string Outcome, IReadOnlyList<string> Notes);

public sealed class ComplianceEngine(RuleSet ruleSet, RuleEvaluatorRegistry registry, ComplianceOptions options, IClock clock, RuleReviewStore reviews)
{
    public RuleSet RuleSet => ruleSet;

    public RuleReviewStore Reviews => reviews;

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
            if (reviews.IsApproved(rule) || options.AllowUnapprovedRules)
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

        if (routingRule is null && options.AccountTypeRouting && AccountTypeRoute(context) is { } accountRoute)
        {
            documentType = accountRoute.Type;
            findings.Add(new("COMPLIANCE-ACCOUNT-ROUTING", FindingSeverity.Warning, false, accountRoute.Reason, null));
            explanation.Add(accountRoute.Reason);
            trace.Add(new("ACCOUNT-TYPE-ROUTING", "AccountTypeRouting", "Applied", [accountRoute.Reason]));
        }
        else if (routingRule is null)
        {
            findings.Add(new("COMPLIANCE-NO-ROUTING-RULE", FindingSeverity.Error, true,
                $"{Fmt.Date(context.DocumentDate)} tarihi için belge türünü belirleyen yürürlükte (ve kullanılabilir) bir kural versiyonu bulunamadı ve hesap türüne göre de belirlenemedi " +
                (context.Buyer.IsEFaturaRegistered ? "(alıcı e-Fatura kayıtlı, satıcı firma profilinde e-Fatura kullanıcısı değil)." : "(satıcı firma profilinde e-Arşiv kullanıcısı olarak işaretli değil).") +
                " Firma profilindeki e-Belge kayıtlarını kontrol edin veya kuralları onaylatın.",
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

        var unapproved = applied.Where(r => !reviews.IsApproved(r)).Select(r => r.VersionTag).ToList();

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
            RoutingRuleVersion = routingRule?.VersionTag ?? (documentType == EDocumentType.Undetermined ? null : "ACCOUNT-TYPE-ROUTING"),
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

    /// <summary>Kural uygulanamadığında tarafların e-Fatura / e-Arşiv kayıt durumuna göre belge türü.</summary>
    private static (EDocumentType Type, string Reason)? AccountTypeRoute(ComplianceContext context)
    {
        var buyer = context.Buyer;
        var seller = context.Seller;
        if (buyer.IsEFaturaRegistered && seller.IsEFaturaRegistered)
        {
            return (EDocumentType.EFatura, "Onaylı belge türü kuralı uygulanamadı; alıcı ve satıcı e-Fatura kayıtlı kullanıcı olduğundan belge hesap türüne göre e-Fatura olarak belirlendi.");
        }

        // Hesap türü VKN/TCKN'den: 11 hane TCKN → şahıs, 10 hane VKN → şirket.
        var taxId = buyer.TaxId ?? string.Empty;
        var who = taxId.Length == 11 ? "şahıs (TCKN) alıcı" : taxId.Length == 10 ? "şirket (VKN) alıcı" : "alıcı";
        var reason = buyer.IsEFaturaRegistered
            ? $"Onaylı belge türü kuralı uygulanamadı; {who} e-Fatura kayıtlı ancak satıcı e-Fatura kullanıcısı olmadığından belge e-Arşiv olarak belirlendi."
            : $"Onaylı belge türü kuralı uygulanamadı; {who} e-Fatura kayıtlı kullanıcı olmadığından belge hesap türüne göre e-Arşiv olarak belirlendi.";
        return (EDocumentType.EArsiv, reason);
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
