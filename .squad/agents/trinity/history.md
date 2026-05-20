# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT, EF Core, Azure PostgreSQL Flexible Server, Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service, Azure Key Vault, Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — no OIDC server. App issues JWTs with `tenant_id`, `tenant_slug`, `roles` claims.
- **CSS approach:** Custom CSS design tokens only — no Tailwind, no Bootstrap, no MudBlazor. Warm cream/tan palette. Base tokens in `wwwroot/css/`.
- **Key design tokens:** `--bg-primary: #F5F0E8`, `--bg-dark: #1A1A2E`, `--accent: #2563EB`, `--border: #E5E0D5`, `--text-primary: #1A1A2E`. Cards: `border-radius: 8px`, `box-shadow: 0 1px 3px rgba(0,0,0,0.08)`, lift on hover.
- **Screen count:** 13 screens across 4 user roles (SuperAdmin, TenantAdmin, Reviewer, Viewer)
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-20 — Pillar Content Seeding & Dashboard Getting Started UX

**Problem:** First-time users saw completely empty pillar pages and a sparse Dashboard with no guidance, creating a poor onboarding experience. Pillar pages showed placeholder content (`{"placeholder":true}`) and Dashboard didn't help users understand next steps.

**Solution implemented:**
1. **Default pillar content seeding** via `AdminBootstrapHostedService.GetDefaultPillarContent` — each pillar now gets meaningful scaffolding:
   - 1 strategy block with 3 actionable points
   - 3 execution steps
   - Conservative, generic copy (no invented marketing-specific advice per charter rule)
2. **Migration 20260520025400_SeedDefaultPillarContent** — backfills existing tenants with rows matching `body_json = '{}'` OR `'{"placeholder":true}'`
3. **Dashboard "Getting Started" empty state** — shows when `averageProgress == 0 && clients.Count == 0`:
   - 3 numbered cards: (1) Add a Client → /clients, (2) Review the 6 Pillars → /brand, (3) Create Your First Playbook Template → /templates
   - Normal dashboard with progress percentage when user has progress > 0
   - Uses `prerender: false` (learned from login form bug on 2026-05-18)

**Pillar content structure:** BodyJson is JSONB with schema:
```json
{
  "strategy": [{"title": string, "points": string[]}],
  "execution": string[],
  "templates": string[],
  "examples": string[],
  "metrics": [[string, string]]
}
```

**Test learnings:**
- BuildContext helper for DashboardPageTests needs BOTH `ClaimTypes.NameIdentifier` and `tenant_id` claims to properly initialize `_userId` and `_tenantId` in Dashboard.razor
- Must mock IProgressService and IClientService for Dashboard tests (using Moq, not NSubstitute per codebase convention)
- Test scenarios: no progress + no clients (getting started), has progress (normal dashboard), has clients (normal dashboard), progress percentage display

**Files changed:**
- AdminBootstrapHostedService.cs (GetDefaultPillarContent method with switch expression for 6 pillars)
- Dashboard.razor (conditional rendering, service injections, OnInitializedAsync logic)
- app.css (`.getting-started` and `.dashboard-progress` styles using CSS tokens)
- DashboardPageTests.cs (BuildContext with claim mocks, 4 new test methods)
- Migration: 20260520025400_SeedDefaultPillarContent.cs

**PR:** https://github.com/cdaly33/six-to-fix-7/pull/58  
**Tests:** All 35 Web unit tests passing (5 new Dashboard tests added)

**Architectural note:** Lazy seeding pattern provides defense-in-depth — `PillarContentService.GetAllForTenantAsync` already checks for missing pillars and seeds on-demand, so migration + AdminBootstrapHostedService create dual guarantees.

---

### 2026-05-18 — PROD BUG: Login form empty-model validation race (prerender: false fix)

**Bug:** Users visiting `/login` on prod saw "The Email field is required." and "The Password field is required." even after filling in both fields and clicking Submit.

