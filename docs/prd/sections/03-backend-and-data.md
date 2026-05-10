# Section 3: Backend and Data

**Product:** StrategicGlue Six-to-Fix  
**Author:** Frink (Backend Dev)  
**Date:** 2026-05-10  
**Status:** Specification — For Engineering Team

---

## 3.1 Application Architecture Overview

### Hosting Model

The system shall be a single ASP.NET Core (.NET 10) application hosting both the Blazor Server frontend and all backend services. Blazor Server components communicate with service layer code via direct C# method calls over the SignalR circuit — there is no REST round-trip for UI data access. REST endpoints are exposed only for external integration surfaces (webhooks, ops tooling, and future public API consumers).

```
┌─────────────────────────────────────────────────────┐
│                Azure App Service                     │
│  ┌─────────────────────────────────────────────────┐ │
│  │          ASP.NET Core (.NET 10) Host            │ │
│  │                                                 │ │
│  │  ┌──────────────┐   ┌──────────────────────┐   │ │
│  │  │ Blazor Server │   │    REST Endpoints    │   │ │
│  │  │  (SignalR)   │   │ /webhooks, /api/ops  │   │ │
│  │  └──────┬───────┘   └──────────────────────┘   │ │
│  │         │                                       │ │
│  │  ┌──────▼─────────────────────────────────┐    │ │
│  │  │            Service Layer (DI)           │    │ │
│  │  │  AuditOrchestrator · SkillRunner        │    │ │
│  │  │  PolicyEngine · CouncilRunner           │    │ │
│  │  │  ReviewerWorkflow · Publisher           │    │ │
│  │  │  CalibrationTracker · TelemetryCollector│    │ │
│  │  └──────┬─────────────────────────────────┘    │ │
│  │         │                                       │ │
│  │  ┌──────▼────────────────────────────────────┐ │ │
│  │  │         Infrastructure Adapters (DI)       │ │ │
│  │  │  IAIClient · IBlobStorage · ISearchClient  │ │ │
│  │  │  IHubSpotClient · IDbConnectionFactory     │ │ │
│  │  └──────┬────────────────────────────────────┘ │ │
│  └─────────┼───────────────────────────────────────┘ │
└────────────┼────────────────────────────────────────┘
             │
    ┌────────▼──────────────────────────────────┐
    │  Azure Services                           │
    │  PostgreSQL · Blob Storage · AI Search    │
    │  Azure OpenAI · Key Vault · App Insights  │
    └───────────────────────────────────────────┘
```

### Service Layer Architecture

The system shall organize all business logic into named services registered via ASP.NET Core's built-in dependency injection container. All external dependencies shall be injected through interfaces, never instantiated directly. Service lifetimes:

| Service | Lifetime | Rationale |
|---|---|---|
| `AuditOrchestrator` | Scoped | One instance per HTTP request / Blazor circuit tick |
| `SkillRunner` | Scoped | Shares scoped context with orchestrator |
| `PolicyEngine` | Singleton | Stateless pure functions; no shared mutable state |
| `CouncilRunner` | Scoped | AI calls; one per request scope |
| `ReviewerWorkflow` | Scoped | Touches DB; one per request scope |
| `Publisher` | Scoped | Transactional; one per request scope |
| `CalibrationTracker` | Scoped | Writes calibration deltas inline with reviewer actions |
| `TelemetryCollector` | Singleton | Accumulates metrics; flushes on a timer |
| `HubSpotSyncService` | Singleton + `IHostedService` | Background sync loop |

### Request Pipeline and Middleware Stack

The system shall configure the ASP.NET Core middleware pipeline in the following order:

