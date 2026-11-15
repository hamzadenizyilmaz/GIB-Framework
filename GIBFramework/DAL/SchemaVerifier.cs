using Microsoft.Data.SqlClient;

namespace GIBFramework.DAL;

public sealed class SchemaVerifier(DatabaseOptions options)
{
    public const int RequiredVersion = 7;

    private const string InstallHint = "Boş veritabanında database/GIBFramework-Plesk-Install.sql dosyasını çalıştırın.";
    private const string UpgradeHint = "Şema 4 için 06, şema 5 için 07, şema 6 için 08-Upgrade-Schema.sql betiğinden başlayarak yükseltmeleri sırayla çalıştırın.";

    public async Task VerifyAsync(CancellationToken cancellationToken)
    {
        int? version;
        try
        {
            await using var connection = new SqlConnection(options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand("""
                SELECT MAX(Version) FROM dbo.SchemaInfo;
                SELECT CASE WHEN
                    (SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID()) >= 130
                    AND COL_LENGTH(N'dbo.Users', N'RowVersion') IS NOT NULL
                    AND COL_LENGTH(N'dbo.AuditEvents', N'DataJson') IS NOT NULL
                    AND COL_LENGTH(N'dbo.OutboxEvents', N'LeaseId') IS NOT NULL
                    AND OBJECT_ID(N'dbo.PasswordResetIntake', N'U') IS NOT NULL
                    AND COL_LENGTH(N'dbo.IntegrationDeliveries', N'LeaseId') IS NOT NULL
                    AND OBJECT_ID(N'dbo.TR_AuditEvents_AppendOnly', N'TR') IS NOT NULL
                    AND (OBJECT_ID(N'dbo.TenantIsolation', N'SP') IS NULL
                        OR EXISTS (SELECT 1 FROM sys.security_policies WHERE object_id = OBJECT_ID(N'dbo.TenantIsolation', N'SP') AND is_enabled = 1))
                    THEN 1 ELSE 0 END;
                """, connection);
            cmd.CommandTimeout = options.CommandTimeoutSeconds;
            await using var results = await cmd.ExecuteReaderAsync(cancellationToken);
            version = await results.ReadAsync(cancellationToken) && !results.IsDBNull(0) ? results.GetInt32(0) : null;
            if (version >= RequiredVersion && (!await results.NextResultAsync(cancellationToken)
                || !await results.ReadAsync(cancellationToken) || results.GetInt32(0) != 1))
            {
                throw new InvalidOperationException("SQL şema kaydı güncel, ancak gerekli alanlar veya satır güvenliği bulunamadı. SQL şemasını doğrulayın.");
            }
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
