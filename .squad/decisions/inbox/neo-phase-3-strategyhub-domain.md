# Decision: Neo — Phase 3 StrategyHub Domain

**Date:** 2026-05-18  
**Author:** Neo (Backend Dev)  
**Branch:** `dev/phase-3-strategyhub-domain`  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/43  
**Status:** Complete — build ✅ tests ✅ (128/128)

---

## What Was Built

### New Entities
| Entity | Table | Purpose |
|---|---|---|
| `PillarContent` | `pillar_contents` | Per-tenant per-pillar content (JSONB body) |
| `UserPillarProgress` | `user_pillar_progresses` | Per-user per-pillar progress 0–100% |
| `PlaybookTemplate` | `playbook_templates` | Tenant-scoped template catalogue |

### New Enums
- `Pillar` (Brand=1 … Management=6)
- `PlaybookTemplateStatus` (Draft/Published/Archived)

### New Constants
- `Roles.SuperAdmin`, `Roles.TenantAdmin`, `Roles.Client` — replaces all magic strings

### Migration
- **Name:** `20260519042934_AddStrategyHubDomain`
- **Type:** Additive only — no DROP TABLE, no DROP COLUMN on legacy audit tables
- **Data migration included:** inserts `Client` identity role; grants it to all existing Reviewer/Viewer users

### Role Mapping
| Old role | New role | Action |
|---|---|---|
| `Reviewer` | `Client` | Users promoted to Client in migration; old role row kept |
| `Viewer` | `Client` | Users promoted to Client in migration; old role row kept |

Old `Reviewer` and `Viewer` role rows **stay in the database** until Phase 6 cleanup. Legacy authorization policy aliases in `Program.cs` ensure existing `.razor` page attributes (Trinity's domain) continue to work.

### Service Interfaces (Phase 4 hooks)
- `IPillarContentService` — GetAsync / UpsertAsync / ListForTenantAsync
- `IProgressService` — GetForUserAsync / UpdateAsync
- `IPlaybookTemplateService` — ListAsync / CreateAsync / UpdateAsync

All registered Scoped in DI; stubs throw `NotImplementedException("Implemented in Phase 4")`.

---

## Key Design Decisions

### JSONB for `PillarContent.BodyJson`
Pillar content schema: `{ strategy: [{title, points[]}], execution: [string], templates: [string], examples: [string], metrics: [[label, value]] }`. JSONB chosen over relational columns because:
- The content shape evolves per pillar; relational would require repeated migrations for each admin editor addition.
- Phase 5 admin editor writes structured JSON directly.
- No cross-pillar SQL joins on content body are ever needed.

### Pillar stored as int
EF Core enum-to-int is efficient and migration-stable. Display names and colors live in the UI layer (Trinity's domain).

### Unique constraint `(tenant_id, pillar)` on `pillar_contents`
Exactly one content row per pillar per tenant. The seeder and the Phase 4 upsert service rely on this constraint for idempotency.

### `PlaybookTemplate.Pillar` nullable
`null` = cross-pillar template (e.g., general onboarding kit). Non-null = pillar-specific. Filtering handled by service layer, not DB views.

---

## Phase 4 Hooks

Phase 4 (Trinity + Neo) implements these interfaces against the new tables:
- `IPillarContentService.GetAsync(tenantId, pillar)` → drives pillar page tabs
- `IProgressService.GetForUserAsync(userId)` → drives dashboard progress cards
- `IPlaybookTemplateService.ListAsync(tenantId, filter)` → drives templates library

No DI wiring changes needed — just swap stub implementations for real ones.

---

## Files Changed (summary)

**New files:**
- `src/SixToFix.Domain/Enums/Pillar.cs`
- `src/SixToFix.Domain/Enums/PlaybookTemplateStatus.cs`
- `src/SixToFix.Domain/Constants/Roles.cs`
- `src/SixToFix.Domain/Entities/PillarContent.cs`
- `src/SixToFix.Domain/Entities/UserPillarProgress.cs`
- `src/SixToFix.Domain/Entities/PlaybookTemplate.cs`
- `src/SixToFix.Infrastructure/Data/EntityConfigurations/PillarContentConfiguration.cs`
- `src/SixToFix.Infrastructure/Data/EntityConfigurations/UserPillarProgressConfiguration.cs`
- `src/SixToFix.Infrastructure/Data/EntityConfigurations/PlaybookTemplateConfiguration.cs`
- `src/SixToFix.Infrastructure/Migrations/20260519042934_AddStrategyHubDomain.cs` (+ Designer)
- `src/SixToFix.Application/Services/IPillarContentService.cs`
- `src/SixToFix.Application/Services/IProgressService.cs`
- `src/SixToFix.Application/Services/IPlaybookTemplateService.cs`
- `src/SixToFix.Infrastructure/Services/PillarContentService.cs`
- `src/SixToFix.Infrastructure/Services/ProgressService.cs`
- `src/SixToFix.Infrastructure/Services/PlaybookTemplateService.cs`

**Modified files:**
- `src/SixToFix.Infrastructure/Data/SixToFixDbContext.cs` — +3 DbSets, +3 query filters
- `src/SixToFix.Infrastructure/Extensions/InfrastructureServiceExtensions.cs` — +3 Scoped registrations
- `src/SixToFix.Infrastructure/Bootstrap/AdminBootstrapHostedService.cs` — role seeder + pillar seeder
- `src/SixToFix.Web/Program.cs` — added `Client` policy; legacy policies updated
- `src/SixToFix.Domain/GlobalUsings.cs`, `src/SixToFix.Application/GlobalUsings.cs`, `src/SixToFix.Infrastructure/GlobalUsings.cs` — added Domain.Enums / Domain.Constants
