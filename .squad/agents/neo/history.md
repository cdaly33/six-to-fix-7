# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains (Brand, Customer, Offering, Communications, Sales, Management)
- **Stack:** .NET 10 LTS, ASP.NET Core, EF Core, Azure PostgreSQL Flexible Server (v16, pgBouncer on port 6432), ASP.NET Core Identity + JWT, Azure OpenAI Service (via IAIClient interface), Azure Blob Storage, Azure AI Search, Azure App Service, Azure Key Vault (managed identity)
- **Auth decision:** ASP.NET Core Identity + JWT — app issues JWTs with `tenant_id`, `tenant_slug`, `roles` claims. No OIDC server.
- **Database:** 15 tables, strict `tenant_id` FK isolation on all tenant-scoped tables. Append-only `category_result_versions` ledger. Two DB roles: `sf_admin` (DDL+DML, migrations), `sf_app` (DML only, runtime). pgBouncer on 6432.
- **Service layer (8 services):** AuditOrchestrator, SkillRunner, PolicyEngine (Singleton), CouncilRunner, ReviewerWorkflow, Publisher, CalibrationTracker, TelemetryCollector
- **Reviewer lockout:** 3 rejections of same category within 24h → HTTP 409 REVIEWER_REJECTION_LOCKOUT
- **Publishing:** Immutable, versioned. All 6 categories must be approved before publishing.
- **CalibrationDelta:** Created on every reviewer score override. Never skipped.
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-10 — Phase 0: Schema, Contracts, State Machines

**15-Table Schema — Key Decisions**
- `tenants` is the only shared (non-tenant-scoped) table. All 14 others carry `tenant_id NOT NULL FK → tenants.id`.
- Exception: `hubspot_sync_events.tenant_id` is nullable to support inbound HubSpot events that arrive before a tenant can be matched. EF Core filter: `tenant_id IS NULL OR tenant_id = :tenantId`.
- `category_result_versions` is APPEND-ONLY. The `sf_app` runtime role has `UPDATE` and `DELETE` REVOKED on this table at the DB level.
- `category_results` holds the "current" view (latest scores). It is updated by application logic within the same transaction that inserts a `category_result_versions` row — no triggers.
- `reviewer_actions.action_type` covers five values: `approve`, `reject`, `edit`, `rerun`, `escalate`.
- `calibration_deltas` records every reviewer score override — requires non-empty `override_reason_code` and `notes` (enforced at service layer).
- `documents.size_bytes` CHECK constraint enforces the 10MB upload limit at DB layer as defense-in-depth.
- `telemetry_events` has a UNIQUE index on `audit_run_id` — exactly one row per audit run.
- Primary composite indexes: `(tenant_id, id)` on every tenant-scoped table. Leading column is `tenant_id` for PostgreSQL index selectivity.

**Service Interface Contract Patterns**
- 8 services registered via interface. PolicyEngine is Singleton (stateless). All others are Scoped (DB-touching, per-request tenant context).
- Every external dependency injected via interface: `IAIClient`, `IBlobStorage`, `ISearchClient`, `IHubSpotClient`. No concrete instantiation inside services.
- Every service method accepts `CancellationToken`.
- All exceptions map to HTTP status codes: `NotFoundException` → 404, `ConflictException` → 409, `ValidationException` → 422, AI failures → 502/503/504.
- `ISearchClient.SearchAsync` requires `tenantId` as a mandatory parameter — the implementation enforces tenant scoping at the search layer.
- `ITelemetryCollector.GetDailyMetricsAsync` bypasses the global tenant query filter (SuperAdmin/OpsViewer cross-tenant operation).

**Lockout State Machine Decisions**
- Lockout scope: per `(tenant_id, audit_run_id, category_id, reviewer_id)` — per reviewer, not per category globally. A different reviewer can always act.
- 24-hour window: rolling from `NOW()`. No cron, no expiry table — the window is evaluated at query time.
- Transaction isolation: `READ COMMITTED` with optimistic retry. SERIALIZABLE rejected as unnecessarily contending.
- CalibrationDelta sequence for Edit: lockout check first → validate inputs → open transaction → insert CalibrationDelta → update category_results → insert category_result_versions → insert reviewer_actions → commit.
- HTTP 409 body: RFC 7807 format with `code: "REVIEWER_REJECTION_LOCKOUT"`. No PII in response body.

