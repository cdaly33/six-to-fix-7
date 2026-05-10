# Decision: Multi-Tenant EF Core Query Filter Pattern

**Author:** Neo (Backend Dev)  
**Date:** 2026-05-10  
**Status:** Proposed — Pending Team Ratification  
**Scope:** All EF Core data access in StrategicGlue Six-to-Fix

---

## Decision

Use **EF Core Global Query Filters** to enforce tenant isolation on all tenant-scoped entities. This is the primary and only mechanism for tenant data scoping — callers never pass `tenant_id` explicitly.

---

## EF Core Approach

**Global Query Filters** are applied in `OnModelCreating` via `modelBuilder.Entity<T>().HasQueryFilter(...)` for every tenant-scoped entity:

```csharp
// Example pattern — applied to ALL tenant-scoped entities
modelBuilder.Entity<Client>()
    .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);

modelBuilder.Entity<AuditRun>()
    .HasQueryFilter(ar => ar.TenantId == _tenantContext.TenantId);

// ... repeated for every tenant-scoped entity
```

The filter expression is evaluated at query time — EF Core injects it as a `WHERE tenant_id = @tenantId` clause into every `SELECT`, `UPDATE`, and `DELETE` generated for that entity. It cannot be forgotten by calling code.

---

## Tenant Context Injection into DbContext

`AppDbContext` receives a `ITenantContext` interface (not `IHttpContextAccessor` directly) to decouple tenant resolution from HTTP transport:

```csharp
public interface ITenantContext
{
    Guid? TenantId { get; }
    string? TenantSlug { get; }
}

public class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor)
        => _accessor = accessor;

    public Guid? TenantId =>
        _accessor.HttpContext?.User?.FindFirst("tenant_id") is { } claim
        && Guid.TryParse(claim.Value, out var id)
            ? id
            : null;

    public string? TenantSlug =>
        _accessor.HttpContext?.User?.FindFirst("tenant_slug")?.Value;
}
```

`AppDbContext` constructor:

```csharp
public AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext) : base(options)
{
    _tenantContext = tenantContext;
}
```

`ITenantContext` is registered as **Scoped** in DI. `AppDbContext` is **Scoped**.

---

## DbContext Lifetime

**Scoped** — required for HTTP-request-scoped tenant resolution. A new `AppDbContext` instance is created per HTTP request (or per Blazor circuit for server-side rendering). This ensures tenant identity captured at request start is used consistently for all queries in that request.

---

## Null Tenant Context: Background Workers and Migrations

When `ITenantContext.TenantId` is `null` (background workers, EF Core migration tool, seed operations), the global query filter must behave safely:

**Resolution: Guard clause in filter expression:**

```csharp
modelBuilder.Entity<AuditRun>()
    .HasQueryFilter(ar =>
        _tenantContext.TenantId == null
        || ar.TenantId == _tenantContext.TenantId);
```

When `TenantId` is `null`:
- The filter passes through all rows — **background workers see all tenants' data**.
- This is intentional for: `Channel<HubSpotEvent>` worker, telemetry aggregation, migration runner.
- Background workers are responsible for querying with explicit `tenant_id` conditions where data isolation is required.
- EF Core `IgnoreQueryFilters()` is also available for specific queries that need cross-tenant visibility (Super Admin operations, `TelemetryCollector.GetDailyMetricsAsync`).

**For migrations:** `ITenantContext` is registered as a no-op null implementation during the migration tool run. EF Core sees `TenantId == null` → filter passes all rows → schema operations are unobstructed.

---

## Index Strategy

All tenant-scoped tables carry a **composite index on `(tenant_id, id)`** as the primary lookup index. Additional composite indexes exist per table for domain-specific access patterns:

| Table | Composite Indexes |
|-------|------------------|
| `users` | `(tenant_id, id)`, `normalized_email` UNIQUE |
| `user_roles` | `(tenant_id, user_id)`, `(user_id, role)` UNIQUE |
| `clients` | `(tenant_id, id)`, `(tenant_id, slug)` UNIQUE |
| `audit_runs` | `(tenant_id, id)`, `(tenant_id, client_id)`, `(tenant_id, slug)` UNIQUE |
| `category_results` | `(tenant_id, audit_run_id)`, `(audit_run_id, category)` UNIQUE |
| `category_result_versions` | `(tenant_id, audit_run_id, category_id)`, `(audit_run_id, category_id, version_number)` UNIQUE |
| `skill_runs` | `(tenant_id, audit_run_id)`, `(audit_run_id, skill_name)` |
| `policy_flags` | `(tenant_id, audit_run_id)`, `(audit_run_id, category_id)` |
| `council_decisions` | `(tenant_id, audit_run_id)`, `(audit_run_id, category_id)` |
| `reviewer_actions` | `(tenant_id, audit_run_id, category_id, reviewer_id)`, `(audit_run_id, category_id, action_type, created_at)` |
| `calibration_deltas` | `(tenant_id, audit_run_id)`, `(reviewer_id, created_at)` |
| `documents` | `(tenant_id, client_id)`, `search_index_id` |
| `hubspot_sync_events` | `(tenant_id, created_at)`, `(hubspot_portal_id, hubspot_subscription_id, occurred_at)` UNIQUE |
| `telemetry_events` | `(tenant_id, event_date)`, `(audit_run_id)` UNIQUE |

Rationale: `tenant_id` as the leading column of the primary composite index ensures PostgreSQL uses a single B-tree scan per tenant — no full table scans when `tenant_id` is selective (which it is in all runtime queries).

---

## sf_app Role: Permitted and Prohibited Operations

The `sf_app` role is the **runtime application role**. It is used for all database connections from the ASP.NET Core application via pgBouncer (port 6432).

**`sf_app` CAN:**
- `SELECT` on all tables
- `INSERT` on all tables
- `UPDATE` on all tables **except** `category_result_versions` (append-only enforcement)
- `DELETE` on no tables (enforced — soft deletes only via `is_active` flags)

**`sf_app` CANNOT:**
- `UPDATE` on `category_result_versions` — REVOKED explicitly
- `DELETE` on `category_result_versions` — REVOKED explicitly
- `DELETE` on any table — REVOKED globally for `sf_app`
- `TRUNCATE` on any table
- `DROP` any table or schema object
- `CREATE` any table, index, or schema object
- `ALTER` any table or schema object
- Execute `GRANT` or `REVOKE`
- Access the `sf_admin` schema objects or sequences for DDL

`sf_app` has `USAGE` on sequences (for generated PKs) and `EXECUTE` on non-DDL functions only.

---

## Migration Strategy

Migrations are run by the `sf_admin` role, which has full DDL + DML privileges.

- **Tool:** `dotnet ef migrations add` / `dotnet ef database update`
- **Connection:** Uses `sf_admin` credentials from Key Vault, not the runtime `sf_app` connection string
- **CI/CD:** Migrations are applied as a step in `deploy-app.yml` before the application starts, using a dedicated migration-runner command (`dotnet run --project src/Migrations`)
- **Query filter during migrations:** `ITenantContext` is registered as a null-returning implementation so all global query filters pass through (no `WHERE tenant_id = NULL` clauses appended to DDL)
- **No tenant_id DEFAULT in migrations:** EF Core migrations do not specify application-level `tenant_id` defaults — the application always sets `tenant_id` explicitly before `SaveChangesAsync()`
