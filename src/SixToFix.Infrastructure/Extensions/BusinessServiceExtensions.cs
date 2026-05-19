using SixToFix.Application.Services;
using SixToFix.Infrastructure.Services;

namespace SixToFix.Infrastructure.Extensions;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;

        services.AddScoped<IAuditOrchestrator, AuditOrchestrator>();
        services.AddScoped<IReviewerWorkflow, ReviewerWorkflow>();
        services.AddScoped<IPublisher, Publisher>();
        services.AddScoped<ICalibrationTracker, CalibrationTracker>();
        services.AddScoped<ITelemetryCollector, TelemetryCollector>();
        services.AddScoped<IClientService, ClientService>();

        // StrategyHub services (Phase 4)
        services.AddScoped<IPillarContentService, PillarContentService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IPlaybookTemplateService, PlaybookTemplateService>();

        return services;
    }
}
