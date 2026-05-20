# Neo — Backend/API/Domain Decisions

## Dispatch #9: Backend Gap Analysis (Advisory)

**Date:** 2026-05-19  
**Status:** Findings delivered (advisory, no PR)

### Gap Analysis Scope
Analyzed remaining backend implementation gaps for StrategyHub domain, API endpoints, and database schema.

---

## Dispatch #10: Fix-Now Sprint — Clients CRUD (PR #52) ✅

**Date:** 2026-05-19  
**Branch:** `fix/fix-now-clients-crud`  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/52  
**Deploy:** 26134652068  
**Status:** Shipped ✅

### Objective
Implement foundational Clients CRUD to unblock admin page and dashboards.

### Changes Made
| Component | Files |
|-----------|-------|
| Entity | `Client.cs` — multi-tenant entity with soft-delete support |
| Migration | `20260519XXX_AddClientEntity.cs` — adds `clients` table with indexes |
| Service Interface | `IClientService.cs` — CRUD + query methods |
| Service Implementation | `ClientService.cs` — tests included |
| API Endpoints | `POST /api/clients`, `GET /api/clients/{id}`, `GET /api/clients`, `PUT /api/clients/{id}`, `DELETE /api/clients/{id}` |
| Blazor Page | `/clients` page — lists, creates, updates, deletes clients |
| Navigation | `SectionSidebar.razor` nav link for `/clients` |

### Verification
- All CRUD operations tested ✅
- Multi-tenant isolation enforced ✅
- API endpoints pass integration tests ✅
- Blazor page renders and interacts correctly ✅

---

## Dispatch #11: Fix-Now Sprint — Templates Create (PR #53) ✅

**Date:** 2026-05-19  
**Branch:** `fix/fix-now-templates`  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/53  
**Deploy:** 26134797173  
**Status:** Shipped ✅

### Objective
Enable Playbook Template creation/publication to support Phase 4 content workflow.

### Changes Made
| Component | Details |
|-----------|---------|
| Admin Page | `/admin/templates` page — create, edit, publish, archive templates |
| Public Page | `/templates` renders published templates by pillar filter |
| Service Enhancement | `PlaybookTemplateService.CreateAsync` — forces Draft status, supports CRUD operations |
| Tests | `PlaybookTemplateServiceUnitTests.cs` — 54 new tests covering all operations |
| Migration | `20260520010000_AddPlaybookTemplateContent.cs` — adds Content column for template body JSON |

### Key Decisions
- **Draft-by-default:** `CreateAsync` always forces `Status = Draft` regardless of caller input (prevents accidental publication)
- **Lazy placeholder seeding:** Missing pillars auto-seed with placeholder content on first read
- **Tenant isolation:** Global query filter + explicit service predicates ensure no cross-tenant leakage

### Verification
- Admin can create, edit, publish, archive templates ✅
- Public page displays published templates filtered by pillar ✅
- All 54 tests passing ✅
- Build succeeded ✅

---

## Background — Production Login 500 Fix (Non-PR Operational Fix)

**Date:** 2026-05-18  
**Author:** Neo  
**Status:** Resolved

### Root Cause — Three-Layer Cascade

#### Layer 1: Missing PostgreSQL Role
PostgreSQL role `sf_app` (runtime connection identity on pgBouncer) did not exist. pgBouncer returned `FATAL: no such user` → unhandled `NpgsqlException` → HTTP 500 on all DB-hitting endpoints.

**Fix:** `CREATE ROLE sf_app WITH LOGIN PASSWORD '<KV DefaultConnection password>'` via psql as sfadmin.

#### Layer 2: EF Core Migrations Never Run
Database was empty — no `AspNetUsers`, `AspNetRoles`, or application tables. Migration `20260516042353_InitialCreate` had never been applied to Azure Flexible Server.

**Fix:** 
1. `dotnet ef database update` with admin connection string — applied InitialCreate
2. `GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sf_app;` + `REVOKE UPDATE, DELETE ON TABLE category_result_versions FROM sf_app;` (append-only enforcement)