1. **Exception Handler** — catches unhandled exceptions, logs structured error, returns `application/problem+json`
2. **HTTPS Redirection** — enforces TLS in all environments
3. **Correlation ID Middleware** — assigns `X-Correlation-ID` to every request (reads inbound or generates UUID); propagates to all downstream log statements
4. **Tenant Resolution Middleware** — resolves `tenant_id` from authenticated claims; short-circuits with 401 if absent on protected routes
5. **Authentication** (ASP.NET Core Identity / Azure AD B2C)
6. **Authorization** (policy-based: `TenantAdmin`, `Auditor`, `Reviewer`, `SuperAdmin`)
7. **Rate Limiting** — per-tenant request throttling via `RateLimiterMiddleware`
8. **Routing** — maps Razor component endpoints and REST controller routes
9. **Blazor Server Hub** — `MapBlazorHub()`
10. **Fallback** — serves Blazor host page

### Configuration Management

The system shall store all secrets (connection strings, API keys, client secrets) in **Azure Key Vault** and resolve them at startup via the ASP.NET Core configuration system using the `Azure.Extensions.AspNetCore.Configuration.Secrets` package. The App Service shall authenticate to Key Vault using a **system-assigned managed identity** — no credentials are embedded in configuration files or environment variables. Non-secret configuration (feature flags, AI model names, skill YAML paths) shall live in `appsettings.json` / `appsettings.{Environment}.json`, with environment-specific overrides in Azure App Service Application Settings.

Configuration hierarchy (highest to lowest precedence):
1. Azure Key Vault secrets
2. Azure App Service Application Settings (environment variables)
3. `appsettings.{Environment}.json`
4. `appsettings.json`

### Health Check Endpoints

The system shall expose ASP.NET Core health checks at `/health`. Checks registered:

| Check | Validates |
|---|---|
| `PostgreSQLHealthCheck` | Can open a connection and execute `SELECT 1` |
| `BlobStorageHealthCheck` | Can reach the configured container |
| `AISearchHealthCheck` | Can reach the search service index |
| `AzureOpenAIHealthCheck` | Connectivity to the configured deployment |
| `KeyVaultHealthCheck` | Can read a sentinel secret |

Response: `200 OK` with JSON body `{ "status": "healthy", "checks": { ... } }`. Any failed check returns `503 Service Unavailable`.

---

## 3.2 Domain Model and Service Boundaries

### Domain Entities

| Entity | Responsibility |
|---|---|
| `Tenant` | Top-level subscription boundary. All tenant-scoped data carries `tenant_id`. |
| `TenantUser` | A platform user belonging to one tenant. Carries role (`TenantAdmin`, `Auditor`, `Reviewer`). |
| `Client` | A company being audited. Belongs to a tenant. Has a slug used in external API routes. |
| `Document` | A reference file uploaded for a client. Stores blob URI, kind, and metadata. Not mutated after upload. |
| `Audit` | The central audit record. Carries engagement metadata, full payload JSONB, scorecard data, and AI readiness flags. Versioned by `version` integer. |
| `AuditRun` | An orchestration session. Tracks overall run state (`pending`, `running`, `completed`, `failed`), start/end timestamps, and a JSONB snapshot of inputs. |
| `SkillRun` | The execution record for one skill within one `AuditRun`. Carries status, prompt used, raw AI output draft, schema validation result, and stale flag. |
| `CategoryPayload` | The structured output of one skill for one marketing area. Carries score, strategy rating, evidence list, confidence score, and policy flag set. |
| `ReviewerAction` | A reviewer's decision on a category payload: `approve`, `edit`, `rerun`, or `escalate`. Immutable once written. Includes override reason code and notes. |
| `CouncilDecision` | The AI council's adjudicated output for a flagged category. Records advocate position, skeptic position, method-judge synthesis, and the final adjusted payload. |
| `PublishedAudit` | The final immutable artifact. Version-stamped. Contains all approved `CategoryPayload` records frozen at publish time. |
| `CalibrationDelta` | Records every reviewer score override (before/after values, reviewer ID, timestamp). Used for offline model calibration. |
| `RunMetricsSample` | Daily telemetry snapshot per completed audit run: token counts, latency percentiles, policy trigger rates. |

