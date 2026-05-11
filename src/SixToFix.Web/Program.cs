using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SixToFix.Api.Endpoints;
using SixToFix.Application.Extensions;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Extensions;
using SixToFix.Infrastructure.Multitenancy;
using SixToFix.Web.Middleware;
using SixToFix.Web.Navigation;
using SixToFix.Web.Realtime;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

// Authentication — JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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

// Authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"))
    .AddPolicy("TenantAdmin", p => p.RequireRole("SuperAdmin", "TenantAdmin"))
    .AddPolicy("Reviewer", p => p.RequireRole("SuperAdmin", "TenantAdmin", "Reviewer"))
    .AddPolicy("Viewer", p => p.RequireRole("SuperAdmin", "TenantAdmin", "Reviewer", "Viewer"));

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<ILoginNavigator, LoginNavigator>();

// SignalR — ARR Affinity required for Blazor Server (see ADR-001)
builder.Services.AddSignalR();

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

// SignalR hub
app.MapHub<SixToFix.Infrastructure.Hubs.AuditRunHub>("/hubs/audit-run");

app.Run();

// For WebApplicationFactory test discovery
public partial class Program { }
