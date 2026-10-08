using System.Text.Json.Serialization;

namespace GIBFramework.Models.Invoices;

public enum InvoiceProfile
{
    TEMELFATURA,
    TICARIFATURA,
    EARSIVFATURA,
}

public enum IssuanceChannel
{
    Integrator,

    GibPortal,
}

public enum InvoiceTypeCode
{
    SATIS,
    IADE,
    TEVKIFAT,
    ISTISNA,
    OZELMATRAH,
    IHRACKAYITLI,
}

public sealed class InvoiceParty
{
    public string TaxId { get; set; } = string.Empty;

    public PartyKind Kind { get; set; }

    public BookkeepingRegime Regime { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? FamilyName { get; set; }

    public string? TaxOffice { get; set; }

    public string? Neighborhood { get; set; }

    public string? Street { get; set; }

    public string? BuildingNumber { get; set; }

    public string? District { get; set; }

    public string City { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public string Country { get; set; } = "Türkiye";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Alias { get; set; }

    public bool IsEFaturaRegistered { get; set; }

    public bool IsEArchiveRegistered { get; set; }

    [JsonIgnore]
    public string FullAddress => string.Join(" ", new[] { Neighborhood, Street, BuildingNumber, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public PartyFacts ToFacts() => new()
    {
        TaxId = TaxId,
        Kind = Kind,
        Regime = Regime,
        IsEFaturaRegistered = IsEFaturaRegistered,
        IsEArchiveRegistered = IsEArchiveRegistered,
        Title = Title,
        Address = string.IsNullOrWhiteSpace(City) ? null : FullAddress,
        TaxOffice = TaxOffice,
    };
}

public sealed class InvoiceLine
{
    public int LineNo { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Quantity { get; set; }

    public string UnitCode { get; set; } = "C62";

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal VatRate { get; set; }

    public string? VatExemptionCode { get; set; }

    public string? WithholdingCode { get; set; }

    public decimal LineExtensionAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal WithholdingPercent { get; set; }

    public decimal WithholdingAmount { get; set; }
}

public sealed record TaxSubtotal(
    string TaxTypeCode,
    string TaxName,
    decimal Percent,
    decimal TaxableAmount,
    decimal TaxAmount,
    string? ExemptionCode,
    string? ExemptionReason);

public sealed record WithholdingSubtotal(string Code, string Name, decimal Percent, decimal TaxableAmount, decimal TaxAmount);

public sealed class InvoiceTotals
{
    public decimal LineExtensionAmount { get; set; }

    public decimal AllowanceTotalAmount { get; set; }

    public decimal TaxExclusiveAmount { get; set; }

    public decimal VatTotal { get; set; }

    public decimal TaxInclusiveAmount { get; set; }

    public decimal WithholdingTotal { get; set; }

    public decimal PayableAmount { get; set; }

    public List<TaxSubtotal> VatSubtotals { get; set; } = [];

    public List<WithholdingSubtotal> WithholdingSubtotals { get; set; } = [];
}

public sealed class Invoice
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid Uuid { get; set; }

    public string? DocumentNumber { get; set; }

    public string? DraftNumber { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public EDocumentType DocumentType { get; set; }

    public InvoiceProfile Profile { get; set; }

    public InvoiceTypeCode TypeCode { get; set; } = InvoiceTypeCode.SATIS;

    public DateOnly IssueDate { get; set; }

    public TimeOnly IssueTime { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    public string Currency { get; set; } = "TRY";

    public decimal? ExchangeRate { get; set; }

    public InvoiceParty Supplier { get; set; } = new();

    public InvoiceParty Customer { get; set; } = new();

    public List<InvoiceLine> Lines { get; set; } = [];

    public List<string> Notes { get; set; } = [];

    public bool DespatchRequired { get; set; }

    public string? DespatchNumber { get; set; }

    public DateOnly? DespatchDate { get; set; }

    public string? OrderNumber { get; set; }

    public string SendingType { get; set; } = "ELEKTRONIK";

    public IssuanceChannel IssuanceChannel { get; set; } = IssuanceChannel.Integrator;

    public GibPortalEnvironment? PortalEnvironment { get; set; }

    public InvoiceTotals Totals { get; set; } = new();

    public string IdempotencyKey { get; set; } = string.Empty;

    public ComplianceDecision? Decision { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? SignedBy { get; set; }

    public DateTimeOffset? SignedAt { get; set; }

    public string? SignedXmlSha256 { get; set; }

    public string? ProviderReference { get; set; }

    public DateTimeOffset? TransmittedAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [JsonIgnore]
    public byte[]? RowVersion { get; set; }

    [JsonIgnore]
    public bool IsContentLocked => Status is not (DocumentStatus.Draft or DocumentStatus.Validating or DocumentStatus.Validated
        or DocumentStatus.AwaitingApproval or DocumentStatus.Approved or DocumentStatus.Signing);

    public void TransitionTo(DocumentStatus to)
    {
        DocumentLifecycle.EnsureTransition(Status, to);
        Status = to;
    }

    public decimal ToTry(decimal amount) => Currency == "TRY"
        ? amount
        : decimal.Round(amount * (ExchangeRate ?? throw new DomainException("INVOICE_EXCHANGE_RATE", "Dövizli faturada kur zorunludur.")), 2, MidpointRounding.AwayFromZero);

    public ComplianceContext ToComplianceContext() => new()
    {
        DocumentDate = IssueDate,
        DeliveryDate = DeliveryDate,
        Seller = Supplier.ToFacts(),
        Buyer = Customer.ToFacts(),
        TotalExcludingVatTry = ToTry(Totals.TaxExclusiveAmount),
        TotalIncludingVatTry = ToTry(Totals.TaxInclusiveAmount),
        DocumentCurrency = Currency,
        DespatchRequired = DespatchRequired,
        DespatchNumber = DespatchNumber,
        Lines = [.. Lines.Select(l => new LineFacts(l.Name, l.Quantity, l.UnitCode, l.UnitPrice, l.LineExtensionAmount))],
    };
}
