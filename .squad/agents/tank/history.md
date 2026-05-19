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
- **2026-05-17 — Azure Deployment Guide:** Added `docs/deployment/AZURE-DEPLOYMENT-GUIDE.md` as the new canonical manual deployment walkthrough. The guide is portal-first, explicitly forbids GitHub Actions for Azure deployment, uses ZIP deploy for App Service, calls out that `HubSpot--WebhookSecret` can be read directly from Key Vault once `KeyVault__Uri` is configured, and documents that EF tooling currently uses `DESIGN_TIME_CONNECTION_STRING` for migrations rather than `ConnectionStrings__AdminConnection`.
- **2026-05-17 — Bicep Deployment Errors Fixed (3):** (1) `microsoft.insights/components` metric `dependencies/failed` only accepts `timeAggregation: 'Count'` — not `'Total'`. `'Total'` is valid for `Microsoft.Web/sites` metrics but NOT Application Insights component metrics. Fix: change the `dependencyFailuresAlert` resource in `main.bicep` only. (2) & (3) `eastus2` can be restricted for PostgreSQL Flexible Server and AI Search provisioning — always add an explicit `param location` override in both `prod.bicepparam` and `dev.bicepparam` (set to `'centralus'`) rather than relying on `resourceGroup().location`. Resources can deploy to any region regardless of their resource group's region.
- **2026-05-17 — PostgreSQL 16 pgBouncer Enablement:** Azure Database for PostgreSQL Flexible Server v16 does not support a `connection_pooling` configuration parameter. For this repo, the runtime connection string uses port `6432`, so the correct Bicep fix is to set `Microsoft.DBforPostgreSQL/flexibleServers/configurations` name `pgbouncer.enabled` with value `'on'` in `infra/modules/postgres.bicep`. Validate with `az bicep build --file infra/main.bicep`.
- **2026-05-17 — pgbouncer.enabled correct value:** Azure PostgreSQL Flexible Server rejects `'on'` for the `pgbouncer.enabled` configuration parameter. The only allowed values are `'True'` and `'False'` (capital T/F, string). Always use `'True'` (not `'on'`, `'true'`, or `true`) when enabling pgBouncer via Bicep configurations resource.

- **2026-05-17 — Comprehensive Bicep Audit (this session):**
  - `linuxFxVersion` for .NET 10 on Linux App Service must be `'DOTNET|10.0'` (not `'DOTNETCORE|10.0'`). The `DOTNETCORE` prefix was used for .NET Core 1–3 and early .NET 5; from .NET 6+ the canonical prefix is `DOTNET`. Using the wrong prefix causes an invalid runtime stack error at deploy time.
  - `require_secure_transport` PostgreSQL GUC value must be lowercase `'on'` or `'off'`. The Azure PostgreSQL Flexible Server ARM API is case-sensitive for configuration values. `'ON'` is rejected at runtime even though PostgreSQL itself is case-insensitive.
  - **CRITICAL LESSON: Always run `az deployment group validate` — not just `az bicep build`.** `az bicep build` validates syntax only. ARM-level validation (`az deployment group validate`) catches runtime parameter errors, invalid enum values, and incompatible resource property combinations. Multiple bugs in this project would have been caught by ARM validation and were only discovered reactively at deploy time.
  - Full audit checklist → `.squad/skills/azure-bicep-validation/SKILL.md`

