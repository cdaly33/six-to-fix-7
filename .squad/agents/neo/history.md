# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains (Brand, Customer, Offering, Communications, Sales, Management)
- **Stack:** .NET 10 LTS, ASP.NET Core, EF Core, Azure PostgreSQL Flexible Server (v16, pgBouncer on port 6432), ASP.NET Core Identity + JWT, Azure OpenAI Service (via IAIClient interface), Azure Blob Storage, Azure AI Search, Azure App Service, Azure Key Vault (managed identity)
- **Auth decision:** ASP.NET Core Identity + JWT — app issues JWTs with `tenant_id`, `tenant_slug`, `roles` claims. No OIDC server.
- **Database:** 15 tables, strict `tenant_id` FK isolation on all tenant-scoped tables. Append-only `category_result_versions` ledger. Two DB roles: `sf_admin` (DDL+DML, migrations), `sf_app` (DML only, runtime). pgBouncer on 6432.
- **Service layer (8 services):** AuditOrchestrator, SkillRunner, PolicyEngine (Singleton), CouncilRunner, ReviewerWorkflow, Publisher, CalibrationTracker, TelemetryCollector
- **Reviewer lockout:** 3 rejections of same category within 24h → HTTP 409 REVIEWER_REJECTION_LOCKOUT
- **Publishing:** Immutable, versioned. All 6 categories must be approved before publishing.
- **CalibrationDelta:** Created on every reviewer score override. Never skipped.
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-10 — Phase 0: Schema, Contracts, State Machines

**15-Table Schema — Key Decisions**
- `tenants` is the only shared (non-tenant-scoped) table. All 14 others carry `tenant_id NOT NULL FK → tenants.id`.
- Exception: `hubspot_sync_events.tenant_id` is nullable to support inbound HubSpot events that arrive before a tenant can be matched. EF Core filter: `tenant_id IS NULL OR tenant_id = :tenantId`.
- `category_result_versions` is APPEND-ONLY. The `sf_app` runtime role has `UPDATE` and `DELETE` REVOKED on this table at the DB level.
- `category_results` holds the "current" view (latest scores). It is updated by application logic within the same transaction that inserts a `category_result_versions` row — no triggers.
- `reviewer_actions.action_type` covers five values: `approve`, `reject`, `edit`, `rerun`, `escalate`.
- `calibration_deltas` records every reviewer score override — requires non-empty `override_reason_code` and `notes` (enforced at service layer).
- `documents.size_bytes` CHECK constraint enforces the 10MB upload limit at DB layer as defense-in-depth.
- `telemetry_events` has a UNIQUE index on `audit_run_id` — exactly one row per audit run.
- Primary composite indexes: `(tenant_id, id)` on every tenant-scoped table. Leading column is `tenant_id` for PostgreSQL index selectivity.

**Service Interface Contract Patterns**
- 8 services registered via interface. PolicyEngine is Singleton (stateless). All others are Scoped (DB-touching, per-request tenant context).
- Every external dependency injected via interface: `IAIClient`, `IBlobStorage`, `ISearchClient`, `IHubSpotClient`. No concrete instantiation inside services.
- Every service method accepts `CancellationToken`.
- All exceptions map to HTTP status codes: `NotFoundException` → 404, `ConflictException` → 409, `ValidationException` → 422, AI failures → 502/503/504.
- `ISearchClient.SearchAsync` requires `tenantId` as a mandatory parameter — the implementation enforces tenant scoping at the search layer.
- `ITelemetryCollector.GetDailyMetricsAsync` bypasses the global tenant query filter (SuperAdmin/OpsViewer cross-tenant operation).

**Lockout State Machine Decisions**
- Lockout scope: per `(tenant_id, audit_run_id, category_id, reviewer_id)` — per reviewer, not per category globally. A different reviewer can always act.
- 24-hour window: rolling from `NOW()`. No cron, no expiry table — the window is evaluated at query time.
- Transaction isolation: `READ COMMITTED` with optimistic retry. SERIALIZABLE rejected as unnecessarily contending.
- CalibrationDelta sequence for Edit: lockout check first → validate inputs → open transaction → insert CalibrationDelta → update category_results → insert category_result_versions → insert reviewer_actions → commit.
- HTTP 409 body: RFC 7807 format with `code: "REVIEWER_REJECTION_LOCKOUT"`. No PII in response body.

**Immutable Publish Semantics Decisions**
- Three triggers for new `category_result_versions` row: AI skill completion (`source_type='ai'`), council adjustment (`source_type='council'`, only if `decision_type='adjusted'`), reviewer edit (`source_type='reviewer'`).
- `version_number` is per `(audit_run_id, category_id)`, implemented as MAX+1 within the insert transaction. Unique constraint `(audit_run_id, category_id, version_number)` provides optimistic concurrency protection.
- Published state enforcement: service layer guard clause (`if status == Published → throw`). Not a DB constraint (cross-table CHECK not supported in PostgreSQL).
- Publish preconditions: all 6 `category_results.status = 'approved'`, `audit_runs.status = 'completed'`.
- No DB triggers anywhere — all consistency maintained by application-layer logic within EF Core transactions.
