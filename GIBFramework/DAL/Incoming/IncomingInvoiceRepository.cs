using Microsoft.Data.SqlClient;
using GIBFramework.Base.Extensions;
using GIBFramework.Models.Incoming;

namespace GIBFramework.DAL.Incoming;

public interface IIncomingInvoiceRepository
{
    Task InsertAsync(IncomingInvoice invoice, DbScope scope, CancellationToken cancellationToken);

    Task<IncomingInvoice?> GetAsync(SqlConnection connection, Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomingInvoice>> ListAsync(SqlConnection connection, Guid tenantId, int take, CancellationToken cancellationToken);

    Task UpdateDecisionAsync(IncomingInvoice invoice, DbScope scope, CancellationToken cancellationToken);
}

public sealed class IncomingInvoiceRepository : IIncomingInvoiceRepository
{
    public async Task InsertAsync(IncomingInvoice invoice, DbScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(scope);
        await using var cmd = scope.Connection.Command("""
            INSERT dbo.IncomingInvoices (Id, TenantId, Ettn, DocumentNumber, Profile, SupplierTaxId, SupplierTitle, CustomerTaxId, IssueDate,
                                         PayableAmount, Currency, Status, SignaturePresent, SignatureValid, IssuesJson, XmlSha256, ReceivedVia, ReceivedAt)
            VALUES (@id, @t, @e, @n, @p, @s, @st, @c, @d, @pay, @cur, @status, @sp, @sv, @issues, @sha, @via, @at)
            """, scope.Transaction);
        cmd.With("@id", invoice.Id).With("@t", invoice.TenantId).With("@e", invoice.Uuid).With("@n", invoice.DocumentNumber).With("@p", invoice.Profile)
            .With("@s", invoice.SupplierTaxId).With("@st", invoice.SupplierTitle).With("@c", invoice.CustomerTaxId)
            .With("@d", invoice.IssueDate.ToDateTime(TimeOnly.MinValue)).WithDecimal("@pay", invoice.PayableAmount).With("@cur", invoice.Currency)
            .With("@status", invoice.Status.ToString()).With("@sp", invoice.SignaturePresent).With("@sv", invoice.SignatureValid)
            .WithMax("@issues", JsonDefaults.Serialize(invoice.Issues)).With("@sha", invoice.XmlSha256).With("@via", invoice.ReceivedVia)
            .With("@at", invoice.ReceivedAt);
        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new ConflictException("INCOMING_DUPLICATE", $"Bu ETTN ({invoice.Uuid}) ile gelen belge zaten alınmış.");
        }
    }

    public async Task<IncomingInvoice?> GetAsync(SqlConnection connection, Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var cmd = connection.Command("SELECT * FROM dbo.IncomingInvoices WHERE TenantId = @t AND Id = @id");
        cmd.With("@t", tenantId).With("@id", id);
        var list = await ReadAsync(cmd, cancellationToken);
        return list.Count == 0 ? null : list[0];
    }

    public async Task<IReadOnlyList<IncomingInvoice>> ListAsync(SqlConnection connection, Guid tenantId, int take, CancellationToken cancellationToken)
    {
        await using var cmd = connection.Command("SELECT TOP (@take) * FROM dbo.IncomingInvoices WHERE TenantId = @t ORDER BY ReceivedAt DESC");
        cmd.With("@take", Math.Clamp(take, 1, 500)).With("@t", tenantId);
        return await ReadAsync(cmd, cancellationToken);
    }

    public async Task UpdateDecisionAsync(IncomingInvoice invoice, DbScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(scope);
        await using var cmd = scope.Connection.Command(
            "UPDATE dbo.IncomingInvoices SET Status = @s, DecidedBy = @by, DecidedAt = @at WHERE TenantId = @t AND Id = @id",
            scope.Transaction);
        await cmd.With("@s", invoice.Status.ToString()).With("@by", invoice.DecidedBy).With("@at", invoice.DecidedAt)
            .With("@t", invoice.TenantId).With("@id", invoice.Id).ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<IncomingInvoice>> ReadAsync(SqlCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<IncomingInvoice>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            list.Add(new IncomingInvoice
            {
                Id = r.Get<Guid>("Id"),
                TenantId = r.Get<Guid>("TenantId"),
                Uuid = r.Get<Guid>("Ettn"),
                DocumentNumber = r.Get<string>("DocumentNumber"),
                Profile = r.Get<string>("Profile"),
                SupplierTaxId = r.Get<string>("SupplierTaxId"),
                SupplierTitle = r.Get<string>("SupplierTitle"),
                CustomerTaxId = r.Get<string>("CustomerTaxId"),
                IssueDate = r.GetNullableDateOnly("IssueDate")!.Value,
                PayableAmount = r.Get<decimal>("PayableAmount"),
                Currency = r.Get<string>("Currency"),
                Status = Enum.Parse<IncomingStatus>(r.Get<string>("Status")),
                SignaturePresent = r.Get<bool>("SignaturePresent"),
                SignatureValid = r.Get<bool>("SignatureValid"),
                Issues = JsonDefaults.Deserialize<List<string>>(r.Get<string>("IssuesJson")),
                XmlSha256 = r.Get<string>("XmlSha256"),
                ReceivedVia = r.Get<string>("ReceivedVia"),
                ReceivedAt = r.Get<DateTimeOffset>("ReceivedAt"),
                DecidedBy = r.GetNullableString("DecidedBy"),
                DecidedAt = r.GetNullableDateTimeOffset("DecidedAt"),
            });
        }

        return list;
    }
}
