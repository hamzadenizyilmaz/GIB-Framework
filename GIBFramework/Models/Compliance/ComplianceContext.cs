namespace GIBFramework.Models.Compliance;

public sealed record ComplianceContext
{
    public required DateOnly DocumentDate { get; init; }

    public DateOnly? DeliveryDate { get; init; }

    public required PartyFacts Seller { get; init; }

    public required PartyFacts Buyer { get; init; }

    public decimal TotalExcludingVatTry { get; init; }

    public decimal TotalIncludingVatTry { get; init; }

    public string DocumentCurrency { get; init; } = "TRY";

    public bool DespatchRequired { get; init; }

    public string? DespatchNumber { get; init; }

    public IReadOnlyList<LineFacts> Lines { get; init; } = [];
}

public sealed record LineFacts(string? Description, decimal Quantity, string? Unit, decimal UnitPrice, decimal LineTotal);

public enum AmountBasis
{
    VatIncluded,
    VatExcluded,
}

public static class ComplianceContextExtensions
{
    public static decimal Amount(this ComplianceContext context, AmountBasis basis)
    {
        ArgumentNullException.ThrowIfNull(context);
        return basis == AmountBasis.VatIncluded ? context.TotalIncludingVatTry : context.TotalExcludingVatTry;
    }
}
