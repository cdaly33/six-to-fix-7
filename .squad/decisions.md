# Squad Decisions

## Phase 0 Architectural Decisions

### ADR-001: Dependency Injection Wiring & Service Lifetimes
**Status:** Accepted | **Author:** Morpheus | **Date:** 2026-05-10

All domain services and cross-cutting infrastructure are registered in ASP.NET Core's built-in DI container with carefully chosen lifetimes respecting multi-tenancy, pgBouncer transaction-mode pooling, and Blazor Server circuit persistence. Domain services are Scoped; stateless services like PolicyEngine are Singleton; HTTP clients from IHttpClientFactory are Transient with Polly pipelines attached. Tenant context flows from JWT claim → ClaimsPrincipal → IHttpContextAccessor → HttpTenantContext → SixToFixDbContext global query filters. DbContext pooling is NOT used—it is incompatible with per-request query filters capturing instance state.

**Key decisions:**
- 8 domain services: Scoped
- PolicyEngine, auth pipelines, circuit breakers: Singleton
- IHttpClientFactory-based clients (AI, Blob, Search, HubSpot): Transient + Polly
- DbContext: Scoped with explicit tenant context injection
- Registration centralized in Program.cs via extension methods
- All Scoped services safely accessible from Singleton (one-way dependency)

---

### ADR-002: Multi-Tenant Data Isolation Pattern
**Status:** Accepted | **Author:** Morpheus | **Date:** 2026-05-10

Tenant isolation is enforced exclusively via EF Core's `HasQueryFilter` mechanism in `SixToFixDbContext`. All 13 tenant-scoped entities (clients, audits, audit_runs, skill_runs, category_payloads, category_result_versions, policy_flags, council_decisions, reviewer_actions, reviewer_rejections, calibration_deltas, hubspot_sync_log, telemetry_daily_snapshots) have global query filters applied at model configuration. The tenant_id is captured from the Scoped ITenantContext at DbContext construction and embedded in filter expressions—this is safe because DbContext is Scoped. Migration tooling uses a separate MigrationDbContext that does NOT apply filters. SuperAdmin endpoints use SuperAdminDbContext without filters or call IgnoreQueryFilters() within authorized code paths. Query filters are the single authoritative enforcement point—service code does NOT add additional `.Where(e => e.TenantId == ...)` clauses.

**Key decisions:**
- Global query filters: single enforcement point, cannot be forgotten
- 13 entities filtered; 2 platform-level entities (tenants, users) not filtered
- Filter expression captures tenantId from Scoped ITenantContext at construction
- MigrationDbContext pattern for DDL tooling without tenant context
- IgnoreQueryFilters() forbidden outside SuperAdminDbContext or MigrationDbContext

---

### ADR-003: AI Council State Machine & Skill Chain Interaction Pattern
**Status:** Accepted | **Author:** Morpheus | **Date:** 2026-05-10

The audit pipeline has three subsystems (Skill Chain, Policy Engine, AI Council) that interact in a strictly defined sequence. AuditOrchestrator is the sole coordinator, calling SkillRunner sequentially for each of 5 skills. After each skill succeeds, PolicyEngine evaluates the output: Trigger-severity flags immediately escalate to CouncilRunner synchronously (not asynchronously queued). Warning-severity flags are informational only. If any skill fails (timeout, schema validation, circuit open, API error), the AuditRun is marked failed and the chain aborts. The AuditRun does NOT transition to completed until all skills succeed AND any triggered categories receive CouncilDecisions. All events (skill-started, skill-completed, council-started, council-completed, run-completed, run-failed) are broadcast via IHubContext<AuditRunHub> to the signalR group for the auditRunId.

**Key decisions:**
- Sequential skill execution with PolicyEngine evaluation per-skill
- Council escalation synchronous within same run (not queued)
- One AuditRun state machine: created → running → [completed | failed]
- Skill failure = immediate abort, no partial continuation
- SignalR event sequence includes skill index, durations, policy flags, decision types

---

### ADR-004: SignalR Event Contract & Concurrent Audit Ordering
**Status:** Accepted | **Author:** Morpheus | **Date:** 2026-05-10

