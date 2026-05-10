# Decisions Log

## Phase 0 — Infrastructure & Architecture Decisions (Sealed)

### 2026-05-10: HubSpot auth model confirmed — Private App token

**By:** Chris (human)  
**What:** HubSpot integration uses Private App token (Bearer token) for outbound API calls. NOT OAuth 2.0. Token stored in Key Vault as `sf-hubspot-private-app-token`. Inbound webhook HMAC-SHA256 validation unchanged — uses `sf-hubspot-webhook-secret`.  
**Why:** Chris answered Q1 from Phase 0 open questions.

---

### 2026-05-10: JWT role claim string confirmed — Reviewer (not Auditor)

**By:** Chris (human)  
**What:** The four canonical JWT role claim strings are: SuperAdmin, TenantAdmin, Reviewer, Viewer. "Auditor" is NOT a valid role name in this system. All architecture documents updated to eliminate Auditor as a role reference. Case-sensitive — must match exactly in JWT issuance and <AuthorizeView> checks.  
**Why:** Chris answered Q12 from Phase 0 open questions. Trinity had flagged the discrepancy.

---

### 2026-05-10: Azure OpenAI subscription confirmed

**By:** Chris (human)  
**What:** Azure OpenAI resource is in rg-StrategicGlue-CommandCenter — same subscription as App Service. DefaultAzureCredential with system-assigned managed identity works. No cross-subscription complexity.  
**Why:** Chris answered Q2 from Phase 0 open questions.

---

### 2026-05-10: Chris infrastructure decisions (Phase 0 resolution)

**By:** Chris (human)  
**Q3 — Blob storage prod KV:** sf-blob-storage-connstr exists in prod Key Vault (not local-dev only)  
**Q4 — Custom domain:** app.strategicglue.com confirmed. SSL: App Service Managed Certificate.  
**Q5 — DB migrations:** Option (b) — MigrateAsync() on startup with sf_admin credentials via GitHub Actions secret.  
**Q6 — E2E target:** Dev environment (app-strategicglue-dev). Single environment for now.  
**Q7 — RBAC scope:** User Access Administrator at resource group scope approved.  
**Q8 — Notifications:** GitHub Step Summary only. No Slack webhook.  
**Q9 — AI Search:** Search Index Data Contributor required (app writes to index).  
**Q10 — API test isolation:** Shared Testcontainer + per-test IDbContextTransaction rollback confirmed.  
**Why:** Chris answered all open questions from Phase 0 artifacts.

---

## Architectural Decisions

### 2026-05-10: AI Council State Machine & Skill Chain Interaction Pattern

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  

#### Decision Summary

The StrategicGlue Six-to-Fix audit pipeline has three automated subsystems that interact in sequence:

1. **Skill Chain** — five sequential AI skills
2. **Policy Engine** — five stateless rules that evaluate skill outputs
3. **AI Council** — three AI personas that deliberate on triggered categories

#### Key Decisions

**1. Skill Sequencing via AuditOrchestrator + SkillRunner**

`AuditOrchestrator` is the single coordinator for an audit run. It calls `SkillRunner.ExecuteAsync(skill, auditRunId)` for each of the five skills **sequentially** — the output of skill N is available as input context for skill N+1.

**2. PolicyEngine Evaluation: Per-Skill, After Each Skill Completes**

PolicyEngine evaluates **after each individual skill completes**, not post-chain. Trigger flags on a skill's output must be known before the next skill runs. Warnings visible during run provide real-time context to the SignalR-watching auditor.

**3. Council Escalation: Synchronous Within the Audit Run**

When PolicyEngine produces one or more `Trigger`-severity flags, `AuditOrchestrator` calls `CouncilRunner.RunAsync(...)` **synchronously within the same audit run execution** — it does not queue the council deliberation for later. The audit run does not transition to `completed` until all triggered categories have received a `CouncilDecision`.

**4. Skill Failure Abort Logic**

All failure kinds (SchemaValidation, Timeout, CircuitOpen, ApiError post-Polly exhaustion) result in **immediate abort**. `AuditRun` transitions to `failed`. No partial-success continuation.

**5. SignalR Event Sequence**

