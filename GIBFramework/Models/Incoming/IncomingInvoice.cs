namespace GIBFramework.Models.Incoming;

public enum IncomingStatus
{
    Received,
    Validated,
    Rejected,
    Accepted,
    Declined,
}

public sealed class IncomingInvoice
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid Uuid { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Profile { get; set; } = string.Empty;

    public string SupplierTaxId { get; set; } = string.Empty;

    public string SupplierTitle { get; set; } = string.Empty;

    public string CustomerTaxId { get; set; } = string.Empty;

    public DateOnly IssueDate { get; set; }

    public decimal PayableAmount { get; set; }

    public string Currency { get; set; } = "TRY";

    public IncomingStatus Status { get; set; }

    public bool SignaturePresent { get; set; }

    public bool SignatureValid { get; set; }

    public List<string> Issues { get; set; } = [];

    public string XmlSha256 { get; set; } = string.Empty;

    public string ReceivedVia { get; set; } = "Api";

    public DateTimeOffset ReceivedAt { get; set; }

    public string? DecidedBy { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }
}
