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


## Phase 1 — Stack Simplification Assessment (2026-05-15)

# Morpheus: Stack Simplification Assessment

**Date:** 2026-05-15  
**Author:** Morpheus (Lead & Architect)  
**Status:** Recommendation — awaiting team decision  
**Requested by:** cdaly33  

---

## Context

Each deployment of this application serves 2–5 active users at a time. This is a multi-tenant SaaS platform where the tenant is a small team. The question is whether the current technology choices are proportionate to that reality.

Trinity already filed `trinity-signalr-polling.md` with a parallel recommendation on SignalR. I've read it. We agree. This document extends the analysis to the full stack.

---

## 1. SignalR (Custom Hub) — Remove

**Verdict: Remove the dedicated hub. Switch to `PeriodicTimer` polling in `AuditDetail.razor`.**

Trinity's filing is correct and I endorse it. The full reasoning:

### What the current implementation costs you

The custom `AuditRunHub` at `/hubs/audit-run` creates a **second WebSocket connection** per user tab, on top of the Blazor Server circuit's existing WebSocket. Every user watching an audit run maintains two persistent connections to the same server.

The hub also has two security bugs Trinity correctly identified:
- `AuditRunHubClientFactory.Create()` builds a `HubConnection` with no JWT token. `[Authorize]` on `AuditRunHub` will reject every connection in production.
- `JoinAuditRun(auditRunId)` adds to a group with no tenant ownership validation. Any authenticated user can join any audit run group.

These aren't small fixes. Threading JWT tokens through a Blazor Server factory while the component's auth state is asynchronous is genuinely awkward. The tenant check in `JoinAuditRun` requires injecting scoped services into the hub, which has its own lifetime complications.

### What polling costs you

An audit run takes 30–90 seconds minimum (5 sequential AI skill calls + optional Council). A 3-second poll interval means the UI is at most 3 seconds stale. For 2–5 users, that's ~1.7 database reads per second during active runs. Against a PostgreSQL Flexible Server, this is noise.

Critically: because this is Blazor Server, polling happens **on the server**. There are no additional HTTP requests from the browser, no extra WebSocket. A `PeriodicTimer` in `AuditDetail.razor` calls `IAuditOrchestrator.GetAuditRunAsync()` and `InvokeAsync(StateHasChanged)` over the circuit that already exists. It's the architecture working as intended.

### What to preserve

**Keep `IRealtimeNotifier` and `AuditRunHubNotifier` in Infrastructure.** They cost nothing dormant. When a single deployment reaches 20+ concurrent users running simultaneous audits, or when sub-second latency becomes a real product requirement, the swap back to SignalR is mechanical: fix the two security bugs, implement Azure SignalR Service for scale-out, and re-enable the hub. The abstraction holds.

**Do not remove the hub code yet** — keep it but disconnect it from the component. Swap `AuditDetail.razor` to use `PeriodicTimer`. `IRealtimeNotifier` can remain wired in `AuditOrchestrator` as a no-op (or you can disable the wiring under a feature flag).

---

## 2. Azure AI Search — Partially Remove

**Verdict: Keep `six-to-fix-evidence`. Remove `six-to-fix-skill-outputs` and `six-to-fix-calibration`.**

Three indexes were specified. My assessment of each:

### `six-to-fix-evidence` — Keep

This is the vector search index over client documents. It's what makes Skill 1 (`6tofix-scorecard-rubric`) work — it retrieves pre-indexed evidence chunks per marketing area for AI input. This is the **core technical differentiator**. Removing it would require replacing the semantic evidence retrieval with something else (full-text search over blob storage, or feeding entire documents to the AI — both worse). Keep it.

### `six-to-fix-skill-outputs` — Remove

The search-index-schema.md itself notes: "Skill output indexing: currently not implemented." The data it would index (skill run outputs, council decisions, calibration notes) already exists in PostgreSQL with proper EF Core navigation and tenant-scoped query filters. A direct database query through `SixToFixDbContext` is faster, cheaper, cheaper to maintain, and already secured by the global query filter. For 2–5 users, there is no query volume that would justify Azure AI Search here.

The `rawJsonPath` fields point to Blob Storage — if you need the full JSON for debugging, go to Blob Storage directly or query `skill_runs.raw_ai_response` in the DB.

### `six-to-fix-calibration` — Remove

