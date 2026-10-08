namespace GIBFramework.Contracts;

public interface ITenantContext
{
    Guid? TenantId { get; }

    string? UserId { get; }

    IReadOnlyList<string> Roles => [];

    bool IsSystem { get; }

    string? Ip { get; }

    string? UserAgent { get; }

    string? CorrelationId { get; }

    Guid RequireTenant() => TenantId ?? throw new ForbiddenOperationException("TENANT_REQUIRED", "Bu işlem için kiracı (tenant_id) bağlamı gerekir.");

    string RequireUser() => UserId ?? throw new ForbiddenOperationException("USER_REQUIRED", "Bu işlem için kullanıcı kimliği gerekir.");
}