### Service Boundaries

**`AuditOrchestrator`** — the top-level coordinator for a full audit run. Receives a `StartAuditRunCommand`, creates the `AuditRun` record, delegates to `SkillRunner` for each skill in sequence, evaluates `PolicyEngine` on each output, routes flagged payloads to `CouncilRunner`, and transitions the run to `completed` or `failed`. The orchestrator is the only service permitted to write `AuditRun` status transitions.

**`SkillRunner`** — executes a single skill against the AI model. Loads the skill definition (YAML frontmatter + markdown prompt), assembles the prompt with context retrieved from Azure AI Search, calls the AI client, validates the JSON output against the skill's registered schema, and returns a `SkillRunResult`. The skill runner does not make business decisions — it is a pure execution adapter.

**`PolicyEngine`** — a stateless singleton. Receives a `CategoryPayload` and returns a `PolicyResult` containing zero or more `PolicyFlag` records. Contains no side effects. All five rules (see §3.4) are implemented as pure functions evaluated against the payload's scalar fields.

**`CouncilRunner`** — executes the AI Council deliberation for a flagged payload. Runs three sequential AI calls (advocate, skeptic, method-judge), collects their outputs, synthesizes a `CouncilDecision`, and returns the adjusted `CategoryPayload`. Each council call is wrapped in the Polly resilience pipeline.

**`ReviewerWorkflow`** — handles reviewer interactions. Validates the action type, enforces the lockout rule (§3.5), writes the `ReviewerAction` record, triggers `CalibrationTracker` if the action is `edit`, and optionally triggers `SkillRunner` for `rerun` actions. The reviewer workflow is the only service permitted to write `ReviewerAction` records.

**`Publisher`** — assembles the `PublishedAudit` record from all approved `CategoryPayload` records for an audit. Validates that every category is approved before publishing. Writes the `PublishedAudit` as an immutable record and marks the `Audit` as published. Triggers HubSpot sync for audit result properties.

**`CalibrationTracker`** — writes `CalibrationDelta` records. Called inline by `ReviewerWorkflow` for every `edit` action that changes a score field. No async processing; writes are synchronous within the reviewer action transaction.

**`TelemetryCollector`** — accumulates `RunMetricsSample` data during a run (token counts from AI responses, latency). Flushes to the database at run completion and also persists to Application Insights via structured log events.

### Immutability Patterns

All domain objects shall be defined as **C# `record` types** with `init`-only properties. Mutations are expressed via `with` expressions that produce new record instances. No domain object shall expose mutable setters. This design choice enables:

- **Replay:** Any audit run can be reconstructed from the append-only event log.
- **Audit trail:** Each version of a `CategoryPayload` is a distinct record in `category_result_versions`.
- **Thread safety:** Immutable objects passed across async boundaries require no locking.
- **Testability:** Pure functions on immutable inputs are trivially verifiable.

Services communicate synchronously via direct C# method calls through injected interfaces. There are no message queues or event buses in v1. Background work (HubSpot sync, telemetry flush) uses `IHostedService`.

---

## 3.3 AI Layer Specification

### Recommendation: Azure OpenAI Service

The system shall use **Azure OpenAI Service** (not Azure AI Inference SDK) for all AI model calls.

**Rationale:**

- **Structured output support:** Azure OpenAI's `response_format: json_schema` mode enforces structured JSON outputs at the model level, reducing schema validation failures and simplifying the hard-gate validation step.
- **.NET SDK maturity:** `Azure.AI.OpenAI` (v2.x) is the most production-hardened .NET AI SDK in the Azure ecosystem, with complete async support, streaming, token usage metadata, and model version management.
- **Schema validation integration:** The SDK surfaces token-level rejection when the model cannot produce schema-conforming output, enabling the system to distinguish model-level failures from application-level validation failures — critical for calibration accuracy.
- **Model availability:** Azure OpenAI provides access to GPT-4o and GPT-4o-mini under the tenant's Azure subscription, subject to capacity reservations, with SLA guarantees not available on public endpoints.
- **Cost model:** Azure OpenAI billing is usage-based (per 1K tokens) and consolidated with existing Azure subscription spend. Reserved throughput (PTU) is available for production scaling without rate limit exposure.

