# Decision: Immutable Publish Semantics

**Author:** Neo (Backend Dev)  
**Date:** 2026-05-10  
**Status:** Proposed — Pending Team Ratification  
**Scope:** `category_result_versions` table, `Publisher` service, `audit_runs` state machine

---

## Decision

`category_result_versions` is an append-only ledger. Once `audit_runs.status = 'published'`, the audit is immutable — no score modifications are permitted. The current view of scores lives in `category_results`; the full history lives in `category_result_versions`. Both tables are kept consistent by application-layer code (not DB triggers).

---

## Append-Only Enforcement

`category_result_versions` receives **INSERT only — never UPDATE or DELETE**.

Enforcement layers (defense-in-depth):
1. **`sf_app` role:** `UPDATE` and `DELETE` privileges on `category_result_versions` are explicitly `REVOKE`d for the runtime database role. The application physically cannot issue those statements.
2. **Service layer:** `IPublisher` and `IReviewerWorkflow` only ever call `dbContext.CategoryResultVersions.Add(...)` — never `.Update()` or `.Remove()` on this entity.
3. **EF Core configuration:** `CategoryResultVersion` entity is configured as `IsReadOnly` in `OnModelCreating` — EF Core is told to treat it as insert-only and will throw if UPDATE/DELETE is attempted via the context.

---

## What Triggers a New Version Row

A new row in `category_result_versions` is inserted exactly three times in a normal audit lifecycle per category:

| Event | `source_type` | `source_id` | Notes |
|-------|:---:|---|---|
| AI skill chain completes scoring | `'ai'` | `skill_runs.id` | First version; `version_number = 1` |
| AI Council adjusts a score | `'council'` | `council_decisions.id` | Only if `decision_type = 'adjusted'` |
| Reviewer overrides a score (Edit action) | `'reviewer'` | `reviewer_actions.id` | Only on Edit — Approve/Reject/Rerun do not change scores |

A category that passes policy evaluation without council and is approved without edits will have exactly **1** version row. A category that goes through council + a reviewer edit will have **3** rows.

---

## Schema

```sql
CREATE TABLE category_result_versions (
    id                      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               uuid        NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    audit_run_id            uuid        NOT NULL REFERENCES audit_runs(id) ON DELETE RESTRICT,
    category_id             uuid        NOT NULL REFERENCES category_results(id) ON DELETE RESTRICT,
    category                varchar(30) NOT NULL
                            CHECK (category IN ('brand','customer','offering',
                                                'communications','sales','management')),
    version_number          integer     NOT NULL,
    activity_score          numeric(4,2),
    documented_strategy     varchar(10),
    gap_analysis            text,
    value_driver_ratings    jsonb,
    confidence              numeric(4,3),
    source_type             varchar(20) NOT NULL
                            CHECK (source_type IN ('ai','reviewer','council')),
    source_id               uuid,       -- FK to skill_runs, council_decisions, or reviewer_actions
    created_by              uuid        NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at              timestamptz NOT NULL DEFAULT now()
);

-- version_number is unique per (audit_run_id, category_id)
CREATE UNIQUE INDEX uix_crv_run_cat_version
    ON category_result_versions (audit_run_id, category_id, version_number);

CREATE INDEX ix_crv_tenant_run_cat
    ON category_result_versions (tenant_id, audit_run_id, category_id);
```

---

## category_results: The "Current View"

`category_results` holds the latest score for each category — the "current" state that the reviewer queue and dashboard read from. It is updated **by application logic** (not a DB trigger) whenever a new `category_result_versions` row is inserted:

```
On AI skill completion:
  INSERT category_result_versions (version_number = 1, source_type = 'ai', ...)
  UPDATE category_results SET activity_score = ..., current_version = 1

On council adjustment:
  INSERT category_result_versions (version_number = 2, source_type = 'council', ...)
  UPDATE category_results SET activity_score = ..., current_version = 2

On reviewer edit:
  INSERT category_result_versions (version_number = 3, source_type = 'reviewer', ...)
  UPDATE category_results SET activity_score = ..., current_version = 3
```

Both the INSERT and the UPDATE happen within the **same EF Core transaction**. If either fails, both roll back — the ledger and the current view stay consistent.

**No DB trigger** is used because:
- Triggers are invisible to EF Core and complicate migration management
- Triggers cannot access application-level context (user ID, source_type, source_id) without session variables — adding complexity for no gain
- Application-layer logic is easier to test and reason about

---

## Published State: Immutability Enforcement

Once `audit_runs.status = 'published'`:

- `category_results` rows for that run are **read-only from the service layer's perspective**
- `category_result_versions` can receive no new rows (the scores are final)
- The enforcement is at the **service layer**, not a DB constraint

**Service layer check pattern (all write-path services):**

```csharp
var auditRun = await dbContext.AuditRuns.FindAsync(auditRunId, ct);
if (auditRun.Status == AuditRunStatus.Published)
    throw new AuditAlreadyPublishedException(auditRunId);
```

This check occurs at the start of every method in `IReviewerWorkflow` and `IPublisher` (guard clause pattern). The DB does not have a check constraint blocking UPDATEs to `category_results` after publish — the service layer is the enforcement boundary. Future hardening could add a DB-level trigger, but this is out of scope for Phase 1.

**Why not a DB constraint?** A CHECK constraint or trigger would require a join to `audit_runs`, which PostgreSQL check constraints do not support across tables. A trigger is possible but adds migration complexity (see above). Service layer enforcement is correct and sufficient given the design.

---

## version_number: Concurrency-Safe Incrementing

`version_number` is incremented **per `(audit_run_id, category_id)`** pair. It is not a global sequence.

**Implementation:** Computed in the application layer within the same transaction that inserts the version row:

```csharp
var nextVersion = await dbContext.CategoryResultVersions
    .Where(v => v.AuditRunId == auditRunId && v.CategoryId == categoryId)
    .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
nextVersion += 1;
```

**Concurrency safety:** The unique index `uix_crv_run_cat_version` on `(audit_run_id, category_id, version_number)` means that a concurrent insert with the same computed `version_number` will throw a `DbUpdateException` (unique constraint violation). The caller retries (optimistic concurrency — at most 2–3 retries expected; in practice, concurrent version writes for the same category are rare given the sequential workflow).

A PostgreSQL sequence per category would be cleaner but impractical (would require creating a sequence dynamically per category — a DDL operation not available to `sf_app`). The MAX+1 pattern with a unique index is the correct approach given role constraints.

---

## Audit Run Publish Preconditions

`Publisher.PublishAuditAsync` enforces these preconditions before writing:

1. `audit_runs.status` must be `'completed'` (not already `'published'` or `'failed'`)
2. All 6 `category_results` rows for the run must have `status = 'approved'`
3. No `category_results` row may have `status IN ('pending','flagged','council_review','rejected')`

If any precondition fails → `NotAllCategoriesApprovedException` → HTTP 409.

On success:
1. `audit_runs.status` → `'published'`
2. `audit_runs.published_at` → `NOW()`
3. `audit_runs.published_by` → `publishedByUserId`
4. `audit_runs.composite_score` → SUM of 6 `activity_score` values
5. `audit_runs.tier` → derived from composite score mapping
6. HubSpot outbound event enqueued to `Channel<HubSpotEvent>`
7. `telemetry_events.completed_at` → `NOW()`
