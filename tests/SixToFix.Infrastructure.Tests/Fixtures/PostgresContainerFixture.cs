using Testcontainers.PostgreSql;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Fixtures;

/// <summary>
/// Shared PostgreSQL container fixture for integration tests.
/// Per test-layer-matrix.md §5: shared Testcontainer + per-test IDbContextTransaction rollback.
/// Uses postgres:16-alpine to mirror the production PostgreSQL 16 version.
/// Never use SQLite — always real PostgreSQL (SQLite cannot reproduce advisory locks, jsonb, etc.).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("sixtofix_test")
            .WithUsername("sf_admin")        // Admin role for migrations
            .WithPassword("test_admin_pw")
            .WithPortBinding(5432, true)     // Random host port — no conflicts between parallel runs
            .Build();
    }

    /// <summary>
    /// Admin connection string for running EF Core migrations and schema assertions.
    /// Per test-layer-matrix.md §5.3: MigrateAsync() and pg_catalog reads use sf_admin.
    /// </summary>
    public string AdminConnectionString => _container.GetConnectionString();

    /// <summary>
    /// App connection string for runtime queries, mirroring the sf_app role in production.
    /// Includes No Reset On Close=true required for pgBouncer transaction pooling compatibility.
    /// Per environment-contract.md §5 and test-layer-matrix.md §5.3.
    /// </summary>
    public string AppConnectionString =>
        _container.GetConnectionString()
            .Replace("Username=sf_admin", "Username=sf_app")
            .Replace("Password=test_admin_pw", "Password=test_app_pw")
        + ";No Reset On Close=true";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await CreateAppRoleAsync();
    }

    /// <summary>
    /// Creates the sf_app role with DML-only permissions, mirroring the production init-db.sh script.
    /// Per test-layer-matrix.md §5.3: sf_app gets DML only (SELECT, INSERT, UPDATE — no DELETE, no DDL).
    /// </summary>
    private async Task CreateAppRoleAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'sf_app') THEN
                    CREATE ROLE sf_app WITH LOGIN PASSWORD 'test_app_pw';
                END IF;
            END $$;
            GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO sf_app;
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO sf_app;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO sf_app;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE ON SEQUENCES TO sf_app;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