**Root cause — Blazor prerender race condition:**
`@rendermode InteractiveServer` defaults to `prerender: true`. Blazor renders the form as static HTML first, then the SignalR circuit connects and re-initialises the component from scratch (`_model = new LoginModel()` — empty). If the user fills in the form during the prerendered window and clicks Submit before the circuit reconnects, the EditForm validates the circuit's fresh empty model, not the values the user typed into the static DOM. The DOM and the interactive model are out of sync.

**Fix:** `@rendermode @(new InteractiveServerRenderMode(prerender: false))`  
Disabling prerender means the form only appears after the interactive circuit is live. Every keystroke routes directly to the bound `_model` — no stale DOM mismatch possible.

**Rule to remember:** Any Blazor page that:
- Has user input fields bound via `@bind-Value`
- AND uses `@rendermode InteractiveServer`
  ...should use `prerender: false` unless there's an explicit SEO or TTFB reason to prerender. Login pages are the highest-risk case because they're the first thing a user touches on cold load.

**Files changed:** `src/SixToFix.Web/Pages/Login.razor` (1 line)  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/40  
**All 18 Web unit tests passed including LoginPageTests.**

### ⚠️ 2026-05-17 — FOLLOW-UP: Client Bearer Token Wiring (Tank flagged)

Tank's prod 401 fix (PR #28) identified a critical gap: `Login.razor` stores JWT in `localStorage` but no client code wires it to HTTP requests for Blazor SSR navigations. Consequence: After login, subsequent page navigations still send no bearer token, so `[Authorize]` pages receive JwtBearer challenge → 401 → redirect loop potential.

**Scope for Trinity/Morpheus:** 
- Add bearer token attachment to all `HttpClient` requests originating from Blazor (both SSR and API calls).
- Consider if Blazor SSR navigations need automatic token attachment or if a cookie-based auth scheme for browser flows is preferable (JwtBearer reserved for `/api`).
- Decision required: Client-side bearer wiring vs cookie auth redesign.

**Documented in:** decisions.md Phase 2, recommendation #4. Tank session log: 2026-05-17T22:19:46Z.

---

### 2026-05-10 — Phase 0: Design System, Component Architecture, Role Visibility Matrix

**Design System decisions locked:**
- Color palette: warm cream (`#F5F0E8`) primary, deep navy (`#1A1A2E`) nav/dark panels, `#2563EB` accent blue. All values are `:root` tokens — nothing hardcoded in components.
- Typography base: 14px on `<html>`. System font stack (no external font load). `--text-xs` through `--text-4xl` in rem.
- Spacing: 4px base unit. 16 named tokens (`--space-1` through `--space-48`).
- Score badge coloring hard rule: 0–3 → `--color-error`, 4–6 → `--color-warning`, 7–10 → `--color-success`.
- Tier colors: tier_1 = green (`#16A34A`), tier_2 = blue (`#2563EB`), tier_3 = amber (`#D97706`).
- Cards: `--radius-md` (8px), `--shadow-sm` at rest, lift to `--shadow-md` on hover.
- All animations wrapped in `@media (prefers-reduced-motion: no-preference)` for a11y.
- CSS load order: `design-system.css` → `components.css` → `app.css`.

**Component architecture decisions locked:**
- 13 pages in `Pages/`, reusable components in `Shared/`, layout shell in `Layout/`.
- `MainLayout` for all authenticated pages; `AuthLayout` for Login only.
- Child components are purely presentational — state flows down via parameters, events bubble up via `EventCallback`. No shared mutable state bags.
- `CategoryReviewDrawer` is a Shared component (not a page) — used on both `AuditDetailPage` and `ReviewerQueuePage`.
- `DataTable<TItem>` is generic and takes `RenderFragment` for header and row templates.
- `SkillChainProgress` and `SkillProgressItem` receive state via parameters only — no direct SignalR subscription.

