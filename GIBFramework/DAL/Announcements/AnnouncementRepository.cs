using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.Announcements;

namespace GIBFramework.DAL.Announcements;

public interface IAnnouncementRepository
{
    Task<IReadOnlyList<Announcement>> ListActiveAsync(DateTimeOffset now, bool authenticated, Guid? tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Announcement>> ListAsync(CancellationToken cancellationToken);

    Task<Announcement?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(Announcement announcement, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(Announcement announcement, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class AnnouncementRepository(SqlConnectionFactory connections) : IAnnouncementRepository
{
    private const string Columns = "Id, Title, Message, Level, Audience, TenantId, StartsAt, EndsAt, IsActive, CreatedBy, CreatedAt, UpdatedAt";

    public async Task<IReadOnlyList<Announcement>> ListActiveAsync(DateTimeOffset now, bool authenticated, Guid? tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command($"""
            SELECT TOP (20) {Columns} FROM dbo.Announcements
            WHERE IsActive = 1 AND StartsAt <= @now AND (EndsAt IS NULL OR EndsAt > @now)
              AND (Audience = 'Everyone'
                   OR (Audience = 'Authenticated' AND @auth = 1)
                   OR (Audience = 'Tenant' AND @auth = 1 AND TenantId = @tenant))
            ORDER BY StartsAt DESC, CreatedAt DESC
            """);
        cmd.With("@now", now).With("@auth", authenticated).With("@tenant", tenantId);
        return await ReadAllAsync(cmd, cancellationToken);
    }

    public async Task<IReadOnlyList<Announcement>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command($"SELECT TOP (500) {Columns} FROM dbo.Announcements ORDER BY CreatedAt DESC");
        return await ReadAllAsync(cmd, cancellationToken);
    }

    public async Task<Announcement?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command($"SELECT {Columns} FROM dbo.Announcements WHERE Id = @id");
        cmd.With("@id", id);
        return (await ReadAllAsync(cmd, cancellationToken)).SingleOrDefault();
    }

    public async Task CreateAsync(Announcement announcement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command($"""
            INSERT dbo.Announcements ({Columns})
            VALUES (@id, @title, @msg, @level, @aud, @tenant, @starts, @ends, @active, @by, @created, @updated)
            """);
        await Bind(cmd, announcement).ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Announcement announcement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("""
            UPDATE dbo.Announcements
            SET Title = @title, Message = @msg, Level = @level, Audience = @aud, TenantId = @tenant,
                StartsAt = @starts, EndsAt = @ends, IsActive = @active, UpdatedAt = @updated
            WHERE Id = @id
            """);
        return await Bind(cmd, announcement).ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenSystemAsync(cancellationToken);
        await using var cmd = connection.Command("DELETE dbo.Announcements WHERE Id = @id");
        return await cmd.With("@id", id).ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static SqlCommand Bind(SqlCommand cmd, Announcement a) =>
        cmd.With("@id", a.Id).With("@title", a.Title).With("@msg", a.Message).With("@level", a.Level.ToString())
            .With("@aud", a.Audience.ToString()).With("@tenant", a.TenantId).With("@starts", a.StartsAt).With("@ends", a.EndsAt)
            .With("@active", a.IsActive).With("@by", a.CreatedBy).With("@created", a.CreatedAt).With("@updated", a.UpdatedAt);

    private static async Task<IReadOnlyList<Announcement>> ReadAllAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<Announcement>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new Announcement(
                r.Get<Guid>("Id"),
                r.Get<string>("Title"),
                r.Get<string>("Message"),
                Enum.Parse<AnnouncementLevel>(r.Get<string>("Level")),
                Enum.Parse<AnnouncementAudience>(r.Get<string>("Audience")),
                r.IsDBNull(r.GetOrdinal("TenantId")) ? null : r.Get<Guid>("TenantId"),
                r.Get<DateTimeOffset>("StartsAt"),
                r.GetNullableDateTimeOffset("EndsAt"),
                r.Get<bool>("IsActive"),
                r.Get<string>("CreatedBy"),
                r.Get<DateTimeOffset>("CreatedAt"),
                r.Get<DateTimeOffset>("UpdatedAt")));
        }

        return list;
    }
}
