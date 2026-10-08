namespace GIBFramework.Models.Audit;

public enum ActorType
{
    User,
    ApiClient,
    System,
}

public sealed record AuditEntry(
    string Action,
    string EntityType,
    string? EntityId,
    string Result = "Success",
    object? Data = null,
    string? FailureReason = null);

public sealed record AuditEvent
{
    public long Sequence { get; init; }

    public Guid EventId { get; init; }

    public Guid TenantId { get; init; }

    public string? UserId { get; init; }

    public ActorType ActorType { get; init; }

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string? EntityId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public string? Ip { get; init; }

    public string? UserAgent { get; init; }

    public string? CorrelationId { get; init; }

    public string? TraceId { get; init; }

    public string Result { get; init; } = "Success";

    public string? FailureReason { get; init; }

    public string? DataJson { get; init; }

    public string PreviousHash { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;

    public string CanonicalContent() => string.Join('|',
        PreviousHash, EventId.ToString("N"), TenantId.ToString("N"), UserId, ActorType, Action, EntityType, EntityId,
        TimestampUtc.UtcTicks, Ip, CorrelationId, Result, FailureReason, DataJson);

    public string ComputeHash() => Hashing.Sha256Hex(CanonicalContent());
}

public sealed record AuditChainVerification(Guid TenantId, long EventCount, bool IsValid, long? FirstBrokenSequence, string? LastHash);
