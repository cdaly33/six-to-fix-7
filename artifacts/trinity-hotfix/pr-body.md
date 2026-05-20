## Root Cause

Two distinct CSS bugs causing the visual breakage in production.

### Bug 1 — Hero section invisible (homepage)

`tokens.css` was missing `--hero-radial-overlay`, `--text-5xl`, and `--text-7xl` custom property definitions.

Per CSS spec, when a `var()` references an undefined custom property, the *entire* property declaration becomes **invalid at computed-value time** and falls back to its initial value. The `.hero { background: radial-gradient(var(--hero-radial-overlay), ...) }` declaration collapsed to `transparent`, leaving the white H1 text (`color: var(--text-inverse)`) invisible on the cream body background.

**Confirmed via Playwright:** `hero bg: rgba(0, 0, 0, 0) none` — background was transparent.

### Bug 2 — Sidebar/shell unstyled (dashboard)

`App.razor` did not reference `SixToFix.Web.styles.css` — the Blazor CSS isolation bundle.

In Blazor, component-scoped `.razor.css` files compile into a single `{ProjectName}.styles.css` bundle. Without this `<link>` tag, `StrategyHubShell.razor.css`, `SectionSidebar.razor.css`, and `NavItem.razor.css` were never loaded — the entire navy sidebar, shell layout, and nav item styles were absent.

## Fix

| File | Change |
|------|--------|
| `src/SixToFix.Web/wwwroot/css/tokens.css` | Added `--hero-radial-overlay`, `--text-5xl`, `--text-6xl`, `--text-7xl` |
| `src/SixToFix.Web/App.razor` | Added `<link rel="stylesheet" href="SixToFix.Web.styles.css" />` |

## Before Screenshots (prod — broken)

Screenshots taken via Playwright before this fix:
- `artifacts/trinity-hotfix/before-home.png` — cream background, hero heading invisible
- `artifacts/trinity-hotfix/before-login.png` — login page
- `artifacts/trinity-hotfix/before-dashboard.png` — sidebar collapsed as plain text

## Verification
- Build: clean
- Tests: 77/94 pass; 17 pre-existing failures (FileLoadException in Infrastructure.Tests, integration auth tests) — unrelated to CSS

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
