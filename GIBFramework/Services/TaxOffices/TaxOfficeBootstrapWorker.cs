using GIBFramework.DAL.TaxOffices;
using GIBFramework.Infrastructure.Tenancy;

namespace GIBFramework.Services.TaxOffices;

public sealed class TaxOfficeOptions
{
    public const string Section = "TaxOffices";

    public bool AutoLoad { get; set; } = true;

    public string SourceUrl { get; set; } =
        "https://cdn.gib.gov.tr/api/gibportal-file/file/getFileResources?objectKey=arsiv%2Fyardim-kaynaklar%2Fyararli-bilgiler%2FDefterdarl%C4%B1kveVergiDaireleriListesi.pdf";

    public string SourceFile { get; set; } = string.Empty;
}

public sealed partial class TaxOfficeBootstrapWorker(
    IServiceScopeFactory scopes,
    IHttpClientFactory httpFactory,
    TaxOfficeOptions options,
    IHostEnvironment environment,
    ILogger<TaxOfficeBootstrapWorker> logger) : BackgroundService
{
    public const string HttpClientName = "GibFiles";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using (var check = scopes.CreateAsyncScope())
            {
                var repository = check.ServiceProvider.GetRequiredService<ITaxOfficeRepository>();
                if ((await repository.GetAllVersionsAsync(stoppingToken)).Count > 0)
                {
                    return;
                }
            }

            var (content, fileName) = await LoadAsync(stoppingToken);
            await using var scope = scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<RequestTenantContext>();
            var service = scope.ServiceProvider.GetRequiredService<TaxOfficeImportService>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            context.SetUser(null, "sistem:gib-aktarim", null, null, null);
            var imported = await service.ImportAsync(content, fileName, new Uri(options.SourceUrl), null, stoppingToken);
            if (imported.Stored.Snapshot.ValidationStatus != Models.TaxOffices.SnapshotValidationStatus.Validated)
            {
                Log.Invalid(logger, imported.Stored.Snapshot.Id);
                return;
            }

            context.SetUser(null, "sistem:onay", null, null, null);
            await service.ApproveAsync(imported.Stored.Snapshot.Id, clock.TurkeyToday, stoppingToken);
            Log.Loaded(logger, imported.Stored.Records.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Failed(logger, ex);
        }
    }

    private async Task<(byte[] Content, string FileName)> LoadAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.SourceFile))
        {
            var path = Path.GetFullPath(options.SourceFile, environment.ContentRootPath);
            return (await File.ReadAllBytesAsync(path, ct), Path.GetFileName(path));
        }

        var client = httpFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(120);
        return (await client.GetByteArrayAsync(new Uri(options.SourceUrl), ct), "DefterdarlikVeVergiDaireleriListesi.pdf");
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "GİB vergi dairesi listesi veritabanına aktarıldı: {Count} kayıt.")]
        public static partial void Loaded(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Warning, Message = "GİB vergi dairesi listesi doğrulanamadı (snapshot {SnapshotId}).")]
        public static partial void Invalid(ILogger logger, Guid snapshotId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "GİB vergi dairesi listesi aktarılamadı.")]
        public static partial void Failed(ILogger logger, Exception exception);
    }
}
