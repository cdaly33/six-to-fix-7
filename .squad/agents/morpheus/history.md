# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT (custom tenant claims), EF Core, Azure PostgreSQL Flexible Server (pgBouncer on 6432), Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service (B2/P2v3), Azure Key Vault (managed identity), Azure Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — app issues its own tokens with `tenant_id`, `tenant_slug`, `roles` claims. No OIDC server (Duende/OpenIddict not used).
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase 0 — Architectural Decisions Locked (2026-05-10)

**Service Lifetimes:**
- `PolicyEngine` is Singleton — pure stateless functions, no DB, thread-safe. All other 7 domain services are Scoped (one per request/circuit). `IAIClient`, `IBlobStorage`, `ISearchClient`, `IHubSpotClient` are Transient via `IHttpClientFactory` with Polly pipelines attached at factory registration. `IHubContext<AuditRunHub>` is Singleton. `HubSpotBackgroundWorker` is a Singleton `IHostedService` that creates its own `IServiceScope` per event for `DbContext` access.
- `AddDbContextPool` is explicitly NOT used — it is incompatible with per-request query filters that capture `ITenantContext` instance state. Plain `AddDbContext<SixToFixDbContext>` is used.

**Tenant Isolation Approach:**
- EF Core `HasQueryFilter` is the single enforcement point. Applied to all 13 tenant-scoped entity types in `SixToFixDbContext.OnModelCreating`. `tenant_id` is captured from `ITenantContext` (Scoped) at `DbContext` construction time.
- `TenantResolutionMiddleware` runs AFTER `UseAuthorization` — unauthenticated requests are rejected before tenant resolution. A `MigrationDbContext` subclass (injecting `TenantContext.None`) is used for `dotnet ef migrations` so tooling never requires a tenant context.
- SuperAdmin cross-tenant queries use a dedicated `SuperAdminDbContext` without global filters. Any use of `IgnoreQueryFilters()` in non-SuperAdmin paths is a critical security finding.

**SignalR Contract:**
- Hub: `/hubs/audit-run`. Clients join a group keyed by `auditRunId`. 7 event types: `skill-started`, `skill-completed`, `skill-failed`, `council-started`, `council-completed`, `run-completed`, `run-failed`. Hub validates tenant ownership in `JoinRun` before adding connection to group. JWT passed via `?access_token=` query string for WebSocket connections.
- Missed events on reconnect are NOT replayed. Client must re-call `JoinRun` and refetch run state via service/API to resync.
- Azure App Service sticky sessions are required for Blazor Server circuits.

**Policy Engine Extensibility:**
- `IPolicyRule` interface: `string RuleName` + `PolicyFlag? Evaluate(CategoryPayload, AuditContext)`. Rules are registered explicitly as `AddSingleton<IPolicyRule, ConcreteRule>()`. `PolicyEngine` receives `IEnumerable<IPolicyRule>`. Rules execute in parallel via `Parallel.ForEach`. A rule that throws is logged at ERROR and produces no flag — it never aborts the audit run.
- All `PolicyFlag` evaluations (including Warnings) are persisted to the `policy_flags` table.
- Benchmark median/stddev context is pre-loaded by `AuditOrchestrator` and passed into `AuditContext` — PolicyEngine itself makes no DB calls.

**AI Council State Machine:**
- PolicyEngine evaluates per-skill (after each skill completes), not post-chain.
- Council escalation is synchronous within the audit run — `AuditOrchestrator` awaits `CouncilRunner.RunAsync` before proceeding to the next skill. The run does not complete until all triggered categories have a `CouncilDecision`.
- Any skill failure (schema validation, timeout, circuit open, API error) causes immediate chain abort. `AuditRun` → `failed`. No partial-success continuation.
- `SkillRunner` never throws for expected AI failures — returns `SkillRunResult` with `Status = Failed`. Unexpected exceptions propagate to `AuditOrchestrator`'s top-level handler.

**Reviewer Lockout Semantics:**
- Lockout scoped to `(tenant_id, audit_run_id, category_id)`. Tracked in `reviewer_rejections` table. 24-hour window is rolling (`now() - 24h`), not reset-on-rejection.
- Race condition handled via `pg_advisory_xact_lock` + `ISOLATION LEVEL SERIALIZABLE` transaction. The lock key is derived from `hashtext(audit_run_id + '|' + category_id)`.
- Lockout is a computed state (count query), not a persisted record. No background job needed — expires naturally as rejections age out of the 24h window.
- `CalibrationDelta` is NOT created when a lockout is triggered — no override occurred.
- `lockoutExpiresAt` is included in the 409 response body (earliest rejection in window + 24h).
- Advisory lock used is `pg_advisory_xact_lock` (transaction-scoped) — compatible with pgBouncer transaction-mode pooling.

