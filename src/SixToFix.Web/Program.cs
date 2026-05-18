using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SixToFix.Api.Endpoints;
using SixToFix.Application.Extensions;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Extensions;
using SixToFix.Infrastructure.Multitenancy;
using SixToFix.Web.Middleware;
using SixToFix.Web.Navigation;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Key Vault configuration (only when KV URI is configured — skipped in local dev)
var kvUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(kvUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(kvUri), new Azure.Identity.DefaultAzureCredential());
}

// Core services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Multi-tenancy
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();
builder.Services.AddScoped<HttpContextTenantContext>();  // so middleware can cast

// Authentication — dual scheme:
//   Cookie  → default for the browser channel (Blazor Server pages, login flow).
//   Bearer  → /api/* endpoints (machine clients, SPA fetch, server-to-server).
// Rationale: Blazor Server pages render on the server during HTTP navigations,
// so credentials must travel as a cookie (browser-attached automatically).
// JS-attached Authorization headers don't exist for SSR navs. See
// .squad/decisions/inbox/morpheus-dual-auth-scheme.md.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "sixtofix.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ReturnUrlParameter = "returnUrl";

        // API callers must see a clean 401/403, never a 302 to /login.
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            var redirectUri = Uri.TryCreate(ctx.RedirectUri, UriKind.Absolute, out var parsedUri)
                ? parsedUri.PathAndQuery + parsedUri.Fragment
                : ctx.RedirectUri;
            ctx.Response.Redirect(redirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            var redirectUri = Uri.TryCreate(ctx.RedirectUri, UriKind.Absolute, out var parsedUri)
                ? parsedUri.PathAndQuery + parsedUri.Fragment
                : ctx.RedirectUri;
            ctx.Response.Redirect(redirectUri);
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey not configured")))
        };
    });

// Authorization policies.
// Policies accept BOTH schemes so the same named policy works whether the
// caller is a Blazor SSR page (cookie) or an /api/* client (bearer).
// API endpoints under /api/* are additionally pinned to JwtBearer via the
// BearerPolicy helper in ApiEndpointExtensions.
static AuthorizationPolicyBuilder DualScheme(AuthorizationPolicyBuilder p) =>
    p.AddAuthenticationSchemes(
        CookieAuthenticationDefaults.AuthenticationScheme,
        JwtBearerDefaults.AuthenticationScheme);

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder(
            CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("SuperAdmin", p => DualScheme(p).RequireAuthenticatedUser().RequireRole("SuperAdmin"))
    .AddPolicy("TenantAdmin", p => DualScheme(p).RequireAuthenticatedUser().RequireRole("SuperAdmin", "TenantAdmin"))
    .AddPolicy("Reviewer", p => DualScheme(p).RequireAuthenticatedUser().RequireRole("SuperAdmin", "TenantAdmin", "Reviewer"))
    .AddPolicy("Viewer", p => DualScheme(p).RequireAuthenticatedUser().RequireRole("SuperAdmin", "TenantAdmin", "Reviewer", "Viewer"));

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<ILoginNavigator, LoginNavigator>();



// Antiforgery
builder.Services.AddAntiforgery();

// HTTP context accessor (for Blazor Server components to access claims)
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Middleware pipeline order is critical — see di-wiring-map.md
app.UseMiddleware<CorrelationIdMiddleware>();        // 1. Correlation ID (first)
app.UseHttpsRedirection();                          // 2.
app.UseStaticFiles();                               // 3.
app.UseRouting();                                   // 4.
app.UseAuthentication();                            // 5.
app.UseAuthorization();                             // 6.
app.UseMiddleware<TenantContextMiddleware>();        // 7. After auth, before endpoints
app.UseAntiforgery();                               // 8.

// Map API endpoints (defined in SixToFix.Api)
app.MapApiEndpoints();

// Blazor Server
app.MapRazorComponents<SixToFix.Web.App>()
    .AddInteractiveServerRenderMode();


app.MapDefaultEndpoints();

app.Run();

// For WebApplicationFactory test discovery
public partial class Program { }