Events are sent in order: skill-started, skill-completed, (council-started/council-completed if triggered), then next skill or run-completed at end.

---

### 2026-05-10: Dependency Injection Wiring & Service Lifetimes

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  

#### Decision Summary

All eight domain services and all cross-cutting services are registered with lifetimes as specified below. The registration code lives exclusively in `Program.cs`.

#### DI Lifetime Table

| Service / Interface | Concrete Type | Lifetime | Justification |
|---|---|---|---|
| `IAuditOrchestrator` | `AuditOrchestrator` | **Scoped** | Coordinates a single audit run. Depends on `DbContext`, tenant context. |
| `ISkillRunner` | `SkillRunner` | **Scoped** | Executes per-run AI calls. Depends on `IAIClient`, `DbContext`. |
| `IPolicyEngine` | `PolicyEngine` | **Singleton** | Pure stateless functions. No DB calls. No per-tenant state. |
| `ICouncilRunner` | `CouncilRunner` | **Scoped** | Invokes AI personas. Depends on `IAIClient`, `DbContext`. |
| `IReviewerWorkflow` | `ReviewerWorkflow` | **Scoped** | Enforces reviewer actions and lockout rule. Requires `DbContext` and tenant context. |
| `IPublisher` | `Publisher` | **Scoped** | Assembles and persists immutable published audit. Requires `DbContext`. |
| `ICalibrationTracker` | `CalibrationTracker` | **Scoped** | Logs `CalibrationDelta` on every score override. Requires `DbContext`. |
| `ITelemetryCollector` | `TelemetryCollector` | **Scoped** | Records daily run metrics. Requires `DbContext`. |
| `IAIClient` | `AzureOpenAIClient` | **Transient** (from factory) | `HttpClient`-backed. Polly pipeline attached. |
| `IBlobStorage` | `AzureBlobStorageClient` | **Transient** (from factory) | Same pattern as `IAIClient`. |
| `ISearchClient` | `AzureSearchClient` | **Transient** (from factory) | Same pattern. |
| `IHubSpotClient` | `HubSpotClient` | **Transient** (from factory) | Same pattern. HMAC secret loaded from Key Vault at startup. |
| `SixToFixDbContext` | `SixToFixDbContext` | **Scoped** | EF Core `DbContext`. One per request/circuit. |

---

### 2026-05-10: Policy Engine Extensibility Contract

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  

#### Decision Summary

The Policy Engine is designed to accommodate new rules without modifying existing code (Open/Closed Principle).

#### Rule Extension Pattern

Rules are registered **explicitly in DI** — not via reflection scanning. Adding a new rule requires:
1. Implement `IPolicyRule` in `Domain/PolicyEngine/Rules/`
2. Add `services.AddSingleton<IPolicyRule, NewRule>()` in `AddDomainServices()`
3. No changes to `PolicyEngine` itself

#### The Five Current Rules: Exact Conditions

| Rule | Severity | Condition |
|---|---|---|
| `LOW_CONFIDENCE` | **Trigger** | `ConfidenceScore < 0.6` |
| `MISSING_EVIDENCE` | **Warning** | `Evidence.Count == 0` |
| `BENCHMARK_OUTLIER` | **Trigger** | `Abs(ActivityScore - median) > (2 * stdDev)` |
| `INSUFFICIENT_EVIDENCE` | **Warning** | `Evidence.Count > 0 && Evidence.Count < 2` |
| `SCORE_STRATEGY_MISMATCH` | **Trigger** | `ActivityScore > 7.0m && DocumentedStrategy == "none"` |

---

### 2026-05-10: Reviewer Lockout Transaction Boundaries

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  

#### Decision Summary

**3 rejections of the same category result by any reviewer within a 24-hour window → lock out further rejections and return HTTP 409 `REVIEWER_REJECTION_LOCKOUT`.**

#### Transaction Boundary: Serializable + Row-Level Lock

The rejection-count-check and rejection-record-insert are performed **in a single serializable transaction** using PostgreSQL advisory locks (`pg_try_advisory_xact_lock`).

**Implementation:**
1. Acquire advisory lock keyed to `(audit_run_id, category_id)`
2. Count rejections in the rolling 24-hour window
3. If count >= 3: ROLLBACK and return 409
4. If count < 3: INSERT new rejection record, COMMIT

