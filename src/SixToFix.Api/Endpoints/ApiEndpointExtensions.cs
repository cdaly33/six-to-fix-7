using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SixToFix.Api.Models;
using SixToFix.Application.Auth;
using SixToFix.Application.Services;

namespace SixToFix.Api.Endpoints;

public static class ApiEndpointExtensions
{
    private static AuthorizeAttribute BearerPolicy(string? policy = null) => new() { AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = policy };
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app) { app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow })).AllowAnonymous().WithName("HealthCheck"); MapAuthEndpoints(app); return app; }
    private static void MapAuthEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext httpContext, IAuthService authService, ILogger<LoginRequest> logger, CancellationToken ct) => { if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) return Results.Problem("Email and password are required.", statusCode: 400); var result = await authService.LoginAsync(request.Email, request.Password, ct); if (result is null) { logger.LogWarning("Failed login attempt for email hash {EmailHash}", request.Email.GetHashCode()); return Results.Problem("Invalid credentials.", statusCode: 401); } await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(result)); return Results.Ok(new LoginResponse(result.AccessToken, result.Email, result.UserId, result.TenantId, result.Roles)); }).AllowAnonymous().WithName("Login");
        app.MapPost("/api/auth/refresh", async (HttpContext httpContext, IAuthService authService, CancellationToken ct) => { var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier); if (!Guid.TryParse(userIdClaim, out var userId)) return Results.Problem("Invalid token.", statusCode: 401); var result = await authService.ReissueTokenAsync(userId, ct); return result is null ? Results.Problem("User not found or inactive.", statusCode: 401) : Results.Ok(new LoginResponse(result.AccessToken, result.Email, result.UserId, result.TenantId, result.Roles)); }).RequireAuthorization(BearerPolicy()).WithName("RefreshToken");
        app.MapGet("/account/logout", async (HttpContext httpContext) => { await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.Redirect("/login"); }).AllowAnonymous().WithName("Logout");
    }
    private static ClaimsPrincipal BuildPrincipal(LoginResult result) { var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, result.UserId.ToString()), new(ClaimTypes.Name, result.Email), new(ClaimTypes.Email, result.Email), new("tenant_id", result.TenantId.ToString()), new("tenant_slug", result.TenantSlug) }; claims.AddRange(result.Roles.Select(role => new Claim(ClaimTypes.Role, role))); return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)); }
    private static Guid GetUserId(HttpContext ctx) => Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static bool VerifyTenantOwnership(HttpContext ctx, Guid? tenantId) => true;
}