Each audit run is isolated in its own SignalR group keyed by auditRunId. Clients authenticate with JWT bearer token (passed as query string `access_token` for WebSocket). JoinRun(auditRunId) validates tenant ownership before adding the connection to the group. Events are sent to the group via IHubContext<AuditRunHub> and are ordered—SignalR guarantees in-order delivery on a single connection within one publisher. The hub is registered at `/hubs/audit-run` and requires [Authorize]. Blazor Server reconnection is handled by the component via OnAfterRenderAsync: client fetches current state via REST API to resync missed events (SignalR does not replay buffered events for disconnected clients). Sticky sessions in Azure App Service ensure a circuit always hits the same node, preserving circuit state.

**Key decisions:**
- Hub at /hubs/audit-run with JWT bearer auth (token from query string)
- JoinRun(auditRunId) validates tenant ownership, silently rejects if unauthorized
- SignalR groups keyed by auditRunId
- 7 event types: skill-started, skill-completed, skill-failed, council-started, council-completed, run-completed, run-failed
- Message ordering guaranteed for single-node; Azure SignalR Service + sticky sessions required for scale-out

---

### ADR-005: Policy Engine Extensibility Contract
**Status:** Accepted | **Author:** Morpheus | **Date:** 2026-05-10

The Policy Engine is Singleton and pure-functional (stateless, no DB calls, no per-tenant state). Rules are discovered and registered explicitly in DI—not via reflection. The IPolicyRule interface has one method: `Evaluate(CategoryPayload, AuditContext) → PolicyFlag?`. PolicyEngine receives IEnumerable<IPolicyRule> via constructor injection and runs all rules in parallel (Parallel.ForEach) with isolated error handling—a broken rule is logged at ERROR but does NOT abort the audit. PolicyFlags from all rules are returned as IReadOnlyList<PolicyFlag>. Input contracts: CategoryPayload (category output from skill with score, strategy, confidence, evidence, narrative), AuditContext (cross-category context with tenant, run ID, benchmark medians, std devs). Output contract: PolicyFlag (ruleName, severity Warning | Trigger, categoryId, detail, evaluatedAt). The five current rules with exact conditions are: LOW_CONFIDENCE (score < 0.6 → Trigger), MISSING_EVIDENCE (0 items → Warning), BENCHMARK_OUTLIER (>2σ from median → Trigger), INSUFFICIENT_EVIDENCE (1 item → Warning), SCORE_STRATEGY_MISMATCH (score > 7.0 + strategy == "none" → Trigger).

**Key decisions:**
- IPolicyRule interface: explicit registration in DI, never via reflection
- Parallel execution with isolated error handling
- Singleton stateless design
- All PolicyFlags persisted to policy_flags table (including Warning-level)
- Five rules: LOW_CONFIDENCE, MISSING_EVIDENCE, BENCHMARK_OUTLIER, INSUFFICIENT_EVIDENCE, SCORE_STRATEGY_MISMATCH with locked thresholds

---

### ADR-006: Reviewer Lockout Transaction Boundaries
**Status:** Accepted | **Author:** Morpheus | **Date:** 2026-05-10

The business rule "3 rejections of same category within 24 hours → HTTP 409 REVIEWER_REJECTION_LOCKOUT" is enforced via a serializable transaction with PostgreSQL advisory lock. ReviewerWorkflow.RejectAsync opens a transaction, acquires `pg_advisory_xact_lock` keyed to (audit_run_id, category_id), counts rejections in the rolling 24-hour window, and either inserts a new rejection or returns 409. The advisory lock is transaction-scoped (pg_advisory_xact_lock), compatible with pgBouncer transaction-mode pooling. The 24-hour window is rolling from now() at query time—no explicit expiry record or scheduled job needed. When lockout is triggered (count >= 3), a CalibrationDelta is NOT created (the rejection was blocked). Only successful score overrides (approve-with-edit, edit) create CalibrationDeltas. The lockout scope is (tenant_id, audit_run_id, category_id), so a different audit run for the same category resets the window. Different reviewers can still approve, edit, or escalate when a lockout is active—the lockout blocks only rejections.

**Key decisions:**
- Serializable isolation + pg_advisory_xact_lock(hashtext(...))
- Scope: (tenant_id, audit_run_id, category_id)
- Rolling 24-hour window from now()
- Lockout expiry automatic; no cron job
- No CalibrationDelta on blocked rejection

