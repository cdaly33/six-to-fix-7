namespace SixToFix.Api.Endpoints;

public static class ApiEndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Health check
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
            .AllowAnonymous()
            .WithName("HealthCheck");

        // Phase 2+: Neo will add auth, audit, admin endpoints here
        return app;
    }
}
