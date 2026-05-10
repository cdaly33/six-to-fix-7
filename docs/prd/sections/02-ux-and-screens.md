# StrategicGlue Six-to-Fix — UX & Screens (PRD Section 2)

> Version 1.0 | Platform: Blazor Server (.NET 10)
> This document specifies every screen in the Six-to-Fix application. A Blazor developer should be able to build the full UI from this specification alone. For visual design tokens (colors, typography, spacing, component specs), see `../design-system.md`.

---

## Navigation Structure

### Top Navigation Bar

Fixed at the top of every authenticated screen. Height: 52px. Background: `var(--bg-dark)`.

**Left:** Logo + "Six-to-Fix" wordmark  
**Center links (role-gated):**

| Link | Visible to |
|---|---|
| Audits | All authenticated roles |
| Clients | Auditor, Tenant Admin, Super Admin |
| Review Queue | Reviewer, Tenant Admin, Super Admin |
| Calibration | Tenant Admin, Super Admin |
| Telemetry | Tenant Admin, Super Admin |
| Admin | Tenant Admin, Super Admin |

**Right:** Tenant switcher (Super Admin only) + User avatar dropdown (Profile, Sign Out)

### Routing Hierarchy

```
/                          → redirect to /audits
/login                     → Login screen
/login/forgot-password     → Forgot Password
/login/setup               → First-Time Setup
/onboarding                → Tenant Onboarding (new tenant)
/audits                    → Audit List Dashboard
/audits/{slug}             → Audit Detail Dashboard
/audits/{slug}/skills      → Skill Chain Runner
/review/{runId}            → Reviewer Queue
/review/{runId}/{catId}    → Category Review (opens drawer)
/calibration               → Calibration Dashboard
/telemetry                 → Telemetry / Ops Dashboard
/clients                   → Client Management List
/clients/new               → New Client Form
/clients/{id}              → Client Detail + Document Management
/admin                     → Tenant Admin Panel
/super-admin               → Super Admin Panel (Super Admin only)
```

### Breadcrumbs

Appear on detail-level screens below the page header. Format: `Audits › Rolling Rock Stone › Skill Chain`. Each segment is a link. The final segment is not linked (current page).

### Global UI Patterns

**Toast System:** A toast container lives at the bottom-right of the viewport. Toasts auto-dismiss after 4 seconds. Multiple toasts stack with 8px vertical gap. Triggered by any async action result (save, approve, run, error).

**Modal Overlay:** Used for confirmation dialogs (delete, escalate confirmation). Full-screen semi-transparent backdrop. Focus trapped inside. `Escape` closes. `role="dialog"`, `aria-modal="true"`.

**Drawer Behavior:** Review Drawer slides in from the right (200ms ease-out). Clicking the backdrop closes it. `Escape` closes. Focus moves to drawer header on open; returns to trigger on close.

**Sample/Demo Banner:** When running against sample data, a sticky amber banner sits below the nav bar: `⚠️ Demo mode — showing sample data`. Hidden in production with real data.

---

## Responsive Behavior

| Viewport | Behavior |
|---|---|
| Mobile (< 640px) | Single-column layouts. Nav collapses to hamburger menu. Card grids become 1-column. Drawers go full-width. Score area cards stack in 1 column. |
| Tablet (640–1023px) | Two-column card grids. Drawers overlay at 360px. Score area cards 2×3 may reflow to 2×2 + 2. |
| Desktop (≥ 1024px) | Full multi-column layouts as specified per screen. |

---

## Screen 1: Login

**Route:** `/login`  
**Access:** Unauthenticated  
**Purpose:** Authenticate an existing user and enter the application.

### Layout

```
┌──────────────────────────────────────────────────────────┐
│               [Six-to-Fix logo / wordmark]               │
│                                                          │
│          ┌────────────────────────────────┐              │
│          │         Sign in                │              │
│          │                                │              │
│          │  Email ________________________│              │
│          │  Password ____________________│              │
│          │                                │              │
│          │  [Forgot password?]            │              │
│          │                                │              │
│          │  [     Sign In     ] (primary) │              │
│          └────────────────────────────────┘              │
└──────────────────────────────────────────────────────────┘
```

