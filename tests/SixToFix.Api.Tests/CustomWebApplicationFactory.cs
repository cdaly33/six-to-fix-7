using Microsoft.AspNetCore.Hosting;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixToFix.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace SixToFix.Api.Tests;

/// <summary>
/// WebApplicationFactory for API contract tests.
/// Uses Testcontainers PostgreSQL (real DB, never SQLite) — per test-layer-matrix.md §2.2.
/// Mocks external services (ISkillRunner, IHubSpotClient, IBlobService) so no real Azure calls
/// are made during tests.
///
/// Usage: Inherit from this class or use as IClassFixture&lt;CustomWebApplicationFactory&gt;.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sixtofix_apitest")
        .WithUsername("sf_admin")
        .WithPassword("test_admin_pw")
        .WithPortBinding(5432, true)
        .Build();

    /// <summary>
    /// Connection string with pgBouncer-compatible settings.
    /// Per environment-contract.md §5: No Reset On Close=true required for transaction pooling.
    /// </summary>
    public string ConnectionString =>
        _postgres.GetConnectionString() + ";No Reset On Close=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace DbContext registration with test database connection
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<SixToFixDbContext>));
            if (dbDescriptor != null)
                services.Remove(dbDescriptor);

            services.AddDbContext<SixToFixDbContext>((sp, options) =>
                options.UseNpgsql(ConnectionString));

            // External service mocks are added here as Phase 2 services are scaffolded.
            // Per test-layer-matrix.md §2.1: ISkillRunner, IHubSpotClient, IBlobService
            // are ALWAYS mocked at this layer — never call real Azure services.
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Run EF Core migrations against the test database
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SixToFixDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
