# Service Interface Contracts — StrategicGlue Six-to-Fix

**Version:** 1.0  
**Author:** Neo (Backend Dev)  
**Date:** 2026-05-10  
**Status:** Locked Planning Artifact — Gates Phase 1 Coding

---

## Overview

All service classes are registered in DI via their interface. External dependencies are **always** injected as interfaces — no concrete instantiation inside services. Scoped services receive per-request tenant context; PolicyEngine is Singleton (pure functions, no state). All methods accept `CancellationToken` except where explicitly noted.

---

## 1. IAuditOrchestrator / AuditOrchestrator

**DI Lifetime:** Scoped  
**Purpose:** Coordinates the full audit run lifecycle — creates the run record, sequences skill execution via `ISkillRunner`, triggers `IPolicyEngine` evaluation after each skill output, routes flagged categories to `ICouncilRunner`, and transitions `audit_runs.status` through its state machine.

### Public Methods

```csharp
Task<AuditRun> CreateAuditRunAsync(
    Guid clientId,
    Guid createdByUserId,
    CancellationToken ct = default);
```
Creates a new `audit_runs` record in `pending` status for the specified client. Returns the full `AuditRun` entity.  
**Throws:** `ClientNotFoundException` → 404 | `AuditRunConflictException` → 409 (if an active run already exists for this client)

```csharp
Task StartAuditRunAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Transitions `audit_runs.status` → `running` and begins sequential skill chain execution. Fires SignalR `skill-started` events via `IHubContext<AuditRunHub>`. Non-blocking — execution continues in a background Task managed by the caller's circuit.  
**Throws:** `AuditRunNotFoundException` → 404 | `InvalidAuditRunStateException` → 409

```csharp
Task<AuditRun> GetAuditRunAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Returns the current state of an audit run, including all `CategoryResult` children.  
**Throws:** `AuditRunNotFoundException` → 404

```csharp
Task<IReadOnlyList<AuditRun>> GetAuditRunsForClientAsync(
    Guid clientId,
    CancellationToken ct = default);
```
Returns all audit runs for the given client within the current tenant, ordered by `created_at` descending.  
**Throws:** `ClientNotFoundException` → 404

```csharp
Task MarkAuditRunFailedAsync(
    Guid auditRunId,
    string failureReason,
    CancellationToken ct = default);
```
Transitions `audit_runs.status` → `failed`. Fires `run-failed` SignalR event. Called by skill/council failure paths.  
**Throws:** `AuditRunNotFoundException` → 404

### Constructor Dependencies
```csharp
AuditOrchestrator(
    ISkillRunner skillRunner,
    IPolicyEngine policyEngine,
    ICouncilRunner councilRunner,
    IPublisher publisher,
    ITelemetryCollector telemetryCollector,
    IDbConnectionFactory dbConnectionFactory,
    IHubContext<AuditRunHub> hubContext,
    ILogger<AuditOrchestrator> logger,
    AppDbContext dbContext)
```

---

## 2. ISkillRunner / SkillRunner

**DI Lifetime:** Scoped  
**Purpose:** Loads skill definition files from disk at startup (registered as `IHostedService` for pre-load), executes a single named skill via `IAIClient` with the full Polly resilience pipeline (timeout → retry → circuit breaker), validates AI output against the skill's declared JSON Schema, and persists `skill_runs` records.

### Public Methods

```csharp
Task<SkillRunResult> ExecuteSkillAsync(
    Guid auditRunId,
    string skillName,
    JsonDocument inputPayload,
    CancellationToken ct = default);
```
Executes the named skill. Writes a `skill_runs` record with `status = running` before calling AI, updates to `completed` or `failed` after. Fires `skill-started` and `skill-completed` / `skill-failed` SignalR events.  
**Returns:** `SkillRunResult` containing `SkillRun` entity, validated output JSON, and schema validation status.  
**Throws:** `SkillNotFoundException` → 400 | `SkillSchemaValidationException` → 502 | `SkillExecutionTimeoutException` → 504 | `SkillCircuitOpenException` → 503

