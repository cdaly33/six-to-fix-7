# ADR: Reviewer Lockout Transaction Boundaries

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  
**Supersedes:** —  

---

## Context

The business rule is: **3 rejections of the same category result by any reviewer within a 24-hour window → lock out further rejections and return HTTP 409 `REVIEWER_REJECTION_LOCKOUT`.**

This rule has non-trivial concurrency semantics. Two concurrent requests can each read a count of 2 and both attempt to perform the 3rd rejection. Without correct transaction boundaries, both succeed — producing 4 rejections and no lockout. This is a correctness bug, not just a race condition.

---

## Decision

### Tracking Table

Rejections are tracked in the `reviewer_rejections` table:

```sql
CREATE TABLE reviewer_rejections (
    rejection_id        UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID            NOT NULL REFERENCES tenants(id),
    audit_run_id        UUID            NOT NULL REFERENCES audit_runs(id),
    category_id         VARCHAR(50)     NOT NULL,
    reviewer_user_id    UUID            NOT NULL REFERENCES users(id),
    rejected_at         TIMESTAMPTZ     NOT NULL DEFAULT now(),
    override_reason     VARCHAR(100)    NOT NULL,
    notes               TEXT            NOT NULL
);

CREATE INDEX ix_reviewer_rejections_lockout_check
    ON reviewer_rejections (tenant_id, audit_run_id, category_id, rejected_at);
```

The lockout check queries this table for the count of rejections within the 24-hour window scoped to `(tenant_id, audit_run_id, category_id)`.

### Lockout Scope

The lockout is scoped to `(tenant_id, audit_run_id, category_id)`. This is the most precise scope:
- **Per-tenant** — mandatory for data isolation
- **Per-audit-run** — a new audit run for the same client/category resets the window
- **Per-category** — rejecting "brand" 3 times does not lock out "customer" rejections

A lockout on run X for category "brand" does not affect other categories or other runs.

### Transaction Boundary: Serializable + Row-Level Lock

The rejection-count-check and rejection-record-insert are performed **in a single serializable transaction** using a `SELECT ... FOR UPDATE` row-level lock on a lockout sentinel row, OR via PostgreSQL's `serializable` isolation level with conflict detection.

**Chosen mechanism: Advisory Lock + Serializable Transaction**

PostgreSQL advisory locks (`pg_try_advisory_xact_lock`) are used as the concurrency gate. The lock key is derived from the `(audit_run_id, category_id)` tuple:

```sql
BEGIN ISOLATION LEVEL SERIALIZABLE;

-- Acquire advisory lock keyed to this (audit_run_id, category_id)
-- If another transaction holds the lock, this blocks until it releases
SELECT pg_advisory_xact_lock(
    hashtext(audit_run_id::text || '|' || category_id)
);

-- Count rejections in the rolling 24-hour window
SELECT COUNT(*) FROM reviewer_rejections
WHERE tenant_id = @tenantId
  AND audit_run_id = @auditRunId
  AND category_id = @categoryId
  AND rejected_at >= now() - INTERVAL '24 hours';

-- If count >= 3: ROLLBACK and return 409
-- If count < 3: INSERT new rejection record, COMMIT
```

This guarantees that only one transaction at a time can read-then-write for a given `(audit_run_id, category_id)` pair. The "two simultaneous 3rd rejections" scenario is prevented: the second transaction blocks on the advisory lock, then reads count = 3 after the first commits, and returns 409.

**Implementation in ReviewerWorkflow:**

```csharp
public async Task<ReviewerActionResult> RejectAsync(RejectCategoryCommand cmd)
{
    await using var tx = await _db.Database.BeginTransactionAsync(
        IsolationLevel.Serializable, cancellationToken);

    // Advisory lock — scoped to transaction, released on COMMIT/ROLLBACK
    var lockKey = HashLockKey(cmd.AuditRunId, cmd.CategoryId);
    await _db.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_xact_lock({0})", lockKey);

    var count = await _db.ReviewerRejections
        .Where(r => r.TenantId == cmd.TenantId
                 && r.AuditRunId == cmd.AuditRunId
                 && r.CategoryId == cmd.CategoryId
                 && r.RejectedAt >= DateTimeOffset.UtcNow.AddHours(-24))
        .CountAsync();

    if (count >= 3)
    {
        await tx.RollbackAsync();
        return ReviewerActionResult.Lockout();
    }

    _db.ReviewerRejections.Add(new ReviewerRejection
    {
        TenantId        = cmd.TenantId,
        AuditRunId      = cmd.AuditRunId,
        CategoryId      = cmd.CategoryId,
        ReviewerUserId  = cmd.ReviewerUserId,
        OverrideReason  = cmd.OverrideReason,
        Notes           = cmd.Notes,
        RejectedAt      = DateTimeOffset.UtcNow
    });

    await _db.SaveChangesAsync();
    await tx.CommitAsync();

    return ReviewerActionResult.Success();
}
```