**Background:** `var(--bg-primary)` full page. Card centered (max-width 400px), `var(--bg-elevated)`, `var(--shadow-md)`, `var(--radius-lg)`, padding 32px.

### Key UI Components

- Email input (type=email, autocomplete=email)
- Password input (type=password, show/hide toggle icon)
- "Forgot password?" link → `/login/forgot-password`
- Sign In primary button (full-width)

### User Interactions

- Submit form → POST credentials → on success: redirect to `/audits`
- Submit with invalid credentials → inline error below password field: "Invalid email or password"
- "Forgot password?" → navigate to Forgot Password screen

### Data Displayed

No user data on initial load. Post-error: validation message.

### Error & Empty States

- Auth failure: error message below password, `--text-danger`, `role="alert"`
- Network error: toast "Unable to reach server — try again"

### Loading Behavior

Sign In button shows spinner + "Signing in…" text while request is in flight. Button disabled during loading.

---

## Screen 1b: Forgot Password

**Route:** `/login/forgot-password`  
**Access:** Unauthenticated  
**Purpose:** Request a password reset email.

### Layout

```
┌──────────────────────────────────────────────────────────┐
│                  Reset your password                     │
│                                                          │
│   Enter your email and we'll send reset instructions.    │
│                                                          │
│   Email ___________________________________________      │
│                                                          │
│   [   Send Reset Link   ]   ← Back to sign in            │
└──────────────────────────────────────────────────────────┘
```

### User Interactions

- Submit → success state: "Check your email for a reset link." No redirect.
- "Back to sign in" → `/login`

---

## Screen 1c: First-Time Setup

**Route:** `/login/setup`  
**Access:** Invited user with a valid setup token  
**Purpose:** Let a newly invited user set their password and confirm their profile.

### Layout

```
┌──────────────────────────────────────────────────────────┐
│                   Welcome to Six-to-Fix                  │
│                                                          │
│   Name     [____________________]                        │
│   Password [____________________]                        │
│   Confirm  [____________________]                        │
│                                                          │
│   [   Create Account   ]                                 │
└──────────────────────────────────────────────────────────┘
```

### User Interactions

- Submit → account activated → redirect to `/audits`
- Invalid/expired token → error page with "Request a new invitation" link

---

## Screen 2: Tenant Onboarding

**Route:** `/onboarding`  
**Access:** Super Admin (creating new tenant) or invited Tenant Admin completing setup  
**Purpose:** Register a new tenant, configure basic settings, and create the first user.

### Layout

Multi-step wizard. Step indicator at top (3 steps: Organization → Admin User → Subscription).

```
┌──────────────────────────────────────────────────────────┐
│  [Step 1: Organization] ── Step 2: Admin ── Step 3: Plan │
│                                                          │
│  Organization Name  [_______________________________]    │
│  Subdomain / Slug   [_______________________________]    │
│  Industry           [Dropdown ▾                    ]    │
│  Billing Email      [_______________________________]    │
│                                                          │
│                                   [Cancel]  [Next →]    │
└──────────────────────────────────────────────────────────┘
```

### Key UI Components

- Step indicator (3 steps, active step highlighted with `--accent`)
- Text inputs, dropdowns per step
- Back / Next / Finish navigation at bottom

### User Interactions

- "Next" validates current step before proceeding
- "Back" returns to previous step without data loss
- "Finish" on final step → creates tenant → redirects Super Admin to `/super-admin` or new Tenant Admin to `/audits`

### Error & Empty States

- Slug already taken: inline error "This subdomain is not available"
- Required field missing: per-field validation messages

---

## Screen 3: Audit List Dashboard

**Route:** `/audits`  
**Access:** All authenticated roles  
**Purpose:** Browse all client audits belonging to the tenant. Jump into a specific audit.

### Layout