---

### ADR-007: Multi-Tenant EF Core Query Filter Pattern
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-10

EF Core Global Query Filters are the primary enforcement mechanism for tenant isolation. Every tenant-scoped entity is decorated with `HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)` in OnModelCreating. ITenantContext is injected into AppDbContext as Scoped. When TenantContext.TenantId is null (background workers, migrations), the filter expression guards with `_tenantContext.TenantId == null || ar.TenantId == _tenantContext.TenantId` to allow unrestricted access. Background workers are responsible for adding explicit WHERE conditions for data isolation. sf_app role permits SELECT, INSERT, UPDATE (except category_result_versions which is append-only), no DELETE, no DDL. All tenant-scoped tables have composite indexes starting with tenant_id as leading column for efficient B-tree scans.

**Key decisions:**
- Global query filters via HasQueryFilter(...)
- Scoped ITenantContext injected into Scoped DbContext
- Null guard in filter expression for background workers
- sf_app: no DELETE, no category_result_versions UPDATE
- Composite indexes: (tenant_id, id) + domain-specific indexes

---

### ADR-008: Reviewer Lockout State Machine
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-10

The lockout check counts reject actions (action_type='reject') by the same reviewer within the same audit run, same category, past 24 hours. Lockout scope is (tenant_id, audit_run_id, category_id, reviewer_id)—per-reviewer, allowing another reviewer to act. The check uses READ COMMITTED isolation with optimistic retry (not SERIALIZABLE, which would serialize all reviewer actions). A race condition that adds a phantom 4th rejection is functionally harmless and extremely unlikely (24-hour window, threshold of 3). The 24-hour window is rolling from now() at query time. No cron job, no expiry table—expired rejections fall out of the window automatically on next query. CalibrationDelta sequencing: for Edit, lockout check runs FIRST, then CalibrationDelta is inserted BEFORE updating category_results. For Reject, no CalibrationDelta is created (rejection does not modify scores). HTTP 409 response includes lockoutExpiresAt computed as oldest-rejection + 24h to inform client of expiry time.

**Key decisions:**
- Lockout scope: (tenant_id, audit_run_id, category_id, reviewer_id)
- READ COMMITTED + optimistic retry (not SERIALIZABLE)
- Rolling 24-hour window from now()
- No CalibrationDelta on blocked rejection
- lockoutExpiresAt: oldest-rejection + 24h

---

### ADR-009: Immutable Publish Semantics
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-10

category_result_versions is append-only. INSERT only—never UPDATE or DELETE. Enforcement at three layers: (1) sf_app role has UPDATE/DELETE revoked, (2) service code calls only .Add(), never .Update()/.Remove(), (3) EF Core entity configured IsReadOnly. A new version row is inserted exactly three times per category per run: (1) AI skill completion (source_type='ai', version=1), (2) AI Council adjustment if decision_type='adjusted' (source_type='council', version=2), (3) Reviewer edit (source_type='reviewer', version=3). category_results holds the current view (latest scores). Both tables updated within same EF Core transaction—if either fails, both roll back. version_number is unique per (audit_run_id, category_id) and computed with MAX+1 + unique index guard (optimistic concurrency). Once audit_runs.status='published', scores are immutable—service layer guards every write-path method with check "if status==published throw AuditAlreadyPublishedException". Publish preconditions: all 6 categories status='approved'. On publish: status→published, published_at→now(), published_by→userId, composite_score→sum(6 scores), tier→derived, HubSpot event enqueued, telemetry→now().

**Key decisions:**
- Append-only: INSERT only, defense-in-depth (role, service, EF Core)
- Three version rows per category: ai, council (if adjusted), reviewer (if edit)
- category_results: current view, both updated in same tx
- version_number: MAX+1 with unique index guard for concurrency
- Published immutable: service-layer guard, not DB constraint
- Publish preconditions: all 6 approved

---

### ADR-010: Polly Resilience Pipeline Configuration — Locked Values
**Status:** Accepted | **Author:** Oracle | **Date:** 2026-05-10

