using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;

namespace GIBFramework.DAL.Invoices;

public sealed record InvoiceQuery(
    DocumentStatus? Status,
    DateOnly? From,
    DateOnly? To,
    string? CustomerTaxId,
    int Take = 50,
    int Skip = 0,
    EDocumentType? DocumentType = null,
    string? Search = null)
{
    public const int MaxTake = 5000;
}

public sealed record InvoiceSummary(
    Guid Id,
    Guid Ettn,
    string? DocumentNumber,
    string? DraftNumber,
    EDocumentType DocumentType,
    InvoiceProfile Profile,
    DocumentStatus Status,
    DateOnly IssueDate,
    string CustomerTaxId,
    string? CustomerTitle,
    decimal PayableAmount,
    string Currency,
    IssuanceChannel IssuanceChannel,
    string CreatedBy,
    DateTimeOffset UpdatedAt);

public sealed record InvoicePage(IReadOnlyList<InvoiceSummary> Items, int Total);

public sealed record InvoiceHistoryEntry(DocumentStatus? From, DocumentStatus To, string Actor, string? Note, DateTimeOffset At);

public interface IInvoiceRepository
{
    Task InsertAsync(Invoice invoice, DbScope scope, CancellationToken cancellationToken);

    Task UpdateAsync(Invoice invoice, DbScope scope, CancellationToken cancellationToken);

    Task<Invoice?> GetAsync(SqlConnection connection, Guid tenantId, Guid id, CancellationToken cancellationToken, SqlTransaction? transaction = null);

    Task<Invoice?> GetByIdempotencyKeyAsync(SqlConnection connection, Guid tenantId, string key, CancellationToken cancellationToken);

    Task<InvoicePage> ListAsync(SqlConnection connection, Guid tenantId, InvoiceQuery query, CancellationToken cancellationToken);

