using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.Identity;

namespace GIBFramework.DAL.Identity;

public interface IUserRepository
{
    Task<UserAccount?> FindByCodeAsync(string userCode, CancellationToken cancellationToken);

    Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserAccount>> ListAsync(Guid? tenantId, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    Task InsertAsync(UserAccount user, CancellationToken cancellationToken);

    Task UpdateAsync(UserAccount user, CancellationToken cancellationToken);

    Task<Guid?> GetSecurityStampAsync(string userCode, CancellationToken cancellationToken);
}

public sealed class UserRepository(SqlConnectionFactory connections, Services.Auth.SecurityStampCache stamps) : IUserRepository
{
    public async Task<UserAccount?> FindByCodeAsync(string userCode, CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = conn.Command("SELECT * FROM dbo.Users WHERE UserCode = @c");
        cmd.With("@c", Normalize(userCode));
        return (await ReadAsync(cmd, cancellationToken)).FirstOrDefault();
    }

    public async Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = conn.Command("SELECT * FROM dbo.Users WHERE Id = @id");
        cmd.With("@id", id);
        return (await ReadAsync(cmd, cancellationToken)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<UserAccount>> ListAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = conn.Command("SELECT * FROM dbo.Users WHERE @t IS NULL OR TenantId = @t ORDER BY UserCode");
        cmd.With("@t", tenantId);
        return await ReadAsync(cmd, cancellationToken);
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = conn.Command("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users) THEN 1 ELSE 0 END");
        return (int)(await cmd.ExecuteScalarAsync(cancellationToken))! == 1;
    }

    public async Task InsertAsync(UserAccount user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        await using var conn = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = conn.Command("""
            INSERT dbo.Users (Id, TenantId, UserCode, DisplayName, Email, Phone, PasswordHash, RolesCsv, IsActive, MustChangePassword, FailedLoginCount,
                              LockoutUntil, TotpSecretProtected, TotpEnabled, TotpLastStep, SecurityStamp, CreatedAt, UpdatedAt, LastLoginAt)
            VALUES (@id, @t, @c, @n, @e, @ph, @h, @r, @a, @m, @f, @l, @ts, @te, @tl, @s, @ca, @ua, @ll)
            """);
        Bind(cmd, user);
        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new ConflictException("USER_EXISTS", $"'{user.UserCode}' kullanıcı kodu zaten kullanılıyor.");
        }
    }

    public async Task UpdateAsync(UserAccount user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        await using var conn = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = conn.Command("""
            UPDATE dbo.Users SET TenantId = @t, DisplayName = @n, Email = @e, Phone = @ph, PasswordHash = @h, RolesCsv = @r, IsActive = @a,
                   MustChangePassword = @m, FailedLoginCount = @f, LockoutUntil = @l, TotpSecretProtected = @ts, TotpEnabled = @te,
                   TotpLastStep = @tl, SecurityStamp = @s, UpdatedAt = @ua, LastLoginAt = @ll
             WHERE Id = @id AND UserCode = @c
            """);
        Bind(cmd, user);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        stamps.Invalidate(user.UserCode);
    }

    public async Task<Guid?> GetSecurityStampAsync(string userCode, CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = conn.Command("SELECT SecurityStamp FROM dbo.Users WHERE UserCode = @c AND IsActive = 1");
        return (Guid?)await cmd.With("@c", Normalize(userCode)).ExecuteScalarAsync(cancellationToken);
    }

    public static string Normalize(string userCode) => (userCode ?? string.Empty).Trim().ToLowerInvariant();

    private static void Bind(SqlCommand cmd, UserAccount u) =>
        cmd.With("@id", u.Id).With("@t", u.TenantId).With("@c", Normalize(u.UserCode)).With("@n", u.DisplayName).With("@e", u.Email).With("@ph", u.Phone)
            .With("@h", u.PasswordHash).With("@r", string.Join(',', u.Roles)).With("@a", u.IsActive).With("@m", u.MustChangePassword)
            .With("@f", u.FailedLoginCount).With("@l", u.LockoutUntil).With("@ts", u.TotpSecretProtected).With("@te", u.TotpEnabled)
            .With("@tl", u.TotpLastStep).With("@s", u.SecurityStamp).With("@ca", u.CreatedAt).With("@ua", u.UpdatedAt).With("@ll", u.LastLoginAt);

    private static async Task<List<UserAccount>> ReadAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<UserAccount>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new UserAccount
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.IsDBNull(r.GetOrdinal("TenantId")) ? null : r.Get<Guid>("TenantId"),
                UserCode = r.Get<string>("UserCode"),
                DisplayName = r.Get<string>("DisplayName"),
                Email = r.GetNullableString("Email"),
                Phone = r.GetNullableString("Phone"),
                PasswordHash = r.Get<string>("PasswordHash"),
                Roles = [.. r.Get<string>("RolesCsv").Split(',', StringSplitOptions.RemoveEmptyEntries)],
                IsActive = r.Get<bool>("IsActive"),
                MustChangePassword = r.Get<bool>("MustChangePassword"),
                FailedLoginCount = r.Get<int>("FailedLoginCount"),
                LockoutUntil = r.GetNullableDateTimeOffset("LockoutUntil"),
                TotpSecretProtected = r.GetNullableString("TotpSecretProtected"),
                TotpEnabled = r.Get<bool>("TotpEnabled"),
                TotpLastStep = r.IsDBNull(r.GetOrdinal("TotpLastStep")) ? null : r.Get<long>("TotpLastStep"),
                SecurityStamp = r.Get<Guid>("SecurityStamp"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
                UpdatedAt = r.Get<DateTimeOffset>("UpdatedAt"),
                LastLoginAt = r.GetNullableDateTimeOffset("LastLoginAt"),
            });
        }

        return list;
    }
}
