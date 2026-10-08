using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.TaxOffices;

namespace GIBFramework.DAL.TaxOffices;

public sealed record StoredSnapshot(
    TaxOfficeSourceSnapshot Snapshot,
    IReadOnlyList<TaxOfficeRecord> Records,
    TaxOfficeDiff Diff,
    DateOnly? EffectiveFrom);

public interface ITaxOfficeRepository
{
    Task InsertSnapshotAsync(StoredSnapshot stored, CancellationToken cancellationToken);

    Task<StoredSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid?> GetLatestAppliedSnapshotIdAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TaxOfficeVersion>> GetAllVersionsAsync(CancellationToken cancellationToken);

    Task PersistApprovalAsync(TaxOfficeSourceSnapshot snapshot, DateOnly effectiveFrom, TaxOfficeApplyResult result, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaxOfficeVersion>> SearchAsync(string text, DateOnly date, int take, CancellationToken cancellationToken);
}

public sealed class TaxOfficeRepository(SqlConnectionFactory connections) : ITaxOfficeRepository
{
    public async Task InsertSnapshotAsync(StoredSnapshot stored, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var s = stored.Snapshot;
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("""
            INSERT dbo.TaxOfficeSnapshots (Id, SourceType, SourceUrl, PublishedAt, DownloadedAt, Sha256, ParserVersion, RecordCount, PreviousSnapshotId,
                                           ImportedBy, ValidationStatus, ValidationErrorsJson, RecordsJson, DiffJson, ApprovedBy, ApprovedAt, EffectiveFrom)
            VALUES (@id, @st, @url, @pub, @dl, @sha, @pv, @rc, @prev, @by, @vs, @errs, @recs, @diff, NULL, NULL, NULL)
            """);
        cmd.With("@id", s.Id).With("@st", s.SourceType).With("@url", s.SourceUrl.ToString()).With("@pub", s.PublishedAt?.ToDateTime(TimeOnly.MinValue))
            .With("@dl", s.DownloadedAt).With("@sha", s.Sha256).With("@pv", s.ParserVersion).With("@rc", s.RowCount).With("@prev", s.PreviousSnapshotId)
            .With("@by", s.ImportedBy).With("@vs", s.ValidationStatus.ToString()).WithMax("@errs", JsonDefaults.Serialize(s.ValidationErrors))
            .WithMax("@recs", JsonDefaults.Serialize(stored.Records)).WithMax("@diff", JsonDefaults.Serialize(stored.Diff));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StoredSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.TaxOfficeSnapshots WHERE Id = @id");
        cmd.With("@id", id);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await r.ReadAsync(cancellationToken))
        {
            return null;
        }

        var snapshot = TaxOfficeSourceSnapshot.Restore(
            r.Get<Guid>("Id"),
            new Uri(r.Get<string>("SourceUrl")),
            r.Get<string>("SourceType"),
            r.GetNullableDateOnly("PublishedAt"),
            r.Get<DateTimeOffset>("DownloadedAt"),
            r.Get<string>("Sha256"),
            r.Get<string>("ParserVersion"),
            r.Get<int>("RecordCount"),
            r.IsDBNull(r.GetOrdinal("PreviousSnapshotId")) ? null : r.Get<Guid>("PreviousSnapshotId"),
            r.Get<string>("ImportedBy"),
            Enum.Parse<SnapshotValidationStatus>(r.Get<string>("ValidationStatus")),
            JsonDefaults.Deserialize<List<string>>(r.Get<string>("ValidationErrorsJson")),
            r.GetNullableString("ApprovedBy"),
            r.GetNullableDateTimeOffset("ApprovedAt"));

        return new StoredSnapshot(
            snapshot,
            JsonDefaults.Deserialize<List<TaxOfficeRecord>>(r.Get<string>("RecordsJson")),
            JsonDefaults.Deserialize<TaxOfficeDiff>(r.Get<string>("DiffJson")),
            r.GetNullableDateOnly("EffectiveFrom"));
    }

