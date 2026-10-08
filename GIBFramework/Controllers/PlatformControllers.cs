using GIBFramework.DAL.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.DAL.TaxOffices;
using GIBFramework.DAL.Tenants;
using GIBFramework.DTOs;
using GIBFramework.Infrastructure.Background;
using GIBFramework.Infrastructure.Providers;
using GIBFramework.Mapper;
using GIBFramework.Models.Audit;
using GIBFramework.Services.Auth;
using GIBFramework.Services.Incoming;
using GIBFramework.Services.TaxOffices;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/tenants")]
[Authorize]
public sealed class TenantsController(ITenantRepository tenants, IAuditTrail audit, ITenantContext context, IClock clock) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.PlatformAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        if (!TaxIdentifier.TryParse(request.Profile.TaxId, out _, out var error))
        {
            throw new ValidationFailedException("TENANT_TAX_ID", "Kiracı VKN/TCKN geçersiz.", [error.Message]);
        }

        var tenant = request.ToTenant(clock.UtcNow);
        await tenants.CreateAsync(tenant, ct);
        await audit.AppendSystemAsync(tenant.Id, new AuditEntry("TENANT_CREATED", "Tenant", tenant.Id.ToString(), "Success", new { by = context.UserId, tenant.Name }), null, ct);
        return CreatedAtAction(nameof(Current), null, tenant);
    }

    [HttpGet]
    [Authorize(Policy = Policies.PlatformAdmin)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await tenants.ListAsync(ct));

    [HttpPut("current")]
    [Authorize(Policy = Policies.CompanyManage)]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateCompanyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EFaturaPrefix == "TSL" || request.EArsivPrefix == "TSL" || request.EFaturaPrefix == request.EArsivPrefix)
        {
            throw new ValidationFailedException("TENANT_PREFIX", "e-Fatura ve e-Arşiv serileri farklı olmalı ve TSL kullanılamaz.", []);
        }

        var tenant = await tenants.GetAsync(context.RequireTenant(), ct) ?? throw new NotFoundException("Firma bulunamadı.");
        var p = tenant.Profile;
        tenant.Name = request.Name.Trim();
        p.Title = request.Title.Trim();
        p.Regime = request.Regime;
        p.TaxOffice = request.TaxOffice.Trim();
        p.Neighborhood = CustomersController.Clean(request.Neighborhood);
        p.Street = CustomersController.Clean(request.Street);
        p.BuildingNumber = CustomersController.Clean(request.BuildingNumber);
        p.District = CustomersController.Clean(request.District);
        p.City = request.City.Trim();
        p.PostalCode = CustomersController.Clean(request.PostalCode);
        p.Email = CustomersController.Clean(request.Email);
        p.Phone = CustomersController.Clean(request.Phone);
        p.IsEFaturaRegistered = request.IsEFaturaRegistered;
        p.IsEArchiveRegistered = request.IsEArchiveRegistered;
        tenant.EFaturaPrefix = request.EFaturaPrefix;
        tenant.EArsivPrefix = request.EArsivPrefix;
        await tenants.UpdateAsync(tenant, ct);
        await audit.AppendSystemAsync(tenant.Id, new AuditEntry("TENANT_UPDATED", "Tenant", tenant.Id.ToString(), "Success",
            new { by = context.UserId, tenant.Name, tenant.EFaturaPrefix, tenant.EArsivPrefix }), null, ct);
        return Ok(tenant);
    }

    [HttpGet("current/sequences")]
    public async Task<IActionResult> Sequences(CancellationToken ct) => Ok(await tenants.SequencesAsync(context.RequireTenant(), ct));

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct) =>
        await tenants.GetAsync(context.RequireTenant(), ct) is { } tenant ? Ok(tenant) : NotFound();
}

[ApiController]
[Route("api/v1/taxpayers")]
[Authorize]
public sealed class TaxpayersController(ICustomerRepository customers, ITenantContext context) : ControllerBase
{
    [HttpPost("identifiers/validate")]
    public IActionResult Validate([FromBody] TaxIdRequest request) =>
        TaxIdentifier.TryParse(request.Value, out var id, out var error)
            ? Ok(new { valid = true, type = id.Type, masked = id.Masked() })
            : Ok(new { valid = false, error = error.Code, message = error.Message });

    [HttpGet("{taxId}")]
    [Authorize(Policy = Policies.InvoiceCreate)]
    public async Task<IActionResult> Lookup(string taxId, CancellationToken ct)
    {
        if (!TaxIdentifier.TryParse(taxId, out _, out var error))
        {
            throw new ValidationFailedException(error.Code, error.Message, []);
        }

        var card = await customers.GetByTaxIdAsync(context.RequireTenant(), taxId, ct);
        return Ok(new
        {
            taxId,
            inSystem = card is not null,
            isEFaturaRegistered = card is { IsActive: true, IsEFaturaRegistered: true },
            documentType = card is { IsActive: true, IsEFaturaRegistered: true } ? EDocumentType.EFatura : EDocumentType.EArsiv,
            customer = card,
        });
    }
}

