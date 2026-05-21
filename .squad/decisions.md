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

---

### ADR-013: Admin Bootstrap Seeder — Idempotent SuperAdmin Creation
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-18

Production had no users in ASP.NET Core Identity, so Chris needed a safe bootstrap path to create the first SuperAdmin without direct database writes. An environment-gated startup hosted service uses `UserManager<ApplicationUser>` and `RoleManager<IdentityRole<Guid>>` to create exactly one bootstrap SuperAdmin when no SuperAdmin user exists. The service is registered only when `SeedAdmin:Enabled=true`, reads `SeedAdmin:Email` and `SeedAdmin:Password` from configuration, confirms email immediately, and assigns the canonical `SuperAdmin` role. Idempotent: any existing SuperAdmin user makes startup a no-op. Non-fatal: missing config or Identity failures are logged and do not crash the host. All user/role changes flow through ASP.NET Core Identity managers. Prod wiring uses Key Vault flat secrets `SeedAdmin--Email` and `SeedAdmin--Password`, plus App Service env var `SeedAdmin__Enabled`.

**Key decisions:**
- Idempotent on startup: no-op if SuperAdmin exists
- Non-fatal error handling: logs and continues
- No raw Identity table writes; all changes via managers
- Prod uses Key Vault references for secrets

---

### ADR-014: StrategyHub Domain Layer — Phase 3 Foundation
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-19

