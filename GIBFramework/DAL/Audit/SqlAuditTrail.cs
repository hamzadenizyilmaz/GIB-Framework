using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.Audit;
using GIBFramework.Services.Auth;

namespace GIBFramework.DAL.Audit;

public sealed class SqlAuditTrail(SqlConnectionFactory connections, ITenantContext context, IClock clock, IHttpContextAccessor http) : IAuditTrail
{
    private ActorType CurrentActor => context.IsSystem ? ActorType.System
        : http.HttpContext?.User.Identity?.AuthenticationType == ApiKeyAuthenticationHandler.SchemeName
            || context.UserId?.StartsWith("entegrasyon:", StringComparison.Ordinal) == true ? ActorType.ApiClient : ActorType.User;

    public Task<AuditEvent> AppendAsync(AuditEntry entry, DbScope? scope, CancellationToken cancellationToken) =>
        AppendCoreAsync(
            context.RequireTenant(),
            context.UserId,
            CurrentActor,
            entry,
            scope,
            system: false,
            cancellationToken);

    public Task<AuditEvent> AppendSystemAsync(Guid tenantId, AuditEntry entry, DbScope? scope, CancellationToken cancellationToken) =>
        AppendCoreAsync(tenantId, "system", ActorType.System, entry, scope, system: true, cancellationToken);

    public Task<AuditEvent> AppendActorAsync(Guid tenantId, AuditEntry entry, CancellationToken cancellationToken) =>
        AppendCoreAsync(tenantId, context.UserId, CurrentActor,
            entry, null, system: false, cancellationToken);

