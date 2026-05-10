namespace SixToFix.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application-layer service registrations
        // Implementations in Infrastructure will register their own interfaces
        // This method reserves space for CQRS handlers, validators, etc. in Phase 2+
        return services;
    }
}
