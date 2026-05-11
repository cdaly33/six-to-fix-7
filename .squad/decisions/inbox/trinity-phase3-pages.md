# Trinity Phase 3 — Page Wiring Decisions

**Date:** 2026-05-10  
**Author:** Trinity (Copilot CLI)  
**Branch:** dev/phase-3-ai-skills  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/12

## Route Changes from Original Spec

| Page | Original Spec | Actual Route | Reason |
|---|---|---|---|
| AuditDetail | `/audits/{id}` (string) | `/audits/{id:guid}` | Guid type constraint for safety |
| CategoryReview | `/review/{id}` (string) | `/review/{id:guid}` | Guid type constraint for safety |
| PublishedResults | `/results/{auditRunId:guid}` | `/results/{clientSlug}` | IPublisher.GetPublishedAuditAsync takes string slug, not Guid |

## Service Constraints Discovered

### IAuditOrchestrator — no tenant-wide list
`GetAuditRunsForClientAsync(Guid clientId)` requires a clientId.  
AuditList page requires `?clientId=` query param; shows error without it.  
**Action needed:** If a tenant-wide list is needed later, add `GetAuditRunsForTenantAsync(Guid tenantId)`.

### IReviewerWorkflow — missing RejectAsync
Only Approve, Edit, Rerun, Escalate, GetLockoutStatus exist.  
Reject button is rendered but disabled with a TODO comment.  
**Action needed (Neo):** Add `RejectAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, string reason)`.

### IReviewerWorkflow — no GetCategoryResultAsync
No way to fetch a single CategoryResult by ID from the reviewer workflow.  
CategoryReview page cannot show category name/score without loading the full AuditRun.  
**Action needed:** Add `GetCategoryResultAsync(Guid categoryId)` to IAuditOrchestrator or IReviewerWorkflow.

### No /api/auth/login endpoint
ApiEndpointExtensions only has `/health`. Login page has TODO comment.  
**Action needed (Neo):** Implement POST `/api/auth/login` returning `{ token: string }`.

### No IClientService
ClientManagement page is a TODO stub. No service for listing/creating clients.  
**Action needed:** Define IClientService and register it in DI.

## SignalR Implementation Pattern Used

- `HubConnectionBuilder` created per-page in `OnInitializedAsync` (not DI-injected service).
- `WithAutomaticReconnect()` configured.
- `JoinAuditRun(string auditRunId)` called with `auditRunId.ToString("N")` (no hyphens).
- `ReceiveEvent(string eventType, object payload)` callback via `_hub.On<string, object>`.
- On `run-completed` or `run-failed`: reload run data then `StateHasChanged`.
- Page implements `IAsyncDisposable`; `LeaveAuditRun` called before `DisposeAsync`.

## Auth Pattern in Blazor Server

- JWT stored in `localStorage` via `IJSRuntime.InvokeVoidAsync("localStorage.setItem", ...)`.
- `ClaimTypes.NameIdentifier` used to get userId for reviewer/publish actions.
- `AuthenticationStateProvider.GetAuthenticationStateAsync()` injected into pages that need userId.

## CSS Pattern

All new CSS uses `var(--*)` design tokens exclusively — zero hardcoded hex values.  
New patterns added to `app.css` (page-level patterns, not component-level):
- `.page-header`, `.page-header-left`, `.page-header-actions`
- `.alert`, `.alert-error`, `.alert-success`
- `.form-group`, `.btn-block`
- `.login-container`, `.login-card`
- `.meta-grid`, `.meta-item`
- `.category-result-row`, `.category-result-info`, `.category-result-actions`
- `.live-event-list`, `.live-event-item`, `.event-type-badge`, `.event-time`, `.event-summary`
- `.dashboard-grid`, `.dashboard-welcome`, `.dashboard-nav-card`
- `.empty-state`
- `.review-action-block`, `.review-guide-list`
- `.score-gap-bar`, `.reviewer-queue-layout`