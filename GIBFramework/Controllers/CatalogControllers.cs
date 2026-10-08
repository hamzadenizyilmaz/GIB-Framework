using System.Globalization;
using System.Numerics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.DAL.Catalog;
using GIBFramework.DTOs;
using GIBFramework.Models.Catalog;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize(Policy = Policies.InvoiceRead)]
public sealed class CustomersController(ICustomerRepository customers, ITenantContext context, IClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<Customer>> List([FromQuery] string? q, [FromQuery] int take = 500, CancellationToken ct = default) =>
        await customers.SearchAsync(context.RequireTenant(), q, take, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Customer>> Get(Guid id, CancellationToken ct) =>
        await customers.GetAsync(context.RequireTenant(), id, ct) is { } c ? c : NotFound();

    [HttpGet("by-tax-id/{taxId}")]
    public async Task<ActionResult<Customer>> ByTaxId(string taxId, CancellationToken ct) =>
        await customers.GetByTaxIdAsync(context.RequireTenant(), taxId, ct) is { } c ? c : NotFound();

    [HttpPost]
    [Authorize(Policy = Policies.CatalogManage)]
    public async Task<ActionResult<Customer>> Save([FromBody] CustomerRequest request, CancellationToken ct)
    {
        var customer = await Build(request, null, ct);
        await customers.UpsertAsync(customer, ct);
        return Ok(customer);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CatalogManage)]
    public async Task<ActionResult<Customer>> Update(Guid id, [FromBody] CustomerRequest request, CancellationToken ct)
    {
        if (await customers.GetAsync(context.RequireTenant(), id, ct) is not { } existing)
        {
            return NotFound();
        }

        if (existing.TaxId != request.TaxId)
        {
            throw new ValidationFailedException("CUSTOMER_TAX_ID_CHANGE", "Cari kartın VKN/TCKN'si değiştirilemez; yeni kart oluşturun.", []);
        }

        var customer = await Build(request, existing, ct);
        await customers.UpsertAsync(customer, ct);
        return Ok(customer);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CatalogManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await customers.DeleteAsync(context.RequireTenant(), id, ct) ? NoContent() : NotFound();

    private async Task<Customer> Build(CustomerRequest r, Customer? existing, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (!TaxIdentifier.TryParse(r.TaxId, out _, out var error))
        {
            throw new ValidationFailedException(error.Code, error.Message, []);
        }

        if (r.TaxId == TaxIdentifier.AnonymousConsumer)
        {
            throw new ValidationFailedException("CUSTOMER_ANONYMOUS", "11111111111 nihai tüketici numarasıdır; bu numarayla cari kart açılmaz.", []);
        }

        var tenantId = context.RequireTenant();
        existing ??= await customers.GetByTaxIdAsync(tenantId, r.TaxId, ct);
        var now = clock.UtcNow;
        return new Customer
        {
            Id = existing?.Id ?? Guid.CreateVersion7(now),
            TenantId = tenantId,
            TaxId = r.TaxId,
            Title = r.Title.Trim(),
            FirstName = Clean(r.FirstName),
            FamilyName = Clean(r.FamilyName),
            Regime = r.Regime,
            TaxOffice = Clean(r.TaxOffice),
            ProvinceName = Clean(r.ProvinceName),
            DistrictName = Clean(r.DistrictName),
            NeighborhoodName = Clean(r.NeighborhoodName),
            Street = Clean(r.Street),
            BuildingNumber = Clean(r.BuildingNumber),
            PostalCode = Clean(r.PostalCode),
            Country = string.IsNullOrWhiteSpace(r.Country) ? "Türkiye" : r.Country.Trim(),
            Email = Clean(r.Email),
            Phone = Clean(r.Phone),
            IsEFaturaRegistered = r.IsEFaturaRegistered,
            EFaturaAlias = Clean(r.EFaturaAlias),
            Notes = Clean(r.Notes),
            IsActive = r.IsActive,
            CreatedBy = existing?.CreatedBy ?? context.RequireUser(),
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now,
        };
    }

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[ApiController]
[Route("api/v1/products")]
[Authorize(Policy = Policies.InvoiceRead)]
public sealed class ProductsController(IProductRepository products, ITenantContext context, IClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<Product>> List([FromQuery] string? q, [FromQuery] bool activeOnly = false, [FromQuery] int take = 1000, CancellationToken ct = default) =>
        await products.SearchAsync(context.RequireTenant(), q, activeOnly, take, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> Get(Guid id, CancellationToken ct) =>
        await products.GetAsync(context.RequireTenant(), id, ct) is { } p ? p : NotFound();

    [HttpPost]
    [Authorize(Policy = Policies.CatalogManage)]
    public async Task<ActionResult<Product>> Create([FromBody] ProductRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var product = Build(request, Guid.CreateVersion7(now), now, now);
        await products.SaveAsync(product, isNew: true, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CatalogManage)]
    public async Task<ActionResult<Product>> Update(Guid id, [FromBody] ProductRequest request, CancellationToken ct)
    {
        if (await products.GetAsync(context.RequireTenant(), id, ct) is not { } existing)
        {
            return NotFound();
        }

        var product = Build(request, id, existing.CreatedAt, clock.UtcNow);
        await products.SaveAsync(product, isNew: false, ct);
        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CatalogManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await products.DeleteAsync(context.RequireTenant(), id, ct) ? NoContent() : NotFound();

    private Product Build(ProductRequest r, Guid id, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.VatRate == 0 && string.IsNullOrWhiteSpace(r.VatExemptionCode))
        {
            throw new ValidationFailedException("PRODUCT_EXEMPTION", "KDV oranı %0 olan ürün için istisna kodu seçilmelidir.", []);
        }

        return new Product
        {
            Id = id,
            TenantId = context.RequireTenant(),
            Code = CustomersController.Clean(r.Code),
            Name = r.Name.Trim(),
            Description = CustomersController.Clean(r.Description),
            UnitCode = r.UnitCode.Trim().ToUpperInvariant(),
            UnitPrice = r.UnitPrice,
            Currency = r.Currency.Trim().ToUpperInvariant(),
            VatRate = r.VatRate,
            VatExemptionCode = r.VatRate == 0 ? CustomersController.Clean(r.VatExemptionCode) : null,
            WithholdingCode = CustomersController.Clean(r.WithholdingCode),
            IsActive = r.IsActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }
}

[ApiController]
[Route("api/v1/settings")]
[Authorize(Policy = Policies.InvoiceRead)]
public sealed class SettingsController(ITenantSettingsRepository settings, ITenantContext context, IClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<TenantSettings> Get(CancellationToken ct) => Public(await settings.GetAsync(context.RequireTenant(), ct));

    private static TenantSettings Public(TenantSettings value)
    {
        value.Messaging = new();
        return value;
    }

    [HttpPut]
    [Authorize(Policy = Policies.SettingsManage)]
    public async Task<TenantSettings> Save([FromBody] TenantSettingsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        var value = new TenantSettings
        {
            Defaults = request.Defaults,
            NoteTemplates = [.. request.NoteTemplates.Where(n => !string.IsNullOrWhiteSpace(n.Text)).Select(n => new NoteTemplate
            {
                Id = n.Id == Guid.Empty ? Guid.NewGuid() : n.Id,
                Title = string.IsNullOrWhiteSpace(n.Title) ? n.Text.Trim()[..Math.Min(40, n.Text.Trim().Length)] : n.Title.Trim(),
                Text = n.Text.Trim(),
                AutoAdd = n.AutoAdd,
            })],
            BankAccounts = [.. request.BankAccounts.Select(b => new BankAccount
            {
                Id = b.Id == Guid.Empty ? Guid.NewGuid() : b.Id,
                BankName = b.BankName.Trim(),
                Branch = CustomersController.Clean(b.Branch),
                Iban = new string([.. b.Iban.Where(c => !char.IsWhiteSpace(c))]).ToUpperInvariant(),
                AccountHolder = b.AccountHolder.Trim(),
                Currency = string.IsNullOrWhiteSpace(b.Currency) ? "TRY" : b.Currency.Trim().ToUpperInvariant(),
                AddToNotes = b.AddToNotes,
            })],
        };

        if (value.NoteTemplates.Any(n => n.Text.Length > 500))
        {
            errors.Add("Not şablonu en fazla 500 karakter olabilir.");
        }

        foreach (var bank in value.BankAccounts)
        {
            if (string.IsNullOrWhiteSpace(bank.BankName) || string.IsNullOrWhiteSpace(bank.AccountHolder))
            {
                errors.Add("Banka hesabında banka adı ve hesap sahibi zorunludur.");
            }

            if (!IsValidIban(bank.Iban))
            {
                errors.Add($"Geçersiz IBAN: {bank.Iban}");
            }
        }

        if (value.Defaults.VatRate is < 0 or > 100 || value.Defaults.PaymentDueDays is < 0 or > 3650)
        {
            errors.Add("Varsayılan KDV oranı veya vade günü geçersiz.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("SETTINGS_INVALID", "Ayarlar kaydedilemedi.", errors);
        }

        var existing = await settings.GetAsync(context.RequireTenant(), ct);
        value.Security = existing.Security;
        value.Messaging = existing.Messaging;
        await settings.SaveAsync(context.RequireTenant(), value, context.RequireUser(), clock.UtcNow, ct);
        return Public(await settings.GetAsync(context.RequireTenant(), ct));
    }

    internal static bool IsValidIban(string iban)
    {
        if (iban.Length is < 15 or > 34 || !iban.All(char.IsAsciiLetterOrDigit) || !char.IsAsciiLetter(iban[0]) || !char.IsAsciiLetter(iban[1]))
        {
            return false;
        }

        if (iban.StartsWith("TR", StringComparison.Ordinal) && iban.Length != 26)
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(CultureInfo.InvariantCulture)));
        return BigInteger.Parse(digits, CultureInfo.InvariantCulture) % 97 == 1;
    }
}
