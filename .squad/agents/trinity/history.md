# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT, EF Core, Azure PostgreSQL Flexible Server, Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service, Azure Key Vault, Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — no OIDC server. App issues JWTs with `tenant_id`, `tenant_slug`, `roles` claims.
- **CSS approach:** Custom CSS design tokens only — no Tailwind, no Bootstrap, no MudBlazor. Warm cream/tan palette. Base tokens in `wwwroot/css/`.
- **Key design tokens:** `--bg-primary: #F5F0E8`, `--bg-dark: #1A1A2E`, `--accent: #2563EB`, `--border: #E5E0D5`, `--text-primary: #1A1A2E`. Cards: `border-radius: 8px`, `box-shadow: 0 1px 3px rgba(0,0,0,0.08)`, lift on hover.
- **Screen count:** 13 screens across 4 user roles (Super Admin, Tenant Admin, Auditor, Reviewer)
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
- Note: PRD uses "Auditor" in some places; this project uses `TenantAdmin` for all audit-running capabilities. `Viewer` replaces a pure read-only role absent from PRD. This discrepancy must be reconciled with Neo (auth layer) before JWT issuance is implemented.
- All UI role checks use `<AuthorizeView Roles="...">` — no ad-hoc code-behind checks for rendering.
- Exception: page-level redirect logic in `OnInitializedAsync` may use `IsInRole()` for navigation decisions only.
- Denied authenticated users are redirected to the most appropriate permitted screen (not a 403 page) to avoid information leakage.
- SuperAdmin sees cross-tenant data at service layer, not via separate UI branches — component is the same, service filters by role.