### 2026-05-10 — Phase 0 Sealed

**All 12 Chris decisions consolidated into canonical decisions.md (21,203 bytes).** 15 inbox files merged and deleted. Orchestration logs written per agent (oracle, trinity, tank). Session log: `.squad/log/2026-05-10T21_28_46Z-phase0-resolution.md`. All team history.md files appended with Phase 0 seal notification. Phase 1 gate: **CLEAR.**

### 2026-05-10 — Phase 3 Cross-Agent Gap Review Completed

**Reviewed PRs #11 (Neo) and #12 (Oracle + Trinity). Branches had zero file overlap — clean merge guaranteed.**

All agent self-reported gaps were already resolved. Morpheus cross-referenced the implementation against `docs/architecture/hubspot-field-mapping.md` (locked spec) and found 5 real functional gaps:

1. **HubSpotClient only pushed 2 of 11 required fields** — fixed by adding `AuditPublishScores` record and expanding `UpdateAuditResultAsync` to push all 11 fields.
2. **Publisher.GetSkillScoreAsync read `ConfidenceScore` instead of `ActivityScore`** — fixed; `ActivityScore` correctly maps to `systems_maturity_score` (0–20) for SystemsMaturity and `ai_readiness` (0–100) for AiReadiness.
3. **Publisher passed `clientSlug` as HubSpot company ID** — fixed; now uses `client.HubSpotCompanyId` with slug fallback.
4. **SkillRunner did not capture `ai_readiness` from `derive-tier`** — fixed with third ActivityScore fallback.
5. **SkillRunner `confidence_scores` sub-object (rubric) was never captured** — fixed with `ReadAverageFromObject` helper averaging per-area confidence scores.

**Deferred to Phase 4:** YAML runtime loading (see `morpheus-yaml-loading.md`); per-category SkillRun architecture for PolicyEngine alignment.

### 2026-05-11 — Phase 4+5 Cross-Agent PR Review and Merge Completed

**PRs reviewed and merged: #15 (Tank — Phase 5 Infra+QA) then #16 (Trinity — Phase 4 UI)**

#### PR #15 — dev/phase-5-infra-qa (Tank)

**Issues found and fixed:**

1. **Bicep linter: hardcoded `@secure()` parameter default** — `postgresAdminPassword` had `= 'ChangeMe!RotateImmediately123'`. Per Azure best practices, `@secure()` parameters must have no default (forces callers to supply value via param files or pipeline secrets). Removed the default. Fix commit: `478a22a`.

**Verification:**
- Build: `dotnet build SixToFix.slnx -v q` → 0 errors, 2 × NU1904 (allowed pre-existing)
- Tests: `dotnet test --filter "Category!=Integration&Category!=E2E"` → **79 passed, 0 failed**
- E2E tests (`[Trait("Category", "E2E")]`) are correctly skipped with `[Fact(Skip = ...)]`
- Integration tests (`[Trait("Category", "Integration")]`) are Testcontainers-backed, correctly filtered out
- `clientAffinityEnabled: true` confirmed in `infra/modules/appservice.bicep` — SignalR sticky sessions intact

**Merged:** `c5e3401` — 61 files changed, +1982 −752

#### PR #16 — dev/phase-4-ui (Trinity)

**Issues found and fixed:**

1. **Missing Tank's test stabilization fix (60b2e8d)** — `SkillYamlValidationTests` used 4 directory levels (`..` × 4) to find repo root, but should be 5. Cherry-picked `60b2e8d` onto phase-4-ui. Conflicts resolved:
   - `Login.razor` — removed stale `LoginResponse` record (replaced by `Dictionary<string, string>`)
   - `Program.cs` — conflict on `using SixToFix.Web.Realtime` (not yet present in phase-4-ui); removed the spurious using
   - `GlobalUsings.cs` and `LoginPageTests.cs` — "deleted by us" (not in phase-4-ui); accepted the cherry-picked versions

