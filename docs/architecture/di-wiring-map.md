# DI Wiring Map — StrategicGlue Six-to-Fix

**Author:** Morpheus (Lead & Architect)  
**Date:** 2026-05-10  
**Status:** Locked (Phase 0)  
**Source of truth:** See also `.squad/decisions/inbox/morpheus-adr-di-wiring.md`

---

## Service Registration Table

All services registered in `Program.cs` via extension methods on `IServiceCollection`. The table below is the complete reference.

| Service / Interface | Concrete Type | Lifetime | Registration Method | Justification |
|---|---|---|---|---|
| **Domain Services** | | | | |
| `IAuditOrchestrator` | `AuditOrchestrator` | Scoped | `AddScoped` | Coordinates single audit run. Depends on `DbContext`, `ITenantContext`, `IHubContext`. |
| `ISkillRunner` | `SkillRunner` | Scoped | `AddScoped` | Executes per-run AI calls. Depends on `IAIClient` (factory), `DbContext`. |
| `IPolicyEngine` | `PolicyEngine` | Singleton | `AddSingleton` | Pure stateless functions. No DB, no per-tenant state. Thread-safe. |
| `ICouncilRunner` | `CouncilRunner` | Scoped | `AddScoped` | Invokes AI personas per run. Depends on `IAIClient` (factory), `DbContext`. |
| `IReviewerWorkflow` | `ReviewerWorkflow` | Scoped | `AddScoped` | Reviewer actions + lockout rule. Requires `DbContext`, `ITenantContext`. |
| `IPublisher` | `Publisher` | Scoped | `AddScoped` | Assembles immutable audit publish artifact. Requires `DbContext`. |
| `ICalibrationTracker` | `CalibrationTracker` | Scoped | `AddScoped` | Logs `CalibrationDelta` on score overrides. Requires `DbContext`. |
| `ITelemetryCollector` | `TelemetryCollector` | Scoped | `AddScoped` | Records daily run metrics. Requires `DbContext`. |
| **Policy Rules** | | | | |
| `IPolicyRule` | `LowConfidenceRule` | Singleton | `AddSingleton` | Stateless rule; Singleton safe. |
| `IPolicyRule` | `MissingEvidenceRule` | Singleton | `AddSingleton` | Stateless rule. |
| `IPolicyRule` | `BenchmarkOutlierRule` | Singleton | `AddSingleton` | Stateless rule. |
| `IPolicyRule` | `InsufficientEvidenceRule` | Singleton | `AddSingleton` | Stateless rule. |
| `IPolicyRule` | `ScoreStrategyMismatchRule` | Singleton | `AddSingleton` | Stateless rule. |
| **Infrastructure Clients** | | | | |
| `IAIClient` | `AzureOpenAIClient` (wrapped) | Transient (via factory) | `AddHttpClient<IAIClient, AzureOpenAIClient>().AddResiliencePipeline("ai")` | Per-call instance from `IHttpClientFactory`. Polly pipeline attached at factory. |
| `IBlobStorage` | `AzureBlobStorageClient` | Transient (via factory) | `AddHttpClient<IBlobStorage, AzureBlobStorageClient>()` | Same pattern as `IAIClient`. Managed identity credential. |
| `ISearchClient` | `AzureSearchClient` | Transient (via factory) | `AddHttpClient<ISearchClient, AzureSearchClient>()` | Same pattern. |
| `IHubSpotClient` | `HubSpotClient` | Transient (via factory) | `AddHttpClient<IHubSpotClient, HubSpotClient>().AddResiliencePipeline("hubspot")` | HMAC secret from Key Vault at startup. |
| **Persistence** | | | | |
| `SixToFixDbContext` | `SixToFixDbContext` | Scoped | `AddDbContext<SixToFixDbContext>` | One per request/circuit. Applies global tenant query filters. **NOT pooled** (`AddDbContextPool` is not used — incompatible with per-request query filter capture). |
| `IDbConnectionFactory` | `NpgsqlConnectionFactory` | Singleton | `AddSingleton` | Stateless factory wrapping pgBouncer connection string (port 6432). Used for raw Dapper queries where needed. |
| **Tenant & Identity Context** | | | | |
| `ITenantContext` | `HttpTenantContext` | Scoped | `AddScoped` | Reads `tenant_id` claim from `IHttpContextAccessor.HttpContext.User`. |
| `ICurrentUser` | `HttpCurrentUser` | Scoped | `AddScoped` | Exposes `UserId`, `Roles` from `ClaimsPrincipal`. |
| `IHttpContextAccessor` | (framework) | Singleton | `AddHttpContextAccessor()` | Required by `HttpTenantContext` and `HttpCurrentUser`. |
| **ASP.NET Core Identity** | | | | |
| `UserManager<AppUser>` | (framework) | Scoped | `AddIdentity<AppUser, AppRole>()` | Standard Identity registration. |
| `SignInManager<AppUser>` | (framework) | Scoped | `AddIdentity<AppUser, AppRole>()` | Standard Identity registration. |
| `RoleManager<AppRole>` | (framework) | Scoped | `AddIdentity<AppUser, AppRole>()` | Standard Identity registration. |
| `IPasswordHasher<AppUser>` | (framework) | Transient | `AddIdentity<AppUser, AppRole>()` | Standard Identity registration. |
| **Authentication & JWT** | | | | |
| JWT Bearer Handler | (framework) | Singleton | `AddAuthentication().AddJwtBearer(...)` | Validates app-issued tokens. Key loaded from Key Vault at startup via managed identity. Token from `Authorization: Bearer ...` header or `?access_token=` query string (hub). |
| Authorization Policies | (framework) | Singleton | `AddAuthorization(options => ...)` | `TenantUser`, `TenantAdmin`, `Auditor`, `Reviewer`, `SuperAdmin` policies. |
| **SignalR** | | | | |
| SignalR Core | (framework) | Singleton (infrastructure) | `AddSignalR()` | Hub instances are Transient. |
| `IHubContext<AuditRunHub>` | (framework) | Singleton | Registered automatically by `AddSignalR()` | Injected into `AuditOrchestrator`, `CouncilRunner` for pushing events. |
| **Background Workers** | | | | |
| `Channel<HubSpotEvent>` | `Channel<HubSpotEvent>` (unbounded) | Singleton | `AddSingleton(Channel.CreateUnbounded<HubSpotEvent>())` | In-memory async queue. Written by webhook endpoint; consumed by background worker. |
| `ChannelReader<HubSpotEvent>` | (from channel) | Singleton | `AddSingleton(sp => sp.GetRequiredService<Channel<HubSpotEvent>>().Reader)` | Convenience registration for background worker injection. |
| `ChannelWriter<HubSpotEvent>` | (from channel) | Singleton | `AddSingleton(sp => sp.GetRequiredService<Channel<HubSpotEvent>>().Writer)` | Injected into HubSpot webhook endpoint handler. |
| `HubSpotBackgroundWorker` | `HubSpotBackgroundWorker` | Singleton (IHostedService) | `AddHostedService<HubSpotBackgroundWorker>()` | Reads from `ChannelReader<HubSpotEvent>`. Creates `IServiceScope` per event for `DbContext` access. |
| **Polly Resilience Pipelines** | | | | |
| Polly AI Pipeline | `ResiliencePipeline` "ai" | Singleton (pipeline registry) | `AddResiliencePipeline("ai", builder => ...)` | 60s timeout → 3 retries exponential (429/5xx) → circuit breaker (50% failure, 60s break). |
| Polly HubSpot Pipeline | `ResiliencePipeline` "hubspot" | Singleton (pipeline registry) | `AddResiliencePipeline("hubspot", builder => ...)` | 30s timeout → 2 retries → no circuit breaker (HubSpot webhook calls are non-critical). |
| **Health Checks** | | | | |
| `IHealthCheck` (DB) | `DbHealthCheck` | Transient | `AddHealthChecks().AddCheck<DbHealthCheck>("db")` | Pings PostgreSQL via a lightweight query. |
| `IHealthCheck` (Storage) | `BlobStorageHealthCheck` | Transient | `.AddCheck<BlobStorageHealthCheck>("storage")` | Checks Azure Blob Storage reachability. |
| `IHealthCheck` (Search) | `SearchHealthCheck` | Transient | `.AddCheck<SearchHealthCheck>("search")` | Checks Azure AI Search service. |
| `IHealthCheck` (OpenAI) | `OpenAIHealthCheck` | Transient | `.AddCheck<OpenAIHealthCheck>("openai")` | Lightweight Azure OpenAI connectivity check. |
| `IHealthCheck` (KeyVault) | `KeyVaultHealthCheck` | Transient | `.AddCheck<KeyVaultHealthCheck>("keyVault")` | Verifies Key Vault access via managed identity. |
| **Observability** | | | | |
| `ILogger<T>` | (framework) | Singleton (factory), Transient (loggers) | `AddLogging()` + Application Insights sink | All domain services receive `ILogger<T>` via constructor. |
| Application Insights | (framework) | Singleton | `AddApplicationInsightsTelemetry()` | Telemetry sink for logs, traces, dependencies. |
| `IMemoryCache` | (framework) | Singleton | `AddMemoryCache()` | Used by `TenantActiveCheck` policy (5-min cache) and benchmark data pre-load. |
| `IOptions<T>` variants | (framework) | Singleton | `AddOptions()` + `Configure<T>(config)` | All configuration is injected via `IOptions<T>`. No `IConfiguration` in domain services. |

