using Microsoft.Data.SqlClient;

namespace GIBFramework.DAL;

public sealed class SchemaVerifier(DatabaseOptions options)
{
    public const int RequiredVersion = 4;

    private const string Hint = "database/GIBFramework_TamKurulum.sql dosyasını çalıştırın (veya: powershell -File database/Kurulum.ps1).";

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
            throw new InvalidOperationException($"GIB Framework veritabanı veya şeması bulunamadı. {Hint}", ex);
        }

        if (version is null || version < RequiredVersion)
        {
            throw new InvalidOperationException(
                $"Veritabanı şema sürümü {version?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "yok"}; en az {RequiredVersion} gerekli. {Hint}");
        }
    }
}