#### Layer 3: Bootstrap User Password Policy Violation
Original `SeedAdmin--Password = GYyE3jnmvGJuMyjtNQAk` lacked digit and non-alphanumeric character. `UserManager.CreateAsync` rejected with `PasswordRequiresNonAlphanumeric` + `PasswordRequiresDigit`. Bootstrap user never created.

**Fix:** Updated KV secret to `GYyE3jnmvGJuMyjtNQAk1!` (meets policy). Restarted app; seeder ran and created `chris@christopherdaly.com` with `SuperAdmin` role.

### Code Fixes (PR #41)

#### Startup Migration Runner
Added auto-running migration executor in `Program.cs` using `ConnectionStrings__AdminConnection` (DDL-capable, sfadmin role). Runs before middleware pipeline. Ensures every app deploy applies pending migrations without manual intervention. Non-fatal failures logged (app continues).

**Why AdminConnection:** `sf_app` has DML-only permissions. DDL (CREATE TABLE, ALTER TABLE) requires sfadmin role on port 5432 (not pgBouncer port 6432).

#### Migration: `20260519033146_GrantAppRolePermissions`
Codifies GRANT/REVOKE SQL in source control so:
- New environment deployments auto-grant correct permissions
- `category_result_versions` append-only enforced at DB layer for `sf_app`
- Includes fail-fast block if `sf_app` role missing

### Standing Rules (New)

1. **When provisioning new PostgreSQL Flexible Server:** Create `sf_app` and `sf_admin` login roles before first app deploy. Startup migration runner fails-fast if role missing.

2. **`SeedAdmin--Password` KV secret must meet Identity policy:** ≥12 chars, ≥1 uppercase, ≥1 digit, ≥1 non-alphanumeric. Validate before storing.

3. **Never use `DefaultConnection` for schema changes.** DefaultConnection = `sf_app` = DML-only on pgBouncer port 6432. Migrations always use AdminConnection = sfadmin on port 5432.

4. **Drift prevention:** KV secret updates (runtime secrets) do not require Bicep PRs. But any manual Azure resource change must be codified in Bicep to prevent silent overwrites on next deploy.

---

## Background — Phase 3 StrategyHub Domain (Completed)

**Date:** 2026-05-18  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/43  
**Status:** Build ✅ Tests ✅ (128/128)

### Entities Added
- `PillarContent` (per-tenant per-pillar JSONB body)
- `UserPillarProgress` (per-user per-pillar 0–100% progress)
- `PlaybookTemplate` (tenant-scoped template catalogue)

### Enums + Constants
- `Pillar` enum (Brand=1 … Management=6)
- `PlaybookTemplateStatus` (Draft/Published/Archived)
- `Roles` constants (SuperAdmin, TenantAdmin, Client)

### Migration
- `20260519042934_AddStrategyHubDomain` — additive only (no drops)
- Data migration includes `Client` identity role grant to all existing Reviewer/Viewer users
- Unique constraint `(tenant_id, pillar)` on `pillar_contents` for idempotency

### Role Mapping
| Old | New | Action |
|-----|-----|--------|
| Reviewer | Client | Promoted in migration; old role row kept until Phase 6 |
| Viewer | Client | Promoted in migration; old role row kept until Phase 6 |

Legacy authorization policy aliases in `Program.cs` ensure existing `.razor` attributes continue to work.

### JSONB Design
`PillarContent.BodyJson` stored as JSONB (not relational columns) because:
- Content shape evolves per pillar; relational would require repeated migrations
- Phase 5 admin editor writes structured JSON directly
- No cross-pillar SQL joins on content body are needed

### Service Interfaces (Phase 4 Stubs)
- `IPillarContentService` — GetAsync / UpsertAsync / ListForTenantAsync
- `IProgressService` — GetForUserAsync / UpdateAsync
- `IPlaybookTemplateService` — ListAsync / CreateAsync / UpdateAsync

All registered Scoped in DI; stubs throw `NotImplementedException("Implemented in Phase 4")`.