    public async Task<IReadOnlyList<AuditEvent>> ListAsync(Guid tenantId, string? entityId, int take, long? beforeSequence,
        CancellationToken cancellationToken, string? userId = null, string? action = null, string? role = null,
        DateTimeOffset? from = null, DateTimeOffset? until = null, string? result = null)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("""
            SELECT TOP (@take) * FROM dbo.AuditEvents
             WHERE TenantId = @t AND (@e IS NULL OR EntityId = @e) AND (@b IS NULL OR Sequence < @b)
               AND (@u IS NULL OR UserId = @u) AND (@a IS NULL OR Action = @a)
               AND (@from IS NULL OR TimestampUtc >= @from) AND (@until IS NULL OR TimestampUtc < @until)
               AND (@result IS NULL OR Result = @result)
               AND (@role IS NULL OR EXISTS (
                   SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(DataJson) = 1 THEN DataJson ELSE N'{}' END, '$.actorRoles')
                   WHERE [value] = @role))
             ORDER BY Sequence DESC
            """);
        cmd.With("@take", Math.Clamp(take, 1, 500)).With("@t", tenantId).With("@e", entityId).With("@b", beforeSequence);
        cmd.With("@u", userId).With("@a", action).With("@role", role);
        cmd.With("@from", from).With("@until", until).With("@result", result);
        return await ReadAsync(cmd, cancellationToken);
    }

    public async Task<AuditChainVerification> VerifyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenForTenantAsync(tenantId, cancellationToken);
        await using var cmd = connection.Command("SELECT * FROM dbo.AuditEvents WHERE TenantId = @t ORDER BY Sequence");
        cmd.With("@t", tenantId);
        var events = await ReadAsync(cmd, cancellationToken);

        var previous = string.Empty;
        foreach (var e in events)
        {
            if (e.PreviousHash != previous || e.ComputeHash() != e.Hash)
            {
                return new AuditChainVerification(tenantId, events.Count, false, e.Sequence, previous);
            }

            previous = e.Hash;
        }

        return new AuditChainVerification(tenantId, events.Count, true, null, previous.Length == 0 ? null : previous);
    }

    private async Task<AuditEvent> AppendCoreAsync(
        Guid tenantId,
        string? userId,
        ActorType actorType,
        AuditEntry entry,
        DbScope? scope,
        bool system,
        CancellationToken cancellationToken)
    {
        SqlConnection? owned = null;
        SqlTransaction? ownedTx = null;
        try
        {
            if (scope is null)
            {
                owned = system ? await connections.OpenSystemAsync(cancellationToken) : await connections.OpenForTenantAsync(tenantId, cancellationToken);
                ownedTx = (SqlTransaction)await owned.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                scope = new DbScope(owned, ownedTx);
            }

            await using (var lockCmd = scope.Connection.Command(
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @r;",
                scope.Transaction))
            {
                var result = (int)(await lockCmd.With("@res", "audit:" + tenantId.ToString("N")).ExecuteScalarAsync(cancellationToken))!;
                if (result < 0)
                {
                    throw new TimeoutException("Audit zinciri kilidi alınamadı.");
                }
            }

            string previous;
            await using (var last = scope.Connection.Command("SELECT TOP 1 Hash FROM dbo.AuditEvents WHERE TenantId = @t ORDER BY Sequence DESC", scope.Transaction))
            {
                previous = (string?)await last.With("@t", tenantId).ExecuteScalarAsync(cancellationToken) ?? string.Empty;
            }

            var evt = new AuditEvent
            {
                EventId = Guid.CreateVersion7(),
                TenantId = tenantId,
                UserId = userId,
                ActorType = actorType,
                Action = entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                TimestampUtc = clock.UtcNow,
                Ip = system ? null : context.Ip,
                UserAgent = system ? null : Truncate(context.UserAgent, 400),
                CorrelationId = context.CorrelationId,
                TraceId = Activity.Current?.TraceId.ToString(),
                Result = entry.Result,
                FailureReason = entry.FailureReason,
                DataJson = !system && context.Roles.Count > 0
                    ? JsonDefaults.Serialize(new { actorRoles = context.Roles, details = entry.Data })
                    : entry.Data is null ? null : JsonDefaults.Serialize(entry.Data),
                PreviousHash = previous,
            };
            evt = evt with { Hash = evt.ComputeHash() };

            await using (var insert = scope.Connection.Command("""
                INSERT dbo.AuditEvents (EventId, TenantId, UserId, ActorType, Action, EntityType, EntityId, TimestampUtc, Ip, UserAgent,
                                        CorrelationId, TraceId, Result, FailureReason, DataJson, PreviousHash, Hash)
                VALUES (@id, @t, @u, @at, @a, @et, @eid, @ts, @ip, @ua, @cid, @tid, @r, @fr, @data, @ph, @h);
                SELECT CAST(SCOPE_IDENTITY() AS bigint);
                """, scope.Transaction))
            {
                insert.With("@id", evt.EventId).With("@t", evt.TenantId).With("@u", evt.UserId).With("@at", evt.ActorType.ToString())
                    .With("@a", evt.Action).With("@et", evt.EntityType).With("@eid", evt.EntityId).With("@ts", evt.TimestampUtc)
                    .With("@ip", evt.Ip).With("@ua", Truncate(evt.UserAgent, 400)).With("@cid", evt.CorrelationId).With("@tid", evt.TraceId)
                    .With("@r", evt.Result).With("@fr", evt.FailureReason).WithMax("@data", evt.DataJson).With("@ph", evt.PreviousHash).With("@h", evt.Hash);
                evt = evt with { Sequence = (long)(await insert.ExecuteScalarAsync(cancellationToken))! };
            }

            if (ownedTx is not null)
            {
                await ownedTx.CommitAsync(cancellationToken);
            }

            return evt;
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync();
            }

            if (owned is not null)
            {
                await owned.DisposeAsync();
            }
        }
    }

    private static string? Truncate(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;

    private static async Task<List<AuditEvent>> ReadAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<AuditEvent>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new AuditEvent
            {
                Sequence = r.Get<long>("Sequence"),
                EventId = r.Get<Guid>("EventId"),
                TenantId = r.Get<Guid>("TenantId"),
                UserId = r.GetNullableString("UserId"),
                ActorType = Enum.Parse<ActorType>(r.Get<string>("ActorType")),
                Action = r.Get<string>("Action"),
                EntityType = r.Get<string>("EntityType"),
                EntityId = r.GetNullableString("EntityId"),
                TimestampUtc = r.Get<DateTimeOffset>("TimestampUtc"),
                Ip = r.GetNullableString("Ip"),
                UserAgent = r.GetNullableString("UserAgent"),
                CorrelationId = r.GetNullableString("CorrelationId"),
                TraceId = r.GetNullableString("TraceId"),
                Result = r.Get<string>("Result"),
                FailureReason = r.GetNullableString("FailureReason"),
                DataJson = r.GetNullableString("DataJson"),
                PreviousHash = r.Get<string>("PreviousHash"),
                Hash = r.Get<string>("Hash"),
            });
        }

        return list;
    }
}
