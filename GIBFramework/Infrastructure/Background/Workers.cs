using GIBFramework.DAL;
using GIBFramework.DAL.Invoices;
using GIBFramework.DAL.LegalSources;
using GIBFramework.Infrastructure.Tenancy;
using GIBFramework.Models.Audit;
using GIBFramework.Services.Invoices;
using GIBFramework.Services.TaxOffices;

namespace GIBFramework.Infrastructure.Background;

public abstract partial class PeriodicWorker(IServiceScopeFactory scopes, ILogger logger) : BackgroundService
{
    protected abstract TimeSpan Interval { get; }

    protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<RequestTenantContext>().UseSystem();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Failed(logger, GetType().Name, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "{Worker} turu başarısız.")]
        public static partial void Failed(ILogger logger, string worker, Exception exception);
    }
}

public sealed class DispatcherWorker(IServiceScopeFactory scopes, WorkerOptions options, IClock clock, ILogger<DispatcherWorker> logger)
    : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(options.DispatcherIntervalSeconds);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var connections = services.GetRequiredService<SqlConnectionFactory>();
        var invoices = services.GetRequiredService<IInvoiceRepository>();
        IReadOnlyList<(Guid TenantId, Guid Id)> batch;
        await using (var conn = await connections.OpenSystemAsync(ct))
        {
            batch = await invoices.FindByStatusAsync(conn, [DocumentStatus.Queued], clock.UtcNow, 20, ct);
        }

        var service = services.GetRequiredService<InvoiceService>();
        foreach (var (tenantId, id) in batch)
        {
            await service.TransmitAsync(tenantId, id, Actor.System, ct);
        }
    }
}

public sealed class ReconciliationWorker(IServiceScopeFactory scopes, WorkerOptions options, IClock clock, ILogger<ReconciliationWorker> logger)
    : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(options.ReconciliationIntervalMinutes);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var connections = services.GetRequiredService<SqlConnectionFactory>();
        var invoices = services.GetRequiredService<IInvoiceRepository>();
        IReadOnlyList<(Guid TenantId, Guid Id)> batch;
        await using (var conn = await connections.OpenSystemAsync(ct))
        {
            batch = await invoices.FindByStatusAsync(
                conn,
                [DocumentStatus.Transmitting, DocumentStatus.InDoubt, DocumentStatus.Sent, DocumentStatus.Acknowledged],
                clock.UtcNow.AddMinutes(-1),
                50,
                ct);
        }

        var service = services.GetRequiredService<InvoiceService>();
        foreach (var (tenantId, id) in batch)
        {
            await service.RefreshStatusAsync(tenantId, id, Actor.System, ct);
        }
    }
}

public sealed partial class LegalSourceWatcher(
    IServiceScopeFactory scopes,
    WorkerOptions options,
    IHttpClientFactory http,
    RuleSet rules,
    IClock clock,
    ILogger<LegalSourceWatcher> logger) : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromHours(options.LegalSourceWatcherIntervalHours);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var checks = services.GetRequiredService<ILegalSourceCheckRepository>();
        var audit = services.GetRequiredService<IAuditTrail>();
        var client = http.CreateClient(nameof(LegalSourceWatcher));

        foreach (var source in rules.Sources.Where(s => s.Uri is not null))
        {
            var previous = await checks.GetLastHashAsync(source.Code, ct) ?? source.Sha256;
            try
            {
                using var response = await client.GetAsync(source.Uri, ct);
                var bytes = response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(ct) : null;
                var hash = bytes is null ? null : Hashing.Sha256Hex(bytes);
                var changed = hash is not null && previous is not null && !string.Equals(hash, previous, StringComparison.OrdinalIgnoreCase);
                await checks.InsertAsync(new LegalSourceCheck(0, source.Code, source.Uri!.ToString(), clock.UtcNow, (int)response.StatusCode, hash, previous, changed, null), ct);

                if (changed)
                {
                    Log.Changed(logger, source.Code, previous!, hash!);
                    await audit.AppendSystemAsync(TaxOfficeImportService.PlatformTenantId,
                        new AuditEntry("LEGAL_SOURCE_CHANGED", "LegalSource", source.Code, "Success", new { previous, current = hash, source.Uri }), null, ct);
                }
            }
            catch (HttpRequestException ex)
            {
                await checks.InsertAsync(new LegalSourceCheck(0, source.Code, source.Uri!.ToString(), clock.UtcNow, null, null, previous, false, ex.Message), ct);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "LEGAL SOURCE CHANGED: {Source} (önceki: {Previous}, yeni: {Current}). Kural incelemesi gerekli.")]
        public static partial void Changed(ILogger logger, string source, string previous, string current);
    }
}

public sealed partial class CertificateExpiryMonitor(
    IServiceScopeFactory scopes,
    ISigningCertificateProvider certificates,
    SigningOptions options,
    IClock clock,
    ILogger<CertificateExpiryMonitor> logger) : PeriodicWorker(scopes, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromHours(12);

    public static int DaysLeft(DateTime notAfter, DateTimeOffset now) => (int)Math.Floor((notAfter.ToUniversalTime() - now.UtcDateTime).TotalDays);

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var cert = certificates.GetSigningCertificate();
        var days = DaysLeft(cert.NotAfter, clock.UtcNow);
        var threshold = options.ExpiryAlertDays.Where(d => days <= d).DefaultIfEmpty(-1).Min();
        if (days < 0)
        {
            Log.Expired(logger, cert.Subject, cert.NotAfter);
        }
        else if (threshold >= 0)
        {
            Log.Expiring(logger, cert.Subject, days, threshold);
        }

        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Critical, Message = "İmza sertifikasının süresi DOLDU: {Subject} ({NotAfter}). Belge imzalanamaz.")]
        public static partial void Expired(ILogger logger, string subject, DateTime notAfter);

        [LoggerMessage(Level = LogLevel.Warning, Message = "İmza sertifikası {Days} gün içinde dolacak ({Threshold} gün eşiği): {Subject}")]
        public static partial void Expiring(ILogger logger, string subject, int days, int threshold);
    }
}
