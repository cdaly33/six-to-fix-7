# Morpheus Phase 4+5 PR Review — Decision Inbox

**Date:** 2026-05-11  
**Reviewer:** Morpheus (Lead Architect)  
**PRs:** #15 (Tank — dev/phase-5-infra-qa), #16 (Trinity — dev/phase-4-ui)

---

## Issues Found and Fixed

### PR #15 — Phase 5 Infra + QA (Tank)

| # | Issue | Fix | Commit |
|---|-------|-----|--------|
| 1 | `@secure()` param `postgresAdminPassword` had hardcoded default `'ChangeMe!RotateImmediately123'` — Bicep linter `no-hardcoded-secure-default` warning | Removed default; parameter is now required (callers supply via `.bicepparam` or pipeline secrets) | `478a22a` |

**Pre-existing warning allowed:** NU1904 `System.Drawing.Common 5.0.0` — deferred upstream.

**Architecture verified:**
- `clientAffinityEnabled: true` in `infra/modules/appservice.bicep` ✅ (SignalR sticky sessions)
- pgBouncer port 6432 = runtime, 5432 = migrations ✅
- JWT roles: SuperAdmin, TenantAdmin, Reviewer, Viewer (no "Auditor") ✅

---

### PR #16 — Phase 4 UI (Trinity)

| # | Issue | Fix | Commit |
|---|-------|-----|--------|
| 1 | Missing Tank's fix `60b2e8d`: `SkillYamlValidationTests` used 4 `..` levels instead of 5 to find repo root | Cherry-picked `60b2e8d`; resolved conflicts manually | `06661cf` |
| 2 | `Login.razor` cherry-pick left stale `LoginResponse` record | Removed during conflict resolution | `06661cf` |
| 3 | `Program.cs` cherry-pick introduced `using SixToFix.Web.Realtime` which doesn't exist in phase-4-ui | Removed spurious using | `640cefe` |
| 4 | `LoginPageTests.cs` uses Moq but `SixToFix.Web.Tests.csproj` only had NSubstitute | Added `Moq 4.*` to test csproj | `640cefe` |
| 5 | `GlobalUsings.cs` referenced `SixToFix.Web.Realtime` and `SixToFix.Web.Tests.Fakes` (non-existent in phase-4-ui) | Removed those global usings for branch build; restored from main during merge | `640cefe` |

**CSS law verified:**
- Zero hardcoded hex (`#RRGGBB`) in `.razor` files ✅
- Zero inline `style=` with color/spacing values in `.razor` files ✅

**Docker/Testcontainers tests:** All such tests carry `[Trait("Category", "Integration")]` — correctly excluded by filter `Category!=Integration&Category!=E2E`. No action required.

---

## Issues Deferred

| # | Issue | Rationale |
|---|-------|-----------|
| 1 | `NU1904` System.Drawing.Common vulnerability | Pre-existing; upstream package dependency, no fix available without upgrading Testcontainers. Deferred to Tank/Neo dependency hygiene pass. |
| 2 | Phase-4-ui branch had no unit tests for new Blazor components (AlertBanner, ConfirmModal, ScoreBar, etc.) | Trinity's phase 4 scope was UI only; component tests are in Tank's bUnit suite. Acceptable for now. |

---

## Final Build/Test Status on Main

| Metric | Result |
|--------|--------|
| `dotnet build SixToFix.slnx -v q` | ✅ 0 errors, 2 warnings (NU1904 only) |
| `dotnet test --filter "Category!=Integration&Category!=E2E"` | ✅ **79 passed, 0 failed, 0 skipped** |
| Infrastructure.Tests | 49 passed |
| Web.Tests | 18 passed |
| Api.Tests | 12 passed |
| Merge commits | PR #15: `c5e3401`, PR #16: `5bc21b9` |
| Fix commits | `478a22a`, `06661cf`, `640cefe` |
