namespace GIBFramework.Models.TaxOffices;

public enum SnapshotValidationStatus
{
    Pending,
    Validated,
    Rejected,
    Approved,
    Applied,
}

public sealed class TaxOfficeSourceSnapshot : AggregateRoot<Guid>
{
    private TaxOfficeSourceSnapshot(Guid id) : base(id) { }

    public required string SourceType { get; init; }

    public required Uri SourceUrl { get; init; }

    public DateOnly? PublishedAt { get; init; }

    public required DateTimeOffset DownloadedAt { get; init; }

    public required string Sha256 { get; init; }

    public required string ParserVersion { get; init; }

    public required int RowCount { get; init; }

    public Guid? PreviousSnapshotId { get; init; }

    public required string ImportedBy { get; init; }

    public SnapshotValidationStatus ValidationStatus { get; private set; } = SnapshotValidationStatus.Pending;

    public IReadOnlyList<string> ValidationErrors { get; private set; } = [];

    public string? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public static TaxOfficeSourceSnapshot Create(
        Uri sourceUrl,
        string sourceType,
        DateOnly? publishedAt,
        DateTimeOffset downloadedAt,
        string sha256,
        string parserVersion,
        int rowCount,
        Guid? previousSnapshotId,
        string importedBy) => new(Guid.CreateVersion7(downloadedAt))
        {
            SourceUrl = sourceUrl,
            SourceType = sourceType,
            PublishedAt = publishedAt,
            DownloadedAt = downloadedAt,
            Sha256 = sha256,
            ParserVersion = parserVersion,
            RowCount = rowCount,
            PreviousSnapshotId = previousSnapshotId,
            ImportedBy = importedBy,
        };

    public static TaxOfficeSourceSnapshot Restore(
        Guid id,
        Uri sourceUrl,
        string sourceType,
        DateOnly? publishedAt,
        DateTimeOffset downloadedAt,
        string sha256,
        string parserVersion,
        int rowCount,
        Guid? previousSnapshotId,
        string importedBy,
        SnapshotValidationStatus status,
        IReadOnlyList<string> errors,
        string? approvedBy,
        DateTimeOffset? approvedAt) => new(id)
        {
            SourceUrl = sourceUrl,
            SourceType = sourceType,
            PublishedAt = publishedAt,
            DownloadedAt = downloadedAt,
            Sha256 = sha256,
            ParserVersion = parserVersion,
            RowCount = rowCount,
            PreviousSnapshotId = previousSnapshotId,
            ImportedBy = importedBy,
            ValidationStatus = status,
            ValidationErrors = errors,
            ApprovedBy = approvedBy,
            ApprovedAt = approvedAt,
        };

    public void MarkValidated(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        EnsureStatus(SnapshotValidationStatus.Pending);
        ValidationErrors = errors;
        ValidationStatus = errors.Count == 0 ? SnapshotValidationStatus.Validated : SnapshotValidationStatus.Rejected;
    }

    public void Approve(string approvedBy, DateTimeOffset at)
    {
        EnsureStatus(SnapshotValidationStatus.Validated);
        if (string.Equals(approvedBy, ImportedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("SNAPSHOT_FOUR_EYES", "Snapshot'ı içe aktaran kullanıcı onaylayamaz.");
        }

        ApprovedBy = approvedBy;
        ApprovedAt = at;
        ValidationStatus = SnapshotValidationStatus.Approved;
    }

    internal void MarkApplied()
    {
        EnsureStatus(SnapshotValidationStatus.Approved);
        ValidationStatus = SnapshotValidationStatus.Applied;
    }

    private void EnsureStatus(SnapshotValidationStatus expected)
    {
        if (ValidationStatus != expected)
        {
            throw new DomainException("SNAPSHOT_STATUS", $"Snapshot durumu {ValidationStatus}; beklenen {expected}.");
        }
    }
}