```
┌─────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]   │
├─────────────────────────────────────────────────────────────────┤
│  Audits                                    [+ New Client]        │
│  ──────────────────────────────────────────────────────────     │
│  Search [________________]  Filter: [All Tiers ▾] [Status ▾]   │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Rolling Rock │  │ Apex Brand   │  │ Summit & Co  │          │
│  │ Stone        │  │              │  │              │          │
│  │ ──────────   │  │ ──────────   │  │ ──────────   │          │
│  │ B2B · Mid ·  │  │ B2C · Ent ·  │  │ B2B · SMB ·  │          │
│  │ Apr 2026     │  │ Mar 2026     │  │ Feb 2026     │          │
│  │              │  │              │  │              │          │
│  │ [Tier 1] ●   │  │ [Tier 2] ●   │  │ [Tier 3] ●   │          │
│  │ Mat: 24/60   │  │ Mat: 38/60   │  │ Mat: 16/60   │          │
│  │ Str: 1/6     │  │ Str: 4/6     │  │ Str: 0/6     │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ...                        │
└─────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Search input: filters cards by client name (client-side, instant)
- Filter dropdowns: Tier, Status (complete/in-progress/not-started)
- Audit card grid: cards 280px wide, flex-wrap, 16px gap
- Each card clickable → `/audits/{slug}`
- "+ New Client" button → `/clients/new`

### Data Displayed (per card)

- Client name, industry, revenue band, last run date
- Tier badge (1/2/3)
- Maturity score (X/60), Strategy score (X/6)

### Error & Empty States

- Empty: "No audits found. Add your first client to get started." + primary CTA
- Filter returns nothing: "No audits match your filters." + clear link
- Load error: centered error box with retry

### Loading Behavior

Skeleton card placeholders (3 cards, same dimensions) while data loads.

---

## Screen 4: Audit Detail Dashboard

**Route:** `/audits/{slug}`  
**Access:** All authenticated roles  
**Purpose:** Full view of one client audit — overall scores, six area scorecards, skill chain summary, deliverable links.

### Layout

```
┌────────────────────────────────────────────────────────────────────┐
│  NAV BAR                                               [User ▾]    │
├────────────────────────────────────────────────────────────────────┤
│  Audits › Rolling Rock Stone                                        │
│  Rolling Rock Stone                         [Run Audit]  [Review]  │
│  B2B Manufacturing · Mid-market · Last run: May 9, 2026             │
│  [Tier 1] Maturity: 24/60   Strategy: 1/6                          │
│  ─────────────────────────────────────────────────────────────     │
│                                                                     │
│  SCORE AREA CARDS (2 × 3 grid)                                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                │
│  │ Brand       │  │ Customer    │  │ Offering    │                │
│  │ Score: 7/10 │  │ Score: 5/10 │  │ Score: 8/10 │                │
│  │ [Current]   │  │ [Partial]   │  │ [None]      │                │
│  │ ▸ Details   │  │ ▸ Details   │  │ ▸ Details   │                │
│  └─────────────┘  └─────────────┘  └─────────────┘                │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                │
│  │ Comms       │  │ Sales       │  │ Management  │                │
│  │ Score: 4/10 │  │ Score: 6/10 │  │ Score: 3/10 │                │
│  │ [None]      │  │ [Current]   │  │ [Partial]   │                │
│  │ ▸ Details   │  │ ▸ Details   │  │ ▸ Details   │                │
│  └─────────────┘  └─────────────┘  └─────────────┘                │
│                                                                     │
│  SKILL CHAIN SUMMARY            DELIVERABLES                       │
│  ┌──────────────────────────┐   ┌────────────────────────────┐    │
│  │ 12 skills · 3 pending    │   │ Executive Summary (PDF)    │    │
│  │ 2 need review            │   │ Strategy Roadmap (PDF)     │    │
│  │ [View Skill Chain →]     │   │ Gap Analysis (PDF)         │    │
│  └──────────────────────────┘   └────────────────────────────┘    │
└────────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Page header with client metadata, overall scores, and tier badge
- Six score area cards (2×3 grid, expandable)
- Skill chain summary panel with "View Skill Chain" link → `/audits/{slug}/skills`
- Deliverable links panel
- "Run Audit" → triggers full skill chain execution (confirmation modal)
- "Review" → `/review/{latestRunId}`

