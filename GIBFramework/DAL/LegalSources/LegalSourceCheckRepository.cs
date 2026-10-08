using GIBFramework.Base.Extensions;

namespace GIBFramework.DAL.LegalSources;

public sealed record LegalSourceCheck(
    long Id,
    string SourceCode,
    string Url,
    DateTimeOffset CheckedAt,
    int? HttpStatus,
    string? Sha256,
    string? PreviousSha256,
    bool Changed,
    string? Error);

public interface ILegalSourceCheckRepository
{
    Task<string?> GetLastHashAsync(string sourceCode, CancellationToken cancellationToken);

    Task InsertAsync(LegalSourceCheck check, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegalSourceCheck>> ListAsync(int take, CancellationToken cancellationToken);
}

public sealed class LegalSourceCheckRepository(SqlConnectionFactory connections) : ILegalSourceCheckRepository
{
    public async Task<string?> GetLastHashAsync(string sourceCode, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("SELECT TOP 1 Sha256 FROM dbo.LegalSourceChecks WHERE SourceCode = @c AND Sha256 IS NOT NULL ORDER BY Id DESC");
        return (string?)await cmd.With("@c", sourceCode).ExecuteScalarAsync(cancellationToken);
    }

    public async Task InsertAsync(LegalSourceCheck check, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("""
            INSERT dbo.LegalSourceChecks (SourceCode, Url, CheckedAt, HttpStatus, Sha256, PreviousSha256, Changed, Error)
            VALUES (@c, @u, @at, @hs, @sha, @prev, @ch, @err)
            """);
        await cmd.With("@c", check.SourceCode).With("@u", check.Url).With("@at", check.CheckedAt).With("@hs", check.HttpStatus)
            .With("@sha", check.Sha256).With("@prev", check.PreviousSha256).With("@ch", check.Changed).With("@err", check.Error)
            .ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LegalSourceCheck>> ListAsync(int take, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("SELECT TOP (@take) * FROM dbo.LegalSourceChecks ORDER BY Id DESC");
        cmd.With("@take", Math.Clamp(take, 1, 500));
        var list = new List<LegalSourceCheck>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new LegalSourceCheck(
                r.Get<long>("Id"),
                r.Get<string>("SourceCode"),
                r.Get<string>("Url"),
                r.Get<DateTimeOffset>("CheckedAt"),
                r.IsDBNull(r.GetOrdinal("HttpStatus")) ? null : r.Get<int>("HttpStatus"),
                r.GetNullableString("Sha256"),
                r.GetNullableString("PreviousSha256"),
                r.Get<bool>("Changed"),
                r.GetNullableString("Error")));
        }

        return list;
    }
}
