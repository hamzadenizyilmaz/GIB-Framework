using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.Tenancy;

namespace GIBFramework.DAL.Tenants;

public sealed record SequenceInfo(string DocumentKind, string Prefix, int FiscalYear, long LastValue, DateTimeOffset UpdatedAt);

public interface ITenantRepository
{
    Task CreateAsync(Tenant tenant, CancellationToken cancellationToken);

    Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken);

    Task<IReadOnlyList<SequenceInfo>> SequencesAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed class TenantRepository(SqlConnectionFactory connections) : ITenantRepository
{
    private const int UniqueViolation = 2627;

    public async Task CreateAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("""
            INSERT dbo.Tenants (Id, Name, TaxId, ProfileJson, EFaturaPrefix, EArsivPrefix, IsActive, CreatedAt)
            VALUES (@id, @n, @tax, @p, @ef, @ea, @a, @c)
            """);
        cmd.With("@id", tenant.Id).With("@n", tenant.Name).With("@tax", tenant.Profile.TaxId).WithMax("@p", JsonDefaults.Serialize(tenant.Profile))
            .With("@ef", tenant.EFaturaPrefix).With("@ea", tenant.EArsivPrefix).With("@a", tenant.IsActive).With("@c", tenant.CreatedAt);
        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == UniqueViolation)
        {
            throw new ConflictException("TENANT_EXISTS", $"{tenant.Profile.TaxId} VKN/TCKN'li kiracı zaten kayıtlı.");
        }
    }

    public async Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.Tenants WHERE Id = @id");
        cmd.With("@id", id);
        var list = await ReadAsync(cmd, cancellationToken);
        return list.Count == 0 ? null : list[0];
    }

    public async Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.Tenants ORDER BY Name");
        return await ReadAsync(cmd, cancellationToken);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("""
            UPDATE dbo.Tenants SET Name = @n, ProfileJson = @p, EFaturaPrefix = @ef, EArsivPrefix = @ea WHERE Id = @id
            """);
        cmd.With("@id", tenant.Id).With("@n", tenant.Name).WithMax("@p", JsonDefaults.Serialize(tenant.Profile))
            .With("@ef", tenant.EFaturaPrefix).With("@ea", tenant.EArsivPrefix);
        if (await cmd.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new NotFoundException("Firma bulunamadı.");
        }
    }

    public async Task<IReadOnlyList<SequenceInfo>> SequencesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("""
            SELECT DocumentKind, Prefix, FiscalYear, LastValue, UpdatedAt FROM dbo.DocumentSequences
             WHERE TenantId = @t ORDER BY FiscalYear DESC, DocumentKind
            """);
        cmd.With("@t", tenantId);
        var list = new List<SequenceInfo>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new SequenceInfo(r.GetString(0), r.GetString(1).Trim(), r.GetInt32(2), r.GetInt64(3), r.GetFieldValue<DateTimeOffset>(4)));
        }

        return list;
    }

    private static async Task<List<Tenant>> ReadAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<Tenant>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new Tenant
            {
                Id = r.Get<Guid>("Id"),
                Name = r.Get<string>("Name"),
                Profile = JsonDefaults.Deserialize<InvoiceParty>(r.Get<string>("ProfileJson")),
                EFaturaPrefix = r.Get<string>("EFaturaPrefix"),
                EArsivPrefix = r.Get<string>("EArsivPrefix"),
                IsActive = r.Get<bool>("IsActive"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
            });
        }

        return list;
    }
}
