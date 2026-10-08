namespace GIBFramework.Models.Evidence;

public enum RetentionClass
{
    VukDocument,

    SpecialIntegratorLog,

    Security,
}

public static class RetentionPolicy
{
    public static DateOnly RetainUntil(RetentionClass retentionClass, int fiscalYear) => retentionClass switch
    {
        RetentionClass.VukDocument => new DateOnly(fiscalYear + 5, 12, 31),
        RetentionClass.SpecialIntegratorLog => new DateOnly(fiscalYear + 10, 12, 31),
        RetentionClass.Security => new DateOnly(fiscalYear + 2, 12, 31),
        _ => throw new ArgumentOutOfRangeException(nameof(retentionClass)),
    };
}

public sealed record EvidenceArtifact(string Name, string ContentType, long Size, string Sha256);

public sealed record EvidenceManifest
{
    public Guid TenantId { get; init; }

    public Guid DocumentId { get; init; }

    public int Sequence { get; init; }

    public string Stage { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public RetentionClass RetentionClass { get; init; }

    public DateOnly RetainUntil { get; init; }

    public IReadOnlyList<EvidenceArtifact> Artifacts { get; init; } = [];

    public string? PreviousManifestHash { get; init; }

    public string ManifestHash { get; init; } = string.Empty;

    public string ComputeHash() => Hashing.Sha256Hex(JsonDefaults.Serialize(this with { ManifestHash = string.Empty }));
}

public sealed record EvidenceVerification(Guid DocumentId, int ManifestCount, bool IsValid, IReadOnlyList<string> Problems);