---

## Middleware Pipeline Order

The order of `app.Use*` calls in `Program.cs`. **Order is mandatory — changing it breaks auth and tenant isolation.**

```csharp
// 1. Exception handling — must be outermost to catch all exceptions
app.UseExceptionHandler("/error");

// 2. HTTPS redirection
app.UseHttpsRedirection();

// 3. Static files (CSS, JS, images) — before auth for performance
app.UseStaticFiles();

// 4. Routing — must come before UseAuthentication to enable route matching
app.UseRouting();

// 5. CORS (if needed for external API consumers)
app.UseCors("ExternalApiPolicy");

// 6. Authentication — validates JWT, populates HttpContext.User
app.UseAuthentication();

// 7. Authorization — enforces [Authorize] attributes and policies
app.UseAuthorization();

// 8. Tenant resolution — reads tenant_id from validated User claim
//    Runs AFTER UseAuthorization so unauthenticated requests are already rejected
app.UseMiddleware<TenantResolutionMiddleware>();

// 9. Correlation ID — reads/generates X-Correlation-ID, stores in HttpContext
app.UseMiddleware<CorrelationIdMiddleware>();

// 10. Endpoint mapping
app.MapControllers();                                // REST endpoints (Minimal API style)
app.MapHub<AuditRunHub>("/hubs/audit-run");          // SignalR hub
app.MapBlazorHub();                                  // Blazor Server circuit
app.MapFallbackToPage("/_Host");                     // Blazor fallback
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
});
```

