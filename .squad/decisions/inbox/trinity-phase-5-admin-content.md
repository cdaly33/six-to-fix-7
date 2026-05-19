# Decision: Phase 5 - TenantAdmin Pillar Content Editor

**Date:** 2026-05-19
**Agent:** Trinity (Blazor Dev)
**Branch:** dev/phase-5-admin-content
**Status:** Implemented

## Route & Role Table

| Route | Layout | Auth |
|---|---|---|
| `/admin/content` | `StrategyHubShell` | `SuperAdmin`, `TenantAdmin` |

## Files Shipped

| File | Type |
|---|---|
| `src/SixToFix.Web/Pages/Admin/PillarContentAdmin.razor` | New page |
| `src/SixToFix.Web/Components/Nav/SectionSidebar.razor` | Updated: Admin nav link |
| `src/SixToFix.Web/Pages/PillarPage.razor` | Updated: Edit content button |
| `src/SixToFix.Web/wwwroot/css/components.css` | Updated: ~260 lines appended |
| `tests/SixToFix.Web.Tests/Pages/PillarContentAdminTests.cs` | New: 11 bUnit tests |
| `tests/SixToFix.Web.Tests/GlobalUsings.cs` | Updated: added Pages.Admin using |

## JSON Schema (BodyJson)

```json
{
  "strategy": "string",
  "execution": ["string"],
  "templates": ["string"],
  "examples": ["string"],
  "metrics": [["label", "value"]]
}
```

## Key Decisions

1. `@attribute [Authorize]` + inner `<AuthorizeView>`: Route-level auth by `[Authorize(Roles = "...")]`. Inner `<AuthorizeView>` required for bUnit role enforcement.

2. `tenant_id` claim for tenant resolution: Never from query-string. Multi-tenant boundary enforced at auth layer.

3. `?pillar=` query string for tab pre-selection only: UX convenience link from PillarPage.

4. Legacy JSON bodies handled gracefully: placeholder and complex array formats fall back to empty fields.

## Build & Test Results

- **Build**: 0 errors, 0 warnings (SixToFix.Web.Tests)
- **Tests**: 27 passed, 0 failed (16 pre-existing + 11 new Phase 5 tests)

## Deferred Items

- Rich-text editor (plain textarea sufficient for Phase 5)
- Audit trail UI (UpdatedByUserId stored, shown as truncated GUID)
- Draft/publish workflow (saves are live immediately)
