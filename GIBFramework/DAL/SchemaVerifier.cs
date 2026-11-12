using Microsoft.Data.SqlClient;

namespace GIBFramework.DAL;

public sealed class SchemaVerifier(DatabaseOptions options)
{
    public const int RequiredVersion = 5;

    private const string InstallHint = "Boş veritabanında database/GIBFramework_Plesk.sql dosyasını çalıştırın.";
    private const string UpgradeHint = "Şema 4 için database/06_Sema5.sql dosyasını ALTER ANY SECURITY POLICY yetkili bir veritabanı yöneticisi çalıştırmalıdır.";

    public async Task VerifyAsync(CancellationToken cancellationToken)
    {
        int? version;
        try
        {
            await using var connection = new SqlConnection(options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand("SELECT MAX(Version) FROM dbo.SchemaInfo", connection);
            var value = await cmd.ExecuteScalarAsync(cancellationToken);
            version = value is int v ? v : null;
        }
        catch (SqlException ex) when (ex.Number is 4060 or 208)
        {
            throw new InvalidOperationException($"GIB Framework veritabanı veya şeması bulunamadı. {InstallHint}", ex);
        }

        if (version is null || version < RequiredVersion)
        {
            var hint = version is null ? InstallHint : UpgradeHint;
            throw new InvalidOperationException(
                $"Veritabanı şema sürümü {version?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "yok"}; en az {RequiredVersion} gerekli. {hint}");
        }
    }
}
