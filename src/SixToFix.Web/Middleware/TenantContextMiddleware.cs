using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Multitenancy;

namespace SixToFix.Web.Middleware;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
            var tenantSlugClaim = context.User.FindFirst("tenant_slug")?.Value;

            if (Guid.TryParse(tenantIdClaim, out var tenantId) && !string.IsNullOrEmpty(tenantSlugClaim))
            {
                if (tenantContext is HttpContextTenantContext mutable)
                {
                    mutable.Resolve(tenantId, tenantSlugClaim);
                }
            }
            else
            {
                _logger.LogWarning("Authenticated request missing tenant claims. Path: {Path}", context.Request.Path);
            }
        }

        await _next(context);
    }
}
