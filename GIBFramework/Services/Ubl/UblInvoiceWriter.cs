using System.Xml;
using System.Xml.Linq;

namespace GIBFramework.Services.Ubl;

public static class UblNs
{
    public static readonly XNamespace Invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    public static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    public static readonly XNamespace Xades = "http://uri.etsi.org/01903/v1.3.2#";
}

public sealed class UblInvoiceWriter
{
    public const string UblVersionId = "2.1";
    public const string CustomizationId = "TR1.2";

    public static string SignatureId(Guid ettn) => "Signature_" + ettn.ToString("D").ToUpperInvariant();

    public XmlDocument Write(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var number = invoice.DocumentNumber ?? throw new DomainException("UBL_NO_NUMBER", "UBL üretimi için belge numarası atanmış olmalıdır.");
        var cur = invoice.Currency;
        var ettn = invoice.Uuid.ToString("D").ToUpperInvariant();

        var root = new XElement(UblNs.Invoice + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", UblNs.Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", UblNs.Cbc),
            new XAttribute(XNamespace.Xmlns + "ext", UblNs.Ext),
            new XAttribute(XNamespace.Xmlns + "ds", UblNs.Ds),
            new XAttribute(XNamespace.Xmlns + "xades", UblNs.Xades),
            new XElement(UblNs.Ext + "UBLExtensions",
                new XElement(UblNs.Ext + "UBLExtension",
                    new XElement(UblNs.Ext + "ExtensionContent"))),
            Cbc("UBLVersionID", UblVersionId),
            Cbc("CustomizationID", CustomizationId),
            Cbc("ProfileID", invoice.Profile.ToString()),
            Cbc("ID", number),
            Cbc("CopyIndicator", "false"),
            Cbc("UUID", ettn),
            Cbc("IssueDate", Invariant.Date(invoice.IssueDate)),
            Cbc("IssueTime", Invariant.Time(invoice.IssueTime)),
            Cbc("InvoiceTypeCode", invoice.TypeCode.ToString()),
            invoice.Notes.Select(n => Cbc("Note", n)),
            Cbc("DocumentCurrencyCode", cur),
            Cbc("LineCountNumeric", invoice.Lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            invoice.OrderNumber is null ? null : new XElement(UblNs.Cac + "OrderReference",
                Cbc("ID", invoice.OrderNumber),
                Cbc("IssueDate", Invariant.Date(invoice.IssueDate))),
            invoice.DespatchNumber is null ? null : new XElement(UblNs.Cac + "DespatchDocumentReference",
                Cbc("ID", invoice.DespatchNumber),
                Cbc("IssueDate", Invariant.Date(invoice.DespatchDate ?? invoice.DeliveryDate ?? invoice.IssueDate))),
            SignatureReference(invoice.Supplier, ettn),
            new XElement(UblNs.Cac + "AccountingSupplierParty", Party(invoice.Supplier)),
            new XElement(UblNs.Cac + "AccountingCustomerParty", Party(invoice.Customer)),
            cur == "TRY" ? null : new XElement(UblNs.Cac + "PricingExchangeRate",
                Cbc("SourceCurrencyCode", cur),
                Cbc("TargetCurrencyCode", "TRY"),
                Cbc("CalculationRate", Invariant.Number(invoice.ExchangeRate ?? 0))),
            TaxTotal(invoice.Totals.VatTotal, invoice.Totals.VatSubtotals, cur),
            invoice.Totals.WithholdingSubtotals.Count == 0 ? null : WithholdingTotal(invoice.Totals, cur),
            new XElement(UblNs.Cac + "LegalMonetaryTotal",
                Amount("LineExtensionAmount", invoice.Totals.LineExtensionAmount, cur),
                Amount("TaxExclusiveAmount", invoice.Totals.TaxExclusiveAmount, cur),
                Amount("TaxInclusiveAmount", invoice.Totals.TaxInclusiveAmount, cur),
                Amount("AllowanceTotalAmount", invoice.Totals.AllowanceTotalAmount, cur),
                Amount("PayableAmount", invoice.Totals.PayableAmount, cur)),
            invoice.Lines.Select(l => Line(l, invoice, cur)));

        var xdoc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using (var reader = xdoc.CreateReader())
        {
            doc.Load(reader);
        }

        if (doc.FirstChild is not XmlDeclaration)
        {
            doc.InsertBefore(doc.CreateXmlDeclaration("1.0", "UTF-8", null), doc.DocumentElement);
        }

        return doc;
    }