Calibration deltas live in the `calibration_deltas` table. The Calibration Dashboard needs to query them. At this scale, that's a `SELECT ... WHERE tenant_id = ? ORDER BY recorded_at DESC` query. No search index needed. The `notes` field (reviewer free text) is searchable via PostgreSQL `ILIKE` or `tsvector` full-text if needed — no Azure AI Search required.

### Cost impact

Azure AI Search Basic SKU: ~$75/month dev, ~$250/month prod (Standard). If you eliminate two of the three indexes, you may be able to drop from Standard to Basic — or eliminate the second index entirely from the Basic SKU allocation. At 2–5 users, search query volume on the evidence index is extremely low (one retrieval per audit run per category = 6 queries per audit run).

---

## 3. pgBouncer — Keep (No Action Needed)

pgBouncer is built into Azure PostgreSQL Flexible Server via the `connection_pooling = PgBouncer` configuration. It's essentially free and requires no separate infrastructure.

**Is it over-engineered at this scale?** Yes, technically. You will never have enough connections at 2–5 users to stress PostgreSQL's connection limits. But pgBouncer is already designed around — the serializable transaction + `pg_advisory_xact_lock` pattern in the reviewer lockout (ADR-006) is correctly scoped to transaction-mode pooling. The team already designed around its constraints. Removing it now would be churn for no user-visible benefit.

**Leave it.** The complexity cost is zero at runtime. The architectural cost of removing it (re-testing the lockout patterns, changing port references) isn't worth it.

---

## 4. AI Council 3-Persona Deliberation — Keep

This is the product. I will not recommend removing it.

The Advocate → Skeptic → Method Judge deliberation protocol is the reason an AI-generated marketing maturity score has credibility. Without it, you have a single LLM call per category with no quality gate. The Policy Engine flags anomalies; the Council interrogates them. This is earned complexity.

The cost: up to 18 sequential Azure OpenAI calls in a worst-case run (6 flagged categories × 3 personas). With the 60s Polly timeout wrapping the retry sequence, worst case is ~20 minutes for a fully-flagged run. That's a product concern (user experience during long runs), not an architectural over-engineering concern.

**Do not touch the Council.**

---

## 5. HubSpot Worker — Keep

`Channel<HubSpotEvent>` + `BackgroundService` is exactly the right pattern. Fire-and-forget, non-blocking, with error isolation. The `HubSpotSyncQueue` entity provides a persistence layer for events that fail. This is clean and proportionate.

**Leave it.**

---

## 6. Redis — Not Present

Redis was discussed in the `auth-spec.md` PRD (for session token storage under the Duende IdentityServer path) but was never implemented. The team landed on ASP.NET Core Identity + custom JWT (ADR-001), which doesn't need Redis. No action.

---

## 7. Auth (ASP.NET Core Identity + Custom JWT) — Keep

The PRD `auth-spec.md` recommended Duende IdentityServer. The team chose ASP.NET Core Identity + custom JWT instead (ADR-001). That decision holds. For 2–5 users per tenant with a self-managed SaaS model, the custom JWT approach is:
- Simpler to operate (no OIDC server process)
- Fully sufficient (tenant claims in token, roles in claims)
- Less expensive (no Duende commercial license)

The tradeoff: no per-tenant OIDC federation, no standard discovery endpoint. If enterprise SSO (SAML/OIDC upstream) becomes a real customer requirement, revisit Duende then. Not now.

**Leave it.**

---

## 8. Polly Resilience Pipelines — Keep

Azure OpenAI Service is an external dependency that times out, rate-limits, and occasionally 5xx's. The locked values in ADR-010 are appropriate. The timeout (60s) wraps the retry sequence correctly. The circuit breaker protects against cascading failures during OpenAI outages. This is not over-engineering — it's table stakes for a system that makes 5–18 LLM calls per audit run.

**Leave it.**

---

## Summary Recommendation

