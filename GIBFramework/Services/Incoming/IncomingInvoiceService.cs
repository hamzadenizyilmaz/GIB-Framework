using System.Xml;
using GIBFramework.DAL;
using GIBFramework.DAL.Incoming;
using GIBFramework.DAL.Tenants;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Evidence;
using GIBFramework.Models.Incoming;
using GIBFramework.Services.Ubl;

namespace GIBFramework.Services.Incoming;

public sealed class IncomingInvoiceService(
    SqlConnectionFactory connections,
    IIncomingInvoiceRepository repository,
    ITenantRepository tenants,
    IXmlSigner signer,
    UblValidator validator,
    IEvidenceVault vault,
    IAuditTrail audit,
    ITenantContext context,
    IClock clock)
{
    public const int MaxBytes = 20 * 1024 * 1024;

    public async Task<IncomingInvoice> ReceiveAsync(byte[] xml, string via, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var tenantId = context.RequireTenant();
        if (xml.Length is 0 or > MaxBytes)
        {
            throw new ValidationFailedException("INCOMING_SIZE", $"XML boyutu 1 bayt ile {MaxBytes / 1024 / 1024} MB arasında olmalıdır.", []);
        }

        XmlDocument doc;
        try
        {
            doc = UblInvoiceReader.LoadSecure(new MemoryStream(xml));
        }
        catch (XmlException ex)
        {
            throw new ValidationFailedException("INCOMING_XML", "XML ayrıştırılamadı.", [ex.Message]);
        }

        UblInvoiceSummary summary;
        try
        {
            summary = UblInvoiceReader.Read(doc);
        }
        catch (DomainException ex)
        {
            throw new ValidationFailedException(ex.Code, ex.Message, []);
        }

        var tenant = await tenants.GetAsync(tenantId, ct) ?? throw new NotFoundException("Kiracı bulunamadı.");
        var signature = signer.Verify(doc);
        var issues = validator.Validate(doc, requireSignature: true)
            .Where(i => i.Severity == FindingSeverity.Error)
            .Select(i => $"{i.Code}: {i.Message}")
            .ToList();
        if (signature.IsPresent && !signature.IsValid)
        {
            issues.Add("SIGNATURE: " + signature.Problem);
        }

        if (summary.Customer.TaxId != tenant.Profile.TaxId)
        {
            issues.Add($"CUSTOMER-MISMATCH: Belgenin alıcısı ({summary.Customer.TaxId}) bu kiracı ({tenant.Profile.TaxId}) değil.");
        }

        if (!Guid.TryParse(summary.Uuid, out var ettn))
        {
            throw new ValidationFailedException("INCOMING_UUID", "Belgede geçerli ETTN yok; kayıt yapılamaz.", issues);
        }

        var incoming = new IncomingInvoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Uuid = ettn,
            DocumentNumber = Truncate(summary.Id, 16) ?? string.Empty,
            Profile = Truncate(summary.ProfileId, 20) ?? string.Empty,
            SupplierTaxId = Truncate(summary.Supplier.TaxId, 11) ?? string.Empty,
            SupplierTitle = Truncate(summary.Supplier.Name ?? $"{summary.Supplier.FirstName} {summary.Supplier.FamilyName}".Trim(), 250) ?? string.Empty,
            CustomerTaxId = Truncate(summary.Customer.TaxId, 11) ?? string.Empty,
            IssueDate = DateOnly.TryParseExact(summary.IssueDate, "yyyy-MM-dd", out var d) ? d : clock.TurkeyToday,
            PayableAmount = summary.PayableAmount ?? 0,
            Currency = Truncate(summary.Currency, 3) ?? "TRY",
            Status = issues.Count == 0 ? IncomingStatus.Validated : IncomingStatus.Rejected,
            SignaturePresent = signature.IsPresent,
            SignatureValid = signature.IsValid,
            Issues = issues,
            XmlSha256 = Hashing.Sha256Hex(xml),
            ReceivedVia = via,
            ReceivedAt = clock.UtcNow,
        };

        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        await repository.InsertAsync(incoming, scope, ct);
        var manifest = await vault.StoreAsync(tenantId, incoming.Id, incoming.IssueDate.Year, "received",
            [
                new EvidenceItem("incoming.xml", "application/xml", xml),
                new EvidenceItem("verification.json", "application/json", JsonDefaults.SerializeToUtf8(new { signature, issues }, indented: true)),
            ],
            RetentionClass.VukDocument, ct);
        await audit.AppendAsync(new AuditEntry("INCOMING_RECEIVED", "IncomingInvoice", incoming.Id.ToString(), issues.Count == 0 ? "Success" : "Failure",
            new { incoming.Uuid, incoming.DocumentNumber, incoming.SupplierTaxId, manifest.ManifestHash, issues = issues.Count }), scope, ct);
        await scope.Transaction.CommitAsync(ct);
        return incoming;
    }

    public async Task<IncomingInvoice> DecideAsync(Guid id, bool accept, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var invoice = await repository.GetAsync(conn, tenantId, id, ct) ?? throw new NotFoundException("Gelen fatura bulunamadı.");
        if (invoice.Status != IncomingStatus.Validated)
        {
            throw new DomainException("INCOMING_STATE", $"Yalnızca doğrulanmış gelen faturalar için karar verilebilir (mevcut: {invoice.Status}).");
        }

        invoice.Status = accept ? IncomingStatus.Accepted : IncomingStatus.Declined;
        invoice.DecidedBy = context.RequireUser();
        invoice.DecidedAt = clock.UtcNow;
        var scope = await connections.BeginAsync(conn, ct);
        await repository.UpdateDecisionAsync(invoice, scope, ct);
        await audit.AppendAsync(new AuditEntry(accept ? "INCOMING_ACCEPTED" : "INCOMING_DECLINED", "IncomingInvoice", id.ToString()), scope, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<IncomingInvoice> GetAsync(Guid id, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        return await repository.GetAsync(conn, tenantId, id, ct) ?? throw new NotFoundException("Gelen fatura bulunamadı.");
    }

    public async Task<IReadOnlyList<IncomingInvoice>> ListAsync(int take, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        return await repository.ListAsync(conn, tenantId, take, ct);
    }

    private static string? Truncate(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
