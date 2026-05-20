using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SixToFix.Api.Models;
using SixToFix.Application.Auth;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Domain.Entities;

namespace SixToFix.Api.Endpoints;

public static class ApiEndpointExtensions
{
    private static AuthorizeAttribute BearerPolicy(string? policy = null) => new() { AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = policy };

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow })).AllowAnonymous().WithName("HealthCheck");
        MapAuthEndpoints(app);
        MapClientEndpoints(app);
        return app;
    }

    private static void MapAuthEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext httpContext, IAuthService authService, ILogger<LoginRequest> logger, CancellationToken ct) => { if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) return Results.Problem("Email and password are required.", statusCode: 400); var result = await authService.LoginAsync(request.Email, request.Password, ct); if (result is null) { logger.LogWarning("Failed login attempt for email hash {EmailHash}", request.Email.GetHashCode()); return Results.Problem("Invalid credentials.", statusCode: 401); } await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(result)); return Results.Ok(new LoginResponse(result.AccessToken, result.Email, result.UserId, result.TenantId, result.Roles)); }).AllowAnonymous().WithName("Login");
        app.MapPost("/api/auth/refresh", async (HttpContext httpContext, IAuthService authService, CancellationToken ct) => { var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier); if (!Guid.TryParse(userIdClaim, out var userId)) return Results.Problem("Invalid token.", statusCode: 401); var result = await authService.ReissueTokenAsync(userId, ct); return result is null ? Results.Problem("User not found or inactive.", statusCode: 401) : Results.Ok(new LoginResponse(result.AccessToken, result.Email, result.UserId, result.TenantId, result.Roles)); }).RequireAuthorization(BearerPolicy()).WithName("RefreshToken");
        app.MapGet("/account/logout", async (HttpContext httpContext) => { await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.Redirect("/login"); }).AllowAnonymous().WithName("Logout");
    }

    private static void MapClientEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clients");

        group.MapGet("/", async (HttpContext ctx, IClientService service, Guid? tenantId, CancellationToken ct) =>
        {
            var resolvedTenantId = ResolveTenantId(ctx, tenantId);
            if (resolvedTenantId is null) return Results.Problem("tenant_id claim is missing or invalid.", statusCode: 400);
            if (!CanAccessTenant(ctx, resolvedTenantId.Value, tenantId)) return Results.Forbid();

            var clients = await service.GetAllForTenantAsync(resolvedTenantId.Value, ct);
            return Results.Ok(clients.Select(ToDto));
        }).RequireAuthorization(BearerPolicy("Viewer")).WithName("GetClients");

        group.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, IClientService service, Guid? tenantId, CancellationToken ct) =>
        {
            var resolvedTenantId = ResolveTenantId(ctx, tenantId);
            if (resolvedTenantId is null) return Results.Problem("tenant_id claim is missing or invalid.", statusCode: 400);
            if (!CanAccessTenant(ctx, resolvedTenantId.Value, tenantId)) return Results.Forbid();

            var client = await service.GetByIdAsync(id, resolvedTenantId.Value, ct);
            return client is null ? Results.NotFound() : Results.Ok(ToDto(client));
        }).RequireAuthorization(BearerPolicy("Viewer")).WithName("GetClientById");

        group.MapPost("/", async (CreateClientDto dto, HttpContext ctx, IClientService service, Guid? tenantId, CancellationToken ct) =>
        {
            var validation = Validate(dto);
            if (validation is not null) return validation;

            var resolvedTenantId = ResolveTenantId(ctx, tenantId);
            if (resolvedTenantId is null) return Results.Problem("tenant_id claim is missing or invalid.", statusCode: 400);
            if (!CanAccessTenant(ctx, resolvedTenantId.Value, tenantId)) return Results.Forbid();

            try
            {
                var id = await service.CreateAsync(dto, resolvedTenantId.Value, ct);
                var client = await service.GetByIdAsync(id, resolvedTenantId.Value, ct);
                return Results.Created($"/api/clients/{id}", ToDto(client!));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireAuthorization(BearerPolicy("TenantAdmin")).WithName("CreateClient");

        group.MapPut("/{id:guid}", async (Guid id, UpdateClientDto dto, HttpContext ctx, IClientService service, Guid? tenantId, CancellationToken ct) =>
        {
            var validation = Validate(dto);
            if (validation is not null) return validation;

            var resolvedTenantId = ResolveTenantId(ctx, tenantId);
            if (resolvedTenantId is null) return Results.Problem("tenant_id claim is missing or invalid.", statusCode: 400);
            if (!CanAccessTenant(ctx, resolvedTenantId.Value, tenantId)) return Results.Forbid();

            try
            {
                var client = await service.UpdateAsync(id, dto, resolvedTenantId.Value, ct);
                return client is null ? Results.NotFound() : Results.Ok(ToDto(client));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireAuthorization(BearerPolicy("TenantAdmin")).WithName("UpdateClient");

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, IClientService service, Guid? tenantId, CancellationToken ct) =>
        {
            var resolvedTenantId = ResolveTenantId(ctx, tenantId);
            if (resolvedTenantId is null) return Results.Problem("tenant_id claim is missing or invalid.", statusCode: 400);
            if (!CanAccessTenant(ctx, resolvedTenantId.Value, tenantId)) return Results.Forbid();

            var deleted = await service.DeleteAsync(id, resolvedTenantId.Value, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(BearerPolicy("TenantAdmin")).WithName("DeleteClient");
    }

    private static ClaimsPrincipal BuildPrincipal(LoginResult result) { var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, result.UserId.ToString()), new(ClaimTypes.Name, result.Email), new(ClaimTypes.Email, result.Email), new("tenant_id", result.TenantId.ToString()), new("tenant_slug", result.TenantSlug) }; claims.AddRange(result.Roles.Select(role => new Claim(ClaimTypes.Role, role))); return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)); }

    private static ClientDto ToDto(Client client) => new(client.Id, client.TenantId, client.Name, client.ContactEmail, client.Notes, client.CreatedAt, client.UpdatedAt);

    private static Guid? ResolveTenantId(HttpContext ctx, Guid? requestedTenantId)
    {
        if (requestedTenantId.HasValue && ctx.User.IsInRole("SuperAdmin")) return requestedTenantId.Value;
        return Guid.TryParse(ctx.User.FindFirstValue("tenant_id"), out var tenantId) ? tenantId : null;
    }

    private static bool CanAccessTenant(HttpContext ctx, Guid resolvedTenantId, Guid? requestedTenantId)
    {
        if (!requestedTenantId.HasValue || ctx.User.IsInRole("SuperAdmin")) return true;
        return Guid.TryParse(ctx.User.FindFirstValue("tenant_id"), out var claimTenantId) && claimTenantId == resolvedTenantId;
    }

    private static IResult? Validate<T>(T dto)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(dto!);
        if (Validator.TryValidateObject(dto!, context, results, validateAllProperties: true)) return null;

        var errors = results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty), (result, member) => new { member, result.ErrorMessage })
            .GroupBy(e => e.member)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage ?? "Invalid value.").ToArray());

        return Results.ValidationProblem(errors);
    }
}
