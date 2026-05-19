using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using SixToFix.Infrastructure.Auth;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Bootstrap;

public sealed class AdminBootstrapHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBootstrapHostedService> _logger;

    public AdminBootstrapHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AdminBootstrapHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => RunBootstrapAsync(stoppingToken), CancellationToken.None);
    }

    public async Task RunBootstrapAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_configuration.GetValue<bool>("SeedAdmin:Enabled"))
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var db = scope.ServiceProvider.GetRequiredService<SixToFixDbContext>();

            await EnsureRolesAsync(roleManager, cancellationToken);
            await EnsureSuperAdminAsync(userManager, roleManager, cancellationToken);
            await SeedPillarContentForAllTenantsAsync(db, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SuperAdmin bootstrap was canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SuperAdmin bootstrap failed; continuing host startup");
        }
    }

    private async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager, CancellationToken cancellationToken)
    {
        string[] requiredRoles = [Roles.SuperAdmin, Roles.TenantAdmin, Roles.Client];
        foreach (var roleName in requiredRoles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await roleManager.FindByNameAsync(roleName) is null)
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!result.Succeeded)
                    _logger.LogError("Failed to create role {Role}: {Errors}", roleName, FormatErrors(result));
                else
                    _logger.LogInformation("Role {Role} created", roleName);
            }
        }
    }

    private async Task EnsureSuperAdminAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        var existingSuperAdmins = await userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
        if (existingSuperAdmins.Count > 0)
        {
            _logger.LogInformation("SuperAdmin already exists; skipping bootstrap");
            return;
        }

        var email = _configuration["SeedAdmin:Email"]?.Trim();
        var password = _configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("SeedAdmin bootstrap is enabled but email or password is missing; skipping bootstrap");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            TenantId = Guid.Empty,
            TenantSlug = "platform",
            FullName = "Bootstrap SuperAdmin",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            _logger.LogError("Failed to create bootstrap SuperAdmin user: {Errors}", FormatErrors(createResult));
            return;
        }

        var roleAssignmentResult = await userManager.AddToRoleAsync(user, Roles.SuperAdmin);
        if (!roleAssignmentResult.Succeeded)
        {
            _logger.LogError("Failed to assign SuperAdmin role to user {UserId}: {Errors}", user.Id, FormatErrors(roleAssignmentResult));
            return;
        }

        _logger.LogInformation("Bootstrap SuperAdmin user {UserId} created", user.Id);
    }

    private async Task SeedPillarContentForAllTenantsAsync(SixToFixDbContext db, CancellationToken cancellationToken)
    {
        var tenants = db.Tenants.ToList();
        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SeedPillarContentForTenantAsync(db, tenant.Id, cancellationToken);
        }
    }

    internal async Task SeedPillarContentForTenantAsync(SixToFixDbContext db, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string emptyBody = """{"strategy":[],"execution":[],"templates":[],"examples":[],"metrics":[]}""";

        foreach (Pillar pillar in Enum.GetValues<Pillar>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exists = db.PillarContents.Any(p => p.TenantId == tenantId && p.Pillar == pillar);
            if (exists) continue;

            db.PillarContents.Add(new PillarContent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Pillar = pillar,
                Title = $"{pillar} Strategy",
                Subtitle = string.Empty,
                BodyJson = emptyBody,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Pillar content seeded for tenant {TenantId}", tenantId);
    }

    private static string FormatErrors(IdentityResult result)
    {
        return string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
    }
}

