# Decision: Reviewer Lockout State Machine

**Author:** Neo (Backend Dev)  
**Date:** 2026-05-10  
**Status:** Proposed — Pending Team Ratification  
**Scope:** `ReviewerWorkflow` service, `reviewer_actions` table

---

## Decision

Enforce the reviewer lockout rule (3 rejections of same category within 24 hours → HTTP 409) via a query-time COUNT check against the `reviewer_actions` table. No cron jobs. No separate lockout table. The 24-hour window is rolling from `NOW()`.

---

## Table: reviewer_actions Schema

```sql
CREATE TABLE reviewer_actions (
    id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid        NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    audit_run_id    uuid        NOT NULL REFERENCES audit_runs(id) ON DELETE CASCADE,
    category_id     uuid        NOT NULL REFERENCES category_results(id) ON DELETE CASCADE,
    reviewer_id     uuid        NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    action_type     varchar(20) NOT NULL
                    CHECK (action_type IN ('approve','reject','edit','rerun','escalate')),
    notes           text,
    override_reason_code varchar(50),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_reviewer_actions_tenant_run_cat_reviewer
    ON reviewer_actions (tenant_id, audit_run_id, category_id, reviewer_id);

CREATE INDEX ix_reviewer_actions_run_cat_type_time
    ON reviewer_actions (audit_run_id, category_id, action_type, created_at);
```

---

## Lockout Check Query

The lockout check counts `reject` actions by the **same reviewer** against the **same category** within the **same audit run** in the past 24 hours:

```sql
SELECT COUNT(*) AS rejection_count
FROM reviewer_actions
WHERE tenant_id       = @tenantId
  AND audit_run_id    = @auditRunId
  AND category_id     = @categoryId
  AND reviewer_id     = @reviewerId
  AND action_type     = 'reject'
  AND created_at      > NOW() - INTERVAL '24 hours';
```

If `rejection_count >= 3` → throw `ReviewerLockoutException` → HTTP 409 with code `REVIEWER_REJECTION_LOCKOUT`.

**Lockout scope clarification:** The lockout is scoped to `(tenant_id, audit_run_id, category_id, reviewer_id)` — it is **per reviewer**, not per category globally. Rationale: a different reviewer can always step in and act. The lockout targets a specific reviewer who has repeatedly rejected the same category, not the category itself.

---

## Atomicity: Check + Insert Transaction

**Isolation Level:** `READ COMMITTED` with **optimistic retry** (not SERIALIZABLE).

Rationale:
- SERIALIZABLE would serialize all reviewer actions globally, causing unnecessary contention in the common case where different reviewers act on different categories simultaneously.
- The lockout window is 24 hours and the threshold is 3 — a race condition that adds a phantom 4th rejection is functionally harmless and extremely unlikely.
- The CHECK + INSERT sequence is wrapped in a single EF Core transaction at `READ COMMITTED`:

```csharp
await using var tx = await dbContext.Database.BeginTransactionAsync(
    IsolationLevel.ReadCommitted, ct);

var rejectionCount = await dbContext.ReviewerActions
    .Where(ra =>
        ra.TenantId == tenantId &&
        ra.AuditRunId == auditRunId &&
        ra.CategoryId == categoryId &&
        ra.ReviewerId == reviewerId &&
        ra.ActionType == "reject" &&
        ra.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24))
    .CountAsync(ct);

if (rejectionCount >= 3)
{
    await tx.RollbackAsync(ct);
    throw new ReviewerLockoutException(auditRunId, categoryId, reviewerId);
}

dbContext.ReviewerActions.Add(new ReviewerAction { ... });
await dbContext.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

If a concurrent transaction slips a 4th rejection through in the same millisecond window, the reviewer is de facto locked out on the next request anyway. No retry loop is needed.

---

## 24-Hour Window

**Rolling** from `NOW()` at the time of the lockout check — NOT a fixed calendar day.

`created_at > NOW() - INTERVAL '24 hours'` means: if a reviewer's 3 rejections were at T+0h, T+4h, and T+8h, their lockout expires at T+24h (rolling from the oldest qualifying rejection that was most recent enough to count).

No cron job, no expiry table, no background worker — the window is evaluated at query time on every action attempt. Expired rejections fall out of the window automatically.

---

## HTTP 409 Response Body

Per the API spec error envelope (RFC 7807 / `application/problem+json`):

```json
{
  "type": "https://strategicglue.com/errors/REVIEWER_REJECTION_LOCKOUT",
  "title": "Reviewer Rejection Lockout",
  "status": 409,
  "detail": "You have rejected this category 3 or more times in the past 24 hours. A different reviewer must act on this category.",
  "code": "REVIEWER_REJECTION_LOCKOUT",
  "correlationId": "uuid-from-X-Correlation-ID-header",
  "traceId": "aspnet-activity-trace-id"
}
```

The response does **not** include the reviewer's ID, rejection timestamps, or any PII in the body (ILogger structured log captures these for diagnostics, no PII exposed to the caller).

---

## CalibrationDelta Sequence Relative to Lockout Check

For `EditAsync` (the only reviewer action that creates a `CalibrationDelta`), the exact sequence is:

1. **Lockout check** — query `reviewer_actions` count. Throw `ReviewerLockoutException` if locked. ← checked FIRST
2. **Validate inputs** — `newActivityScore` in range [0,10], `overrideReasonCode` not empty, `notes` not empty.
3. **Open transaction** (READ COMMITTED)
4. **Insert `calibration_deltas`** record ← BEFORE updating category
5. **Update `category_results`** with new scores
6. **Insert `category_result_versions`** row (`source_type = 'reviewer'`, incremented `version_number`)
7. **Insert `reviewer_actions`** record (`action_type = 'edit'`)
8. **Commit transaction**

Rationale for CalibrationDelta before lockout check: The lockout check is on `reject` actions only — `edit` is a separate `action_type`. An `edit` cannot trigger lockout. CalibrationDelta is created after the lockout check passes to ensure we only record deltas for actions that actually succeed. If the lockout check blocked the action, no delta should be recorded.

**For `RejectAsync` (implicit in the `reject` variant of `EditAsync` or explicit reject):** No `CalibrationDelta` is created (rejections do not modify scores). The sequence is:
1. Lockout check
2. Insert `reviewer_actions` record (`action_type = 'reject'`)
3. Update `category_results.status = 'rejected'`

---

## Lockout Expiry

Automatic — no cron, no scheduled job, no cleanup required. The 24-hour window is evaluated at query time on every action. Rejected actions older than 24 hours are simply not counted. The table can be purged periodically by `sf_admin` for storage hygiene (never by `sf_app`).
