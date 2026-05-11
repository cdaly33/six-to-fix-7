# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform for marketing maturity audits. AI skill chain is the core product feature.
- **Stack:** .NET 10 LTS, Azure OpenAI Service (Azure.AI.OpenAI SDK, structured output + JSON Schema enforcement), Polly resilience (timeout 60s, retry 3x exponential, circuit breaker 50%/60s), Azure AI Search, Azure Blob Storage, HubSpot Webhooks API v3 (HMAC-SHA256), Azure Key Vault (managed identity)
- **Skill system:** 5 sequential skills with YAML frontmatter (name, version, model, output_schema_pointer, depends_on). Loaded at startup. Executed by SkillRunner.
  - 6tofix-scorecard-rubric → systems-maturity-scoring → gap-analysis-template → value-driver-rating → derive-tier
- **AI failure semantics:** Schema validation failure = HTTP 502, SkillRun.status = "failed", NO retry. This is intentional.
- **AI Council:** 3 personas (Advocate, Skeptic, Method Judge). Triggered only by policy "Trigger" rules (not Warnings). Produces CouncilDecision with decision_type (confirmed/adjusted).
- **HubSpot:** Outbound upsert on client create and audit publish. Inbound webhook HMAC validated before processing. Processing is async (background Channel worker). Failure is non-blocking.
- **Azure AI Search:** Documents indexed within 30s SLA. Search scoped to tenant.
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-10 — Phase 0: Skill Schemas, Polly Config, AI Council Spec, HubSpot Mapping

**Skill JSON Schema Contracts (docs/architecture/skill-schemas.md)**
- All 5 skill output schemas enforce `additionalProperties: false` — no undeclared fields may be returned by Azure OpenAI
- `composite_score` in Skill 1 output is validated to be within [0, 60] but the model must compute it correctly (sum of 6 area scores) — there is no server-side recomputation at validation time; a mismatch between sum and declared value fails at the Policy Engine, not schema validation
- `output_schema_pointer` in skill YAML frontmatter is a JSON Pointer (RFC 6901) resolving to the schema within the frontmatter block (e.g., `#/output_schema`)
- Skill chain uses **accumulated context**: each skill receives all prior skill outputs, not just the immediately prior one
- Integer vs number distinction is critical: area scores are `integer`, confidence scores are `number` (float). Mixing these causes schema validation failure

**Polly Resilience Pipeline (`.squad/decisions/inbox/oracle-polly-config.md`)**
- **3 total attempts** = 1 initial + 2 retries. The timeout wraps the entire retry sequence (not per-attempt)
- Exponential backoff: base 2s, 2× multiplier, ±20% jitter → delays of ~2s, ~4s before attempts 2 and 3
- Circuit breaker is **application-scoped singleton** — a sustained Azure OpenAI outage blocks all concurrent audit runs
- Minimum throughput of 3 calls before circuit can open — prevents single-call blips from opening the circuit
- Schema validation failures do NOT count toward circuit breaker failure ratio — they are application-layer events, not HTTP failures
- `HTTP 503 + Retry-After: 60` is returned when `CIRCUIT_OPEN`; `HTTP 502` for all other skill failure reasons

**AI Council Deliberation (docs/architecture/ai-council-spec.md)**
- Council is called by `AuditOrchestrator` after `PolicyEngine.Evaluate()` — only if at least one Trigger-severity flag exists
- Personas are **sequential with cascading context**: Advocate is blind; Skeptic sees Advocate; Method Judge sees both
- **3N OpenAI calls** per Council session (N = number of flagged categories)
- Each persona call is wrapped by the shared Polly pipeline independently
- Council schema failure within a persona aborts that category's deliberation but does NOT abort the audit run (unlike skill schema failures which abort the entire chain)
- `decision_type = 'adjusted'` triggers new rows in `category_result_versions` with `source = 'council_adjustment'` — original scores are preserved via append-only semantics
- `council-started` SignalR event fires before first persona call; `council-completed` fires when `CouncilDecision` is persisted

**HubSpot Field Mapping (docs/architecture/hubspot-field-mapping.md)**
- 15 custom HubSpot Company properties required — must be provisioned per portal before integration activates
- Idempotency on client upsert: search by `strategicglue_client_id` before creating — prevents duplicates on retry
- Published audit scores pushed to HubSpot reflect **final reviewer-approved scores**, not raw Skill 1 output
- Inbound webhook validation uses HMAC-SHA256; v3 signature (method+url+body+timestamp) preferred over v1
- HubSpot webhook response is always `HTTP 200` immediately after HMAC passes — processing is async via Channel worker
- Inbound sync-back: only `name` and `domain` changes are accepted from HubSpot; `strategicglue_*` properties set by us are never overwritten by inbound events

