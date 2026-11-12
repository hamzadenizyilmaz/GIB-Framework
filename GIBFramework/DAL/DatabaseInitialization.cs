using Microsoft.Data.SqlClient;
using GIBFramework.Infrastructure.Tenancy;
using GIBFramework.Services.Auth;

namespace GIBFramework.DAL;

public sealed class DatabaseInitialization(
    DatabaseOptions options,
    SchemaVerifier schema,
    IServiceScopeFactory scopes,
    ILogger<DatabaseInitialization> logger) : BackgroundService
{
    private readonly TaskCompletionSource _initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool Configured => !string.IsNullOrWhiteSpace(options.ConnectionString);
    public bool Ready => _initialized.Task.IsCompletedSuccessfully;
    public Task WaitAsync(CancellationToken ct) => _initialized.Task.WaitAsync(ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Configured) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await schema.VerifyAsync(deadline.Token);
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<RequestTenantContext>().UseSystem();
                await scope.ServiceProvider.GetRequiredService<UserBootstrapper>().EnsureAdminAsync(deadline.Token);
                await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().EnsureAsync(deadline.Token);
                _initialized.TrySetResult();
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested
                && ex is SqlException or InvalidOperationException or ArgumentException or ConflictException or OperationCanceledException)
            {
                logger.LogWarning("Veritabanı başlangıç işlemi tamamlanamadı ({ErrorType}). Yeniden denenecek.", ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
