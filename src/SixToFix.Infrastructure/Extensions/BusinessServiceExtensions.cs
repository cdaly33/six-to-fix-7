using SixToFix.Application.Services;
using SixToFix.Infrastructure.Services;

namespace SixToFix.Infrastructure.Extensions;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IPillarContentService, PillarContentService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IPlaybookTemplateService, PlaybookTemplateService>();
        return services;
    }
}