#### Lockout Scope

The lockout is scoped to `(tenant_id, audit_run_id, category_id)` — per audit run, per category. A new audit run for the same category resets the window.

#### 24-Hour Window: Rolling

The 24-hour window **rolls** — computed as `now() - 24 hours` at the time of each rejection check. It does NOT reset from the most recent rejection.

---

### 2026-05-10: SignalR Event Contract & Concurrent Audit Ordering

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  

#### Decision Summary

SignalR events are ordered within a run, isolated between runs, tenant-safe, and resilient to Blazor Server reconnects.

#### Hub Endpoint

```
/hubs/audit-run
```

Requires JWT bearer token as query string `access_token` (WebSocket standard).

#### Group Key: auditRunId

Each connected client joins a SignalR group keyed by `auditRunId` (UUID string). All clients watching the same audit run receive the same events.

#### Event Contract

1. **skill-started** — before calling `SkillRunner.ExecuteAsync`
2. **skill-completed** — after `SkillRunner` returns `Status = Succeeded` AND PolicyEngine evaluation is complete
3. **skill-failed** — after `SkillRunner.ExecuteAsync` returns `Status = Failed`
4. **council-started** — before calling `CouncilRunner.RunAsync`
5. **council-completed** — after `CouncilRunner.RunAsync` returns a decision
6. **run-completed** — after all 5 skills succeed
7. **run-failed** — after skill failure causes abort

#### Message Ordering Guarantee

SignalR over WebSocket delivers messages in order on a single connection. Events are sent sequentially from `AuditOrchestrator` (which is inherently sequential).

---

### 2026-05-10: Multi-Tenant Data Isolation Pattern

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  

#### Decision Summary

Tenant isolation is enforced exclusively via EF Core's `HasQueryFilter` mechanism. Every entity type that is tenant-scoped has a query filter applied at model configuration time.

#### Enforcement Mechanism: EF Core Global Query Filters

This is the **single, authoritative** enforcement point. Application service code does not add additional `.Where(e => e.TenantId == ...)` clauses.

**Entities covered by global query filters (all 13 tenant-scoped tables):**
```
clients, audits, audit_runs, skill_runs, category_payloads,
category_result_versions, policy_flags, council_decisions,
reviewer_actions, reviewer_rejections, calibration_deltas,
hubspot_sync_log, telemetry_daily_snapshots
```

#### Filter Configuration Pattern

```csharp
protected override void OnModelCreating(ModelBuilder mb)
{
    var tenantId = _tenantContext.TenantId;
    mb.Entity<Client>().HasQueryFilter(e => e.TenantId == tenantId);
    // ... all 13 tenant-scoped tables
}
```

#### Middleware → Claim Extraction → Service Registration → EF Core Filter Chain

```
HTTP Request
  ▼
[UseAuthentication] — JWT bearer middleware validates token, populates HttpContext.User
  ▼
[UseAuthorization] — Enforces [Authorize] attributes
  ▼
[TenantResolutionMiddleware] — Reads tenant_id claim, validates, stores in HttpContext.Items
  ▼
[Endpoint] — Injects ITenantContext (Scoped)
  ▼
[HttpTenantContext] — Reads TenantId from HttpContext.Items
  ▼
[SixToFixDbContext] — Global query filters applied with captured tenant_id
```

---

### 2026-05-10: Immutable Publish Semantics

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Neo (Backend Dev)  

#### Decision Summary

`category_result_versions` is an append-only ledger. Once `audit_runs.status = 'published'`, the audit is immutable — no score modifications are permitted.

#### Append-Only Enforcement

`category_result_versions` receives **INSERT only — never UPDATE or DELETE**.

**Enforcement layers (defense-in-depth):**
1. **`sf_app` role:** `UPDATE` and `DELETE` privileges on `category_result_versions` are explicitly `REVOKE`d.
2. **Service layer:** `IPublisher` and `IReviewerWorkflow` only call `.Add()` — never `.Update()` or `.Remove()`.
3. **EF Core configuration:** `CategoryResultVersion` entity is configured as `IsReadOnly`.

#### What Triggers a New Version Row

