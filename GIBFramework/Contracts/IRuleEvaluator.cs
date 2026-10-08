namespace GIBFramework.Contracts;

public interface IRuleEvaluator
{
    string Kind { get; }

    bool IsRouting { get; }

    IEnumerable<string> ValidateParameters(LegalRule rule);

    RuleOutcome Evaluate(LegalRule rule, ComplianceContext context);
}

public sealed class RuleOutcome
{
    private readonly List<ComplianceFinding> _findings = [];
    private readonly List<string> _explanation = [];

    private RuleOutcome(bool applied) => Applied = applied;

    public bool Applied { get; }

    public EDocumentType? Routing { get; private set; }

    public IReadOnlyList<ComplianceFinding> Findings => _findings;

    public IReadOnlyList<string> Explanation => _explanation;

    public static RuleOutcome Apply() => new(applied: true);

    public static RuleOutcome NotApplicable(string reason) => new RuleOutcome(applied: false).Explain(reason);

    public RuleOutcome Explain(string sentence)
    {
        _explanation.Add(sentence);
        return this;
    }

    public RuleOutcome RouteTo(EDocumentType documentType)
    {
        Routing = documentType;
        return this;
    }

    public RuleOutcome Fail(LegalRule rule, string suffix, string message) =>
        Add(rule, suffix, message, rule.Severity, rule.Blocking);

    public RuleOutcome Add(LegalRule rule, string suffix, string message, FindingSeverity severity, bool blocking)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _findings.Add(new ComplianceFinding($"{rule.Code}-{suffix}", severity, blocking, message, rule.VersionTag));
        return this;
    }
}
