using GIBFramework.Models.Audit;

namespace GIBFramework.Contracts;

public interface IAuditTrail
{
    Task<AuditEvent> AppendAsync(AuditEntry entry, DbScope? scope, CancellationToken cancellationToken);

    Task<AuditEvent> AppendSystemAsync(Guid tenantId, AuditEntry entry, DbScope? scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEvent>> ListAsync(Guid tenantId, string? entityId, int take, long? beforeSequence, CancellationToken cancellationToken);

    Task<AuditChainVerification> VerifyAsync(Guid tenantId, CancellationToken cancellationToken);
}
