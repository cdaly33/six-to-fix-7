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
