namespace GIBFramework.Models.Taxpayers;

public enum BookkeepingRegime
{
    Unknown = 0,

    Bilanco,

    IsletmeHesabi,

    BasitUsul,

    SerbestMeslek,

    NotATaxpayer,
}

public enum PartyKind
{
    LegalEntity,
    NaturalPerson,
}

public sealed record PartyFacts
{
    public string? TaxId { get; init; }

    public PartyKind Kind { get; init; }

    public BookkeepingRegime Regime { get; init; }

    public bool IsEFaturaRegistered { get; init; }

    public bool IsEArchiveRegistered { get; init; }

    public string? Title { get; init; }

    public string? Address { get; init; }

    public string? TaxOffice { get; init; }
}
