using System.ComponentModel.DataAnnotations;

namespace GIBFramework.DTOs;

public sealed record PartyDto
{
    [StringLength(11)]
    public string? TaxId { get; init; }

    public PartyKind Kind { get; init; }

    public BookkeepingRegime Regime { get; init; }

    [Required, StringLength(250)]
    public string Title { get; init; } = string.Empty;

    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? FamilyName { get; init; }

    [StringLength(100)]
    public string? TaxOffice { get; init; }

    [StringLength(150)]
    public string? Neighborhood { get; init; }

    [StringLength(250)]
    public string? Street { get; init; }

    [StringLength(20)]
    public string? BuildingNumber { get; init; }

    [StringLength(100)]
    public string? District { get; init; }

    [Required, StringLength(100)]
    public string City { get; init; } = string.Empty;

    [StringLength(10)]
    public string? PostalCode { get; init; }

    [StringLength(100)]
    public string Country { get; init; } = "Türkiye";

    [EmailAddress, StringLength(200)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(200)]
    public string? Alias { get; init; }
}

public sealed record InvoiceLineDto
{
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0.0001", "999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Quantity { get; init; }

    [StringLength(10)]
    public string UnitCode { get; init; } = "C62";

    [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal UnitPrice { get; init; }

    [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal DiscountAmount { get; init; }

    public decimal VatRate { get; init; }

    [StringLength(10)]
    public string? VatExemptionCode { get; init; }

    [StringLength(10)]
    public string? WithholdingCode { get; init; }
}

public sealed record CreateInvoiceRequest
{
    public DateOnly? DeliveryDate { get; init; }

    public InvoiceProfile Profile { get; init; } = InvoiceProfile.TEMELFATURA;

    public InvoiceTypeCode TypeCode { get; init; } = InvoiceTypeCode.SATIS;

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "TRY";

    public decimal? ExchangeRate { get; init; }

    [Required]
    public PartyDto Customer { get; init; } = new();

    [Required, MinLength(1)]
    public List<InvoiceLineDto> Lines { get; init; } = [];

    public List<string> Notes { get; init; } = [];

    public bool DespatchRequired { get; init; }

    [StringLength(50)]
    public string? DespatchNumber { get; init; }

    public DateOnly? DespatchDate { get; init; }

    [StringLength(50)]
    public string? OrderNumber { get; init; }

    [RegularExpression("^(ELEKTRONIK|KAGIT)$")]
    public string SendingType { get; init; } = "ELEKTRONIK";

    public bool SaveCustomer { get; init; }

    public bool? CustomerIsEFaturaRegistered { get; init; }
}

public sealed record ReasonRequest([Required, StringLength(1000, MinimumLength = 3)] string Reason);

public sealed record ObjectionRequest(
    [Required, StringLength(50)] string Method,
    [StringLength(100)] string? ReferenceNumber,
    DateOnly NotificationDate,
    [Required, StringLength(1000, MinimumLength = 3)] string Reason);

public sealed record DecisionView(
    Guid DecisionId,
    EDocumentType DocumentType,
    ValidationStatus Validation,
    string? RoutingRuleVersion,
    IReadOnlyList<string> LegalBasis,
    IReadOnlyList<string> Explanation,
    IReadOnlyList<ComplianceFinding> Findings,
    bool UsesUnapprovedRules,
    string RuleSetHash);

public sealed record InvoiceView
{
    public Guid Id { get; init; }

    public Guid Ettn { get; init; }

    public string? DocumentNumber { get; init; }

    public string? DraftNumber { get; init; }

    public DocumentStatus Status { get; init; }

    public IReadOnlyList<DocumentStatus> AllowedTransitions { get; init; } = [];

    public EDocumentType DocumentType { get; init; }

    public InvoiceProfile Profile { get; init; }

    public InvoiceTypeCode TypeCode { get; init; }

    public DateOnly IssueDate { get; init; }

    public TimeOnly IssueTime { get; init; }

    public DateOnly? DeliveryDate { get; init; }

    public string Currency { get; init; } = "TRY";

    public decimal? ExchangeRate { get; init; }

    public InvoiceParty Supplier { get; init; } = new();

    public InvoiceParty Customer { get; init; } = new();

    public IReadOnlyList<InvoiceLine> Lines { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public InvoiceTotals Totals { get; init; } = new();

    public DecisionView? Decision { get; init; }

    public string? LastError { get; init; }

    public string? ProviderReference { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public string? ApprovedBy { get; init; }

    public string? SignedBy { get; init; }

    public DateTimeOffset? SignedAt { get; init; }

    public string? SignedXmlSha256 { get; init; }

    public IssuanceChannel IssuanceChannel { get; init; }

    public GibPortalEnvironment? PortalEnvironment { get; init; }

    public string? DespatchNumber { get; init; }

    public string? OrderNumber { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
