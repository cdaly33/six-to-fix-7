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
