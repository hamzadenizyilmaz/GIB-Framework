using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;

namespace GIBFramework.DAL.Sequences;

public sealed class SqlDocumentNumberAllocator : IDocumentNumberAllocator
{
    public async Task<DocumentNumber> AllocateAsync(DocumentSeriesKey series, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var cmd = connection.Command("""
            IF NOT EXISTS (SELECT 1 FROM dbo.DocumentSequences WITH (UPDLOCK, HOLDLOCK)
                           WHERE TenantId = @t AND DocumentKind = @k AND Prefix = @p AND FiscalYear = @y)
                INSERT dbo.DocumentSequences (TenantId, DocumentKind, Prefix, FiscalYear, LastValue, UpdatedAt)
                VALUES (@t, @k, @p, @y, 0, SYSDATETIMEOFFSET());

            DECLARE @next TABLE (Value bigint);
            UPDATE dbo.DocumentSequences
               SET LastValue = LastValue + 1, UpdatedAt = SYSDATETIMEOFFSET()
            OUTPUT inserted.LastValue INTO @next
             WHERE TenantId = @t AND DocumentKind = @k AND Prefix = @p AND FiscalYear = @y;
            SELECT Value FROM @next;
            """, transaction);
        cmd.With("@t", series.TenantId).With("@k", series.DocumentKind).With("@p", series.Prefix).With("@y", series.Year);

        var next = (long)(await cmd.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Numara tahsis edilemedi."));
        return DocumentNumber.Create(series.Prefix, series.Year, next);
    }
}
