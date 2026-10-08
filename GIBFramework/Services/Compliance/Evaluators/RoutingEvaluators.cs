namespace GIBFramework.Services.Compliance.Evaluators;

public sealed class EFaturaRoutingEvaluator : IRuleEvaluator
{
    public string Kind => "EFaturaRouting";

    public bool IsRouting => true;

    public IEnumerable<string> ValidateParameters(LegalRule rule) => [];

    public RuleOutcome Evaluate(LegalRule rule, ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Buyer.IsEFaturaRegistered)
        {
            return RuleOutcome.NotApplicable("Alıcı e-Fatura uygulamasına kayıtlı kullanıcı değil.");
        }

        if (!context.Seller.IsEFaturaRegistered)
        {
            return RuleOutcome.Apply()
                .RouteTo(EDocumentType.Undetermined)
                .Explain("Alıcı e-Fatura uygulamasına kayıtlı, ancak satıcı kayıtlı değil.")
                .Fail(rule, "001", "Alıcı e-Fatura kayıtlı kullanıcısı, satıcı değil. Belge türü otomatik belirlenemez; satıcının kayıt durumu ve uygulanacak belge türü manuel incelenmelidir.");
        }

        return RuleOutcome.Apply()
            .RouteTo(EDocumentType.EFatura)
            .Explain("Alıcı ve satıcı e-Fatura uygulamasına kayıtlı kullanıcılardır.")
            .Explain("Kayıtlı kullanıcılar arasında düzenlenen faturalar e-Fatura olarak düzenlenir.");
    }
}

public sealed class EArchiveRoutingEvaluator : IRuleEvaluator
{
    public string Kind => "EArchiveRouting";

    public bool IsRouting => true;

    public IEnumerable<string> ValidateParameters(LegalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var p = rule.Parameters;
        var errors = new List<string>();

        try
        {
            _ = p.GetEnumList<BookkeepingRegime>("sellerRegimes");
        }
        catch (ArgumentException ex)
        {
            errors.Add($"sellerRegimes geçersiz: {ex.Message}");
        }

        if (p.Has("amountThresholdTry"))
        {
            if (p.TryGetDecimal("amountThresholdTry") is not > 0)
            {
                errors.Add("amountThresholdTry pozitif olmalıdır.");
            }

            if (!p.Has("amountBasis"))
            {
                errors.Add("amountThresholdTry tanımlıysa amountBasis (VatIncluded/VatExcluded) zorunludur.");
            }
        }

        return errors;
    }

    public RuleOutcome Evaluate(LegalRule rule, ComplianceContext context)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Buyer.IsEFaturaRegistered)
        {
            return RuleOutcome.NotApplicable("Alıcı e-Fatura kayıtlı kullanıcısı olduğundan e-Arşiv kuralı uygulanmaz.");
        }

        var regimes = rule.Parameters.GetEnumList<BookkeepingRegime>("sellerRegimes");
        if (regimes.Count > 0 && !regimes.Contains(context.Seller.Regime))
        {
            return RuleOutcome.NotApplicable($"Satıcının rejimi ({context.Seller.Regime}) '{rule.Title}' kapsamında değil.");
        }

        var outcome = RuleOutcome.Apply().Explain("Alıcı e-Fatura uygulamasına kayıtlı kullanıcı değil.");
        if (regimes.Count > 0)
        {
            outcome.Explain($"Satıcı, '{rule.Title}' kapsamındaki rejimde ({context.Seller.Regime}).");
        }

        var threshold = rule.Parameters.TryGetDecimal("amountThresholdTry");
        if (threshold is null)
        {
            outcome.RouteTo(EDocumentType.EArsiv)
                .Explain($"{Fmt.Date(context.DocumentDate)} tarihinde yürürlükte olan kurala göre tutardan bağımsız olarak e-Arşiv Fatura düzenlenmesi gerekir.");
        }
        else
        {
            var basis = rule.Parameters.GetEnum("amountBasis", AmountBasis.VatIncluded);
            var amount = context.Amount(basis);
            if (amount > threshold.Value)
            {
                outcome.RouteTo(EDocumentType.EArsiv)
                    .Explain($"Belge tutarı ({Fmt.Basis(basis)} {Fmt.Try(amount)}) {Fmt.Try(threshold.Value)} eşiğini aştığından e-Arşiv Fatura düzenlenmesi gerekir.");
            }
            else if (context.Seller.IsEArchiveRegistered)
            {
                return outcome.RouteTo(EDocumentType.EArsiv)
                    .Explain($"Belge tutarı ({Fmt.Basis(basis)} {Fmt.Try(amount)}) {Fmt.Try(threshold.Value)} eşiğini aşmıyor; ancak satıcı e-Arşiv Fatura uygulamasına kayıtlı olduğundan e-Fatura kullanıcısı olmayanlara e-Arşiv Fatura düzenler.");
            }
            else
            {
                return outcome.RouteTo(EDocumentType.PaperInvoice)
                    .Explain($"Belge tutarı ({Fmt.Basis(basis)} {Fmt.Try(amount)}) {Fmt.Try(threshold.Value)} eşiğini aşmadığından e-Arşiv zorunluluğu doğmaz; kâğıt fatura düzenlenebilir.");
            }
        }

        if (!context.Seller.IsEArchiveRegistered)
        {
            outcome.Add(
                rule,
                "REG",
                "Belge e-Arşiv Fatura olarak düzenlenmeli, ancak satıcı e-Arşiv Fatura uygulamasına kayıtlı görünmüyor. Kayıt/başvuru durumu kontrol edilmelidir.",
                FindingSeverity.Warning,
                blocking: false);
        }

        return outcome;
    }
}