**Immutable Publish Semantics Decisions**
- Three triggers for new `category_result_versions` row: AI skill completion (`source_type='ai'`), council adjustment (`source_type='council'`, only if `decision_type='adjusted'`), reviewer edit (`source_type='reviewer'`).
- `version_number` is per `(audit_run_id, category_id)`, implemented as MAX+1 within the insert transaction. Unique constraint `(audit_run_id, category_id, version_number)` provides optimistic concurrency protection.
- Published state enforcement: service layer guard clause (`if status == Published → throw`). Not a DB constraint (cross-table CHECK not supported in PostgreSQL).
- Publish preconditions: all 6 `category_results.status = 'approved'`, `audit_runs.status = 'completed'`.
- No DB triggers anywhere — all consistency maintained by application-layer logic within EF Core transactions.

## Phase 1 — Infrastructure Scaffolding (2026-05-10)

### Completed

**Domain Entities (15 classes) — `src/SixToFix.Domain/Entities/`**
- Tenant, User, Client, Audit, AuditRun, CategoryConfig, SkillRun, CategoryResult, CategoryResultVersion, Policy, PolicyFlag, CouncilSession, HubSpotSyncQueue, BlobReference, ReviewerLockout
- All use `class` (not `record`) for EF Core compatibility
- All tenant-scoped entities have `Guid TenantId`
- Navigation properties configured for all FK relationships

**Infrastructure Auth**
- `ApplicationUser` (extends `IdentityUser<Guid>`) — `src/SixToFix.Infrastructure/Auth/ApplicationUser.cs`
- `JwtTokenService` — generates HMAC-SHA256 JWT tokens with tenant_id + tenant_slug claims

**Infrastructure Data**
- `SixToFixDbContext` — inherits `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`
- All 14 domain DbSets registered
- Global query filters with `IsResolved` guard (passthrough for background/admin, filter for HTTP requests)
- `UseSnakeCaseNamingConvention()` applied
- `ApplyConfigurationsFromAssembly` loads all 14 entity configurations
- `SixToFixDbContextFactory` — design-time factory for EF Core migrations (deferred to post-Phase 1)

**Entity Type Configurations (14 files) — `src/SixToFix.Infrastructure/Data/EntityConfigurations/`**
- All entities: explicit `ToTable()`, `HasKey()`, `HasMaxLength()`, FK relationships, indexes on TenantId and FK columns
- `CategoryResultVersionConfiguration` — append-only semantics documented
- `HasDefaultValueSql("now()")` on all `CreatedAt` (and `UpdatedAt` where applicable)

**Application Interfaces**
- `ITokenService` + `TokenRequest` record — `src/SixToFix.Application/Auth/ITokenService.cs`
- `IDbConnectionFactory` — pgBouncer-aware connection interface — `src/SixToFix.Application/Data/IDbConnectionFactory.cs`

**Infrastructure Implementations**
- `NpgsqlConnectionFactory` — appends `No Reset On Close=true` for pgBouncer compatibility
- `InfrastructureServiceExtensions.AddInfrastructureServices` — registers DbContext, Identity, ITokenService, IDbConnectionFactory

### Decisions Filed
- `.squad/decisions/inbox/neo-phase1-schema.md` — global query filter pattern, migrations deferral, EFCore.NamingConventions package requirement

### Not Done (by design)
- Migrations — deferred to post-Phase 1 build
- .csproj modifications (Morpheus owns)
- EFCore.NamingConventions package (Morpheus must add to Infrastructure csproj)

## Phase 3 — Minimal API Endpoints (2026-05-10)