### 24-Hour Window: Rolling

The 24-hour window **rolls** — it is computed as `now() - 24 hours` at the time of each rejection check. It does NOT reset from the most recent rejection. This is the more consistent interpretation: it asks "how many rejections in the last 24 hours?" not "how long since the last rejection?"

**Consequence:** A lockout triggered at T=0 expires naturally when the oldest rejection in the window ages past 24 hours. If all 3 rejections happened at T=0, the lockout expires at T+24h automatically. No explicit lockout expiry record or scheduled job is needed.

### Lockout Expiry: Automatic (No Explicit Reset Record)

The lockout does not persist as a separate entity. It is a computed state derived from the `reviewer_rejections` count query. When the oldest rejection ages out of the 24-hour window, the lockout lifts automatically on the next rejection attempt.

No `reviewer_lockouts` table is needed. No background job to reset lockouts. The query is always authoritative.

### HTTP 409 Response Body

```json
{
  "type": "https://strategicglue.com/errors/reviewer-rejection-lockout",
  "title": "Reviewer Rejection Lockout",
  "status": 409,
  "detail": "This category has been rejected 3 times within 24 hours. Further rejections are locked until the window expires.",
  "code": "REVIEWER_REJECTION_LOCKOUT",
  "categoryId": "brand",
  "auditRunId": "uuid",
  "lockoutExpiresAt": "2026-05-11T15:00:00Z",
  "correlationId": "uuid",
  "traceId": "aspnet-trace-id"
}
```

`lockoutExpiresAt` is computed as the timestamp of the earliest rejection in the current window + 24 hours. This tells the calling client when the lockout naturally expires so they can inform the reviewer.

### CalibrationDelta on Lockout

**A `CalibrationDelta` is NOT created when the lockout is triggered.** The rejection was not applied — it was rejected by the system. Only successful score overrides (approve-with-edit, edit actions) create a `CalibrationDelta`. A blocked rejection creates only a `reviewer_rejections` row (which is already present from prior successful rejections that built up the count).

Stated precisely: the PRD rule "CalibrationDelta created on every reviewer score override — no exceptions" refers to successful overrides, not to rejected-rejection attempts. A 409 lockout response means no override occurred and no `CalibrationDelta` is created.

---

## Consequences

### ReviewerWorkflow Service Design

`ReviewerWorkflow.RejectAsync` must manage the transaction boundary explicitly. It cannot rely on EF Core's implicit `SaveChangesAsync` transaction — it needs the advisory lock and serializable isolation. The service opens the transaction explicitly via `IDbContextTransaction`.

### EF Core Entity

The `ReviewerRejection` entity maps to `reviewer_rejections`. The global query filter applies `TenantId == tenantContext.TenantId` in all non-SuperAdmin contexts.

### Migration

The `reviewer_rejections` table is created in a migration with the composite index on `(tenant_id, audit_run_id, category_id, rejected_at)` to support the lockout check query at acceptable performance under load.

### Different Reviewer Can Still Act

The lockout blocks any reviewer from **rejecting** the category further within the window. It does not block approval, editing, or escalation by any reviewer. A supervisor or second reviewer can approve or escalate even when the lockout is active.

### pgBouncer Compatibility

Advisory locks require a persistent connection for the duration of the transaction. pgBouncer in **transaction mode** (the default for pooling) releases the connection after each transaction, which would break advisory locks that span transaction boundaries. Since the advisory lock is acquired and released within a single `BEGIN ... COMMIT` block, it is compatible with transaction-mode pooling. The advisory lock is a transaction-scoped lock (`pg_advisory_xact_lock`), not a session-scoped lock (`pg_advisory_lock`).

---
