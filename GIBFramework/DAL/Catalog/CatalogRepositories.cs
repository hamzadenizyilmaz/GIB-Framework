using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.Catalog;

namespace GIBFramework.DAL.Catalog;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> SearchAsync(Guid tenantId, string? text, int take, CancellationToken cancellationToken);

    Task<Customer?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<Customer?> GetByTaxIdAsync(Guid tenantId, string taxId, CancellationToken cancellationToken);

    Task UpsertAsync(Customer customer, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> SearchAsync(Guid tenantId, string? text, bool activeOnly, int take, CancellationToken cancellationToken);

    Task<Product?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task SaveAsync(Product product, bool isNew, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}

public interface ITenantSettingsRepository
{
    Task<TenantSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken);

    Task SaveAsync(Guid tenantId, TenantSettings settings, string user, DateTimeOffset at, CancellationToken cancellationToken);
}

internal static class Like
{
    public static string? Pattern(string? text) => string.IsNullOrWhiteSpace(text)
        ? null
        : "%" + text.Trim().Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal) + "%";
}

public sealed class CustomerRepository(SqlConnectionFactory connections) : ICustomerRepository
{
    public async Task<IReadOnlyList<Customer>> SearchAsync(Guid tenantId, string? text, int take, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("""
            SELECT TOP (@take) * FROM dbo.Customers
             WHERE TenantId = @t AND (@q IS NULL OR Title LIKE @q OR TaxId LIKE @q OR Email LIKE @q)
             ORDER BY Title
            """);
        cmd.With("@t", tenantId).With("@q", Like.Pattern(text)).With("@take", Math.Clamp(take, 1, 5000));
        return await ReadAsync(cmd, cancellationToken);
    }

    public async Task<Customer?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.Customers WHERE TenantId = @t AND Id = @id");
        cmd.With("@t", tenantId).With("@id", id);
        return (await ReadAsync(cmd, cancellationToken)).SingleOrDefault();
    }

    public async Task<Customer?> GetByTaxIdAsync(Guid tenantId, string taxId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.Customers WHERE TenantId = @t AND TaxId = @tax");
        cmd.With("@t", tenantId).With("@tax", taxId);
        return (await ReadAsync(cmd, cancellationToken)).SingleOrDefault();
    }

    public async Task UpsertAsync(Customer c, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(c);
        await using var connection = await connections.OpenForTenantAsync(c.TenantId, cancellationToken);
        await using var cmd = connection.Command("""
            MERGE dbo.Customers WITH (HOLDLOCK) AS t
            USING (SELECT @t AS TenantId, @tax AS TaxId) AS s ON t.TenantId = s.TenantId AND t.TaxId = s.TaxId
            WHEN MATCHED THEN UPDATE SET
                Title = @title, FirstName = @fn, FamilyName = @ln, Regime = @reg, TaxOffice = @vd, ProvinceName = @il, DistrictName = @ilce,
                NeighborhoodName = @mah, Street = @street, BuildingNumber = @bno, PostalCode = @pk, Country = @country, Email = @mail,
                Phone = @tel, IsEFaturaRegistered = @ef, EFaturaAlias = @alias, Notes = @notes, IsActive = @active, UpdatedAt = @u
            WHEN NOT MATCHED THEN INSERT
                (Id, TenantId, TaxId, Title, FirstName, FamilyName, Regime, TaxOffice, ProvinceName, DistrictName, NeighborhoodName, Street,
                 BuildingNumber, PostalCode, Country, Email, Phone, IsEFaturaRegistered, EFaturaAlias, Notes, IsActive, CreatedBy, CreatedAt, UpdatedAt)
            VALUES (@id, @t, @tax, @title, @fn, @ln, @reg, @vd, @il, @ilce, @mah, @street, @bno, @pk, @country, @mail, @tel, @ef, @alias, @notes,
                    @active, @by, @c, @u)
            OUTPUT inserted.Id;
            """);
        cmd.With("@id", c.Id).With("@t", c.TenantId).With("@tax", c.TaxId).With("@title", c.Title).With("@fn", c.FirstName).With("@ln", c.FamilyName)
            .With("@reg", c.Regime.ToString()).With("@vd", c.TaxOffice).With("@il", c.ProvinceName).With("@ilce", c.DistrictName)
            .With("@mah", c.NeighborhoodName).With("@street", c.Street).With("@bno", c.BuildingNumber).With("@pk", c.PostalCode)
            .With("@country", c.Country).With("@mail", c.Email).With("@tel", c.Phone).With("@ef", c.IsEFaturaRegistered)
            .With("@alias", c.EFaturaAlias).With("@notes", c.Notes).With("@active", c.IsActive).With("@by", c.CreatedBy)
            .With("@c", c.CreatedAt).With("@u", c.UpdatedAt);
        c.Id = (Guid)(await cmd.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("DELETE dbo.Customers WHERE TenantId = @t AND Id = @id");
        return await cmd.With("@t", tenantId).With("@id", id).ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<IReadOnlyList<Customer>> ReadAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<Customer>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new Customer
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.Get<Guid>("TenantId"),
                TaxId = r.Get<string>("TaxId"),
                Title = r.Get<string>("Title"),
                FirstName = r.GetNullableString("FirstName"),
                FamilyName = r.GetNullableString("FamilyName"),
                Regime = Enum.TryParse<BookkeepingRegime>(r.Get<string>("Regime"), out var regime) ? regime : BookkeepingRegime.Unknown,
                TaxOffice = r.GetNullableString("TaxOffice"),
                ProvinceName = r.GetNullableString("ProvinceName"),
                DistrictName = r.GetNullableString("DistrictName"),
                NeighborhoodName = r.GetNullableString("NeighborhoodName"),
                Street = r.GetNullableString("Street"),
                BuildingNumber = r.GetNullableString("BuildingNumber"),
                PostalCode = r.GetNullableString("PostalCode"),
                Country = r.Get<string>("Country"),
                Email = r.GetNullableString("Email"),
                Phone = r.GetNullableString("Phone"),
                IsEFaturaRegistered = r.Get<bool>("IsEFaturaRegistered"),
                EFaturaAlias = r.GetNullableString("EFaturaAlias"),
                Notes = r.GetNullableString("Notes"),
                IsActive = r.Get<bool>("IsActive"),
                CreatedBy = r.Get<string>("CreatedBy"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
                UpdatedAt = r.Get<DateTimeOffset>("UpdatedAt"),
            });
        }

        return list;
    }
}

