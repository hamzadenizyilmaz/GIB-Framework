using GIBFramework.Models.Tax;

namespace GIBFramework.DAL.Tax;

public static class TaxCatalogLoader
{
    public static TaxCatalog LoadFromDirectory(string seedRoot) =>
        LoadFromJson(File.ReadAllText(Path.Combine(seedRoot, "tax", "tax-definitions.json")));

    public static TaxCatalog LoadFromJson(string json)
    {
        var dto = JsonDefaults.Deserialize<TaxFileDto>(json);
        var errors = new List<string>();

        foreach (var w in dto.Withholdings)
        {
            if (w.Numerator <= 0 || w.Denominator <= 0 || w.Numerator > w.Denominator)
            {
                errors.Add($"Tevkifat {w.Code}: oran {w.Numerator}/{w.Denominator} geçersiz.");
            }
        }

        foreach (var dup in dto.Withholdings.GroupBy(w => w.Code).Where(g => g.Count() > 1))
        {
            var periods = dup.Select(w => new EffectivePeriod(w.EffectiveFrom, w.EffectiveTo)).ToList();
            for (var i = 0; i < periods.Count; i++)
            {
                for (var j = i + 1; j < periods.Count; j++)
                {
                    if (periods[i].Overlaps(periods[j]))
                    {
                        errors.Add($"Tevkifat {dup.Key}: geçerlilik aralıkları çakışıyor.");
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException("Vergi tanımları doğrulanamadı: " + string.Join("; ", errors));
        }

        return new TaxCatalog
        {
            VatRates = dto.VatRates,
            TaxTypes = dto.TaxTypes,
            Withholdings = dto.Withholdings,
            Exemptions = dto.Exemptions,
            ReviewStatus = dto.ReviewStatus,
            ReviewNotes = dto.ReviewNotes,
        };
    }

    private sealed record TaxFileDto(
        RuleReviewStatus ReviewStatus,
        string? ReviewNotes,
        List<VatRateDefinition> VatRates,
        List<TaxTypeDefinition> TaxTypes,
        List<WithholdingDefinition> Withholdings,
        List<ExemptionDefinition> Exemptions);
}