    private static XElement SignatureReference(InvoiceParty supplier, string ettn) =>
        new(UblNs.Cac + "Signature",
            new XElement(UblNs.Cbc + "ID", new XAttribute("schemeID", "VKN_TCKN"), supplier.TaxId),
            new XElement(UblNs.Cac + "SignatoryParty",
                new XElement(UblNs.Cac + "PartyIdentification",
                    new XElement(UblNs.Cbc + "ID", new XAttribute("schemeID", Scheme(supplier.TaxId)), supplier.TaxId)),
                Address(supplier)),
            new XElement(UblNs.Cac + "DigitalSignatureAttachment",
                new XElement(UblNs.Cac + "ExternalReference",
                    Cbc("URI", "#Signature_" + ettn))));

    private static XElement Party(InvoiceParty p) =>
        new(UblNs.Cac + "Party",
            new XElement(UblNs.Cac + "PartyIdentification",
                new XElement(UblNs.Cbc + "ID", new XAttribute("schemeID", Scheme(p.TaxId)), p.TaxId)),
            p.Kind == PartyKind.LegalEntity || Scheme(p.TaxId) == "VKN"
                ? new XElement(UblNs.Cac + "PartyName", Cbc("Name", p.Title))
                : null,
            Address(p),
            string.IsNullOrWhiteSpace(p.TaxOffice) ? null : new XElement(UblNs.Cac + "PartyTaxScheme",
                new XElement(UblNs.Cac + "TaxScheme", Cbc("Name", p.TaxOffice))),
            p.Email is null && p.Phone is null ? null : new XElement(UblNs.Cac + "Contact",
                p.Phone is null ? null : Cbc("Telephone", p.Phone),
                p.Email is null ? null : Cbc("ElectronicMail", p.Email)),
            Scheme(p.TaxId) == "TCKN"
                ? new XElement(UblNs.Cac + "Person",
                    Cbc("FirstName", p.FirstName ?? FirstName(p.Title)),
                    Cbc("FamilyName", p.FamilyName ?? FamilyName(p.Title)))
                : null);

    private static XElement Address(InvoiceParty p) =>
        new(UblNs.Cac + "PostalAddress",
            StreetLine(p) is { } street ? Cbc("StreetName", street) : null,
            p.BuildingNumber is null ? null : Cbc("BuildingNumber", p.BuildingNumber),
            Cbc("CitySubdivisionName", p.District ?? p.City),
            Cbc("CityName", p.City),
            p.PostalCode is null ? null : Cbc("PostalZone", p.PostalCode),
            new XElement(UblNs.Cac + "Country", Cbc("Name", p.Country)));

    private static string? StreetLine(InvoiceParty p)
    {
        var parts = new[] { p.Neighborhood, p.Street }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return parts.Length == 0 ? null : string.Join(" ", parts);
    }

    private static XElement TaxTotal(decimal total, IEnumerable<TaxSubtotal> subtotals, string cur) =>
        new(UblNs.Cac + "TaxTotal",
            Amount("TaxAmount", total, cur),
            subtotals.Select(s => new XElement(UblNs.Cac + "TaxSubtotal",
                Amount("TaxableAmount", s.TaxableAmount, cur),
                Amount("TaxAmount", s.TaxAmount, cur),
                Cbc("Percent", Invariant.Number(s.Percent)),
                new XElement(UblNs.Cac + "TaxCategory",
                    s.ExemptionCode is null ? null : Cbc("TaxExemptionReasonCode", s.ExemptionCode),
                    s.ExemptionReason is null ? null : Cbc("TaxExemptionReason", s.ExemptionReason),
                    new XElement(UblNs.Cac + "TaxScheme",
                        Cbc("Name", s.TaxName),
                        Cbc("TaxTypeCode", s.TaxTypeCode))))));