### Score Area Card Expanded State

When a card is clicked, it expands in place to show:
- Top gap item (text, `--text-primary`)
- Evidence list (bulleted, `--text-sm`, `--text-secondary`)
- Warning chips (amber pills)
- Collapse button ("▴ Collapse")

`aria-expanded` toggles on the card element; the expanded content region is toggled with `aria-hidden`.

### Data Displayed

- Client: name, industry, revenue band, last run date, tier, maturity score, strategy score
- Per area card: area name, activity score (0–10), documented strategy badge (None/Partial/Current), gaps, evidence, warnings
- Skill chain: total skills, pending count, needs-review count
- Deliverables: file name, type (PDF/DOCX), download link

### Error & Empty States

- No audit data yet: "No audit data available. Run the audit to generate results."
- Area card with no data: "Not yet scored" in muted text

### Loading Behavior

Page-level loading spinner while audit data fetches. Score area cards show skeleton placeholders individually.

---

## Screen 5: Skill Chain Runner

**Route:** `/audits/{slug}/skills`  
**Access:** Auditor, Tenant Admin, Super Admin  
**Purpose:** View and execute individual skills in the audit skill chain. Monitor progress. Handle stale or blocked states.

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]    │
├──────────────────────────────────────────────────────────────────┤
│  Audits › Rolling Rock Stone › Skill Chain                        │
│  Skill Chain — Rolling Rock Stone          [Run All] [Run Pending]│
│  ────────────────────────────────────────────────────────────    │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ ① Brand Identity Assessment    [approved ✓]      [RUN]    │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ ② Customer Segment Analysis    [needs_review ●]  [RUN]    │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ ③ Offering Gap Mapping     ·Stale  [stale ⚠]    [RERUN]  │  │  (row bg: --bg-warning)
│  ├────────────────────────────────────────────────────────────┤  │
│  │ ④ Comms Effectiveness Score    [running ↻]       [-----]  │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ ⑤ Sales Readiness Check       [blocked ◌]        [BLOCKED]│  │  (btn disabled)
│  │    └─ Depends on: Offering Gap Mapping                     │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ ⑥ Management Maturity Score    [pending —]       [RUN]    │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Ordered skill list, full-width
- Per-row: step number, skill name, status badge, action button
- "Run All" → confirmation modal → starts all pending/stale skills
- "Run Pending" → starts only pending skills
- Blocked skill shows dependency tooltip on hover

### Status Badges

| Status | Badge color | Row background |
|---|---|---|
| pending | gray pill | default |
| running | blue pill + spinner | default |
| approved | green pill | default |
| needs_review | amber pill | default |
| stale | amber pill | `var(--bg-warning)` |
| blocked | gray pill, muted | default |
| failed | red pill | `var(--bg-danger)` |

### User Interactions

- RUN button → starts that skill → status transitions to "running" in real-time (SignalR or polling)
- RERUN on stale → re-executes with latest data
- Click "needs_review" status badge → opens Review Drawer for that skill's output
- "Run All" → confirmation modal before executing
- Blocked button → disabled, hover tooltip shows blocking dependency

### Data Displayed

- Skill name, step number, current status, last run timestamp (tooltip on badge)
- Dependency chain (for blocked rows)

### Error & Empty States

- Run failure: row background turns `--bg-danger`, status badge "failed", error detail expands below row
- Empty skill chain: "No skills configured for this audit type."

### Loading Behavior

Initial list load: skeleton rows (6 rows). Individual skill run: button replaced with spinner immediately.

---

## Screen 6: Reviewer Queue