The `IAIClient` interface shall abstract the Azure OpenAI SDK, enabling swap-out for testing (mock) or future model changes without service layer changes.

### Skill System

Each skill shall be defined as a file pairing:

1. **YAML frontmatter** — declares skill identity, model parameters, schema reference, and evidence retrieval config.
2. **Markdown prompt body** — the system + user prompt template, with `{{variable}}` placeholders for runtime context injection.

Example skill frontmatter:
```yaml
---
skill_id: 6tofix-scorecard-rubric
version: "1.0"
description: Scores all six marketing areas against the rubric
model: gpt-4o
temperature: 0.2
max_tokens: 4096
output_schema: category-payload-v1
evidence_retrieval:
  top_k: 5
  hybrid_weight: 0.7
areas:
  - brand
  - customer
  - offering
  - communications
  - sales
  - management
---
```

**Skill loading:** At application startup, `SkillLoader` scans the configured skills directory, parses YAML frontmatter, compiles prompt templates, and registers each skill in the DI-resolved `ISkillRegistry`. Skills are immutable after load.

**Skill execution sequence (sequential, each depends on prior):**

| Order | Skill ID | Output |
|---|---|---|
| 1 | `6tofix-scorecard-rubric` | Activity score + documented strategy per area |
| 2 | `systems-maturity-scoring` | 4 maturity dimension ratings |
| 3 | `gap-analysis-template` | Narrative gap analysis per area |
| 4 | `value-driver-rating` | 6 value driver ratings |
| 5 | `derive-tier` | Synthesized tier recommendation |

Each skill receives the outputs of all prior skills as context. `SkillRunner` passes the cumulative output map to the prompt template at each step.

**Evidence retrieval:** Before each skill call, `SkillRunner` queries Azure AI Search with a hybrid query (semantic similarity + keyword) using the audit's client context and the skill's `evidence_retrieval` configuration. Retrieved document chunks are injected into the prompt as a `{{evidence}}` block.

### Schema Validation (Hard Gate)

Every AI output shall be validated against a registered JSON Schema before any downstream processing. This validation is a **hard gate** — a validation failure does not trigger retry; it marks the `SkillRun` as `failed` with reason `SCHEMA_VALIDATION_FAILURE` and surfaces the error to the orchestrator. The orchestrator halts the run on schema failure.

Schema files shall be stored in the `schema/` directory. Validation uses `NJsonSchema` or `JsonSchema.Net`. The validator shall check:
- Required fields present
- Field types match
- Numeric values within declared ranges
- String enum values from allowed set

Schema validation failures are logged with the full AI response (PII-scrubbed) for offline diagnosis.

### Polly Resilience Pipeline

All AI model calls shall be wrapped in a Polly v8 resilience pipeline with the following policies applied in order:

| Policy | Configuration | Behavior |
|---|---|---|
| **Timeout** | 90 seconds | Cancels the call; logs `AI_TIMEOUT` |
| **Retry** | 3 attempts, exponential backoff (2s, 4s, 8s) with jitter | Retries on `429 Too Many Requests`, `503 Service Unavailable`, and transient `HttpRequestException` |
| **Circuit Breaker** | Opens after 5 consecutive failures; half-open after 30s | Returns `BrokenCircuitException`; `SkillRun` marked `CIRCUIT_OPEN` |

The Polly pipeline is registered as a named `ResiliencePipeline<HttpResponseMessage>` via `AddResiliencePipeline` in the DI container and injected into `IAIClient`.

### AI Council