public sealed class ProductRepository(SqlConnectionFactory connections) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> SearchAsync(Guid tenantId, string? text, bool activeOnly, int take, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("""
            SELECT TOP (@take) * FROM dbo.Products
             WHERE TenantId = @t AND (@active = 0 OR IsActive = 1) AND (@q IS NULL OR Name LIKE @q OR Code LIKE @q)
             ORDER BY Name
            """);
        cmd.With("@t", tenantId).With("@active", activeOnly).With("@q", Like.Pattern(text)).With("@take", Math.Clamp(take, 1, 5000));
        return await ReadAsync(cmd, cancellationToken);
    }

    public async Task<Product?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.Products WHERE TenantId = @t AND Id = @id");
        cmd.With("@t", tenantId).With("@id", id);
        return (await ReadAsync(cmd, cancellationToken)).SingleOrDefault();
    }

    public async Task SaveAsync(Product p, bool isNew, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(p);
        await using var connection = await connections.OpenForTenantAsync(p.TenantId, cancellationToken);
        await using var cmd = connection.Command(isNew
            ? """
              INSERT dbo.Products (Id, TenantId, Code, Name, Description, UnitCode, UnitPrice, Currency, VatRate, VatExemptionCode, WithholdingCode,
                                   IsActive, CreatedAt, UpdatedAt)
              VALUES (@id, @t, @code, @name, @desc, @unit, @price, @cur, @vat, @ex, @wh, @active, @c, @u)
              """
            : """
              UPDATE dbo.Products SET Code = @code, Name = @name, Description = @desc, UnitCode = @unit, UnitPrice = @price, Currency = @cur,
                     VatRate = @vat, VatExemptionCode = @ex, WithholdingCode = @wh, IsActive = @active, UpdatedAt = @u
               WHERE TenantId = @t AND Id = @id
              """);
        cmd.With("@id", p.Id).With("@t", p.TenantId).With("@code", p.Code).With("@name", p.Name).With("@desc", p.Description)
            .With("@unit", p.UnitCode).WithDecimal("@price", p.UnitPrice).With("@cur", p.Currency).WithDecimal("@vat", p.VatRate)
            .With("@ex", p.VatExemptionCode).With("@wh", p.WithholdingCode).With("@active", p.IsActive).With("@c", p.CreatedAt).With("@u", p.UpdatedAt);
        if (await cmd.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new NotFoundException("Ürün bulunamadı.");
        }
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("DELETE dbo.Products WHERE TenantId = @t AND Id = @id");
        return await cmd.With("@t", tenantId).With("@id", id).ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<IReadOnlyList<Product>> ReadAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<Product>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new Product
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.Get<Guid>("TenantId"),
                Code = r.GetNullableString("Code"),
                Name = r.Get<string>("Name"),
                Description = r.GetNullableString("Description"),
                UnitCode = r.Get<string>("UnitCode"),
                UnitPrice = r.Get<decimal>("UnitPrice"),
                Currency = r.Get<string>("Currency"),
                VatRate = r.Get<decimal>("VatRate"),
                VatExemptionCode = r.GetNullableString("VatExemptionCode"),
                WithholdingCode = r.GetNullableString("WithholdingCode"),
                IsActive = r.Get<bool>("IsActive"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
                UpdatedAt = r.Get<DateTimeOffset>("UpdatedAt"),
            });
        }

        return list;
    }
}

public sealed class TenantSettingsRepository(SqlConnectionFactory connections) : ITenantSettingsRepository
{
    public async Task<TenantSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("SELECT SettingsJson, UpdatedBy, UpdatedAt FROM dbo.TenantSettings WHERE TenantId = @t");
        cmd.With("@t", tenantId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await r.ReadAsync(cancellationToken))
        {
            return new TenantSettings();
        }

        var settings = JsonDefaults.Deserialize<TenantSettings>(r.Get<string>("SettingsJson"));
        settings.UpdatedBy = r.Get<string>("UpdatedBy");
        settings.UpdatedAt = r.Get<DateTimeOffset>("UpdatedAt");
        return settings;
    }

    public async Task SaveAsync(Guid tenantId, TenantSettings settings, string user, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("""
            MERGE dbo.TenantSettings WITH (HOLDLOCK) AS t
            USING (SELECT @t AS TenantId) AS s ON t.TenantId = s.TenantId
            WHEN MATCHED THEN UPDATE SET SettingsJson = @json, UpdatedBy = @by, UpdatedAt = @at
            WHEN NOT MATCHED THEN INSERT (TenantId, SettingsJson, UpdatedBy, UpdatedAt) VALUES (@t, @json, @by, @at);
            """);
        cmd.With("@t", tenantId).WithMax("@json", JsonDefaults.Serialize(settings)).With("@by", user).With("@at", at);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
