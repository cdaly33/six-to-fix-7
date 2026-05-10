using Microsoft.EntityFrameworkCore;
using SixToFix.Infrastructure.Data;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Fixtures;

/// <summary>
/// Base class for infrastructure integration tests using a real PostgreSQL database.
/// Each test runs within a transaction that is rolled back on disposal, providing test isolation
/// without the overhead of re-creating the database schema per test.
///
/// Per test-layer-matrix.md §5: shared Testcontainer lifecycle + per-test transaction rollback.
/// The DbContext uses the sf_app role connection string to mirror production runtime behavior.
/// </summary>
[Collection("PostgreSQL")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly PostgresContainerFixture Fixture;
    protected SixToFixDbContext DbContext = null!;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction = null!;

    protected IntegrationTestBase(PostgresContainerFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        // Run migrations using the admin connection before each test class setup
        var adminOptions = new DbContextOptionsBuilder<SixToFixDbContext>()
            .UseNpgsql(Fixture.AdminConnectionString)
            .Options;

        var designTimeContext = new DesignTimeTenantContext();
        await using var migrationContext = new SixToFixDbContext(adminOptions, designTimeContext);
        await migrationContext.Database.MigrateAsync();

        // Runtime DbContext uses the app role connection, mirroring production
        var appOptions = new DbContextOptionsBuilder<SixToFixDbContext>()
            .UseNpgsql(Fixture.AppConnectionString)
            .Options;

        DbContext = new SixToFixDbContext(appOptions, designTimeContext);

        // Begin a transaction — rolled back in DisposeAsync for test isolation
        _transaction = await DbContext.Database.BeginTransactionAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await DbContext.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection definition binding tests to the shared PostgreSQL container fixture.
/// All test classes decorated with [Collection("PostgreSQL")] share one container instance.
/// </summary>
[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgresContainerFixture>;
