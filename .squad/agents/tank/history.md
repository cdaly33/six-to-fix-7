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
