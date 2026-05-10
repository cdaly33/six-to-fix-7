# Tank Phase 1 Infrastructure Decisions

**Author:** Tank (DevOps & QA)  
**Date:** 2026-05-10  
**Phase:** 1 — Infrastructure Scaffolding

---

## Decisions Confirmed and Implemented

### 1. Key Vault RBAC Model (Not Access Policies)
`infra/bicep/modules/keyvault.bicep` sets `enableRbacAuthorization: true`. Access policies are
disabled entirely. Role assignments are in `modules/rbac.bicep` using well-known built-in role
definition IDs from `managed-identity-wiring.md §6.5`.

**Key Vault SKU correction:** `environment-contract.md §4` specifies Premium SKU for prod
(HSM-backed keys). Implemented as `isProd ? 'premium' : 'standard'`.

### 2. ARR Affinity Enabled (SignalR Blazor Server)
`infra/bicep/modules/appservice.bicep` sets `clientAffinityEnabled: true` on
`Microsoft.Web/sites`. This is required for Blazor Server SignalR circuits — without sticky
sessions a reconnect can land on a different node and lose circuit state.
Per `environment-contract.md §6`.

### 3. AI Search Role: Search Index Data Contributor
`infra/bicep/modules/rbac.bicep` grants `Search Index Data Contributor`
(ID `8ebe5a00-799e-43f5-93ac-243d3dce84a7`) to the App Service managed identity on the AI Search
resource. The application **writes** to the index (confirmed Q9 in Phase 0 decisions).
`Search Index Data Reader` would be insufficient.

### 4. Azure OpenAI Role: Cognitive Services OpenAI User
`infra/bicep/modules/rbac.bicep` grants `Cognitive Services OpenAI User`
(ID `5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`). This is sufficient for inference calls.
`managed-identity-wiring.md §6.5` confirms this ID.

**Note:** The task template specified `Cognitive Services OpenAI Contributor`
(ID `a001fd3d-188f-4b5d-821b-7da978bf7442`). The architecture docs (`managed-identity-wiring.md`)
specify `Cognitive Services OpenAI User` instead. Implemented User role per architecture docs,
which is the least-privilege correct choice for inference-only access.

### 5. OIDC Authentication — No AZURE_CLIENT_SECRET
All GitHub Actions workflows that deploy to Azure use OIDC (workload identity federation) via
`azure/login@v2` with `client-id`, `tenant-id`, and `subscription-id`. No
`AZURE_CLIENT_SECRET` is stored anywhere. Per `github-actions-dag.md §4.1`.

### 6. GitHub Actions Workflow Structure
Implemented the exact 4-workflow DAG from `github-actions-dag.md §1`:
- `ci.yml` — PR gate: restore → build → unit-tests → coverage-gate + lint + security-scan
- `integration.yml` — push to main: integration tests with Testcontainers
- `e2e.yml` — `workflow_run` after Integration Tests succeed: guard → deploy-dev → smoke-test → playwright-e2e → notify
- `deploy.yml` — `workflow_run` after E2E succeeds: guard → validate-bicep → deploy-infra → deploy-app → health-check → notify

Guard jobs in `e2e.yml` and `deploy.yml` check `github.event.workflow_run.conclusion != 'success'`
and abort immediately, preventing deploys after failed upstream workflows.

### 7. PostgreSQL SKU Alignment
`infra/bicep/modules/postgresql.bicep` uses SKUs from `environment-contract.md §4`:
- Dev: `Standard_B2ms` / `Burstable`
- Prod: `Standard_D4s_v3` / `GeneralPurpose`
- Prod HA: `SameZone` (not `ZoneRedundant`)

The task template had slightly different SKU names (`Standard_B1ms`, `Standard_D2s_v3`). Used
the values locked in `environment-contract.md` as the authoritative source.

### 8. App Service Plan SKU Alignment
`infra/bicep/modules/appservice.bicep` uses:
- Dev: `B2` / `Basic` per `environment-contract.md §4`
- Prod: `P2v3` / `PremiumV3` per `environment-contract.md §4`

The task template had `B1`/`P1v3`. Used the locked environment-contract values.

### 9. TLS Version per Environment
Dev: TLS 1.2, Prod: TLS 1.3 — per `environment-contract.md §4`.

### 10. Test Projects Added to Solution
`dotnet sln SixToFix.slnx add` was run for all 6 test projects and **succeeded**:
- `tests/SixToFix.Domain.Tests/SixToFix.Domain.Tests.csproj` ✅
- `tests/SixToFix.Application.Tests/SixToFix.Application.Tests.csproj` ✅
- `tests/SixToFix.Infrastructure.Tests/SixToFix.Infrastructure.Tests.csproj` ✅
- `tests/SixToFix.Api.Tests/SixToFix.Api.Tests.csproj` ✅
- `tests/SixToFix.Web.Tests/SixToFix.Web.Tests.csproj` ✅
- `tests/SixToFix.E2E.Tests/SixToFix.E2E.Tests.csproj` ✅

`SixToFix.slnx` (created by Morpheus) was present. No coordinator follow-up needed.

### 11. SixToFix.Domain.Tests References Both Domain and Application
Per `test-layer-matrix.md §3.1`: `SixToFix.Domain.Tests` covers both `SixToFix.Domain` and
`SixToFix.Application`. The `.csproj` includes both project references. This differs slightly from
the task template (which only had Domain), aligned to the architecture doc's dependency map.

### 12. PostgresContainerFixture: sf_admin / sf_app Role Split
`tests/SixToFix.Infrastructure.Tests/Fixtures/PostgresContainerFixture.cs` implements the
sf_admin (migrations) / sf_app (runtime DML only) role split from `test-layer-matrix.md §5.3`.
sf_app gets SELECT, INSERT, UPDATE only — no DELETE, no DDL, matching production.

---

## Files Created

### Bicep Infrastructure
- `infra/bicep/main.bicep`
- `infra/bicep/modules/appinsights.bicep`
- `infra/bicep/modules/appservice.bicep`
- `infra/bicep/modules/keyvault.bicep`
- `infra/bicep/modules/openai.bicep`
- `infra/bicep/modules/postgresql.bicep`
- `infra/bicep/modules/rbac.bicep`
- `infra/bicep/modules/search.bicep`
- `infra/bicep/modules/storage.bicep`
- `infra/bicep/parameters/dev.bicepparam`

### GitHub Actions Workflows
- `.github/workflows/ci.yml`
- `.github/workflows/integration.yml`
- `.github/workflows/e2e.yml`
- `.github/workflows/deploy.yml`

### Test Projects
- `tests/SixToFix.Domain.Tests/SixToFix.Domain.Tests.csproj`
- `tests/SixToFix.Application.Tests/SixToFix.Application.Tests.csproj`
- `tests/SixToFix.Infrastructure.Tests/SixToFix.Infrastructure.Tests.csproj`
- `tests/SixToFix.Api.Tests/SixToFix.Api.Tests.csproj`
- `tests/SixToFix.Web.Tests/SixToFix.Web.Tests.csproj`
- `tests/SixToFix.E2E.Tests/SixToFix.E2E.Tests.csproj`

### Test Base Classes
- `tests/SixToFix.Infrastructure.Tests/Fixtures/PostgresContainerFixture.cs`
- `tests/SixToFix.Infrastructure.Tests/Fixtures/IntegrationTestBase.cs`
- `tests/SixToFix.Api.Tests/CustomWebApplicationFactory.cs`