| Technology | Verdict | Rationale |
|---|---|---|
| SignalR (custom hub) | **Remove / Suspend** | 2 security bugs, 2 WebSocket connections per user, zero UX benefit at 2–5 users. Replace with PeriodicTimer polling. |
| `six-to-fix-evidence` search index | **Keep** | Core product feature (semantic evidence retrieval for Skill 1). |
| `six-to-fix-skill-outputs` index | **Remove** | Not implemented. Data lives in PostgreSQL. No query volume justification. |
| `six-to-fix-calibration` index | **Remove** | Duplicates `calibration_deltas` table. PostgreSQL ILIKE covers any real search need. |
| pgBouncer | **Keep** | Free, already designed around, no removal upside. |
| AI Council 3-persona deliberation | **Keep** | Product differentiator. Earned complexity. |
| HubSpot Worker | **Keep** | Clean, proportionate pattern. |
| Redis | **N/A** | Not present in the codebase. |
| ASP.NET Core Identity + JWT | **Keep** | Correct choice for this scale. Duende path deferred appropriately. |
| Polly pipelines | **Keep** | Necessary for external AI calls. |
| EF Core global query filters | **Keep** | Correct multi-tenancy pattern regardless of scale. |
| Azure Blob Storage | **Keep** | Documents have to live somewhere. |
| Azure Key Vault | **Keep** | Not over-engineering; industry standard. |
| Application Insights | **Keep** | Essentially free at this traffic volume. |

### The Two Actions

