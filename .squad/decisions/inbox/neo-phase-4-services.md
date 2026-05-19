# Decision: Neo — Phase 4 Services

**Date:** 2026-05-19  
**Author:** Neo (Backend Dev)  
**Branch:** `dev/phase-4-services`  
**Status:** Complete — build ✅ tests ✅

---

## What Was Built

### Three New Services

| Service | Interface | Assembly |
|---|---|---|
| `PillarContentService` | `IPillarContentService` | Infrastructure |
| `ProgressService` | `IProgressService` | Infrastructure |
| `PlaybookTemplateService` | `IPlaybookTemplateService` | Infrastructure |

Interfaces defined in `SixToFix.Application/Services/`; implementations in `SixToFix.Infrastructure/Services/`.

---

## Service Decisions

### PillarContentService

- `GetForTenantAsync(tenantId, pillar)` — returns single row or null
- `GetAllForTenantAsync(tenantId)` — lazy-seeds placeholder rows; always returns exactly 6 ordered by Pillar enum value
- `UpsertAsync(tenantId, pillar, bodyJson, updatedByUserId)` — insert or update; preserves original CreatedAt on update

**Placeholder seeding:** Implemented as lazy seeding inside `GetAllForTenantAsync`. When fewer than 6 rows exist, missing pillars are seeded with `{"placeholder":true}` body and the subtitle "No content yet — your administrator can add strategy details here." before returning. No separate seeder call or page hook required.

### ProgressService

- `GetForUserAsync(userId)` — all 0–6 progress rows for a user
- `GetForUserPillarAsync(userId, pillar)` — single row or null
- `SetPercentAsync(userId, pillar, percent)` — `Math.Clamp(percent, 0, 100)` applied; inserts if missing
- `GetAverageForUserAsync(userId)` — `sum(all rows) / 6`, missing pillars = 0; integer division

**Tenant injection:** `ITenantContext` is injected so `SetPercentAsync` can stamp `TenantId` correctly on new rows. Consistent with `ClientService` pattern.

### PlaybookTemplateService

- `GetPublishedAsync(tenantId, pillar?)` — if pillar is not null, returns matching pillar rows PLUS null-pillar (cross-cutting) rows; orders by Popularity DESC, Name ASC
- `GetByIdAsync(tenantId, id)` — returns null if not found (no throw)
- `CreateAsync` — always forces `Status = Draft` regardless of caller input
- `UpdateAsync` — updates mutable fields (Name, Format, Pillar, Popularity, Notes, LastUpdatedAt); does not change Status
- `PublishAsync` / `ArchiveAsync` — transitions; throw `InvalidOperationException` if template not found

---

## Tenant Isolation Strategy

Two layers of defence:

1. **Global query filter** on `SixToFixDbContext`: `!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId` — when the tenant context is resolved (all HTTP requests), the EF model automatically appends the WHERE clause at translation time.

2. **Explicit predicate** in every service query: `e.TenantId == tenantId`. Even if the global filter is bypassed (background jobs, seeder, migration context), the service-layer predicate ensures correct scoping. This matches the existing pattern in `ClientService`.

---

## Service Registration

Three `AddScoped<I…, …>()` registrations added to `BusinessServiceExtensions.cs` in a clearly labelled StrategyHub block.

---

## Tests

All three services covered in `tests/SixToFix.Infrastructure.Tests/Services/`:

| File | Tests |
|---|---|
| `PillarContentServiceTests.cs` | 13 tests |
| `ProgressServiceTests.cs` | 17 tests |
| `PlaybookTemplateServiceTests.cs` | 20 tests |

Coverage areas per service:
- Multi-tenant isolation (tenant A cannot see tenant B's data)
- Basic CRUD / read operations
- Lazy placeholder seeding (null → 6 rows; partial → fills gaps; idempotent)
- Percent clamping (above 100 → 100; below 0 → 0; edge cases 0, 100)
- Status transitions (Draft→Published, Draft→Archived, Published→Archived)
- `GetPublished` with and without pillar filter
- `CreateAsync` always forces Draft
- Not-found paths throw `InvalidOperationException`

All integration tests marked `[Trait("Category", "Integration")]`; excluded from the fast non-integration test run.

---

## Build Status

```
dotnet build SixToFix.slnx -v q → Build succeeded. 0 Error(s)
dotnet test --filter "Category!=Integration&Category!=E2E" → 160/160 passed
```