Phase 3 (PR #43) added the StrategyHub domain model: `PillarContent` (per-tenant per-pillar JSONB content), `UserPillarProgress` (per-user per-pillar 0–100%), `PlaybookTemplate` (tenant-scoped template catalogue). Enums: `Pillar` (Brand=1 … Management=6), `PlaybookTemplateStatus` (Draft/Published/Archived). Three new roles: `SuperAdmin`, `TenantAdmin`, `Client` replace magic strings; migration grants `Client` role to all existing Reviewer/Viewer users. Service interfaces (`IPillarContentService`, `IProgressService`, `IPlaybookTemplateService`) are stubs in Phase 3, implemented in Phase 4. `PillarContent.BodyJson` uses JSONB schema `{strategy: [title, points[]], execution: [string], templates: [string], examples: [string], metrics: [[label, value]]}` for evolving content without migration churn. Unique constraint `(tenant_id, pillar)` on pillar_contents ensures idempotent seeding.

**Key decisions:**
- JSONB for evolving content schema; relational columns avoided
- Pillar stored as int; display names/colors in UI
- Unique (tenant_id, pillar) for idempotent seeding
- `PlaybookTemplate.Pillar` nullable for cross-pillar templates
- Old Reviewer/Viewer role rows kept; legacy policies aliased in Program.cs until Phase 6 cleanup

---

### ADR-015: StrategyHub Services — Phase 4 Implementations
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-19

Phase 4 (PR #46) implements three services from Phase 3 interfaces. `PillarContentService` provides `GetForTenantAsync(tenantId, pillar)`, `GetAllForTenantAsync(tenantId)` (lazy-seeds 6 rows), `UpsertAsync(...)`. `ProgressService` provides `GetForUserAsync(userId)`, `SetPercentAsync(userId, pillar, percent, clamp 0-100)`, `GetAverageForUserAsync(...)` (sum/6). `PlaybookTemplateService` provides `GetPublishedAsync(tenantId, pillar?)` (matching + cross-pillar rows, ordered Popularity DESC then Name ASC), CRUD with status transitions (Draft→Published→Archived). Tenant isolation: EF Core global query filters + explicit service-layer predicates (two layers of defence). Lazy seeding in `GetAllForTenantAsync`: if < 6 rows exist, missing pillars seeded with `{"placeholder":true}` before return. All three services marked Scoped in DI.

**Key decisions:**
- Lazy seeding inside GetAllForTenantAsync (no separate seeder)
- `SetPercentAsync` stamps TenantId from ITenantContext (Scoped injection)
- `CreateAsync` always forces Draft regardless of input
- Two-layer tenant isolation: global filter + explicit predicate
- All services Scoped (phase 4 hook pattern consistent with Phase 3)

---

### ADR-016: Phase 6 — Remove Legacy Audit/Calibration System
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-19

The product pivoted from the Six-to-Fix audit/calibration system to StrategyHub. Phase 6 removes all domain entities, services, pages, and tests tied to legacy audit/calibration/skill-chain/telemetry system. Forward-only EF migration `DropLegacyAuditTables` drops 16 legacy tables. Post-Phase-6: clean domain model focused on StrategyHub (~120 files removed). Migration is forward-only; rollback requires manual DDL. Only remaining references are in EF Core migration Designer snapshots, which are historical metadata and should never be manually edited.

**Key decisions:**
- Complete removal of legacy entities/services/pages
- Forward-only migration (no rollback)
- Designer snapshots left intact (auto-generated, historical)
- No orphaned policies or nav entries

---

### ADR-017: Prod Login 500 — Three-Layer Root Cause & Fixes
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-18

Production login failed (HTTP 500) due to three cascading issues. **Layer 1:** `sf_app` PostgreSQL role did not exist (created during burstable tier migration but not in pgBouncer). Fixed by manual `CREATE ROLE sf_app` executed as sfadmin. **Layer 2:** EF Core migrations never ran against prod (database empty). Fixed by `dotnet ef database update` with AdminConnection + startup migration runner in Program.cs (uses AdminConnection/sfadmin for DDL, DefaultConnection for DML). Migration `20260519033146_GrantAppRolePermissions` codifies GRANT/REVOKE SQL; fail-fast if `sf_app` missing. **Layer 3:** `SeedAdmin--Password` lacked digit+non-alphanumeric (Identity policy violation). Fixed by updating KV secret to meet policy. Standing rules: provision `sf_app` role before first deploy; validate `SeedAdmin--Password` meets Identity policy (≥12 chars, uppercase, digit, non-alphanumeric); never use DefaultConnection for DDL; startup migration runner prevents manual intervention.

**Key decisions:**
- Startup migration runner (Program.cs) + AdminConnection for all DDL
- Migration `GrantAppRolePermissions` codifies role permissions + fail-fast guard
- Prod KV secret management separate from Bicep (runtime secret, no Bicep PR)
- Standing rules for future PostgreSQL provisioning

---

### ADR-018: ITenantService API — TenantAdminPanel MVP
**Status:** Accepted | **Author:** Neo | **Date:** 2026-05-19

`ITenantService` interface for TenantAdminPanel MVP (PR #57). `GetCurrentTenantAsync(tenantId)` returns TenantDto or null (active only). `UpdateTenantNameAsync(tenantId, newName)` validates non-empty, trims, updates Name + UpdatedAt, returns DTO. `GetTenantUsersAsync(tenantId)` returns list of TenantUserDto (Id, Email, FullName, Role via UserManager, IsActive, LastLogin null, CreatedAt), ordered by Email. Uses `UserManager<ApplicationUser>` for role lookup (not DbContext.Users). Scoped service. DTOs are immutable records. LastLogin always null (deferred enhancement). Following `IClientService` pattern: Scoped, DTOs, validation in service, logs updates.

**Key decisions:**
- `UserManager<ApplicationUser>` for role lookups (abstracts Identity schema)
- LastLogin nullable in DTO (enhancement deferred)
- Name-only editing for MVP (slug immutable, plan tier/HubSpot deferred)
- User list read-only for MVP (invite/delete deferred)

---

### ADR-019: Bicep Drift Prevention — SeedAdmin App Settings
**Status:** Accepted | **Author:** Tank | **Date:** 2026-05-18

Chris manually wired three App Settings on `app-sixtofix-prod` for bootstrap seeder: `SeedAdmin__Enabled`, `SeedAdmin__Email` (KV ref), `SeedAdmin__Password` (KV ref). `infra/modules/appservice.bicep` was missing the KV references—next deploy would have overwritten and re-broken the seeder. Added `SeedAdmin__Email` and `SeedAdmin__Password` as KV references to `appservice.bicep` (same pattern as ConnectionStrings/Jwt). `SeedAdmin__Enabled` conditional expression kept: `isProd ? 'true' : 'false'`. **Standing rule:** When any manual Azure change is made (Portal, az CLI) not yet in Bicep, Tank must proactively open a Bicep PR to codify it—do NOT wait for Chris. Manual changes not in Bicep are time-bombs: next deploy silently wipes them.

**Key decisions:**
- Added KV references for Email + Password (following existing pattern)
- Conditional expression preserved for Enabled flag
- Proactive Bicep PR required for all manual changes

---

### ADR-020: CSP & Security Headers — Font Allowlist + Hardened Policy
**Status:** Accepted | **Author:** Tank | **Date:** 2026-05-18

Added CSP to prevent XSS and control resource loading. New middleware `SecurityHeadersMiddleware.cs` (Program.cs step 2). Policy: `default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; font-src 'self' https://fonts.gstatic.com data:; connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com; img-src 'self' data: blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`. `'unsafe-inline'` on script-src required for Blazor Server circuit bootstrapper; `'unsafe-inline'` on style-src for Blazor scoped CSS. `wss:` in connect-src for SignalR WebSocket. `data:` in font-src for base64-encoded fonts. Additional headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`. CI workflows validated: test.yml uses SixToFix.slnx ✅; deploy-app.yml ✅; deploy-infra.yml fixed stale RG name (`rg-StrategicGlue-CommandCenter` → `rg-sixtofix-prod`). No unreflected Bicep drift. `.gitignore` already has `*.lscache`.

**Key decisions:**
- CSP as single middleware injected early (step 2)
- `'unsafe-inline'` justified for Blazor requirements
- `wss:` for SignalR WebSocket connections
- Additional headers for DENY + strict origin-when-cross-origin

---

### ADR-021: CSS Hotfix — Hero + Sidebar Styling
**Status:** Accepted | **Author:** Trinity | **Date:** 2026-05-19

Production CSS broken (PR #50): Hero H1 invisible, sidebar collapsed. **Root cause 1:** `tokens.css` missing `--hero-radial-overlay` (hero background radial-gradient var), `--text-5xl/6xl/7xl` (heading sizes). Missing CSS custom properties make entire property declaration invalid at computed-value time—fallback to initial (transparent background). **Root cause 2:** `App.razor` missing `<link href="SixToFix.Web.styles.css" />` (Blazor CSS isolation bundle not linked). `StrategyHubShell.razor.css`, `SectionSidebar.razor.css`, `NavItem.razor.css` never loaded. Chose NOT to add Tailwind CDN—custom CSS token system is complete, no Tailwind utility classes used. Minimal fix: add 4 missing tokens to `tokens.css` + add CSS isolation link to `App.razor`. Verified: hero now renders navy gradient ✅, sidebar styles load ✅, H1 visible on navy ✅. Known issue (not caused): smoke test expects 302 → 200 (Phase 2 public homepage), deferred.

**Key decisions:**
- Custom CSS token system complete; Tailwind not needed
- Add missing var tokens instead of CDN
- Add Blazor CSS isolation link to App.razor
- Defer smoke test update to separate PR

---

### ADR-022: Login.razor Prerendering Disabled for Form Data
**Status:** Accepted | **Author:** Trinity | **Date:** 2026-05-18

Production bug (PR #40): Login form showed "Email required" + "Password required" errors even when fields visibly filled. **Root cause:** `Login.razor` used `@rendermode InteractiveServer` (defaults `prerender: true`). Lifecycle: (1) server prerender → static HTML form, (2) user fills inputs in static DOM, (3) Blazor SignalR circuit connects → component re-initializes with empty `_model = new LoginModel()`, (4) user submits → validates empty model → required-field errors. Stale DOM / empty model race. **Decision:** Disable prerendering: `@rendermode @(new InteractiveServerRenderMode(prerender: false))`. Form only appears after circuit live, all keystrokes flow directly to `_model`, no race. **Standing rule:** Any page with `@bind-Value` user inputs under InteractiveServer should use `prerender: false` unless explicit SEO/TTFB requirement. Read-only display pages safe with `prerender: true`. Login/registration/data-entry highest risk.

**Key decisions:**
- Disable prerender for form-heavy pages
- Keep prerender on read-only display pages
- Apply rule to Login, registration, data-entry

---

### ADR-023: Visual Foundation — Phase 1 StrategyHub Shell
**Status:** Accepted | **Author:** Trinity | **Date:** 2026-05-19

Phase 1 (PR #44) delivered StrategyHub UI shell: navbar, sidebar, layout components, tokens, icon system, form styles. Blazor components: `StrategyHubShell.razor` (flex-based responsive shell), `SectionSidebar.razor` (260px navy sidebar), `NavItem.razor` (nav with hover/active states), `Logo.razor`, icon sprites. CSS: token system (`tokens.css` → `components.css` → `app.css` → `public.css` cascade), custom properties for colors/spacing/type, component styles. Signed in / logged out layouts differ. Deferred to Phase 2: public homepage, hero section, pillar marketing cards, new pillar pages (`/brand`, etc.), legacy page deletion, role rename outside shell, CSP for fonts.

**Key decisions:**
- Token cascade: tokens → components → app → public
- Flex-based responsive shell (no grid)
- Custom properties for theming (no Tailwind)
- Signed in / logged out layout variants

---

### ADR-024: Public Homepage — Phase 2 Marketing + Auth-Aware CTAs
**Status:** Accepted | **Author:** Trinity | **Date:** 2026-05-19

Phase 2 (PR #45) added public homepage at `/`. `Home.razor` rendered as static SSR (no `@rendermode`, no SignalR overhead), shows 6 pillar cards with auth-aware CTAs. `PublicLayout.razor` wraps public pages—sticky navy header, footer. CSS animations (fadeSlideUp keyframes, Framer Motion -like behavior, prefers-reduced-motion guard). Pillar card links → `/login` for now (Phase 4 adds pillar pages). Dashboard removed from `/` (now only `/dashboard`, [Authorize]). No-redirect gate on homepage (recommended approach—authenticated users see Dashboard/My-Playbook links, anonymous see Sign-In/Get-Started). Auth contract test updated (old test assumed wrong behavior; now asserts 200 OK for `/`, [Authorize] on `/dashboard`). Deferred to Phase 4: pillar pages, post-login redirect TODO, deep-links from cards.

**Key decisions:**
- `/` is public for everyone (no redirect gate)
- Static SSR for Home.razor (better TTFB/SEO)
- CSS animations only (no JS library)
- Pillar links → `/login` (Phase 4 adds pages)

---

### ADR-025: Dashboard + Pillar Pages + Templates Library — Phase 4 Pages
**Status:** Accepted | **Author:** Trinity | **Date:** 2026-05-19

Phase 4 (PR #47) rewritten Dashboard, new PillarPage (6 routes), Templates library. `PillarPage.razor`: single component with 6 `@page` directives (`/brand` … `/management`), pillar resolved from NavigationManager.Uri, ordinal "(PILLAR 2 OF 6)" derived from enum. Dashboard: personalized playbook overview + progress grid + recent content (0% empty state with Getting Started cards). Templates.razor: `/templates` library with pillar filter + modal card view. CSS tokens only (zero hardcoded hex); `--pillar-brand`, `--pillar-customer`, etc.; `--color-gold-400`, `--color-navy-800`. All three pages use `@rendermode @(new InteractiveServerRenderMode(prerender: false))` (Phase 3 lesson). `EmptyContentMessage.razor` role-gated. BodyJson schema (Phase 3 domain): if `"placeholder":true` exists, treated as empty state. Cherry-picked service interfaces/implementations from PR #46 (dependency on PR #46). Deferred: BodyJson editing, template creation/publishing flows, CSP fonts, auth redirect.

**Key decisions:**
- Single PillarPage component with 6 `@page` routes
- BodyJson placeholder detection for empty state
- Pillar cards always rendered; progress loaded if userId available (more resilient)
- Cherry-picked services from PR #46 (temporary; drop before merge if #46 merges first)
- CSS tokens only, zero hardcoded colors

---

### ADR-026: Pillar Content Admin Editor — Phase 5
**Status:** Accepted | **Author:** Trinity | **Date:** 2026-05-19

Phase 5 (PR #55) added tenant admin content editor at `/admin/content`. Route-level auth `[Authorize]` + inner `<AuthorizeView>` for role enforcement (SuperAdmin, TenantAdmin). Tenant resolved from `tenant_id` claim (never query string—multi-tenant boundary enforced at auth). Pillar selection via `?pillar=` query param (tab pre-selection UX). BodyJson schema per ADR-025; plain textarea (rich editor deferred). All 6 pillar tabs rendered. Graceful fallback for legacy/placeholder JSON bodies. Page: `PillarContentAdmin.razor` (11 tests, all pass). Deferred: rich-text editor, audit trail UI (UpdatedByUserId stored/shown as truncated GUID), draft/publish workflow (live immediate).

**Key decisions:**
- Tenant from `tenant_id` claim (auth boundary)
- `?pillar=` query for UX convenience only
- Plain textarea for Phase 5 (rich editor deferred)
- Live save (no draft workflow)

---

### ADR-027: Default Pillar Content Seeding Strategy
**Status:** Accepted | **Author:** Trinity | **Date:** 2026-05-20

StrategyHub users encountered empty pillar pages on first login (poor UX). Each pillar seeded with meaningful default content on tenant creation + backfill via migration. Each pillar: Title + Subtitle (1-sentence value prop) + BodyJson (1 strategy block with 3 actionable points, 3 execution steps, empty arrays for templates/examples/metrics). Conservative scaffolding—generic, widely applicable, non-invented advice. Implementation: `AdminBootstrapHostedService.GetDefaultPillarContent` (switch expression), `SeedPillarContentForTenantAsync` (calls GetDefaultPillarContent instead of empty body), Migration `20260520025400_SeedDefaultPillarContent` (UPDATE existing rows where body empty/placeholder). Defense-in-depth: `PillarContentService.GetAllForTenantAsync` lazy-seeds if seeder/migration fails. Pillar content: Brand (Define/Audit/Guidelines/Train), Customer (Know/Personas/Journey/Measure), Offering (Structure/Document/Bundle/Renew), Communication (Orchestrate/Map/Calendar/Track), Sales (Systematize/Process/CRM/Train), Management (Drive/Roles/KPIs/Reviews). Positive: new users see helpful scaffolding; reduced first-login emptiness; provides concrete examples. Negative: generic content may not match every tenant's industry; users still customize. Migration idempotent (only updates matching rows).

**Key decisions:**
- Bootstrap seeder + migration-based backfill
- Generic scaffolding (no invented domain expertise)
- Lazy self-healing in PillarContentService
- Migration targets empty/placeholder rows only (idempotent)

---

### Morpheus — Build & Deployment Date Tracking (Assembly + Env Var)
**Author:** Morpheus (Lead & Architect)  
**Date:** 2026-05-20  
**Status:** Recommended (awaiting implementation decision)  
**Scope:** Backend exposure of build and deployment timestamps  

User requested visibility into last build/deployment dates in the web interface. For a 2–5 user internal SaaS app with a single prod deployment pipeline, this decision recommends exposing timestamps via a lightweight endpoint and optional footer UI using assembly metadata + GitHub Actions environment variable (pushed to App Settings).

**Architecture Decision: Option 1 — Assembly Build Time + Azure Deployment Time**

**Source of Truth:**
- Build timestamp: Embedded in assembly at compile time via `[AssemblyInformationalVersion]` or custom build attribute
- Deployment timestamp: Retrieved from environment variable set by deploy-app.yml (GitHub Actions context)

**Implementation:**
- Backend: New scoped service `IDeploymentInfoService` reads build timestamp from assembly reflection and deployment time from environment variable
- Endpoint: `GET /api/deployment-info` (optional Bearer auth; if user authenticated, returns full info; if anonymous, returns minimal build-time only)
- UI: Optional footer component in `MainLayout.razor` showing "Last deployed: [time]" and "Built: [time]"

**Why This Approach (vs. Other Options):**
1. **Minimal dependencies:** No Azure SDK, no database, no scheduled jobs
2. **Single source per concern:** Build time immutable in binary, deploy time atomic environment variable set once per pipeline
3. **Transparent to operators:** Deployment date visible in both Azure Portal and app UI with no drift
4. **Low operational risk:** Safe fallback if env var missing (returns "unavailable", app still boots)
5. **Good for 2–5 user app:** Suffices for small team without multi-environment complexity

**Options Rejected:**
- **Option 2 (Azure Deployment History API):** MEDIUM effort (4–5 hours); promoted as fallback if app grows to multiple environments
- **Option 3 (Database Audit Table):** LARGE effort (6–8 hours); over-engineered, adds schema maintenance overhead

**Effort:** SMALL (2–3 hours)
- Add one environment variable to deploy-app.yml
- Create `IDeploymentInfoService` + registration in `Program.cs`
- Wire endpoint in `ApiEndpointExtensions`
- Optional: Razor footer component

**Security:** ✅ Non-sensitive metadata; low leakage risk; does not expose git commit hash, branch, or artifact paths

**Implementation Deliverables:**
1. Environment Variable: `DEPLOYMENT_TIMESTAMP: ${{ github.run_started_at }}` in deploy-app.yml
2. Service interface: `IDeploymentInfoService` with `(DateTime BuildTime, DateTime? DeploymentTime) GetTimestamps()`
3. Endpoint: `GET /api/deployment-info` returning `{ buildTime: "2026-05-20T21:30:00Z", deploymentTime: "2026-05-20T21:35:00Z" }`
4. Optional UI: Footer in `MainLayout.razor` displaying human-readable deploy time

**Next Steps:**
- Implementer (Neo or Trinity): Wire Option 1 per deliverables
- QA (Tank): Add smoke test verifying `/api/deployment-info` returns valid timestamps
- Review (Morpheus): Verify assembly reflection approach, env var plumbing is secure

**Related Decisions:** morpheus-dual-auth-scheme.md (Bearer for `/api/*` calls); Program.cs middleware pipeline (no changes required)

---

### Tank — Build/Deploy Metadata Visibility Pattern (App Settings)
**Author:** Tank (DevOps & QA)  
**Date:** 2026-05-20  
**Status:** Recommended (awaiting implementation approval)  
**Scope:** Ops visibility, metadata capture in deployment workflow  

Operators and users need visibility: what commit hash is running? When was the app built? When was the app deployed? Currently, `/health` endpoint returns only `{ status, timestamp }` (current time); it does not capture deployment history.

**Decision: Pattern 2 — Deployment Metadata Pushed to App Settings** ✅

**Pattern Comparison:**

| Pattern | Approach | Reliability | Complexity | Selection |
|---------|----------|-------------|-----------|-----------|
| 1 | Assembly Build Info Baked at Build Time | ⭐⭐⭐⭐⭐ | Medium | Build ≠ deploy time; can't track history separately |
| 2 | Deployment Metadata Pushed to App Settings | ⭐⭐⭐⭐⭐ | Low | ✅ **SELECTED** |
| 3 | Runtime Query from GitHub/Azure APIs | ⭐⭐ | High | Slow, fragile, unnecessary complexity |

**Why Pattern 2 (Recommended):**
1. **Aligns with team architecture:** App Service config pattern already proven in Bicep (appservice.bicep:50–69)
2. **Immediate operational visibility:** Ops see in Azure Portal; no API query needed
3. **Minimal attack surface:** Metadata (commit hash, timestamps) is not sensitive
4. **Perfect for small team (2–5 users):** Removes manual debugging ("what version is live?")
5. **Zero ongoing overhead:** One-time write at deploy; read-only thereafter; no cache invalidation

**Implementation (Sketch):**

GitHub Actions (deploy-app.yml) — After deployment step:
```bash
az webapp config appsettings set \
  --name $WEBAPP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    BUILD_COMMIT="${{ github.sha }}" \
    DEPLOYED_AT="$(date -Iseconds)"
```

App code (`/health` or `/version` endpoint):
- Read `BUILD_COMMIT`, `DEPLOYED_AT` from `IConfiguration`
- Return in JSON response

Bicep (optional; for documentation):
- Add AppSettings outputs showing possible metadata keys

**Next Steps:**
1. Await Coordinator decision on implementation priority
2. If approved: Neo to implement `/version` endpoint; Tank to add workflow step
3. Test in dev environment before prod rollout

**Audit Trail:** 3 patterns evaluated; 2–5 concurrent users; current health endpoint exists (returns only status + current timestamp); manual Azure App Service deployment + GitHub Actions workflow; stakeholders: Ops (visibility), QA (test metadata), Neo (implementation)

---

### Trinity — Build & Deployment Metadata UI Placement (Sidebar Footer)
**Author:** Trinity (Frontend & Design)  
**Date:** 2026-05-20  
**Status:** Recommended (awaiting implementation decision)  
**Scope:** StrategyHub UI — metadata display and role-based visibility  

How to surface "Last build" and "Last deployment" infrastructure metadata in StrategyHub without adding visual noise, while respecting role-based visibility and design system constraints?

**Decision: Sidebar Footer with Admin-Only Visibility** ✅

**Placement: Sidebar Footer**
- Primary: Embed in `SectionSidebar.razor` footer, below user info card and "Sign Out"
- Rationale: Low-noise zone already established as metadata zone; visible to all authenticated users but non-intrusive; aligns with ops/admin workflows (admins glance while navigating); mobile-responsive

**Component Shape: Compact Status Row**
```html
<div class="metadata-row">
  <span class="metadata-label">Last Build</span>
  <span class="metadata-value" title="2h 34m ago">2h ago</span>
</div>
<div class="metadata-row">
  <span class="metadata-label">Last Deploy</span>
  <span class="metadata-value" title="15m 22s ago">15m ago</span>
</div>
```

Style: Tiny, muted, monospace-friendly
- Font: `var(--text-xs)` (0.75rem)
- Color: `var(--text-muted)` (slate-400)
- Spacing: `var(--space-1)` vertical, `var(--space-2)` horizontal
- No animation, no hover state, no color emphasis

**Role Visibility: Admin-Only**
- Show to: `SuperAdmin` or `TenantAdmin`
- Hide from: `Reviewer`, `Viewer`
- Rationale: Operational metadata is not a StrategyHub business concern; admins managing infrastructure need quick visibility; other roles don't

**Time Format Convention**

Display Format (Client-Side): Relative time with tooltip fallback

| Scenario | Display | Tooltip (on hover) |
|----------|---------|-------------------|
| Just now | "now" | "less than 1m ago" |
| < 1 hour | "23m ago" | "2026-05-20 19:35:22 UTC" |
| < 24 hours | "3h ago" | "2026-05-20 18:35:22 UTC" |
| 1+ days | "2d ago" | "2026-05-18 20:35:22 UTC" |

Storage/API: Always UTC ISO-8601 (`2026-05-20T20:35:22Z`)

Refresh: Fetch on page load, cache locally. **Do NOT poll** — auto-refreshes require user action (F5) or explicit "Refresh" button if persistence is needed.

**Anti-Patterns to Avoid**

❌ **Do NOT:**
- Fetch via polling or WebSocket (burden on backend, noisy SignalR messages)
- Use animations or color-coded status (metadata is secondary, not a warning)
- Hardcode hex colors or inline styles (use token system)
- Show on high-traffic pages (Dashboard, ReviewerQueue, AuditDetail)
- Use bold or large fonts (diminishes visual hierarchy)
- Make timestamps clickable or interactive (read-only metadata only)
- Auto-refresh on timer (stale data without refresh context is worse than honest staleness)

Why: StrategyHub value is in playbooks, not deployment status. Ops metadata belongs in a dedicated ops dashboard. Polling/animation can confuse screen readers. Many clients polling for rarely-changing data increases API bloat.

**Implementation Notes**

Files to touch:
- `SectionSidebar.razor` — add metadata section in footer
- `SectionSidebar.razor.css` — add `.metadata-row`, `.metadata-label`, `.metadata-value` classes using `var(--text-xs)`, `var(--text-muted)`
- (Morpheus/Neo) API service: expose `GetBuildMetadataAsync()` returning `{ buildTimestamp: DateTime, deploymentTimestamp: DateTime }`

No changes needed:
- `tokens.css` (reuse existing semantic tokens)
- Other pages/components (metadata isolated to sidebar)

**Secondary Placements (Lower Priority)**

If sidebar footer proves insufficient:
- Small expandable badge in topbar (right-aligned, after user menu)
- Dedicated read-only "System Status" micro-panel in TenantAdminPanel
- Never on Dashboard, PillarPage, or public-facing screens

**Next Steps (If Approved)**
1. **Morpheus/Neo:** Define API contract for `GetBuildMetadataAsync()` and wire to CI/CD artifact metadata
2. **Trinity (next session):** Implement `BuildMetadataPanel.razor` component + update `SectionSidebar.razor` with `<AuthorizeView>` gate
3. **Test:** Ensure role-based visibility works, time format renders correctly, no polling errors

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
