using GIBFramework.DAL.TaxOffices;
using GIBFramework.Models.Audit;
using GIBFramework.Models.TaxOffices;

namespace GIBFramework.Services.TaxOffices;

public sealed record TaxOfficeImportResult(StoredSnapshot Stored, IReadOnlyList<string> Warnings);

public sealed class TaxOfficeImportService(ITaxOfficeRepository repository, IAuditTrail audit, ITenantContext context, IClock clock)
{
    public static readonly Guid PlatformTenantId = Guid.Empty;

    public async Task<TaxOfficeImportResult> ImportAsync(byte[] content, string fileName, Uri sourceUrl, DateOnly? publishedAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        var user = context.RequireUser();
        var isPdf = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<TaxOfficeRecord> records;
        var warnings = new List<string>();
        string parserVersion;
        if (isPdf)
        {
            var parsed = TaxOfficePdfParser.Parse(new MemoryStream(content));
            records = parsed.Records;
            warnings.AddRange(parsed.Warnings);
            parserVersion = TaxOfficePdfParser.ParserVersion;
        }
        else
        {
            using var reader = new StreamReader(new MemoryStream(content));
            records = TaxOfficeCsvParser.Parse(reader);
            parserVersion = TaxOfficeCsvParser.ParserVersion;
        }

        var catalog = TaxOfficeCatalog.Load(await repository.GetAllVersionsAsync(ct));
        var diff = TaxOfficeDiffer.Diff(catalog.CurrentRecords(clock.TurkeyToday), records);
        var snapshot = TaxOfficeSourceSnapshot.Create(
            sourceUrl,
            isPdf ? "GIB_PDF" : "CSV",
            publishedAt,
            clock.UtcNow,
            Hashing.Sha256Hex(content),
            parserVersion,
            records.Count,
            await repository.GetLatestAppliedSnapshotIdAsync(ct),
            user);

        var errors = TaxOfficeDiffer.Validate([.. records]).ToList();
        if (records.Count == 0)
        {
            errors.Add("Dosyadan hiç vergi dairesi kaydı çıkarılamadı.");
        }

        snapshot.MarkValidated(errors);
        var stored = new StoredSnapshot(snapshot, records, diff, null);
        await repository.InsertSnapshotAsync(stored, ct);
        await audit.AppendSystemAsync(PlatformTenantId, new AuditEntry("TAX_OFFICE_SNAPSHOT_IMPORTED", "TaxOfficeSnapshot", snapshot.Id.ToString(),
            snapshot.ValidationStatus == SnapshotValidationStatus.Validated ? "Success" : "Failure",
            new { by = user, snapshot.Sha256, records = records.Count, added = diff.Count(TaxOfficeChangeKind.Added), closed = diff.Count(TaxOfficeChangeKind.Closed), errors = errors.Count }),
            null, ct);
        return new TaxOfficeImportResult(stored, warnings);
    }

    public async Task<StoredSnapshot> ApproveAsync(Guid snapshotId, DateOnly effectiveFrom, CancellationToken ct)
    {
        var user = context.RequireUser();
        var stored = await repository.GetSnapshotAsync(snapshotId, ct) ?? throw new NotFoundException("Snapshot bulunamadı.");
        stored.Snapshot.Approve(user, clock.UtcNow);

        var catalog = TaxOfficeCatalog.Load(await repository.GetAllVersionsAsync(ct));
        var diff = TaxOfficeDiffer.Diff(catalog.CurrentRecords(effectiveFrom.AddDays(-1)), stored.Records);
        var result = catalog.Apply(stored.Snapshot, diff, effectiveFrom);
        await repository.PersistApprovalAsync(stored.Snapshot, effectiveFrom, result, ct);
        await audit.AppendSystemAsync(PlatformTenantId, new AuditEntry("TAX_OFFICE_SNAPSHOT_APPLIED", "TaxOfficeSnapshot", snapshotId.ToString(), "Success",
            new { by = user, effectiveFrom, added = result.Added.Count, closed = result.Closed.Count }), null, ct);
        return stored with { Diff = diff, EffectiveFrom = effectiveFrom };
    }
}
