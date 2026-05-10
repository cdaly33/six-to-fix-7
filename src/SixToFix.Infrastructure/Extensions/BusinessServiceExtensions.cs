using System.Threading.Channels;
using SixToFix.Application.Models;
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

        services.AddSingleton(_ => Channel.CreateUnbounded<HubSpotEvent>());

        return services;
    }
}