### Completed
- 12 Minimal API endpoints in `SixToFix.Api/Endpoints/ApiEndpointExtensions.cs`
- Request record types in `SixToFix.Api/Models/ApiModels.cs`
- `IAuthService` / `AuthService` for UserManager-backed login (avoids coupling Api → Infrastructure)
- `IReviewerWorkflow.RejectAsync` — serializable tx + pg_advisory_xact_lock, increments ReviewerLockout counter
- `IPublisher.GetPublishedAuditByRunIdAsync(Guid)` — fetch by run ID vs client slug
- `ICalibrationTracker.GetCalibrationHistoryAsync(Guid clientId)` — cross-run history
- PR #11: `dev/phase-3-api-endpoints` → `main`

### Key Implementation Decisions
- `SixToFix.Api` only references `SixToFix.Application` — login routing through `IAuthService` keeps this clean
- HubSpot webhook reads raw body (EnableBuffering), validates HMAC via `IHubSpotClient.ValidateWebhookSignatureAsync`, pushes to `Channel<HubSpotEvent>`
- Auth policies in `Program.cs` are hierarchical: TenantAdmin policy includes SuperAdmin+TenantAdmin roles; Reviewer includes those plus Reviewer; Viewer includes all four. Endpoint `RequireAuthorization("TenantAdmin")` enforces this.
- `RejectAsync` reuses the same serializable-tx + pg_advisory_xact_lock pattern as `CheckLockoutAsync` so both read and write happen atomically
- `Program.cs` already had `app.MapApiEndpoints()` — no changes needed there

### Completed
- 5 Application service interfaces: IAuditOrchestrator, IReviewerWorkflow, IPublisher, ICalibrationTracker, ITelemetryCollector
- 9 Application models in SixToFix.Application.Models
- 14 domain exceptions in SixToFix.Application.Exceptions
- 3 new domain entities: TelemetryEvent, CalibrationDelta, ReviewerAction
- Entity configurations and DbContext registrations for all 3
- 5 Infrastructure service implementations

### Key Implementation Decisions
- ReviewerWorkflow lockout: serializable transaction + pg_advisory_xact_lock keyed on (categoryId, reviewerId) hash
- CalibrationDelta: CategoryId stored as string (matches Category column pattern in domain)
- Publisher: fetches SystemsMaturityScore and AiReadinessPct from SkillRun.ConfidenceScore by skill name
- AuditOrchestrator: checks Oracle-owned exception types by Name string to avoid coupling to Oracle's assembly
- TelemetryCollector.GetDailyMetricsAsync: uses IgnoreQueryFilters() for cross-tenant ops metrics
- HubSpot channel: Channel<HubSpotEvent> singleton registered in BusinessServiceExtensions; Oracle's HubSpotWorker consumes
- SignalR group key: auditRunId.ToString("N") (no dashes)
- ISkillRunner, IPolicyEngine, ICouncilRunner: created stubs — Oracle will replace with real implementations

### 2026-05-15 — SignalR Removal + Polling Status Endpoint

**SignalR Push Removed**
- Removed `IHubContext<AuditRunHub, IAuditRunHubClient>` injection from `AuditOrchestrator` and all hub push calls (`run-started`, `skill-started`, `skill-completed`, `run-completed`, `run-failed`).
- Removed `IRealtimeNotifier` injection from `SkillRunner` and `CouncilRunner` — all `NotifyAsync` calls removed.
- `AuditRunHub.cs`, `AuditRunHubNotifier.cs`, `IRealtimeNotifier.cs`, `AuditRunHubClientFactory.cs` left dormant (not deleted — mechanical swap-back if needed).
- Removed `builder.Services.AddSignalR()` and `app.MapHub<AuditRunHub>("/hubs/audit-run")` from `Program.cs`.
- Removed `services.AddScoped<IRealtimeNotifier, AuditRunHubNotifier>()` from `InfrastructureServiceExtensions`.

