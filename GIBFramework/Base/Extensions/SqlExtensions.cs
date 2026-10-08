using System.Data;
using Microsoft.Data.SqlClient;

namespace GIBFramework.Base.Extensions;

public static class SqlExtensions
{
    public static SqlCommand Command(this SqlConnection connection, string sql, SqlTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new SqlCommand(sql, connection, transaction);
    }

    public static SqlCommand With(this SqlCommand command, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    public static SqlCommand WithDecimal(this SqlCommand command, string name, decimal value)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Parameters.Add(new SqlParameter(name, SqlDbType.Decimal) { Precision = 19, Scale = 4, Value = value });
        return command;
    }

    public static SqlCommand WithMax(this SqlCommand command, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Parameters.Add(new SqlParameter(name, SqlDbType.NVarChar, -1) { Value = (object?)value ?? DBNull.Value });
        return command;
    }

    public static string? GetNullableString(this SqlDataReader reader, string column)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetString(i);
    }

    public static DateTimeOffset? GetNullableDateTimeOffset(this SqlDataReader reader, string column)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetDateTimeOffset(i);
    }

    public static DateOnly? GetNullableDateOnly(this SqlDataReader reader, string column)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : DateOnly.FromDateTime(reader.GetDateTime(i));
    }

    public static T Get<T>(this SqlDataReader reader, string column)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.GetFieldValue<T>(reader.GetOrdinal(column));
    }
}
