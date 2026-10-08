namespace GIBFramework.Contracts;

public sealed record TaxpayerLookupResult(
    string TaxId,
    bool IsEFaturaRegistered,
    string? Title,
    IReadOnlyList<string> Aliases,
    DateOnly? RegisteredSince,
    string Source);

public enum SendOutcome
{
    Accepted,

    Rejected,

    Unknown,
}

public sealed record SendRequest(
    Guid TenantId,
    Guid InvoiceId,
    Guid Ettn,
    string DocumentNumber,
    EDocumentType DocumentType,
    string IdempotencyKey,
    byte[] SignedUbl,
    string? CustomerAlias);

public sealed record SendResult(SendOutcome Outcome, string? ProviderReference, string? Message, byte[] RawRequest, byte[] RawResponse);

public enum ProviderDocumentState
{
    NotFound,
    Received,
    Sent,
    Delivered,
    Accepted,
    Rejected,
    Cancelled,
}

public sealed record ProviderStatusResult(ProviderDocumentState State, string? ProviderReference, string? Message, DateTimeOffset CheckedAt);

public sealed record ProviderCancellationResult(bool Success, string? ProviderReference, string? Message);

public interface IEDocumentProvider
{
    string Name { get; }

    Task<TaxpayerLookupResult> LookupTaxpayerAsync(string taxId, CancellationToken cancellationToken);

    Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken);

    Task<ProviderStatusResult> GetStatusAsync(Guid tenantId, Guid ettn, CancellationToken cancellationToken);

    Task<ProviderCancellationResult> CancelArchiveInvoiceAsync(Guid tenantId, Guid ettn, string reason, CancellationToken cancellationToken);
}
