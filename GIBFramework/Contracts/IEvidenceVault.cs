using GIBFramework.Models.Evidence;

namespace GIBFramework.Contracts;

public sealed record EvidenceItem(string Name, string ContentType, byte[] Content);

public interface IEvidenceVault
{
    Task<EvidenceManifest> StoreAsync(
        Guid tenantId,
        Guid documentId,
        int fiscalYear,
        string stage,
        IReadOnlyList<EvidenceItem> items,
        RetentionClass retentionClass,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EvidenceManifest>> ListAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken);

    Task<byte[]?> ReadAsync(Guid tenantId, Guid documentId, int sequence, string name, CancellationToken cancellationToken);

    Task<EvidenceVerification> VerifyAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken);
}
