# Morpheus Phase 6 Review — PR #17 (ClientService) + PR #18 (YAML Loading)

**Date:** 2026-05-10  
**Reviewer:** Morpheus (Lead Architect)  
**PRs Reviewed:** #17 `dev/phase-6-client-service` (Neo), #18 `dev/phase-6-yaml-loading` (Oracle)

---

## PR #17 — IClientService / ClientService

### Issues Found and Fixed

#### ISSUE-1: Interface and DTOs in wrong layer (Architecture violation) — FIXED
**Severity:** Blocking  
**Neo placed:**
- `IClientService` in `src/SixToFix.Infrastructure/Interfaces/IClientService.cs`
- `ClientDto`, `CreateClientRequest`, `UpdateClientRequest` in `src/SixToFix.Infrastructure/Models/`

**Correct locations:**
- `IClientService` → `src/SixToFix.Application/Services/IClientService.cs` (namespace: `SixToFix.Application.Services`)
- `ClientDto`, `CreateClientRequest`, `UpdateClientRequest` → `src/SixToFix.Application/Models/` (namespace: `SixToFix.Application.Models`)

**Rationale:** Every other service interface in this codebase lives in `SixToFix.Application/Services/` (`IPublisher`, `ICalibrationTracker`, `ITelemetryCollector`, `IReviewerWorkflow`, `IAuditOrchestrator`, etc.). Application models live in `SixToFix.Application/Models/`. The dependency arrow flows **Infrastructure → Application**, not the other way. Placing an interface in Infrastructure would invert the dependency direction.

**Fix commit:** `92f4e00` — files renamed/relocated; `BusinessServiceExtensions.cs` updated to remove `using SixToFix.Infrastructure.Interfaces;`; `ClientService.cs` usings updated to reference Application namespaces.

#### ISSUE-2: Tenant ID assigned from parameter instead of authoritative ITenantContext — FIXED
**Severity:** Security  
**Location:** `ClientService.CreateClientAsync` — `TenantId = tenantId` (parameter)

**Problem:** If a buggy or malicious caller passes a `tenantId` parameter that differs from the authenticated tenant's context, the created client would be assigned to a different tenant. Reads would be correctly filtered by the global query filter (so the wrong-tenant client would not be visible), but the DB row would contain an incorrect `tenant_id` value creating a data integrity / audit trail problem.

**Fix:** Changed to `TenantId = _tenant.TenantId` — uses the authoritative `ITenantContext` injected into the service, matching the same source used by the EF Core global query filter. The `tenantId` parameter is retained on the interface for call-site clarity per Neo's design comment, but the implementation ignores it for the new entity assignment.

**Fix commit:** `92f4e00`

### Verified (No Action Needed)

- **Tenant isolation:** EF Core global query filter on `SixToFixDbContext` scopes all `Clients` queries to the current tenant. `ClientService` does NOT add redundant `.Where(c => c.TenantId == ...)` clauses — correct per ADR-002. The global query filter is the single enforcement point.
- **Service lifetime:** `IClientService → ClientService` registered as Scoped. Correct — `ClientService` injects `SixToFixDbContext` (Scoped) and `ITenantContext` (Scoped). Singleton would cause captive dependency error.
- **DTOs:** Interface boundary exposes only `ClientDto`, `CreateClientRequest`, `UpdateClientRequest` records. No EF entity types leak across the boundary.
- **Structured logging:** All `LogDebug`/`LogInformation`/`LogWarning` calls use message templates with structured parameters (`{ClientId}`, `{TenantId}`, `{Count}`, `{Slug}`). No string interpolation. No PII (no names, emails, company names in log messages).
- **Soft-delete:** `DeleteClientAsync` sets `client.IsActive = false` and saves. `sf_app` role has no `DELETE` privilege per ADR-007. Correct.

---

## Interface Location Decision — IClientService

**Decision: Application/Services/ (enforced)**

All service interfaces in this project reside in `SixToFix.Application`. This is the established convention from Phase 1 onward and is non-negotiable:
- The Application layer defines contracts (interfaces + models)
- The Infrastructure layer provides implementations
- Dependency direction: Infrastructure → Application (never the reverse)