```csharp
Task<SkillDefinition> GetSkillDefinitionAsync(
    string skillName,
    CancellationToken ct = default);
```
Returns the parsed skill definition (YAML frontmatter + prompt body) for the named skill.  
**Throws:** `SkillNotFoundException` → 400

```csharp
Task MarkDownstreamSkillsStaleAsync(
    Guid auditRunId,
    int fromSkillIndex,
    CancellationToken ct = default);
```
Sets `status = stale` on all `skill_runs` records for this audit run with `skill_index >= fromSkillIndex`. Called by `ReviewerWorkflow` when a reviewer triggers a rerun.  
**Throws:** `AuditRunNotFoundException` → 404

### Constructor Dependencies
```csharp
SkillRunner(
    IAIClient aiClient,
    IDbConnectionFactory dbConnectionFactory,
    IHubContext<AuditRunHub> hubContext,
    ILogger<SkillRunner> logger,
    AppDbContext dbContext)
```

---

## 3. IPolicyEngine / PolicyEngine

**DI Lifetime:** Singleton  
**Purpose:** Stateless, pure-function evaluation of 5 policy rules against a `CategoryResult` payload. No database access. No async I/O. All inputs passed in; all outputs returned as value objects.

### Public Methods

```csharp
IReadOnlyList<PolicyFlag> EvaluateCategory(
    CategoryResultPayload payload,
    PolicyEvaluationContext context);
```
Evaluates all 5 rules against the payload in order: `LOW_CONFIDENCE`, `MISSING_EVIDENCE`, `BENCHMARK_OUTLIER`, `INSUFFICIENT_EVIDENCE`, `SCORE_STRATEGY_MISMATCH`. Returns all matching flags (multiple rules can match simultaneously). Returns empty list if no rules fire.  
**Never throws.** Defensive — if payload fields are null, rules that depend on them are skipped (treated as non-matching).

```csharp
bool RequiresCouncilEscalation(
    IReadOnlyList<PolicyFlag> flags);
```
Returns `true` if any flag has `severity = trigger`. Pure boolean aggregation.  
**Never throws.**

```csharp
PolicyEvaluationSummary SummarizeFlags(
    IReadOnlyList<PolicyFlag> flags);
```
Returns a summary DTO with counts per severity, trigger flag names, and a boolean `RequiresEscalation`.  
**Never throws.**

### Constructor Dependencies
```csharp
PolicyEngine(
    ILogger<PolicyEngine> logger)
```
No database dependencies. No scoped dependencies. Safe as Singleton.

---

## 4. ICouncilRunner / CouncilRunner

**DI Lifetime:** Scoped  
**Purpose:** Runs the 3-persona AI Council deliberation (Advocate → Skeptic → Method Judge) for a flagged `CategoryResult`. Persists a `council_decisions` record, writes a new `category_result_versions` entry with `source_type = 'council'` if the decision is `adjusted`, and fires SignalR `council-started` / `council-completed` events.

### Public Methods

```csharp
Task<CouncilDecision> RunCouncilAsync(
    Guid auditRunId,
    Guid categoryId,
    IReadOnlyList<PolicyFlag> triggeringFlags,
    CancellationToken ct = default);
```
Executes all 3 AI personas sequentially, aggregates their outputs, and produces a `CouncilDecision`. If `decision_type = adjusted`, updates `category_results.activity_score` and inserts a new `category_result_versions` row. Fires SignalR events.  
**Returns:** `CouncilDecision` entity.  
**Throws:** `CategoryResultNotFoundException` → 404 | `CouncilExecutionException` → 502 (wraps AI client failures)

```csharp
Task<CouncilDecision?> GetCouncilDecisionAsync(
    Guid auditRunId,
    Guid categoryId,
    CancellationToken ct = default);
```
Returns the existing council decision for the given category if one exists, null otherwise.  
**Throws:** `AuditRunNotFoundException` → 404

### Constructor Dependencies
```csharp
CouncilRunner(
    IAIClient aiClient,
    ICalibrationTracker calibrationTracker,
    IDbConnectionFactory dbConnectionFactory,
    IHubContext<AuditRunHub> hubContext,
    ILogger<CouncilRunner> logger,
    AppDbContext dbContext)
```

