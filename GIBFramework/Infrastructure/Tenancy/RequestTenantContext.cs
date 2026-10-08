namespace GIBFramework.Infrastructure.Tenancy;

public sealed class RequestTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public string? UserId { get; private set; }

    public IReadOnlyList<string> Roles { get; private set; } = [];

    public bool IsSystem { get; private set; }

    public string? Ip { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CorrelationId { get; private set; }

    public void SetUser(Guid? tenantId, string? userId, string? ip, string? userAgent, string? correlationId, IReadOnlyList<string>? roles = null)
    {
        TenantId = tenantId;
        UserId = userId;
        Roles = roles ?? [];
        Ip = ip;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        IsSystem = false;
    }

    public void UseSystem(Guid? tenantId = null, string? correlationId = null)
    {
        TenantId = tenantId;
        UserId = "system";
        Roles = [];
        IsSystem = true;
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N");
    }
}