**Route:** `/review/{runId}`  
**Access:** Reviewer, Tenant Admin, Super Admin  
**Purpose:** See all categories flagged for human review within a specific audit run. Open and act on each.

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]    │
├──────────────────────────────────────────────────────────────────┤
│  Review Queue — Rolling Rock Stone (Run #47)                      │
│  3 items pending · 1 in progress · 5 approved                    │
│  ──────────────────────────────────────────────────────────      │
│                                                                   │
│  Filter: [All ▾]  [Area ▾]  [Confidence ▾]                       │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │ Brand Identity       Score: 6/10  [LOW_CONFIDENCE]  [→]  │    │
│  ├──────────────────────────────────────────────────────────┤    │
│  │ Customer Segment     Score: 4/10  [MISSING_EVIDENCE] [→] │    │
│  ├──────────────────────────────────────────────────────────┤    │
│  │ Sales Readiness      Score: 7/10  [needs_review]    [→]  │    │
│  └──────────────────────────────────────────────────────────┘    │
│                                                                   │
│  APPROVED                                                         │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │ Offering Gap         Score: 8/10  [approved ✓]      [→]  │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Summary stats row (pending / in-progress / approved counts)
- Filter bar: by status, area, confidence flag
- Queue list: rows grouped by status (Pending → In Progress → Approved)
- Each row: category name, current score, warning chip(s), open-drawer arrow/button
- Clicking any row → opens Review Drawer (slides in from right)

### Data Displayed

- Run ID, client name
- Per row: category name, area, current score, status, warning codes

### Error & Empty States

- All approved: "🎉 All items reviewed. Audit is ready for delivery."
- Load error: error box with retry

### Loading Behavior

Skeleton rows (5 rows) while queue loads.

---

## Screen 7: Category Review Drawer / Panel

**Trigger:** Clicking a queue item on Screen 6 (or a needs_review badge on Screen 5)  
**Route overlay:** `/review/{runId}/{catId}` (URL updates for deep-linking)  
**Access:** Reviewer, Tenant Admin, Super Admin  
**Purpose:** Inspect the full AI-generated output for one category; approve, edit, re-run, or escalate.

### Layout

```
                        ┌───────────────────────────────────────┐
                        │  Brand Identity Assessment    [✕]     │
                        │  Current Score: 6/10  [needs_review]  │
                        │  ─────────────────────────────────    │
[Main content dimmed]   │                                       │
                        │  DOCUMENTED STRATEGY                  │
                        │  ● Brand voice: Partially documented  │
                        │  ● Visual identity: Not documented    │
                        │                                       │
                        │  TOP GAP                              │
                        │  No brand voice guide in evidence.    │
                        │                                       │
                        │  EVIDENCE                             │
                        │  [WEAK ●] Brand_Brief.pdf             │
                        │  [MODERATE ●] Website_Audit.docx      │
                        │                                       │
                        │  WARNINGS                             │
                        │  [LOW_CONFIDENCE]  [MISSING_EVIDENCE] │
                        │                                       │
                        │  BENCHMARK CONTEXT                    │
                        │  Industry median: 7.2/10              │
                        │                                       │
                        │  ─────────────────────────────────    │
                        │  [Approve] [Edit Score] [Re-run]      │
                        │  [Escalate to Council]                │
                        └───────────────────────────────────────┘
```

**Edit Score mode** replaces footer with an inline form:

```
                        │  New Score  [____]   (0–10)           │
                        │  Reason     [Dropdown ▾]              │
                        │  Notes *    [                       ] │
                        │             [                       ] │
                        │  [Save Override]  [Cancel]            │
```

**Locked state:** Amber banner below header: "Locked: reviewer edit in progress."

### Key UI Components

- Drawer header: category name, current score, status badge, close (✕)
- Scrollable body: documented strategy list, top gap, evidence list with strength badges, warning chips, benchmark context, AI explanation
- Action footer: Approve (primary), Edit Score (secondary), Re-run (secondary), Escalate (danger)
- Edit mode form: score input (number, 0–10), reason code select, mandatory notes textarea
- Locked state banner

### User Interactions

- **Approve:** POST approval → status → "approved" → toast "Category approved" → drawer closes → queue updates
- **Edit Score:** toggles Edit mode form in footer
  - Save Override: POST override → updated score shown in header → toast "Score updated"
  - Cancel: returns to view mode
- **Re-run:** confirmation modal → triggers skill re-execution → drawer shows running state
- **Escalate:** opens modal: "Escalate to Council — add context note" → submit → status → "escalated" → toast
- **Close (✕) / Escape / backdrop click:** closes drawer, focus returns to triggering queue row
- Score/status updates reflect immediately in the queue list behind the drawer (optimistic or post-confirm)

### Data Displayed

- Category name, area, current score, status
- Documented strategy elements (per element: name, documentation state)
- Top gap description
- Evidence items: document name, strength badge
- Warning codes (as chips)
- Benchmark context: industry median, percentile
- AI explanation text (collapsible, `--text-sm`)

### Error & Empty States

- Save fails: inline error below notes field, toast "Failed to save — try again"
- Locked by another user: locked banner replaces action buttons with read-only view

---

## Screen 8: Calibration Dashboard

**Route:** `/calibration`  
**Access:** Tenant Admin, Super Admin  
**Purpose:** Review override patterns, track calibration deltas, identify systematic AI scoring drift.

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]    │
├──────────────────────────────────────────────────────────────────┤
│  Calibration                         [Export CSV]  [Date Range ▾]│
│  ────────────────────────────────────────────────────────────    │
│                                                                   │
│  SUMMARY STATS                                                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │ 42 Overr.│  │ Avg Δ    │  │ Top Area │  │ Drift    │        │
│  │ (30 days)│  │ -1.4 pts │  │ Brand    │  │ ↑ 0.3   │        │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘        │
│                                                                   │
│  OVERRIDE PATTERNS TABLE                                          │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ Area        │ Avg AI Score │ Avg Override │ Delta │ Count  │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ Brand       │ 7.2          │ 5.8          │ -1.4  │ 12     │  │
│  │ Customer    │ 5.1          │ 6.0          │ +0.9  │ 8      │  │
│  │ Offering    │ 6.5          │ 6.5          │  0.0  │ 3      │  │
│  │ Comms       │ 4.8          │ 5.5          │ +0.7  │ 9      │  │
│  │ Sales       │ 7.0          │ 6.2          │ -0.8  │ 6      │  │
│  │ Management  │ 5.9          │ 5.9          │  0.0  │ 4      │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  OVERRIDE DETAIL LOG                                              │
│  [Filterable table of individual overrides with timestamps]       │
└──────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Summary stat tiles (4 tiles, border, `--bg-secondary`)
- Override patterns table: sortable by column headers, area rows
- Delta column: negative values in `--text-danger`, positive in `--text-success`, zero in `--text-muted`
- Override detail log: paginated table — client, area, category, AI score, override score, reviewer, reason, date
- Export CSV button: downloads current filtered data
- Date range filter

