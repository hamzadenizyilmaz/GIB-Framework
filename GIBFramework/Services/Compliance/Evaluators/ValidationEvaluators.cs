namespace GIBFramework.Services.Compliance.Evaluators;

public sealed class Vuk230RequiredFieldsEvaluator : IRuleEvaluator
{
    public string Kind => "Vuk230RequiredFields";

    public bool IsRouting => false;

    public IEnumerable<string> ValidateParameters(LegalRule rule) => [];

    public RuleOutcome Evaluate(LegalRule rule, ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(context);

        var outcome = RuleOutcome.Apply();
        var seller = context.Seller;
        var buyer = context.Buyer;
        var buyerTaxIdRequired = rule.Parameters.GetBool("buyerTaxIdRequiredForTaxpayers", defaultValue: true);

        Require(outcome, rule, "001", seller.Title, "Düzenleyenin adı, soyadı veya ticaret unvanı eksik.");
        Require(outcome, rule, "002", seller.Address, "Düzenleyenin iş adresi eksik.");
        Require(outcome, rule, "003", seller.TaxOffice, "Düzenleyenin bağlı olduğu vergi dairesi eksik.");
        RequireTaxId(outcome, rule, "004", seller.TaxId, required: true, "Düzenleyenin vergi kimlik numarası");
        Require(outcome, rule, "005", buyer.Title, "Müşterinin adı, soyadı veya ticaret unvanı eksik.");
        Require(outcome, rule, "006", buyer.Address, "Müşterinin adresi eksik.");
        RequireTaxId(
            outcome,
            rule,
            "007",
            buyer.TaxId,
            required: buyerTaxIdRequired && buyer.Regime != BookkeepingRegime.NotATaxpayer,
            "Müşterinin vergi kimlik numarası");

        if (context.Lines.Count == 0)
        {
            outcome.Fail(rule, "008", "Malın veya işin nevi, miktarı, fiyatı ve tutarı bilgileri (fatura satırı) yok.");
        }

        for (var i = 0; i < context.Lines.Count; i++)
        {
            var line = context.Lines[i];
            if (string.IsNullOrWhiteSpace(line.Description) || line.Quantity <= 0 || line.UnitPrice < 0)
            {
                outcome.Fail(rule, "009", $"{i + 1}. satırda malın/işin nevi, miktarı veya fiyatı eksik ya da geçersiz.");
            }
        }

        if (context.DespatchRequired && string.IsNullOrWhiteSpace(context.DespatchNumber))
        {
            outcome.Fail(rule, "010", "İrsaliye düzenlenmesi gereken teslimde irsaliye numarası eksik.");
        }

        outcome.Explain(outcome.Findings.Count == 0
            ? "VUK 230 kapsamındaki zorunlu fatura bilgileri tam."
            : $"VUK 230 kapsamında {outcome.Findings.Count} zorunlu bilgi eksik veya hatalı.");
        return outcome;
    }

    private static void Require(RuleOutcome outcome, LegalRule rule, string suffix, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            outcome.Fail(rule, suffix, message);
        }
    }

    private static void RequireTaxId(RuleOutcome outcome, LegalRule rule, string suffix, string? value, bool required, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                outcome.Fail(rule, suffix, $"{label} eksik.");
            }

            return;
        }

        if (!TaxIdentifier.TryParse(value, out _, out var error))
        {
            outcome.Fail(rule, suffix, $"{label} geçersiz: {error.Message}");
        }
    }
}

public sealed class IssuancePeriodEvaluator : IRuleEvaluator
{
    public string Kind => "IssuancePeriod";

    public bool IsRouting => false;

    public IEnumerable<string> ValidateParameters(LegalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.Parameters.Has("maxDays") && rule.Parameters.GetInt("maxDays") > 0
            ? []
            : ["maxDays pozitif tam sayı olmalıdır."];
    }

    public RuleOutcome Evaluate(LegalRule rule, ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(context);

        var maxDays = rule.Parameters.GetInt("maxDays");
        var outcome = RuleOutcome.Apply();

        if (context.DeliveryDate is not { } delivery)
        {
            return outcome
                .Add(rule, "001", "Teslim/hizmet tarihi bildirilmediği için düzenleme süresi kontrol edilemedi.", FindingSeverity.Warning, blocking: false)
                .Explain("Teslim/hizmet tarihi bilinmediğinden VUK 231 süre kontrolü yapılamadı.");
        }

        var elapsed = context.DocumentDate.DayNumber - delivery.DayNumber;
        if (elapsed > maxDays)
        {
            return outcome
                .Fail(rule, "002", $"Fatura, teslim/hizmet tarihinden ({Fmt.Date(delivery)}) {elapsed} gün sonra düzenleniyor; azami süre {maxDays} gündür. Süresinde düzenlenmeyen fatura hiç düzenlenmemiş sayılabilir.")
                .Explain($"Teslim/hizmet tarihi {Fmt.Date(delivery)}; {maxDays} günlük azami düzenleme süresi aşıldı ({elapsed} gün).");
        }

        return outcome.Explain($"Teslim/hizmet tarihi {Fmt.Date(delivery)}; fatura {maxDays} günlük azami süre içinde ({Math.Max(elapsed, 0)} gün).");
    }
}

public sealed class InvoiceObligationThresholdEvaluator : IRuleEvaluator
{
    public string Kind => "InvoiceObligationThreshold";

    public bool IsRouting => false;

    public IEnumerable<string> ValidateParameters(LegalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var errors = new List<string>();
        if (rule.Parameters.TryGetDecimal("amountTry") is not > 0)
        {
            errors.Add("amountTry pozitif olmalıdır.");
        }

        if (!rule.Parameters.Has("amountBasis"))
        {
            errors.Add("amountBasis zorunludur.");
        }

        return errors;
    }

    public RuleOutcome Evaluate(LegalRule rule, ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(context);

        var limit = rule.Parameters.GetDecimal("amountTry");
        var basis = rule.Parameters.GetEnum("amountBasis", AmountBasis.VatIncluded);
        var amount = context.Amount(basis);

        return amount > limit
            ? RuleOutcome.Apply().Explain($"VUK 232: {Fmt.Basis(basis)} tutar ({Fmt.Try(amount)}) {Fmt.Try(limit)} haddini aştığından fatura düzenlenmesi mecburidir.")
            : RuleOutcome.Apply().Explain($"VUK 232: {Fmt.Basis(basis)} tutar ({Fmt.Try(amount)}) {Fmt.Try(limit)} haddini aşmıyor; fatura alıcının talebi halinde mecburidir.");
    }
}
