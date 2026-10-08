using Microsoft.Data.SqlClient;

namespace GIBFramework.Contracts;

public readonly record struct DocumentSeriesKey(Guid TenantId, string DocumentKind, string Prefix, int Year);

public interface IDocumentNumberAllocator
{
    Task<DocumentNumber> AllocateAsync(DocumentSeriesKey series, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken);
}