1. **Immediate:** Swap `AuditDetail.razor` to `PeriodicTimer` polling (Trinity's recommendation, endorsed). Disable hub connection in the component. Do not delete the hub code — preserve IRealtimeNotifier.

2. **Near-term:** Drop `six-to-fix-skill-outputs` and `six-to-fix-calibration` indexes from the Bicep and search-index-schema spec. Replace with direct PostgreSQL queries in the relevant service methods. This reduces Azure AI Search costs and removes an unimplemented dead-end spec.

Everything else in the stack is proportionate to what the product actually is.

— Morpheus



# Neo: Stack Simplification Assessment — 2026-05-15

**Author:** Neo | **Requested by:** cdaly33 | **Status:** Findings for team review

---

## 1. SignalR Hub (/hubs/audit-run) — Current State

**Fully implemented. Not aspirational.**

Both `AuditOrchestrator` and `SkillRunner` are live SignalR publishers:

- `AuditOrchestrator` directly injects `IHubContext<AuditRunHub, IAuditRunHubClient>` and fires `run-started`, `skill-started`, `skill-completed`, `run-completed`, `run-failed` (dash-separated).
- `SkillRunner` injects `IRealtimeNotifier` (backed by `AuditRunHubNotifier`) and fires `skill_started`, `skill_completed`, `skill_failed` (underscore-separated). **Inconsistency with Orchestrator naming — needs alignment.**
- `AuditDetail.razor` is a fully working consumer: creates a `HubConnection`, joins the run's group, handles `ReceiveEvent`, and updates the UI live.
- Hub is registered in `Program.cs` at `/hubs/audit-run` with `[Authorize]`.

**Two open defects regardless of whether we keep SignalR:**

1. `JoinAuditRun` in `AuditRunHub.cs` does NOT validate tenant ownership before adding the connection to a group — ADR-004 requires it. This is a cross-tenant data exposure risk.
2. The Blazor client builds `HubConnection` via `.WithUrl(uri)` only, no query-string `access_token`. ADR-004 specifies JWT via `?access_token=` for WebSocket transport. This will fail auth on native WebSocket connections in production.

**Recommendation:** Fix the two defects above regardless of polling vs. push decision. They're correctness/security issues, not architecture choices.

---

## 2. Polling Alternative — Backend Shape

If SignalR is removed, the minimal polling API is already there:

```
GET /api/audit-runs/{id}   (exists, returns AuditRun with Status, CompletedAt, ErrorMessage)
```

This is sufficient for functional polling today. No schema change required — `SkillRun` rows (with `SequenceIndex`, `SkillName`, `Status`) already exist and can be joined to derive progress.

For a richer progress endpoint (recommended if polling):

```
GET /api/audit-runs/{id}/status

Response:
{
  "auditRunId": "...",
  "status": "running",               // pending | running | awaiting_review | failed | published
  "completedSkillCount": 2,
  "totalSkillCount": 5,
  "currentSkillName": "gap-analysis-template",
  "failureReason": null,
  "startedAt": "...",
  "completedAt": null
}
```

This endpoint reads from `AuditRuns` + `SkillRuns` — no new columns needed. A 5-second poll interval at 2–5 users is effectively free (10 queries/minute against PostgreSQL via pgBouncer). The Blazor component's reconnect resync path (already fetches REST state on hub reconnect) is already the right pattern for polling.

**Cost to remove SignalR:** Delete `AuditRunHub.cs`, `AuditRunHubNotifier.cs`, `IAuditRunHubClient.cs` (Application), `IAuditRunHubClient.cs` (Web), `AuditRunHubClientFactory.cs`, remove `IHubContext` injection from `AuditOrchestrator`, replace `IRealtimeNotifier` calls in `SkillRunner` with a no-op or remove. Replace `ConnectHubAsync()` in `AuditDetail.razor` with a polling timer. Rough estimate: ~1 day of backend + frontend work.

**Recommendation for 2–5 users:** Polling is completely adequate. The hub adds operational complexity (sticky sessions required, WebSocket auth gap, scale-out not implemented). For this scale, polling is simpler and more resilient. If real-time feel matters to the product, keep SignalR but fix the two defects first.

---

## 3. pgBouncer (port 6432)

**Verdict: Not justified at 2–5 users, but not harmful — leave it.**

pgBouncer transaction-mode pooling targets high-concurrency scenarios (100+ short-lived connections). With Blazor Server, each circuit holds a Scoped `DbContext` that opens/closes connections per operation. At 2–5 users that's 2–5 circuits maximum — Azure PostgreSQL Flexible Server handles 100+ direct connections natively.

pgBouncer adds: `No Reset On Close=true` parameter requirement, port split (5432 for migrations, 6432 for runtime), advisory lock compatibility requirement (ADR-006 already solved this). Removing it saves one infrastructure component but is low-value churn given it's already wired correctly.

**Recommendation:** Leave pgBouncer in place for now. Document the port split (migrations on 5432, app on 6432) clearly. If the project stays at ≤10 users long-term, remove it in a future cleanup phase.

---

## 4. Background Channel\<HubSpotEvent\>

**Verdict: Justified. Keep it.**

The async channel decouples HubSpot API latency (rate limits, slow external calls, retries) from the user-facing publish response. `Publisher.PublishAuditAsync` enqueues and returns immediately; `HubSpotWorker` handles the external call in the background. Even at 2 users, blocking the publish response on a third-party API call is bad UX.

The channel is unbounded (in-memory, no persistence). If the process restarts mid-queue, events are lost. At 2–5 users this is acceptable. If HubSpot sync reliability becomes critical, a DB-backed outbox (`hubspot_sync_queue` table already exists in the schema) is the upgrade path — no new infrastructure needed, just swap the channel for a table.

**Recommendation:** Keep the channel as-is. For higher reliability, migrate to the existing `hubspot_sync_queue` DB table as an outbox — but that's a future decision, not today's.

---

## 5. Redis

**Not in the stack. Not needed at this scale.**

Zero Redis references anywhere — no packages, no connection strings, no configuration. SignalR runs single-node (`AddSignalR()` only, no `AddStackExchangeRedis()`). Azure SignalR Service (ADR-004 scale-out path) is also not implemented.

At 2–5 users on a single App Service instance, Redis would only be needed for: distributed cache (not used), SignalR scale-out (not needed), or distributed lock (advisory locks via PostgreSQL already cover the reviewer lockout use case in ADR-006/ADR-008). **Do not add Redis.**

---

## Summary Table

| Component | Current State | Justified at 2–5 users? | Recommendation |
|---|---|---|---|
| SignalR `/hubs/audit-run` | Fully implemented | Marginal — 2 open defects | Fix defects or replace with polling |
| Polling `GET /audit-runs/{id}/status` | Partially available (no dedicated endpoint) | Yes, simpler | Add dedicated status endpoint if removing hub |
| pgBouncer port 6432 | Fully wired | Low ROI but harmless | Leave in place |
| `Channel<HubSpotEvent>` | Fully implemented | Yes | Keep as-is |
| Redis | Not present | No | Do not add |




## Phase 1 — Stack Simplification & Security Fixes (2026-05-15)

### 2026-05-15: SignalR Removal — Replaced with PeriodicTimer Polling

**Status:** Implemented  
**Author:** Neo (Backend) & Trinity (Blazor Dev)  
**Requested by:** cdaly33

**What:** The dedicated SignalR hub (AuditRunHub at /hubs/audit-run) was replaced with a PeriodicTimer polling model in Blazor Server. Blazor Server already maintains a persistent WebSocket per user via its circuit; a second dedicated SignalR connection was redundant and had two security bugs: missing JWT on the client side and missing tenant ownership check in JoinAuditRun.

**Polling implementation:** PeriodicTimer polling every 3 seconds calls IAuditOrchestrator.GetAuditRunAsync directly (Scoped service, no HTTP round-trip). At current scale (2–5 concurrent users), polling is functionally indistinguishable from real-time push. New endpoint: GET /api/audit-runs/{id}/status (requires Bearer JWT, tenant-scoped via global query filter).

**Files removed (Web project):**
- SixToFix.Web/Realtime/AuditRunHubClientFactory.cs
- SixToFix.Web/Realtime/IAuditRunHubClient.cs
- Microsoft.AspNetCore.SignalR.Client NuGet package
- SignalR client wiring from AuditDetail.razor, Program.cs, _Imports.razor

**Files kept (server-side infrastructure — dormant, reactivatable):**
- SixToFix.Infrastructure/Hubs/AuditRunHub.cs
- SixToFix.Infrastructure/Hubs/AuditRunHubNotifier.cs
- SixToFix.Application/Services/IRealtimeNotifier.cs

**Why:** Eliminates security defects and redundant transport. Simplifies codebase. Polling satisfies current UX at low scale; if scale grows to 50+ concurrent users, revisit with server-sent events or reactive push.

---

### 2026-05-15: Azure AI Search — Removed Unused Indexes

**Status:** Implemented  
**Author:** Tank (DevOps & QA)  
**Date:** 2026-05-15

**What:** Removed six-to-fix-skill-outputs and six-to-fix-calibration from all Azure AI Search infrastructure. Only six-to-fix-evidence remains. Both removed indexes were never implemented — no code paths wrote documents to them. Their data lives in PostgreSQL: six-to-fix-skill-outputs → skill_runs table; six-to-fix-calibration → calibration_deltas table.

**Files changed:**
- infra/modules/search.bicep — removed index definitions
- infra/search-indexes/provision-indexes.ps1 — removed skill-outputs and calibration index definitions
- src/SixToFix.Infrastructure/ExternalClients/AzureSearchClient.cs — removed indexes from RequiredIndexes and BuildRequiredIndexes()
- 	ests/SixToFix.Infrastructure.Tests/Services/AzureSearchClientTests.cs — updated test to expect only six-to-fix-evidence
- docs/architecture/search-index-schema.md — updated to reflect single index

**Why:** Eliminates dead infrastructure and creates option to downgrade Azure AI Search SKU. No RBAC changes needed — Search Index Data Contributor role assignment is scoped to the search service resource, not individual indexes.

**SKU downgrade note:** Prod currently uses standard SKU with 2 replicas. Downgrade to asic is NOT yet safe (basic allows only 1 replica; prod uses 2 for HA). Future decision if team is willing to accept 1 replica.

---

### 2026-05-15: Deployment Documentation Updated — Stale References Fixed

**Status:** Complete  
**Author:** Tank (DevOps)  
**Branch:** dev/simplify-stack-signalr-search  
**Commit:** 4f32fef

**What:** docs/deployment/NEXT-STEPS-FOR-CHRIS.md updated to reflect recent stack simplification:

1. **AI Search Indexes (Step 3):** Updated from 3 indexes to 1. Removed references to six-to-fix-skill-outputs and six-to-fix-calibration; updated script output example.  
2. **SignalR Reference:** Changed "SignalR for real-time audit progress" to "PeriodicTimer polling for audit progress updates".  
3. **Document Date:** Updated to "Written: 2026-05-10 (evening) | Updated: 2026-05-15".

**Why:** Documentation now accurate and current. Chris can proceed with deployment without confusion about index count or SignalR presence.

---

### 2026-05-15: Security Review — Secret Handling in Deployment Guide

**Status:** Findings Ready for Remediation  
**Author:** Morpheus (Security Lead)  
**Requested by:** cdaly33  
**Severity:** 3 High/Medium issues, 1 Minor gap

**Bottom line:** The overall architecture is correct — secrets belong in Key Vault, the app loads them via AddAzureKeyVault + managed identity at runtime, and design-time migrations use a session-scoped env var that never reaches disk or source control. **Three real problems** need fixes before Chris runs the deployment steps.

**Finding 1 — PSReadLine history leak (HIGH):** NEXT-STEPS-FOR-CHRIS.md Step 5 tells Chris to run z keyvault secret set --value "<secret>". PowerShell 5.1+ persists command history to disk at %APPDATA%\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt. The --value argument including full connection string with password is written to that file.  
**Fix:** Replace z keyvault secret set --value commands with Read-Host -AsSecureString pattern that prompts without echoing. History entry contains no secret value.

**Finding 2 — Bicep writes admin credentials to runtime secret (HIGH):** infra/main.bicep lines 59–61 bootstrap DefaultConnection (runtime connection string) using ${postgresAdminLogin} and ${postgresAdminPassword}. The runtime app should use least-privilege sf_app role, not admin sfadmin credentials.  
**Fix:** Remove DefaultConnection from ootstrapSecrets in main.bicep or clearly mark as placeholder. Have Chris manually set with sf_app credentials after the sf_app role is created. Do NOT auto-populate runtime connection string with admin credentials.

**Finding 3 — Secret name mismatch (MEDIUM):** Name inconsistencies across NEXT-STEPS-FOR-CHRIS.md, ppservice.bicep, and main.bicep:  
- NEXT-STEPS says to set Jwt--SigningKey, but Bicep bootstrap writes Jwt--Key (placeholder)  
- NEXT-STEPS says to set HubSpot--PrivateAppToken, but Bicep bootstrap writes HubSpot--ApiKey (placeholder)  
- ppservice.bicep KV references read Jwt--Key and HubSpot--ApiKey, not the names Chris is told to set

Currently works (accidentally) because Program.cs adds the KV config provider last, overriding earlier sources. But fragile and confusing.  
**Fix:** Pick ONE canonical name for each secret and use consistently. Recommended canonical names:  
  - Jwt--SigningKey (maps to Jwt:SigningKey that Program.cs reads)  
  - HubSpot--PrivateAppToken (maps to HubSpot:PrivateAppToken)  
  - AzureOpenAI--ApiKey (maps to AzureOpenAI:ApiKey)  
- Update ppservice.bicep KV references to use these names.

**Finding 4 — Design-time env var in PSReadLine history (MEDIUM):** The assignment $env:DESIGN_TIME_CONNECTION_STRING = "Host=...;Password=..." is recorded in PSReadLine history.  
**Fix:** Use the Read-Host -AsSecureString pattern for the design-time variable too. Also update docs/deployment/migrations.md to address Bash history exposure (inline assignment would land in Bash history).

**Finding 5 — Runtime config chain ✅ CORRECT:** Tracing the code path confirms: Bicep provisions Key Vault → identity.bicep grants managed identity KV Secrets User role → appservice.bicep sets KV references → Program.cs AddAzureKeyVault → config reads live KV values. Nothing sensitive in ppsettings.json or source control. Dormant dev credentials in ppsettings.Development.json are committed but acceptable — never used outside local dev machines.

**Finding 6 — GitHub Actions ✅ CORRECT:** Uses OIDC federation, no stored service principal secrets. Workflows reference only non-secret identifiers.

**Finding 7 — Missing KV references (LOW):** AzureOpenAI--Endpoint and AzureOpenAI--DeploymentName lack explicit App Service settings in ppservice.bicep, though the KV config provider picks them up implicitly. Optional: add explicit settings to make dependency visible.

**Recommended actions (priority order):**
1. Update docs/deployment/NEXT-STEPS-FOR-CHRIS.md to use Read-Host -AsSecureString for all secret values.
2. Fix Bicep runtime credential issue: remove DefaultConnection from ootstrapSecrets or add separate sfAppPassword parameter.
3. Align secret names across docs and Bicep: use canonical set Jwt--SigningKey, HubSpot--PrivateAppToken, AzureOpenAI--ApiKey.
4. Update docs/deployment/migrations.md to address history exposure.

---

### 2026-05-15T22:12:06-05:00: User Directive — Git Workflow

**By:** cdaly33 (via Copilot CLI)  
**What:** All development work done by Squad agents or Copilot must be done on branches and merged back to main via PR. No direct commits to main.  
**Why:** User request — captured for team memory. Existing git workflow decisions already establish dev/phase-{N}-{slug} or eature/ branch naming convention.