### Data Displayed

- Total overrides in period, average delta, highest-drift area, overall drift trend
- Per area: avg AI score, avg override score, delta, override count
- Per override: all fields above plus reviewer name, reason code, notes excerpt

### Error & Empty States

- No overrides in selected period: "No overrides in this date range."

### Loading Behavior

Table skeleton (6 rows) while data fetches.

---

## Screen 9: Telemetry / Ops Dashboard

**Route:** `/telemetry`  
**Access:** Tenant Admin, Super Admin  
**Purpose:** Monitor daily audit run metrics and quality trends to detect operational issues.

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]    │
├──────────────────────────────────────────────────────────────────┤
│  Telemetry                                         [Date Range ▾]│
│  ────────────────────────────────────────────────────────────    │
│                                                                   │
│  DAILY RUN METRICS (line/bar chart area)                          │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  Runs/day ▬▬▬  Failures/day ▬▬▬  Avg duration (s) ▬▬▬    │  │
│  │  [Chart placeholder — 30-day view]                         │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  QUALITY TREND                                                    │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  Avg score trend  ▬▬▬  Override rate ▬▬▬                  │  │
│  │  [Chart placeholder]                                        │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  RECENT RUNS TABLE                                                │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ Client │ Run ID │ Status │ Duration │ Skills │ Errors │ TS  │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ ...    │ ...    │ ...    │ ...      │ ...    │ ...    │ ... │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Chart areas (use a lightweight chart library or SVG — not specified, defer to dev); data-driven line/bar charts
- Summary metric tiles above charts: total runs, success rate, avg duration
- Recent runs table: sortable, paginated
- Date range picker filter