---

## 5. IReviewerWorkflow / ReviewerWorkflow

**DI Lifetime:** Scoped  
**Purpose:** Handles all reviewer actions (approve, edit, rerun, escalate) against `CategoryResult` entries. Enforces the reviewer lockout rule (3 rejections of same category/24h → HTTP 409). Creates `CalibrationDelta` on every score override. Coordinates with `ISkillRunner` for reruns and `ICouncilRunner` for manual escalations.

### Public Methods

```csharp
Task ApproveAsync(
    Guid auditRunId,
    Guid categoryId,
    Guid reviewerId,
    CancellationToken ct = default);
```
Marks `category_results.status = 'approved'`. Writes a `reviewer_actions` record with `action_type = 'approve'`.  
**Throws:** `CategoryResultNotFoundException` → 404 | `ReviewerLockoutException` → 409 (REVIEWER_REJECTION_LOCKOUT) | `InvalidCategoryStateException` → 409

```csharp
Task<CategoryResult> EditAsync(
    Guid auditRunId,
    Guid categoryId,
    Guid reviewerId,
    decimal newActivityScore,
    string? newDocumentedStrategy,
    string overrideReasonCode,
    string notes,
    CancellationToken ct = default);
```
Updates `category_results` with new scores. Creates a `calibration_deltas` record. Inserts a new `category_result_versions` row with `source_type = 'reviewer'`. Writes a `reviewer_actions` record with `action_type = 'edit'`. Returns updated `CategoryResult`.  
**Throws:** `CategoryResultNotFoundException` → 404 | `ReviewerLockoutException` → 409 | `InvalidScoreRangeException` → 422 | `MissingOverrideReasonException` → 422

```csharp
Task RerunAsync(
    Guid auditRunId,
    Guid categoryId,
    Guid reviewerId,
    CancellationToken ct = default);
```
Triggers a new `SkillRun` for the category's owning skill. Marks downstream `skill_runs` as stale. Writes a `reviewer_actions` record with `action_type = 'rerun'`.  
**Throws:** `CategoryResultNotFoundException` → 404 | `ReviewerLockoutException` → 409

```csharp
Task<CouncilDecision> EscalateAsync(
    Guid auditRunId,
    Guid categoryId,
    Guid reviewerId,
    CancellationToken ct = default);
```
Manually escalates a category to the AI Council (bypasses policy trigger check). Writes a `reviewer_actions` record with `action_type = 'escalate'`. Invokes `ICouncilRunner.RunCouncilAsync`.  
**Throws:** `CategoryResultNotFoundException` → 404 | `ReviewerLockoutException` → 409

```csharp
Task<ReviewerLockoutStatus> GetLockoutStatusAsync(
    Guid auditRunId,
    Guid categoryId,
    Guid reviewerId,
    CancellationToken ct = default);
```
Returns the current lockout state for the given reviewer/category combination. Used by UI to pre-check before showing action buttons.

### Constructor Dependencies
```csharp
ReviewerWorkflow(
    ISkillRunner skillRunner,
    ICouncilRunner councilRunner,
    ICalibrationTracker calibrationTracker,
    IDbConnectionFactory dbConnectionFactory,
    ILogger<ReviewerWorkflow> logger,
    AppDbContext dbContext)
```

---

## 6. IPublisher / Publisher

**DI Lifetime:** Scoped  
**Purpose:** Publishes the final audit result once all 6 `CategoryResult` entries are approved. Sets `audit_runs.status = 'published'`, computes and persists composite/systems-maturity/AI-readiness scores, triggers HubSpot outbound sync, and records the publish event in `hubspot_sync_events`. Published state is terminal — further modifications to category scores are rejected.

### Public Methods

