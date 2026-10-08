using System.IO.Compression;
using System.Text;
using GIBFramework.DAL.Tenants;
using GIBFramework.Infrastructure.GibPortal;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Evidence;
using GIBFramework.Services.Invoices;
using GIBFramework.Services.Ubl;

namespace GIBFramework.Services.GibPortal;

public sealed record GibPortalStatus(
    bool Connected,
    GibPortalEnvironment? Environment,
    string? PortalTaxId,
    string? PortalTitle,
    string? PortalUserCode,
    DateTimeOffset? ConnectedAt,
    bool ProductionAllowed);

public sealed record GibPortalDraftResult(Guid Ettn, string? PortalDocumentNumber, GibPortalEnvironment Environment);

public sealed record GibPortalSmsResult(string MaskedPhone, int ValidMinutes);

public sealed record GibPortalTestUser(string UserCode, string Password);

public sealed class GibPortalService(
    GibPortalClient client,
    GibPortalSessionStore sessions,
    GibPortalOptions options,
    InvoiceService invoices,
    ITenantRepository tenants,
    IXmlSigner signer,
    UblValidator validator,
    IEvidenceVault vault,
    IAuditTrail audit,
    ITenantContext context,
    IClock clock)
{
    public const string ProviderReference = "GIB-EARSIV-PORTAL";

    public GibPortalStatus Status()
    {
        var current = sessions.Get(context.RequireTenant(), context.RequireUser());
        var s = current?.Session;
        return new GibPortalStatus(s is not null, s?.Environment, s?.PortalTaxId, s?.PortalTitle, s?.PortalUserCodeMasked, s?.ConnectedAt, options.AllowProduction);
    }

    public async Task<GibPortalTestUser> SuggestTestUserAsync(CancellationToken ct)
    {
        if (!options.Enabled)
        {
            throw new ProviderUnavailableException("GİB e-Arşiv Portal bağlantısı devre dışı (GibPortal:Enabled=false).");
        }

        try
        {
            return new GibPortalTestUser(await client.SuggestTestUserAsync(ct), GibPortalProtocol.TestUserPassword);
        }
        catch (GibPortalException ex)
        {
            throw new ProviderUnavailableException(ex.Message);
        }
    }

    public async Task<GibPortalStatus> ConnectAsync(GibPortalEnvironment environment, string userCode, string password, CancellationToken ct)
    {
        if (!options.Enabled)
        {
            throw new ProviderUnavailableException("GİB e-Arşiv Portal bağlantısı devre dışı (GibPortal:Enabled=false).");
        }

        if (environment == GibPortalEnvironment.Production && !options.AllowProduction)
        {
            throw new ForbiddenOperationException("GIB_PORTAL_PRODUCTION_DISABLED", "Canlı GİB portal bağlantısı kapalı (GibPortal:AllowProduction=false). Yönetici açmadan yalnızca test portalı kullanılabilir.");
        }

        if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrEmpty(password))
        {
            throw new ValidationFailedException("GIB_PORTAL_CREDENTIALS", "GİB kullanıcı kodu ve şifre zorunludur.", []);
        }

        var tenantId = context.RequireTenant();
        var userId = context.RequireUser();
        var tenant = await tenants.GetAsync(tenantId, ct) ?? throw new NotFoundException("Firma bulunamadı.");

        string token;
        try
        {
            token = await client.LoginAsync(environment, userCode.Trim(), password, ct);
        }
        catch (GibPortalException ex)
        {
            await AuditAsync("GIB_PORTAL_LOGIN_FAILED", tenantId.ToString(), "Failure", new { environment, user = Mask(userCode) }, ex.Message, ct);
            throw new ForbiddenOperationException("GIB_PORTAL_LOGIN_FAILED", ex.Message);
        }

        var info = await client.GetUserInfoAsync(environment, token, ct);
        if (environment == GibPortalEnvironment.Production && info.TaxId != tenant.Profile.TaxId)
        {
            await client.LogoutAsync(environment, token, ct);
            await AuditAsync("GIB_PORTAL_TAXID_MISMATCH", tenantId.ToString(), "Failure", new { environment, portal = info.TaxId }, null, ct);
            throw new ForbiddenOperationException("GIB_PORTAL_TAXID_MISMATCH",
                $"GİB portal hesabının VKN/TCKN'si ({info.TaxId}) bu firmanın VKN/TCKN'si ({tenant.Profile.TaxId}) ile aynı değil. Başka bir mükellef adına fatura düzenlenemez.");
        }

        var title = string.IsNullOrWhiteSpace(info.Title) ? $"{info.FirstName} {info.LastName}".Trim() : info.Title;
        sessions.Save(new GibPortalSession(tenantId, userId, environment, Mask(userCode), info.TaxId, title, clock.UtcNow), token);
        await AuditAsync("GIB_PORTAL_CONNECTED", tenantId.ToString(), "Success", new { environment, user = Mask(userCode), portalTaxId = info.TaxId }, null, ct);
        return Status();
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        var userId = context.RequireUser();
        if (sessions.Get(tenantId, userId) is { } current)
        {
            await client.LogoutAsync(current.Session.Environment, current.Token, ct);
            sessions.Remove(tenantId, userId);
            await AuditAsync("GIB_PORTAL_DISCONNECTED", tenantId.ToString(), "Success", new { current.Session.Environment }, null, ct);
        }
    }

    public async Task<GibPortalRecipient> LookupRecipientAsync(string taxId, CancellationToken ct)
    {
        var (session, token) = RequireSession();
        return await Call(session, () => client.GetRecipientAsync(session.Environment, token, taxId, ct));
    }

    public async Task<GibPortalDraftResult> CreateDraftAsync(Guid invoiceId, CancellationToken ct)
    {
        var (session, token) = RequireSession();
        var invoice = await invoices.GetAsync(invoiceId, ct);
        if (invoice.Status != DocumentStatus.Approved)
        {
            throw new DomainException("INVOICE_STATE", $"GİB portalında taslak için fatura onaylı (Approved) olmalıdır (mevcut: {invoice.Status}).");
        }

        var unsupported = GibPortalDraftMapper.Unsupported(invoice);
        if (unsupported.Count > 0)
        {
            throw new ValidationFailedException("GIB_PORTAL_UNSUPPORTED", "Bu fatura GİB e-Arşiv Portal üzerinden düzenlenemez.", unsupported);
        }

        var draft = GibPortalDraftMapper.Map(invoice);
        var accountLock = sessions.AccountLock(session);
        await accountLock.WaitAsync(ct);
        GibPortalDocumentRow created;
        try
        {
            var day = invoice.IssueDate;
            var before = (await Call(session, () => client.ListDraftsAsync(session.Environment, token, day, day, ct))).Select(r => r.Ettn).ToHashSet(StringComparer.OrdinalIgnoreCase);
            await Call(session, () => client.CreateDraftAsync(session.Environment, token, draft, ct));

            GibPortalDocumentRow? found = null;
            for (var attempt = 0; attempt < 4 && found is null; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(750 * attempt), ct);
                }

                found = (await Call(session, () => client.ListDraftsAsync(session.Environment, token, day, day, ct)))
                    .Where(r => !before.Contains(r.Ettn) && r.RecipientTaxId == invoice.Customer.TaxId && !r.IsApproved)
                    .LastOrDefault();
            }

            created = found ?? throw new GibPortalException(
                "Taslak GİB portalında oluşturuldu ancak listede bulunamadı. Mükerrer oluşturmamak için portalda 'Düzenlenen Belgeler' ekranını kontrol edin.");
        }
        finally
        {
            accountLock.Release();
        }

        var ettn = Guid.Parse(created.Ettn);
        await vault.StoreAsync(invoice.TenantId, invoice.Id, invoice.IssueDate.Year, "gib-portal-draft",
            [
                new EvidenceItem("portal-draft-request.json", "application/json", JsonDefaults.SerializeToUtf8(draft, indented: true)),
                new EvidenceItem("portal-draft-row.json", "application/json", Encoding.UTF8.GetBytes(created.Raw.GetRawText())),
            ],
            RetentionClass.SpecialIntegratorLog, ct);
        await invoices.BeginExternalIssuanceAsync(invoice.Id, IssuanceChannel.GibPortal, session.Environment, ettn,
            $"GİB e-Arşiv Portal ({session.Environment}) taslağı: {created.Ettn}", new { session.Environment, created.Ettn, created.DocumentNumber }, ct);
        return new GibPortalDraftResult(ettn, created.DocumentNumber, session.Environment);
    }

    public async Task<GibPortalSmsResult> SendSmsAsync(Guid invoiceId, CancellationToken ct)
    {
        var (session, token) = RequireSession();
        var invoice = await RequirePortalInvoiceAsync(invoiceId, session, ct);
        if (session.Environment == GibPortalEnvironment.Test)
        {
            throw new DomainException("GIB_PORTAL_TEST_NO_SMS", "Test portalı SMS göndermez; imza için kod girmeden 'İmzala' kullanın.");
        }

        var phone = await Call(session, () => client.GetPhoneAsync(session.Environment, token, ct));
        var oid = await Call(session, () => client.SendSmsAsync(session.Environment, token, phone, ct));
        sessions.SavePendingSms(invoice.Id, oid);
        await AuditAsync("GIB_PORTAL_SMS_SENT", invoice.Id.ToString(), "Success", new { phone = MaskPhone(phone) }, null, ct);
        return new GibPortalSmsResult(MaskPhone(phone), 5);
    }

    public async Task<Invoice> SignAsync(Guid invoiceId, string? smsCode, CancellationToken ct)
    {
        var (session, token) = RequireSession();
        var invoice = await RequirePortalInvoiceAsync(invoiceId, session, ct);
        var ettn = invoice.Uuid.ToString("D");
        var row = await FindRowAsync(session, token, invoice, ettn, ct)
            ?? throw new NotFoundException("Taslak GİB portalında bulunamadı (silinmiş olabilir).");

        if (!row.IsApproved)
        {
            if (session.Environment == GibPortalEnvironment.Production)
            {
                if (smsCode is not { Length: 6 } || !smsCode.All(char.IsAsciiDigit))
                {
                    throw new ValidationFailedException("GIB_PORTAL_SMS_CODE", "6 haneli SMS onay kodu gereklidir.", []);
                }

                var oid = sessions.TakePendingSms(invoice.Id)
                    ?? throw new DomainException("GIB_PORTAL_SMS_EXPIRED", "SMS kodu isteği bulunamadı veya süresi doldu; yeniden kod isteyin.");
                if (!await Call(session, () => client.VerifySmsAndSignAsync(session.Environment, token, smsCode, oid, [row], ct)))
                {
                    await AuditAsync("GIB_PORTAL_SIGN_FAILED", invoice.Id.ToString(), "Failure", null, "SMS kodu doğrulanamadı", ct);
                    throw new ValidationFailedException("GIB_PORTAL_SMS_INVALID", "SMS onay kodu GİB tarafından kabul edilmedi.", []);
                }
            }
            else
            {
                await Call(session, () => client.SignWithHsmAsync(session.Environment, token, [row], ct));
            }

            row = await FindRowAsync(session, token, invoice, ettn, ct);
            if (row is not { IsApproved: true })
            {
                throw new GibPortalException("İmza isteği gönderildi ancak belge GİB portalında henüz 'Onaylandı' görünmüyor; birkaç saniye sonra tekrar deneyin.");
            }
        }

        var zip = await Call(session, () => client.DownloadZipAsync(session.Environment, token, ettn, ct));
        var (xml, html) = ExtractZip(zip);
        var items = new List<EvidenceItem>
        {
            new("gib-portal.zip", "application/zip", zip),
            new("portal-signed-row.json", "application/json", Encoding.UTF8.GetBytes(row.Raw.GetRawText())),
        };

        string? signedSha = null;
        SignatureVerification? signature = null;
        IReadOnlyList<UblIssue> ublIssues = [];
        if (xml is not null)
        {
            signedSha = Hashing.Sha256Hex(xml);
            var doc = UblInvoiceReader.LoadSecure(new MemoryStream(xml));
            signature = signer.Verify(doc);
            ublIssues = validator.Validate(doc, requireSignature: true);
            items.Add(new EvidenceItem("signed.xml", "application/xml", xml));
        }

        if (html is not null)
        {
            items.Add(new EvidenceItem("invoice.html", "text/html", html));
        }

        items.Add(new EvidenceItem("verification.json", "application/json", JsonDefaults.SerializeToUtf8(new { signature, ublIssues, environment = session.Environment }, indented: true)));
        var manifest = await vault.StoreAsync(invoice.TenantId, invoice.Id, invoice.IssueDate.Year, "signed", items, RetentionClass.VukDocument, ct);

        return await invoices.CompleteExternalIssuanceAsync(
            invoice.Id,
            row.DocumentNumber ?? throw new GibPortalException("GİB portalı belge numarası döndürmedi."),
            signedSha,
            signature?.SignerSubject,
            $"{ProviderReference}:{session.Environment}",
            new { session.Environment, row.DocumentNumber, ettn, manifest.ManifestHash, signatureValid = signature?.IsValid, ublErrors = ublIssues.Count(i => i.Severity == FindingSeverity.Error) },
            ct);
    }

    public async Task<IReadOnlyList<GibPortalDocumentRow>> ListDocumentsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidateRange(from, to);
        var (session, token) = RequireSession();
        return await Call(session, () => client.ListDraftsAsync(session.Environment, token, from, to, ct));
    }

    public async Task<IReadOnlyList<System.Text.Json.JsonElement>> ListIssuedToMeAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidateRange(from, to);
        var (session, token) = RequireSession();
        return await Call(session, () => client.ListIssuedToMeAsync(session.Environment, token, from, to, ct));
    }

    public async Task<string> GetDocumentHtmlAsync(string ettn, bool approved, CancellationToken ct)
    {
        if (!Guid.TryParse(ettn, out _))
        {
            throw new ValidationFailedException("GIB_PORTAL_ETTN", "ETTN geçersiz.", []);
        }

        var (session, token) = RequireSession();
        return await Call(session, () => client.GetHtmlAsync(session.Environment, token, ettn, approved, ct));
    }

    public async Task<GibPortalUserInfo> GetUserInfoAsync(CancellationToken ct)
    {
        var (session, token) = RequireSession();
        return await Call(session, () => client.GetUserInfoAsync(session.Environment, token, ct));
    }

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 31)
        {
            throw new ValidationFailedException("GIB_PORTAL_RANGE", "Tarih aralığı en fazla 31 gün olabilir ve bitiş başlangıçtan önce olamaz.", []);
        }
    }

    public async Task<string> GetHtmlAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await invoices.GetAsync(invoiceId, ct);
        if (invoice.IssuanceChannel != IssuanceChannel.GibPortal)
        {
            throw new DomainException("INVOICE_CHANNEL", "Bu fatura GİB portal kanalında değil.");
        }

        var signedManifest = (await vault.ListAsync(invoice.TenantId, invoice.Id, ct)).LastOrDefault(m => m.Stage == "signed");
        if (signedManifest is not null && await vault.ReadAsync(invoice.TenantId, invoice.Id, signedManifest.Sequence, "invoice.html", ct) is { } stored)
        {
            return Encoding.UTF8.GetString(stored);
        }

        var (session, token) = RequireSession();
        return await Call(session, () => client.GetHtmlAsync(session.Environment, token, invoice.Uuid.ToString("D"), approved: false, ct));
    }

    public async Task<Invoice> DeleteDraftAsync(Guid invoiceId, string reason, CancellationToken ct)
    {
        var (session, token) = RequireSession();
        var invoice = await RequirePortalInvoiceAsync(invoiceId, session, ct);
        var row = await FindRowAsync(session, token, invoice, invoice.Uuid.ToString("D"), ct);
        if (row is { IsApproved: true })
        {
            throw new DomainException("GIB_PORTAL_ALREADY_SIGNED", "Belge GİB'de imzalanmış; silinemez. 'İmzala' ile süreci tamamlayın veya iptal talebi açın.");
        }

        if (row is not null)
        {
            await Call(session, () => client.DeleteDraftsAsync(session.Environment, token, [row], reason, ct));
        }

        return await invoices.AbortExternalIssuanceAsync(invoice.Id, "GİB portal taslağı silindi: " + reason, ct);
    }

    public async Task<string> RequestCancellationAsync(Guid invoiceId, string reason, CancellationToken ct)
    {
        var (session, token) = RequireSession();
        var invoice = await invoices.GetAsync(invoiceId, ct);
        if (invoice.IssuanceChannel != IssuanceChannel.GibPortal || invoice.Status != DocumentStatus.Sent)
        {
            throw new DomainException("INVOICE_STATE", "İptal talebi yalnızca GİB portalında düzenlenmiş (Sent) faturalar için açılabilir.");
        }

        var issuedOn = DateOnly.FromDateTime(TurkeyTime.ToTurkey(invoice.SignedAt ?? invoice.UpdatedAt).DateTime);
        if (clock.TurkeyToday.DayNumber - issuedOn.DayNumber > CancellationWindowDays)
        {
            throw new ValidationFailedException("GIB_PORTAL_CANCEL_WINDOW",
                $"İptal bildirimi teslimden itibaren {CancellationWindowDays} gün içinde yapılabilir; süre geçmiş görünüyor.", []);
        }

        var message = await Call(session, () => client.CreateCancellationRequestAsync(session.Environment, token, invoice.Uuid.ToString("D"), reason, ct));
        await invoices.RecordActionAsync(
            new InvoiceAction { Id = Guid.CreateVersion7(), InvoiceId = invoice.Id, Kind = InvoiceActionKind.Cancellation, Reason = reason, Status = "Requested", ProviderReference = ProviderReference, Method = "GIB_PORTAL" },
            "INVOICE_CANCELLATION_REQUESTED",
            new { message },
            ct);
        return message;
    }

    public const int CancellationWindowDays = 8;

    private (GibPortalSession Session, string Token) RequireSession() =>
        sessions.Get(context.RequireTenant(), context.RequireUser())
        ?? throw new ForbiddenOperationException("GIB_PORTAL_NOT_CONNECTED", "GİB e-Arşiv Portal oturumu yok veya süresi doldu. GİB kullanıcı kodu ve şifrenizle yeniden bağlanın.");

    private async Task<Invoice> RequirePortalInvoiceAsync(Guid invoiceId, GibPortalSession session, CancellationToken ct)
    {
        var invoice = await invoices.GetAsync(invoiceId, ct);
        if (invoice.IssuanceChannel != IssuanceChannel.GibPortal || invoice.Status != DocumentStatus.Signing)
        {
            throw new DomainException("INVOICE_STATE", "Bu işlem için faturanın GİB portalında imza bekleyen bir taslağı olmalıdır.");
        }

        if (invoice.PortalEnvironment != session.Environment)
        {
            throw new DomainException("GIB_PORTAL_ENVIRONMENT", $"Taslak {invoice.PortalEnvironment} ortamında; şu anki oturum {session.Environment}.");
        }

        return invoice;
    }

    private async Task<GibPortalDocumentRow?> FindRowAsync(GibPortalSession session, string token, Invoice invoice, string ettn, CancellationToken ct)
    {
        var rows = await Call(session, () => client.ListDraftsAsync(session.Environment, token, invoice.IssueDate, invoice.IssueDate, ct));
        return rows.FirstOrDefault(r => string.Equals(r.Ettn, ettn, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<T> Call<T>(GibPortalSession session, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (GibPortalException ex) when (ex.SessionExpired)
        {
            sessions.Remove(session.TenantId, session.OwnerUserId);
            throw new ForbiddenOperationException("GIB_PORTAL_SESSION_EXPIRED", "GİB portal oturumu zaman aşımına uğradı; yeniden bağlanın.");
        }
        catch (GibPortalException ex)
        {
            throw new ValidationFailedException("GIB_PORTAL_ERROR", ex.Message, []);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderUnavailableException("GİB portalına ulaşılamadı: " + ex.Message);
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            throw new ProviderUnavailableException("GİB portalı zamanında yanıt vermedi.");
        }
    }

    private async Task Call(GibPortalSession session, Func<Task> action) =>
        await Call(session, async () =>
        {
            await action();
            return true;
        });

    private static (byte[]? Xml, byte[]? Html) ExtractZip(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        byte[]? Read(string extension)
        {
            var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && e.Length < 20 * 1024 * 1024);
            if (entry is null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        return (Read(".xml"), Read(".html"));
    }

    private Task AuditAsync(string action, string entityId, string result, object? data, string? failure, CancellationToken ct) =>
        audit.AppendAsync(new AuditEntry(action, "GibPortal", entityId, result, data, failure), null, ct);

    private static string Mask(string userCode)
    {
        var c = userCode.Trim();
        return c.Length <= 4 ? new string('*', c.Length) : string.Concat(c.AsSpan(0, 2), new string('*', c.Length - 4), c.AsSpan(c.Length - 2));
    }

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length < 4 ? "****" : new string('*', digits.Length - 4) + digits[^4..];
    }
}
