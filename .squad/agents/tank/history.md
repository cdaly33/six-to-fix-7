# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform for marketing maturity audits. CI/CD gates and test coverage are product requirements.
- **Stack:** .NET 10 LTS, Azure Bicep, GitHub Actions (OIDC for Bicep deploy, zip deploy for app), Azure App Service (B2 dev / P2v3 prod), Azure PostgreSQL Flexible Server v16, Azure Key Vault (managed identity), Azure Blob Storage, Azure AI Search, Application Insights, Log Analytics; xUnit, bUnit, Testcontainers, Playwright
- **Azure resource group:** rg-StrategicGlue-CommandCenter
- **Resource naming pattern:** `{type}-strategicglue-{env}` (e.g., `psql-strategicglue-dev`, `kv-strategicglue-prod`)
- **Environments:** dev (auto-deploy on main push), prod (manual approval gate, release tag)
- **ARR Affinity:** Must be enabled — Blazor Server SignalR circuits require sticky sessions
- **pgBouncer:** PostgreSQL connections via port 6432, not default 5432
- **Test strategy:** xUnit (unit), bUnit (Blazor components), xUnit + Testcontainers (integration with real PostgreSQL), WebApplicationFactory (API/contract), Playwright (E2E — merge to main only). Coverage target: 80% domain logic. AI calls mocked at all layers.
- **Quality gate:** All tests pass + no new compiler warnings required to merge
- **4 workflows:** deploy-infra.yml, deploy-app.yml, validate-skills.yml, test.yml
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-05-10 — Phase 5:** The repo now treats `Category=Integration` and `Category=E2E` as the authoritative xUnit traits for workflow filtering, so PR-safe test runs should always exclude Docker-backed integration coverage and Playwright scaffolding with `Category!=Integration&Category!=E2E`.
- **2026-05-10 — Phase 5:** `AuditDetail.razor` uses an injected `IAuditRunHubClientFactory` seam for SignalR connections, which keeps the Blazor page testable without opening a real WebSocket and should remain the preferred pattern for future real-time UI tests.
- **2026-05-15 — Search Index Cleanup:** Removed `six-to-fix-skill-outputs` and `six-to-fix-calibration` indexes. Only `six-to-fix-evidence` remains. Updated: `infra/modules/search.bicep` (SKU comment), `infra/search-indexes/provision-indexes.ps1`, `AzureSearchClient.cs` (RequiredIndexes + BuildRequiredIndexes), `AzureSearchClientTests.cs`, and `docs/architecture/search-index-schema.md`. Identity role assignment is service-scoped (not index-scoped), so no change needed to `identity.bicep`. Bicep validated clean with `az bicep build`. Prod SKU stays Standard — evidence index uses semantic search + 2 replicas for HA, both of which require Standard tier.
- **2026-05-15 — Docs Synchronization:** Always check deployment docs for staleness when infrastructure changes happen. Updated `docs/deployment/NEXT-STEPS-FOR-CHRIS.md` to remove stale references to three Search indexes (now only one), changed SignalR reference to PeriodicTimer polling. The script output examples must reflect the actual behavior of `provision-indexes.ps1` — do not trust docs; verify against code. Commit: 4f32fef.
- **2026-05-15 — Secret Handling Fixes:** (1) `main.bicep` bootstrapSecrets: DefaultConnection must use `sf_app` + separate `sfAppPassword` @secure param — never `sfadmin`. Placeholder-only secrets (JWT, HubSpot, OpenAI) should be omitted from bootstrapSecrets entirely; empty KV secret slots are safer than placeholders that look valid. (2) Always verify Bicep secret names against the config keys the code actually reads. `Program.cs` is the source of truth: it reads `Jwt:SigningKey` → KV secret `Jwt--SigningKey`; `AiServiceExtensions.cs` reads `HubSpot:PrivateAppToken` → KV secret `HubSpot--PrivateAppToken`. (3) `az keyvault secret set --value` writes credentials to PSReadLine history (`ConsoleHost_history.txt`). Use `Read-Host -AsSecureString` for any command that handles real credentials. URL-only values (endpoints) are not credentials and inline `--value` is fine. (4) New `sfAppPassword` param in `main.bicep` requires adding `SF_APP_PASSWORD` to GitHub Secrets and passing it in `deploy-infra.yml`.

---

## Phase 0 — Planning Artifacts (2026-05-10)

**What I produced:** 4 locked planning artifacts in `docs/architecture/`. These govern all infrastructure, CI/CD, and test implementation work.

### Artifacts Written

