namespace GIBFramework.Base;

public readonly record struct EffectivePeriod
{
    public EffectivePeriod(DateOnly from, DateOnly? to = null)
    {
        if (to is { } end && end < from)
        {
            throw new DomainException("EFFECTIVE_PERIOD_INVALID", $"Bitiş tarihi ({end:yyyy-MM-dd}) başlangıçtan ({from:yyyy-MM-dd}) önce olamaz.");
        }

        From = from;
        To = to;
    }

    public DateOnly From { get; }

    public DateOnly? To { get; }

    public bool IsOpenEnded => To is null;

    public bool Contains(DateOnly date) => date >= From && (To is null || date <= To.Value);

    public bool Overlaps(EffectivePeriod other) =>
        (To is null || other.From <= To.Value) && (other.To is null || From <= other.To.Value);

    public EffectivePeriod CloseBefore(DateOnly date) => new(From, date.AddDays(-1));

    public override string ToString() => $"{From:yyyy-MM-dd} - {(To is null ? "…" : To.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))}";
}