**Correlation & Logging (`.squad/decisions/inbox/oracle-correlation-logging.md`)**
- All log calls use structured message templates — string interpolation is prohibited
- PII prohibition is absolute: no names, emails, company names, or AI content in any log payload — use UUIDs only
- `raw_ai_response` and `prompt_used` are stored in `skill_runs` table (database, access-controlled) but never in log sinks
- Background workers generate their own correlation IDs per event — no HTTP context available
- `StrategicGlue` namespace prefix in `appsettings.json` logging config ensures app-level `Information` events reach Application Insights while framework noise is filtered at `Warning`

### 2026-05-10 — HubSpot auth confirmed

- Chris confirmed outbound HubSpot authentication uses a Private App bearer token stored as `sf-hubspot-private-app-token`
- Inbound webhook HMAC validation remains separate and continues using `sf-hubspot-webhook-secret`
- Updated `docs/architecture/hubspot-field-mapping.md` and `docs/architecture/environment-contract.md` to remove OAuth/client-secret references for outbound HubSpot auth

### 2026-05-10 — Phase 0 Sealed

**Status:** All Phase 0 questions resolved by Chris. 15 inbox files consolidated into canonical `decisions.md` (21,203 bytes).

**Decision** entries merged include:
- HubSpot Private App token (Q1) — oracle
- Azure OpenAI same-subscription (Q2) — trinity
- 8 infrastructure decisions (Q3–Q10) — tank
- JWT role name confirmation (Q12) — trinity
- 9 architecture ADRs (Morpheus, Neo) — all locked

**Next:** Phase 1 gate is clear. Infrastructure teams can begin implementation from decisions.md.

### 2026-05-10 — Phase 3: AI Skill Definitions

**Skill files created:** `docs/skills/{skill-name}/skill.yaml` for all 5 skills. YAML is authoritative source of truth — inline SkillDefinitions in SkillRunner mirror them exactly.

**Phase 2 stub schemas corrected:** All 5 skills now use canonical schemas from `skill-schemas.md`. Critical differences vs stubs:
- Skill 1 output: nested `area_scores`, `confidence_scores`, `evidence_used`, `documented_strategy`, `composite_score` (not flat fields)
- Skill 2 output: `systems_maturity_score` (not `overall_systems_score`), nested `maturity_dimensions`, top-level `confidence`
- Skill 3 output: `gaps[{area, severity, description, recommendations[]}]` + `priority_areas[]` (not `category`, `current_score`, `target_score`)
- Skill 4 output: `value_drivers[{driver_name, current_rating, potential_rating, impact, linked_area, rationale}]` (not `driver`, `impact_score`, `effort_score`)
- Skill 5 output: `tier` is string enum `tier_1/tier_2/tier_3` (not integer 1–4), `ai_readiness` integer 0–100

**YAML file loading deferred:** Requires YamlDotNet + IHostedService pre-loader. Inline definitions are functionally equivalent. Future implementor: fail fast on startup if YAML is missing/invalid.

**CouncilRunner personas confirmed correct:** Hard-coded constants match ai-council-spec.md exactly. No file loading needed.

**Azure Search index schema documented:** Three indexes: `six-to-fix-evidence` (vector search for Skill 1 evidence retrieval), `six-to-fix-skill-outputs` (audit trail), `six-to-fix-calibration` (model improvement).

**HubSpot gaps found:** `UpdateAuditResultAsync` sends only 2 of 11 required audit result fields. `ISearchClient.SearchAsync` has no caller-supplied additional filter support (evidence retrieval can't scope to clientId/area). Both documented in oracle-phase3-hubspot.md.

**Policy Engine field path update needed:** LOW_CONFIDENCE now reads `confidence_scores.{area}` not top-level `confidence`. SCORE_STRATEGY_MISMATCH reads `documented_strategy.{area}`. Owner: Neo to update rule implementations.

### 2026-05-10 — Phase 2: AI Services Implementation

**Implementation status:** All Phase 2 AI services implemented on `dev/phase-2-ai-services` branch.

**Key implementation decisions:**
- `IRealtimeNotifier` abstraction added to Application layer to avoid circular dependency (Infrastructure → Web)
- `HubSpotEvent` stub created in `Application.Models` — will converge with Neo's definition on merge
- `Channel<HubSpotEvent>` registered as Singleton in `AiServiceExtensions` (not BusinessServiceExtensions) — coordinate with Neo to avoid double registration
- Skill definitions are hard-coded stubs in `SkillRunner` — replace with YAML file loading when `docs/skills/` files are available
- `CouncilSession.TenantId` and `SkillRunId` are resolved from `AuditRun`, `CategoryResult`, and matching `SkillRun` records inside `CouncilRunner`
- JSON schema validation in SkillRunner relies on Azure OpenAI structured output + JSON parseability check — full JSON Schema validation can be added with `System.Text.Json.Schema` when needed
- `GetSasUriAsync` requires BlobServiceClient to have shared key credentials — throws `InvalidOperationException` when using DefaultAzureCredential (token-based); alternative: pre-sign via storage account key stored in KV, or use user delegation SAS