**SignalR pattern chosen: Option B — `IAuditHubConnection` Scoped service:**
- One `HubConnection` per Blazor circuit (Scoped DI lifetime), never Singleton.
- Pages inject `IAuditHubConnection`, subscribe to typed event delegates in `OnInitializedAsync`, unsubscribe in `DisposeAsync`.
- Hub callbacks always call `await InvokeAsync(StateHasChanged)` — required to marshal to render thread.
- Pages implement `IAsyncDisposable`. Service itself is disposed by DI container on circuit end.
- On SignalR reconnect: re-call `JoinGroupAsync(auditRunId)` to rejoin group, then re-fetch current state via API snapshot (events are not replayed).
- `.WithAutomaticReconnect()` configured on `HubConnection`.

**Role visibility decisions locked:**
- Four roles: `SuperAdmin`, `TenantAdmin`, `Reviewer`, `Viewer` (exact JWT claim strings — case-sensitive).
- Chris confirmed the canonical JWT role claim strings are `SuperAdmin`, `TenantAdmin`, `Reviewer`, and `Viewer`. `Auditor` is not a valid role name and must not appear in JWT issuance or `<AuthorizeView>` checks.
- All UI role checks use `<AuthorizeView Roles="...">` — no ad-hoc code-behind checks for rendering.
- Exception: page-level redirect logic in `OnInitializedAsync` may use `IsInRole()` for navigation decisions only.
- Denied authenticated users are redirected to the most appropriate permitted screen (not a 403 page) to avoid information leakage.
- SuperAdmin sees cross-tenant data at service layer, not via separate UI branches — component is the same, service filters by role.

### 2026-05-10 — Chris decision sync

- Confirmed canonical JWT role claim strings: `SuperAdmin`, `TenantAdmin`, `Reviewer`, `Viewer`; removed invalid `Auditor` role references from architecture docs.
- Recorded Chris's Azure OpenAI subscription decision: same subscription as App Service, managed identity via `DefaultAzureCredential`, no cross-subscription auth complexity.

### 2026-05-10 — Phase 0 Sealed

**Status:** All Phase 0 questions resolved by Chris. 15 inbox files consolidated into canonical `decisions.md` (21,203 bytes).

**Decisions merged** include:
- HubSpot Private App token (Q1) — oracle
- Azure OpenAI same-subscription (Q2) — this decision
- 8 infrastructure decisions (Q3–Q10) — tank
- JWT role confirmation (Q12) — this decision
- 9 architecture ADRs (Morpheus, Neo) — all locked

**Team updates:** All team history.md files appended with Phase 0 seal notification. Phase 1 gate: CLEAR.

### 2026-05-10 — Phase 3: Blazor Page Wiring

**Pages implemented (wired to real services):**
- `Login.razor` — EditForm + DataAnnotationsValidator, IHttpClientFactory POST to `/api/auth/login`, JWT stored via `localStorage.setItem`. NOTE: endpoint not yet implemented by Neo.
- `Dashboard.razor` (new) — nav hub at `/` and `/dashboard` with role-gated cards for all four roles.
- `AuditList.razor` — `IAuditOrchestrator.GetAuditRunsForClientAsync(Guid)`. Requires `?clientId=` query param; no tenant-wide run list exists on the service.
- `AuditDetail.razor` — `IAuditOrchestrator.GetAuditRunAsync`, `IPublisher.PublishAuditAsync`, SignalR via `HubConnectionBuilder` to `/hubs/audit-run`, `JoinAuditRun`/`LeaveAuditRun` hub methods, `IAsyncDisposable`.
- `CategoryReview.razor` — `IReviewerWorkflow.GetLockoutStatusAsync`, `ApproveAsync`, `RejectAsync`, `RerunAsync`, `EscalateAsync`.
- `ReviewerQueue.razor` — GUID nav helper; full queue listing requires a new `GetPendingCategoriesAsync` service method.
- `PublishedResults.razor` (new) — `IPublisher.GetPublishedAuditAsync(string clientSlug)` and `GetPublishedVersionsAsync`. Route is `/results/{clientSlug}` (string, not Guid).

**8 remaining stubs updated** with detailed Phase 4 TODO comments: CalibrationDashboard, ClientManagement, DocumentManagement, SkillChainRunner, SuperAdminPanel, TelemetryDashboard, TenantAdminPanel, TenantOnboarding.

