namespace GIBFramework.Models.Compliance;

public enum FindingSeverity
{
    Info,
    Warning,
    Error,
}

public enum RuleReviewStatus
{
    Draft,

    PendingExpertReview,

    Approved,

    Retired,
}

public enum LegalSourceKind
{
    Law,
    Communique,
    TechnicalGuide,
    Standard,
    MasterData,
}

public sealed record LegalSource(
    string Code,
    string Title,
    LegalSourceKind Kind,
    Uri? Uri,
    string? Sha256,
    string? Notes);

public sealed record LegalBasisReference(LegalSource Source, string? Article)
{
    public string Key => Article is null ? Source.Code : $"{Source.Code}:{Article}";
}

public sealed record RuleReview(
    RuleReviewStatus Status,
    string? ReviewedBy,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? Notes);

public sealed record LegalRule
{
    public required string Code { get; init; }

    public required int Version { get; init; }

    public required string Kind { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required EffectivePeriod Period { get; init; }

    public required FindingSeverity Severity { get; init; }

    public required bool Blocking { get; init; }

    public int Priority { get; init; }

    public required IReadOnlyList<LegalBasisReference> LegalBasis { get; init; }

    public required RuleParameters Parameters { get; init; }

    public required RuleReview Review { get; init; }

    public string VersionTag => $"{Code}:v{Version}";

    public bool IsApproved => Review.Status == RuleReviewStatus.Approved;
}
