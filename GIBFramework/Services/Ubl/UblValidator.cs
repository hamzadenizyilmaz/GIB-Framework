using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace GIBFramework.Services.Ubl;

public sealed record UblIssue(string Code, FindingSeverity Severity, string Message);

public sealed partial class UblValidator(UblOptions options, IHostEnvironment environment)
{
    private static readonly string[] Profiles = ["TEMELFATURA", "TICARIFATURA", "EARSIVFATURA", "IHRACAT", "YOLCUBERABERFATURA", "KAMU"];
    private static readonly string[] TypeCodes = ["SATIS", "IADE", "TEVKIFAT", "ISTISNA", "OZELMATRAH", "IHRACKAYITLI"];
    private readonly Lazy<XmlSchemaSet?> _schemas = new(() => LoadSchemas(options, environment));

    public bool SchemaValidationAvailable => _schemas.Value is not null;

    public IReadOnlyList<UblIssue> Validate(XmlDocument document, bool requireSignature)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<UblIssue>();

        if (_schemas.Value is { } schemas)
        {
            var copy = new XmlDocument { PreserveWhitespace = true, XmlResolver = null, Schemas = schemas };
            copy.LoadXml(document.OuterXml);
            copy.Validate((_, e) => issues.Add(new UblIssue("UBL-XSD", e.Severity == XmlSeverityType.Error ? FindingSeverity.Error : FindingSeverity.Warning, e.Message)));
        }
        else
        {
            issues.Add(new UblIssue(
                "UBL-XSD-NOT-CONFIGURED",
                options.RequireSchemaValidation ? FindingSeverity.Error : FindingSeverity.Info,
                "UBL-TR XSD paketi yapılandırılmadı (Ubl:SchemaDirectory); XSD doğrulaması atlandı."));
        }

        UblInvoiceSummary s;
        try
        {
            s = UblInvoiceReader.Read(document);
        }
        catch (DomainException ex)
        {
            issues.Add(new UblIssue(ex.Code, FindingSeverity.Error, ex.Message));
            return issues;
        }

        Require(issues, s.UblVersionId == UblInvoiceWriter.UblVersionId, "UBL-VERSION", $"UBLVersionID {UblInvoiceWriter.UblVersionId} olmalıdır.");
        Require(issues, s.CustomizationId == UblInvoiceWriter.CustomizationId, "UBL-CUSTOMIZATION", $"CustomizationID {UblInvoiceWriter.CustomizationId} olmalıdır.");
        Require(issues, s.ProfileId is not null && Profiles.Contains(s.ProfileId), "UBL-PROFILE", $"Geçersiz ProfileID: '{s.ProfileId}'.");
        Require(issues, s.InvoiceTypeCode is not null && TypeCodes.Contains(s.InvoiceTypeCode), "UBL-TYPECODE", $"Geçersiz InvoiceTypeCode: '{s.InvoiceTypeCode}'.");
        Require(issues, s.Id is not null && DocumentNumberPattern().IsMatch(s.Id), "UBL-ID", $"Belge numarası 3 karakter + 4 hane yıl + 9 hane sıra biçiminde olmalıdır: '{s.Id}'.");
        Require(issues, Guid.TryParseExact(s.Uuid, "D", out var uuid) && uuid != Guid.Empty, "UBL-UUID", "UUID (ETTN) geçerli bir GUID olmalıdır.");
        Require(issues, DateOnly.TryParseExact(s.IssueDate, "yyyy-MM-dd", out _), "UBL-ISSUEDATE", "IssueDate yyyy-MM-dd biçiminde olmalıdır.");
        Require(issues, s.Currency is { Length: 3 }, "UBL-CURRENCY", "DocumentCurrencyCode 3 harfli ISO 4217 kodu olmalıdır.");
        Require(issues, s.LineCount > 0, "UBL-LINES", "En az bir InvoiceLine olmalıdır.");
        Require(issues, s.LineCountNumeric is null || s.LineCountNumeric == s.LineCount, "UBL-LINECOUNT", "LineCountNumeric satır sayısıyla uyuşmuyor.");

        CheckParty(issues, s.Supplier, "SUPPLIER", "Satıcı");
        CheckParty(issues, s.Customer, "CUSTOMER", "Alıcı");

        Require(issues, Close(s.LineExtensionAmount, s.SumOfLineExtensions), "UBL-TOTAL-LINES", "LineExtensionAmount satır toplamına eşit değil.");
        Require(issues, Close(s.TaxInclusiveAmount, (s.TaxExclusiveAmount ?? 0) + s.TaxTotal), "UBL-TOTAL-TAX", "TaxInclusiveAmount = TaxExclusiveAmount + vergiler olmalıdır.");
        Require(issues, Close(s.PayableAmount, (s.TaxInclusiveAmount ?? 0) - s.WithholdingTotal), "UBL-TOTAL-PAYABLE", "PayableAmount = TaxInclusiveAmount − tevkifat olmalıdır.");
        Require(issues, s.WithholdingTotal == 0 || s.InvoiceTypeCode is "TEVKIFAT" or "IADE", "UBL-WITHHOLDING-TYPE", "Tevkifat içeren fatura TEVKIFAT tipinde olmalıdır.");

        if (requireSignature)
        {
            Require(issues, s.HasSignature, "UBL-SIGNATURE", "Belge imzalı değil (ds:Signature yok).");
            if (s.HasSignature)
            {
                CheckSignatureStructure(document, issues);
            }
        }