**_Imports.razor additions:** `System.ComponentModel.DataAnnotations`, `System.Security.Claims`, `Microsoft.AspNetCore.SignalR.Client`, `SixToFix.Application.Services`, `SixToFix.Application.Models`, `SixToFix.Domain.Entities`, `static Microsoft.AspNetCore.Components.Web.RenderMode`.

**Key service constraints discovered:**
- `IAuditOrchestrator` has no `GetAuditRunsForTenantAsync` — only per-client lists.
- `IPublisher.GetPublishedAuditAsync` uses `string clientSlug` not `Guid auditRunId`.
- `ScoreCard` component is a full display card (required `Category` + `Score` params) — not an inline badge.
- `@rendermode InteractiveServer` requires `@using static Microsoft.AspNetCore.Components.Web.RenderMode` in `_Imports.razor`.
- `SixToFix.Domain.Entities` namespace must be explicitly imported in Web project (not transitive via GlobalUsings).

**PR:** https://github.com/cdaly33/six-to-fix-7/pull/12

### 2026-05-10 — Phase 4: UI shell and review patterns

- The authenticated shell now flows through `AppShell`, `Sidebar`, and `TopNav`, with navigation styling centralized in `wwwroot/css` instead of component-scoped stylesheets.
- Route aliases for the Phase 4 screen inventory are safe to add because existing `/audits/*` paths still need to keep working during rollout.
- Review UI should stay thin: comments and buttons call `IReviewerWorkflow` directly; richer category evidence waits for a dedicated query API instead of duplicating domain logic in the page.
- Progress visualization should prefer semantic HTML (`<progress>`) over inline width styling so Razor stays free of inline visual attributes.

### 2026-05-15 — SignalR → PeriodicTimer polling swap in AuditDetail

**What changed:**
- `AuditDetail.razor` — removed `IAuditRunHubClientFactory` injection and all `HubConnection` wiring (`ConnectHubAsync`, `IAuditRunHubClient _hub`, `_hubConnected`, `_events`, `IAsyncDisposable`). Replaced with a `PeriodicTimer` polling loop (`StartPollingAsync`) that calls `IAuditOrchestrator.GetAuditRunAsync` every 3 seconds while the run is `pending` or `running`, stops on `completed`/`awaiting_review`/`failed`. Implements `IDisposable` (not `IAsyncDisposable`) — cancels and disposes timer on component disposal.
- Deleted `SixToFix.Web/Realtime/AuditRunHubClientFactory.cs` and `IAuditRunHubClient.cs` (the Web-project client-side hub wrappers).
- Removed `builder.Services.AddScoped<IAuditRunHubClientFactory, AuditRunHubClientFactory>()` from `Program.cs`.
- Removed `@using Microsoft.AspNetCore.SignalR.Client` from `_Imports.razor` and the NuGet package reference from `.csproj`.
- `SkillChainRunner.razor` — confirmed stub only, no hub wiring, no change needed.
- `SkillProgressBar.razor` — confirmed parameter-driven, unaffected.

**Server-side hub infrastructure kept dormant (per ADR-004 dormancy decision):**
- `AuditRunHub.cs`, `AuditRunHubNotifier.cs`, `IRealtimeNotifier.cs` — all kept.
- Hub mapped in `Program.cs` at `/hubs/audit-run` — kept (no client will connect, harmless).

**Pre-existing bugs also fixed:**
- `SkillRunner.cs` was missing `IRealtimeNotifier _notifier` injection (field and constructor param); added.
- `AuditOrchestrator.cs` constructor had already dropped `IHubContext` but `AuditOrchestratorTests.cs` still passed it as an 8th arg; removed the hub setup from the test and updated the hub-event test to assert DB state instead.

**Test rewrites:**
- `FakeAuditRunHubClient.cs` — deleted (no longer needed).
- `AuditDetailPageTests.cs` — replaced hub-trigger tests with polling-appropriate tests (initial render state, publish button visibility by role).
- `AuditOrchestratorTests.cs` — removed `_hubContext` mock setup, updated one test to verify DB state transition instead of hub event receipt.