---

## Background — Phase 4 Services (Completed)

**Date:** 2026-05-19  
**Status:** Build ✅ Tests ✅

### Three Services Implemented

| Service | Interface | Impl |
|---------|-----------|------|
| PillarContentService | `IPillarContentService` | Infrastructure |
| ProgressService | `IProgressService` | Infrastructure |
| PlaybookTemplateService | `IPlaybookTemplateService` | Infrastructure |

### PillarContentService
- `GetForTenantAsync(tenantId, pillar)` — returns single row or null
- `GetAllForTenantAsync(tenantId)` — lazy-seeds placeholder rows; always returns 6 ordered by Pillar enum
- `UpsertAsync(tenantId, pillar, bodyJson, updatedByUserId)` — preserves CreatedAt on update

**Placeholder seeding:** Inside `GetAllForTenantAsync`, missing pillars seeded with `{"placeholder":true}` body + subtitle "No content yet — administrator can add details here."

### ProgressService
- `GetForUserAsync(userId)` — all 0–6 progress rows for user
- `GetForUserPillarAsync(userId, pillar)` — single row or null
- `SetPercentAsync(userId, pillar, percent)` — `Math.Clamp(percent, 0, 100)`; inserts if missing
- `GetAverageForUserAsync(userId)` — `sum(all) / 6`; missing = 0

**Tenant injection:** `ITenantContext` injected so `SetPercentAsync` stamps `TenantId` correctly on new rows.

### PlaybookTemplateService
- `GetPublishedAsync(tenantId, pillar?)` — returns matching pillar rows PLUS null-pillar (cross-cutting) rows; orders by Popularity DESC, Name ASC
- `GetByIdAsync(tenantId, id)` — returns null if not found (no throw)
- `CreateAsync` — forces `Status = Draft`
- `UpdateAsync` — updates mutable fields (Name, Format, Pillar, Popularity, Notes, LastUpdatedAt); Status immutable
- `PublishAsync` / `ArchiveAsync` — transitions; throw `InvalidOperationException` if not found

### Tenant Isolation (Two Layers)
1. **Global query filter** on `SixToFixDbContext`: appends WHERE clause at EF translation time
2. **Explicit predicate** in every service query: `e.TenantId == tenantId` (defense-in-depth)

### Tests
- `PillarContentServiceTests.cs` — 13 tests
- `ProgressServiceTests.cs` — 17 tests
- `PlaybookTemplateServiceTests.cs` — 20 tests

Coverage: multi-tenant isolation, CRUD, lazy seeding, clamping, transitions, not-found paths, Draft-by-default.

---

## Background — Admin Bootstrap Seeder (Operational)

**Date:** 2026-05-18  
**Status:** Implemented

### Context
Production needed a safe bootstrap path to create first SuperAdmin without direct DB writes.

### Decision
Added environment-gated startup hosted service using `UserManager<ApplicationUser>` and `RoleManager<IdentityRole<Guid>>` to create bootstrap SuperAdmin when none exists.

### Safety Properties
- Idempotent: any existing SuperAdmin makes startup a no-op
- Non-fatal: missing config or Identity failures logged, do not crash host
- No raw Identity table writes: all user/role changes via ASP.NET Core Identity managers
- Prod wiring: KV flat secrets `SeedAdmin--Email` + `SeedAdmin--Password`, plus App Service env var `SeedAdmin__Enabled`

---

## Background — Phase 6 Retire Legacy (Decision Logged)

**Date:** 2026-05-19  
**Status:** Implemented

### Context
Product pivoted from Six-to-Fix audit/calibration system to StrategyHub. Phase 6 removes legacy code.

### Decision
Remove all domain entities, services, pages, and tests tied to legacy audit/calibration/skill-chain/telemetry. Add forward-only EF migration (`DropLegacyAuditTables`) to drop 16 legacy tables.

### Consequences
- Clean domain model focused on StrategyHub
- Reduced code surface: ~120 files removed
- Forward-only migration; rollback requires manual DDL

---