Neo's deviation (`Infrastructure/Interfaces/`) was noted in the PR but is incorrect. Morpheus has relocated both the interface and the DTOs to Application as part of this review.

---

## PR #18 — ISkillLoader / SkillLoader / YAML Loading

### Issues Found and Fixed

**None.** Oracle's implementation was architecturally correct on all checklist items.

### Verified (No Action Needed)

- **Singleton lifetime:** `ISkillLoader → SkillLoader` registered as Singleton. Correct — `SkillLoader` is stateless (constructs `_skillsRoot` and `_deserializer` once in constructor, then reads files on demand). No per-request state. Scoped `SkillRunner` → Singleton `ISkillLoader` follows the safe one-way dependency direction per ADR-001.
- **Path-walking logic:** `ResolveSkillsRoot` walks up from `IHostEnvironment.ContentRootPath` first, then from `AppContext.BaseDirectory` as fallback. Finds `docs/skills/` by walking the directory tree rather than using a hardcoded relative path. Robust across dev, test, and published deployments. Tests pass (5 `SkillLoader_LoadAsync_ReturnsValidSkillDefinition` tests exercise this via `RepositoryRoot` content root mock).
- **Inline fallback intact:** `SkillRunner.GetSkillDefinitionAsync` wraps `_skillLoader.LoadAsync` in `try/catch`. On any exception, falls back to static `SkillDefinitions` dictionary. YAML loading is never a hard failure in production. Only if the inline fallback also lacks the skill is `SkillNotFoundException` thrown. Fallback comment updated to be accurate.
- **output_schema JSON conversion:** `ConvertToJson` calls `NormalizeYamlValue` which recursively converts `Dictionary<object, object>` → `Dictionary<string, object?>` and `List<object>` → `List<object?>`, then `JsonSerializer.Serialize`. Tests verify the round-trip produces valid JSON (`JsonDocument.Parse` does not throw).
- **YamlDotNet version consistency:** Infrastructure.csproj: `17.1.0`; Infrastructure.Tests.csproj: `17.*`. Both resolved to 17.x. No NU1605 downgrade warning.
- **ISkillLoader location:** Oracle correctly placed it in `SixToFix.Application/Services/`. Namespace: `SixToFix.Application.Services`. No relocation needed.

### SkillLoader Singleton Lifetime Rationale

`SkillLoader` qualifies as Singleton because:
1. **No instance-variable mutation after construction** — `_skillsRoot` and `_deserializer` are set once in the constructor and never mutated.
2. **File I/O is stateless** — each `LoadAsync` call opens, reads, closes a file independently. Multiple concurrent reads of different skill files are safe.
3. **No per-tenant or per-request context** — unlike `ClientService`, `SkillLoader` has no need for `ITenantContext` or `DbContext`. Skill YAML files are tenant-neutral (platform-level config).
4. **Constructor resolution of `docs/skills/` path** — path is resolved once at startup, not per request. This is intentional for fail-fast behavior if the deployment is missing skill files.
5. **Consuming service is Scoped** — `SkillRunner` (Scoped) injecting a Singleton is the safe one-way direction per ADR-001.

---

## Final Build and Test Status on main

After merging both PRs (merge commits `8b58a1b` and `73bf103`):

```
dotnet build SixToFix.slnx -v q
→ Build succeeded. 2 Warning(s), 0 Error(s)
   (Both warnings are pre-existing NU1904 for System.Drawing.Common 5.0.0 — allowed)

dotnet test SixToFix.slnx --filter "Category!=Integration&Category!=E2E" -v minimal
→ 84 passed, 0 failed
   - SixToFix.Infrastructure.Tests: 54 passed (includes 5 new SkillLoader + existing 49)
   - SixToFix.Web.Tests:            18 passed
   - SixToFix.Api.Tests:            12 passed
```

---

## Commit SHAs

| SHA | Description |
|-----|-------------|
| `92f4e00` | fix(arch): PR #17 — relocate IClientService and DTOs to Application layer; fix tenant assignment |
| `8b58a1b` | merge: phase 6 client service (PR #17) |
| `73bf103` | merge: phase 6 YAML skill loading (PR #18) |

---

## Phase 6 Seal

Both PRs reviewed, fixed, and merged. Main is green at **84 tests, 0 errors**.