    Task AddHistoryAsync(DbScope scope, Invoice invoice, DocumentStatus? from, string actor, string? note, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvoiceHistoryEntry>> GetHistoryAsync(SqlConnection connection, Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid TenantId, Guid Id)>> FindByStatusAsync(SqlConnection systemConnection, IReadOnlyList<DocumentStatus> statuses, DateTimeOffset updatedBefore, int take, CancellationToken cancellationToken);

    Task InsertActionAsync(InvoiceAction action, DbScope scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvoiceAction>> GetActionsAsync(SqlConnection connection, Guid tenantId, Guid invoiceId, CancellationToken cancellationToken);
}

public sealed class InvoiceRepository : IInvoiceRepository
{
    private const int UniqueViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task InsertAsync(Invoice invoice, DbScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(scope);
        await using var cmd = scope.Connection.Command("""
            INSERT dbo.Invoices (Id, TenantId, Ettn, DocumentNumber, DraftNumber, DocumentType, Profile, TypeCode, Status, IssueDate, CustomerTaxId, Currency,
                                 PayableAmount, IdempotencyKey, ContentJson, DecisionJson, SignedXmlSha256, ProviderReference, LastError,
                                 CreatedBy, CreatedAt, ApprovedBy, ApprovedAt, SignedBy, SignedAt, TransmittedAt, UpdatedAt)
            VALUES (@id, @t, @ettn, @num, @draft, @dt, @p, @tc, @s, @d, @c, @cur, @pay, @idem, @content, @decision, @sha, @pr, @err,
                    @cb, @ca, @ab, @aa, @sb, @sa, @ta, @u);
            SELECT RowVersion FROM dbo.Invoices WHERE Id = @id;
            """, scope.Transaction);
        Bind(cmd, invoice);
        cmd.With("@idem", invoice.IdempotencyKey).With("@cb", invoice.CreatedBy).With("@ca", invoice.CreatedAt);
        try
        {
            invoice.RowVersion = (byte[])(await cmd.ExecuteScalarAsync(cancellationToken))!;
        }
        catch (SqlException ex) when (ex.Number is UniqueViolation or UniqueConstraintViolation)
        {
            throw new ConflictException("INVOICE_DUPLICATE", "Aynı ETTN veya idempotency anahtarına sahip fatura zaten var.");
        }
    }

    public async Task UpdateAsync(Invoice invoice, DbScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(scope);
        await using var cmd = scope.Connection.Command("""
            DECLARE @rv TABLE (RowVersion binary(8));
            UPDATE dbo.Invoices
               SET Ettn = @ettn, DocumentNumber = @num, DocumentType = @dt, Profile = @p, TypeCode = @tc, Status = @s, IssueDate = @d,
                   CustomerTaxId = @c, Currency = @cur, PayableAmount = @pay, ContentJson = @content, DecisionJson = @decision,
                   SignedXmlSha256 = @sha, ProviderReference = @pr, LastError = @err, ApprovedBy = @ab, ApprovedAt = @aa,
                   SignedBy = @sb, SignedAt = @sa, TransmittedAt = @ta, UpdatedAt = @u
            OUTPUT inserted.RowVersion INTO @rv
             WHERE Id = @id AND TenantId = @t AND RowVersion = @rowversion;
            SELECT RowVersion FROM @rv;
            """, scope.Transaction);
        Bind(cmd, invoice);
        cmd.With("@rowversion", invoice.RowVersion);
        try
        {
            invoice.RowVersion = (byte[]?)await cmd.ExecuteScalarAsync(cancellationToken)
                ?? throw new ConflictException("INVOICE_CONCURRENCY", "Fatura başka bir işlem tarafından değiştirildi; yeniden yükleyip tekrar deneyin.");
        }
        catch (SqlException ex) when (ex.Number is UniqueViolation or UniqueConstraintViolation)
        {
            throw new ConflictException("INVOICE_NUMBER_DUPLICATE", "Belge numarası zaten kullanılmış.");
        }
    }

    public async Task<Invoice?> GetAsync(SqlConnection connection, Guid tenantId, Guid id, CancellationToken cancellationToken, SqlTransaction? transaction = null)
    {
        await using var cmd = connection.Command(
            transaction is null
                ? "SELECT * FROM dbo.Invoices WHERE Id = @id AND TenantId = @t"
                : "SELECT * FROM dbo.Invoices WITH (UPDLOCK, ROWLOCK) WHERE Id = @id AND TenantId = @t",
            transaction);
        cmd.With("@id", id).With("@t", tenantId);
        return await ReadSingleAsync(cmd, cancellationToken);
    }

    public async Task<Invoice?> GetByIdempotencyKeyAsync(SqlConnection connection, Guid tenantId, string key, CancellationToken cancellationToken)
    {
        await using var cmd = connection.Command("SELECT * FROM dbo.Invoices WHERE TenantId = @t AND IdempotencyKey = @k");
        cmd.With("@t", tenantId).With("@k", key);
        return await ReadSingleAsync(cmd, cancellationToken);
    }

    public async Task<InvoicePage> ListAsync(SqlConnection connection, Guid tenantId, InvoiceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var cmd = connection.Command("""
            SELECT Id, Ettn, DocumentNumber, DraftNumber, DocumentType, Profile, Status, IssueDate, CustomerTaxId, PayableAmount, Currency,
                   CreatedBy, UpdatedAt, JSON_VALUE(ContentJson, '$.customer.title') AS CustomerTitle,
                   JSON_VALUE(ContentJson, '$.issuanceChannel') AS Channel, COUNT(*) OVER () AS TotalCount
              FROM dbo.Invoices
             WHERE TenantId = @t
               AND (@s IS NULL OR Status = @s)
               AND (@dt IS NULL OR DocumentType = @dt)
               AND (@from IS NULL OR IssueDate >= @from)
               AND (@to IS NULL OR IssueDate <= @to)
               AND (@c IS NULL OR CustomerTaxId = @c)
               AND (@q IS NULL OR DocumentNumber LIKE @q OR DraftNumber LIKE @q OR CustomerTaxId LIKE @q
                    OR JSON_VALUE(ContentJson, '$.customer.title') LIKE @q)
             ORDER BY IssueDate DESC, ClusterKey DESC
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
            """);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : "%" + query.Search.Trim().Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal) + "%";
        cmd.With("@t", tenantId).With("@s", query.Status?.ToString()).With("@dt", query.DocumentType?.ToString())
            .With("@from", query.From?.ToDateTime(TimeOnly.MinValue)).With("@to", query.To?.ToDateTime(TimeOnly.MinValue))
            .With("@c", query.CustomerTaxId).With("@q", search)
            .With("@skip", Math.Max(query.Skip, 0)).With("@take", Math.Clamp(query.Take, 1, InvoiceQuery.MaxTake));

        var list = new List<InvoiceSummary>();
        var total = 0;
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            total = r.Get<int>("TotalCount");
            list.Add(new InvoiceSummary(
                r.Get<Guid>("Id"),
                r.Get<Guid>("Ettn"),
                r.GetNullableString("DocumentNumber"),
                r.GetNullableString("DraftNumber"),
                Enum.Parse<EDocumentType>(r.Get<string>("DocumentType")),
                Enum.Parse<InvoiceProfile>(r.Get<string>("Profile")),
                Enum.Parse<DocumentStatus>(r.Get<string>("Status")),
                DateOnly.FromDateTime(r.Get<DateTime>("IssueDate")),
                r.Get<string>("CustomerTaxId"),
                r.GetNullableString("CustomerTitle"),
                r.Get<decimal>("PayableAmount"),
                r.Get<string>("Currency"),
                Enum.TryParse<IssuanceChannel>(r.GetNullableString("Channel"), out var channel) ? channel : IssuanceChannel.Integrator,
                r.Get<string>("CreatedBy"),
                r.Get<DateTimeOffset>("UpdatedAt")));
        }

        if (list.Count == 0 && query.Skip > 0)
        {
            await using var count = connection.Command("SELECT COUNT(*) FROM dbo.Invoices WHERE TenantId = @t");
            total = (int)(await count.With("@t", tenantId).ExecuteScalarAsync(cancellationToken) ?? 0);
        }

        return new InvoicePage(list, total);
    }