### Why TenantResolutionMiddleware is AFTER UseAuthorization

`UseAuthorization` rejects unauthenticated requests before they reach tenant resolution. If `TenantResolutionMiddleware` ran before `UseAuthorization`, it would execute on unauthenticated requests and have no `ClaimsPrincipal` to read from. Placing it after ensures the `tenant_id` claim is always available when middleware executes.

### Why CorrelationIdMiddleware is AFTER TenantResolutionMiddleware

Correlation IDs may be enriched with `tenant_id` for log correlation. Reading the tenant context first means the correlation ID middleware can include it in the `ILogger` scope.

---

## Extension Method Structure

`Program.cs` calls these extension methods in order:

```csharp
// Builder phase
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPersistence(builder.Configuration)
    .AddTenantContext()
    .AddIdentityAndAuth(builder.Configuration)
    .AddDomainServices()
    .AddInfrastructureClients(builder.Configuration)
    .AddResiliencePipelines()
    .AddSignalRHub()
    .AddBackgroundWorkers()
    .AddPlatformHealthChecks()
    .AddObservability(builder.Configuration);

var app = builder.Build();

// Pipeline phase (middleware order as above)
```

Each `Add*` extension method is defined in a dedicated file under `src/Infrastructure/DependencyInjection/`:

| File | Extension Method |
|---|---|
| `PersistenceServiceExtensions.cs` | `AddPersistence` |
| `TenantContextServiceExtensions.cs` | `AddTenantContext` |
| `IdentityAuthServiceExtensions.cs` | `AddIdentityAndAuth` |
| `DomainServiceExtensions.cs` | `AddDomainServices` |
| `InfrastructureClientExtensions.cs` | `AddInfrastructureClients` |
| `ResiliencePipelineExtensions.cs` | `AddResiliencePipelines` |
| `SignalRServiceExtensions.cs` | `AddSignalRHub` |
| `BackgroundWorkerExtensions.cs` | `AddBackgroundWorkers` |
| `HealthCheckExtensions.cs` | `AddPlatformHealthChecks` |
| `ObservabilityExtensions.cs` | `AddObservability` |

---