[ApiController]
[Route("api/v1/tax-offices")]
[Authorize]
public sealed class TaxOfficesController(ITaxOfficeRepository repository, TaxOfficeImportService imports, IClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] DateOnly? date, [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await repository.SearchAsync(q ?? string.Empty, date ?? clock.TurkeyToday, take, ct));

    [HttpPost("imports")]
    [Authorize(Policy = Policies.TaxOfficeManage)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] Uri sourceUrl, [FromForm] DateOnly? publishedAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var result = await imports.ImportAsync(buffer.ToArray(), file.FileName, sourceUrl, publishedAt, ct);
        return Ok(new { snapshot = result.Stored.Snapshot, diff = result.Stored.Diff, records = result.Stored.Records.Count, result.Warnings });
    }

    [HttpGet("imports/{id:guid}")]
    [Authorize(Policy = Policies.TaxOfficeManage)]
    public async Task<IActionResult> GetImport(Guid id, CancellationToken ct) =>
        await repository.GetSnapshotAsync(id, ct) is { } s ? Ok(s) : NotFound();

    [HttpPost("imports/{id:guid}/approve")]
    [Authorize(Policy = Policies.TaxOfficeManage)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSnapshotRequest request, CancellationToken ct) =>
        Ok(await imports.ApproveAsync(id, request.EffectiveFrom, ct));
}

[ApiController]
[Route("api/v1/incoming-invoices")]
[Authorize(Policy = Policies.IncomingManage)]
public sealed class IncomingInvoicesController(IncomingInvoiceService service) : ControllerBase
{
    [HttpPost]
    [Consumes("application/xml", "text/xml")]
    [RequestSizeLimit(IncomingInvoiceService.MaxBytes)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var incoming = await service.ReceiveAsync(buffer.ToArray(), "Api", ct);
        return CreatedAtAction(nameof(Get), new { id = incoming.Id }, incoming);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 50, CancellationToken ct = default) => Ok(await service.ListAsync(take, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, [FromBody] IncomingDecisionRequest request, CancellationToken ct) =>
        Ok(await service.DecideAsync(id, request.Accept, ct));
}

[ApiController]
[Route("api/v1/audit")]
[Authorize(Policy = Policies.AuditRead)]
public sealed class AuditController(IAuditTrail audit, ITenantContext context) : ControllerBase
{
    [HttpGet("events")]
    public async Task<IActionResult> Events([FromQuery] string? entityId, [FromQuery] int take = 100, [FromQuery] long? before = null, CancellationToken ct = default) =>
        Ok(await audit.ListAsync(context.RequireTenant(), entityId, take, before, ct));

    [HttpGet("verify")]
    public async Task<IActionResult> Verify(CancellationToken ct) => Ok(await audit.VerifyAsync(context.RequireTenant(), ct));
}

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class DocumentsController(ISigningCertificateProvider certificates, IClock clock) : ControllerBase
{
    [HttpGet("document-numbers/{value}")]
    public IActionResult ParseNumber(string value) =>
        DocumentNumber.TryParse(value, out var n)
            ? Ok(new { valid = true, prefix = n.Value.Prefix, year = n.Value.Year, sequence = n.Value.Sequence })
            : Ok(new { valid = false });

    [HttpGet("document-lifecycle")]
    public IActionResult Lifecycle() =>
        Ok(Enum.GetValues<DocumentStatus>().ToDictionary(s => s.ToString(), DocumentLifecycle.AllowedFrom));

    [HttpGet("certificates/current")]
    public IActionResult Certificate()
    {
        var cert = certificates.GetSigningCertificate();
        return Ok(new
        {
            mode = certificates.Mode,
            cert.Subject,
            cert.Issuer,
            cert.SerialNumber,
            cert.Thumbprint,
            validFrom = cert.NotBefore,
            validTo = cert.NotAfter,
            daysLeft = CertificateExpiryMonitor.DaysLeft(cert.NotAfter, clock.UtcNow),
        });
    }
}

[ApiController]
[Route("api/v1/dev")]
[AllowAnonymous]
public sealed class DevAuthController(TokenService tokens, JwtOptions options, IHostEnvironment environment) : ControllerBase
{
    [HttpPost("token")]
    public IActionResult Token([FromBody] DevTokenRequest request)
    {
        if (!environment.IsDevelopment() || !options.DevTokenEnabled)
        {
            return NotFound();
        }

        return Ok(new DevTokenResponse(tokens.Issue(request.UserId, request.TenantId, request.Roles), "Bearer", request.Roles));
    }
}
