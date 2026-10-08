namespace GIBFramework.Models.TaxOffices;

public enum TaxOfficeChangeKind
{
    Added,
    Closed,
    Renamed,
    Moved,
    Changed,
}

public sealed record TaxOfficeChange(TaxOfficeChangeKind Kind, string GibCode, TaxOfficeRecord? Before, TaxOfficeRecord? After);

public sealed record TaxOfficeDiff(IReadOnlyList<TaxOfficeChange> Changes)
{
    public bool IsEmpty => Changes.Count == 0;

    public int Count(TaxOfficeChangeKind kind) => Changes.Count(c => c.Kind == kind);
}

public static class TaxOfficeDiffer
{
    public static IReadOnlyList<string> Validate(IReadOnlyCollection<TaxOfficeRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var errors = new List<string>();
        var codes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.GibCode) || !r.GibCode.All(char.IsAsciiDigit))
            {
                errors.Add($"Geçersiz vergi dairesi kodu: '{r.GibCode}' ({r.Name}).");
            }

            if (!codes.Add(r.GibCode))
            {
                errors.Add($"Mükerrer vergi dairesi kodu: {r.GibCode}.");
            }

            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.ProvinceName))
            {
                errors.Add($"{r.GibCode}: ad ve il zorunludur.");
            }
        }

        foreach (var r in records.Where(r => r.ParentGibCode is not null && !codes.Contains(r.ParentGibCode)))
        {
            errors.Add($"{r.GibCode}: üst birim kodu '{r.ParentGibCode}' snapshot içinde yok.");
        }

        return errors;
    }

    public static TaxOfficeDiff Diff(IReadOnlyCollection<TaxOfficeRecord> previous, IReadOnlyCollection<TaxOfficeRecord> current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var before = previous.ToDictionary(r => r.GibCode, StringComparer.Ordinal);
        var after = current.ToDictionary(r => r.GibCode, StringComparer.Ordinal);
        var changes = new List<TaxOfficeChange>();

        foreach (var (code, now) in after)
        {
            if (!before.TryGetValue(code, out var old))
            {
                changes.Add(new(TaxOfficeChangeKind.Added, code, null, now));
            }
            else if (Normalize(old) != Normalize(now))
            {
                changes.Add(new(Classify(old, now), code, old, now));
            }
        }

        changes.AddRange(before.Keys.Except(after.Keys, StringComparer.Ordinal)
            .Select(code => new TaxOfficeChange(TaxOfficeChangeKind.Closed, code, before[code], null)));

        return new TaxOfficeDiff([.. changes.OrderBy(c => c.GibCode, StringComparer.Ordinal)]);
    }

    private static TaxOfficeChangeKind Classify(TaxOfficeRecord old, TaxOfficeRecord now)
    {
        var a = Normalize(old);
        var b = Normalize(now);
        if (a.ProvinceCode != b.ProvinceCode || a.DistrictName != b.DistrictName)
        {
            return TaxOfficeChangeKind.Moved;
        }

        return a.Name != b.Name ? TaxOfficeChangeKind.Renamed : TaxOfficeChangeKind.Changed;
    }

    private static TaxOfficeRecord Normalize(TaxOfficeRecord r) => r with
    {
        Name = TurkishText.NormalizeUpper(r.Name),
        ProvinceName = TurkishText.NormalizeUpper(r.ProvinceName),
        DistrictName = r.DistrictName is null ? null : TurkishText.NormalizeUpper(r.DistrictName),
    };
}
