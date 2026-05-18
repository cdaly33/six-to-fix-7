using Microsoft.AspNetCore.Identity;
using SixToFix.Application.Auth;
using SixToFix.Application.Data;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Auth;
using SixToFix.Infrastructure.Bootstrap;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core + PostgreSQL
        services.AddDbContext<SixToFixDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                    npgsql.CommandTimeout(30);
                })
            .UseSnakeCaseNamingConvention();
        });

        // ASP.NET Core Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<SixToFixDbContext>()
        .AddDefaultTokenProviders();

        // JWT token service
        services.AddScoped<ITokenService, JwtTokenService>();

        // Auth service (login + token re-issue)
        services.AddScoped<IAuthService, Services.AuthService>();

        // pgBouncer-aware connection factory
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();

        if (configuration.GetValue<bool>("SeedAdmin:Enabled"))
        {
            services.AddHostedService<AdminBootstrapHostedService>();
        }

        // Business + AI services
        services.AddBusinessServices(configuration);
        services.AddAiServices(configuration);

        return services;
    }
}
