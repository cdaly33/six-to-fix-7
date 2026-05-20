# Trinity — History Archive (Summarized from 27KB+ original)

**Purpose:** Compress Trinity's full history into summary form to keep active history.md under 15KB.

**Archived Date:** 2026-05-20T04:30:00Z

## Summary of Learnings

### Phase 0 — Visual Design Foundation (2026-05-10)

**CSS Token System (Custom, no Tailwind):**
- Warm palette: `--bg-primary: #F5F0E8` (cream), `--bg-dark: #1A1A2E` (navy), `--accent: #2563EB` (blue)
- Base tokens in `wwwroot/css/tokens.css` → `components.css` → `app.css` → `public.css` cascade
- Card styles: `border-radius: 8px`, `box-shadow: 0 1px 3px rgba(0,0,0,0.08)`, hover lift
- No Tailwind, no Bootstrap, no MudBlazor — fully custom CSS
- 13 screens across 4 roles (SuperAdmin, TenantAdmin, Reviewer, Viewer)

### Phase 1 — Visual Foundation (PR #44, 2026-05-19)

**Shell Architecture:**
- `StrategyHubShell.razor` — flex-based responsive layout (no CSS grid)
- `SectionSidebar.razor` — 260px navy sidebar (nav items, role-based menus)
- `NavItem.razor` — nav with hover/active states
- `Logo.razor` + icon sprites system
- Signed-in vs logged-out layout variants

**CSS Token Gaps Fixed:**
- Added `--hero-radial-overlay: rgba(7, 17, 46, 0.45)` (was missing, broke hero background)
- Added `--text-5xl: 3rem`, `--text-6xl: 3.75rem`, `--text-7xl: 4.5rem` (were missing, broke heading sizes)
- Undefined CSS var makes entire property invalid at computed-value time → fallback to initial (transparent)

**Blazor CSS Isolation:**
- Added `<link href="SixToFix.Web.styles.css" />` to `App.razor` (was missing)
- Bundle includes all component-scoped `.razor.css` stylesheets
- Without link, StrategyHubShell.razor.css, SectionSidebar.razor.css, NavItem.razor.css never loaded

**Prerendering Lesson:**
- Login.razor: `@rendermode InteractiveServer` (prerender: true by default)
- Lifecycle race: prerender → static HTML → user fills form → circuit connects → component reinits with empty model → validation fails
- Fix: `@rendermode @(new InteractiveServerRenderMode(prerender: false))`
- **Standing rule:** Form-heavy pages use `prerender: false` (Login, registration, data-entry); read-only pages safe with prerender true

### Phase 2 — Public Homepage (PR #45, 2026-05-19)

**Public Layout & Home.razor:**
- `PublicLayout.razor` — sticky navy header, auth-aware CTAs, footer (public pages template)
- `Home.razor` — static SSR (no `@rendermode`), 6 pillar cards, better TTFB/SEO, no SignalR overhead
- Public-for-all strategy: authenticated users see Dashboard/My-Playbook links; anonymous see Sign-In/Get-Started
- No redirect gate on homepage (improves UX)

**CSS Animations:**
- `@keyframes fadeSlideUp` — Framer Motion-like behavior (y:16→0, opacity:0→1)
- Wrapped in `prefers-reduced-motion` for a11y
- 300ms animation budget per phase directive

**Auth Contract Update:**
- Old test: `Get_Root_Unauthenticated_BrowserNav_RedirectsToLogin_Not401` (tested wrong behavior)
- New: `GET /` → 200 OK, `GET /dashboard` → [Authorize]
- Phase 4 deferred: pillar pages, post-login redirect TODO, authenticated deep-links

### Phase 3 — Dashboard + Pillar Pages + Templates (PR #47, 2026-05-19)

**Single PillarPage Component:**
- 6 `@page` directives (`/brand`, `/customer`, `/offering`, `/communication`, `/sales`, `/management`)
- Pillar resolved from NavigationManager.Uri segment (avoid 6 near-identical components)
- Ordinal "(PILLAR 2 OF 6)" derived from `(int)Pillar` enum

**Dashboard Rewrite:**
- Personalized playbook overview with progress grid + recent content
- "Getting Started" empty state when 0% progress + 0 clients (3 action cards)
- Pillar cards always render with static metadata; progress loaded if userId available (more resilient)
- `prerender: false` applied (Phase 1 lesson)

**Templates Library:**
- `/templates` page with pillar filter + modal card view
- Read-only in Phase 4 (creation/publishing deferred)

**BodyJson Schema:**
```json
{
  "strategy": [{"title": string, "points": string[]}],
  "execution": [string],
  "templates": [string],
  "examples": [string],
  "metrics": [[string, string]]
}
```
- If `"placeholder": true` detected, render empty state
- Neo's PillarContentService handles placeholder check in GetAllForTenantAsync (lazy-seeding fallback)

**CSS Tokens Only:**
- Zero hardcoded hex colors
- `--pillar-brand`, `--pillar-customer`, etc. (6 pillars)
- `--color-gold-400`, `--color-navy-800` (semantic names)
- Accent-specific rules auto-generated for all 6 pillars

