using Microsoft.EntityFrameworkCore.Design;
using SixToFix.Application.Multitenancy;

namespace SixToFix.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Migrations are NOT run during Phase 1 scaffolding.
/// Run `dotnet ef migrations add InitialCreate -p src/SixToFix.Infrastructure -s src/SixToFix.Web`
/// after Phase 1 is committed and the solution builds.
/// Connection string: set DESIGN_TIME_CONNECTION_STRING env var to the sf_admin connection string.
/// </summary>
public sealed class SixToFixDbContextFactory : IDesignTimeDbContextFactory<SixToFixDbContext>
{
    public SixToFixDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DESIGN_TIME_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=sixtofix;Username=sf_admin;Password=dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<SixToFixDbContext>();
        optionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

        return new SixToFixDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }
}

/// <summary>Used only by EF Core tooling at design time.</summary>
internal sealed class DesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public string TenantSlug => string.Empty;
    public bool IsResolved => false;
}