### 2026-06-06 — Phase 5: Cookie-based auth wiring (browser-side login flow)
**Core problem solved:** Blazor Server components run server-side over SignalR. A C# HttpClient POST from Login.razor is a server-to-server call — the browser never receives Set-Cookie. Fixed by using JS.InvokeAsync<string>("SixToFix.login", ...) so the browser's etch() receives the cookie directly.

**Key learnings:**
- orceLoad: true is required for post-login/logout navigation. Without it, Blazor does client-side nav and the new auth cookie is never sent on the next request.
- @rendermode InteractiveServer must be explicit on Login.razor and Logout.razor — JS interop in event handlers requires an active SignalR circuit, which SSR-only rendering doesn't establish.
- [SupplyParameterFromQuery] works on routable Blazor 8+ components for capturing ?returnUrl=....
- Server-side /logout was renamed to /account/logout to avoid route conflict with Logout.razor Blazor page at @page "/logout".

**Files changed:**
- src/SixToFix.Web/wwwroot/js/auth.js — created (browser fetch, localStorage helpers)
- src/SixToFix.Web/App.razor — added auth.js script tag
- src/SixToFix.Web/Navigation/ILoginNavigator.cs — added NavigateTo(string?), orceLoad: true
- src/SixToFix.Web/Pages/Login.razor — JS interop approach, @rendermode InteractiveServer, returnUrl
- src/SixToFix.Web/Pages/Logout.razor — new page: clears localStorage, navigates to /account/logout
- src/SixToFix.Web/Components/RedirectToLogin.razor — appends returnUrl to redirect
- src/SixToFix.Api/Endpoints/ApiEndpointExtensions.cs — renamed /logout → /account/logout
- src/SixToFix.Web/Program.cs — updated LogoutPath to /account/logout
- 	ests/SixToFix.Web.Tests/Pages/LoginPageTests.cs — replaced IHttpClientFactory mocks with JSInterop stubs

## Learnings — Phase 1 (2026-05-18)

### What was built
- Design token system rewritten to StrategyHub navy/gold/slate palette (`wwwroot/css/tokens.css`)
- 10 inline SVG icon components in `Components/Icons/` (Lucide-style, `currentColor` stroke)
- `Components/Brand/Logo.razor` — book-open SVG tile + Playfair Display wordmark
- `Components/Nav/NavItem.razor` — active/hover state nav link
- `Components/Nav/SectionSidebar.razor` — full nav with auth state (display name, role, initials)
- `Layout/StrategyHubShell.razor` — 260px navy sidebar + 56px topbar + content area
- `Layout/MainLayout.razor` — authenticates then routes to StrategyHubShell or auth-shell wrapper

### Where the token system lives
`src/SixToFix.Web/wwwroot/css/tokens.css` — loaded first, before components.css and app.css.
Semantic tokens (`--text-primary`, `--accent`, `--bg-page`, etc.) map to primitive tokens.
Components should only ever reference semantic tokens; primitives are for the token file only.

### Shell composition
```
MainLayout (LayoutComponentBase)
  └─ AuthorizeView
       ├─ Authorized → StrategyHubShell (ChildContent wrapper)
       │    ├─ SectionSidebar (nav + auth state)
       │    │    ├─ Logo (book-open + wordmark)
       │    │    └─ NavItem (×8 pillar links)
       │    └─ main.sh-shell__content (@ChildContent = @Body)
       └─ NotAuthorized → div.auth-shell (login page)
```

### Google Fonts + CSP gotcha
Google Fonts CDN requires the browser to reach `fonts.googleapis.com` and `fonts.gstatic.com`.
If the app has a Content-Security-Policy header, `font-src` and `style-src` must include those origins.
Current CSP is managed by Neo/Tank — they need to add:
- `style-src: https://fonts.googleapis.com`
- `font-src: https://fonts.gstatic.com`
This is deferred to Phase 2 / Neo Phase 3.

## Learnings — Phase 4 (2026-05-20)

