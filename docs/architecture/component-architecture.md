# Blazor Component Architecture — StrategicGlue Six-to-Fix

> **Version:** 1.0  
> **Author:** Trinity (Blazor Dev)  
> **Date:** 2026-05-10  
> **Status:** Locked — gates all UI development  
>
> This document defines the full component tree for every screen, the shared component catalog with parameter contracts, and the single authoritative SignalR state propagation pattern. All Razor development follows this structure exactly.

---

## Section 1: Project Structure

```
StrategicGlue.Web/
├── Pages/                          # Route-based Razor pages (@page directive)
│   ├── Auth/
│   │   └── LoginPage.razor
│   ├── Admin/
│   │   ├── TenantAdminPage.razor
│   │   └── SuperAdminPage.razor
│   ├── Audits/
│   │   ├── AuditListPage.razor
│   │   ├── AuditDetailPage.razor
│   │   └── SkillChainRunnerPage.razor
│   ├── Calibration/
│   │   └── CalibrationDashboardPage.razor
│   ├── Clients/
│   │   └── ClientManagementPage.razor
│   ├── Documents/
│   │   └── DocumentManagementPage.razor
│   ├── Onboarding/
│   │   └── TenantOnboardingPage.razor
│   ├── Review/
│   │   └── ReviewerQueuePage.razor
│   └── Telemetry/
│       └── TelemetryDashboardPage.razor
│
├── Shared/                         # Reusable components (no @page directive)
│   ├── AuditScoreSummary.razor
│   ├── CategoryReviewDrawer.razor
│   ├── ConfirmDialog.razor
│   ├── DataTable.razor
│   ├── LoadingSpinner.razor
│   ├── PolicyFlagBadge.razor
│   ├── ScoreCard.razor
│   ├── SkillChainProgress.razor
│   ├── SkillProgressItem.razor
│   ├── StatusDot.razor
│   └── TierBadge.razor
│
├── Layout/
│   ├── MainLayout.razor            # Authenticated shell: sidebar + top nav + main
│   ├── AuthLayout.razor            # Minimal unauthenticated shell (login)
│   ├── NavSidebar.razor            # Role-filtered navigation links
│   └── TopNav.razor                # Tenant name, user name, sign-out
│
├── wwwroot/
│   └── css/
│       ├── design-system.css       # All :root custom property tokens
│       ├── components.css          # Component-level styles (.btn, .card, .drawer, …)
│       └── app.css                 # Global resets, body, typography baseline
│
└── _Imports.razor                  # Global @using directives
```

### CSS File Responsibilities

| File | Contains |
|---|---|
| `design-system.css` | All `:root` tokens — colors, typography, spacing, shadows, radii, transitions |
| `components.css` | Named CSS classes for buttons, cards, tables, badges, drawers, status dots, progress bars |
| `app.css` | `html`/`body` resets, base font size (14px), link defaults, focus-visible global, utility overrides |

Load order in `<head>`: `design-system.css` → `components.css` → `app.css`.

---

## Section 2: Layout Shell

### AuthLayout.razor

Wraps unauthenticated pages (Login only). Minimal: centered card on `--bg-primary` background, no nav, no sidebar.

```
<div class="auth-shell">
  @Body
</div>
```

### MainLayout.razor

Wraps all authenticated pages. Three-zone layout:

```
┌──────────────────────────────────────────────────┐
│  TopNav.razor  (--bg-dark, full width)            │
├─────────────┬────────────────────────────────────┤
│ NavSidebar  │  <main class="main-content">        │
│ .razor      │    @Body                            │
│ (role-      │  </main>                            │
│  filtered)  │                                     │
└─────────────┴────────────────────────────────────┘
```

**CascadingAuthenticationState** wraps the entire layout tree in `App.razor`, making `AuthenticationState` available to all child components without explicit parameter threading.

### TopNav.razor