    private static XElement WithholdingTotal(InvoiceTotals totals, string cur) =>
        new(UblNs.Cac + "WithholdingTaxTotal",
            Amount("TaxAmount", totals.WithholdingTotal, cur),
            totals.WithholdingSubtotals.Select(w => new XElement(UblNs.Cac + "TaxSubtotal",
                Amount("TaxableAmount", w.TaxableAmount, cur),
                Amount("TaxAmount", w.TaxAmount, cur),
                Cbc("Percent", Invariant.Number(w.Percent)),
                new XElement(UblNs.Cac + "TaxCategory",
                    new XElement(UblNs.Cac + "TaxScheme",
                        Cbc("Name", w.Name),
                        Cbc("TaxTypeCode", w.Code))))));

    private static XElement Line(InvoiceLine l, Invoice invoice, string cur) =>
        new(UblNs.Cac + "InvoiceLine",
            Cbc("ID", l.LineNo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(UblNs.Cbc + "InvoicedQuantity", new XAttribute("unitCode", l.UnitCode), Invariant.Number(l.Quantity)),
            Amount("LineExtensionAmount", l.LineExtensionAmount, cur),
            l.DiscountAmount == 0 ? null : new XElement(UblNs.Cac + "AllowanceCharge",
                Cbc("ChargeIndicator", "false"),
                Amount("Amount", l.DiscountAmount, cur),
                Amount("BaseAmount", TaxEngineRound(l.Quantity * l.UnitPrice), cur)),
            new XElement(UblNs.Cac + "TaxTotal",
                Amount("TaxAmount", l.VatAmount, cur),
                new XElement(UblNs.Cac + "TaxSubtotal",
                    Amount("TaxableAmount", l.LineExtensionAmount, cur),
                    Amount("TaxAmount", l.VatAmount, cur),
                    Cbc("Percent", Invariant.Number(l.VatRate)),
                    new XElement(UblNs.Cac + "TaxCategory",
                        l.VatExemptionCode is null ? null : Cbc("TaxExemptionReasonCode", l.VatExemptionCode),
                        new XElement(UblNs.Cac + "TaxScheme",
                            Cbc("Name", "KDV"),
                            Cbc("TaxTypeCode", Models.Tax.TaxCatalog.VatTaxTypeCode))))),
            l.WithholdingAmount == 0 ? null : new XElement(UblNs.Cac + "WithholdingTaxTotal",
                Amount("TaxAmount", l.WithholdingAmount, cur),
                new XElement(UblNs.Cac + "TaxSubtotal",
                    Amount("TaxableAmount", l.VatAmount, cur),
                    Amount("TaxAmount", l.WithholdingAmount, cur),
                    Cbc("Percent", Invariant.Number(l.WithholdingPercent)),
                    new XElement(UblNs.Cac + "TaxCategory",
                        new XElement(UblNs.Cac + "TaxScheme",
                            Cbc("Name", invoice.Totals.WithholdingSubtotals.FirstOrDefault(w => w.Code == l.WithholdingCode)?.Name ?? l.WithholdingCode),
                            Cbc("TaxTypeCode", l.WithholdingCode))))),
            new XElement(UblNs.Cac + "Item",
                l.Description is null ? null : Cbc("Description", l.Description),
                Cbc("Name", l.Name)),
            new XElement(UblNs.Cac + "Price", Amount("PriceAmount", l.UnitPrice, cur)));

    private static decimal TaxEngineRound(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static XElement Cbc(string name, string? value) => new(UblNs.Cbc + name, value);

    private static XElement Amount(string name, decimal value, string currency) =>
        new(UblNs.Cbc + name, new XAttribute("currencyID", currency), Invariant.Amount(value));

    private static string Scheme(string taxId) => taxId.Length == 11 ? "TCKN" : "VKN";

    private static string FirstName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? fullName : string.Join(' ', parts[..^1]);
    }

    private static string FamilyName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? fullName : parts[^1];
    }
}