Every Azure OpenAI Service call passes through a Polly resilience pipeline composed of three policies in order: Timeout (outermost) → Retry → Circuit Breaker (innermost). These values are final architectural commitments. Timeout: 60s pessimistic (cancels task immediately, not cooperative)—wraps entire retry sequence, not per-attempt. On timeout: TimeoutRejectedException → SkillRun.status='failed', failure_reason='AI_TIMEOUT'. Retry: 3 total attempts (initial + 2 retries) with exponential backoff (2s base, 2× multiplier, ±20% jitter). On max retries: MaxRetryAttemptsExceededException → failure_reason='MAX_RETRIES_EXCEEDED'. Retries trigger on HttpRequestException, HTTP 429, HTTP 5xx. NO retry on HTTP 400, 422, TimeoutRejectedException, BrokenCircuitException, or schema validation failure. Circuit Breaker: AdvancedCircuitBreaker (ratio-based), 50% failure ratio threshold, 60s sampling window, 3-call minimum throughput, 60s break duration, 1 half-open probe. On circuit open: BrokenCircuitException → failure_reason='CIRCUIT_OPEN'. Schema validation (post-HTTP-200) is app-layer concern—does not retry, does not count toward circuit breaker.

**Key decisions:**
- Timeout: 60s pessimistic, wraps all retries
- Retry: 3 attempts, exponential backoff 2s base, 2× multiplier, ±20% jitter
- Retry on: HttpRequestException, 429, 5xx
- No retry on: 400, 422, timeout, circuit open, schema validation
- Circuit Breaker: 50% ratio, 60s window, 3-call min, 60s break, 1 probe
- Schema validation: app-layer, non-retryable

---

### ADR-011: Correlation ID & Logging Strategy
**Status:** Accepted | **Author:** Oracle | **Date:** 2026-05-10

Correlation ID propagates cross-cutting from request entry through all downstream operations. CorrelationIdMiddleware reads X-Correlation-ID header (or generates Guid if absent), stores in HttpContext.Items, sets response header. Logger scope includes CorrelationId so all log entries within scope carry it automatically. Background workers generate new Guid per event. Dual correlation: X-Correlation-ID (app-level, human-readable) + X-Trace-ID (W3C Trace Context / App Insights operation ID). All logs must be structured (message templates + parameters)—NO string interpolation. Log levels: Trace (dev only), Debug (diagnostic), Information (normal events), Warning (non-fatal anomalies), Error (failures), Critical (system-wide). Prohibited in logs: user names, email, company names, AI narrative, raw prompt, raw response, document content, HubSpot webhook body—use IDs only. Application Insights: Warning+ by default; Information for audit-critical events (skill-started, skill-completed, council-*). SkillRun.raw_ai_response stored in DB (not logs) for debugging with database-level access control.

**Key decisions:**
- X-Correlation-ID header propagation + generation if absent
- Logger scope includes CorrelationId for automatic context
- Structured logging only (templates + parameters)
- Log levels: Trace (dev), Debug (diagnostic), Information, Warning, Error, Critical
- NO PII in logs: user names, email, company names, narrative, prompts, responses, content
- Dual correlation: X-Correlation-ID + X-Trace-ID
- Application Insights: Warning+ default, Information for audit events

---

### ADR-012: Git Workflow — Dev Branches Only, Merge to Main via PR
**Status:** Accepted | **Author:** Chris (via Copilot) | **Date:** 2026-05-10

All implementation work must land on named development branches and reach `main` only through pull requests. Direct commits or pushes to `main` are prohibited. Phase branches use the `dev/phase-{N}-{slug}` convention, and contributors are expected to make regular, reviewable commits while work is in progress.

**Key decisions:**
- No direct commits or pushes to `main`
- All work happens on dev or feature branches
- Merge to `main` only through pull requests
- Phase branch naming convention: `dev/phase-{N}-{slug}`
- This directive applies to all agents and future phases

**Resolved inbox references:**
- `.squad/decisions/inbox/copilot-directive-git-workflow.md` — adopted by ADR-012
- `.squad/decisions/inbox/neo-phase1-schema.md` — Phase 1 data-layer decisions recorded and implemented
- `.squad/decisions/inbox/tank-phase1-infra.md` — Phase 1 infra, workflow, and test decisions recorded and implemented

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