2. **Missing Moq package in Web.Tests** — `LoginPageTests.cs` uses Moq but csproj only had NSubstitute. Added `Moq 4.*` to `SixToFix.Web.Tests.csproj`.

3. **GlobalUsings.cs referenced non-existent namespaces** — `SixToFix.Web.Realtime` and `SixToFix.Web.Tests.Fakes` don't exist in phase-4-ui. Removed them so branch builds cleanly.

Fix commits: `06661cf` (cherry-pick), `640cefe` (compatibility fixes)

**CSS law verification:**
- Zero hardcoded hex values in `.razor` files — confirmed via grep
- Zero inline `style=` attributes with color/spacing — confirmed via grep
- All styling via CSS custom properties — compliant

**Merge into main — conflicts resolved:**
- `infra/main.bicep` — both branches added it; kept main's version (Tank's with the required-param fix)
- `Program.cs` — kept `using SixToFix.Web.Realtime` from main (namespace exists post phase-5 merge)
- `GlobalUsings.cs` — kept main's version with Realtime and Fakes usings (both exist on main)

**Final state on main:**
- Build: 0 errors, 2 × NU1904 (allowed)
- Tests: **79 passed, 0 failed** (49 Infrastructure + 18 Web + 12 API)
- Merge commit: `5bc21b9`

### 2026-05-10 — Phase 6 Cross-Agent PR Review and Merge Completed

**PRs reviewed and merged: #17 (Neo — ClientService) then #18 (Oracle — YAML Loading)**

#### PR #17 — dev/phase-6-client-service (Neo)

**Issues found and fixed:**

1. **Interface and DTOs in wrong layer** — Neo placed `IClientService` in `Infrastructure/Interfaces/` and DTOs in `Infrastructure/Models/`. Convention is: all service interfaces go in `Application/Services/`, models in `Application/Models/`. Dependency arrow is Infrastructure → Application. Relocated all four files, updated namespaces and usings. Fix commit: `92f4e00`.

2. **Tenant assignment used parameter instead of ITenantContext** — `CreateClientAsync` set `TenantId = tenantId` (the parameter) instead of `_tenant.TenantId`. A buggy or malicious caller could assign a new client to a different tenant. Fixed to use `_tenant.TenantId` (same authoritative source as the EF Core global query filter). Fix commit: `92f4e00`.

**Verified clean:**
- Soft-delete: `IsActive = false` ✓
- Service lifetime: Scoped ✓ (injects SixToFixDbContext + ITenantContext, both Scoped)
- DTOs: no entity types at boundary ✓
- Structured logging: no string interpolation, no PII ✓
- Global query filter: correct single enforcement point, no redundant .Where(c => c.TenantId) ✓

**Merged:** `8b58a1b`

#### PR #18 — dev/phase-6-yaml-loading (Oracle)

**Issues found and fixed: None.** Oracle's implementation was architecturally correct.

**Verified clean:**
- `ISkillLoader` correctly in `Application/Services/` ✓
- `SkillLoader` Singleton: stateless after construction, no per-request context, safe for concurrent reads ✓
- Path-walking logic: walks up from ContentRootPath then AppContext.BaseDirectory to find `docs/skills/` ✓
- Inline fallback intact in `SkillRunner.GetSkillDefinitionAsync`: YAML failure → inline dict → SkillNotFoundException ✓
- `output_schema` YAML→JSON: `NormalizeYamlValue` recursive normalization → `JsonSerializer.Serialize` → valid JSON ✓
- YamlDotNet 17.x in both Infrastructure.csproj and Tests.csproj: no NU1605 ✓
- 5 new `SkillLoader_LoadAsync_ReturnsValidSkillDefinition` parameterized tests all pass ✓

**Merged:** `73bf103`

**Final state on main:**
- Build: 0 errors, 2 × NU1904 (allowed)
- Tests: **84 passed, 0 failed** (54 Infrastructure + 18 Web + 12 API)
- Merge commits: `8b58a1b` (PR #17), `73bf103` (PR #18)
- Fix commit: `92f4e00` (PR #17 architecture and security fixes)

**Architecture reinforced:** Interface location convention (`Application/Services/` for all service interfaces) is non-negotiable. Dependency direction Infrastructure → Application must be preserved. `CreateClientAsync` pattern: always use `_tenant.TenantId` for new entity tenant assignment, never the method parameter.