**Polling Status Endpoint Added**
- `GET /api/audit-runs/{id}/status` added to `ApiEndpointExtensions.cs`, backed by `IAuditOrchestrator.GetAuditRunStatusAsync`.
- Tenant isolation enforced via EF Core global query filter (returns null → 404, not 403, to avoid leaking existence).
- `AuditRunStatusResponse` record in `SixToFix.Application/Models/`.
- `completedSkillCount` = COUNT of SkillRuns with Status `completed` OR `failed`.
- `currentSkillName` = first SkillRun with Status `running`, or null.
- `failureReason` maps to `AuditRun.ErrorMessage`.

### 2026-05-16 — Aspire Integration

**Added .NET Aspire for local dev orchestration (branch: `feature/aspire-integration`)**
- AppHost project: `src/SixToFix.AppHost/` — orchestrates PostgreSQL container + SixToFix.Web in local dev
- ServiceDefaults project: `src/SixToFix.ServiceDefaults/` — OpenTelemetry, health checks, service discovery
- ServiceDefaults wired into `SixToFix.Web` via `builder.AddServiceDefaults()` (after CreateBuilder) and `app.MapDefaultEndpoints()` (before app.Run())
- PostgreSQL container orchestrated in dev via AppHost `AddPostgres("postgres").AddDatabase("sixtofix")`
- AppHost uses `Aspire.AppHost.Sdk` v13.3.3 as a NuGet-based project SDK (workload-based Aspire is deprecated in .NET 10)
- GrpcNetClient instrumentation omitted from ServiceDefaults — no stable NuGet release and project has no gRPC
- Production deployment (Azure App Service + Azure PostgreSQL) is unchanged
- Run locally with: `dotnet run --project src/SixToFix.AppHost`

### 2026-05-17 — Env-Gated SuperAdmin Bootstrap

- Added `AdminBootstrapHostedService` to create the first Identity SuperAdmin via `UserManager`/`RoleManager` only when `SeedAdmin:Enabled=true`.
- Seeder is idempotent: if any user already has canonical role `SuperAdmin`, startup logs and skips.
- Missing `SeedAdmin:Email` or `SeedAdmin:Password` logs a warning and does not crash the host.
- Production wiring uses Key Vault secrets `SeedAdmin--Email`/`SeedAdmin--Password` and App Service setting `SeedAdmin__Enabled`.

### 2026-05-18 — Prod Login 500 Fix

**Three-layer root cause cascade — not one bug, three:**

**1. `sf_app` PostgreSQL role did not exist.**
The runtime connection string (`DefaultConnection`) uses role `sf_app` on pgBouncer port 6432. That role was never created on the Azure PostgreSQL Flexible Server. pgBouncer returned `FATAL: no such user (SqlState 08P01)` on every DB call → unhandled `NpgsqlException` in `ExceptionHandlerMiddlewareImpl` → 500.
Fix: `CREATE ROLE sf_app WITH LOGIN PASSWORD '<from-KV>'` via psql as sfadmin.

**2. EF Core migrations had never been applied.**
The DB was empty — no tables, including no `AspNetUsers`, `AspNetRoles`, etc. The migration `20260516042353_InitialCreate` had never been run against prod.
Fix: `dotnet ef database update` with `DESIGN_TIME_CONNECTION_STRING` = AdminConnection (sfadmin, port 5432). Then granted DML to sf_app with `REVOKE UPDATE, DELETE ON category_result_versions` per append-only schema rule.

**3. `SeedAdmin--Password` KV secret failed Identity password complexity.**
The original password `GYyE3jnmvGJuMyjtNQAk` had no digit and no non-alphanumeric character. `UserManager.CreateAsync` returned `PasswordRequiresNonAlphanumeric` / `PasswordRequiresDigit`. The seeder caught the error and logged it, so the host stayed up, but the user was never created.
Fix: Updated KV secret `SeedAdmin--Password` to `GYyE3jnmvGJuMyjtNQAk1!` (appended `1!`). Restarted app; seeder ran successfully, user seeded. Login confirmed.

