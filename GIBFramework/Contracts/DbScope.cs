using Microsoft.Data.SqlClient;

namespace GIBFramework.Contracts;

public sealed record DbScope(SqlConnection Connection, SqlTransaction Transaction);