        return issues;
    }

    private static void CheckSignatureStructure(XmlDocument document, List<UblIssue> issues)
    {
        var ns = new XmlNamespaceManager(document.NameTable);
        ns.AddNamespace("inv", UblNs.Invoice.NamespaceName);
        ns.AddNamespace("ext", UblNs.Ext.NamespaceName);
        ns.AddNamespace("cac", UblNs.Cac.NamespaceName);
        ns.AddNamespace("cbc", UblNs.Cbc.NamespaceName);
        ns.AddNamespace("ds", UblNs.Ds.NamespaceName);
        ns.AddNamespace("xades", UblNs.Xades.NamespaceName);

        var signatures = document.SelectNodes("/inv:Invoice/ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent/ds:Signature", ns)!;
        Require(issues, signatures.Count == 1, "GIB-SIG-PLACEMENT", "XAdES imzası ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent içinde tek ds:Signature olmalıdır.");
        if (signatures.Count != 1)
        {
            return;
        }

        var sig = signatures[0]!;
        Require(issues, sig.SelectNodes("ds:SignedInfo/ds:Reference[@URI='']", ns)!.Count == 1, "GIB-SIG-REFERENCE", "SignedInfo içinde tam olarak bir Reference URI=\"\" olmalıdır (enveloped).");
        Require(issues, sig.SelectNodes("ds:SignedInfo/ds:Reference/ds:Transforms", ns)!.Count > 0, "GIB-SIG-TRANSFORMS", "Reference Transforms elemanı zorunludur.");
        var method = sig.SelectSingleNode("ds:SignedInfo/ds:SignatureMethod/@Algorithm", ns)?.Value;
        Require(issues, method is not null && method != "http://www.w3.org/2000/09/xmldsig#rsa-sha1", "GIB-SIG-METHOD", "UBL 2.1 belgelerinde rsa-sha1 imza algoritması kullanılamaz.");
        Require(issues, sig.SelectSingleNode("ds:KeyInfo/ds:X509Data/ds:X509Certificate", ns) is not null, "GIB-SIG-CERT", "KeyInfo/X509Data/X509Certificate zorunludur.");
        Require(issues, sig.SelectSingleNode("ds:Object/xades:QualifyingProperties/xades:SignedProperties/xades:SignedSignatureProperties/xades:SigningTime", ns) is not null, "GIB-SIG-SIGNINGTIME", "XAdES SigningTime zorunludur.");
        Require(issues, sig.SelectSingleNode("ds:Object/xades:QualifyingProperties/xades:SignedProperties/xades:SignedSignatureProperties/xades:SigningCertificate", ns) is not null, "GIB-SIG-SIGNINGCERT", "XAdES SigningCertificate zorunludur.");
        Require(issues, document.SelectSingleNode("/inv:Invoice/cac:Signature/cbc:ID[@schemeID='VKN_TCKN']", ns) is not null, "GIB-SIG-CAC", "cac:Signature/cbc:ID@schemeID='VKN_TCKN' zorunludur.");
    }

    private static void CheckParty(List<UblIssue> issues, UblPartySummary p, string code, string label)
    {
        if (p.TaxId is null || !TaxIdentifier.TryParse(p.TaxId, out var id, out _))
        {
            issues.Add(new UblIssue($"UBL-{code}-ID", FindingSeverity.Error, $"{label} VKN/TCKN geçersiz veya eksik."));
            return;
        }

        Require(issues, p.Scheme == (id.Type == TaxIdentifierType.Vkn ? "VKN" : "TCKN"), $"UBL-{code}-SCHEME", $"{label} schemeID kimlik türüyle uyuşmuyor.");
        if (id.Type == TaxIdentifierType.Tckn)
        {
            Require(issues, !string.IsNullOrWhiteSpace(p.FirstName) && !string.IsNullOrWhiteSpace(p.FamilyName), $"UBL-{code}-PERSON", $"{label} TCKN'li gerçek kişi; Person/FirstName ve FamilyName zorunludur.");
        }
        else
        {
            Require(issues, !string.IsNullOrWhiteSpace(p.Name), $"UBL-{code}-NAME", $"{label} VKN'li; PartyName/Name zorunludur.");
        }
    }

    private static void Require(List<UblIssue> issues, bool condition, string code, string message)
    {
        if (!condition)
        {
            issues.Add(new UblIssue(code, FindingSeverity.Error, message));
        }
    }

    private static bool Close(decimal? actual, decimal expected) => actual is { } a && Math.Abs(a - expected) <= 0.01m;

    private static XmlSchemaSet? LoadSchemas(UblOptions options, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(options.SchemaDirectory))
        {
            return null;
        }

        var dir = Path.GetFullPath(options.SchemaDirectory, environment.ContentRootPath);
        var main = Directory.Exists(dir) ? Directory.GetFiles(dir, "UBL-Invoice-2.1.xsd", SearchOption.AllDirectories).FirstOrDefault() : null;
        if (main is null)
        {
            return null;
        }

        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        using var reader = XmlReader.Create(main, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() });
        set.Add(null, reader);
        set.Compile();
        return set;
    }

    [GeneratedRegex("^[A-Z0-9]{3}[0-9]{4}[0-9]{9}$")]
    private static partial Regex DocumentNumberPattern();
}