### What was built
- `Dashboard.razor` — completely rewritten for StrategyHub. Serif H1 welcome, gold Resume CTA → /brand, overall progress card, 6-pillar card grid (3-col XL / 2-col MD / 1-col SM), recently-updated section.
- `PillarPage.razor` — new single component with 6 `@page` directives (/brand … /management). 5 tabs: Strategy, Execution Blueprint, Templates, Examples, Metrics. Mark Progress buttons (25/50/75/100%). `EmptyContentMessage` when DB content is absent.
- `Templates.razor` — new `/templates` page. Pillar filter dropdown, card grid, modal for template detail. Calls `IPlaybookTemplateService.GetPublishedAsync`.
- `Components/Shared/EmptyContentMessage.razor` — shared empty-state component; shows role-gated message (TenantAdmin/SuperAdmin see setup guidance, others see "coming soon").
- CSS — ~600 lines appended to `components.css`: gold button, progress bars (thin/filled/pillar-colored), pillar icon tiles, pillar tags, dashboard/pillar/templates/empty-state layout.

### Routing pattern
Single `PillarPage.razor` handles all 6 pillars. Pillar resolved from last URL segment via `NavigationManager.Uri`. Ordinal ("PILLAR N OF 6") from `(int)Pillar`. This avoids 6 near-identical files.

### BodyJson schema
```json
{ "strategy": [{"title": "…", "points": ["…"]}],
  "execution": ["…"], "templates": ["…"],
  "examples": ["…"], "metrics": [["label", "value"]] }
```
`"placeholder": true` → treat as empty.

### Pillar cards always-render pattern
`_pillarCards` is built unconditionally from static `PillarMeta` with 0% defaults. Progress percents are patched in only when userId resolves. Previously an early return on missing `NameIdentifier` left the grid empty. Fixed to be resilient in tests and any auth edge-case.

### Cherry-pick dependency
`IPillarContentService`, `IProgressService`, `IPlaybookTemplateService` live in PR #46 (Neo). Cherry-picked commit `449ffd1` to unblock build. **PR dependency**: this PR must be merged after PR #46, or the cherry-picked commits need to be dropped post-merge.

### Test counts
Web.Tests grew from 20 → 27: +7 `PillarPageTests`, +5 rewritten `DashboardPageTests` (old 3 tests replaced).

### PR
https://github.com/cdaly33/six-to-fix-7/pull/47

## Hotfix — 2026-05-19: CSS Rendering Broken on Prod (PR #50)

### Root cause 1 — Hero section invisible on homepage

`tokens.css` was missing `--hero-radial-overlay`, `--text-5xl`, `--text-6xl`, and `--text-7xl`.

Per CSS spec, when a `var()` references an undefined custom property the **entire property
declaration becomes invalid at computed-value time** and falls back to its initial value.
The `.hero { background: radial-gradient(var(--hero-radial-overlay), ...) }` declaration in
`public.css` collapsed to `transparent`. The hero heading had `color: var(--text-inverse)` = white,
which was invisible on the light cream body background (`--color-slate-50`).

**Playwright confirmation:** `hero bg: rgba(0, 0, 0, 0) none` (before fix).

### Root cause 2 — Sidebar/shell completely unstyled on /dashboard

`App.razor` did not include `<link rel="stylesheet" href="SixToFix.Web.styles.css" />`.

In Blazor, component-scoped `.razor.css` files compile into a single isolation bundle named
`{ProjectName}.styles.css`. Without the `<link>` tag the bundle is never sent to the browser.
All of `StrategyHubShell.razor.css`, `SectionSidebar.razor.css`, and `NavItem.razor.css` were
absent — the 260px navy sidebar, the flex shell layout, and nav item styles did not apply.

### Fix applied (2 files, 7 lines)

| File | Change |
|------|--------|
| `tokens.css` | Added `--hero-radial-overlay`, `--text-5xl`, `--text-6xl`, `--text-7xl` |
| `App.razor` | Added `<link rel="stylesheet" href="SixToFix.Web.styles.css" />` |

### Lessons