- **2026-05-18 — Bicep Drift Prevention (SeedAdmin settings, PR #39):**
  - **Standing rule:** Any time a manual Azure change is made (Portal, az CLI, or otherwise) that is not yet reflected in Bicep, Tank must proactively open a Bicep PR to codify it — do NOT wait for Chris to ask. Manual changes that survive in Bicep are safe; manual changes not in Bicep are a time-bomb waiting for the next `deploy-infra` run to wipe them.
  - `SeedAdmin__Email` and `SeedAdmin__Password` KV references were missing from `infra/modules/appservice.bicep`. Chris had manually set them on `app-sixtofix-prod` to wire the bootstrap seeder, but the next `deploy-infra` would have overwritten the entire `appsettings` block and deleted them.
  - Added both as `@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SeedAdmin--Email/Password)` following existing KV-reference pattern. `SeedAdmin__Enabled` was already present.
  - PR #39: `infra(bicep): add SeedAdmin app settings to prevent drift`

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

- **2026-05-17 — Prod 401 root cause (post-redeploy):** Site was returning `HTTP 401 + WWW-Authenticate: Bearer` on every Blazor page (`/`, `/dashboard`). App was healthy: container `appsvc/dotnetcore:10.0` warm, `/health` 200, `/login` 200, Easy Auth confirmed OFF. The 401 was the app's own JwtBearer challenge. Because `JwtBearer` is the only auth scheme registered and the Blazor pages use `[Authorize]` mapped via `MapRazorComponents`, the endpoint authorization middleware calls `ChallengeAsync` against the default scheme BEFORE Blazor SSR renders. Result: the `<NotAuthorized><RedirectToLogin/></NotAuthorized>` flow in `Routes.razor` never runs. Fix: override `JwtBearerEvents.OnChallenge` in `Program.cs` to 302 to `/login?returnUrl=...` for HTML/browser navigations (path not under `/api` AND `Accept` includes `text/html` or `Sec-Fetch-Mode: navigate`). API/XHR callers keep 401 unchanged. PR #28 merged, deploy run 26011803996 succeeded, verified: `curl -H 'Accept: text/html' /` → 302 to `/login?returnUrl=%2F`.
- **2026-05-17 — linuxFxVersion correction (supersedes earlier note):** For .NET 10 on Azure Linux App Service the working value is `DOTNETCORE|10.0`. Earlier history claimed `DOTNET|10.0` was canonical; that value silently falls back to a PHP container (returns 403 from nginx). Source of truth is the actual running container: `appsvc/dotnetcore:10.0_*.tuxprod`. Always validate with `curl /health` after deploy.
- **2026-05-17 — Deploy verification rule:** A workflow `success` status only means the artifact landed. It does NOT prove the app is reachable. Always probe `GET /`, `GET /health`, and at least one `[Authorize]` route after every deploy. Add this to the deploy-app workflow as a smoke step. Expect `GET /` to be 200 or 302 (NEVER 401, NEVER from a non-Kestrel server).

- **2026-05-17 — Phase Prevention (dev/phase-prevention-tank):** Implemented three guardrails after linuxFxVersion and JwtBearer-401 prod bugs:
  1. `smoke-prod` job in deploy-app.yml — post-deploy probes `GET /` (expect 302, not 403, not nginx), `GET /health` (expect 200), `GET /api/audit-runs anon` (expect 401). Retries with exponential backoff for cold-start.
  2. `AuthContractTests.cs` in `SixToFix.Api.Tests` — `[Trait("Category","Contract")]` WebApplicationFactory tests assert unauthenticated GET / → 302 to /login and GET /api/audit-runs → 401. Runs in the standard PR filter.
  3. `linuxFxVersion` guardrail in `deploy-infra.yml` — validates the value starts with `DOTNETCORE|` before any Bicep deploy; fails with an actionable error message on mismatch. Also corrected the actual value in `infra/modules/appservice.bicep`: `'DOTNET|10.0'` → `'DOTNETCORE|10.0'`.
  Prevention checklist: `.squad/decisions/inbox/tank-401-prod-fix.md`.

- **2026-05-18 — Postgres Burstable Downsize (dev/phase-postgres-burstable, PR #38):**
  - `infra/modules/postgres.bicep` prod SKU changed: `Standard_D4s_v3`/`GeneralPurpose` → `Standard_B2ms`/`Burstable`. Expected savings ~$700–$820/month.
  - **Azure constraint:** Burstable tier does NOT support HA (`SameZone` or `ZoneRedundant`). HA must be `Disabled` for all environments when using Burstable. Attempting to deploy Burstable + any HA mode will fail at ARM validation.
  - **Azure constraint:** Geo-redundant backups (`geoRedundantBackup: 'Enabled'`) are only available on General Purpose and Memory Optimized tiers. Burstable tier only supports LRS — `geoRedundantBackup` must be `'Disabled'`.
  - **Portal prerequisite for in-place downgrade:** Azure blocks a GeneralPurpose → Burstable SKU change while HA is enabled. Chris must disable HA in the Portal first, then change the SKU, before running `deploy-infra`. PR #38 contains the manual checklist.
  - **Duplicate module deleted:** `infra/bicep/` (10 files) was a parallel Bicep tree with zero references in any workflow, CI job, or docs. Active deploy tree is `infra/main.bicep` + `infra/modules/` only. The duplicate postgresql.bicep had stale prod SKU values. Deleted to prevent future confusion.
  - Decision note: `.squad/decisions/inbox/tank-postgres-burstable-downsize.md`.

- **2026-05-18 — KV Reference Diagnosis (prod `Jwt__SigningKey` error):**
  - **Root cause confirmed:** The App Setting `Jwt__SigningKey` references `Jwt--SigningKey` in `kv-sixtofix-prod`, but that secret **does not exist** in Key Vault. The Portal error icon is exactly this — a 404 on the secret. Fix: `az keyvault secret set --vault-name kv-sixtofix-prod --name Jwt--SigningKey --file <path-to-signing-key>`.
  - **Two other broken KV references:** `AzureOpenAI--ApiKey` and `HubSpot--PrivateAppToken` are also referenced in App Settings but missing from KV. They will also show error status in Portal. These need to be set once credentials are available.
  - **Managed identity RBAC is correct:** App Service principal `2bcf226b-0f3f-4312-aa16-b378da34aa9e` has `Key Vault Secrets User` role scoped to `kv-sixtofix-prod` — not the problem.
  - **KV is RBAC mode** (`enableRbacAuthorization: true`). Access policy panel in Portal is irrelevant; role assignments are the authority.
  - **Seeder wiring gap:** Chris added KV secrets `SeedAdmin--Email` and `SeedAdmin--Password` (both enabled, no expiry ✅), and `SeedAdmin__Enabled=true` app setting is correct. BUT the App Settings `SeedAdmin__Email` and `SeedAdmin__Password` (the KV reference pointers) were never added. Seeder will not receive credentials until those two app settings are created.
  - **Chris's "SeedAdmin--Email is set to true" confusion:** This setting does NOT appear in the App Settings list. The KV secrets page has an "Enabled" toggle — Chris likely toggled the secret itself (which is correct, enabled=true) rather than creating a `SeedAdmin--Email=true` app setting. No incorrect app setting was persisted.
  - **App is running fine:** Startup probe succeeded (12s), `Site started` status confirmed. KV resolution errors are lazy (portal-side resolution status), not startup crashes. App passes health check.
  - **Fix commands (Chris to run):**
    ```
    # 1. JWT signing key (most critical — causes auth failures)
    $key = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object { [char]$_ })
    az keyvault secret set --vault-name kv-sixtofix-prod --name Jwt--SigningKey --value $key

    # 2. Seeder app settings (KV references — run as-is, safe to run)
    az webapp config appsettings set --resource-group rg-sixtofix-prod --name app-sixtofix-prod --settings `
      "SeedAdmin__Email=@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=SeedAdmin--Email)" `
      "SeedAdmin__Password=@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=SeedAdmin--Password)"

    # 3. AzureOpenAI and HubSpot secrets — set when credentials are ready
    # az keyvault secret set --vault-name kv-sixtofix-prod --name AzureOpenAI--ApiKey --value <key>
    # az keyvault secret set --vault-name kv-sixtofix-prod --name HubSpot--PrivateAppToken --value <token>
    ```
  - **Bicep follow-up:** `infra/modules/appservice.bicep` app settings block should include `SeedAdmin__Email` and `SeedAdmin__Password` KV references. They are currently absent. Add them to match live config before next `deploy-infra` run (otherwise Bicep will DELETE the manually-added settings on next deploy).
  - Decision note: `.squad/decisions/inbox/tank-keyvault-secret-naming.md`.
