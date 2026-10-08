using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using GIBFramework.DAL.Messaging;
using GIBFramework.DAL.Tenants;
using GIBFramework.Infrastructure.Tenancy;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Integrations;
using GIBFramework.Services.Invoices;
using GIBFramework.Services.Messaging;

namespace GIBFramework.Services.Integrations;

public sealed record IntegrationView(
    Guid Id,
    IntegrationKind Kind,
    string Name,
    bool IsActive,
    Dictionary<string, string?> Settings,
    IReadOnlyList<string> Events,
    Dictionary<string, bool> Secrets,
    string InboundUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastInboundAt,
    DateTimeOffset? LastOutboundAt);

public sealed record SaveIntegrationRequest(
    IntegrationKind Kind,
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    bool IsActive,
    Dictionary<string, string?>? Settings,
    Dictionary<string, string?>? Secrets,
    List<string>? Events);

public sealed record IntegrationSetup(string InboundUrl, string SignatureHeader, string? SigningSecret, string? FileName, string? Code, IReadOnlyList<string> Steps);

public sealed record InboundResult(int StatusCode, object Body);

public sealed partial class IntegrationService(
    IntegrationRepository repository,
    InvoiceService invoices,
    InvoiceDocumentService documents,
    ITenantRepository tenants,
    RequestTenantContext requestContext,
    ITenantContext context,
    IDataProtectionProvider dataProtection,
    IHttpClientFactory httpFactory,
    IHostEnvironment environment,
    IEnumerable<IMarketplaceClient> marketplaces,
    IAuditTrail audit,
    AppOptions app,
    IClock clock,
    ILogger<IntegrationService> logger)
{
    public const string HttpClientName = "Integrations";
    public const string SignatureHeader = "X-GibFramework-Signature";
    public const string AnonymousConsumer = TaxIdentifier.AnonymousConsumer;

    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
    ];

    private readonly IDataProtector _protector = dataProtection.CreateProtector("GIBFramework.Integrations.v1");

    public string InboundUrl(Guid id) => $"{app.BaseUrl}/api/v1/hooks/{id}";

    public async Task<IReadOnlyList<IntegrationView>> ListAsync(CancellationToken ct) =>
        [.. (await repository.ListAsync(context.RequireTenant(), ct)).Select(View)];

    public async Task<IntegrationView> GetAsync(Guid id, CancellationToken ct) => View(await LoadOwnedAsync(id, ct));

    public async Task<IntegrationView> CreateAsync(SaveIntegrationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var integration = new Integration
        {
            Id = Guid.CreateVersion7(now),
            TenantId = context.RequireTenant(),
            Kind = request.Kind,
            CreatedBy = context.RequireUser(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(integration, request, []);
        await repository.InsertAsync(integration, ct);
        await AuditAsync(integration, "INTEGRATION_CREATED", ct);
        return View(integration);
    }

    public async Task<IntegrationView> UpdateAsync(Guid id, SaveIntegrationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var integration = await LoadOwnedAsync(id, ct);
        if (request.Kind != integration.Kind)
        {
            throw new ValidationFailedException("INTEGRATION_KIND", "Entegrasyon türü değiştirilemez.", []);
        }

        Apply(integration, request, Secrets(integration));
        integration.UpdatedAt = clock.UtcNow;
        await repository.UpdateAsync(integration, ct);
        await AuditAsync(integration, "INTEGRATION_UPDATED", ct);
        return View(integration);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var integration = await LoadOwnedAsync(id, ct);
        await repository.DeleteAsync(integration.TenantId, id, ct);
        await AuditAsync(integration, "INTEGRATION_DELETED", ct);
    }

    public async Task<IntegrationView> RotateSecretAsync(Guid id, CancellationToken ct)
    {
        var integration = await LoadOwnedAsync(id, ct);
        var secrets = Secrets(integration);
        secrets[IntegrationCatalog.SigningSecret] = NewSecret();
        integration.SecretsProtected = _protector.Protect(JsonDefaults.Serialize(secrets));
        integration.UpdatedAt = clock.UtcNow;
        await repository.UpdateAsync(integration, ct);
        await AuditAsync(integration, "INTEGRATION_SECRET_ROTATED", ct);
        return View(integration);
    }

    public async Task<IntegrationSetup> SetupAsync(Guid id, CancellationToken ct)
    {
        var integration = await LoadOwnedAsync(id, ct);
        var info = IntegrationCatalog.Get(integration.Kind);
        var secret = Secrets(integration).GetValueOrDefault(IntegrationCatalog.SigningSecret);
        var url = InboundUrl(integration.Id);
        var (file, code) = integration.Kind switch
        {
            IntegrationKind.Whmcs => ("gibframework.php", HookCode.Whmcs(url, secret ?? string.Empty, integration.Setting("taxIdField") ?? "TC Kimlik / Vergi No")),
            IntegrationKind.WiseCp => ("gibframework.php", HookCode.WiseCp(url, secret ?? string.Empty)),
            IntegrationKind.Webhook => ("ornek-istek.sh", HookCode.Curl(url, secret ?? string.Empty)),
            _ => ((string?)null, (string?)null),
        };
        return new IntegrationSetup(url, info.SignatureHeader, secret, file, code, info.Steps);
    }

    public async Task<IReadOnlyList<IntegrationDelivery>> DeliveriesAsync(Guid id, CancellationToken ct)
    {
        var integration = await LoadOwnedAsync(id, ct);
        return await repository.DeliveriesAsync(integration.TenantId, id, 100, ct);
    }

    public async Task<bool> RetryAsync(Guid id, Guid deliveryId, CancellationToken ct)
    {
        var integration = await LoadOwnedAsync(id, ct);
        return await repository.RetryDeliveryAsync(integration.TenantId, deliveryId, clock.UtcNow, ct);
    }

    public async Task<IntegrationDelivery> TestAsync(Guid id, CancellationToken ct)
    {
        var integration = await LoadOwnedAsync(id, ct);
        if (integration.Setting(IntegrationCatalog.WebhookUrl) is null)
        {
            throw new ValidationFailedException("INTEGRATION_NO_WEBHOOK", "Deneme için önce bildirim adresi (webhook URL) girin.", []);
        }

        var now = clock.UtcNow;
        var delivery = new IntegrationDelivery
        {
            Id = Guid.CreateVersion7(now),
            TenantId = integration.TenantId,
            IntegrationId = integration.Id,
            Direction = DeliveryDirection.Out,
            EventType = "ping",
            Status = DeliveryStatus.Pending,
            RequestBody = JsonDefaults.Serialize(new { id = Guid.NewGuid(), @event = "ping", createdAt = now, tenantId = integration.TenantId, data = new { message = "GIB Framework bağlantı denemesi" } }),
            CreatedAt = now,
            NextAttemptAt = now,
        };
        await repository.InsertDeliveryAsync(delivery, ct);
        delivery.Attempts = 1;
        await DeliverAsync(delivery, ct);
        return (await repository.DeliveriesAsync(integration.TenantId, integration.Id, 20, ct)).First(d => d.Id == delivery.Id);
    }

    public async Task<InboundResult> ReceiveAsync(Guid id, byte[] body, IHeaderDictionary headers, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(headers);
        var integration = await repository.GetAsync(id, ct);
        if (integration is null || !integration.IsActive)
        {
            return new InboundResult(StatusCodes.Status404NotFound, new { error = "Entegrasyon bulunamadı veya kapalı." });
        }

        var now = clock.UtcNow;
        var text = Encoding.UTF8.GetString(body);
        var log = new IntegrationDelivery
        {
            Id = Guid.CreateVersion7(now),
            TenantId = integration.TenantId,
            IntegrationId = integration.Id,
            Direction = DeliveryDirection.In,
            EventType = headers["X-WC-Webhook-Topic"].FirstOrDefault() ?? headers["X-Shopify-Topic"].FirstOrDefault() ?? "order",
            Attempts = 1,
            RequestBody = text,
            CreatedAt = now,
            CompletedAt = now,
        };

        async Task<InboundResult> Finish(DeliveryStatus status, int code, object response, string? error = null)
        {
            log.Status = status;
            log.HttpStatus = code;
            log.Error = error;
            log.ResponseBody = JsonDefaults.Serialize(response);
            await repository.InsertDeliveryAsync(log, CancellationToken.None);
            await repository.TouchAsync(integration.Id, DeliveryDirection.In, now, CancellationToken.None);
            return new InboundResult(code, response);
        }

        if (integration.Kind == IntegrationKind.WooCommerce && text.StartsWith("webhook_id=", StringComparison.Ordinal))
        {
            return await Finish(DeliveryStatus.Ignored, 200, new { ok = true, message = "WooCommerce ping alındı." });
        }

        var secret = Secrets(integration).GetValueOrDefault(IntegrationCatalog.SigningSecret);
        if (string.IsNullOrEmpty(secret) || !VerifySignature(integration.Kind, secret, body, headers))
        {
            return await Finish(DeliveryStatus.Failed, 401, new { error = "İmza doğrulanamadı." }, "İmza doğrulanamadı.");
        }

        MapResult mapped;
        try
        {
            using var doc = JsonDocument.Parse(body);
            mapped = InboundMappers.Map(integration, doc.RootElement, headers["X-Shopify-Topic"].FirstOrDefault());
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return await Finish(DeliveryStatus.Failed, 400, new { error = "Gövde okunamadı: " + ex.Message }, ex.Message);
        }

        if (mapped.Order is not { } order)
        {
            return await Finish(DeliveryStatus.Ignored, 200, new { ok = true, ignored = mapped.IgnoredReason });
        }

        log.ExternalId = Truncate(order.ExternalId, 100);
        var import = await ImportAsync(integration, order, ct);
        log.InvoiceId = import.InvoiceId;
        return await Finish(import.Status, import.HttpStatus, import.Response, import.Error);
    }

    public static readonly IntegrationKind[] MarketplaceKinds = [IntegrationKind.Trendyol, IntegrationKind.Hepsiburada, IntegrationKind.N11];

    public static bool IsMarketplace(IntegrationKind kind) => MarketplaceKinds.Contains(kind);

    public async Task<object> SyncAsync(Guid id, CancellationToken ct) => await SyncCoreAsync(await LoadOwnedAsync(id, ct), ct);

    public async Task<int> SyncDueAsync(CancellationToken ct)
    {
        var count = 0;
        var now = clock.UtcNow;
        foreach (var integration in await repository.ActiveByKindsAsync(MarketplaceKinds, ct))
        {
            var minutes = int.TryParse(integration.Setting("pollMinutes"), out var m) ? Math.Clamp(m, 5, 1440) : 15;
            if (integration.LastInboundAt is { } last && last.AddMinutes(minutes) > now)
            {
                continue;
            }

            try
            {
                await SyncCoreAsync(integration, ct);
                count++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.SyncFailed(logger, integration.Id, ex.Message);
            }
        }

        return count;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> Running = new();

    private async Task<object> SyncCoreAsync(Integration integration, CancellationToken ct)
    {
        if (!IsMarketplace(integration.Kind))
        {
            throw new ValidationFailedException("INTEGRATION_NOT_MARKETPLACE", "Bu entegrasyon sipariş çekmez; siparişler gelen adrese gönderilir.", []);
        }

        if (!Running.TryAdd(integration.Id, 0))
        {
            return new { created = 0, skipped = 0, failed = 0, error = "Bu entegrasyon için eşitleme zaten sürüyor; birkaç saniye sonra tekrar deneyin." };
        }

        try
        {
            return await SyncLockedAsync(integration, ct);
        }
        finally
        {
            Running.TryRemove(integration.Id, out _);
        }
    }

    private async Task<object> SyncLockedAsync(Integration integration, CancellationToken ct)
    {

        var client = marketplaces.First(c => c.Kind == integration.Kind);
        var now = clock.UtcNow;
        var maxDays = integration.Kind == IntegrationKind.Trendyol ? 13 : 14;
        var initialDays = int.TryParse(integration.Setting("initialDays"), out var d) ? Math.Clamp(d, 1, maxDays) : 3;
        var from = (integration.LastInboundAt?.AddHours(-3) ?? now.AddDays(-initialDays));
        if (from < now.AddDays(-maxDays))
        {
            from = now.AddDays(-maxDays);
        }

        var to = now.AddHours(3);
        int created = 0, skipped = 0, failed = 0;
        string? error = null;
        try
        {
            var orders = await client.FetchAsync(integration, Secrets(integration), from, to, ct);
            foreach (var mo in orders)
            {
                var externalId = Truncate(mo.Order.ExternalId, 100);
                if (await repository.OrderExistsAsync(integration.Id, externalId, ct))
                {
                    skipped++;
                    continue;
                }

                var result = await ImportAsync(integration, mo.Order, ct);
                var at = clock.UtcNow;
                await repository.InsertDeliveryAsync(new IntegrationDelivery
                {
                    Id = Guid.CreateVersion7(at),
                    TenantId = integration.TenantId,
                    IntegrationId = integration.Id,
                    Direction = DeliveryDirection.In,
                    EventType = $"{integration.Kind.ToString().ToLowerInvariant()}.order",
                    Status = result.Status,
                    HttpStatus = result.HttpStatus,
                    Attempts = 1,
                    RequestBody = mo.Raw,
                    ResponseBody = JsonDefaults.Serialize(result.Response),
                    Error = result.Error,
                    InvoiceId = result.InvoiceId,
                    ExternalId = externalId,
                    CreatedAt = at,
                    CompletedAt = at,
                }, ct);
                if (result.Status == DeliveryStatus.Succeeded)
                {
                    created++;
                }
                else
                {
                    failed++;
                }
            }
        }
        catch (MarketplaceException ex)
        {
            error = ex.Message;
            await repository.InsertDeliveryAsync(new IntegrationDelivery
            {
                Id = Guid.CreateVersion7(now),
                TenantId = integration.TenantId,
                IntegrationId = integration.Id,
                Direction = DeliveryDirection.In,
                EventType = $"{integration.Kind.ToString().ToLowerInvariant()}.sync",
                Status = DeliveryStatus.Failed,
                Attempts = 1,
                Error = ex.Message,
                CreatedAt = now,
                CompletedAt = clock.UtcNow,
            }, ct);
        }

        await repository.TouchAsync(integration.Id, DeliveryDirection.In, now, ct);
        return new { created, skipped, failed, error, from, to };
    }

    private sealed record ImportResult(DeliveryStatus Status, int HttpStatus, object Response, string? Error, Guid? InvoiceId);

    private async Task<ImportResult> ImportAsync(Integration integration, InboundOrder order, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (order.Lines.Count == 0)
        {
            return new ImportResult(DeliveryStatus.Failed, 422, new { error = "Siparişte satır yok." }, "Siparişte satır yok.", null);
        }

        var key = $"int:{integration.Id:N}:{order.ExternalId}";
        requestContext.SetUser(integration.TenantId, $"entegrasyon:{integration.Name}", null, $"GIBFramework-{integration.Kind}", null, [Roles.ApiClient]);
        try
        {
            var draft = await BuildDraftAsync(integration, order, ct);
            var (invoice, created) = await invoices.CreateDraftAsync(draft, Truncate(key, 100), ct,
                new CustomerCardOptions(
                    (integration.Flag(IntegrationCatalog.SaveCustomer) || integration.Setting(IntegrationCatalog.SaveCustomer) is null) && draft.Customer.TaxId != AnonymousConsumer,
                    null));
            await repository.LinkOrderAsync(integration.Id, Truncate(order.ExternalId, 100), integration.TenantId, invoice.Id, now, ct);
            string? warning = null;
            if (created && (integration.Flag(IntegrationCatalog.AutoSubmit) || integration.Setting(IntegrationCatalog.AutoSubmit) is null))
            {
                try
                {
                    invoice = await invoices.SubmitAsync(invoice.Id, ct);
                }
                catch (ValidationFailedException ex)
                {
                    warning = string.Join(" | ", ex.Details.DefaultIfEmpty(ex.Message));
                }
            }

            return new ImportResult(DeliveryStatus.Succeeded, created ? 201 : 200, new
            {
                ok = true,
                created,
                invoiceId = invoice.Id,
                draftNumber = invoice.DraftNumber,
                status = invoice.Status.ToString(),
                documentType = invoice.DocumentType.ToString(),
                warning,
            }, warning, invoice.Id);
        }
        catch (Exception ex) when (ex is ValidationFailedException or DomainException or ForbiddenOperationException or NotFoundException or ConflictException)
        {
            var details = ex is ValidationFailedException v ? string.Join(" | ", v.Details.DefaultIfEmpty(v.Message)) : ex.Message;
            return new ImportResult(DeliveryStatus.Failed, 422, new { error = ex.Message, details }, details, null);
        }
    }

    public async Task RouteAsync(OutboxEventContext evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var list = (await repository.ListAsync(evt.Invoice.TenantId, ct)).Where(i => i.IsActive).ToList();
        if (list.Count == 0)
        {
            return;
        }

        var links = await repository.OrdersForInvoiceAsync(evt.Invoice.Id, ct);
        var now = clock.UtcNow;
        foreach (var integration in list)
        {
            var orderId = links.Where(l => l.IntegrationId == integration.Id).Select(l => l.ExternalId).FirstOrDefault();
            if (integration.Setting(IntegrationCatalog.WebhookUrl) is not null && integration.Events.Contains(evt.EventType, StringComparer.Ordinal))
            {
                await QueueAsync(integration, evt.EventType, JsonDefaults.Serialize(new
                {
                    id = Guid.NewGuid(),
                    @event = evt.EventType,
                    createdAt = now,
                    tenantId = integration.TenantId,
                    data = Payload(evt, orderId),
                }), evt.Invoice.Id, orderId, now, ct);
            }

            if (IsMarketplace(integration.Kind) && orderId is not null && evt.EventType == InvoiceEvents.Issued && evt.Invoice.DocumentNumber is not null
                && (integration.Flag("sendInvoiceLink") || integration.Setting("sendInvoiceLink") is null))
            {
                var i = evt.Invoice;
                await QueueAsync(integration, $"{integration.Kind.ToString().ToLowerInvariant()}.invoice_link", JsonDefaults.Serialize(new MarketplaceInvoiceLink(
                    orderId,
                    i.OrderNumber,
                    documents.PdfLink(i.TenantId, i.Id, InvoiceDocumentService.ArchiveLinkLifetime),
                    i.DocumentNumber,
                    i.SignedAt ?? i.TransmittedAt ?? now)), i.Id, orderId, now, ct);
            }

            if (integration.Kind == IntegrationKind.WooCommerce && orderId is not null && evt.EventType == InvoiceEvents.Issued
                && (integration.Flag("addOrderNote") || integration.Setting("addOrderNote") is null))
            {
                var i = evt.Invoice;
                var note = $"{InvoiceDocumentService.DocumentTypeLabel(i)} düzenlendi. Belge no: {i.DocumentNumber}, tutar: {InvoiceDocumentService.Money(i.Totals.PayableAmount, i.Currency)}. Faturayı görüntüleyin: {evt.PublicLink}";
                await QueueAsync(integration, "woocommerce.order_note", JsonDefaults.Serialize(new { orderId, note, customer_note = true }), i.Id, orderId, now, ct);
            }
        }
    }

    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var batch = await repository.ClaimDueDeliveriesAsync(20, clock.UtcNow, TimeSpan.FromMinutes(5), ct);
        foreach (var d in batch)
        {
            await DeliverAsync(d, ct);
        }

        return batch.Count;
    }

    public static bool IsAnonymousConsumer(string taxId) => taxId == AnonymousConsumer;

    private async Task DeliverAsync(IntegrationDelivery delivery, CancellationToken ct)
    {
        var integration = await repository.GetAsync(delivery.IntegrationId, ct);
        var now = clock.UtcNow;
        if (integration is null || !integration.IsActive)
        {
            await repository.CompleteDeliveryAsync(delivery.Id, DeliveryStatus.Ignored, null, null, "Entegrasyon kapalı.", null, now, ct);
            return;
        }

        int? status = null;
        string? response = null;
        try
        {
            if (delivery.EventType.EndsWith(".invoice_link", StringComparison.Ordinal) && IsMarketplace(integration.Kind))
            {
                var link = JsonDefaults.Deserialize<MarketplaceInvoiceLink>(delivery.RequestBody ?? "{}");
                response = await marketplaces.First(c => c.Kind == integration.Kind).SendInvoiceLinkAsync(integration, Secrets(integration), link, ct);
                status = 200;
                await repository.CompleteDeliveryAsync(delivery.Id, DeliveryStatus.Succeeded, status, response, null, null, now, ct);
                await repository.TouchAsync(integration.Id, DeliveryDirection.Out, now, ct);
                return;
            }

            using var request = delivery.EventType == "woocommerce.order_note"
                ? await WooNoteRequestAsync(integration, delivery)
                : await WebhookRequestAsync(integration, delivery);
            using var http = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            status = (int)http.StatusCode;
            response = await http.Content.ReadAsStringAsync(ct);
            if (!http.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Karşı sistem HTTP {status} döndürdü.");
            }

            await repository.CompleteDeliveryAsync(delivery.Id, DeliveryStatus.Succeeded, status, response, null, null, now, ct);
            await repository.TouchAsync(integration.Id, DeliveryDirection.Out, now, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException or MarketplaceException)
        {
            var permanent = ex is InvalidOperationException or UriFormatException || ex is MarketplaceException { Transient: false } || status is 400 or 401 or 403 or 404 or 410 or 422;
            DateTimeOffset? retry = !permanent && delivery.Attempts < Backoff.Length ? now.Add(Backoff[Math.Clamp(delivery.Attempts - 1, 0, Backoff.Length - 1)]) : null;
            Log.DeliveryFailed(logger, delivery.Id, ex.Message);
            await repository.CompleteDeliveryAsync(delivery.Id, retry is null ? DeliveryStatus.Failed : DeliveryStatus.Pending, status, response,
                ex is TaskCanceledException ? "Zaman aşımı." : ex.Message, retry, now, CancellationToken.None);
        }
    }

    private async Task<HttpRequestMessage> WebhookRequestAsync(Integration integration, IntegrationDelivery delivery)
    {
        var url = await SafeUriAsync(integration.Setting(IntegrationCatalog.WebhookUrl) ?? throw new InvalidOperationException("Bildirim adresi tanımlı değil."));
        var body = delivery.RequestBody ?? "{}";
        var timestamp = clock.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var secret = Secrets(integration).GetValueOrDefault(IntegrationCatalog.SigningSecret) ?? string.Empty;
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-GibFramework-Event", delivery.EventType);
        request.Headers.Add("X-GibFramework-Delivery", delivery.Id.ToString());
        request.Headers.Add("X-GibFramework-Timestamp", timestamp);
        request.Headers.Add(SignatureHeader, "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"))));
        return request;
    }

    private async Task<HttpRequestMessage> WooNoteRequestAsync(Integration integration, IntegrationDelivery delivery)
    {
        var secrets = Secrets(integration);
        var key = secrets.GetValueOrDefault(IntegrationCatalog.ConsumerKey);
        var secret = secrets.GetValueOrDefault(IntegrationCatalog.ConsumerSecret);
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("WooCommerce REST API anahtarı (Consumer key / secret) girilmemiş.");
        }

        using var doc = JsonDocument.Parse(delivery.RequestBody ?? "{}");
        var orderId = doc.RootElement.GetProperty("orderId").GetString();
        var store = (integration.Setting("storeUrl") ?? throw new InvalidOperationException("Mağaza adresi tanımlı değil.")).TrimEnd('/');
        var url = await SafeUriAsync($"{store}/wp-json/wc/v3/orders/{Uri.EscapeDataString(orderId ?? string.Empty)}/notes");
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonDefaults.Serialize(new { note = doc.RootElement.GetProperty("note").GetString(), customer_note = true }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
        return request;
    }

    private async Task<Uri> SafeUriAsync(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("Adres geçerli bir http(s) URL'si olmalıdır.");
        }

        if (environment.IsDevelopment())
        {
            return uri;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Güvenlik nedeniyle yalnızca HTTPS adreslerine gönderim yapılır.");
        }

        var addresses = IPAddress.TryParse(uri.Host, out var ip) ? [ip] : await Dns.GetHostAddressesAsync(uri.Host);
        if (addresses.Length == 0 || addresses.Any(IsPrivate))
        {
            throw new InvalidOperationException("Adres iç ağa / yerel makineye çözülüyor; gönderim engellendi.");
        }

        return uri;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
        {
            return true;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        return b[0] is 10 or 127 or 0 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] is >= 64 and <= 127);
    }

    private async Task QueueAsync(Integration integration, string eventType, string body, Guid invoiceId, string? externalId, DateTimeOffset now, CancellationToken ct) =>
        await repository.InsertDeliveryAsync(new IntegrationDelivery
        {
            Id = Guid.CreateVersion7(now),
            TenantId = integration.TenantId,
            IntegrationId = integration.Id,
            Direction = DeliveryDirection.Out,
            EventType = eventType,
            Status = DeliveryStatus.Pending,
            RequestBody = body,
            InvoiceId = invoiceId,
            ExternalId = externalId,
            CreatedAt = now,
            NextAttemptAt = now,
        }, ct);

    private static object Payload(OutboxEventContext evt, string? orderId)
    {
        var i = evt.Invoice;
        return new
        {
            invoiceId = i.Id,
            documentNumber = i.DocumentNumber,
            draftNumber = i.DraftNumber,
            documentType = i.DocumentType.ToString(),
            status = i.Status.ToString(),
            issueDate = i.IssueDate,
            currency = i.Currency,
            payableAmount = i.Totals.PayableAmount,
            taxExclusiveAmount = i.Totals.TaxExclusiveAmount,
            vatTotal = i.Totals.VatTotal,
            ettn = i.Uuid,
            orderNumber = i.OrderNumber,
            externalOrderId = orderId,
            customer = new { taxId = i.Customer.TaxId, title = i.Customer.Title, email = i.Customer.Email, phone = i.Customer.Phone },
            note = evt.Note,
            links = new { view = evt.PublicLink, panel = evt.PanelLink },
        };
    }

    private async Task<Invoice> BuildDraftAsync(Integration integration, InboundOrder order, CancellationToken ct)
    {
        var tenant = await tenants.GetAsync(integration.TenantId, ct) ?? throw new NotFoundException("Firma bulunamadı.");
        var c = order.Customer;
        var digits = new string([.. (c.TaxId ?? string.Empty).Where(char.IsAsciiDigit)]);
        var taxId = TaxIdentifier.TryParse(digits, out _, out _) ? digits : AnonymousConsumer;
        var person = taxId.Length == 11;
        var name = c.Name?.Trim();
        string? first = null, family = null;
        if (person && !string.IsNullOrWhiteSpace(name))
        {
            var cut = name.LastIndexOf(' ');
            (first, family) = cut > 0 ? (name[..cut], name[(cut + 1)..]) : (name, name);
        }

        var title = person ? name ?? c.Company ?? "Nihai tüketici" : c.Company ?? name ?? "Alıcı";
        if (person && string.IsNullOrWhiteSpace(first))
        {
            (first, family) = ("Nihai", "Tüketici");
        }

        var party = new InvoiceParty
        {
            TaxId = taxId,
            Kind = person ? PartyKind.NaturalPerson : PartyKind.LegalEntity,
            Regime = person ? BookkeepingRegime.NotATaxpayer : BookkeepingRegime.Bilanco,
            Title = Truncate(title, 250),
            FirstName = first is null ? null : Truncate(first, 100),
            FamilyName = family is null ? null : Truncate(family, 100),
            TaxOffice = c.TaxOffice is null ? null : Truncate(c.TaxOffice, 100),
            Street = c.Address is null ? null : Truncate(c.Address, 250),
            District = c.District is null ? null : Truncate(c.District, 100),
            City = Truncate(c.City ?? (string.IsNullOrWhiteSpace(tenant.Profile.City) ? "İstanbul" : tenant.Profile.City), 100),
            PostalCode = c.PostalCode is null ? null : Truncate(new string([.. c.PostalCode.Where(char.IsAsciiDigit)]), 10),
            Country = c.Country is null or "TR" or "tr" or "Turkey" or "Türkiye" ? "Türkiye" : c.Country,
            Email = MessagingSettingsService.IsEmail(c.Email) ? c.Email : null,
            Phone = c.Phone is null ? null : Truncate(c.Phone, 30),
        };

        var lines = new List<InvoiceLine>();
        var no = 1;
        foreach (var l in order.Lines)
        {
            var divisor = order.PricesIncludeTax ? 1 + (l.VatRate / 100m) : 1m;
            lines.Add(new InvoiceLine
            {
                LineNo = no++,
                Name = Truncate(l.Name, 250),
                Quantity = l.Quantity <= 0 ? 1 : l.Quantity,
                UnitCode = string.IsNullOrWhiteSpace(l.UnitCode) ? "C62" : l.UnitCode,
                UnitPrice = decimal.Round(l.UnitPrice / divisor, 4, MidpointRounding.AwayFromZero),
                DiscountAmount = decimal.Round(l.Discount / divisor, 2, MidpointRounding.AwayFromZero),
                VatRate = l.VatRate,
                VatExemptionCode = l.VatRate == 0 ? "351" : null,
            });
        }

        return new Invoice
        {
            Currency = order.Currency.ToUpperInvariant(),
            Customer = party,
            Lines = lines,
            Notes = [.. order.Notes.Select(n => Truncate(n, 500))],
            OrderNumber = order.OrderNumber is null ? null : Truncate(order.OrderNumber, 50),
            Profile = InvoiceProfile.TEMELFATURA,
            TypeCode = InvoiceTypeCode.SATIS,
        };
    }

    private static bool VerifySignature(IntegrationKind kind, string secret, byte[] body, IHeaderDictionary headers)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, body);
        switch (kind)
        {
            case IntegrationKind.WooCommerce:
                return Same(Convert.ToBase64String(hash), headers["X-WC-Webhook-Signature"].FirstOrDefault());
            case IntegrationKind.Shopify:
                return Same(Convert.ToBase64String(hash), headers["X-Shopify-Hmac-Sha256"].FirstOrDefault());
            default:
                var given = headers[SignatureHeader].FirstOrDefault() ?? string.Empty;
                return Same("sha256=" + Convert.ToHexStringLower(hash), given.Trim().ToLowerInvariant());
        }
    }

    private static bool Same(string expected, string? given) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given.Trim()));

    private void Apply(Integration integration, SaveIntegrationRequest request, Dictionary<string, string> existingSecrets)
    {
        var info = IntegrationCatalog.Get(request.Kind);
        var errors = new List<string>();
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal);
        var secrets = new Dictionary<string, string>(existingSecrets, StringComparer.Ordinal);
        foreach (var field in info.Fields)
        {
            if (field.Type is "secret" or "generated")
            {
                var value = request.Secrets?.GetValueOrDefault(field.Key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    secrets[field.Key] = value.Trim();
                }

                if (field.Type == "generated" && !secrets.ContainsKey(field.Key))
                {
                    secrets[field.Key] = NewSecret();
                }

                if (field.Required && !secrets.ContainsKey(field.Key))
                {
                    errors.Add($"{field.Label} zorunludur.");
                }

                continue;
            }

            var raw = request.Settings?.GetValueOrDefault(field.Key)?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                raw = field.Default;
            }

            if (field.Required && string.IsNullOrWhiteSpace(raw))
            {
                errors.Add($"{field.Label} zorunludur.");
            }

            if (field.Type == "url" && !string.IsNullOrWhiteSpace(raw)
                && (!Uri.TryCreate(raw, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp)))
            {
                errors.Add($"{field.Label} geçerli bir http(s) adresi olmalıdır.");
            }

            if (field.Type == "bool")
            {
                raw = string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            }

            if (field.Options is { } options && raw is not null && !options.Contains(raw))
            {
                errors.Add($"{field.Label} için geçersiz değer.");
            }

            settings[field.Key] = raw is null ? null : Truncate(raw, 500);
        }

        var known = InvoiceEvents.Catalog.Select(e => e.Code).ToHashSet(StringComparer.Ordinal);
        var events = (request.Events ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();
        if (errors.Count > 0)
        {
            throw new ValidationFailedException("INTEGRATION_INVALID", "Entegrasyon kaydedilemedi.", errors);
        }

        integration.Name = request.Name.Trim();
        integration.IsActive = request.IsActive;
        integration.Settings = settings;
        integration.Events = events;
        integration.SecretsProtected = _protector.Protect(JsonDefaults.Serialize(secrets));
    }

    private Dictionary<string, string> Secrets(Integration integration)
    {
        if (string.IsNullOrEmpty(integration.SecretsProtected))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return new Dictionary<string, string>(JsonDefaults.Deserialize<Dictionary<string, string>>(_protector.Unprotect(integration.SecretsProtected)), StringComparer.Ordinal);
        }
        catch (CryptographicException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private IntegrationView View(Integration i)
    {
        var secrets = Secrets(i);
        var info = IntegrationCatalog.Get(i.Kind);
        return new IntegrationView(i.Id, i.Kind, i.Name, i.IsActive, i.Settings, i.Events,
            info.Fields.Where(f => f.Type is "secret" or "generated").ToDictionary(f => f.Key, f => secrets.ContainsKey(f.Key), StringComparer.Ordinal),
            InboundUrl(i.Id), i.CreatedAt, i.UpdatedAt, i.LastInboundAt, i.LastOutboundAt);
    }

    private async Task<Integration> LoadOwnedAsync(Guid id, CancellationToken ct)
    {
        var integration = await repository.GetAsync(id, ct);
        return integration is not null && integration.TenantId == context.RequireTenant() ? integration : throw new NotFoundException("Entegrasyon bulunamadı.");
    }

    private Task AuditAsync(Integration integration, string action, CancellationToken ct) =>
        audit.AppendSystemAsync(integration.TenantId, new AuditEntry(action, "Integration", integration.Id.ToString(), "Success",
            new { integration.Kind, integration.Name, integration.IsActive, by = context.UserId }), null, ct);

    private static string NewSecret() => "gfs_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Entegrasyon gönderimi {DeliveryId} başarısız: {Error}")]
        public static partial void DeliveryFailed(ILogger logger, Guid deliveryId, string error);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Pazaryeri eşitlemesi {IntegrationId} başarısız: {Error}")]
        public static partial void SyncFailed(ILogger logger, Guid integrationId, string error);
    }
}

public sealed record OutboxEventContext(string EventType, Invoice Invoice, string? Note, string PublicLink, string PanelLink);