| File | Description |
|------|-------------|
| `docs/architecture/environment-contract.md` | Complete App Settings schema (KV references vs direct values), exhaustive Key Vault secrets list (6 secrets), managed identity RBAC bindings for 4 Azure services, dev vs prod environment-specific values table, pgBouncer connection note (port 6432, transaction pooling, Npgsql caveats), ARR Affinity configuration for SignalR, startup validation pattern using `IOptions<T>` with data annotations + `ValidateOnStart()` |
| `docs/architecture/test-layer-matrix.md` | 5-layer test responsibility matrix (Unit / bUnit / Integration-Testcontainers / API-WebApplicationFactory / E2E-Playwright), mock boundary rules (always mock: ISkillRunner, IHubSpotClient, IBlobService; never mock: PostgreSQL, tenant_id isolation, JWT parsing), 5-project test structure, 80% coverage gate on Domain + Application assemblies only, Testcontainers base class with sf_admin/sf_app role split, mandatory cross-tenant isolation test pattern, workflow trigger table |
| `docs/architecture/managed-identity-wiring.md` | Why managed identity, DefaultAzureCredential chain for local dev vs prod, per-service wiring table (Key Vault/Blob/OpenAI/AI Search) with SDK class, RBAC role, and registration pattern, Key Vault transparent IConfiguration integration via AddAzureKeyVault, startup fail-fast auth probe, Bicep roleAssignment resources with well-known role definition IDs, local dev setup (az login steps) |
| `docs/architecture/github-actions-dag.md` | 4-workflow inventory with trigger conditions, full job DAG for each workflow (ci.yml: restore→build→unit-tests→coverage-gate + lint + security-scan; integration.yml: restore→build→integration-tests→coverage-report; e2e.yml: guard→deploy-staging→smoke-test→playwright-e2e→notify; deploy.yml: guard→validate-bicep→deploy-infra→deploy-app→health-check→notify), production environment protection (Chris approval required), GitHub secrets/variables table (OIDC — no service principal password), Azure OIDC federated credential configuration, NuGet + Playwright cache key patterns, artifact passing table, branch protection rules for main |

### Key Decisions Made

- **4 workflows, not 2:** Explicitly separated CI (PR gate), integration (push to main), E2E (merge to main), and deploy (after E2E). Each has a clear trigger and can fail independently.
- **`workflow_run` triggers for e2e.yml and deploy.yml:** Ensures E2E only runs after integration tests succeed, and deploy only runs after E2E succeeds. Guard jobs check `github.event.workflow_run.conclusion == 'success'`.
- **No SQLite in integration tests:** Testcontainers with real PostgreSQL 16 only. SQLite cannot reproduce PostgreSQL-specific behavior.
- **Mandatory cross-tenant isolation tests:** Every repository test file must assert that tenant A cannot see tenant B's data. Documented as a hard requirement.
- **sf_admin for migrations, sf_app for runtime queries — even in test fixtures:** Mirrors production role separation exactly.
- **80% coverage gate applies to Domain + Application only:** Infrastructure, API, and Blazor projects excluded from the gate (tested via integration/E2E layers).

### Open Questions Flagged (across all artifacts)

1. Azure OpenAI resource subscription placement (same subscription required for managed identity)
2. HubSpot auth model: Private App token vs OAuth 2.0 client secret
3. `sf-blob-storage-connstr` secret needed in prod Key Vault or dev-only?
4. Custom domain for prod JWT issuer (`https://app.strategicglue.com`)
5. AI Search role: Data Reader vs Data Contributor (does the app write to the index?)
6. E2E staging: dev environment vs dedicated slot vs separate staging environment
7. DB migration runner in deploy.yml: CI runner with direct DB access vs app-startup `MigrateAsync()`
8. Slack webhook for deploy notifications
9. `User Access Administrator` scope narrowing for GitHub Actions OIDC principal

## 2026-05-10 — Chris Phase 0 decisions applied

- Updated Phase 0 architecture docs with Chris's confirmed decisions for prod Key Vault blob fallback secret, prod custom domain + managed certificate, startup `MigrateAsync()` deploy behavior, dev-only E2E target, resource-group RBAC scope, Step Summary-only notifications, AI Search contributor access, and shared Testcontainer + per-test transaction rollback.

### 2026-05-10 — Phase 0 Sealed

**Status:** All Phase 0 questions resolved by Chris. 15 inbox files consolidated into canonical `decisions.md` (21,203 bytes).

**Decisions merged** include:
- HubSpot Private App token (Q1) — oracle
- Azure OpenAI same-subscription (Q2) — trinity
- 8 infrastructure decisions (Q3–Q10) — this decision
- JWT role confirmation (Q12) — trinity
- 9 architecture ADRs (Morpheus, Neo) — all locked

**Orchestration log written:** `.squad/orchestration-log/2026-05-10T21_28_46Z-tank.md`. Phase 1 gate: CLEAR.

- **2026-05-15 — Docs Synchronization Post Stack Simplification:** Updated docs/deployment/NEXT-STEPS-FOR-CHRIS.md to remove stale references after SignalR removal and search index cleanup. Three issues fixed: (1) AI Search indexes — reduced from 3 to 1; (2) Real-time mechanism reference — changed SignalR to PeriodicTimer polling; (3) Document currency — added update timestamp. Branch: dev/simplify-stack-signalr-search, Commit: 4f32fef. Learning: Always audit deployment docs when infrastructure changes occur; verify script output examples against actual code behavior in provision-indexes.ps1, etc.
