namespace GIBFramework.Models.TaxOffices;

public sealed record TaxOfficeVersion(
    Guid Id,
    TaxOfficeRecord Data,
    string NormalizedName,
    string SearchKey,
    EffectivePeriod Period,
    Guid SourceSnapshotId)
{
    public string GibCode => Data.GibCode;
}

public sealed class TaxOfficeCatalog
{
    private readonly List<TaxOfficeVersion> _versions = [];

    public static TaxOfficeCatalog Load(IEnumerable<TaxOfficeVersion> versions)
    {
        var catalog = new TaxOfficeCatalog();
        catalog._versions.AddRange(versions);
        return catalog;
    }

    public IReadOnlyList<TaxOfficeVersion> Versions => _versions;

    public IReadOnlyList<TaxOfficeRecord> CurrentRecords(DateOnly date) => [.. ActiveOn(date).Select(v => v.Data)];

    public IEnumerable<TaxOfficeVersion> ActiveOn(DateOnly date) => _versions.Where(v => v.Period.Contains(date));

    public TaxOfficeVersion? Find(string gibCode, DateOnly date) =>
        _versions.SingleOrDefault(v => v.GibCode == gibCode && v.Period.Contains(date));

    public IEnumerable<TaxOfficeVersion> Search(string text, DateOnly date)
    {
        var key = TurkishText.ToSearchKey(text);
        return ActiveOn(date)
            .Where(v => v.SearchKey.Contains(key, StringComparison.Ordinal) || v.GibCode.StartsWith(text.Trim(), StringComparison.Ordinal))
            .OrderBy(v => v.SearchKey, StringComparer.Ordinal);
    }

    public TaxOfficeApplyResult Apply(TaxOfficeSourceSnapshot snapshot, TaxOfficeDiff diff, DateOnly effectiveFrom)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diff);

        if (snapshot.ValidationStatus != SnapshotValidationStatus.Approved)
        {
            throw new DomainException("SNAPSHOT_NOT_APPROVED", "Yalnızca onaylanmış snapshot uygulanabilir.");
        }

        var working = new List<TaxOfficeVersion>(_versions);
        var closed = new List<TaxOfficeVersion>();
        var added = new List<TaxOfficeVersion>();

        foreach (var change in diff.Changes)
        {
            var current = working.SingleOrDefault(v => v.GibCode == change.GibCode && v.Period.IsOpenEnded);

            if (change.Kind != TaxOfficeChangeKind.Added)
            {
                if (current is null)
                {
                    throw new DomainException("TAX_OFFICE_UNKNOWN", $"{change.GibCode} için açık versiyon yok; fark bu katalog durumuna ait değil.");
                }

                if (current.Period.From >= effectiveFrom)
                {
                    throw new DomainException("TAX_OFFICE_BACKDATED", $"{change.GibCode}: yeni versiyon mevcut versiyondan ({current.Period.From:yyyy-MM-dd}) sonra başlamalıdır.");
                }

                var closedVersion = current with { Period = current.Period.CloseBefore(effectiveFrom) };
                working[working.IndexOf(current)] = closedVersion;
                closed.Add(closedVersion);
            }
            else if (current is not null)
            {
                throw new DomainException("TAX_OFFICE_DUPLICATE", $"{change.GibCode} zaten aktif.");
            }

            if (change.After is { } data)
            {
                var version = new TaxOfficeVersion(
                    Guid.CreateVersion7(),
                    data,
                    TurkishText.NormalizeUpper(data.Name),
                    TurkishText.ToSearchKey(data.Name),
                    new EffectivePeriod(effectiveFrom),
                    snapshot.Id);
                working.Add(version);
                added.Add(version);
            }
        }

        _versions.Clear();
        _versions.AddRange(working);
        snapshot.MarkApplied();
        return new TaxOfficeApplyResult(closed, added);
    }
}

public sealed record TaxOfficeApplyResult(IReadOnlyList<TaxOfficeVersion> Closed, IReadOnlyList<TaxOfficeVersion> Added);