1. **Always link `{ProjectName}.styles.css` in App.razor.** Blazor CSS isolation is opt-in;
   the bundle exists at build time but won't load unless explicitly referenced. Add it when the
   project is scaffolded, not when isolation CSS first appears.
2. **Validate all `var()` tokens before shipping.** An undefined `var()` inside any CSS shorthand
   (background, border, animation, transition) silently nukes the whole declaration. Run a token
   coverage check: `grep -r 'var(--' wwwroot/css/ | grep -v ':root' | sort -u` and cross-reference
   against all definitions in tokens.css.
3. **History.md ≠ deployed code.** Phase 2 history recorded `--text-5xl` / `--hero-radial-overlay`
   as added, but the PR (#45) either didn't include the tokens.css diff or it was lost in a rebase.
   Treat history notes as aspirational until CI confirms the file diff.

### Post-deploy smoke test note

The smoke test expected `GET /` → 302 (redirect to /login), but the homepage is now a public 200
page. That mismatch is pre-existing (introduced with the public homepage in Phase 2); it is NOT
caused by this hotfix. It should be fixed separately by updating the smoke test expectation.

### PR
https://github.com/cdaly33/six-to-fix-7/pull/50

---

## Learnings — Phase 2 (2026-05-19)

### What was built
- `Layout/PublicLayout.razor` — public page shell: sticky navy header (auth-aware CTAs via AuthorizeView),
  main slot, simple navy footer
- `Pages/Home.razor` — public homepage at `/` (no [Authorize], static SSR, @layout PublicLayout)
  - Hero: radial-gradient navy bg, gold pill badge, serif H1 with gold accent, sub copy, gold CTA → /login
  - Six Pillars grid: 3-col xl / 2-col md / 1-col sm; pillar-accent icon tiles; serif card names
- `wwwroot/css/public.css` — all hero/pillar/layout styles; zero hardcoded hex; all via tokens
- `Components/Icons/ArrowRightIcon.razor` — Lucide-style SVG for hero CTA

### Homepage design choices

**Routing:** `/` is public for everyone. Authenticated users see "Dashboard" + "My Playbook" in the
header; anonymous users see "Sign In" + "Get Started". `Dashboard.razor` no longer holds `@page "/"`.

**Animation approach:** No JS library. CSS `@keyframes fadeSlideUp` (opacity 0→1, translateY 16px→0,
300ms ease-out) with 50/100/150ms staggered delays on badge → H1 → sub → CTA. Entire block wrapped
in `@media (prefers-reduced-motion: no-preference)` for accessibility.

**Card hover lift:** `transform: translateY(-4px)` on `.pillar-card:hover`, `var(--transition-fast)`
(150ms ease). Same as the mockup's `whileHover={{ y: -4 }}` Framer Motion prop.

**Gold button variant:** `.public-btn-gold` + `.public-btn-gold--lg`. Uses `--color-gold-400` bg,
`--color-slate-900` text, `--radius-md` (16px ≈ rounded-2xl), hover to `--color-gold-300`.

### Where pillar card descriptions live
Verbatim from `docs/visual-examples/strategyhub_other_screens_mockup.jsx` lines 560–565 (the
`sectionContent` inline conditionals in the `Homepage` function).

### Auth contract test update
`AuthContractTests.Get_Root_Unauthenticated_BrowserNav_RedirectsToLogin_Not401` was renamed to
`Get_Root_Unauthenticated_BrowserNav_ReturnsOk` — `/` is now a public page that must return 200.
A new `Get_Dashboard_Unauthenticated_BrowserNav_RedirectsToLogin_Not401` test covers the original
contract for the protected `/dashboard` route.

### Token additions
`tokens.css` gained `--text-5xl` (3rem), `--text-6xl` (3.75rem), `--text-7xl` (4.5rem) for hero
headings, and `--hero-radial-overlay: rgba(31, 58, 147, 0.35)` for the gradient depth layer. The
rgba value encodes the hex #1f3a93 (navy-600 that sits between navy-700 and navy-800) — not an
existing primitive, defined as a semantic public-page token.

### PR
https://github.com/cdaly33/six-to-fix-7/pull/45
