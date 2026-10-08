using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace GIBFramework.Services.Ubl;

public sealed record UblPartySummary(string? TaxId, string? Scheme, string? Name, string? FirstName, string? FamilyName);

public sealed record UblInvoiceSummary
{
    public string? UblVersionId { get; init; }

    public string? CustomizationId { get; init; }

    public string? ProfileId { get; init; }

    public string? Id { get; init; }

    public string? Uuid { get; init; }

    public string? IssueDate { get; init; }

    public string? InvoiceTypeCode { get; init; }

    public string? Currency { get; init; }

    public int? LineCountNumeric { get; init; }

    public int LineCount { get; init; }

    public UblPartySummary Supplier { get; init; } = new(null, null, null, null, null);

    public UblPartySummary Customer { get; init; } = new(null, null, null, null, null);

    public decimal? LineExtensionAmount { get; init; }

    public decimal? TaxExclusiveAmount { get; init; }

    public decimal? TaxInclusiveAmount { get; init; }

    public decimal? AllowanceTotalAmount { get; init; }

    public decimal? PayableAmount { get; init; }

    public decimal TaxTotal { get; init; }

    public decimal WithholdingTotal { get; init; }

    public decimal SumOfLineExtensions { get; init; }

    public bool HasSignature { get; init; }
}

public static class UblInvoiceReader
{
    public static XmlDocument LoadSecure(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 50_000_000,
        };
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var reader = XmlReader.Create(stream, settings);
        doc.Load(reader);
        return doc;
    }

    public static UblInvoiceSummary Read(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var x = XDocument.Parse(document.OuterXml, LoadOptions.PreserveWhitespace);
        var root = x.Root ?? throw new DomainException("UBL_EMPTY", "XML belgesi boş.");
        if (root.Name != UblNs.Invoice + "Invoice")
        {
            throw new DomainException("UBL_NOT_INVOICE", $"Kök eleman UBL Invoice değil: {root.Name}.");
        }

        var lines = root.Elements(UblNs.Cac + "InvoiceLine").ToList();
        var monetary = root.Element(UblNs.Cac + "LegalMonetaryTotal");

        return new UblInvoiceSummary
        {
            UblVersionId = Cbc(root, "UBLVersionID"),
            CustomizationId = Cbc(root, "CustomizationID"),
            ProfileId = Cbc(root, "ProfileID"),
            Id = Cbc(root, "ID"),
            Uuid = Cbc(root, "UUID"),
            IssueDate = Cbc(root, "IssueDate"),
            InvoiceTypeCode = Cbc(root, "InvoiceTypeCode"),
            Currency = Cbc(root, "DocumentCurrencyCode"),
            LineCountNumeric = int.TryParse(Cbc(root, "LineCountNumeric"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lc) ? lc : null,
            LineCount = lines.Count,
            Supplier = Party(root.Element(UblNs.Cac + "AccountingSupplierParty")),
            Customer = Party(root.Element(UblNs.Cac + "AccountingCustomerParty")),
            LineExtensionAmount = Dec(monetary, "LineExtensionAmount"),
            TaxExclusiveAmount = Dec(monetary, "TaxExclusiveAmount"),
            TaxInclusiveAmount = Dec(monetary, "TaxInclusiveAmount"),
            AllowanceTotalAmount = Dec(monetary, "AllowanceTotalAmount"),
            PayableAmount = Dec(monetary, "PayableAmount"),
            TaxTotal = root.Elements(UblNs.Cac + "TaxTotal").Sum(t => Dec(t, "TaxAmount") ?? 0),
            WithholdingTotal = root.Elements(UblNs.Cac + "WithholdingTaxTotal").Sum(t => Dec(t, "TaxAmount") ?? 0),
            SumOfLineExtensions = lines.Sum(l => Dec(l, "LineExtensionAmount") ?? 0),
            HasSignature = root.Descendants(UblNs.Ds + "Signature").Any(),
        };
    }

    private static UblPartySummary Party(XElement? wrapper)
    {
        var party = wrapper?.Element(UblNs.Cac + "Party");
        var id = party?.Elements(UblNs.Cac + "PartyIdentification").Select(p => p.Element(UblNs.Cbc + "ID"))
            .FirstOrDefault(e => e?.Attribute("schemeID")?.Value is "VKN" or "TCKN");
        var person = party?.Element(UblNs.Cac + "Person");
        return new UblPartySummary(
            id?.Value.Trim(),
            id?.Attribute("schemeID")?.Value,
            party?.Element(UblNs.Cac + "PartyName")?.Element(UblNs.Cbc + "Name")?.Value,
            person?.Element(UblNs.Cbc + "FirstName")?.Value,
            person?.Element(UblNs.Cbc + "FamilyName")?.Value);
    }

    private static string? Cbc(XElement parent, string name) => parent.Element(UblNs.Cbc + name)?.Value.Trim();

    private static decimal? Dec(XElement? parent, string name) =>
        decimal.TryParse(parent?.Element(UblNs.Cbc + name)?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
}
