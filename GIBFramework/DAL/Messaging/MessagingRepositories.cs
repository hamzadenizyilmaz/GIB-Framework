using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.Integrations;
using GIBFramework.Models.Messaging;

namespace GIBFramework.DAL.Messaging;

public sealed record TenantLogo(string ContentType, byte[] Data, string Sha256, DateTimeOffset UpdatedAt);

public sealed record MessageQuery(MessageChannel? Channel, MessageStatus? Status, string? Search, int Take);

public sealed class OutboxEventRepository(SqlConnectionFactory connections)
{
    private const string InsertSql = """
        INSERT dbo.OutboxEvents (TenantId, EventType, EntityType, EntityId, PayloadJson, CreatedAt)
        VALUES (@t, @type, @et, @eid, @p, @at)
        """;

    public async Task EnqueueAsync(DbScope scope, Guid? tenantId, string eventType, string entityType, string entityId, string payloadJson, DateTimeOffset at, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        await using var cmd = scope.Connection.Command(InsertSql, scope.Transaction);
        await Bind(cmd, tenantId, eventType, entityType, entityId, payloadJson, at).ExecuteNonQueryAsync(ct);
    }

    public async Task EnqueueAsync(Guid? tenantId, string eventType, string entityType, string entityId, string payloadJson, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command(InsertSql);
        await Bind(cmd, tenantId, eventType, entityType, entityId, payloadJson, at).ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxEvent>> PendingAsync(int take, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT TOP (@n) * FROM dbo.OutboxEvents WHERE ProcessedAt IS NULL ORDER BY Id");
        cmd.With("@n", take);
        var list = new List<OutboxEvent>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new OutboxEvent(
                r.Get<long>("Id"),
                r.IsDBNull(r.GetOrdinal("TenantId")) ? null : r.Get<Guid>("TenantId"),
                r.Get<string>("EventType"),
                r.Get<string>("EntityType"),
                r.Get<string>("EntityId"),
                r.Get<string>("PayloadJson"),
                r.Get<DateTimeOffset>("CreatedAt"),
                r.Get<int>("Attempts")));
        }

