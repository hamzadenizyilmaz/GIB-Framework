namespace GIBFramework.Models.Documents;

public readonly record struct Ettn
{
    private Ettn(Guid value) => Value = value;

    public Guid Value { get; }

    public bool IsVersion4 => Value.Version == 4;

    public static Ettn New() => new(Guid.NewGuid());

    public static Ettn Parse(string value) =>
        Guid.TryParseExact(value, "D", out var guid) && guid != Guid.Empty
            ? new Ettn(guid)
            : throw new DomainException("ETTN_FORMAT", $"Geçersiz ETTN: '{value}'.");

    public override string ToString() => Value.ToString("D").ToUpperInvariant();
}
