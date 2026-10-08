using GIBFramework.Models.Tax;

namespace GIBFramework.Services.Tax;

public sealed record TaxIssue(string Code, string Message);

public sealed class TaxEngine(TaxCatalog catalog)
{
    public TaxCatalog Catalog => catalog;

    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public IReadOnlyList<TaxIssue> Calculate(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var issues = new List<TaxIssue>();
        var date = invoice.IssueDate;

        if (invoice.Lines.Count == 0)
        {
            issues.Add(new("TAX-NO-LINES", "Fatura en az bir satır içermelidir."));
        }

        if (invoice.Currency != "TRY" && invoice.ExchangeRate is not > 0)
        {
            issues.Add(new("TAX-EXCHANGE-RATE", "Dövizli faturada pozitif TL kuru zorunludur."));
        }

        var lineNo = 1;
        foreach (var line in invoice.Lines)
        {
            line.LineNo = lineNo++;
            var label = $"{line.LineNo}. satır";

            if (line.Quantity <= 0)
            {
                issues.Add(new("TAX-QUANTITY", $"{label}: miktar pozitif olmalıdır."));
            }

            if (line.UnitPrice < 0)
            {
                issues.Add(new("TAX-UNIT-PRICE", $"{label}: birim fiyat negatif olamaz."));
            }

            var gross = Round(line.Quantity * line.UnitPrice);
            if (line.DiscountAmount < 0 || line.DiscountAmount > gross)
            {
                issues.Add(new("TAX-DISCOUNT", $"{label}: iskonto 0 ile satır tutarı arasında olmalıdır."));
            }

            line.LineExtensionAmount = Round(gross - line.DiscountAmount);

            if (!catalog.IsVatRateAllowed(line.VatRate, date))
            {
                issues.Add(new("TAX-VAT-RATE", $"{label}: %{line.VatRate} KDV oranı {Fmt.Date(date)} tarihinde tanımlı değil."));
            }

            if (line.VatRate == 0)
            {
                if (string.IsNullOrWhiteSpace(line.VatExemptionCode))
                {
                    issues.Add(new("TAX-EXEMPTION-REQUIRED", $"{label}: %0 KDV için istisna/muafiyet kodu zorunludur."));
                }
                else if (catalog.FindExemption(line.VatExemptionCode) is null)
                {
                    issues.Add(new("TAX-EXEMPTION-UNKNOWN", $"{label}: '{line.VatExemptionCode}' istisna kodu tanımlı değil."));
                }
            }
            else if (!string.IsNullOrWhiteSpace(line.VatExemptionCode))
            {
                issues.Add(new("TAX-EXEMPTION-UNEXPECTED", $"{label}: KDV oranı sıfırdan büyükken istisna kodu kullanılamaz."));
            }

            line.VatAmount = Round(line.LineExtensionAmount * line.VatRate / 100m);
            line.WithholdingPercent = 0;
            line.WithholdingAmount = 0;

            if (!string.IsNullOrWhiteSpace(line.WithholdingCode))
            {
                var definition = catalog.FindWithholding(line.WithholdingCode, date);
                if (definition is null)
                {
                    issues.Add(new("TAX-WITHHOLDING-UNKNOWN", $"{label}: '{line.WithholdingCode}' tevkifat kodu {Fmt.Date(date)} tarihinde tanımlı değil."));
                }
                else if (line.VatRate == 0)
                {
                    issues.Add(new("TAX-WITHHOLDING-NO-VAT", $"{label}: KDV'siz satırda tevkifat uygulanamaz."));
                }
                else
                {
                    line.WithholdingPercent = definition.Percent;
                    line.WithholdingAmount = Round(line.VatAmount * definition.Numerator / definition.Denominator);
                }
            }
        }

        var totals = new InvoiceTotals
        {
            LineExtensionAmount = invoice.Lines.Sum(l => l.LineExtensionAmount),
            AllowanceTotalAmount = invoice.Lines.Sum(l => l.DiscountAmount),
            VatSubtotals =
            [
                .. invoice.Lines
                    .GroupBy(l => (l.VatRate, Code: l.VatExemptionCode))
                    .OrderByDescending(g => g.Key.VatRate)
                    .Select(g => new TaxSubtotal(
                        TaxCatalog.VatTaxTypeCode,
                        catalog.TaxName(TaxCatalog.VatTaxTypeCode),
                        g.Key.VatRate,
                        g.Sum(l => l.LineExtensionAmount),
                        g.Sum(l => l.VatAmount),
                        g.Key.Code,
                        g.Key.Code is null ? null : catalog.FindExemption(g.Key.Code)?.Name)),
            ],
            WithholdingSubtotals =
            [
                .. invoice.Lines
                    .Where(l => l.WithholdingAmount > 0)
                    .GroupBy(l => l.WithholdingCode!)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new WithholdingSubtotal(
                        g.Key,
                        catalog.FindWithholding(g.Key, date)?.Name ?? g.Key,
                        g.First().WithholdingPercent,
                        g.Sum(l => l.VatAmount),
                        g.Sum(l => l.WithholdingAmount))),
            ],
        };

        totals.TaxExclusiveAmount = totals.LineExtensionAmount;
        totals.VatTotal = totals.VatSubtotals.Sum(s => s.TaxAmount);
        totals.TaxInclusiveAmount = totals.TaxExclusiveAmount + totals.VatTotal;
        totals.WithholdingTotal = totals.WithholdingSubtotals.Sum(s => s.TaxAmount);
        totals.PayableAmount = totals.TaxInclusiveAmount - totals.WithholdingTotal;
        invoice.Totals = totals;

        if (invoice.TypeCode is InvoiceTypeCode.SATIS or InvoiceTypeCode.TEVKIFAT or InvoiceTypeCode.ISTISNA)
        {
            invoice.TypeCode = totals.WithholdingTotal > 0
                ? InvoiceTypeCode.TEVKIFAT
                : invoice.Lines.Count > 0 && invoice.Lines.All(l => l.VatRate == 0 && l.VatExemptionCode is not null && l.VatExemptionCode != "351")
                    ? InvoiceTypeCode.ISTISNA
                    : InvoiceTypeCode.SATIS;
        }

        return issues;
    }
}