        return list;
    }

    public async Task CompleteAsync(long id, string? error, DateTimeOffset at, bool finished, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.OutboxEvents
               SET Attempts = Attempts + 1, LastError = @e, ProcessedAt = CASE WHEN @f = 1 THEN @at ELSE NULL END
             WHERE Id = @id
            """);
        await cmd.With("@id", id).With("@e", error is null ? null : Truncate(error, 2000)).With("@f", finished).With("@at", at).ExecuteNonQueryAsync(ct);
    }

    private static SqlCommand Bind(SqlCommand cmd, Guid? tenantId, string eventType, string entityType, string entityId, string payloadJson, DateTimeOffset at) =>
        cmd.With("@t", tenantId).With("@type", eventType).With("@et", entityType).With("@eid", entityId).WithMax("@p", payloadJson).With("@at", at);

    internal static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

public sealed class MessageRepository(SqlConnectionFactory connections)
{
    public async Task InsertAsync(OutboundMessage m, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(m);
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            INSERT dbo.Messages (Id, TenantId, Channel, Recipient, RecipientName, Subject, Body, TemplateKey, EntityType, EntityId, AttachmentsJson,
                                 Status, Attempts, NextAttemptAt, CreatedBy, CreatedAt)
            VALUES (@id, @t, @ch, @to, @name, @sub, @body, @tk, @et, @eid, @x, @st, 0, @next, @by, @at)
            """);
        await cmd.With("@id", m.Id).With("@t", m.TenantId).With("@ch", m.Channel.ToString()).With("@to", m.Recipient).With("@name", m.RecipientName)
            .With("@sub", m.Subject).WithMax("@body", m.Body).With("@tk", m.TemplateKey).With("@et", m.EntityType).With("@eid", m.EntityId)
            .WithMax("@x", JsonDefaults.Serialize(m.Extras)).With("@st", m.Status.ToString()).With("@next", m.NextAttemptAt)
            .With("@by", m.CreatedBy).With("@at", m.CreatedAt).ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<OutboundMessage>> ClaimDueAsync(int take, DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            WITH due AS (
                SELECT TOP (@n) * FROM dbo.Messages WITH (ROWLOCK, UPDLOCK, READPAST)
                 WHERE Status IN ('Queued','Sending') AND NextAttemptAt <= @now
                 ORDER BY NextAttemptAt)
            UPDATE due SET Status = 'Sending', Attempts = Attempts + 1, NextAttemptAt = @lease
            OUTPUT inserted.*
            """);
        cmd.With("@n", take).With("@now", now).With("@lease", now.Add(lease));
        return await ReadAllAsync(cmd, ct);
    }

    public async Task MarkSentAsync(Guid id, string? providerId, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("UPDATE dbo.Messages SET Status = 'Sent', SentAt = @at, ProviderMessageId = @p, LastError = NULL WHERE Id = @id");
        await cmd.With("@id", id).With("@at", at).With("@p", providerId is null ? null : OutboxEventRepository.Truncate(providerId, 200)).ExecuteNonQueryAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string error, DateTimeOffset? retryAt, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.Messages
               SET Status = CASE WHEN @retry IS NULL THEN 'Failed' ELSE 'Queued' END,
                   NextAttemptAt = COALESCE(@retry, NextAttemptAt),
                   LastError = @e
             WHERE Id = @id
            """);
        await cmd.With("@id", id).With("@retry", retryAt).With("@e", OutboxEventRepository.Truncate(error, 2000)).ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> RequeueAsync(Guid? tenantId, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.Messages SET Status = 'Queued', NextAttemptAt = @now, Attempts = 0, LastError = NULL
             WHERE Id = @id AND Status IN ('Failed','Cancelled') AND ((@t IS NULL AND TenantId IS NULL) OR TenantId = @t)
            """);
        return await cmd.With("@id", id).With("@t", tenantId).With("@now", now).ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<bool> CancelAsync(Guid? tenantId, Guid id, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.Messages SET Status = 'Cancelled'
             WHERE Id = @id AND Status = 'Queued' AND ((@t IS NULL AND TenantId IS NULL) OR TenantId = @t)
            """);
        return await cmd.With("@id", id).With("@t", tenantId).ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<IReadOnlyList<OutboundMessage>> ListAsync(Guid? tenantId, MessageQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT TOP (@n) * FROM dbo.Messages
             WHERE ((@t IS NULL AND TenantId IS NULL) OR TenantId = @t)
               AND (@ch IS NULL OR Channel = @ch)
               AND (@st IS NULL OR Status = @st)
               AND (@q IS NULL OR Recipient LIKE @q OR Subject LIKE @q OR RecipientName LIKE @q)
             ORDER BY CreatedAt DESC
            """);
        cmd.With("@n", Math.Clamp(query.Take, 1, 500)).With("@t", tenantId).With("@ch", query.Channel?.ToString()).With("@st", query.Status?.ToString())
            .With("@q", string.IsNullOrWhiteSpace(query.Search) ? null : "%" + query.Search.Trim().Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal) + "%");
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<OutboundMessage?> GetAsync(Guid? tenantId, Guid id, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT * FROM dbo.Messages WHERE Id = @id AND ((@t IS NULL AND TenantId IS NULL) OR TenantId = @t)");
        cmd.With("@id", id).With("@t", tenantId);
        return (await ReadAllAsync(cmd, ct)).FirstOrDefault();
    }

    public async Task<Dictionary<string, int>> StatsAsync(Guid? tenantId, DateTimeOffset since, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT Channel + ':' + Status AS K, COUNT(*) AS N FROM dbo.Messages
             WHERE ((@t IS NULL AND TenantId IS NULL) OR TenantId = @t) AND CreatedAt >= @since
             GROUP BY Channel, Status
            """);
        cmd.With("@t", tenantId).With("@since", since);
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            map[r.GetString(0)] = r.GetInt32(1);
        }

        return map;
    }

    private static async Task<IReadOnlyList<OutboundMessage>> ReadAllAsync(SqlCommand cmd, CancellationToken ct)
    {
        var list = new List<OutboundMessage>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var extras = r.GetNullableString("AttachmentsJson");
            list.Add(new OutboundMessage
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.IsDBNull(r.GetOrdinal("TenantId")) ? null : r.Get<Guid>("TenantId"),
                Channel = Enum.Parse<MessageChannel>(r.Get<string>("Channel")),
                Recipient = r.Get<string>("Recipient"),
                RecipientName = r.GetNullableString("RecipientName"),
                Subject = r.GetNullableString("Subject"),
                Body = r.Get<string>("Body"),
                TemplateKey = r.GetNullableString("TemplateKey"),
                EntityType = r.GetNullableString("EntityType"),
                EntityId = r.GetNullableString("EntityId"),
                Extras = string.IsNullOrEmpty(extras) ? new MessageExtras() : JsonDefaults.Deserialize<MessageExtras>(extras),
                Status = Enum.Parse<MessageStatus>(r.Get<string>("Status")),
                Attempts = r.Get<int>("Attempts"),
                NextAttemptAt = r.Get<DateTimeOffset>("NextAttemptAt"),
                LastError = r.GetNullableString("LastError"),
                ProviderMessageId = r.GetNullableString("ProviderMessageId"),
                CreatedBy = r.Get<string>("CreatedBy"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
                SentAt = r.GetNullableDateTimeOffset("SentAt"),
            });
        }

        return list;
    }
}

