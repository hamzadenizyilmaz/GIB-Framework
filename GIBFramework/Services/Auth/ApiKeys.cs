using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using GIBFramework.Base.Extensions;
using GIBFramework.DAL;

namespace GIBFramework.Services.Auth;

public sealed record ApiKeyInfo(
    Guid Id,
    Guid TenantId,
    string Name,
    string Prefix,
    IReadOnlyList<string> Roles,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt)
{
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}

public sealed record CreateApiKeyRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Name,
    [Required, MinLength(1)] IReadOnlyList<string> Roles,
    [Range(1, 3650)] int? ExpiresInDays);

public sealed record CreatedApiKey(string Key, ApiKeyInfo ApiKey);

public sealed class ApiKeyRepository(SqlConnectionFactory connections)
{
    public async Task InsertAsync(ApiKeyInfo key, string hash, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            INSERT dbo.ApiKeys (Id, TenantId, Name, Prefix, KeyHash, RolesJson, CreatedBy, CreatedAt, ExpiresAt)
            VALUES (@id, @t, @n, @p, @h, @r, @by, @at, @exp)
            """);
        await cmd.With("@id", key.Id).With("@t", key.TenantId).With("@n", key.Name).With("@p", key.Prefix).With("@h", hash)
            .With("@r", JsonDefaults.Serialize(key.Roles)).With("@by", key.CreatedBy).With("@at", key.CreatedAt).With("@exp", key.ExpiresAt)
            .ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKeyInfo>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT * FROM dbo.ApiKeys WHERE TenantId = @t ORDER BY CreatedAt DESC");
        cmd.With("@t", tenantId);
        var list = new List<ApiKeyInfo>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(Read(r));
        }

        return list;
    }

    public async Task<(ApiKeyInfo Key, string Hash)?> FindByPrefixAsync(string prefix, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT * FROM dbo.ApiKeys WHERE Prefix = @p");
        cmd.With("@p", prefix);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (Read(r), r.Get<string>("KeyHash")) : null;
    }

    public async Task<bool> RevokeAsync(Guid tenantId, Guid id, string user, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("UPDATE dbo.ApiKeys SET RevokedAt = @at, RevokedBy = @by WHERE TenantId = @t AND Id = @id AND RevokedAt IS NULL");
        return await cmd.With("@at", at).With("@by", user).With("@t", tenantId).With("@id", id).ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task TouchAsync(Guid id, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("UPDATE dbo.ApiKeys SET LastUsedAt = @at WHERE Id = @id AND (LastUsedAt IS NULL OR LastUsedAt < DATEADD(MINUTE, -5, @at))");
        await cmd.With("@at", at).With("@id", id).ExecuteNonQueryAsync(ct);
    }

    private static ApiKeyInfo Read(SqlDataReader r) => new(
        r.Get<Guid>("Id"),
        r.Get<Guid>("TenantId"),
        r.Get<string>("Name"),
        r.Get<string>("Prefix"),
        JsonDefaults.Deserialize<List<string>>(r.Get<string>("RolesJson")),
        r.Get<string>("CreatedBy"),
        r.Get<DateTimeOffset>("CreatedAt"),
        r.GetNullableDateTimeOffset("ExpiresAt"),
        r.GetNullableDateTimeOffset("LastUsedAt"),
        r.GetNullableDateTimeOffset("RevokedAt"));
}

public sealed class ApiKeyService(ApiKeyRepository repository, SecurityPolicyService policies, ITenantContext context, IAuditTrail audit, IClock clock, Messaging.UserNotifier notifier)
{
    public const string KeyPrefix = "gfk_";

    public static readonly IReadOnlyList<string> AssignableRoles =
    [
        Roles.ApiClient, Roles.InvoiceCreator, Roles.Accountant, Roles.InvoiceApprover, Roles.InvoiceSigner,
        Roles.IntegratorManager, Roles.ReadOnlyAuditor, Roles.ArchiveAuditor,
    ];

    public Task<IReadOnlyList<ApiKeyInfo>> ListAsync(CancellationToken ct) => repository.ListAsync(context.RequireTenant(), ct);

    public async Task<CreatedApiKey> CreateAsync(CreateApiKeyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = request.Roles.Where(r => !AssignableRoles.Contains(r, StringComparer.Ordinal)).ToList();
        if (invalid.Count > 0)
        {
            throw new ValidationFailedException("API_KEY_ROLES", "API anahtarına bu roller verilemez.", invalid);
        }

        var tenantId = context.RequireTenant();
        if ((await policies.GetAsync(tenantId, ct)).ApiKeyMaxDays is { } maxDays && (request.ExpiresInDays is null || request.ExpiresInDays > maxDays))
        {
            throw new ValidationFailedException("API_KEY_EXPIRY", $"Güvenlik politikası gereği API anahtarı en fazla {maxDays} gün geçerli olabilir.", []);
        }

        var now = clock.UtcNow;
        var prefix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var key = $"{KeyPrefix}{prefix}_{secret}";
        var info = new ApiKeyInfo(Guid.CreateVersion7(now), tenantId, request.Name.Trim(), prefix, [.. request.Roles.Distinct(StringComparer.Ordinal)],
            context.RequireUser(), now, request.ExpiresInDays is { } days ? now.AddDays(days) : null, null, null);
        await repository.InsertAsync(info, Hash(key), ct);
        await audit.AppendSystemAsync(tenantId, new Models.Audit.AuditEntry("API_KEY_CREATED", "ApiKey", info.Id.ToString(), "Success",
            new { info.Name, info.Prefix, info.Roles, by = info.CreatedBy }), null, ct);
        await notifier.ApiKeyCreatedAsync(tenantId, info.Name, info.Roles, info.CreatedBy, ct);
        return new CreatedApiKey(key, info);
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        var revoked = await repository.RevokeAsync(tenantId, id, context.RequireUser(), clock.UtcNow, ct);
        if (revoked)
        {
            await audit.AppendSystemAsync(tenantId, new Models.Audit.AuditEntry("API_KEY_REVOKED", "ApiKey", id.ToString(), "Success",
                new { by = context.UserId }), null, ct);
        }

        return revoked;
    }

    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyRepository repository,
    IClock clock) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || values.ToString() is not { Length: > 0 } key)
        {
            return AuthenticateResult.NoResult();
        }

        var parts = key.Split('_');
        if (parts.Length != 3 || $"{parts[0]}_" != ApiKeyService.KeyPrefix || parts[1].Length != 12)
        {
            return AuthenticateResult.Fail("API anahtarı biçimi geçersiz.");
        }

        if (await repository.FindByPrefixAsync(parts[1], Context.RequestAborted) is not { } found)
        {
            return AuthenticateResult.Fail("API anahtarı bulunamadı.");
        }

        var (info, hash) = found;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ApiKeyService.Hash(key)), Encoding.ASCII.GetBytes(hash)))
        {
            return AuthenticateResult.Fail("API anahtarı geçersiz.");
        }

        var now = clock.UtcNow;
        if (!info.IsActive(now))
        {
            return AuthenticateResult.Fail("API anahtarı iptal edilmiş veya süresi dolmuş.");
        }

        await repository.TouchAsync(info.Id, now, Context.RequestAborted);
        var claims = new List<Claim>
        {
            new(GibFrameworkClaims.Subject, $"api:{info.Name}"),
            new(GibFrameworkClaims.Name, info.Name),
            new(GibFrameworkClaims.TenantId, info.TenantId.ToString()),
            new("api_key_id", info.Id.ToString()),
        };
        claims.AddRange(info.Roles.Select(r => new Claim(GibFrameworkClaims.Role, r)));
        var identity = new ClaimsIdentity(claims, SchemeName, GibFrameworkClaims.Name, GibFrameworkClaims.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
