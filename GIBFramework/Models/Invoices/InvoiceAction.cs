namespace GIBFramework.Models.Invoices;

public enum InvoiceActionKind
{
    Cancellation,
    Objection,
}

public sealed class InvoiceAction
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid InvoiceId { get; set; }

    public InvoiceActionKind Kind { get; set; }

    public string? Method { get; set; }

    public string? ReferenceNumber { get; set; }

    public DateOnly? NotificationDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Requested";

    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string? ProviderReference { get; set; }
}