Displays:
- Platform logo / tenant name (from cascading auth claims: `tenant_slug`)
- Logged-in user display name
- Sign-out button

Reads `AuthenticationState` via `[CascadingParameter]`. No service injection — display only.

### NavSidebar.razor

Renders navigation links filtered by role using `<AuthorizeView Roles="...">`. Links include:

| Link | Visible To |
|---|---|
| Audits | All authenticated |
| Reviewer Queue | Reviewer, TenantAdmin, SuperAdmin |
| Clients | TenantAdmin, SuperAdmin |
| Documents | TenantAdmin, Reviewer, SuperAdmin |
| Calibration | TenantAdmin, SuperAdmin |
| Telemetry | TenantAdmin, SuperAdmin |
| Tenant Admin | TenantAdmin, SuperAdmin |
| Super Admin | SuperAdmin only |

Active link highlighted via `NavLinkMatch.All` on `<NavLink>`.

---

## Section 3: Screen-to-Component Decomposition

---

### 1. Login

| Property | Value |
|---|---|
| **Component** | `LoginPage.razor` |
| **Route** | `/login` |
| **Layout** | `AuthLayout` |
| **SignalR** | None |
| **Role Gate** | Public. Already-authenticated users are redirected to `/audits` |

**Data:**  
No service injection. Submits to ASP.NET Core Identity login endpoint via `NavigationManager` redirect or form POST.

**State:**
```csharp
private string _email = "";
private string _password = "";
private bool _isLoading;
private string? _errorMessage;
```

**Child Components:** None — self-contained form using `.input-field`, `.btn-primary`.

---

### 2. Tenant Onboarding