### Data Displayed

- Daily: runs count, failure count, avg execution duration
- Per-run: client name, run ID, status, duration, skills run, error count, timestamp
- Quality trend: average maturity score per day, override rate per day

### Error & Empty States

- No data: "No telemetry data for this period."
- Chart failure: fallback to numeric table with note "Charts unavailable"

---

## Screen 10: Client Management

**Route:** `/clients` (list), `/clients/new` (create), `/clients/{id}` (detail/edit)  
**Access:** Auditor, Tenant Admin, Super Admin  
**Purpose:** Create, view, and edit client records. Manage client metadata used by the audit system.

### Layout — Client List (`/clients`)

```
┌──────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]    │
├──────────────────────────────────────────────────────────────────┤
│  Clients                                        [+ New Client]   │
│  ────────────────────────────────────────────────────────────    │
│  Search [________________]                                        │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Name              │ Industry    │ Revenue  │ Status  │   │    │
│  ├─────────────────────────────────────────────────────────┤    │
│  │ Rolling Rock Stone│ Manufactur. │ Mid-Mkt  │ Active  │ →│    │
│  │ Apex Brand        │ Consumer    │ Enterprise│ Active  │ →│    │
│  └─────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

### Layout — Client Detail (`/clients/{id}`)

```
┌──────────────────────────────────────────────────────────────────┐
│  Clients › Rolling Rock Stone                                     │
│  Rolling Rock Stone                             [Edit] [Delete]   │
│  ────────────────────────────────────────────────────────────    │
│                                                                   │
│  DETAILS                    QUICK LINKS                          │
│  Industry: Manufacturing    [View Audit Dashboard →]             │
│  Revenue: Mid-Market        [Run Audit →]                        │
│  Region: Midwest                                                  │
│  Contact: Jane Smith                                              │
│  Email: jane@rrs.com                                             │
│  ────────────────────────────────────────────────────────────    │
│  DOCUMENTS (see Screen 11 below)                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Layout — New/Edit Client Form (`/clients/new`, edit mode on `/clients/{id}`)

Form fields: Client Name, Slug (auto-generated, editable), Industry (dropdown), Revenue Band (dropdown), Region (text), Primary Contact Name, Email. Save (primary) / Cancel (secondary).

### User Interactions

- List row → `/clients/{id}`
- "+ New Client" → `/clients/new`
- Edit button → switches Client Detail to edit mode (in-place form)
- Delete → confirmation modal → on confirm: soft-delete → redirect to `/clients`
- "View Audit Dashboard" → `/audits/{slug}`

### Error & Empty States

- Empty list: "No clients yet — Add your first client"
- Slug already taken: inline error
- Delete fails: toast "Could not delete client"

---

## Screen 11: Document Management

**Location:** Embedded within Client Detail (`/clients/{id}`) below client info  
**Also accessible via:** a dedicated section on the client page  
**Access:** Auditor, Tenant Admin, Super Admin  
**Purpose:** Upload, list, and delete reference documents (PDFs, DOCX) used as evidence by the audit skill chain.

### Layout (embedded in Client Detail)

```
│  DOCUMENTS                                       [+ Upload]      │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Brand_Brief.pdf          PDF    234 KB    May 1 2026 [✕]│    │
│  │ Website_Audit.docx       DOCX   512 KB    Apr 28 2026[✕]│    │
│  │ Annual_Report_2025.pdf   PDF    1.2 MB    Mar 10 2026[✕]│    │
│  └─────────────────────────────────────────────────────────┘    │
```

### Key UI Components

- "+ Upload" button: opens file picker; accepts `.pdf`, `.docx`, `.xlsx`
- Upload progress: inline progress bar per file
- Document list: name, type badge, size, upload date, delete (✕) button
- Delete: confirmation modal before removing

### User Interactions

