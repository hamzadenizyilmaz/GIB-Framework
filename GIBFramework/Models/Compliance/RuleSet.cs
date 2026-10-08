using GIBFramework.Services.Compliance;

namespace GIBFramework.Models.Compliance;

public sealed class RuleSet
{
    private RuleSet(IReadOnlyList<LegalSource> sources, IReadOnlyList<LegalRule> rules, string hash)
    {
        Sources = sources;
        Rules = rules;
        Hash = hash;
    }

    public IReadOnlyList<LegalSource> Sources { get; }

    public IReadOnlyList<LegalRule> Rules { get; }

    public string Hash { get; }

    public static RuleSet Create(
        IReadOnlyList<LegalSource> sources,
        IReadOnlyList<LegalRule> rules,
        string hash,
        RuleEvaluatorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(registry);

        var errors = new List<string>();

        foreach (var dup in sources.GroupBy(s => s.Code, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            errors.Add($"Kaynak kodu birden fazla tanımlı: {dup.Key}");
        }

        foreach (var dup in rules.GroupBy(r => r.VersionTag, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            errors.Add($"Kural versiyonu birden fazla tanımlı: {dup.Key}");
        }

        foreach (var byCode in rules.Where(r => r.Review.Status != RuleReviewStatus.Retired).GroupBy(r => r.Code, StringComparer.Ordinal))
        {
            var versions = byCode.OrderBy(r => r.Period.From).ToList();
            for (var i = 0; i < versions.Count; i++)
            {
                for (var j = i + 1; j < versions.Count; j++)
                {
                    if (versions[i].Period.Overlaps(versions[j].Period))
                    {
                        errors.Add($"{versions[i].VersionTag} ({versions[i].Period}) ile {versions[j].VersionTag} ({versions[j].Period}) geçerlilik aralıkları çakışıyor.");
                    }
                }
            }
        }

        foreach (var rule in rules)
        {
            if (!registry.TryGet(rule.Kind, out var evaluator))
            {
                errors.Add($"{rule.VersionTag}: bilinmeyen kural türü '{rule.Kind}'.");
                continue;
            }

            if (rule.LegalBasis.Count == 0)
            {
                errors.Add($"{rule.VersionTag}: hukuki dayanak (legalBasis) zorunludur.");
            }

            if (rule.Review.Status == RuleReviewStatus.Approved && (rule.Review.ReviewedBy is null || rule.Review.ApprovedBy is null || rule.Review.ReviewedBy == rule.Review.ApprovedBy))
            {
                errors.Add($"{rule.VersionTag}: onaylı kural iki farklı kişi tarafından incelenmiş ve onaylanmış olmalıdır.");
            }

            errors.AddRange(evaluator.ValidateParameters(rule).Select(e => $"{rule.VersionTag}: {e}"));
        }

        if (errors.Count > 0)
        {
            throw new RuleSetValidationException(errors);
        }

        return new RuleSet(sources, [.. rules.OrderBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Version)], hash);
    }

    public IEnumerable<LegalRule> EffectiveOn(DateOnly date) =>
        Rules.Where(r => r.Review.Status != RuleReviewStatus.Retired && r.Period.Contains(date));
}

public sealed class RuleSetValidationException : Exception
{
    public RuleSetValidationException() : this([]) { }

    public RuleSetValidationException(string message) : this([message]) { }

    public RuleSetValidationException(string message, Exception innerException) : base(message, innerException) => Errors = [message];

    public RuleSetValidationException(IReadOnlyList<string> errors)
        : base("Kural seti doğrulanamadı:" + Environment.NewLine + string.Join(Environment.NewLine, errors)) => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}