| Event | `source_type` | Notes |
|-------|:---:|---|
| AI skill chain completes scoring | `'ai'` | First version; `version_number = 1` |
| AI Council adjusts a score | `'council'` | Only if `decision_type = 'adjusted'` |
| Reviewer overrides a score (Edit action) | `'reviewer'` | Only on Edit — Approve/Reject/Rerun do not change scores |

#### Published State: Immutability Enforcement

Once `audit_runs.status = 'published'`:
- `category_results` rows for that run are **read-only from the service layer's perspective**
- `category_result_versions` can receive no new rows
- Enforcement is at the **service layer** (guard clause pattern in all write-path services)

#### Audit Run Publish Preconditions

`Publisher.PublishAuditAsync` enforces these preconditions before writing:
1. `audit_runs.status` must be `'completed'`
2. All 6 `category_results` rows for the run must have `status = 'approved'`
3. No `category_results` row may have `status IN ('pending','flagged','council_review','rejected')`

---

### 2026-05-10: Multi-Tenant EF Core Query Filter Pattern (Neo's Ratification)

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Neo (Backend Dev)  

#### Decision Summary

Use **EF Core Global Query Filters** to enforce tenant isolation on all tenant-scoped entities.

#### EF Core Approach

Global Query Filters are applied in `OnModelCreating` via `modelBuilder.Entity<T>().HasQueryFilter(...)`:

```csharp
modelBuilder.Entity<Client>()
    .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);
```

The filter expression is evaluated at query time — EF Core injects it as a `WHERE tenant_id = @tenantId` clause into every `SELECT`, `UPDATE`, and `DELETE` generated for that entity.

#### DbContext Lifetime

**Scoped** — required for HTTP-request-scoped tenant resolution. A new `AppDbContext` instance is created per HTTP request (or per Blazor circuit).

#### Null Tenant Context: Background Workers and Migrations

Guard clause in filter expression:

```csharp
modelBuilder.Entity<AuditRun>()
    .HasQueryFilter(ar =>
        _tenantContext.TenantId == null
        || ar.TenantId == _tenantContext.TenantId);
```

---

### 2026-05-10: Reviewer Lockout State Machine (Neo's State Flow)

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Neo (Backend Dev)  

#### Decision Summary

Enforce the reviewer lockout rule (3 rejections of same category within 24 hours → HTTP 409) via a query-time COUNT check.

#### Lockout Check Query

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

If `rejection_count >= 3` → HTTP 409 with code `REVIEWER_REJECTION_LOCKOUT`.

#### Lockout Scope

Scoped to `(tenant_id, audit_run_id, category_id, reviewer_id)` — per reviewer, per audit run, per category.

#### Atomicity: Check + Insert Transaction

**Isolation Level:** `READ COMMITTED` with **optimistic retry** (not SERIALIZABLE).

The sequence is wrapped in a single EF Core transaction.

#### 24-Hour Window

**Rolling** from `NOW()` at the time of the lockout check.

#### Lockout Expiry

Automatic — the 24-hour window is evaluated at query time on every action. Expired rejections fall out of the window automatically.

---

### 2026-05-10: Correlation ID & Logging Strategy

**Status:** Locked — Architectural Commitment  
**Date:** 2026-05-10  
**Owner:** Oracle (AI & Integration Dev)  

#### Summary

This document defines the cross-cutting correlation ID propagation strategy and structured logging conventions. These rules apply to all components.

**Non-negotiable constraint:** No PII in any log payload, ever.

#### Correlation ID Propagation

**Generation:**
- Per HTTP request: If incoming request includes valid `X-Correlation-ID` header GUID, propagate that value.
- If header absent or invalid: Generate a new `Guid.NewGuid()`.
- Response header: Every HTTP response must include `X-Correlation-ID`.

**Middleware Implementation:**
A dedicated `CorrelationIdMiddleware` is registered early in the ASP.NET Core pipeline. It:
1. Reads `X-Correlation-ID` from request header
2. Generates a GUID if absent
3. Stores correlation ID in `IHttpContextAccessor.HttpContext.Items["CorrelationId"]`
4. Sets it on response header
5. Begins an `ILogger` scope — correlation ID included in all log entries produced within the scope