    public async Task<Guid?> GetLatestAppliedSnapshotIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("SELECT TOP 1 Id FROM dbo.TaxOfficeSnapshots WHERE ValidationStatus = 'Applied' ORDER BY ApprovedAt DESC");
        return (Guid?)await cmd.ExecuteScalarAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaxOfficeVersion>> GetAllVersionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.TaxOffices");
        return await ReadVersionsAsync(cmd, cancellationToken);
    }

    public async Task PersistApprovalAsync(TaxOfficeSourceSnapshot snapshot, DateOnly effectiveFrom, TaxOfficeApplyResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(result);
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var update = connection.Command("""
            UPDATE dbo.TaxOfficeSnapshots SET ValidationStatus = @vs, ApprovedBy = @by, ApprovedAt = @at, EffectiveFrom = @ef
             WHERE Id = @id AND ValidationStatus = 'Validated'
            """, tx))
        {
            update.With("@vs", snapshot.ValidationStatus.ToString()).With("@by", snapshot.ApprovedBy).With("@at", snapshot.ApprovedAt)
                .With("@ef", effectiveFrom.ToDateTime(TimeOnly.MinValue)).With("@id", snapshot.Id);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ConflictException("SNAPSHOT_STATE", "Snapshot onay için uygun durumda değil (başka bir işlem tarafından değiştirilmiş olabilir).");
            }
        }

        foreach (var c in result.Closed)
        {
            await using var close = connection.Command("UPDATE dbo.TaxOffices SET EffectiveTo = @to WHERE Id = @id AND EffectiveTo IS NULL", tx);
            await close.With("@to", c.Period.To!.Value.ToDateTime(TimeOnly.MinValue)).With("@id", c.Id).ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var v in result.Added)
        {
            await using var insert = connection.Command("""
                INSERT dbo.TaxOffices (Id, GibCode, Name, NormalizedName, SearchKey, OfficeType, ProvinceCode, ProvinceName, DistrictName,
                                       ParentGibCode, IsBranch, EffectiveFrom, EffectiveTo, SourceSnapshotId)
                VALUES (@id, @code, @n, @nn, @sk, @ot, @pc, @pn, @dn, @parent, @br, @from, NULL, @snap)
                """, tx);
            insert.With("@id", v.Id).With("@code", v.GibCode).With("@n", v.Data.Name).With("@nn", v.NormalizedName).With("@sk", v.SearchKey)
                .With("@ot", v.Data.OfficeType.ToString()).With("@pc", v.Data.ProvinceCode).With("@pn", v.Data.ProvinceName)
                .With("@dn", v.Data.DistrictName).With("@parent", v.Data.ParentGibCode).With("@br", v.Data.IsBranch)
                .With("@from", v.Period.From.ToDateTime(TimeOnly.MinValue)).With("@snap", v.SourceSnapshotId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaxOfficeVersion>> SearchAsync(string text, DateOnly date, int take, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("""
            SELECT TOP (@take) * FROM dbo.TaxOffices
             WHERE EffectiveFrom <= @d AND (EffectiveTo IS NULL OR EffectiveTo >= @d)
               AND (SearchKey LIKE @k OR GibCode LIKE @c)
             ORDER BY SearchKey
            """);
        cmd.With("@take", Math.Clamp(take, 1, 2000)).With("@d", date.ToDateTime(TimeOnly.MinValue))
            .With("@k", "%" + EscapeLike(TurkishText.ToSearchKey(text)) + "%").With("@c", EscapeLike(text.Trim()) + "%");
        return await ReadVersionsAsync(cmd, cancellationToken);
    }

    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal);

    private static async Task<List<TaxOfficeVersion>> ReadVersionsAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<TaxOfficeVersion>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            var data = new TaxOfficeRecord(
                r.Get<string>("GibCode"),
                r.Get<string>("Name"),
                Enum.Parse<TaxOfficeType>(r.Get<string>("OfficeType")),
                r.Get<string>("ProvinceCode"),
                r.Get<string>("ProvinceName"),
                r.GetNullableString("DistrictName"),
                r.GetNullableString("ParentGibCode"),
                r.Get<bool>("IsBranch"));
            list.Add(new TaxOfficeVersion(
                r.Get<Guid>("Id"),
                data,
                r.Get<string>("NormalizedName"),
                r.Get<string>("SearchKey"),
                new EffectivePeriod(r.GetNullableDateOnly("EffectiveFrom")!.Value, r.GetNullableDateOnly("EffectiveTo")),
                r.Get<Guid>("SourceSnapshotId")));
        }

        return list;
    }
}
