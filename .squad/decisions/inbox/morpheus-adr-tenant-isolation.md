# ADR: Multi-Tenant Data Isolation Pattern

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  
**Supersedes:** —  

---

## Context

StrategicGlue Six-to-Fix is strict multi-tenant SaaS. All 15 database tables that are tenant-scoped carry a `tenant_id` foreign key referencing the `tenants` table. A query that returns data across tenant boundaries — even one row — is a critical security failure. The isolation layer must be enforced automatically, not by convention.

Three candidate patterns were evaluated:

1. **Application service layer check** — each service method adds a `.Where(e => e.TenantId == tenantId)` manually. Fragile: any omission is a data leak. Rejected.
2. **Repository layer filter** — a generic `TenantRepository<T>` wraps all queries with a tenant filter. Better, but still requires every data access path to flow through the repository. Rejected as incomplete — EF Core navigation properties and raw projections bypass it.
3. **EF Core Global Query Filters (`HasQueryFilter`)** — filters applied at the `DbContext` model configuration level, enforced on every LINQ query including navigations. Cannot be forgotten by individual developers. **Selected.**

---

## Decision

### Enforcement Mechanism: EF Core Global Query Filters

Tenant isolation is enforced exclusively via EF Core's `HasQueryFilter` mechanism in `SixToFixDbContext`. Every entity type that is tenant-scoped has a query filter applied at model configuration time that constrains all queries to the ambient `ITenantContext.TenantId`.

This is the **single, authoritative** enforcement point. Application service code does not add additional `.Where(e => e.TenantId == ...)` clauses — that would be redundant and would create a false sense of correctness if the filter were ever disabled.

**Entities covered by global query filters (all 13 tenant-scoped tables):**

```
clients, audits, audit_runs, skill_runs, category_payloads,
category_result_versions, policy_flags, council_decisions,
reviewer_actions, reviewer_rejections, calibration_deltas,
hubspot_sync_log, telemetry_daily_snapshots
```

**Entities NOT filtered (platform-level tables):**

```
tenants, users (AspNetUsers — cross-tenant by design, isolated via Identity)
```

### Filter Configuration Pattern

```csharp
protected override void OnModelCreating(ModelBuilder mb)
{
    // tenant context is captured at DbContext construction time (Scoped lifetime)
    var tenantId = _tenantContext.TenantId;

    mb.Entity<Client>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<Audit>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<AuditRun>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<SkillRun>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<CategoryPayload>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<CategoryResultVersion>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<PolicyFlag>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<CouncilDecision>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<ReviewerAction>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<ReviewerRejection>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<CalibrationDelta>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<HubSpotSyncLog>().HasQueryFilter(e => e.TenantId == tenantId);
    mb.Entity<TelemetryDailySnapshot>().HasQueryFilter(e => e.TenantId == tenantId);
}
```

The `tenantId` value is captured as a local variable at model-building time from the Scoped `ITenantContext`. Because `SixToFixDbContext` is Scoped, `OnModelCreating` is called once per scope with the correct tenant ID.

> **Note:** EF Core caches the compiled model. Global query filters that capture instance values (not lambdas over navigable properties) must be expressed carefully. The `tenantId` local variable approach is the standard pattern and is safe with EF Core's per-context model compilation when using `UseModel` or when context pooling is disabled. **Context pooling (`AddDbContextPool`) is NOT used** — it is incompatible with per-request query filters that capture instance state. Plain `AddDbContext` is used.

### Middleware → Claim Extraction → Service Registration → EF Core Filter Chain

```
HTTP Request arrives
  │
  ▼
[UseAuthentication]
  — JWT bearer middleware validates token signature, expiry, issuer
  — Populates HttpContext.User (ClaimsPrincipal)
  │
  ▼
[UseAuthorization]
  — Enforces [Authorize] attributes and policies
  — Rejects unauthenticated requests before they reach services
  │
  ▼
[TenantResolutionMiddleware]
  — Reads HttpContext.User.FindFirst("tenant_id")
  — Validates claim is present and is a valid Guid
  — If missing or invalid → 401 Unauthorized (short-circuits pipeline)
  — Stores resolved Guid in HttpContext.Items["TenantId"] as fast-path cache
  │
  ▼
[Endpoint / Blazor Component / SignalR Hub Method]
  — Injects ITenantContext (Scoped)
  │
  ▼
[HttpTenantContext : ITenantContext]
  — Reads TenantId from HttpContext.Items["TenantId"] (already validated)
  — Returns Guid tenant_id
  │
  ▼
[SixToFixDbContext] (Scoped, constructed with ITenantContext)
  — Global query filters applied with captured tenant_id
  — Every LINQ query restricted to tenant's rows
```

