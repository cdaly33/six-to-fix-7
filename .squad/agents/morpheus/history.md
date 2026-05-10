# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT (custom tenant claims), EF Core, Azure PostgreSQL Flexible Server (pgBouncer on 6432), Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service (B2/P2v3), Azure Key Vault (managed identity), Azure Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — app issues its own tokens with `tenant_id`, `tenant_slug`, `roles` claims. No OIDC server (Duende/OpenIddict not used).
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase 0 — Architectural Decisions Locked (2026-05-10)

**Service Lifetimes:**
- `PolicyEngine` is Singleton — pure stateless functions, no DB, thread-safe. All other 7 domain services are Scoped (one per request/circuit). `IAIClient`, `IBlobStorage`, `ISearchClient`, `IHubSpotClient` are Transient via `IHttpClientFactory` with Polly pipelines attached at factory registration. `IHubContext<AuditRunHub>` is Singleton. `HubSpotBackgroundWorker` is a Singleton `IHostedService` that creates its own `IServiceScope` per event for `DbContext` access.
- `AddDbContextPool` is explicitly NOT used — it is incompatible with per-request query filters that capture `ITenantContext` instance state. Plain `AddDbContext<SixToFixDbContext>` is used.

**Tenant Isolation Approach:**
- EF Core `HasQueryFilter` is the single enforcement point. Applied to all 13 tenant-scoped entity types in `SixToFixDbContext.OnModelCreating`. `tenant_id` is captured from `ITenantContext` (Scoped) at `DbContext` construction time.
- `TenantResolutionMiddleware` runs AFTER `UseAuthorization` — unauthenticated requests are rejected before tenant resolution. A `MigrationDbContext` subclass (injecting `TenantContext.None`) is used for `dotnet ef migrations` so tooling never requires a tenant context.
- SuperAdmin cross-tenant queries use a dedicated `SuperAdminDbContext` without global filters. Any use of `IgnoreQueryFilters()` in non-SuperAdmin paths is a critical security finding.

**SignalR Contract:**
- Hub: `/hubs/audit-run`. Clients join a group keyed by `auditRunId`. 7 event types: `skill-started`, `skill-completed`, `skill-failed`, `council-started`, `council-completed`, `run-completed`, `run-failed`. Hub validates tenant ownership in `JoinRun` before adding connection to group. JWT passed via `?access_token=` query string for WebSocket connections.
- Missed events on reconnect are NOT replayed. Client must re-call `JoinRun` and refetch run state via service/API to resync.
- Azure App Service sticky sessions are required for Blazor Server circuits.

**Policy Engine Extensibility:**
- `IPolicyRule` interface: `string RuleName` + `PolicyFlag? Evaluate(CategoryPayload, AuditContext)`. Rules are registered explicitly as `AddSingleton<IPolicyRule, ConcreteRule>()`. `PolicyEngine` receives `IEnumerable<IPolicyRule>`. Rules execute in parallel via `Parallel.ForEach`. A rule that throws is logged at ERROR and produces no flag — it never aborts the audit run.
- All `PolicyFlag` evaluations (including Warnings) are persisted to the `policy_flags` table.
- Benchmark median/stddev context is pre-loaded by `AuditOrchestrator` and passed into `AuditContext` — PolicyEngine itself makes no DB calls.

**AI Council State Machine:**
- PolicyEngine evaluates per-skill (after each skill completes), not post-chain.
- Council escalation is synchronous within the audit run — `AuditOrchestrator` awaits `CouncilRunner.RunAsync` before proceeding to the next skill. The run does not complete until all triggered categories have a `CouncilDecision`.
- Any skill failure (schema validation, timeout, circuit open, API error) causes immediate chain abort. `AuditRun` → `failed`. No partial-success continuation.
- `SkillRunner` never throws for expected AI failures — returns `SkillRunResult` with `Status = Failed`. Unexpected exceptions propagate to `AuditOrchestrator`'s top-level handler.

**Reviewer Lockout Semantics:**
- Lockout scoped to `(tenant_id, audit_run_id, category_id)`. Tracked in `reviewer_rejections` table. 24-hour window is rolling (`now() - 24h`), not reset-on-rejection.
- Race condition handled via `pg_advisory_xact_lock` + `ISOLATION LEVEL SERIALIZABLE` transaction. The lock key is derived from `hashtext(audit_run_id + '|' + category_id)`.
- Lockout is a computed state (count query), not a persisted record. No background job needed — expires naturally as rejections age out of the 24h window.
- `CalibrationDelta` is NOT created when a lockout is triggered — no override occurred.
- `lockoutExpiresAt` is included in the 409 response body (earliest rejection in window + 24h).
- Advisory lock used is `pg_advisory_xact_lock` (transaction-scoped) — compatible with pgBouncer transaction-mode pooling.