The AI Council is triggered when `PolicyEngine` returns a flag of severity `TRIGGER` (not `WARNING`) on a `CategoryPayload`. The council runs three sequential AI calls:

| Persona | Role | Prompt Focus |
|---|---|---|
| **Advocate** | Optimistic analyst | Argues for the highest defensible score; surfaces supporting evidence |
| **Skeptic** | Challenging analyst | Challenges the score; surfaces missing evidence and counterarguments |
| **Method-Judge** | Rigor arbiter | Reviews both positions; applies the rubric strictly; produces the authoritative synthesis |

The method-judge's output is the `CouncilDecision`. Its adjusted `CategoryPayload` replaces the original payload for the category in question. The `CouncilDecision` record preserves all three positions for audit trail. All three council calls share the same Polly pipeline.

---

## 3.4 Policy Engine

The `PolicyEngine` shall evaluate the following five rules against every `CategoryPayload`. Rules are evaluated in order; multiple flags may apply to a single payload. Policy evaluation shall be a pure function with no database calls, no AI calls, and no side effects.

### Policy Rules

| # | Rule Name | Condition | Flag | Severity |
|---|---|---|---|---|
| 1 | Low Confidence | `confidence < 0.6` | `LOW_CONFIDENCE` | TRIGGER |
| 2 | Missing Evidence | `evidence.Count == 0` | `MISSING_EVIDENCE` | WARNING |
| 3 | Benchmark Outlier | `|score - industry_median| > 2` | `BENCHMARK_OUTLIER` | TRIGGER |
| 4 | Insufficient Evidence | `evidence.Count < 2` | `INSUFFICIENT_EVIDENCE` | WARNING |
| 5 | Score-Strategy Mismatch | `activity_score > 7 AND documented_strategy == "none"` | `SCORE_STRATEGY_MISMATCH` | TRIGGER |

### Warning vs. Trigger Severity

- **WARNING:** Informational flag. Surfaced to the reviewer in the category queue with a visual indicator. Does **not** trigger AI Council. Reviewer may approve despite warnings.
- **TRIGGER:** Escalates the category to the AI Council before it enters the reviewer queue. The reviewer reviews the council-adjusted payload, not the raw skill output.

### Effect on Reviewer Queue

A `CategoryPayload` enters the reviewer queue after:
1. Policy evaluation completes.
2. If TRIGGER flags are present: after `CouncilRunner` produces a `CouncilDecision` and the payload is adjusted.
3. If only WARNING flags or no flags: directly from skill output.

Categories with no flags may still be reviewed but are not mandatory review items. The reviewer queue UI shall distinguish: `REQUIRES_REVIEW` (has TRIGGER flags or council-adjusted) vs. `ADVISORY` (warnings only) vs. `CLEAN` (no flags).

---

## 3.5 Reviewer Workflow

### ReviewerAction Types

| Action | Effect |
|---|---|
| `approve` | Marks the `CategoryPayload` as approved. No further processing required. |
| `edit` | Reviewer modifies one or more payload fields. A new `CategoryPayload` version is written to `category_result_versions`. A `CalibrationDelta` is written for each changed score field. Notes and override reason code are required. |
| `rerun` | Triggers `SkillRunner` to re-execute the skill for this category. The existing payload is marked stale. Policy engine re-evaluates the new output. |
| `escalate` | Explicitly routes the category to a senior reviewer (TenantAdmin). Creates a `ReviewerAction` record with `escalate` type and optional notes. |

### Rejection Lockout Rule

The system shall enforce a **reviewer rejection lockout** per category per reviewer:

- A "rejection" counts as any `rerun` or `escalate` action on a category.
- If the same reviewer performs 3 or more rejections on the same category within a rolling 24-hour window, the system shall:
  1. Block further `rerun` and `escalate` actions for that reviewer on that category.
  2. Return `HTTP 409 Conflict` with error code `REVIEWER_REJECTION_LOCKOUT`.
  3. Log the lockout event with reviewer ID, category, and timestamp.