- Upload: drag-and-drop or click to browse → file validation (type, max 20MB) → progress → success toast
- Delete: confirm modal → DELETE request → removed from list → toast "Document deleted"

### Error & Empty States

- No documents: "No documents uploaded. Upload reference materials to enable evidence-based scoring."
- Upload fails: toast "Upload failed — check file type and size"
- Invalid file type: inline error "Only PDF, DOCX, and XLSX files are accepted"

---

## Screen 12: Tenant Admin Panel

**Route:** `/admin`  
**Access:** Tenant Admin, Super Admin  
**Purpose:** Manage users, roles, and subscription settings for the current tenant.

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]    │
├──────────────────────────────────────────────────────────────────┤
│  Tenant Admin                                                     │
│  ────────────────────────────────────────────────────────────    │
│                                                                   │
│  TABS: [Users]  [Roles & Permissions]  [Subscription]            │
│                                                                   │
│  ─── USERS TAB ───────────────────────────────────────────────   │
│  [+ Invite User]                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Name          │ Email              │ Role      │ Status  │    │
│  ├─────────────────────────────────────────────────────────┤    │
│  │ Jane Smith    │ jane@co.com        │ Auditor   │ Active  │    │
│  │ Bob Lee       │ bob@co.com         │ Reviewer  │ Pending │    │
│  └─────────────────────────────────────────────────────────┘    │
│  (click row to edit role or deactivate)                          │
└──────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Tabbed layout: Users / Roles & Permissions / Subscription
- Users tab: user list table + "Invite User" button (opens modal with email + role fields)
- Role edit: inline dropdown per user row
- Subscription tab: current plan, billing email, usage stats, upgrade CTA

### User Interactions

- Invite User → modal with email + role → sends invitation email → user appears as "Pending"
- Edit role → inline dropdown → save → toast "Role updated"
- Deactivate user → confirmation modal → user status → "Inactive"

### Error & Empty States

- No users (other than self): "Invite your team to collaborate."
- Invite fails (duplicate email): inline modal error

---

## Screen 13: Super Admin Panel

**Route:** `/super-admin`  
**Access:** Super Admin only  
**Purpose:** Manage all tenants, system-wide configuration, and global settings.

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  NAV BAR                                             [User ▾]    │
├──────────────────────────────────────────────────────────────────┤
│  Super Admin                                                      │
│  ────────────────────────────────────────────────────────────    │
│                                                                   │
│  TABS: [Tenants]  [System Config]  [Audit Types]  [AI Settings]  │
│                                                                   │
│  ─── TENANTS TAB ─────────────────────────────────────────────   │
│  [+ New Tenant]                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Tenant Name   │ Slug       │ Plan      │ Users │ Status  │    │
│  ├─────────────────────────────────────────────────────────┤    │
│  │ Acme Corp     │ acme       │ Pro       │ 8     │ Active  │    │
│  │ Beta LLC      │ beta       │ Starter   │ 2     │ Active  │    │
│  └─────────────────────────────────────────────────────────┘    │
│  (click row to view/manage tenant)                               │
│                                                                   │
│  ─── SYSTEM CONFIG TAB ───────────────────────────────────────   │
│  Global AI model settings, feature flags, rate limits            │
└──────────────────────────────────────────────────────────────────┘
```

### Key UI Components

- Tabs: Tenants / System Config / Audit Types / AI Settings
- Tenants tab: table of all tenants + "+ New Tenant" → `/onboarding`
- Tenant row click → opens tenant-scoped admin view (or modal with tenant details + "Switch to Tenant" action)
- System Config tab: key-value settings for AI model, rate limits, feature flags (editable form)
- AI Settings tab: prompt template management, model selection per skill type

### User Interactions

- "+ New Tenant" → `/onboarding`
- "Switch to Tenant" → sets active tenant context in nav (tenant switcher in top nav)
- Disable Tenant → confirmation modal → tenant status → "Suspended"

### Error & Empty States

- System Config save fails: toast "Config update failed — check values"
- No tenants (first run): "+ Create first tenant" CTA
