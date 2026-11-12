using System.Data;
using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;

namespace GIBFramework.DAL;

public sealed class SqlConnectionFactory(DatabaseOptions options, ITenantContext tenant)
{
    public Task<SqlConnection> OpenAsync(CancellationToken cancellationToken) =>
        OpenCoreAsync(tenant.TenantId, tenant.IsSystem, cancellationToken);

    public Task<SqlConnection> OpenForTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        OpenCoreAsync(tenantId, system: false, cancellationToken);

    public Task<SqlConnection> OpenSystemAsync(CancellationToken cancellationToken) =>
        OpenCoreAsync(null, system: true, cancellationToken);

    public async Task<IAsyncDisposable> AcquireTenantLockAsync(Guid tenantId, string resource, CancellationToken ct)
    {
        var connection = await OpenForTenantAsync(tenantId, ct);
        var name = $"{tenantId:N}:{resource}";
        try
        {
            await using var cmd = connection.Command("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = @name, @LockMode = 'Exclusive',
                    @LockOwner = 'Session', @LockTimeout = 0;
                SELECT @result;
                """);
            if (await cmd.With("@name", name).ExecuteScalarAsync(ct) is not int result || result < 0)
            {
                throw new ConflictException("OPERATION_IN_PROGRESS", "Bu kayıt için başka bir işlem devam ediyor.");
            }

            return new TenantLock(connection, name);
        }
        catch (Exception ex)
        {
            if (ex is not ConflictException) SqlConnection.ClearPool(connection);
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class TenantLock(SqlConnection connection, string name) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using var ownedConnection = connection;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await using var cmd = connection.Command("EXEC sys.sp_releaseapplock @Resource = @name, @LockOwner = 'Session';");
                await cmd.With("@name", name).ExecuteNonQueryAsync(deadline.Token);
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or OperationCanceledException)
            {
                SqlConnection.ClearPool(connection);
            }
        }
    }

    public async Task<DbScope> BeginAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return new DbScope(connection, tx);
    }

    private async Task<SqlConnection> OpenCoreAsync(Guid? tenantId, bool system, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:GibFramework yapılandırılmamış.");
        }

        var connection = new SqlConnection(options.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var cmd = connection.Command(
                "EXEC sp_set_session_context @key = N'TenantId', @value = @t; EXEC sp_set_session_context @key = N'SystemContext', @value = @s;");
            cmd.Parameters.Add(new SqlParameter("@t", SqlDbType.UniqueIdentifier) { Value = (object?)tenantId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@s", SqlDbType.Bit) { Value = system });
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
