using System.Data;
using Microsoft.Data.SqlClient;

namespace MegaScreen.Web.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString =
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
