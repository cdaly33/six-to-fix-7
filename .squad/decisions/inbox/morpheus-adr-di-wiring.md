# ADR: Dependency Injection Wiring & Service Lifetimes

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  
**Supersedes:** —  

---

## Context

The StrategicGlue Six-to-Fix application hosts Blazor Server, ASP.NET Core REST endpoints, SignalR hubs, and background services in a single .NET 10 process. Eight domain services of varying statefulness, plus cross-cutting infrastructure clients, must be registered in ASP.NET Core's built-in DI container with lifetimes that are correct for multi-tenant SaaS.

Key constraints that drive this decision:

- **Multi-tenancy:** Every Scoped service must resolve `tenant_id` from the ambient HTTP request. Singleton services must never hold per-tenant state.
- **pgBouncer:** EF Core `DbContext` uses transaction-mode pooling on port 6432. Scoped lifetime for `DbContext` aligns with request scope and prevents connection leaks.
- **Blazor Server circuit:** Blazor components share the circuit's DI scope for the lifetime of the connection. Scoped services used from Blazor components must be safe for use across the circuit lifetime, not just per-HTTP-request.
- **PolicyEngine:** Declared stateless in the PRD — no database calls, no per-tenant mutable state. Safe as Singleton.
- **AI clients (Azure OpenAI, Blob, Search, HubSpot):** `HttpClient`-backed. Registered via `IHttpClientFactory` with Polly pipelines attached. The factory is Singleton; resolved `HttpClient` instances are transient from the factory's perspective but pipeline-safe.
- **No third-party DI containers.** ASP.NET Core's built-in container only.

---

## Decision

All eight domain services and all cross-cutting services are registered with lifetimes as specified in the table below. The registration code lives exclusively in `Program.cs` (or extension methods called from `Program.cs`), organized into logical `Add*` extension methods on `IServiceCollection`.

### DI Lifetime Table

| Service / Interface | Concrete Type | Lifetime | Justification |
|---|---|---|---|
| `IAuditOrchestrator` | `AuditOrchestrator` | **Scoped** | Coordinates a single audit run. Depends on `DbContext`, tenant context. Must not span requests. |
| `ISkillRunner` | `SkillRunner` | **Scoped** | Executes per-run AI calls. Depends on `IAIClient` (via factory), `DbContext`. One instance per orchestration scope. |
| `IPolicyEngine` | `PolicyEngine` | **Singleton** | Pure stateless functions. No DB calls. No per-tenant state. Safe to share across all requests. |
| `ICouncilRunner` | `CouncilRunner` | **Scoped** | Invokes AI personas. Depends on `IAIClient` (via factory), `DbContext`. Per-run scope. |
| `IReviewerWorkflow` | `ReviewerWorkflow` | **Scoped** | Enforces reviewer actions and lockout rule. Requires `DbContext` and tenant context. |
| `IPublisher` | `Publisher` | **Scoped** | Assembles and persists immutable published audit. Requires `DbContext`. |
| `ICalibrationTracker` | `CalibrationTracker` | **Scoped** | Logs `CalibrationDelta` on every score override. Requires `DbContext`. |
| `ITelemetryCollector` | `TelemetryCollector` | **Scoped** | Records daily run metrics. Requires `DbContext`. |
| `IAIClient` | `AzureOpenAIClient` (wrapped) | **Transient** (from `IHttpClientFactory`) | `HttpClient`-backed. Polly pipeline attached at factory registration. Each call site gets a fresh instance from the factory; no state carried between calls. |
| `IBlobStorage` | `AzureBlobStorageClient` | **Transient** (from `IHttpClientFactory`) | Same pattern as `IAIClient`. Managed identity credential resolved once at startup. |
| `ISearchClient` | `AzureSearchClient` | **Transient** (from `IHttpClientFactory`) | Same pattern. |
| `IHubSpotClient` | `HubSpotClient` | **Transient** (from `IHttpClientFactory`) | Same pattern. HMAC secret loaded from Key Vault at startup via managed identity. |
| `IDbConnectionFactory` | `NpgsqlConnectionFactory` | **Singleton** | Wraps Npgsql connection string (pgBouncer port 6432). Stateless factory; `DbContext` is Scoped and manages actual connection lifecycle. |
| `SixToFixDbContext` | `SixToFixDbContext` | **Scoped** | EF Core `DbContext`. One per request/circuit. Applies global query filters for `tenant_id`. Connects through pgBouncer on port 6432. |
| `ITenantContext` | `HttpTenantContext` | **Scoped** | Extracts `tenant_id` and `tenant_slug` from the ambient `IHttpContextAccessor`. Resolved by all Scoped services. |
| `ICurrentUser` | `HttpCurrentUser` | **Scoped** | Exposes `UserId`, `Roles` from `ClaimsPrincipal`. Used by `ReviewerWorkflow` for lockout checks. |
| ASP.NET Core Identity | `UserManager<AppUser>`, `SignInManager<AppUser>`, etc. | **Scoped** (framework default) | Standard Identity registration via `AddIdentity<AppUser, AppRole>()`. |
| JWT bearer auth | — | **Singleton** (framework) | Registered via `AddAuthentication().AddJwtBearer(...)`. Key validation parameters loaded once at startup. |
| SignalR | `AuditRunHub` | **Transient** (hub instances) | Hub class per-invocation. Registered via `AddSignalR()`. Hub context (`IHubContext<AuditRunHub>`) is Singleton. |
| `IHubContext<AuditRunHub>` | — | **Singleton** | Injected into `AuditOrchestrator` and `CouncilRunner` to push events. Safe to hold as Singleton; internally thread-safe. |
| `Channel<HubSpotEvent>` | Unbounded channel | **Singleton** | In-memory async queue. Produced by webhook endpoint; consumed by `HubSpotBackgroundWorker`. Must outlive requests. |
| `HubSpotBackgroundWorker` | `IHostedService` | **Singleton** (hosted service) | Registered via `AddHostedService<HubSpotBackgroundWorker>()`. Reads from `Channel<HubSpotEvent>`. Creates its own `IServiceScope` for `DbContext` access. |
| Polly pipelines | `ResiliencePipeline` (per AI client) | **Singleton** | Registered via `AddResiliencePipeline(name, ...)`. Circuit breaker state is per-pipeline-instance; must be Singleton to function correctly. |
| Health checks | `IHealthCheck` implementations | **Transient** (framework default) | Registered via `AddHealthChecks().AddCheck<DbHealthCheck>(...)` etc. |