| Property | Value |
|---|---|
| **Component** | `TenantOnboardingPage.razor` |
| **Route** | `/onboard` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize(Roles = "TenantAdmin,SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:** `@inject ITenantService TenantService`  
Calls: `TenantService.GetOnboardingStatusAsync()`, `TenantService.SaveOnboardingStepAsync()`

**State:**
```csharp
private OnboardingDto? _status;
private int _currentStep;
private bool _isSubmitting;
private string? _errorMessage;
```

**Child Components:**
- `ConfirmDialog.razor` — for destructive step resets

---

### 3. Audit List

| Property | Value |
|---|---|
| **Component** | `AuditListPage.razor` |
| **Route** | `/audits` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize]` → unauthorized redirect to `/login` |

**Data:** `@inject IAuditService AuditService`  
Calls: `AuditService.GetAuditListAsync(tenantId, page, filters)`

**State:**
```csharp
private List<AuditSummaryDto>? _audits;
private bool _isLoading;
private AuditListFilters _filters = new();
private int _currentPage = 1;
```

**Child Components:**
- `DataTable<AuditSummaryDto>` — sortable audit rows
- `TierBadge` — per-row tier indicator
- `LoadingSpinner` — initial load state
- `ConfirmDialog` — delete audit confirmation (TenantAdmin/SuperAdmin only)

---

### 4. Audit Detail Dashboard

| Property | Value |
|---|---|
| **Component** | `AuditDetailPage.razor` |
| **Route** | `/audits/{AuditRunId}` |
| **Layout** | `MainLayout` |
| **SignalR** | Yes — joins group by `AuditRunId`, listens to `run-completed`, `run-failed` for status refresh |
| **Role Gate** | `[Authorize]` → unauthorized redirect to `/login` |

**Data:**  
`@inject IAuditService AuditService`  
`@inject IAuditHubConnection HubConnection`  
Calls: `AuditService.GetAuditDetailAsync(AuditRunId)`

**State:**
```csharp
[Parameter] public string AuditRunId { get; set; } = "";
private AuditDetailDto? _detail;
private bool _isLoading;
private CategoryReviewDrawerContext? _openDrawerContext; // null = drawer closed
```

**Child Components:**
- `AuditScoreSummary` — 6 area scores grid
- `TierBadge` — overall tier
- `ScoreCard` ×6 — one per marketing area
- `CategoryReviewDrawer` — opens when reviewer clicks an area card
- `StatusDot` — current run status
- `LoadingSpinner`

---

### 5. Skill Chain Runner

| Property | Value |
|---|---|
| **Component** | `SkillChainRunnerPage.razor` |
| **Route** | `/audits/{AuditRunId}/run` |
| **Layout** | `MainLayout` |
| **SignalR** | Yes — joins group by `AuditRunId`, listens to ALL 7 event types |
| **Role Gate** | `[Authorize(Roles = "TenantAdmin,SuperAdmin")]` → unauthorized redirect to `/audits/{AuditRunId}` |

**Data:**  
`@inject IAuditService AuditService`  
`@inject IAuditHubConnection HubConnection`  
Calls: `AuditService.StartSkillChainAsync(AuditRunId)`, `AuditService.GetRunStatusAsync(AuditRunId)`

**State:**
```csharp
[Parameter] public string AuditRunId { get; set; } = "";
private AuditRunStatusDto? _runStatus;
private List<SkillProgressDto> _skills = new();
private bool _isRunning;
private bool _hasFailed;
private string? _failureMessage;
```

**Child Components:**
- `SkillChainProgress` — all 5 skills with real-time updates
- `SkillProgressItem` ×5 — individual skill rows
- `StatusDot` — overall run status
- `LoadingSpinner` — while loading initial state

**SignalR Events Handled:**

| Event | Effect |
|---|---|
| `skill-started` | Updates matching `SkillProgressDto.Status` → `running` |
| `skill-completed` | Updates matching skill → `completed`, sets progress 100% |
| `skill-failed` | Updates matching skill → `failed`, sets `_hasFailed` |
| `council-started` | Updates skill display to show council deliberation sub-state |
| `council-completed` | Council sub-state resolved |
| `run-completed` | Sets `_isRunning = false`, navigates or shows completion banner |
| `run-failed` | Sets `_hasFailed = true`, shows error panel |

---

### 6. Reviewer Queue

| Property | Value |
|---|---|
| **Component** | `ReviewerQueuePage.razor` |
| **Route** | `/review` |
| **Layout** | `MainLayout` |
| **SignalR** | None (queue refreshes on action; no live feed needed) |
| **Role Gate** | `[Authorize(Roles = "Reviewer,TenantAdmin,SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:**  
`@inject IReviewerService ReviewerService`  
Calls: `ReviewerService.GetQueueAsync(tenantId)`, `ReviewerService.ApproveAsync()`, `ReviewerService.RejectAsync()`, `ReviewerService.RerunAsync()`, `ReviewerService.EscalateAsync()`

**State:**
```csharp
private List<ReviewQueueItemDto>? _queue;
private bool _isLoading;
private CategoryReviewDrawerContext? _openDrawerContext;
```

**Child Components:**
- `DataTable<ReviewQueueItemDto>` — sortable queue rows
- `CategoryReviewDrawer` — opens on row click, handles approve/reject/rerun/escalate
- `StatusDot` — per-row category status
- `PolicyFlagBadge` — per-row policy flags
- `LoadingSpinner`

---

### 7. Category Review Drawer

| Property | Value |
|---|---|
| **Component** | `CategoryReviewDrawer.razor` |
| **Type** | Shared component — **not** a routed page |
| **Used On** | `AuditDetailPage`, `ReviewerQueuePage` |

**Parameters:**
```csharp
[Parameter] public CategoryReviewDrawerContext? Context { get; set; }
[Parameter] public EventCallback OnClose { get; set; }
[Parameter] public EventCallback<ReviewActionResult> OnActionCompleted { get; set; }
```

**State:**
```csharp
private ReviewAction _selectedAction;
private int? _overrideScore;
private string _overrideStrategyLevel = "";
private string _overrideReason = "";
private string _reviewNotes = "";
private bool _isSubmitting;
private string? _errorMessage;
```

Visible when `Context != null`. Rendered via portal-style absolute positioning (`.drawer` + `.drawer-overlay`).

---

### 8. Calibration Dashboard

| Property | Value |
|---|---|
| **Component** | `CalibrationDashboardPage.razor` |
| **Route** | `/calibration` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize(Roles = "TenantAdmin,SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:**  
`@inject ICalibrationService CalibrationService`  
Calls: `CalibrationService.GetCalibrationHistoryAsync(tenantId, filters)`, `CalibrationService.GetDeltaSummaryAsync()`

**State:**
```csharp
private List<CalibrationDeltaDto>? _history;
private CalibrationSummaryDto? _summary;
private CalibrationFilters _filters = new();
private bool _isLoading;
```

**Child Components:**
- `DataTable<CalibrationDeltaDto>` — delta history rows
- `ScoreCard` (summary variant) — aggregate delta stats
- `LoadingSpinner`

---

### 9. Telemetry Dashboard

| Property | Value |
|---|---|
| **Component** | `TelemetryDashboardPage.razor` |
| **Route** | `/telemetry` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize(Roles = "TenantAdmin,SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:**  
`@inject ITelemetryService TelemetryService`  
Calls: `TelemetryService.GetDailyMetricsAsync(tenantId, dateRange)`, `TelemetryService.GetAggregatesAsync()`

**State:**
```csharp
private TelemetryDailyDto[]? _metrics;
private TelemetryAggregateDto? _aggregates;
private DateRange _dateRange = DateRange.Last30Days;
private bool _isLoading;
```

SuperAdmin sees cross-tenant aggregates; TenantAdmin sees own tenant only. Role filtering happens at the service layer, not the component.

**Child Components:**
- `DataTable<TelemetryDailyDto>` — daily metrics rows
- `LoadingSpinner`

---

### 10. Client Management

| Property | Value |
|---|---|
| **Component** | `ClientManagementPage.razor` |
| **Route** | `/clients` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize(Roles = "TenantAdmin,SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:**  
`@inject IClientService ClientService`  
Calls: `ClientService.GetClientsAsync(tenantId)`, `ClientService.CreateClientAsync()`, `ClientService.DeleteClientAsync()`

**State:**
```csharp
private List<ClientDto>? _clients;
private bool _isLoading;
private bool _showCreateModal;
private ClientCreateDto _newClient = new();
private bool _isSubmitting;
```

**Child Components:**
- `DataTable<ClientDto>`
- `ConfirmDialog` — delete client
- `LoadingSpinner`

---

### 11. Document Management

| Property | Value |
|---|---|
| **Component** | `DocumentManagementPage.razor` |
| **Route** | `/documents` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize(Roles = "TenantAdmin,Reviewer,SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:**  
`@inject IDocumentService DocumentService`  
Calls: `DocumentService.GetDocumentsAsync(clientId)`, `DocumentService.UploadDocumentAsync()`, `DocumentService.DeleteDocumentAsync()`

**State:**
```csharp
private List<DocumentDto>? _documents;
private bool _isLoading;
private bool _isUploading;
private string? _uploadError;
```

**Child Components:**
- `DataTable<DocumentDto>`
- `ConfirmDialog` — delete document
- `LoadingSpinner`

---

### 12. Tenant Admin Panel

| Property | Value |
|---|---|
| **Component** | `TenantAdminPage.razor` |
| **Route** | `/admin/tenant` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize(Roles = "TenantAdmin,SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:**  
`@inject ITenantService TenantService`  
`@inject IUserManagementService UserService`  
Calls: `TenantService.GetTenantSettingsAsync()`, `UserService.GetUsersAsync(tenantId)`, `UserService.InviteUserAsync()`, `UserService.UpdateUserRoleAsync()`

**State:**
```csharp
private TenantSettingsDto? _settings;
private List<TenantUserDto>? _users;
private bool _isLoading;
private bool _showInviteForm;
private UserInviteDto _invite = new();
```

**Child Components:**
- `DataTable<TenantUserDto>` — user list with role column
- `ConfirmDialog` — remove user
- `LoadingSpinner`

---

### 13. Super Admin Panel

| Property | Value |
|---|---|
| **Component** | `SuperAdminPage.razor` |
| **Route** | `/admin/super` |
| **Layout** | `MainLayout` |
| **SignalR** | None |
| **Role Gate** | `[Authorize(Roles = "SuperAdmin")]` → unauthorized redirect to `/audits` |

**Data:**  
`@inject ISuperAdminService SuperAdminService`  
Calls: `SuperAdminService.GetAllTenantsAsync()`, `SuperAdminService.GetSystemHealthAsync()`, `SuperAdminService.SuspendTenantAsync()`

**State:**
```csharp
private List<TenantSummaryDto>? _tenants;
private SystemHealthDto? _health;
private bool _isLoading;
```

**Child Components:**
- `DataTable<TenantSummaryDto>` — all tenant rows
- `TierBadge` — per-tenant tier distribution
- `ConfirmDialog` — suspend tenant
- `LoadingSpinner`

---

## Section 4: Shared Component Catalog

---

### ScoreCard.razor

Displays a single marketing area score with badge, label, evidence count, and optional policy flags.

```csharp
[Parameter] public string AreaName { get; set; } = "";         // "Brand", "Customer", etc.
[Parameter] public int Score { get; set; }                     // 0–10
[Parameter] public string DocumentedStrategy { get; set; } = ""; // "current" | "partial" | "none"
[Parameter] public int EvidenceCount { get; set; }
[Parameter] public List<PolicyFlagDto> PolicyFlags { get; set; } = new();
[Parameter] public bool IsClickable { get; set; }              // true → emits OnClick
[Parameter] public EventCallback OnClick { get; set; }
```

Renders: `.card` with area icon, `.score-badge` (color per score range), strategy level chip, evidence count, and `PolicyFlagBadge` per flag.

---

### AuditScoreSummary.razor

Six `ScoreCard` components in a 3×2 grid. Emits an event when a card is clicked (for opening the review drawer).

```csharp
[Parameter] public List<AreaScoreDto> AreaScores { get; set; } = new();
[Parameter] public bool AllowDrawerOpen { get; set; }           // true for Reviewer/TenantAdmin
[Parameter] public EventCallback<AreaScoreDto> OnAreaSelected { get; set; }
```

---

### TierBadge.razor

```csharp
[Parameter] public string Tier { get; set; } = "";   // "tier_1" | "tier_2" | "tier_3"
[Parameter] public bool ShowLabel { get; set; } = true;
```

Renders a `.tier-badge` pill. Maps `tier_1` → `.tier-1`, etc.

---

### SkillProgressItem.razor

One row in the skill chain execution list.

```csharp
[Parameter] public string SkillName { get; set; } = "";
[Parameter] public string Status { get; set; } = "pending";  // pending | running | completed | failed
[Parameter] public int ProgressPercent { get; set; }         // 0–100
[Parameter] public string? ErrorMessage { get; set; }
[Parameter] public bool CouncilActive { get; set; }          // true while council running on this skill
```

Renders: skill label, `.status-dot`, `.progress-bar` (visible when running), error message (visible when failed), council sub-label (when `CouncilActive`).

---

### SkillChainProgress.razor

Composes all 5 `SkillProgressItem` components. Receives live updates from the parent page via parameters (page owns SignalR subscription and pushes state down).

```csharp
[Parameter] public List<SkillProgressDto> Skills { get; set; } = new();
[Parameter] public bool IsRunning { get; set; }
[Parameter] public bool HasFailed { get; set; }
[Parameter] public string? FailureMessage { get; set; }
```

No direct SignalR subscription — state flows down from `SkillChainRunnerPage` as parameters.

---

### CategoryReviewDrawer.razor

See Section 3, Screen 7.

---

### PolicyFlagBadge.razor

```csharp
[Parameter] public PolicyFlagDto Flag { get; set; } = new();  // Level: "Warning" | "Trigger", Rule: string
[Parameter] public bool ShowTooltip { get; set; } = true;
```

Renders a compact badge (`.policy-flag-badge`) — amber for Warning, red for Trigger. Tooltip shows rule name and description on `:hover`/`:focus`.

---

### ConfirmDialog.razor

Reusable modal for destructive action confirmation.

```csharp
[Parameter] public bool IsOpen { get; set; }
[Parameter] public string Title { get; set; } = "Confirm";
[Parameter] public string Message { get; set; } = "";
[Parameter] public string ConfirmLabel { get; set; } = "Confirm";
[Parameter] public string CancelLabel { get; set; } = "Cancel";
[Parameter] public bool IsDangerous { get; set; }              // true → .btn-danger on confirm
[Parameter] public EventCallback OnConfirm { get; set; }
[Parameter] public EventCallback OnCancel { get; set; }
```

Renders as a centered overlay modal with `.shadow-xl`. Traps focus while open (a11y).

---

### LoadingSpinner.razor

```csharp
[Parameter] public string Label { get; set; } = "Loading…";
[Parameter] public bool FullPage { get; set; }   // centers in viewport vs inline
```

---

### DataTable.razor

Generic sortable table. `TItem` is constrained to a DTO with a string key.

```csharp
[Parameter] public List<TItem> Items { get; set; } = new();
[Parameter] public RenderFragment<TItem> RowTemplate { get; set; } = null!;
[Parameter] public RenderFragment HeaderTemplate { get; set; } = null!;
[Parameter] public bool IsLoading { get; set; }
[Parameter] public string EmptyMessage { get; set; } = "No items found.";
[Parameter] public string? SortColumn { get; set; }
[Parameter] public bool SortDescending { get; set; }
[Parameter] public EventCallback<string> OnSort { get; set; }
```

Callers define `HeaderTemplate` and `RowTemplate` as render fragments. `DataTable` handles: empty state, loading state (spinner row), sticky header, hover row highlight.

---

### StatusDot.razor

```csharp
[Parameter] public string Status { get; set; } = "pending";  // pending | running | completed | failed
[Parameter] public bool ShowLabel { get; set; } = true;
```

---

## Section 5: SignalR State Propagation Pattern

### Chosen Pattern: B — `IAuditHubConnection` Scoped Service

**Justification:**  
A Scoped service (one per Blazor circuit/user) owns the `HubConnection` lifetime. Pages that need live data inject `IAuditHubConnection` and subscribe to typed event delegates. Child components receive state via parameters — they never touch the hub directly.

This is preferred over:
- **Option A (Singleton):** Cross-user event bleed risk; wrong scope for multi-tenant.
- **Option C (CascadingValue):** Tightly couples layout to hub state; awkward for pages that don't need it.

---

### HubConnection Lifetime

```
Circuit created
  └── IAuditHubConnection (Scoped) registered in DI
        └── HubConnection built (not yet connected)

Page.OnInitializedAsync()
  └── await HubConnection.JoinGroupAsync(auditRunId)   ← connects + joins group
        └── subscribes to typed event handlers

Page.DisposeAsync()
  └── await HubConnection.LeaveGroupAsync(auditRunId)
  └── unsubscribes handlers

Circuit destroyed
  └── Scoped IAuditHubConnection.DisposeAsync()
        └── await _hubConnection.DisposeAsync()
```

---

### Interface Definition

```csharp
public interface IAuditHubConnection : IAsyncDisposable
{
    Task JoinGroupAsync(string auditRunId);
    Task LeaveGroupAsync(string auditRunId);

    event Func<SkillStartedEvent, Task>? OnSkillStarted;
    event Func<SkillCompletedEvent, Task>? OnSkillCompleted;
    event Func<SkillFailedEvent, Task>? OnSkillFailed;
    event Func<CouncilStartedEvent, Task>? OnCouncilStarted;
    event Func<CouncilCompletedEvent, Task>? OnCouncilCompleted;
    event Func<RunCompletedEvent, Task>? OnRunCompleted;
    event Func<RunFailedEvent, Task>? OnRunFailed;
}
```

---

### Component Subscription Pattern

Pages subscribe in `OnInitializedAsync` and call `StateHasChanged()` explicitly (required for Blazor Server — hub callbacks arrive on a non-render thread).

```csharp
@implements IAsyncDisposable
@inject IAuditHubConnection HubConnection

protected override async Task OnInitializedAsync()
{
    HubConnection.OnSkillStarted += HandleSkillStarted;
    HubConnection.OnSkillCompleted += HandleSkillCompleted;
    // ... subscribe all relevant events
    await HubConnection.JoinGroupAsync(AuditRunId);
}

private async Task HandleSkillStarted(SkillStartedEvent e)
{
    var skill = _skills.FirstOrDefault(s => s.Name == e.SkillName);
    if (skill is not null) skill.Status = "running";
    await InvokeAsync(StateHasChanged);   // ← required: marshals to render thread
}

public async ValueTask DisposeAsync()
{
    HubConnection.OnSkillStarted -= HandleSkillStarted;
    HubConnection.OnSkillCompleted -= HandleSkillCompleted;
    // ... unsubscribe all
    await HubConnection.LeaveGroupAsync(AuditRunId);
}
```

**`StateHasChanged()` rule:** Always called via `InvokeAsync(StateHasChanged)` inside hub event handlers. Never called from child components — only the owning page calls it.

---

### Child Components Receive State via Parameters

`SkillChainProgress` and `SkillProgressItem` are purely presentational:

```razor
<!-- SkillChainRunnerPage.razor -->
<SkillChainProgress Skills="_skills"
                    IsRunning="_isRunning"
                    HasFailed="_hasFailed"
                    FailureMessage="_failureMessage" />
```

When the page calls `StateHasChanged()`, Blazor re-renders the tree and parameters flow down automatically. Child components never call `StateHasChanged()` themselves.

---

### Circuit Reconnect Behavior

Blazor Server's built-in reconnect UI (`components-reconnect-modal`) handles transient disconnects. For the `HubConnection`:

- `HubConnection` is configured with `.WithAutomaticReconnect()` — it will attempt to rejoin after a network blip.
- On reconnect, the service fires an `OnReconnected` callback. The page re-calls `JoinGroupAsync(auditRunId)` to re-enter the SignalR group (group membership is lost on server restart).
- **State recovery on reconnect:** Page calls `AuditService.GetRunStatusAsync(AuditRunId)` to reload current state. Events missed during disconnection are not replayed — the API provides current snapshot.
- If the circuit is fully terminated and recreated, `OnInitializedAsync` runs again from scratch — full state reload.

---

### IAsyncDisposable on Components

All pages that use `IAuditHubConnection` implement `IAsyncDisposable`. The Blazor runtime calls `DisposeAsync()` when the page is navigated away from. This is the disposal contract:

```csharp
public async ValueTask DisposeAsync()
{
    // 1. Unsubscribe all event handlers (prevent memory leaks)
    HubConnection.OnSkillStarted -= HandleSkillStarted;
    // ... all events

    // 2. Leave the SignalR group
    await HubConnection.LeaveGroupAsync(AuditRunId);
    // Note: IAuditHubConnection itself is NOT disposed here —
    // it's Scoped and the DI container disposes it when the circuit ends.
}
```