    public async Task AddHistoryAsync(DbScope scope, Invoice invoice, DocumentStatus? from, string actor, string? note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(invoice);
        await using var cmd = scope.Connection.Command("""
            INSERT dbo.InvoiceStatusHistory (TenantId, InvoiceId, FromStatus, ToStatus, Actor, Note, At)
            VALUES (@t, @i, @f, @to, @a, @n, @at)
            """, scope.Transaction);
        cmd.With("@t", invoice.TenantId).With("@i", invoice.Id).With("@f", from?.ToString()).With("@to", invoice.Status.ToString())
            .With("@a", actor).With("@n", note).With("@at", invoice.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceHistoryEntry>> GetHistoryAsync(SqlConnection connection, Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var cmd = connection.Command("SELECT * FROM dbo.InvoiceStatusHistory WHERE TenantId = @t AND InvoiceId = @i ORDER BY Id");
        cmd.With("@t", tenantId).With("@i", id);
        var list = new List<InvoiceHistoryEntry>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            var from = r.GetNullableString("FromStatus");
            list.Add(new InvoiceHistoryEntry(
                from is null ? null : Enum.Parse<DocumentStatus>(from),
                Enum.Parse<DocumentStatus>(r.Get<string>("ToStatus")),
                r.Get<string>("Actor"),
                r.GetNullableString("Note"),
                r.Get<DateTimeOffset>("At")));
        }

        return list;
    }

    public async Task<IReadOnlyList<(Guid TenantId, Guid Id)>> FindByStatusAsync(
        SqlConnection systemConnection,
        IReadOnlyList<DocumentStatus> statuses,
        DateTimeOffset updatedBefore,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        await using var cmd = systemConnection.Command("""
            SELECT TOP (@take) TenantId, Id FROM dbo.Invoices
             WHERE Status IN (SELECT value FROM STRING_SPLIT(@statuses, ',')) AND UpdatedAt <= @before
             ORDER BY UpdatedAt
            """);
        cmd.With("@take", take).With("@statuses", string.Join(',', statuses)).With("@before", updatedBefore);
        var list = new List<(Guid, Guid)>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add((r.GetGuid(0), r.GetGuid(1)));
        }

        return list;
    }