public sealed class TenantLogoRepository(SqlConnectionFactory connections)
{
    public async Task<TenantLogo?> GetAsync(Guid tenantId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT ContentType, Data, Sha256, UpdatedAt FROM dbo.TenantLogos WHERE TenantId = @t");
        cmd.With("@t", tenantId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)
            ? new TenantLogo(r.GetString(0), (byte[])r[1], r.GetString(2), r.GetDateTimeOffset(3))
            : null;
    }

    public async Task<(string Sha256, DateTimeOffset UpdatedAt)?> InfoAsync(Guid tenantId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT Sha256, UpdatedAt FROM dbo.TenantLogos WHERE TenantId = @t");
        cmd.With("@t", tenantId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetString(0), r.GetDateTimeOffset(1)) : null;
    }

    public async Task SaveAsync(Guid tenantId, string contentType, byte[] data, string user, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            MERGE dbo.TenantLogos WITH (HOLDLOCK) AS t
            USING (SELECT @t AS TenantId) AS s ON t.TenantId = s.TenantId
            WHEN MATCHED THEN UPDATE SET ContentType = @ct, Data = @d, Sha256 = @h, UpdatedBy = @by, UpdatedAt = @at
            WHEN NOT MATCHED THEN INSERT (TenantId, ContentType, Data, Sha256, UpdatedBy, UpdatedAt) VALUES (@t, @ct, @d, @h, @by, @at);
            """);
        cmd.With("@t", tenantId).With("@ct", contentType).With("@h", Hashing.Sha256Hex(data)).With("@by", user).With("@at", at);
        cmd.Parameters.Add(new SqlParameter("@d", System.Data.SqlDbType.VarBinary, -1) { Value = data });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("DELETE dbo.TenantLogos WHERE TenantId = @t");
        return await cmd.With("@t", tenantId).ExecuteNonQueryAsync(ct) == 1;
    }
}

public sealed class IntegrationRepository(SqlConnectionFactory connections)
{
    public async Task<IReadOnlyList<Integration>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT * FROM dbo.Integrations WHERE TenantId = @t ORDER BY CreatedAt");
        cmd.With("@t", tenantId);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<Integration?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT * FROM dbo.Integrations WHERE Id = @id");
        cmd.With("@id", id);
        return (await ReadAllAsync(cmd, ct)).FirstOrDefault();
    }

    public async Task InsertAsync(Integration i, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(i);
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            INSERT dbo.Integrations (Id, TenantId, Kind, Name, IsActive, SettingsJson, SecretsProtected, CreatedBy, CreatedAt, UpdatedAt)
            VALUES (@id, @t, @k, @n, @a, @s, @sec, @by, @at, @at)
            """);
        await cmd.With("@id", i.Id).With("@t", i.TenantId).With("@k", i.Kind.ToString()).With("@n", i.Name).With("@a", i.IsActive)
            .WithMax("@s", Document(i)).WithMax("@sec", i.SecretsProtected).With("@by", i.CreatedBy).With("@at", i.CreatedAt).ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Integration i, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(i);
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.Integrations SET Name = @n, IsActive = @a, SettingsJson = @s, SecretsProtected = @sec, UpdatedAt = @at
             WHERE Id = @id AND TenantId = @t
            """);
        await cmd.With("@id", i.Id).With("@t", i.TenantId).With("@n", i.Name).With("@a", i.IsActive).WithMax("@s", Document(i))
            .WithMax("@sec", i.SecretsProtected).With("@at", i.UpdatedAt).ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("DELETE dbo.Integrations WHERE Id = @id AND TenantId = @t");
        return await cmd.With("@id", id).With("@t", tenantId).ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task TouchAsync(Guid id, DeliveryDirection direction, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command(direction == DeliveryDirection.In
            ? "UPDATE dbo.Integrations SET LastInboundAt = @at WHERE Id = @id"
            : "UPDATE dbo.Integrations SET LastOutboundAt = @at WHERE Id = @id");
        await cmd.With("@id", id).With("@at", at).ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> LinkOrderAsync(Guid integrationId, string externalId, Guid tenantId, Guid invoiceId, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            IF NOT EXISTS (SELECT 1 FROM dbo.IntegrationOrders WHERE IntegrationId = @i AND ExternalId = @e)
                INSERT dbo.IntegrationOrders (IntegrationId, ExternalId, TenantId, InvoiceId, CreatedAt) VALUES (@i, @e, @t, @inv, @at)
            """);
        return await cmd.With("@i", integrationId).With("@e", externalId).With("@t", tenantId).With("@inv", invoiceId).With("@at", at).ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<bool> OrderExistsAsync(Guid integrationId, string externalId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT COUNT(*) FROM dbo.IntegrationOrders WHERE IntegrationId = @i AND ExternalId = @e");
        return (int)(await cmd.With("@i", integrationId).With("@e", externalId).ExecuteScalarAsync(ct) ?? 0) > 0;
    }

    public async Task<IReadOnlyList<Integration>> ActiveByKindsAsync(IReadOnlyCollection<IntegrationKind> kinds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command($"SELECT * FROM dbo.Integrations WHERE IsActive = 1 AND Kind IN ({string.Join(",", kinds.Select((_, i) => "@k" + i))})");
        var n = 0;
        foreach (var k in kinds)
        {
            cmd.With("@k" + n++, k.ToString());
        }

        return await ReadAllAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<(Guid IntegrationId, string ExternalId)>> OrdersForInvoiceAsync(Guid invoiceId, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("SELECT IntegrationId, ExternalId FROM dbo.IntegrationOrders WHERE InvoiceId = @inv");
        cmd.With("@inv", invoiceId);
        var list = new List<(Guid, string)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add((r.GetGuid(0), r.GetString(1)));
        }

        return list;
    }

    public async Task InsertDeliveryAsync(IntegrationDelivery d, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(d);
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            INSERT dbo.IntegrationDeliveries (Id, TenantId, IntegrationId, Direction, EventType, Status, HttpStatus, Attempts, NextAttemptAt,
                                              RequestBody, ResponseBody, Error, InvoiceId, ExternalId, CreatedAt, CompletedAt)
            VALUES (@id, @t, @i, @dir, @ev, @st, @http, @att, @next, @req, @res, @err, @inv, @ext, @at, @done)
            """);
        await cmd.With("@id", d.Id).With("@t", d.TenantId).With("@i", d.IntegrationId).With("@dir", d.Direction.ToString()).With("@ev", d.EventType)
            .With("@st", d.Status.ToString()).With("@http", d.HttpStatus).With("@att", d.Attempts).With("@next", d.NextAttemptAt)
            .WithMax("@req", Cap(d.RequestBody)).WithMax("@res", Cap(d.ResponseBody)).With("@err", d.Error is null ? null : OutboxEventRepository.Truncate(d.Error, 2000))
            .With("@inv", d.InvoiceId).With("@ext", d.ExternalId).With("@at", d.CreatedAt).With("@done", d.CompletedAt).ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<IntegrationDelivery>> ClaimDueDeliveriesAsync(int take, DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            WITH due AS (
                SELECT TOP (@n) * FROM dbo.IntegrationDeliveries WITH (ROWLOCK, UPDLOCK, READPAST)
                 WHERE Status = 'Pending' AND Direction = 'Out' AND NextAttemptAt <= @now
                 ORDER BY NextAttemptAt)
            UPDATE due SET Attempts = Attempts + 1, NextAttemptAt = @lease
            OUTPUT inserted.*
            """);
        cmd.With("@n", take).With("@now", now).With("@lease", now.Add(lease));
        return await ReadDeliveriesAsync(cmd, ct);
    }

    public async Task CompleteDeliveryAsync(Guid id, DeliveryStatus status, int? httpStatus, string? response, string? error, DateTimeOffset? retryAt, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.IntegrationDeliveries
               SET Status = @st, HttpStatus = @http, ResponseBody = @res, Error = @err,
                   NextAttemptAt = @retry, CompletedAt = CASE WHEN @st = 'Pending' THEN NULL ELSE @at END
             WHERE Id = @id
            """);
        await cmd.With("@id", id).With("@st", status.ToString()).With("@http", httpStatus).WithMax("@res", Cap(response))
            .With("@err", error is null ? null : OutboxEventRepository.Truncate(error, 2000)).With("@retry", retryAt).With("@at", at).ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> RetryDeliveryAsync(Guid tenantId, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            UPDATE dbo.IntegrationDeliveries SET Status = 'Pending', NextAttemptAt = @now, Attempts = 0, Error = NULL, CompletedAt = NULL
             WHERE Id = @id AND TenantId = @t AND Direction = 'Out' AND Status = 'Failed'
            """);
        return await cmd.With("@id", id).With("@t", tenantId).With("@now", now).ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<IReadOnlyList<IntegrationDelivery>> DeliveriesAsync(Guid tenantId, Guid integrationId, int take, CancellationToken ct)
    {
        await using var c = await connections.OpenSystemAsync(ct);
        await using var cmd = c.Command("""
            SELECT TOP (@n) * FROM dbo.IntegrationDeliveries WHERE TenantId = @t AND IntegrationId = @i ORDER BY CreatedAt DESC
            """);
        cmd.With("@n", Math.Clamp(take, 1, 200)).With("@t", tenantId).With("@i", integrationId);
        return await ReadDeliveriesAsync(cmd, ct);
    }

    private static string? Cap(string? value) => value is null ? null : OutboxEventRepository.Truncate(value, 20000);

    private static string Document(Integration i) => JsonDefaults.Serialize(new IntegrationSettingsDocument { Settings = i.Settings, Events = i.Events });

    private static async Task<IReadOnlyList<Integration>> ReadAllAsync(SqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Integration>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var doc = JsonDefaults.Deserialize<IntegrationSettingsDocument>(r.Get<string>("SettingsJson"));
            list.Add(new Integration
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.Get<Guid>("TenantId"),
                Kind = Enum.Parse<IntegrationKind>(r.Get<string>("Kind")),
                Name = r.Get<string>("Name"),
                IsActive = r.Get<bool>("IsActive"),
                Settings = doc.Settings,
                Events = doc.Events,
                SecretsProtected = r.GetNullableString("SecretsProtected"),
                CreatedBy = r.Get<string>("CreatedBy"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
                UpdatedAt = r.Get<DateTimeOffset>("UpdatedAt"),
                LastInboundAt = r.GetNullableDateTimeOffset("LastInboundAt"),
                LastOutboundAt = r.GetNullableDateTimeOffset("LastOutboundAt"),
            });
        }

        return list;
    }

    private static async Task<IReadOnlyList<IntegrationDelivery>> ReadDeliveriesAsync(SqlCommand cmd, CancellationToken ct)
    {
        var list = new List<IntegrationDelivery>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new IntegrationDelivery
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.Get<Guid>("TenantId"),
                IntegrationId = r.Get<Guid>("IntegrationId"),
                Direction = Enum.Parse<DeliveryDirection>(r.Get<string>("Direction")),
                EventType = r.Get<string>("EventType"),
                Status = Enum.Parse<DeliveryStatus>(r.Get<string>("Status")),
                HttpStatus = r.IsDBNull(r.GetOrdinal("HttpStatus")) ? null : r.Get<int>("HttpStatus"),
                Attempts = r.Get<int>("Attempts"),
                NextAttemptAt = r.GetNullableDateTimeOffset("NextAttemptAt"),
                RequestBody = r.GetNullableString("RequestBody"),
                ResponseBody = r.GetNullableString("ResponseBody"),
                Error = r.GetNullableString("Error"),
                InvoiceId = r.IsDBNull(r.GetOrdinal("InvoiceId")) ? null : r.Get<Guid>("InvoiceId"),
                ExternalId = r.GetNullableString("ExternalId"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
                CompletedAt = r.GetNullableDateTimeOffset("CompletedAt"),
            });
        }

        return list;
    }
}
