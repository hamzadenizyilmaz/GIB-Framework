namespace GIBFramework.Services.Invoices;

public sealed record InvoiceEventPayload(Guid InvoiceId, string? From, string To, string Actor, string? Note);

public static class InvoiceEvents
{
    public const string Created = "invoice.created";
    public const string AwaitingApproval = "invoice.awaiting_approval";
    public const string Rejected = "invoice.rejected";
    public const string Approved = "invoice.approved";
    public const string Signed = "invoice.signed";
    public const string Issued = "invoice.issued";
    public const string Cancelled = "invoice.cancelled";
    public const string Failed = "invoice.failed";

    public static IReadOnlyList<(string Code, string Label)> Catalog { get; } =
    [
        (Created, "Fatura taslağı oluşturuldu"),
        (AwaitingApproval, "Fatura onaya gönderildi"),
        (Approved, "Fatura onaylandı"),
        (Rejected, "Fatura onaylanmadı"),
        (Signed, "Fatura imzalandı / numara aldı"),
        (Issued, "Fatura düzenlendi (GİB'e / alıcıya iletildi)"),
        (Cancelled, "Fatura iptal edildi"),
        (Failed, "Fatura gönderilemedi / reddedildi"),
    ];

    private static readonly DocumentStatus[] IssuedStates = [DocumentStatus.Sent, DocumentStatus.Acknowledged, DocumentStatus.Delivered, DocumentStatus.Accepted];

    public static string? ForTransition(DocumentStatus from, DocumentStatus to) => to switch
    {
        DocumentStatus.AwaitingApproval => AwaitingApproval,
        DocumentStatus.Draft when from == DocumentStatus.AwaitingApproval => Rejected,
        DocumentStatus.Approved when from == DocumentStatus.AwaitingApproval => Approved,
        DocumentStatus.Signed => Signed,
        _ when IssuedStates.Contains(to) && !IssuedStates.Contains(from) => Issued,
        DocumentStatus.Cancelled => Cancelled,
        DocumentStatus.Failed or DocumentStatus.Rejected => Failed,
        _ => null,
    };
}
