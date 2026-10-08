namespace GIBFramework.Models.Compliance;

public enum EDocumentType
{
    Undetermined,
    EFatura,
    EArsiv,

    PaperInvoice,
}

public enum ValidationStatus
{
    Passed,
    PassedWithWarnings,

    RequiresReview,

    Failed,
}

public sealed record ComplianceFinding(
    string Code,
    FindingSeverity Severity,
    bool Blocking,
    string Message,
    string? RuleVersion);

public sealed record ComplianceDecision
{
    public required Guid DecisionId { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }

    public required DateOnly DocumentDate { get; init; }

    public required EDocumentType DocumentType { get; init; }

    public string? RoutingRuleVersion { get; init; }

    public required IReadOnlyList<string> LegalBasis { get; init; }

    public required IReadOnlyList<string> RuleVersions { get; init; }

    public required IReadOnlyList<ComplianceFinding> Findings { get; init; }

    public required IReadOnlyList<string> Explanation { get; init; }

    public required ValidationStatus Validation { get; init; }

    public required bool UsesUnapprovedRules { get; init; }

    public required string RuleSetHash { get; init; }
}
