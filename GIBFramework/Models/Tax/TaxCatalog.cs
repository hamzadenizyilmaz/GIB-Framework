namespace GIBFramework.Models.Tax;

public sealed record VatRateDefinition(decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo)
{
    public bool IsEffectiveOn(DateOnly date) => new EffectivePeriod(EffectiveFrom, EffectiveTo).Contains(date);
}

public sealed record TaxTypeDefinition(string Code, string Name, string ShortName);

public sealed record WithholdingDefinition(string Code, string Name, int Numerator, int Denominator, DateOnly EffectiveFrom, DateOnly? EffectiveTo)
{
    public decimal Percent => decimal.Round(100m * Numerator / Denominator, 2);

    public bool IsEffectiveOn(DateOnly date) => new EffectivePeriod(EffectiveFrom, EffectiveTo).Contains(date);
}

public sealed record ExemptionDefinition(string Code, string Name);

public sealed class TaxCatalog
{
    public const string VatTaxTypeCode = "0015";
    public const string VatWithholdingTaxTypeCode = "9015";

    public required IReadOnlyList<VatRateDefinition> VatRates { get; init; }

    public required IReadOnlyList<TaxTypeDefinition> TaxTypes { get; init; }

    public required IReadOnlyList<WithholdingDefinition> Withholdings { get; init; }

    public required IReadOnlyList<ExemptionDefinition> Exemptions { get; init; }

    public required RuleReviewStatus ReviewStatus { get; init; }

    public string? ReviewNotes { get; init; }

    public bool IsVatRateAllowed(decimal rate, DateOnly date) => VatRates.Any(r => r.Rate == rate && r.IsEffectiveOn(date));

    public WithholdingDefinition? FindWithholding(string code, DateOnly date) =>
        Withholdings.FirstOrDefault(w => w.Code == code && w.IsEffectiveOn(date));

    public ExemptionDefinition? FindExemption(string code) => Exemptions.FirstOrDefault(e => e.Code == code);

    public string TaxName(string code) => TaxTypes.FirstOrDefault(t => t.Code == code)?.Name ?? code;
}
