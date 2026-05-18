using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using SixToFix.Infrastructure.Auth;

namespace SixToFix.Infrastructure.Bootstrap;

public sealed class AdminBootstrapHostedService : BackgroundService
{
    private const string SuperAdminRole = "SuperAdmin";

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

            var role = await roleManager.FindByNameAsync(SuperAdminRole);
            if (role is not null)
            {
                var existingSuperAdmins = await userManager.GetUsersInRoleAsync(SuperAdminRole);
                if (existingSuperAdmins.Count > 0)
                {
                    _logger.LogInformation("SuperAdmin already exists; skipping bootstrap");
                    return;
                }
            }

            var email = _configuration["SeedAdmin:Email"]?.Trim();
            var password = _configuration["SeedAdmin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("SeedAdmin bootstrap is enabled but email or password is missing; skipping bootstrap");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (role is null)
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(SuperAdminRole));
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Failed to create SuperAdmin role: {Errors}", FormatErrors(roleResult));
                    return;
                }
            }

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

            var roleAssignmentResult = await userManager.AddToRoleAsync(user, SuperAdminRole);
            if (!roleAssignmentResult.Succeeded)
            {
                _logger.LogError("Failed to assign SuperAdmin role to user {UserId}: {Errors}", user.Id, FormatErrors(roleAssignmentResult));
                return;
            }

            _logger.LogInformation("Bootstrap SuperAdmin user {UserId} created", user.Id);
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

    private static string FormatErrors(IdentityResult result)
    {
        return string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
    }
}