### Tenant Claim Validation

On every authenticated request:

1. JWT signature validated by `AddJwtBearer` middleware (public key from Key Vault at startup).
2. `tenant_id` claim extracted and validated as a non-empty Guid by `TenantResolutionMiddleware`.
3. The tenant's existence in the `tenants` table is **not** re-validated on every request — it is validated at login time when the JWT is issued. If a tenant is deactivated after token issuance, a `TenantActiveCheck` policy enforcer on sensitive endpoints performs a cached DB lookup (5-minute sliding cache via `IMemoryCache`).

### Behavior When Tenant Context Is Missing

| Scenario | Behavior |
|---|---|
| No JWT token | `UseAuthentication` sets `IsAuthenticated = false`; `UseAuthorization` rejects with 401 |
| JWT present, `tenant_id` claim missing | `TenantResolutionMiddleware` returns 401 with `MISSING_TENANT_CLAIM` |
| `tenant_id` claim not a valid Guid | `TenantResolutionMiddleware` returns 401 with `INVALID_TENANT_CLAIM` |
| Blazor circuit, no HTTP context | `BlazorTenantContext` reads from `AuthenticationState`; same validation applies |
| `ITenantContext.TenantId` accessed without middleware (e.g., background worker) | Throws `InvalidOperationException` — background workers must create their own scope with explicit tenant context |

### Migrations

Migrations run under the `sf_admin` role, which has DDL + DML. The migration `DbContext` is a **separate subclass** (`MigrationDbContext`) that does NOT inject `ITenantContext` and does NOT apply global query filters. This prevents migration tooling from requiring a tenant context to exist.

```csharp
// Used only by migration tooling (dotnet ef migrations ...)
public class MigrationDbContext(DbContextOptions<MigrationDbContext> options)
    : SixToFixDbContext(options, TenantContext.None)
{
    // TenantContext.None returns Guid.Empty; query filters disabled via IgnoreQueryFilters() in migration tooling
}
```

The `sf_app` role (DML only, used at runtime) cannot perform DDL. Migrations are applied in CI/CD via a dedicated step running as `sf_admin`. The runtime app connects as `sf_app`.

### SuperAdmin Cross-Tenant Queries

SuperAdmin endpoints (e.g., `GET /api/admin/tenants`) use a dedicated `SuperAdminDbContext` that does NOT register global query filters. SuperAdmin endpoints are protected by the `SuperAdmin` role claim. These contexts are never used in tenant-scoped code paths.

Alternatively, specific queries can call `.IgnoreQueryFilters()` on a regular `SixToFixDbContext` within a SuperAdmin-authorized code path — but this is discouraged. The `SuperAdminDbContext` approach is preferred for clarity.

---

## Consequences

### Testing

- Integration tests must seed a `TenantId` via a `TestTenantContext` registered in `WebApplicationFactory`. Tests that assert cross-tenant isolation create two tenants and verify that one tenant's data is not returned for the other.
- The `MigrationDbContext` pattern means `dotnet ef migrations add` can run without a running application or tenant context.
- Unit tests of services that use `SixToFixDbContext` via `UseInMemoryDatabase` must provide a `TestTenantContext` — there is no way to use the real `DbContext` in unit tests without it.

### Migration Tooling

- Two separate `DbContext` types (`SixToFixDbContext` for runtime, `MigrationDbContext` for migrations) must be kept in sync in terms of entity configuration. The model is shared; filters are the only difference.
- `MigrationDbContext` is registered only when `ASPNETCORE_ENVIRONMENT=MigrationTools` or invoked via `IDesignTimeDbContextFactory`.

### Global Query Filter Bypass Risk

- EF Core allows `IgnoreQueryFilters()` to bypass filters. Any use of `IgnoreQueryFilters()` in non-SuperAdmin code paths must be treated as a critical security review finding. A Roslyn analyzer rule (Ralph's domain) will flag any call to `IgnoreQueryFilters()` outside of `SuperAdminDbContext` or `MigrationDbContext`.

---
