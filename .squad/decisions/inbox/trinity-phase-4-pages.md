# Trinity Phase 4 Decision — Dashboard + Pillar Pages + Templates Library

**Date:** 2026-05-20  
**Agent:** Trinity (Blazor Dev)  
**Branch:** `dev/phase-4-pages`  
**Status:** Ready for review

---

## What shipped

| File | Role |
|------|------|
| `src/SixToFix.Web/Pages/Dashboard.razor` | Rewritten — personalized playbook overview with progress, pillar grid, recent content |
| `src/SixToFix.Web/Pages/PillarPage.razor` | New — 6 pillar routes (/brand … /management), 5-tab layout, mark-progress buttons |
| `src/SixToFix.Web/Pages/Templates.razor` | New — template library at /templates with pillar filter + modal |
| `src/SixToFix.Web/Components/Shared/EmptyContentMessage.razor` | New — role-gated empty state for pillar tabs |
| `src/SixToFix.Web/wwwroot/css/components.css` | +~600 lines StrategyHub page styles (all CSS tokens, no hardcoded hex) |
| `src/SixToFix.Web/_Imports.razor` | Added `@using SixToFix.Domain.Enums` |
| `tests/SixToFix.Web.Tests/Pages/DashboardPageTests.cs` | Rewritten — 5 bUnit tests for new dashboard |
| `tests/SixToFix.Web.Tests/Pages/PillarPageTests.cs` | New — 7 bUnit tests for PillarPage |
| `tests/SixToFix.Web.Tests/GlobalUsings.cs` | Added `global using SixToFix.Domain.Enums` |
| `src/SixToFix.Application/Services/IPillarContentService.cs` | Cherry-picked from dev/phase-4-services (PR #46) |
| `src/SixToFix.Application/Services/IProgressService.cs` | Cherry-picked from dev/phase-4-services (PR #46) |
| `src/SixToFix.Application/Services/IPlaybookTemplateService.cs` | Cherry-picked from dev/phase-4-services (PR #46) |
| `src/SixToFix.Infrastructure/Services/PillarContentService.cs` | Cherry-picked from dev/phase-4-services (PR #46) |
| `src/SixToFix.Infrastructure/Services/ProgressService.cs` | Cherry-picked from dev/phase-4-services (PR #46) |
| `src/SixToFix.Infrastructure/Services/PlaybookTemplateService.cs` | Cherry-picked from dev/phase-4-services (PR #46) |

---

## Key decisions

1. **Single `PillarPage.razor` for all 6 pillar routes.** One component with 6 `@page` directives (`/brand`, `/customer`, `/offering`, `/communication`, `/sales`, `/management`). Pillar resolved from `NavigationManager.Uri` segment, avoiding 6 near-identical components. Ordinal (e.g., "PILLAR 2 OF 6") derived from `(int)Pillar`.

2. **BodyJson schema.** Pillar content is stored as JSON in `PillarContent.BodyJson` with schema:
   ```json
   { "strategy": [{"title": "…", "points": ["…"]}],
     "execution": ["…"],
     "templates": ["…"],
     "examples": ["…"],
     "metrics": [["label", "value"]] }
   ```
   If `"placeholder": true` exists in the JSON the content is treated as empty and `EmptyContentMessage` is shown.

3. **Dashboard pillar cards always render.** Static `PillarMeta` array is always built into `_pillarCards` with 0% progress defaults. Progress percents are loaded from `IProgressService` only when a valid userId is available from claims. This prevents an early-return bug when the test environment has no `NameIdentifier` claim, and is more resilient in production edge cases.

4. **Cherry-pick from dev/phase-4-services.** `IPillarContentService`, `IProgressService`, and `IPlaybookTemplateService` exist only in PR #46 (Neo's branch). Commit `449ffd1` was cherry-picked to unblock the build. **This PR depends on PR #46.** If #46 merges first the cherry-picked commits will need to be dropped before this branch is merged.

5. **CSS tokens only, zero hardcoded hex.** All pillar accent colors use `--pillar-brand`, `--pillar-customer`, etc. Gold uses `--color-gold-400`, navy uses `--color-navy-800`. Pillar icon tiles and tags generate accent-specific rules for all 6 pillars.

6. **`prerender: false` on all three new/rewritten pages.** Following the Phase 3 lesson (prerender race on Login), `@rendermode @(new InteractiveServerRenderMode(prerender: false))` applied to Dashboard, PillarPage, and Templates.

---

## What is deferred

- **BodyJson editing UI (TenantAdmin/SuperAdmin)** — content is display-only in this phase.
- **Template creation / publishing flow** — read-only card+modal in this phase.
- **Deep-link from Homepage pillar cards** → already route-ready (Phase 2 deferred these links).
- **CSP `font-src` for Google Fonts** → Tank/Phase 2 deferred item, unchanged.
- **Login → `/dashboard` post-auth redirect** — LoginNavigator already wires this; no change needed here.

---

## Build & test results

```
dotnet build SixToFix.slnx -v q
  → Build succeeded. 0 Error(s)

dotnet test SixToFix.slnx --filter "Category!=Integration&Category!=E2E" -v minimal
  → All passed
     65  SixToFix.Domain.Tests
     62  SixToFix.Infrastructure.Tests
     27  SixToFix.Web.Tests  (was 20; +7 new PillarPage tests, +5 rewritten Dashboard tests)
     15  SixToFix.Api.Tests
```