```csharp
Task<PublishResult> PublishAuditAsync(
    Guid auditRunId,
    Guid publishedByUserId,
    CancellationToken ct = default);
```
Validates all 6 categories are in `approved` status. Computes composite scores. Sets `audit_runs.status = 'published'`, `published_at = NOW()`, `published_by`. Enqueues a `HubSpotEvent` to the background channel. Returns a `PublishResult` with the computed scores and tier.  
**Throws:** `AuditRunNotFoundException` → 404 | `NotAllCategoriesApprovedException` → 409 | `AuditAlreadyPublishedException` → 409 | `InvalidAuditRunStateException` → 409

```csharp
Task<PublishedAuditSummary> GetPublishedAuditAsync(
    string clientSlug,
    CancellationToken ct = default);
```
Returns the latest published audit summary for the given client slug within the current tenant. Powers the `GET /api/audits/{clientSlug}` endpoint.  
**Throws:** `ClientNotFoundException` → 404 | `NoPublishedAuditException` → 404

```csharp
Task<IReadOnlyList<PublishedAuditVersion>> GetPublishedVersionsAsync(
    string clientSlug,
    CancellationToken ct = default);
```
Returns all publish events for a client, ordered by `published_at` descending. Powers the `GET /api/audits/{clientSlug}/versions` endpoint.  
**Throws:** `ClientNotFoundException` → 404

### Constructor Dependencies
```csharp
Publisher(
    IHubSpotClient hubSpotClient,
    Channel<HubSpotEvent> hubSpotChannel,
    ITelemetryCollector telemetryCollector,
    IDbConnectionFactory dbConnectionFactory,
    ILogger<Publisher> logger,
    AppDbContext dbContext)
```

---

## 7. ICalibrationTracker / CalibrationTracker

**DI Lifetime:** Scoped  
**Purpose:** Records a `CalibrationDelta` on every reviewer score override. Never skipped — this is the primary model improvement signal. Provides query access to calibration history for the Calibration Dashboard.

### Public Methods

```csharp
Task<CalibrationDelta> RecordDeltaAsync(
    Guid auditRunId,
    Guid categoryId,
    Guid reviewerId,
    decimal originalActivityScore,
    decimal adjustedActivityScore,
    string? originalDocumentedStrategy,
    string? adjustedDocumentedStrategy,
    string overrideReasonCode,
    string notes,
    CancellationToken ct = default);
```
Inserts a new `calibration_deltas` record. `overrideReasonCode` and `notes` are required — throws if either is null or empty.  
**Returns:** The persisted `CalibrationDelta` entity.  
**Throws:** `MissingOverrideReasonException` → 422 | `MissingCalibrationNotesException` → 422

```csharp
Task<IReadOnlyList<CalibrationDelta>> GetDeltasForAuditRunAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Returns all calibration deltas for the given audit run within the current tenant.  
**Throws:** `AuditRunNotFoundException` → 404

```csharp
Task<CalibrationSummary> GetCalibrationSummaryAsync(
    DateOnly from,
    DateOnly to,
    CancellationToken ct = default);
```
Returns aggregated calibration statistics (total overrides, average delta, top override reason codes) for the date range. Used by the Calibration Dashboard.

### Constructor Dependencies
```csharp
CalibrationTracker(
    IDbConnectionFactory dbConnectionFactory,
    ILogger<CalibrationTracker> logger,
    AppDbContext dbContext)
```

---

## 8. ITelemetryCollector / TelemetryCollector

**DI Lifetime:** Scoped  
**Purpose:** Collects and stores per-audit-run telemetry. Maintains a running `telemetry_events` record that is updated throughout the audit run lifecycle and finalized on completion. Powers the `/api/ops/metrics/daily` endpoint.

### Public Methods

```csharp
Task InitializeTelemetryAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Creates the initial `telemetry_events` record for an audit run. Called by `AuditOrchestrator` at run start.  
**Throws:** `AuditRunNotFoundException` → 404 | `TelemetryAlreadyInitializedException` → 409

```csharp
Task IncrementSkillRunCountAsync(
    Guid auditRunId,
    int tokensUsed,
    int latencyMs,
    CancellationToken ct = default);
```
Atomically increments `skill_run_count`, `total_tokens_used`, and `total_latency_ms` on the telemetry record.

