using System.Diagnostics;
using Microsoft.Data.SqlClient;
using GIBFramework.Services.Auth;
using GIBFramework.Services.Ubl;

namespace GIBFramework.DAL;

public sealed record ReadinessResult(bool Ready, string? Reason, long DurationMs);

public sealed class ReadinessCheck(
    DatabaseOptions database,
    DatabaseInitialization initialization,
    SchemaVerifier schema,
    IHostEnvironment environment,
    DemoOptions demo,
    SigningOptions signing,
    ProviderOptions providers,
    GibPortalOptions portal,
    UblValidator ubl)
{
    public async Task<ReadinessResult> CheckAsync(CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        ReadinessResult Result(string? reason = null) => new(reason is null, reason, timer.ElapsedMilliseconds);

        if (string.IsNullOrWhiteSpace(database.ConnectionString))
        {
            return Result("Veritabanı bağlantısı yapılandırılmadı.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            await schema.VerifyAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result("Veritabanı hazırlık denetimi zaman aşımına uğradı.");
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
        {
            return Result("Veritabanına bağlanılamadı veya uygulama şeması güncel değil.");
        }

        if (!initialization.Ready)
        {
            return Result("Veritabanı başlangıç işlemi tamamlanmadı.");
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Demo"))
        {
            return Result();
        }

        if (demo.Enabled || signing.Mode != SigningMode.WindowsStore || !providers.PortalOnly)
        {
            return Result("Üretim için demo kapalı, imza modu WindowsStore ve sağlayıcı modu PortalOnly olmalıdır.");
        }

        if (!portal.Enabled || !portal.AllowProduction || signing.TrustedRootSha256.Length == 0
            || signing.TrustedRootSha256.Any(hash => hash.Length != 64 || !hash.All(Uri.IsHexDigit)))
        {
            return Result("Üretim portalı ve güvenilen kök sertifikaların SHA-256 özetleri yapılandırılmalıdır.");
        }

        try
        {
            if (!ubl.ProductionValidationAvailable)
            {
                return Result("Üretim için sürüm ve SHA-256 özeti sabitlenmiş UBL doğrulama paketi gereklidir.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.Xml.XmlException or System.Xml.Schema.XmlSchemaException or System.Xml.Xsl.XsltException)
        {
            return Result("UBL doğrulama paketi yüklenemedi.");
        }

        return Result();
    }
}