    public async Task InsertActionAsync(InvoiceAction action, DbScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(scope);
        await using var cmd = scope.Connection.Command("""
            INSERT dbo.InvoiceActions (Id, TenantId, InvoiceId, Kind, Method, ReferenceNumber, NotificationDate, Reason, Status, RequestedBy, CreatedAt, ProviderReference)
            VALUES (@id, @t, @i, @k, @m, @ref, @nd, @r, @s, @by, @at, @pr)
            """, scope.Transaction);
        cmd.With("@id", action.Id).With("@t", action.TenantId).With("@i", action.InvoiceId).With("@k", action.Kind.ToString())
            .With("@m", action.Method).With("@ref", action.ReferenceNumber).With("@nd", action.NotificationDate?.ToDateTime(TimeOnly.MinValue))
            .With("@r", action.Reason).With("@s", action.Status).With("@by", action.RequestedBy).With("@at", action.CreatedAt).With("@pr", action.ProviderReference);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceAction>> GetActionsAsync(SqlConnection connection, Guid tenantId, Guid invoiceId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.Command("SELECT * FROM dbo.InvoiceActions WHERE TenantId = @t AND InvoiceId = @i ORDER BY CreatedAt");
        cmd.With("@t", tenantId).With("@i", invoiceId);
        var list = new List<InvoiceAction>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new InvoiceAction
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.Get<Guid>("TenantId"),
                InvoiceId = r.Get<Guid>("InvoiceId"),
                Kind = Enum.Parse<InvoiceActionKind>(r.Get<string>("Kind")),
                Method = r.GetNullableString("Method"),
                ReferenceNumber = r.GetNullableString("ReferenceNumber"),
                NotificationDate = r.GetNullableDateOnly("NotificationDate"),
                Reason = r.Get<string>("Reason"),
                Status = r.Get<string>("Status"),
                RequestedBy = r.Get<string>("RequestedBy"),
                CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
                ProviderReference = r.GetNullableString("ProviderReference"),
            });
        }

        return list;
    }

    private static void Bind(SqlCommand cmd, Invoice invoice)
    {
        cmd.With("@id", invoice.Id).With("@t", invoice.TenantId).With("@ettn", invoice.Uuid).With("@num", invoice.DocumentNumber).With("@draft", invoice.DraftNumber)
            .With("@dt", invoice.DocumentType.ToString()).With("@p", invoice.Profile.ToString()).With("@tc", invoice.TypeCode.ToString())
            .With("@s", invoice.Status.ToString()).With("@d", invoice.IssueDate.ToDateTime(TimeOnly.MinValue)).With("@c", invoice.Customer.TaxId)
            .With("@cur", invoice.Currency).WithDecimal("@pay", invoice.Totals.PayableAmount)
            .WithMax("@content", JsonDefaults.Serialize(InvoiceContent.From(invoice)))
            .WithMax("@decision", invoice.Decision is null ? null : JsonDefaults.Serialize(invoice.Decision))
            .With("@sha", invoice.SignedXmlSha256).With("@pr", invoice.ProviderReference).With("@err", invoice.LastError)
            .With("@ab", invoice.ApprovedBy).With("@aa", invoice.ApprovedAt).With("@sb", invoice.SignedBy).With("@sa", invoice.SignedAt)
            .With("@ta", invoice.TransmittedAt).With("@u", invoice.UpdatedAt);
    }

    private static async Task<Invoice?> ReadSingleAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await r.ReadAsync(cancellationToken))
        {
            return null;
        }

        var invoice = new Invoice
        {
            Id = r.Get<Guid>("Id"),
            TenantId = r.Get<Guid>("TenantId"),
            Uuid = r.Get<Guid>("Ettn"),
            DocumentNumber = r.GetNullableString("DocumentNumber"),
            DraftNumber = r.GetNullableString("DraftNumber"),
            Status = Enum.Parse<DocumentStatus>(r.Get<string>("Status")),
            IdempotencyKey = r.Get<string>("IdempotencyKey"),
            SignedXmlSha256 = r.GetNullableString("SignedXmlSha256"),
            ProviderReference = r.GetNullableString("ProviderReference"),
            LastError = r.GetNullableString("LastError"),
            CreatedBy = r.Get<string>("CreatedBy"),
            CreatedAt = r.Get<DateTimeOffset>("CreatedAt"),
            ApprovedBy = r.GetNullableString("ApprovedBy"),
            ApprovedAt = r.GetNullableDateTimeOffset("ApprovedAt"),
            SignedBy = r.GetNullableString("SignedBy"),
            SignedAt = r.GetNullableDateTimeOffset("SignedAt"),
            TransmittedAt = r.GetNullableDateTimeOffset("TransmittedAt"),
            UpdatedAt = r.Get<DateTimeOffset>("UpdatedAt"),
            RowVersion = r.Get<byte[]>("RowVersion"),
        };
        JsonDefaults.Deserialize<InvoiceContent>(r.Get<string>("ContentJson")).ApplyTo(invoice);
        var decision = r.GetNullableString("DecisionJson");
        invoice.Decision = decision is null ? null : JsonDefaults.Deserialize<ComplianceDecision>(decision);
        return invoice;
    }
}