**Background Workers:**
Generate their own correlation ID per dequeued event via `Guid.NewGuid()`. Set via `ILogger.BeginScope`.

#### Log Levels

| Level | When to Use |
|---|---|
| `Trace` | Highly verbose, development only |
| `Debug` | Diagnostic details not needed in production |
| `Information` | Normal operational events |
| `Warning` | Non-fatal anomalies requiring attention |
| `Error` | Failures requiring investigation |
| `Critical` | System-wide failures |

#### Structured Logging Requirements

**Format: Structured Parameters Only**

All log calls **must** use structured logging message templates. String interpolation is **prohibited**.

**Correct:**
```csharp
logger.LogInformation("Skill {SkillName} completed for {AuditRunId}",
    skillName, auditRunId);
```

**Required Context in Every AI Call Log:**
- `CorrelationId`
- `AuditRunId`
- `SkillName`
- `TenantId`

#### Prohibited Content in Log Payloads

The following are **never logged**:
- User display names | PII
- Email addresses | PII
- Company names | Client-confidential
- AI-generated narrative text | Too large + PII
- Raw AI prompt content | Too large + client data
- Raw AI response content | Too large + PII
- Document content/excerpts | Client-confidential
- HubSpot webhook request body | May contain PII

**Use IDs only.**

---

### 2026-05-10: Polly Resilience Pipeline Configuration — Locked Values

**Status:** Locked — Architectural Commitment  
**Date:** 2026-05-10  
**Owner:** Oracle (AI & Integration Dev)  

#### Summary

Every Azure OpenAI Service call passes through a single Polly resilience pipeline composed of three policies in order: Timeout → Retry → Circuit Breaker.

#### Pipeline Composition Order

```
Request
  ▼
Timeout Policy (60s pessimistic)
  ▼
Retry Policy (3 total attempts)
  ▼
Circuit Breaker (50% / 60s)
  ▼
Azure OpenAI API
```

#### Policy 1: Timeout

| Parameter | Value |
|---|---|
| Strategy | `TimeoutStrategy.Pessimistic` |
| Duration | **60 seconds** |

**On timeout:** `TimeoutRejectedException` is thrown.
- Mapping: `TimeoutRejectedException` → `SkillRun.failure_reason = 'AI_TIMEOUT'`
- Not retried — retry policy does not catch `TimeoutRejectedException`

#### Policy 2: Retry

| Parameter | Value |
|---|---|
| Total Attempts | **3** (initial attempt + 2 retries) |
| Backoff Strategy | Exponential with jitter |
| Base Delay | 2 seconds |
| Multiplier | 2× |
| Jitter | ±20% |

**Retry On:** `HttpRequestException`, HTTP 429 (Too Many Requests), HTTP 5xx (500, 502, 503, 504)

**Do NOT retry:**
- `HTTP 400 Bad Request`
- `HTTP 422 Unprocessable Entity`
- `TimeoutRejectedException`
- `BrokenCircuitException`
- Schema validation failure

**On max retries exceeded:** `MaxRetryAttemptsExceededException` is thrown.

#### Policy 3: Circuit Breaker

| Parameter | Value |
|---|---|
| Implementation | `AdvancedCircuitBreaker` |
| Failure Ratio Threshold | **0.5 (50%)** |
| Sampling Duration | **60 seconds** |
| Minimum Throughput | **3 calls** |
| Break Duration | **60 seconds** |
| Half-Open Probe Count | **1** |

**On circuit open:** `BrokenCircuitException` is thrown immediately.

**Scope:** The circuit breaker state is **application-scoped**. All skill calls across all concurrent audit runs share the same circuit breaker state.

#### Schema Validation Failure Handling

Schema validation failure occurs after a successful HTTP response (application-layer). The Polly pipeline has already completed successfully.

**Outcomes:**
- `SkillRun.status = 'failed'`
- `SkillRun.failure_reason = 'SCHEMA_VALIDATION_FAILURE'`
- `HTTP 502 Bad Gateway` returned to caller
- **No retry** — schema failures are deterministic
- Schema failures do NOT count toward circuit breaker failure ratio

---

## Decision Archive

(Entries older than 30 days would be archived here in production. Current entries are all from 2026-05-10.)

