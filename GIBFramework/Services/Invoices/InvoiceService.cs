using System.Text;
using System.Xml;
using GIBFramework.DAL;
using GIBFramework.DAL.Catalog;
using GIBFramework.DAL.Invoices;
using GIBFramework.DAL.Tenants;
using GIBFramework.Infrastructure.Providers;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Evidence;
using GIBFramework.Services.Compliance;
using GIBFramework.Services.Qr;
using GIBFramework.Services.Tax;
using GIBFramework.Services.Ubl;

namespace GIBFramework.Services.Invoices;

public sealed record Actor(string UserId, bool IsSystem)
{
    public static Actor System { get; } = new("system", true);
}

public sealed record CustomerCardOptions(bool Save, bool? IsEFaturaRegistered);

public sealed partial class InvoiceService(
    SqlConnectionFactory connections,
    IInvoiceRepository invoices,
    ITenantRepository tenants,
    IDocumentNumberAllocator numbers,
    IAuditTrail audit,
    ComplianceEngine compliance,
    TaxEngine taxes,
    UblInvoiceWriter ublWriter,
    UblValidator ublValidator,
    QrCodeService qr,
    IXmlSigner signer,
    IEvidenceVault vault,
    ProviderRegistry providers,
    ICustomerRepository customers,
    DAL.Messaging.OutboxEventRepository events,
    ITenantContext context,
    IClock clock,
    WorkflowOptions workflow,
    ProviderOptions providerOptions,
    WorkerOptions workerOptions,
    ILogger<InvoiceService> logger)
{
    private const string SignedStage = "signed";
    private const string DraftSeriesKind = "Draft";
    private const string DraftSeriesPrefix = "TSL";

    public async Task<(Invoice Invoice, bool Created)> CreateDraftAsync(Invoice draft, string? idempotencyKey, CancellationToken ct, CustomerCardOptions? customerCard = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var (tenantId, actor) = Who();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
        {
            throw new ValidationFailedException("IDEMPOTENCY_KEY_REQUIRED", $"'{GibFrameworkHeaders.IdempotencyKey}' başlığı zorunludur (en fazla 100 karakter).", []);
        }

        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        if (await invoices.GetByIdempotencyKeyAsync(conn, tenantId, idempotencyKey, ct) is { } existing)
        {
            return (existing, false);
        }

        var tenant = await tenants.GetAsync(tenantId, ct) ?? throw new NotFoundException("Kiracı bulunamadı.");
        if (!tenant.IsActive)
        {
            throw new ForbiddenOperationException("TENANT_INACTIVE", "Kiracı pasif durumda.");
        }

        if (!TaxIdentifier.TryParse(draft.Customer.TaxId, out _, out var idError))
        {
            throw new ValidationFailedException("CUSTOMER_TAX_ID", "Alıcı VKN/TCKN geçersiz.", [idError.Message]);
        }

        var card = await customers.GetByTaxIdAsync(tenantId, draft.Customer.TaxId, ct);
        if (customerCard is { Save: true } && draft.Customer.TaxId != TaxIdentifier.AnonymousConsumer)
        {
            card = await SaveCustomerCardAsync(tenantId, actor, draft.Customer, card, customerCard.IsEFaturaRegistered, ct);
        }

        draft.Customer.IsEFaturaRegistered = card is { IsActive: true, IsEFaturaRegistered: true };
        draft.Customer.Alias ??= draft.Customer.IsEFaturaRegistered ? card!.EFaturaAlias : null;
        draft.Supplier = JsonDefaults.Deserialize<InvoiceParty>(JsonDefaults.Serialize(tenant.Profile));

        var now = clock.UtcNow;
        draft.Id = Guid.CreateVersion7(now);
        draft.TenantId = tenantId;
        draft.Uuid = Guid.NewGuid();
        draft.Status = DocumentStatus.Draft;
        draft.IdempotencyKey = idempotencyKey;
        draft.CreatedBy = actor.UserId;
        draft.CreatedAt = now;
        draft.UpdatedAt = now;
        if (draft.IssueDate == default)
        {
            draft.IssueDate = clock.TurkeyToday;
        }

        if (draft.IssueTime == default)
        {
            var local = clock.TurkeyNow;
            draft.IssueTime = new TimeOnly(local.Hour, local.Minute, local.Second);
        }

        var taxIssues = taxes.Calculate(draft);
        if (taxIssues.Count > 0)
        {
            throw new ValidationFailedException("INVOICE_TAX", "Vergi hesaplaması doğrulanamadı.", [.. taxIssues.Select(i => $"{i.Code}: {i.Message}")]);
        }

        ApplyDecision(draft, compliance.Evaluate(draft.ToComplianceContext()));
        if (draft.DocumentType == EDocumentType.PaperInvoice)
        {
            throw new ValidationFailedException(
                "INVOICE_PAPER",
                "Bu işlem için elektronik belge zorunluluğu doğmuyor ve satıcı e-Arşiv kullanıcısı değil; GIB Framework yalnızca e-Belge düzenler.",
                draft.Decision!.Explanation);
        }

        try
        {
            var scope = await connections.BeginAsync(conn, ct);
            var draftNumber = await numbers.AllocateAsync(new DocumentSeriesKey(tenantId, DraftSeriesKind, DraftSeriesPrefix, draft.IssueDate.Year), conn, scope.Transaction, ct);
            draft.DraftNumber = draftNumber.Value;
            await invoices.InsertAsync(draft, scope, ct);
            await invoices.AddHistoryAsync(scope, draft, null, actor.UserId, "Taslak oluşturuldu", ct);
            await PublishAsync(scope, draft, InvoiceEvents.Created, null, actor, null, ct);
            await AuditAsync(scope, tenantId, actor, "INVOICE_CREATED", draft, new { draft.DocumentType, draft.Totals.PayableAmount, decision = draft.Decision!.DecisionId }, ct);
            await scope.Transaction.CommitAsync(ct);
            return (draft, true);
        }
        catch (ConflictException ex) when (ex.Code == "INVOICE_DUPLICATE")
        {
            await conn.DisposeAsync();
            await using var again = await connections.OpenForTenantAsync(tenantId, ct);
            var winner = await invoices.GetByIdempotencyKeyAsync(again, tenantId, idempotencyKey, ct);
            if (winner is null)
            {
                throw;
            }

            return (winner, false);
        }
    }

    public async Task<Invoice> SubmitAsync(Guid id, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        Require(invoice, DocumentStatus.Draft);

        await TransitionAsync(scope, invoice, DocumentStatus.Validating, actor, null, ct);
        var problems = taxes.Calculate(invoice).Select(i => $"{i.Code}: {i.Message}").ToList();
        ApplyDecision(invoice, compliance.Evaluate(invoice.ToComplianceContext()));
        var decision = invoice.Decision!;
        problems.AddRange(decision.Findings.Where(f => f.Blocking).Select(f => $"{f.Code}: {f.Message}"));
        if (invoice.DocumentType is EDocumentType.Undetermined or EDocumentType.PaperInvoice)
        {
            problems.Add($"COMPLIANCE-DOCUMENT-TYPE: Belge türü '{invoice.DocumentType}' ile düzenlenemez.");
        }

        if (problems.Count > 0)
        {
            invoice.LastError = string.Join(" | ", problems);
            await TransitionAsync(scope, invoice, DocumentStatus.Draft, actor, "Doğrulama başarısız", ct);
            await AuditAsync(scope, tenantId, actor, "INVOICE_VALIDATION_FAILED", invoice, new { problems }, ct, "Failure");
            await scope.Transaction.CommitAsync(ct);
            throw new ValidationFailedException("INVOICE_VALIDATION", "Fatura doğrulamadan geçemedi; taslağa geri alındı.", problems);
        }

        invoice.LastError = null;
        await TransitionAsync(scope, invoice, DocumentStatus.Validated, actor, $"Uyum kararı: {decision.Validation}", ct);
        await TransitionAsync(scope, invoice, DocumentStatus.AwaitingApproval, actor, null, ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_SUBMITTED", invoice, new { decision.DocumentType, decision.Validation, decision.RuleVersions }, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> ApproveAsync(Guid id, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        Require(invoice, DocumentStatus.AwaitingApproval);

        if (workflow.RequireMakerChecker
            && string.Equals(invoice.CreatedBy, actor.UserId, StringComparison.OrdinalIgnoreCase)
            && !context.Roles.Any(r => workflow.SelfApprovalRoles.Contains(r, StringComparer.Ordinal)))
        {
            throw new ForbiddenOperationException("MAKER_CHECKER", "Faturayı oluşturan kullanıcı aynı faturayı onaylayamaz (dört göz ilkesi).");
        }

        invoice.ApprovedBy = actor.UserId;
        invoice.ApprovedAt = clock.UtcNow;
        await TransitionAsync(scope, invoice, DocumentStatus.Approved, actor, null, ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_APPROVED", invoice, null, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> RejectAsync(Guid id, string reason, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        Require(invoice, DocumentStatus.AwaitingApproval);

        invoice.LastError = "Onaylanmadı: " + reason;
        await TransitionAsync(scope, invoice, DocumentStatus.Draft, actor, reason, ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_APPROVAL_REJECTED", invoice, new { reason }, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> SignAsync(Guid id, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        Invoice invoice;

        await using (var conn = await connections.OpenForTenantAsync(tenantId, ct))
        {
            var scope = await connections.BeginAsync(conn, ct);
            invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
            Require(invoice, DocumentStatus.Approved);
            if (invoice.DocumentNumber is null)
            {
                var tenant = await tenants.GetAsync(tenantId, ct) ?? throw new NotFoundException("Kiracı bulunamadı.");
                var prefix = invoice.DocumentType == EDocumentType.EFatura ? tenant.EFaturaPrefix : tenant.EArsivPrefix;
                var number = await numbers.AllocateAsync(
                    new DocumentSeriesKey(tenantId, invoice.DocumentType.ToString(), prefix, invoice.IssueDate.Year), conn, scope.Transaction, ct);
                invoice.DocumentNumber = number.Value;
            }

            await TransitionAsync(scope, invoice, DocumentStatus.Signing, actor, $"Belge no: {invoice.DocumentNumber}", ct);
            await AuditAsync(scope, tenantId, actor, "INVOICE_NUMBER_ASSIGNED", invoice, new { invoice.DocumentNumber }, ct);
            await scope.Transaction.CommitAsync(ct);
        }

        try
        {
            var ubl = ublWriter.Write(invoice);
            var unsigned = ToBytes(ubl);
            var qrPayload = qr.BuildPayload(invoice);
            signer.Sign(ubl, UblInvoiceWriter.SignatureId(invoice.Uuid));
            var signedBytes = ToBytes(ubl);

            var reloaded = UblInvoiceReader.LoadSecure(new MemoryStream(signedBytes));
            var verification = signer.Verify(reloaded);
            var problems = ublValidator.Validate(reloaded, requireSignature: true)
                .Where(i => i.Severity == FindingSeverity.Error)
                .Select(i => $"{i.Code}: {i.Message}")
                .ToList();
            if (!verification.IsValid)
            {
                problems.Add("SIGNATURE: " + verification.Problem);
            }

            if (problems.Count > 0)
            {
                throw new ValidationFailedException("UBL_INVALID", "İmzalı UBL belgesi doğrulanamadı.", problems);
            }

            var manifest = await vault.StoreAsync(
                tenantId,
                invoice.Id,
                invoice.IssueDate.Year,
                SignedStage,
                [
                    new EvidenceItem("invoice.json", "application/json", JsonDefaults.SerializeToUtf8(invoice, indented: true)),
                    new EvidenceItem("decision.json", "application/json", JsonDefaults.SerializeToUtf8(invoice.Decision, indented: true)),
                    new EvidenceItem("unsigned.xml", "application/xml", unsigned),
                    new EvidenceItem("signed.xml", "application/xml", signedBytes),
                    new EvidenceItem("qr.json", "application/json", Encoding.UTF8.GetBytes(qrPayload)),
                    new EvidenceItem("qr.png", "image/png", qr.RenderPng(qrPayload)),
                ],
                RetentionClass.VukDocument,
                ct);

            await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
            var scope = await connections.BeginAsync(conn, ct);
            invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
            Require(invoice, DocumentStatus.Signing);
            invoice.SignedXmlSha256 = Hashing.Sha256Hex(signedBytes);
            invoice.SignedBy = actor.UserId;
            invoice.SignedAt = clock.UtcNow;
            invoice.LastError = null;
            await TransitionAsync(scope, invoice, DocumentStatus.Signed, actor, null, ct);
            await TransitionAsync(scope, invoice, DocumentStatus.Queued, actor, null, ct);
            await AuditAsync(scope, tenantId, actor, "INVOICE_SIGNED", invoice, new
            {
                invoice.DocumentNumber,
                invoice.SignedXmlSha256,
                manifest.ManifestHash,
                signer = verification.SignerSubject,
                qrStandard = QrCodeService.StandardVersion,
            }, ct);
            await scope.Transaction.CommitAsync(ct);
            return invoice;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await using var conn = await connections.OpenForTenantAsync(tenantId, CancellationToken.None);
            var scope = await connections.BeginAsync(conn, CancellationToken.None);
            var current = await LoadForUpdateAsync(scope, tenantId, id, CancellationToken.None);
            if (current.Status == DocumentStatus.Signing)
            {
                current.LastError = ex.Message;
                await TransitionAsync(scope, current, DocumentStatus.Approved, actor, "İmza başarısız", CancellationToken.None);
                await AuditAsync(scope, tenantId, actor, "INVOICE_SIGN_FAILED", current, null, CancellationToken.None, "Failure", ex.Message);
                await scope.Transaction.CommitAsync(CancellationToken.None);
            }

            throw;
        }
    }

    public Task<Invoice> TransmitAsync(Guid id, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        return TransmitAsync(tenantId, id, actor, ct);
    }

    public async Task<Invoice> TransmitAsync(Guid tenantId, Guid id, Actor actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Invoice invoice;

        await using (var conn = await connections.OpenForTenantAsync(tenantId, ct))
        {
            var scope = await connections.BeginAsync(conn, ct);
            invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
            if (invoice.Status != DocumentStatus.Queued)
            {
                await scope.Transaction.RollbackAsync(ct);
                return invoice;
            }

            invoice.TransmittedAt = clock.UtcNow;
            await TransitionAsync(scope, invoice, DocumentStatus.Transmitting, actor, providers.Active.Name, ct);
            await scope.Transaction.CommitAsync(ct);
        }

        var signedXml = await LoadSignedXmlAsync(invoice, ct);
        SendResult result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(providerOptions.RequestTimeoutSeconds));
            try
            {
                result = await providers.Active.SendAsync(
                    new SendRequest(tenantId, invoice.Id, invoice.Uuid, invoice.DocumentNumber!, invoice.DocumentType, invoice.IdempotencyKey, signedXml, invoice.Customer.Alias),
                    timeout.Token);
            }
            catch (ProviderUnavailableException ex)
            {
                result = new SendResult(SendOutcome.Rejected, null, ex.Message, [], []);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                result = new SendResult(SendOutcome.Unknown, null, ex.Message, [], []);
            }
        }

        var manifest = await vault.StoreAsync(
            tenantId,
            invoice.Id,
            invoice.IssueDate.Year,
            "transmission",
            [
                new EvidenceItem("provider-request.bin", "application/octet-stream", result.RawRequest),
                new EvidenceItem("provider-response.bin", "application/octet-stream", result.RawResponse),
                new EvidenceItem("outcome.json", "application/json", JsonDefaults.SerializeToUtf8(new
                {
                    provider = providers.Active.Name,
                    result.Outcome,
                    result.ProviderReference,
                    result.Message,
                    at = clock.UtcNow,
                }, indented: true)),
            ],
            RetentionClass.SpecialIntegratorLog,
            CancellationToken.None);

        await using (var conn = await connections.OpenForTenantAsync(tenantId, CancellationToken.None))
        {
            var scope = await connections.BeginAsync(conn, CancellationToken.None);
            invoice = await LoadForUpdateAsync(scope, tenantId, id, CancellationToken.None);
            var target = result.Outcome switch
            {
                SendOutcome.Accepted => DocumentStatus.Sent,
                SendOutcome.Rejected => DocumentStatus.Failed,
                _ => DocumentStatus.InDoubt,
            };
            invoice.ProviderReference = result.ProviderReference ?? invoice.ProviderReference;
            invoice.LastError = result.Outcome == SendOutcome.Accepted ? null : result.Message;
            await TransitionAsync(scope, invoice, target, actor, result.Message, CancellationToken.None);
            await AuditAsync(scope, tenantId, actor, "INVOICE_TRANSMITTED", invoice, new { provider = providers.Active.Name, result.Outcome, result.ProviderReference, manifest.ManifestHash },
                CancellationToken.None, result.Outcome == SendOutcome.Accepted ? "Success" : "Failure", result.Outcome == SendOutcome.Accepted ? null : result.Message);
            await scope.Transaction.CommitAsync(CancellationToken.None);
        }

        if (result.Outcome == SendOutcome.Unknown)
        {
            Log.InDoubt(logger, invoice.Id, result.Message);
        }

        return invoice;
    }

    public Task<Invoice> RefreshStatusAsync(Guid id, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        return RefreshStatusAsync(tenantId, id, actor, ct);
    }

    public async Task<Invoice> RefreshStatusAsync(Guid tenantId, Guid id, Actor actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Invoice invoice;
        await using (var conn = await connections.OpenForTenantAsync(tenantId, ct))
        {
            invoice = await invoices.GetAsync(conn, tenantId, id, ct) ?? throw new NotFoundException("Fatura bulunamadı.");
        }

        if (invoice.IssuanceChannel == IssuanceChannel.GibPortal)
        {
            return invoice;
        }

        if (invoice.Status is not (DocumentStatus.Transmitting or DocumentStatus.InDoubt or DocumentStatus.Sent or DocumentStatus.Acknowledged
            or DocumentStatus.Delivered or DocumentStatus.Accepted or DocumentStatus.Failed))
        {
            return invoice;
        }

        var status = await providers.Active.GetStatusAsync(tenantId, invoice.Uuid, ct);
        var graceExpired = invoice.UpdatedAt <= clock.UtcNow.AddMinutes(-workerOptions.InDoubtGraceMinutes);
        DocumentStatus? target = status.State switch
        {
            ProviderDocumentState.NotFound when invoice.Status is DocumentStatus.InDoubt or DocumentStatus.Transmitting && graceExpired => DocumentStatus.Failed,
            ProviderDocumentState.NotFound => null,
            ProviderDocumentState.Received or ProviderDocumentState.Sent => DocumentStatus.Sent,
            ProviderDocumentState.Delivered => DocumentStatus.Delivered,
            ProviderDocumentState.Accepted => DocumentStatus.Accepted,
            ProviderDocumentState.Rejected => DocumentStatus.Rejected,
            ProviderDocumentState.Cancelled => DocumentStatus.Cancelled,
            _ => null,
        };

        if (target is null || target == invoice.Status)
        {
            return invoice;
        }

        var path = DocumentLifecycle.FindReconciliationPath(invoice.Status, target.Value);
        if (path.Count == 0)
        {
            Log.NoPath(logger, invoice.Id, invoice.Status, status.State);
            return invoice;
        }

        var critical = invoice.Status == DocumentStatus.Failed && status.State != ProviderDocumentState.NotFound;
        await using (var conn = await connections.OpenForTenantAsync(tenantId, ct))
        {
            var scope = await connections.BeginAsync(conn, ct);
            var current = await LoadForUpdateAsync(scope, tenantId, id, ct);
            if (current.Status != invoice.Status)
            {
                await scope.Transaction.RollbackAsync(ct);
                return current;
            }

            current.ProviderReference ??= status.ProviderReference;
            if (target == DocumentStatus.Failed)
            {
                current.LastError = "Sağlayıcıda bulunamadı; bekleme süresi sonunda alınmadığı kabul edildi.";
            }

            foreach (var step in path)
            {
                await TransitionAsync(scope, current, step, actor, $"Uzlaştırma: sağlayıcı durumu {status.State}", ct);
            }

            await AuditAsync(scope, tenantId, actor, critical ? "RECONCILIATION_CRITICAL_INCIDENT" : "INVOICE_RECONCILED", current,
                new { from = invoice.Status, to = current.Status, providerState = status.State }, ct, critical ? "Failure" : "Success",
                critical ? "Yerelde başarısız görünen belge sağlayıcıda bulundu." : null);
            await scope.Transaction.CommitAsync(ct);
            invoice = current;
        }

        if (critical)
        {
            Log.Critical(logger, invoice.Id, status.State);
        }

        return invoice;
    }

    public async Task<Invoice> RetryAsync(Guid id, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        var invoice = await RefreshStatusAsync(tenantId, id, actor, ct);
        Require(invoice, DocumentStatus.Failed);

        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        Require(invoice, DocumentStatus.Failed);
        await TransitionAsync(scope, invoice, DocumentStatus.Queued, actor, "Yeniden gönderim (aynı ETTN/idempotency anahtarı)", ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_REQUEUED", invoice, null, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> BeginExternalIssuanceAsync(Guid id, IssuanceChannel channel, GibPortalEnvironment? environment, Guid externalEttn, string note, object? auditData, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        Require(invoice, DocumentStatus.Approved);
        invoice.IssuanceChannel = channel;
        invoice.PortalEnvironment = environment;
        invoice.Uuid = externalEttn;
        invoice.LastError = null;
        await TransitionAsync(scope, invoice, DocumentStatus.Signing, actor, note, ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_EXTERNAL_DRAFT_CREATED", invoice, auditData, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> AbortExternalIssuanceAsync(Guid id, string reason, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        Require(invoice, DocumentStatus.Signing);
        invoice.LastError = reason;
        await TransitionAsync(scope, invoice, DocumentStatus.Approved, actor, reason, ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_EXTERNAL_DRAFT_ABORTED", invoice, new { reason }, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> CompleteExternalIssuanceAsync(Guid id, string documentNumber, string? signedXmlSha256, string? signerSubject, string providerReference, object? auditData, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        Require(invoice, DocumentStatus.Signing);
        if (invoice.IssuanceChannel == IssuanceChannel.Integrator)
        {
            throw new DomainException("INVOICE_CHANNEL", "Bu fatura harici kanalda düzenlenmiyor.");
        }

        var now = clock.UtcNow;
        invoice.DocumentNumber = documentNumber;
        invoice.SignedXmlSha256 = signedXmlSha256;
        invoice.SignedBy = actor.UserId;
        invoice.SignedAt = now;
        invoice.TransmittedAt = now;
        invoice.ProviderReference = providerReference;
        invoice.LastError = null;
        var note = $"{providerReference}: {documentNumber}";
        await TransitionAsync(scope, invoice, DocumentStatus.Signed, actor, signerSubject ?? note, ct);
        await TransitionAsync(scope, invoice, DocumentStatus.Queued, actor, note, ct);
        await TransitionAsync(scope, invoice, DocumentStatus.Transmitting, actor, note, ct);
        await TransitionAsync(scope, invoice, DocumentStatus.Sent, actor, note, ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_ISSUED_EXTERNALLY", invoice, auditData, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task RecordActionAsync(InvoiceAction action, string auditAction, object? auditData, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, action.InvoiceId, ct);
        action.TenantId = tenantId;
        action.RequestedBy = actor.UserId;
        action.CreatedAt = clock.UtcNow;
        await invoices.InsertActionAsync(action, scope, ct);
        await AuditAsync(scope, tenantId, actor, auditAction, invoice, auditData, ct);
        await scope.Transaction.CommitAsync(ct);
    }

    public async Task<Invoice> CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationFailedException("CANCEL_REASON", "İptal gerekçesi zorunludur.", []);
        }

        var invoice = await RefreshStatusAsync(tenantId, id, actor, ct);
        if (invoice.IssuanceChannel == IssuanceChannel.GibPortal)
        {
            throw new ValidationFailedException("CANCEL_VIA_GIB_PORTAL", "Bu fatura GİB e-Arşiv Portal üzerinden düzenlendi; iptal talebi GİB portal ekranından açılmalıdır.", []);
        }

        if (invoice.DocumentType != EDocumentType.EArsiv)
        {
            throw new ValidationFailedException(
                "CANCEL_NOT_SUPPORTED",
                "Sistem üzerinden iptal yalnızca e-Arşiv Fatura için desteklenir. e-Fatura'da ret/itiraz/iade süreçleri kullanılmalıdır (kural teyidi bekliyor).",
                []);
        }

        Require(invoice, DocumentStatus.Delivered);
        var result = await providers.Active.CancelArchiveInvoiceAsync(tenantId, invoice.Uuid, reason, ct);
        var manifest = await vault.StoreAsync(tenantId, invoice.Id, invoice.IssueDate.Year, "cancellation",
            [new EvidenceItem("cancellation.json", "application/json", JsonDefaults.SerializeToUtf8(new { reason, result, by = actor.UserId, at = clock.UtcNow }, indented: true))],
            RetentionClass.VukDocument, ct);

        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        await invoices.InsertActionAsync(new InvoiceAction
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            InvoiceId = id,
            Kind = InvoiceActionKind.Cancellation,
            Reason = reason,
            Status = result.Success ? "Completed" : "Failed",
            RequestedBy = actor.UserId,
            CreatedAt = clock.UtcNow,
            ProviderReference = result.ProviderReference,
        }, scope, ct);

        if (result.Success)
        {
            await TransitionAsync(scope, invoice, DocumentStatus.Cancelled, actor, reason, ct);
        }

        await AuditAsync(scope, tenantId, actor, "INVOICE_CANCELLATION", invoice, new { reason, result.Success, result.Message, manifest.ManifestHash }, ct,
            result.Success ? "Success" : "Failure", result.Success ? null : result.Message);
        await scope.Transaction.CommitAsync(ct);

        return result.Success
            ? invoice
            : throw new ValidationFailedException("CANCEL_FAILED", "Sağlayıcı iptali kabul etmedi.", [result.Message ?? string.Empty]);
    }

    public async Task<Invoice> ObjectAsync(Guid id, string method, string? referenceNumber, DateOnly notificationDate, string reason, CancellationToken ct)
    {
        var (tenantId, actor) = Who();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        var scope = await connections.BeginAsync(conn, ct);
        var invoice = await LoadForUpdateAsync(scope, tenantId, id, ct);
        if (invoice.Status is not (DocumentStatus.Delivered or DocumentStatus.Accepted))
        {
            throw new DomainException("INVOICE_STATE", $"İtiraz yalnızca teslim edilmiş/kabul edilmiş belgeler için kaydedilebilir (mevcut: {invoice.Status}).");
        }

        var action = new InvoiceAction
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            InvoiceId = id,
            Kind = InvoiceActionKind.Objection,
            Method = method,
            ReferenceNumber = referenceNumber,
            NotificationDate = notificationDate,
            Reason = reason,
            Status = "Recorded",
            RequestedBy = actor.UserId,
            CreatedAt = clock.UtcNow,
        };
        var manifest = await vault.StoreAsync(tenantId, id, invoice.IssueDate.Year, "objection",
            [new EvidenceItem("objection.json", "application/json", JsonDefaults.SerializeToUtf8(action, indented: true))], RetentionClass.VukDocument, ct);
        await invoices.InsertActionAsync(action, scope, ct);
        await TransitionAsync(scope, invoice, DocumentStatus.Objected, actor, $"{method} {referenceNumber}", ct);
        await AuditAsync(scope, tenantId, actor, "INVOICE_OBJECTION", invoice, new { method, referenceNumber, notificationDate, manifest.ManifestHash }, ct);
        await scope.Transaction.CommitAsync(ct);
        return invoice;
    }

    public async Task<Invoice> GetAsync(Guid id, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        return await invoices.GetAsync(conn, tenantId, id, ct) ?? throw new NotFoundException("Fatura bulunamadı.");
    }

    public async Task<InvoicePage> ListAsync(InvoiceQuery query, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        return await invoices.ListAsync(conn, tenantId, query, ct);
    }

    public async Task<(IReadOnlyList<InvoiceHistoryEntry> History, IReadOnlyList<InvoiceAction> Actions)> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        _ = await invoices.GetAsync(conn, tenantId, id, ct) ?? throw new NotFoundException("Fatura bulunamadı.");
        return (await invoices.GetHistoryAsync(conn, tenantId, id, ct), await invoices.GetActionsAsync(conn, tenantId, id, ct));
    }

    public async Task<byte[]> GetSignedXmlAsync(Guid id, CancellationToken ct) => await LoadSignedXmlAsync(await GetAsync(id, ct), ct);

    public async Task<byte[]> GetQrPngAsync(Guid id, CancellationToken ct)
    {
        var invoice = await GetAsync(id, ct);
        var manifest = (await vault.ListAsync(invoice.TenantId, invoice.Id, ct)).LastOrDefault(m => m.Stage == SignedStage)
            ?? throw new NotFoundException("Fatura henüz imzalanmamış; karekod yok.");
        return await vault.ReadAsync(invoice.TenantId, invoice.Id, manifest.Sequence, "qr.png", ct) ?? throw new NotFoundException("Karekod bulunamadı.");
    }

    public async Task<IReadOnlyList<Models.Evidence.EvidenceManifest>> GetEvidenceAsync(Guid id, CancellationToken ct)
    {
        var invoice = await GetAsync(id, ct);
        return await vault.ListAsync(invoice.TenantId, invoice.Id, ct);
    }

    public async Task<EvidenceVerification> VerifyEvidenceAsync(Guid id, CancellationToken ct)
    {
        var invoice = await GetAsync(id, ct);
        var verification = await vault.VerifyAsync(invoice.TenantId, invoice.Id, ct);
        if (invoice.SignedXmlSha256 is { } expected)
        {
            var signed = await LoadSignedXmlAsync(invoice, ct, verifyHash: false);
            if (Hashing.Sha256Hex(signed) != expected)
            {
                verification = verification with
                {
                    IsValid = false,
                    Problems = [.. verification.Problems, "İmzalı XML hash'i veritabanındaki kayıtla uyuşmuyor."],
                };
            }
        }

        return verification;
    }

    private async Task<Models.Catalog.Customer> SaveCustomerCardAsync(
        Guid tenantId, Actor actor, InvoiceParty party, Models.Catalog.Customer? existing, bool? isEFaturaRegistered, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var card = new Models.Catalog.Customer
        {
            Id = existing?.Id ?? Guid.CreateVersion7(now),
            TenantId = tenantId,
            TaxId = party.TaxId,
            Title = party.Title,
            FirstName = party.FirstName,
            FamilyName = party.FamilyName,
            Regime = party.Regime,
            TaxOffice = party.TaxOffice,
            ProvinceName = party.City,
            DistrictName = party.District,
            NeighborhoodName = party.Neighborhood,
            Street = party.Street,
            BuildingNumber = party.BuildingNumber,
            PostalCode = party.PostalCode,
            Country = string.IsNullOrWhiteSpace(party.Country) ? "Türkiye" : party.Country,
            Email = party.Email,
            Phone = party.Phone,
            IsEFaturaRegistered = isEFaturaRegistered ?? existing?.IsEFaturaRegistered ?? false,
            EFaturaAlias = party.Alias ?? existing?.EFaturaAlias,
            Notes = existing?.Notes,
            IsActive = true,
            CreatedBy = existing?.CreatedBy ?? actor.UserId,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now,
        };
        await customers.UpsertAsync(card, ct);
        return card;
    }

    private (Guid TenantId, Actor Actor) Who() =>
        (context.RequireTenant(), new Actor(context.RequireUser(), context.IsSystem));

    private void ApplyDecision(Invoice invoice, ComplianceDecision decision)
    {
        invoice.Decision = decision;
        invoice.DocumentType = decision.DocumentType;
        invoice.Profile = decision.DocumentType switch
        {
            EDocumentType.EArsiv => InvoiceProfile.EARSIVFATURA,
            EDocumentType.EFatura when invoice.Profile == InvoiceProfile.EARSIVFATURA => InvoiceProfile.TEMELFATURA,
            _ => invoice.Profile,
        };
    }

    private async Task<Invoice> LoadForUpdateAsync(DbScope scope, Guid tenantId, Guid id, CancellationToken ct) =>
        await invoices.GetAsync(scope.Connection, tenantId, id, ct, scope.Transaction) ?? throw new NotFoundException("Fatura bulunamadı.");

    private static void Require(Invoice invoice, DocumentStatus expected)
    {
        if (invoice.Status != expected)
        {
            throw new DomainException("INVOICE_STATE", $"Bu işlem için fatura {expected} durumunda olmalıdır (mevcut: {invoice.Status}).");
        }
    }

    private async Task TransitionAsync(DbScope scope, Invoice invoice, DocumentStatus to, Actor actor, string? note, CancellationToken ct)
    {
        var from = invoice.Status;
        invoice.TransitionTo(to);
        invoice.UpdatedAt = clock.UtcNow;
        await invoices.UpdateAsync(invoice, scope, ct);
        await invoices.AddHistoryAsync(scope, invoice, from, actor.UserId, note, ct);
        if (InvoiceEvents.ForTransition(from, to) is { } eventType)
        {
            await PublishAsync(scope, invoice, eventType, from, actor, note, ct);
        }
    }

    private Task PublishAsync(DbScope scope, Invoice invoice, string eventType, DocumentStatus? from, Actor actor, string? note, CancellationToken ct) =>
        events.EnqueueAsync(
            scope,
            invoice.TenantId,
            eventType,
            "Invoice",
            invoice.Id.ToString(),
            JsonDefaults.Serialize(new InvoiceEventPayload(invoice.Id, from?.ToString(), invoice.Status.ToString(), actor.UserId, note)),
            clock.UtcNow,
            ct);

    public async Task<Invoice?> FindAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await using var conn = await connections.OpenForTenantAsync(tenantId, ct);
        return await invoices.GetAsync(conn, tenantId, id, ct);
    }

    public async Task<byte[]?> TryGetSignedXmlAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        try
        {
            return await LoadSignedXmlAsync(invoice, ct);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<byte[]?> TryGetQrPngAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var manifest = (await vault.ListAsync(invoice.TenantId, invoice.Id, ct)).LastOrDefault(m => m.Stage == SignedStage);
        return manifest is null ? null : await vault.ReadAsync(invoice.TenantId, invoice.Id, manifest.Sequence, "qr.png", ct);
    }

    private Task<AuditEvent> AuditAsync(
        DbScope scope,
        Guid tenantId,
        Actor actor,
        string action,
        Invoice invoice,
        object? data,
        CancellationToken ct,
        string result = "Success",
        string? failure = null)
    {
        var entry = new AuditEntry(action, "Invoice", invoice.Id.ToString(), result, data, failure);
        return actor.IsSystem ? audit.AppendSystemAsync(tenantId, entry, scope, ct) : audit.AppendAsync(entry, scope, ct);
    }

    private async Task<byte[]> LoadSignedXmlAsync(Invoice invoice, CancellationToken ct, bool verifyHash = true)
    {
        var manifest = (await vault.ListAsync(invoice.TenantId, invoice.Id, ct)).LastOrDefault(m => m.Stage == SignedStage)
            ?? throw new NotFoundException("Fatura henüz imzalanmamış.");
        var bytes = await vault.ReadAsync(invoice.TenantId, invoice.Id, manifest.Sequence, "signed.xml", ct)
            ?? throw new NotFoundException("İmzalı XML kanıt kasasında bulunamadı.");
        if (verifyHash && invoice.SignedXmlSha256 is { } expected && Hashing.Sha256Hex(bytes) != expected)
        {
            throw new InvalidOperationException("Kanıt kasasındaki imzalı XML veritabanındaki hash ile uyuşmuyor (bütünlük ihlali).");
        }

        return bytes;
    }

    internal static byte[] ToBytes(XmlDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false }))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Fatura {InvoiceId} gönderim sonucu belirsiz (InDoubt): {Message}. Uzlaştırma bekleniyor.")]
        public static partial void InDoubt(ILogger logger, Guid invoiceId, string? message);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Fatura {InvoiceId}: {Status} durumundan sağlayıcı durumu {ProviderState} için güvenli uzlaştırma yolu yok.")]
        public static partial void NoPath(ILogger logger, Guid invoiceId, DocumentStatus status, ProviderDocumentState providerState);

        [LoggerMessage(Level = LogLevel.Critical, Message = "KRİTİK: Fatura {InvoiceId} yerelde Failed iken sağlayıcıda {ProviderState} durumunda bulundu. Mükerrer gönderim riski — inceleme gerekli.")]
        public static partial void Critical(ILogger logger, Guid invoiceId, ProviderDocumentState providerState);
    }
}
