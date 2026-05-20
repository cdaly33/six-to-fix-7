# Neo — History Archive (Summarized from 28KB+ original)

**Purpose:** Compress Neo's full history into summary form to keep active history.md under 15KB.

**Archived Date:** 2026-05-20T04:30:00Z

## Summary of Learnings

### Phase 0 — Schema Design & Service Architecture (2026-05-10)

**15-Table Schema Decisions:**
- 14 tenant-scoped tables carry `tenant_id NOT NULL FK → tenants.id`
- Exception: `hubspot_sync_events.tenant_id` nullable (events arrive before tenant match); filter: `tenant_id IS NULL OR tenant_id = :tenantId`
- `category_result_versions` APPEND-ONLY; `sf_app` role has UPDATE/DELETE REVOKED at DB layer
- `category_results` holds current view; updated in same transaction as version insert (no triggers)
- `reviewer_actions.action_type`: approve, reject, edit, rerun, escalate
- `calibration_deltas` requires non-empty reason_code + notes
- `documents.size_bytes` CHECK constraint (10MB limit, defense-in-depth)
- `telemetry_events` has UNIQUE index on audit_run_id (one row per run)
- All tenant-scoped tables: `(tenant_id, id)` composite index (tenant_id leading)

**Service Layer (8 services):**
- PolicyEngine: Singleton (stateless)
- Others: Scoped (DB-touching, tenant context per request)
- All external deps via interface (IAIClient, IBlobStorage, ISearchClient, IHubSpotClient)
- All methods accept CancellationToken
- Exception mapping: NotFoundException→404, ConflictException→409, ValidationException→422, AI failures→502/503/504

**Lockout State Machine:**
- Scope: per `(tenant_id, audit_run_id, category_id, reviewer_id)` — per reviewer, not category-global
- 24-hour rolling window from NOW() — no cron, no expiry table
- Isolation: READ COMMITTED + optimistic retry (not SERIALIZABLE)
- CalibrationDelta sequence for Edit: check lockout → validate → transaction → insert CalibrationDelta → update category_results → insert version → insert action → commit
- HTTP 409: RFC 7807 format, no PII

**Immutable Publish:**
- Three version sources: ai (skill completion), council (if adjusted), reviewer (if edit)
- `version_number`: MAX+1 per (audit_run_id, category_id), unique constraint for concurrency
- Published state: service-layer guard (not DB constraint)
- Preconditions: all 6 categories approved, audit_runs status completed

### Phases 1–3 Implementation (2026-05-15 to 2026-05-19)

**Phase 1 — StrategyHub Domain & Role Hierarchy (PR #43)**
- New entities: PillarContent (JSONB), UserPillarProgress (0-100%), PlaybookTemplate
- Enums: Pillar (Brand=1…Management=6), PlaybookTemplateStatus (Draft/Published/Archived)
- Roles: SuperAdmin, TenantAdmin, Client (Client replaces Reviewer/Viewer in migration; old roles kept for backward compat)
- Migration backfills all Reviewer/Viewer users with Client role
- Service interfaces stubbed; implemented in Phase 4
- Schema: Pillar content JSONB to avoid repeated migrations; lazy seeding in GetAllForTenantAsync

**Phase 2 — Service Implementations (PR #46)**
- PillarContentService: GetForTenantAsync, GetAllForTenantAsync (lazy-seeds 6 rows), UpsertAsync (preserves CreatedAt)
- ProgressService: GetForUserAsync, SetPercentAsync (clamped 0-100), GetAverageForUserAsync (sum/6)
- PlaybookTemplateService: GetPublishedAsync (filters by pillar ± cross-pillar), CRUD with status transitions
- All services: two-layer tenant isolation (global filter + explicit predicate)
- Test coverage: PillarContentServiceTests (13), ProgressServiceTests (17), PlaybookTemplateServiceTests (20)

**Phase 3 — Prod Login 500 Fix & TenantAdminPanel MVP (PR #57)**
- **Root cause 3-layer:** (1) `sf_app` role didn't exist on prod (created only on pgBouncer); (2) migrations never ran (database empty); (3) SeedAdmin password lacked digit/non-alphanumeric
- **Fixes:** (1) Manual `CREATE ROLE sf_app` (non-driftable); (2) Startup migration runner in Program.cs using AdminConnection; (3) Migration `GrantAppRolePermissions` codifies GRANT/REVOKE + fail-fast if `sf_app` missing; (4) KV secret updated
- **Standing rules:** Provision `sf_app` before first deploy; validate SeedAdmin password; never use DefaultConnection for DDL; startup migration runner prevents manual intervention
- **TenantService:** ITenantService interface (GetCurrentTenantAsync, UpdateTenantNameAsync, GetTenantUsersAsync)
- **DTOs:** TenantDto (Id, Name, Slug, CreatedAt), TenantUserDto (with role via UserManager)
- **MVP:** name-only editing; user list read-only; LastLogin nullable (deferred)
- **5 unit tests:** all passing

### Architecture Patterns Established

**DI Lifetime Rules:**
- PolicyEngine: Singleton (stateless, pure-functional)
- Domain services (Audit*, Reviewer*, Publisher, etc.): Scoped
- HTTP clients (IHttpClientFactory): Transient + Polly pipelines
- DbContext: Scoped with global tenant query filters
- DbContext pooling: NOT used (incompatible with per-request filters)

**Tenant Context Flow:**
- JWT claim (tenant_id) → ClaimsPrincipal → IHttpContextAccessor → HttpTenantContext → SixToFixDbContext global filter
- Tenant isolation: single authoritative enforcement point (query filters)
- Background workers: must add explicit WHERE conditions (filter null guard)

**Error Handling:**
- NotFoundException, ConflictException, ValidationException service exceptions
- HTTP 409 REVIEWER_REJECTION_LOCKOUT for lockout violations
- RFC 7807 Problem Details format for all error responses

**Testing Patterns:**
- Mandatory cross-tenant isolation tests (tenant A cannot see tenant B's data)
- Testcontainers with real PostgreSQL 16
- Moq for external service mocking
- Skeptical isolation: even with filters, explicit WHERE clauses in tests

## Key Files Delivered
- Domain entities (Pillar, PlaybookTemplate, PillarContent, UserPillarProgress)
- Service interfaces & implementations (3 services × 3 commits)
- Migrations (20260519042934_AddStrategyHubDomain, 20260519033146_GrantAppRolePermissions, etc.)
- Program.cs startup migration runner
- DI registration patterns (BusinessServiceExtensions)
- 35+ unit tests (all passing)

## Prod Fix Timeline
- 2026-05-18 01:00 UTC: Chris reports 500 on login
- 2026-05-18 02:30 UTC: Root cause identified (3 layers)
- 2026-05-18 03:30 UTC: All fixes applied + verified (curl GET /api/auth/login → HTTP 200)
- 2026-05-19: Codified in PR #57 (StartupMigrationRunner + GrantAppRolePermissions migration)

## Standing Rules Established by Neo

1. **Startup migration runner (Program.cs)** prevents manual DB maintenance burden
2. **AdminConnection for all DDL** — DefaultConnection (sf_app) is DML-only
3. **Provision sf_app role before first deploy** — fail-fast guard in migration
4. **SeedAdmin--Password policy validation:** ≥12 chars, uppercase, digit, non-alphanumeric
5. **Tenant context from JWT claim** — never query string
6. **Two-layer tenant isolation:** global filter + explicit predicate

---

**Next steps for Neo:** TenantAdminPanel UI integration (Phase 5), Phase 6 legacy retirement, monitoring of prod stability.