**Code fixes committed (PR #41):**
- `Program.cs`: added startup migration runner using `AdminConnection` (sfadmin). Every deploy now auto-applies pending migrations — no more manual `dotnet ef database update` required.
- New migration `20260519033146_GrantAppRolePermissions`: codifies GRANT/REVOKE on sf_app in source control; includes fail-fast DO block if sf_app doesn't exist.

**Gotchas for future:**
- When creating a new PostgreSQL Flexible Server, `sf_app` must be created manually (or via a deploy-infra step) BEFORE the app starts, since EF Core migrations need a working connection.
- The startup migration runner uses `AdminConnection` (port 5432 direct, DDL perms). `DefaultConnection` (port 6432 pgBouncer, DML-only) cannot run DDL — never swap these.
- `SeedAdmin--Password` in KV must meet Identity password policy: ≥12 chars, uppercase, digit, non-alphanumeric. Always verify before storing.
- The seeder creates the `SuperAdmin` role BEFORE creating the user, so a partial seeder failure leaves the role but not the user. Idempotency handles this correctly on next run.

## Phase 3 — StrategyHub Domain Model + Role Rename (2026-05-18)

**Branch:** `dev/phase-3-strategyhub-domain` | **PR:** (see decisions inbox)

### Completed

**New Domain Entities (`src/SixToFix.Domain/`)**
- `Enums/Pillar.cs` — Brand=1, Customer=2, Offering=3, Communication=4, Sales=5, Management=6
- `Enums/PlaybookTemplateStatus.cs` — Draft=0, Published=1, Archived=2
- `Constants/Roles.cs` — SuperAdmin, TenantAdmin, Client constants replacing magic strings
- `Entities/PillarContent.cs` — per-tenant per-pillar content with `BodyJson` (JSONB) holding strategy/execution/templates/examples/metrics
- `Entities/UserPillarProgress.cs` — per-user per-pillar progress (0–100%), LastActivityAt
- `Entities/PlaybookTemplate.cs` — tenant-scoped template catalogue; nullable Pillar for cross-pillar templates

**EF Core Configuration (`src/SixToFix.Infrastructure/Data/EntityConfigurations/`)**
- `PillarContentConfiguration` — `BodyJson` → `HasColumnType("jsonb")`, unique index `(tenant_id, pillar)`
- `UserPillarProgressConfiguration` — unique index `(user_id, pillar)`, standard index `(tenant_id, pillar)`
- `PlaybookTemplateConfiguration` — standard index `(tenant_id, status)`, nullable Pillar stored as int?
- All three entities added to `SixToFixDbContext` with tenant-scoped global query filters

**Migration: `20260519042934_AddStrategyHubDomain`**
- Three new tables: `pillar_contents`, `user_pillar_progresses`, `playbook_templates`
- ADDITIVE ONLY — zero DROP TABLE / DROP COLUMN on legacy audit tables
- Data migration in `Up()`: inserts `Client` identity role; grants Client to all existing Reviewer/Viewer users (old roles stay — Phase 6 cleanup)

**Role Rename**
- `Roles.cs` constants class; Program.cs gets a new `Client` authorization policy
- Legacy `Reviewer`/`Viewer` policies updated to also accept `Client` during transition window
- `AdminBootstrapHostedService` refactored: now seeds SuperAdmin + TenantAdmin + Client roles; pillar content placeholder seeder runs once per tenant
- 2 authorization policies in `Program.cs` updated; 1 Roles.cs constant class introduced

**Service Stubs (Phase 4 hooks)**
- `IPillarContentService` — GetAsync / UpsertAsync / ListForTenantAsync
- `IProgressService` — GetForUserAsync / UpdateAsync
- `IPlaybookTemplateService` — ListAsync / CreateAsync / UpdateAsync
- Stubs throw `NotImplementedException("Implemented in Phase 4")`, registered Scoped in DI

### Schema Decisions

- **JSONB for BodyJson:** Pillar content shape evolves independently of schema. JSONB allows admin editing of nested arrays (strategy blocks, execution items, examples) without new columns/migrations. Phase 5 admin editor writes structured JSON directly.
- **Pillar stored as int:** EF Core enum-as-int is efficient and unambiguous. Display names live in the UI layer.
- **Unique (TenantId, Pillar) on PillarContent:** exactly one content row per pillar per tenant. Upsert pattern (Phase 4) checks existence before insert.
- **Unique (UserId, Pillar) on UserPillarProgress:** one progress row per user per pillar; TenantId index for dashboard aggregations.
- **PlaybookTemplate.Pillar nullable:** null = cross-pillar (e.g., general onboarding kit). Non-null = pillar-specific template.

### Role Rename Approach
- Legacy Reviewer/Viewer role rows stay in `asp_net_roles` until Phase 6.
- Migration SQL promotes existing Reviewer/Viewer users to Client role (additive — no membership rows deleted).
- Program.cs legacy policies kept as aliases so old `.razor` page attributes (Trinity's domain) continue to work during transition.
## Followup — 2026-05-19: Domain Coverage Fix (PR #43)

**Why coverage dipped:** PR #43 added 4 new StrategyHub domain types (`Pillar` enum, `PlaybookTemplateStatus` enum, `PillarContent`, `UserPillarProgress`, `PlaybookTemplate`) — 5 files, ~60 new lines — with no corresponding tests. The `User` entity and `Roles` constants from Phase 1 were also uncovered. This dropped `SixToFix.Domain` line coverage from ~80%+ to 74.20%, failing the coverage gate.

**Tests added (37 new tests in `tests/SixToFix.Domain.Tests/StrategyHubTests.cs`):**
- `Pillar` enum: all 6 values map to expected int values (Brand=1…Management=6); 6-value count check
- `PlaybookTemplateStatus` enum: all 3 values map to expected ints; 3-value count check
- `PillarContent`: default values, property assignment round-trip, JSON body round-trip via `System.Text.Json`, all pillar values assignable
- `UserPillarProgress`: default values, property assignment round-trip, boundary percent values (0/50/100), all pillar values assignable
- `PlaybookTemplate`: default values, property assignment round-trip, null-pillar (spans-all) case, all 3 status transitions, format variant strings
- `User`: default values, property assignment round-trip
- `Roles`: all 3 constants verified

**Result:** Domain line coverage lifted from 74.20% → ~99.58%. Total test count: 159 (all passing). CI green on PR #43.



## Phase 4 Services — 2026-05-19

**Branch:** `dev/phase-4-services`

### Summary
Implemented all three StrategyHub services + interfaces + DI registration + 50 integration tests.

### Services Implemented

**PillarContentService (IPillarContentService)**
- GetForTenantAsync: single pillar lookup, null if not seeded
- GetAllForTenantAsync: lazy placeholder seeding; always returns exactly 6 rows
- UpsertAsync: insert-or-update; stamps UpdatedAt + UpdatedByUserId
- Placeholder body: {"placeholder":true}; subtitle describes missing content to admin

**ProgressService (IProgressService)**
- GetForUserAsync / GetForUserPillarAsync: reads existing rows
- SetPercentAsync: Math.Clamp(percent, 0, 100); insert if missing via ITenantContext; updates LastActivityAt
- GetAverageForUserAsync: integer average over 6 pillars; missing = 0
- Injects ITenantContext (consistent with ClientService pattern)

**PlaybookTemplateService (IPlaybookTemplateService)**
- GetPublishedAsync(tenantId, pillar?): pillar filter includes null-pillar cross-cutting rows
- GetByIdAsync: null if not found
- CreateAsync: always forces Status = Draft
- UpdateAsync: mutates mutable fields; Status unchanged
- PublishAsync / ArchiveAsync: throw InvalidOperationException if not found

### Tenant Isolation
Dual-layer: global EF query filter + explicit WHERE TenantId == tenantId in every query.

### DI Registration
Three AddScoped calls added to BusinessServiceExtensions.cs.

### Tests (50 integration tests)
- PillarContentServiceTests.cs: 13 tests
- ProgressServiceTests.cs: 17 tests
- PlaybookTemplateServiceTests.cs: 20 tests

All tagged [Trait("Category", "Integration")].

### Build
Build succeeded. 0 Errors. Non-integration tests: 160/160 passed.