### DI Registration Organization

All registrations are grouped into extension methods called from `Program.cs`:

```csharp
builder.Services
    .AddDomainServices()          // 8 domain services, all Scoped
    .AddInfrastructureClients()   // IAIClient, IBlobStorage, ISearchClient, IHubSpotClient — IHttpClientFactory + Polly
    .AddTenantContext()           // ITenantContext, ICurrentUser
    .AddPersistence(config)       // SixToFixDbContext, IDbConnectionFactory
    .AddIdentityAndAuth(config)   // ASP.NET Core Identity + JWT bearer
    .AddSignalRHub()              // SignalR + IHubContext
    .AddBackgroundWorkers()       // Channel<HubSpotEvent> + HubSpotBackgroundWorker
    .AddResiliencePipelines()     // Polly — all AI call pipelines
    .AddPlatformHealthChecks();   // /health endpoint checks
```

---

## Tenant Context Propagation

The `tenant_id` flows from JWT claim to EF Core query filter via this chain:

```
[JWT Bearer Token]
  └─ "tenant_id" claim (Guid)
       └─ ClaimsPrincipal (set by JWT bearer middleware)
            └─ IHttpContextAccessor.HttpContext.User
                 └─ HttpTenantContext : ITenantContext
                      ├─ .TenantId (Guid) — read on first access, cached for request
                      └─ .TenantSlug (string)
                           └─ SixToFixDbContext (Scoped)
                                └─ HasQueryFilter(e => e.TenantId == tenantContext.TenantId)
                                     └─ Applied to all IQueryable<TEntity> for tenant-scoped entities
```

**HttpTenantContext** is injected into `SixToFixDbContext` via constructor:

```csharp
public class SixToFixDbContext(DbContextOptions<SixToFixDbContext> options, ITenantContext tenant)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Applied to every tenant-scoped entity type
        mb.Entity<Client>().HasQueryFilter(e => e.TenantId == tenant.TenantId);
        mb.Entity<Audit>().HasQueryFilter(e => e.TenantId == tenant.TenantId);
        // ... all 13 tenant-scoped tables
    }
}
```

**Missing tenant context** (e.g., unauthenticated request reaching a service): `HttpTenantContext.TenantId` throws `UnauthorizedAccessException` if `tenant_id` claim is absent. This is a safety net — authorization middleware should prevent unauthenticated calls from reaching services in the first place.

**Blazor Server note:** Blazor components run on a persistent SignalR circuit. `IHttpContextAccessor.HttpContext` is `null` after the initial HTTP handshake. For Blazor, `ITenantContext` is seeded from the circuit's initial `AuthenticationState` and stored in a Scoped `BlazorTenantContext` that reads from `AuthenticationStateProvider` rather than `IHttpContextAccessor`. The `AddTenantContext()` extension registers the correct implementation based on whether the call is from a Blazor circuit or an HTTP request — handled by registering `HttpTenantContext` for HTTP middleware and providing `CascadingAuthenticationState` + `BlazorTenantContext` for Blazor component scopes.

---

## Consequences

### Testing

- Scoped services are easily testable with `WebApplicationFactory<Program>` and a test `DbContext` scoped to the test.
- `PolicyEngine` (Singleton) is directly instantiatable in unit tests with `new PolicyEngine()` — no DI required.
- `ITenantContext` must be mocked or provided via test seeding in integration tests. A `TestTenantContext` implementation returning a fixed `TenantId` is registered in the test `WebApplicationFactory`.
- `IHubContext<AuditRunHub>` must be mocked in unit tests of `AuditOrchestrator` and `CouncilRunner`.

### Program.cs Structure

`Program.cs` is the single orchestration point for DI registration but delegates detail to extension methods. Each extension method is defined in a dedicated file under `Infrastructure/DependencyInjection/`. `Program.cs` itself remains readable and sequential.

### Constructor Shapes

Services receive only interfaces in their constructors — no concrete types except `DbContext`. `ILogger<T>` is injected in all domain services. `IOptions<T>` is used for configuration; no `IConfiguration` is injected directly into domain services.

---