**Service Integration:**
- Cherry-picked Neo's service interfaces from PR #46 (temporary; drop before merge if #46 ships first)
- IPillarContentService, IProgressService, IPlaybookTemplateService injected
- Blazor SSR navigation automatically flows through component lifecycle

**Test Coverage:**
- DashboardPageTests: 5 bUnit tests (rewritten)
- PillarPageTests: 7 bUnit tests (new)
- PillarContentAdminTests: 11 bUnit tests (Phase 5)
- Total Web tests: 27 (was 20)

### Phase 4 — Pillar Content Admin (PR #55, 2026-05-19)

**PillarContentAdmin.razor:**
- Route: `/admin/content` (StrategyHubShell layout)
- Auth: `[Authorize]` + inner `<AuthorizeView>` (SuperAdmin, TenantAdmin)
- Tenant from `tenant_id` claim (never query string — auth boundary enforced)
- `?pillar=` query param for tab pre-selection UX

**Content Editor:**
- 6 pillar tabs (Brand, Customer, Offering, Communication, Sales, Management)
- Plain textarea for Phase 4 (rich editor deferred)
- Live save (no draft workflow)
- BodyJson schema per Phase 3 definition

**Edit Button on PillarPage:**
- Linked to `/admin/content?pillar={enum}` for UX convenience
- Only renders for SuperAdmin/TenantAdmin (authorization check)

### Phase 5 — Default Pillar Content Seeding (PR #58, 2026-05-19)

**Problem:** First-time users saw empty pillar pages (placeholder JSON) and sparse Dashboard with no guidance.

**Solution:**
- `AdminBootstrapHostedService.GetDefaultPillarContent` — switch expression for 6 pillars, each with:
  - Title (e.g., "Brand Strategy")
  - Subtitle (1-sentence value prop)
  - BodyJson with 1 strategy block (3 points), 3 execution steps, empty template/example/metric arrays
- `SeedPillarContentForTenantAsync` calls GetDefaultPillarContent (no more empty body)
- Migration `20260520025400_SeedDefaultPillarContent` backfills existing tenants (targets empty/placeholder rows only, idempotent)

**Dashboard Getting Started UX:**
- Empty state when `averageProgress == 0 && clients.Count == 0`
- 3 numbered action cards: (1) Add Client, (2) Review 6 Pillars, (3) Create Playbook Template
- Normal dashboard when user has progress > 0

**Pillar Content Philosophy:**
- Conservative scaffolding (generic, widely applicable)
- NO invented marketing-specific recommendations
- Actionable format (strategy points + execution steps)
- Defense-in-depth: PillarContentService lazy-seeding self-heals if bootstrap/migration fails

**Test Updates:**
- BuildContext helper for DashboardPageTests needs ClaimTypes.NameIdentifier + tenant_id claims
- Mock IProgressService and IClientService (using Moq)
- Test scenarios: no progress/clients (getting started), has progress (normal), has clients (normal)

## Pillar Content Substance

| Pillar | Strategy Focus | Execution Theme |
|--------|----------------|-----------------|
| **Brand** | Define Your Identity | Audit perception, document guidelines, train team |
| **Customer** | Know Your Audience | Create personas, map journey, measure engagement |
| **Offering** | Structure Your Portfolio | Document services, bundle packages, establish renewal paths |
| **Communication** | Orchestrate Your Message | Map content to journey, establish calendar, track performance |
| **Sales** | Systematize Revenue Generation | Document process, configure CRM, train on qualification |
| **Management** | Drive Accountability | Define roles, set KPIs, schedule reviews |

## Standing Rules Established by Trinity

1. **Prerender: false for form-heavy pages** (Login, registration, data-entry)
2. **Prerender: true safe for read-only display pages** (Marketing pages, dashboards with no input)
3. **CSS custom property undefined → entire property invalid** (always provide all token vars)
4. **Blazor CSS isolation requires explicit `<link>` in App.razor** (never assume automatic injection)
5. **Public homepage for everyone** (no redirect gate; auth-aware CTAs in header)
6. **CSS tokens only (no Tailwind)** in this codebase
7. **Conservative, non-invented copy for default content** (no marketing guesses; provide scaffolding only)

## Key Files Delivered
- 13 .razor component files (Home, PillarPage, Dashboard, PillarContentAdmin, Templates, Layout, Auth variants)
- 4 CSS files (tokens, components, app, public — full cascade)
- 35+ bUnit tests (all passing)
- 2 migrations (pillar-seed-content + defaults)
- AdminBootstrapHostedService extensions

## Outcomes

- **Phase 1 (Visual Foundation):** Shell, layouts, token system, icon system, CSS foundation ✅
- **Phase 2 (Public Homepage):** Marketing page, hero section, 6 pillar cards ✅
- **Phase 3 (Pages + Dashboard):** Dashboard rewrite, 6 pillar pages, templates library ✅
- **Phase 4 (Admin Content Editor):** TenantAdmin pillar content editor ✅
- **Phase 5 (Seed Content):** Default content + Getting Started UX ✅

**98 unit tests passing. 0 errors. Build clean.** ✅

---

**Next steps for Trinity:** Polish deferred features (rich editor, draft/publish), monitor user feedback on default content, iterate based on usage patterns.