- The lockout window resets after 24 hours from the first rejection in the window.
- TenantAdmin role is exempt from the lockout rule.

### Edit Action Requirements

When a reviewer submits an `edit` action, the system shall require:
- `notes`: non-empty string, minimum 10 characters.
- `override_reason_code`: one of `EVIDENCE_CONTEXT_MISSING`, `BENCHMARK_INAPPLICABLE`, `RUBRIC_INTERPRETATION_OVERRIDE`, `CLIENT_SPECIFIC_FACTOR`, `DATA_QUALITY_ISSUE`.

Missing either field shall return `HTTP 400 Bad Request` with a structured error listing the missing fields.

### CalibrationDelta Feed

For every `edit` action, `CalibrationTracker` shall write one `CalibrationDelta` record per changed score field, capturing:
- `audit_run_id` and `category`
- `field_name` (e.g., `activity_score`, `maturity_dimension_1`)
- `original_value` (from the `SkillRun` output)
- `reviewer_value` (submitted by reviewer)
- `delta` (computed: `reviewer_value - original_value`)
- `reviewer_id` and `reviewed_at`
- `override_reason_code`

These records feed the offline calibration pipeline for model fine-tuning.

---

## 3.6 HubSpot Integration Architecture

### Integration Points

The system shall maintain a bidirectional sync between the platform's `Client` records and HubSpot Companies/Contacts using the HubSpot REST API v3, authenticated via a **private app token** stored in Azure Key Vault.

```
Platform                         HubSpot
───────                         ───────
Client (create/update)  ──────► Company (upsert by domain)
PublishedAudit          ──────► Company custom properties
                                 (audit_score, audit_tier, etc.)

HubSpot Contact event   ──────► POST /webhooks/hubspot
(domain match)          ──────► Client create/link
```

### Sync Triggers

| Trigger | Direction | Action |
|---|---|---|
| `Client` created in platform | Outbound | Upsert HubSpot Company by `company_domain` |
| `Client` updated in platform | Outbound | Patch HubSpot Company properties |
| `PublishedAudit` created | Outbound | Patch HubSpot Company custom audit properties |
| HubSpot Contact created with matching domain | Inbound | Create or link `Client` via webhook |
| HubSpot Company property updated | Inbound | Log sync event; no automatic platform update in v1 |

### Idempotency

Every outbound sync operation shall check the `hubspot_sync_log` table for a recent successful sync of the same entity before calling the HubSpot API. Inbound webhook events are deduplicated by `hubspot_event_id`. The `HubSpotSyncService` (background `IHostedService`) retries failed sync operations on an exponential backoff schedule (max 5 retries over ~15 minutes).

### Webhook Receiver

The system shall expose `POST /webhooks/hubspot`. The receiver shall:
1. Validate the HMAC-SHA256 signature from the `X-HubSpot-Signature` header.
2. Return `HTTP 200 OK` immediately (before processing) to prevent HubSpot retry.
3. Enqueue the event for background processing via an in-memory `Channel<HubSpotEvent>`.

All inbound processing occurs outside the HTTP request context. Webhook processing failures are logged and retried.

### Error Handling

Failed sync operations are recorded in `hubspot_sync_log` with `status = 'failed'` and `error_detail`. The background service queries for failed records and retries them. After 5 failed attempts, the record is marked `status = 'dead'` and an alert is emitted to Application Insights.

---

## 3.7 Document Processing

### Upload Flow

```
Client Browser / Blazor UI
        │
        ▼  (multipart/form-data)
ASP.NET Core Minimal API endpoint
        │
        ▼
DocumentService.UploadAsync()
        ├─► Validate: file type, size (max 50 MB), MIME type allow-list
        ├─► Generate blob name: {tenant_id}/{client_id}/{document_id}/{filename}
        ├─► Upload to Azure Blob Storage (via IBlobStorage)
        ├─► Write Document record to PostgreSQL
        └─► Enqueue indexing job via IHostedService channel
                │
                ▼
        DocumentIndexingWorker (IHostedService)
                ├─► Extract text (Azure Document Intelligence or direct text extraction)
                ├─► Chunk into overlapping segments (~512 tokens, 64-token overlap)
                └─► Upsert chunks to Azure AI Search index
```

