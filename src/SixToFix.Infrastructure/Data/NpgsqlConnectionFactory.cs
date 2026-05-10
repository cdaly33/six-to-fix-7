using System.Data;
using Npgsql;
using SixToFix.Application.Data;

namespace SixToFix.Infrastructure.Data;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly ILogger<NpgsqlConnectionFactory> _logger;

    public NpgsqlConnectionFactory(IConfiguration configuration, ILogger<NpgsqlConnectionFactory> logger)
    {
        var cs = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        if (!cs.Contains("No Reset On Close", StringComparison.OrdinalIgnoreCase))
        {
            cs += ";No Reset On Close=true";
        }

        _connectionString = cs;
        _logger = logger;
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