```csharp
Task IncrementPolicyTriggerCountAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Atomically increments `policy_trigger_count`.

```csharp
Task IncrementCouncilRunCountAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Atomically increments `council_run_count`.

```csharp
Task IncrementReviewerActionCountAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Atomically increments `reviewer_action_count`.

```csharp
Task FinalizeTelemetryAsync(
    Guid auditRunId,
    CancellationToken ct = default);
```
Sets `completed_at = NOW()` on the telemetry record. Called by `Publisher.PublishAuditAsync` and `AuditOrchestrator.MarkAuditRunFailedAsync`.

```csharp
Task<IReadOnlyList<TelemetryEvent>> GetDailyMetricsAsync(
    DateOnly date,
    CancellationToken ct = default);
```
Returns all `telemetry_events` records for the given date. Used by the ops metrics endpoint. This method ignores the global `tenant_id` query filter and returns cross-tenant data; it is available only to `SuperAdmin` / `OpsViewer` roles (enforced at endpoint level).

### Constructor Dependencies
```csharp
TelemetryCollector(
    IDbConnectionFactory dbConnectionFactory,
    ILogger<TelemetryCollector> logger,
    AppDbContext dbContext)
```

---

## External Integration Interfaces (Owned by Oracle, consumed by Neo)

### IAIClient

```csharp
public interface IAIClient
{
    Task<AiCompletionResult> CompleteAsync(
        string skillName,
        string systemPrompt,
        string userPrompt,
        JsonSchema outputSchema,
        CancellationToken ct = default);
}
```
Wraps Azure OpenAI Service with structured output / JSON Schema enforcement. Returns `AiCompletionResult` containing raw JSON, token usage, and model metadata. Polly resilience pipeline is applied at the `SkillRunner` layer, not inside the client.  
**Throws:** `AiTimeoutException` | `AiRateLimitException` | `AiSchemaValidationException` | `AiCircuitOpenException`

---

### IBlobStorage

```csharp
public interface IBlobStorage
{
    Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobPath,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    Task<Stream> DownloadAsync(
        string containerName,
        string blobPath,
        CancellationToken ct = default);

    Task DeleteAsync(
        string containerName,
        string blobPath,
        CancellationToken ct = default);

    Task<Uri> GetSasUriAsync(
        string containerName,
        string blobPath,
        TimeSpan expiry,
        CancellationToken ct = default);
}
```
Wraps Azure Blob Storage. Container names and blob paths constructed by callers; `IBlobStorage` is path-agnostic. All operations use managed identity credentials.

---

### ISearchClient

```csharp
public interface ISearchClient
{
    Task IndexDocumentAsync(
        string indexName,
        string documentId,
        IDictionary<string, object> fields,
        CancellationToken ct = default);

    Task<SearchResult> SearchAsync(
        string indexName,
        string query,
        string tenantId,
        CancellationToken ct = default);

    Task DeleteDocumentAsync(
        string indexName,
        string documentId,
        CancellationToken ct = default);
}
```
Wraps Azure AI Search. All `SearchAsync` calls include `tenantId` as a required filter parameter — the implementation enforces tenant scoping at the search layer, not the caller's responsibility to append filter strings.

---

### IHubSpotClient

```csharp
public interface IHubSpotClient
{
    Task<HubSpotCompany> UpsertCompanyAsync(
        string hubSpotPortalId,
        string companyName,
        IDictionary<string, string> properties,
        CancellationToken ct = default);

    Task UpdateAuditResultAsync(
        string hubSpotCompanyId,
        string tier,
        decimal compositeScore,
        CancellationToken ct = default);

    Task<bool> ValidateWebhookSignatureAsync(
        string signature,
        string requestBody,
        CancellationToken ct = default);
}
```
Wraps the HubSpot CRM API. `ValidateWebhookSignatureAsync` is called in the webhook endpoint middleware before any business logic. All outbound calls are fire-and-forget from the business perspective (failures log but do not block publishing). Actual retry logic lives in the `Channel<HubSpotEvent>` background worker.
