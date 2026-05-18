using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Auth;
using SixToFix.Infrastructure.Bootstrap;
using SixToFix.Infrastructure.Data;
using SixToFix.Infrastructure.Extensions;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Bootstrap;

[Trait("Category", "Unit")]
public sealed class AdminBootstrapHostedServiceTests
{
    private const string ValidPassword = "Bootstrap1!Pass";

    [Fact]
    public async Task WhenSeedAdminDisabled_DoesNothing()
    {
        var fixture = BuildFixture(new Dictionary<string, string?>
        {
            ["SeedAdmin:Enabled"] = "false",
            ["SeedAdmin:Email"] = "admin@example.com",
            ["SeedAdmin:Password"] = ValidPassword
        });

        await fixture.Service.RunBootstrapAsync();

        var users = await fixture.Provider.GetRequiredService<UserManager<ApplicationUser>>().Users.ToListAsync();
        users.Should().BeEmpty();
    }

    [Fact]
    public void WhenSeedAdminDisabled_DoesNotRegisterHostedService()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=sixtofix;Username=sf_app;Password=dev_password",
            ["SeedAdmin:Enabled"] = "false"
        });
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(AdminBootstrapHostedService));
    }

    [Fact]
    public async Task WhenEnabledAndNoSuperAdminExists_CreatesConfirmedUserAndAssignsRole()
    {
        var fixture = BuildFixture(new Dictionary<string, string?>
        {
            ["SeedAdmin:Enabled"] = "true",
            ["SeedAdmin:Email"] = "admin@example.com",
            ["SeedAdmin:Password"] = ValidPassword
        });

        await fixture.Service.RunBootstrapAsync();

        using var assertionScope = fixture.Provider.CreateScope();
        var userManager = assertionScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = assertionScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var dbContext = assertionScope.ServiceProvider.GetRequiredService<SixToFixDbContext>();
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == "admin@example.com");

        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeTrue();
        user.TenantId.Should().Be(Guid.Empty);
        user.TenantSlug.Should().Be("platform");
        (await roleManager.RoleExistsAsync("SuperAdmin")).Should().BeTrue();
        (await userManager.IsInRoleAsync(user, "SuperAdmin")).Should().BeTrue();
    }

    [Fact]
    public async Task WhenSuperAdminAlreadyExists_SkipsBootstrap()
    {
        var fixture = BuildFixture(new Dictionary<string, string?>
        {
            ["SeedAdmin:Enabled"] = "true",
            ["SeedAdmin:Email"] = "new-admin@example.com",
            ["SeedAdmin:Password"] = ValidPassword
        });
        var userManager = fixture.Provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = fixture.Provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        (await roleManager.CreateAsync(new IdentityRole<Guid>("SuperAdmin"))).Succeeded.Should().BeTrue();
        var existing = new ApplicationUser
        {
            UserName = "existing@example.com",
            Email = "existing@example.com",
            EmailConfirmed = true,
            TenantId = Guid.Empty,
            TenantSlug = "platform",
            FullName = "Existing SuperAdmin"
        };
        (await userManager.CreateAsync(existing, ValidPassword)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(existing, "SuperAdmin")).Succeeded.Should().BeTrue();

        await fixture.Service.RunBootstrapAsync();

        userManager.Users.Should().ContainSingle();
        fixture.Logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("SuperAdmin already exists; skipping bootstrap", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, "Bootstrap1!Pass")]
    [InlineData("", "Bootstrap1!Pass")]
    [InlineData("admin@example.com", null)]
    [InlineData("admin@example.com", "")]
    public async Task WhenEmailOrPasswordMissing_LogsWarningAndDoesNotThrow(string? email, string? password)
    {
        var fixture = BuildFixture(new Dictionary<string, string?>
        {
            ["SeedAdmin:Enabled"] = "true",
            ["SeedAdmin:Email"] = email,
            ["SeedAdmin:Password"] = password
        });

        var act = () => fixture.Service.RunBootstrapAsync();

        await act.Should().NotThrowAsync();
        fixture.Provider.GetRequiredService<UserManager<ApplicationUser>>().Users.Should().BeEmpty();
        fixture.Logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("email or password is missing", StringComparison.Ordinal));
    }

    private static TestFixture BuildFixture(Dictionary<string, string?> settings)
    {
        var configuration = BuildConfiguration(settings);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ITenantContext, TestTenantContext>();
        services.AddLogging();
        services.AddDataProtection();
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        services.AddDbContext<SixToFixDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<SixToFixDbContext>()
        .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var logger = new ListLogger<AdminBootstrapHostedService>();
        var service = new AdminBootstrapHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            logger);

        return new TestFixture(provider, service, logger);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private sealed record TestFixture(
        ServiceProvider Provider,
        AdminBootstrapHostedService Service,
        ListLogger<AdminBootstrapHostedService> Logger);

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string TenantSlug => string.Empty;
        public bool IsResolved => false;
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
