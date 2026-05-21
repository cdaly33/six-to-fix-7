using SixToFix.Application.Services;
using SixToFix.Infrastructure.Services;

namespace SixToFix.Infrastructure.Extensions;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IPillarContentService, PillarContentService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IPlaybookTemplateService, PlaybookTemplateService>();
        services.AddScoped<ITenantService, TenantService>();
        // Singleton: deployment metadata is immutable for the lifetime of the process
        services.AddSingleton<IDeploymentInfoService>(sp =>
            new DeploymentInfoService(sp.GetRequiredService<IConfiguration>()));
        return services;
    }
}
