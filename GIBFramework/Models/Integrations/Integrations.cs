namespace GIBFramework.Models.Integrations;

public enum IntegrationKind
{
    Webhook,
    WooCommerce,
    Shopify,
    Whmcs,
    WiseCp,
    Trendyol,
    Hepsiburada,
    N11,
}

public enum DeliveryDirection
{
    In,
    Out,
}

public enum DeliveryStatus
{
    Pending,
    Succeeded,
    Failed,
    Ignored,
}

public sealed class Integration
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public IntegrationKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Dictionary<string, string?> Settings { get; set; } = [];

    public List<string> Events { get; set; } = [];

    public string? SecretsProtected { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastInboundAt { get; set; }

    public DateTimeOffset? LastOutboundAt { get; set; }

    public string? Setting(string key) => Settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    public bool Flag(string key) => string.Equals(Setting(key), "true", StringComparison.OrdinalIgnoreCase);
}

public sealed class IntegrationSettingsDocument
{
    public Dictionary<string, string?> Settings { get; set; } = [];

    public List<string> Events { get; set; } = [];
}

public sealed class IntegrationDelivery
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid IntegrationId { get; set; }

    public DeliveryDirection Direction { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; }

    public int? HttpStatus { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? RequestBody { get; set; }

    public string? ResponseBody { get; set; }

    public string? Error { get; set; }

    public Guid? InvoiceId { get; set; }

    public string? ExternalId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record InboundOrder(
    string ExternalId,
    string? OrderNumber,
    string Currency,
    bool PricesIncludeTax,
    InboundCustomer Customer,
    IReadOnlyList<InboundLine> Lines,
    IReadOnlyList<string> Notes);

public sealed record InboundCustomer(
    string? TaxId,
    string? Name,
    string? Company,
    string? TaxOffice,
    string? Email,
    string? Phone,
    string? Address,
    string? District,
    string? City,
    string? PostalCode,
    string? Country);

public sealed record InboundLine(string Name, decimal Quantity, decimal UnitPrice, decimal VatRate, decimal Discount, string? UnitCode);