internal sealed record InvoiceContent(
    EDocumentType DocumentType,
    InvoiceProfile Profile,
    InvoiceTypeCode TypeCode,
    DateOnly IssueDate,
    TimeOnly IssueTime,
    DateOnly? DeliveryDate,
    string Currency,
    decimal? ExchangeRate,
    InvoiceParty Supplier,
    InvoiceParty Customer,
    List<InvoiceLine> Lines,
    List<string> Notes,
    bool DespatchRequired,
    string? DespatchNumber,
    DateOnly? DespatchDate,
    string? OrderNumber,
    string SendingType,
    InvoiceTotals Totals,
    IssuanceChannel IssuanceChannel = IssuanceChannel.Integrator,
    GibPortalEnvironment? PortalEnvironment = null)
{
    public static InvoiceContent From(Invoice i) => new(
        i.DocumentType, i.Profile, i.TypeCode, i.IssueDate, i.IssueTime, i.DeliveryDate, i.Currency, i.ExchangeRate, i.Supplier, i.Customer,
        i.Lines, i.Notes, i.DespatchRequired, i.DespatchNumber, i.DespatchDate, i.OrderNumber, i.SendingType, i.Totals,
        i.IssuanceChannel, i.PortalEnvironment);

    public void ApplyTo(Invoice i)
    {
        i.DocumentType = DocumentType;
        i.Profile = Profile;
        i.TypeCode = TypeCode;
        i.IssueDate = IssueDate;
        i.IssueTime = IssueTime;
        i.DeliveryDate = DeliveryDate;
        i.Currency = Currency;
        i.ExchangeRate = ExchangeRate;
        i.Supplier = Supplier;
        i.Customer = Customer;
        i.Lines = Lines;
        i.Notes = Notes;
        i.DespatchRequired = DespatchRequired;
        i.DespatchNumber = DespatchNumber;
        i.DespatchDate = DespatchDate;
        i.OrderNumber = OrderNumber;
        i.SendingType = SendingType;
        i.Totals = Totals;
        i.IssuanceChannel = IssuanceChannel;
        i.PortalEnvironment = PortalEnvironment;
    }
}
