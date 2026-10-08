using System.Collections.Concurrent;
using System.Text;

namespace GIBFramework.Infrastructure.Providers;

public sealed class SandboxEDocumentProvider(ProviderOptions options, IClock clock) : IEDocumentProvider
{
    private readonly ConcurrentDictionary<(Guid Tenant, Guid Ettn), SandboxDocument> _documents = new();

    public string Name => "Sandbox";

    public int SendCount(Guid tenantId, Guid ettn) => _documents.TryGetValue((tenantId, ettn), out var d) ? d.SendCount : 0;

    public Task<TaxpayerLookupResult> LookupTaxpayerAsync(string taxId, CancellationToken cancellationToken)
    {
        var registered = options.SandboxRegisteredTaxpayers.Contains(taxId, StringComparer.Ordinal);
        return Task.FromResult(new TaxpayerLookupResult(
            taxId,
            registered,
            null,
            registered ? [$"urn:mail:defaultpk@{taxId}.sandbox"] : [],
            registered ? new DateOnly(2020, 1, 1) : null,
            Name));
    }

    public Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = Encoding.UTF8.GetString(request.SignedUbl);
        var raw = Encoding.UTF8.GetBytes($"SANDBOX SEND ettn={request.Ettn} no={request.DocumentNumber} type={request.DocumentType} bytes={request.SignedUbl.Length}");

        if (body.Contains("SANDBOX:REJECT", StringComparison.Ordinal))
        {
            return Task.FromResult(new SendResult(SendOutcome.Rejected, null, "Sandbox: belge reddedildi (test senaryosu).", raw, Encoding.UTF8.GetBytes("REJECTED")));
        }

        var doc = _documents.AddOrUpdate(
            (request.TenantId, request.Ettn),
            _ => new SandboxDocument(request.DocumentType, "SBX-" + request.Ettn.ToString("N")[..12].ToUpperInvariant(), ProviderDocumentState.Sent, 1),
            (_, existing) => existing with { SendCount = existing.SendCount + 1 });

        if (body.Contains("SANDBOX:TIMEOUT", StringComparison.Ordinal))
        {
            return Task.FromResult(new SendResult(SendOutcome.Unknown, null, "Sandbox: yanıt zaman aşımına uğradı (belge sağlayıcıya ulaştı).", raw, []));
        }

        return Task.FromResult(new SendResult(SendOutcome.Accepted, doc.Reference, "Sandbox: alındı.", raw, Encoding.UTF8.GetBytes("ACCEPTED " + doc.Reference)));
    }

    public Task<ProviderStatusResult> GetStatusAsync(Guid tenantId, Guid ettn, CancellationToken cancellationToken)
    {
        if (!_documents.TryGetValue((tenantId, ettn), out var doc))
        {
            return Task.FromResult(new ProviderStatusResult(ProviderDocumentState.NotFound, null, "Sandbox: belge yok.", clock.UtcNow));
        }

        if (doc.State == ProviderDocumentState.Sent)
        {
            doc = _documents[(tenantId, ettn)] = doc with { State = ProviderDocumentState.Delivered };
        }

        return Task.FromResult(new ProviderStatusResult(doc.State, doc.Reference, null, clock.UtcNow));
    }

    public Task<ProviderCancellationResult> CancelArchiveInvoiceAsync(Guid tenantId, Guid ettn, string reason, CancellationToken cancellationToken)
    {
        if (!_documents.TryGetValue((tenantId, ettn), out var doc) || doc.DocumentType != EDocumentType.EArsiv)
        {
            return Task.FromResult(new ProviderCancellationResult(false, null, "Sandbox: iptal edilecek e-Arşiv belgesi yok."));
        }

        _documents[(tenantId, ettn)] = doc with { State = ProviderDocumentState.Cancelled };
        return Task.FromResult(new ProviderCancellationResult(true, doc.Reference + "-C", "Sandbox: iptal edildi."));
    }

    private sealed record SandboxDocument(EDocumentType DocumentType, string Reference, ProviderDocumentState State, int SendCount);
}

public sealed class GibDirectProvider : IEDocumentProvider
{
    private const string Message = "GİB doğrudan entegrasyonu etkin değil: GİB izni/test süreci tamamlanmadan kullanılamaz.";

    public string Name => "GibDirect";

    public Task<TaxpayerLookupResult> LookupTaxpayerAsync(string taxId, CancellationToken cancellationToken) => throw new ProviderUnavailableException(Message);

    public Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken) => throw new ProviderUnavailableException(Message);

    public Task<ProviderStatusResult> GetStatusAsync(Guid tenantId, Guid ettn, CancellationToken cancellationToken) => throw new ProviderUnavailableException(Message);

    public Task<ProviderCancellationResult> CancelArchiveInvoiceAsync(Guid tenantId, Guid ettn, string reason, CancellationToken cancellationToken) =>
        throw new ProviderUnavailableException(Message);
}

public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IEDocumentProvider> _providers;

    public ProviderRegistry(IEnumerable<IEDocumentProvider> providers, ProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        if (options.SpecialIntegratorMode)
        {
            throw new InvalidOperationException("SpecialIntegratorMode GİB özel entegratör izni olmadan açılamaz.");
        }

        if (string.Equals(options.Active, "GibDirect", StringComparison.OrdinalIgnoreCase) && !options.GibDirectEnabled)
        {
            throw new InvalidOperationException("GibDirect sağlayıcısı GibDirectEnabled=false iken seçilemez.");
        }

        Active = _providers.TryGetValue(options.Active, out var active)
            ? active
            : throw new InvalidOperationException($"Tanımsız sağlayıcı: {options.Active}");
    }

    public IEDocumentProvider Active { get; }

    public IReadOnlyCollection<string> Names => _providers.Keys;
}
