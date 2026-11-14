using GIBFramework.Models.Audit;

namespace GIBFramework.Contracts;

public interface IAuditTrail
{
    Task<AuditEvent> AppendAsync(AuditEntry entry, DbScope? scope, CancellationToken cancellationToken);

    Task<AuditEvent> AppendSystemAsync(Guid tenantId, AuditEntry entry, DbScope? scope, CancellationToken cancellationToken);

    Task<AuditEvent> AppendActorAsync(Guid tenantId, AuditEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEvent>> ListAsync(Guid tenantId, string? entityId, int take, long? beforeSequence, CancellationToken cancellationToken,
        string? userId = null, string? action = null, string? role = null, DateTimeOffset? from = null, DateTimeOffset? until = null,
        string? result = null);

    Task<AuditChainVerification> VerifyAsync(Guid tenantId, CancellationToken cancellationToken);
}