### Document Kinds

| Kind | Description |
|---|---|
| `interview_transcript` | Raw transcript from stakeholder interview |
| `brand_guide` | Brand identity guidelines document |
| `sales_data` | Sales performance data export |
| `website_audit` | Website technical or content audit report |
| `competitive_scan` | Competitive landscape analysis |
| `martech_inventory` | Marketing technology stack inventory |

Document kind is validated against this enumeration on upload. Unknown kinds return `HTTP 400 Bad Request`.

### Evidence Retrieval by Skills

Each skill call is preceded by an Azure AI Search hybrid query. The search request:
- **Semantic query:** the skill's evidence retrieval query (constructed from prompt context)
- **Keyword filter:** `client_id eq '{id}'` and `tenant_id eq '{id}'` (security boundary)
- **Hybrid weight:** configurable per skill (default: 0.7 semantic / 0.3 keyword)
- **Top-K:** configurable per skill (default: 5 chunks)

Retrieved chunks are assembled into the `{{evidence}}` block injected into the skill prompt. The search always filters by `tenant_id` and `client_id` to prevent cross-tenant data leakage.

---

## 3.8 Database Strategy

### Append-Only Ledger Pattern

The system shall implement an append-only ledger for all audit state. `category_result_versions` is the canonical history table — every new version of a `CategoryPayload` is a new row; no rows are updated or deleted. The `category_results_current` table is a pointer table holding only the latest `version_id` per `(audit_id, category)`. This separation enables:
- Complete replay of any audit's history.
- Point-in-time queries for calibration analysis.
- Immutable audit trail without soft-delete patterns.

All other domain tables use `created_at` timestamps and logical append patterns. Hard deletes are never performed on audit-related tables. Tenants and clients may be soft-deleted (`deleted_at` timestamp) but their child records are retained.

### Multi-Tenant Isolation

Every tenant-scoped table shall carry a `tenant_id UUID NOT NULL` foreign key referencing `tenants.id`. The application layer shall include `AND tenant_id = @tenantId` in every query that touches tenant-scoped data. Queries without this filter shall be a blocking code review failure.

The database does not enforce row-level security (RLS) in v1; isolation is the application's responsibility via query construction. This is an acceptable tradeoff for v1 given the team size, with RLS considered for v2.

### Migration Strategy

Database migrations shall be numbered SQL files in `db/migrations/`, named `{NNNN}_{description}.sql`. Migrations are applied in ascending order by the `sf_admin` role. The migration runner shall track applied migrations in a `schema_migrations` table (migration name, applied_at, applied_by). Rollback scripts shall be provided for every migration as `{NNNN}_{description}_rollback.sql`.

### Connection Pooling

The system shall use **pgBouncer** in transaction pooling mode, deployed as a sidecar or shared Azure Container Apps instance. The application connects to pgBouncer; pgBouncer manages the physical connection pool to PostgreSQL. Connection string configuration values (max pool size, connection timeout) are stored in Azure Key Vault.

### Database Roles

| Role | Permissions | Used By |
|---|---|---|
| `sf_admin` | DDL + DML; `azure_pg_admin` group member | Migration runner only |
| `sf_app` | DML only (SELECT, INSERT, UPDATE on granted tables) | C# application runtime |
| `sf_dev` | DML + read-only DDL inspection | Developer local tooling and pgAdmin |

The application runtime credential shall never be `sf_admin`. Migration execution shall never use `sf_app`. This separation ensures runtime SQL injection cannot perform DDL mutations.
