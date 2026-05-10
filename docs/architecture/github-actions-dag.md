# GitHub Actions DAG

> **Status: LOCKED — Phase 0 Planning Artifact**
>
> Owner: Tank (DevOps & QA) | Phase: 0 | Last updated: 2026-05-10

This document is the authoritative contract for all GitHub Actions workflows, job dependencies, triggers, secrets, and artifact flow. No workflow is built without reference to this plan.

---

## 1. Workflow Inventory

| Workflow File | Display Name | Trigger | Condition | Runs On |
|---------------|-------------|---------|-----------|---------|
| `ci.yml` | CI — PR Gate | `pull_request` (all branches → `main`) | Always on PR | `ubuntu-latest` |
| `integration.yml` | Integration Tests | `push` to `main` | Always on push to main | `ubuntu-latest` |
| `e2e.yml` | E2E — Playwright | `workflow_run` | After `integration.yml` completes with `success` on `main` | `ubuntu-latest` |
| `deploy.yml` | Deploy — Infra + App | `workflow_run` | After `e2e.yml` completes with `success` on `main` | `ubuntu-latest` |

### 1.1 Full Trigger Definitions

```yaml
# ci.yml
on:
  pull_request:
    branches: [main]

# integration.yml
on:
  push:
    branches: [main]

# e2e.yml
on:
  workflow_run:
    workflows: ["Integration Tests"]
    types: [completed]
    branches: [main]

# deploy.yml
on:
  workflow_run:
    workflows: ["E2E — Playwright"]
    types: [completed]
    branches: [main]
```

> `e2e.yml` and `deploy.yml` use `workflow_run` triggers so they only fire when the upstream workflow **succeeds**. Each workflow must check `github.event.workflow_run.conclusion == 'success'` in a conditional at the top of the job, or use a check job that fails fast if the conclusion is not success.

---

## 2. Job DAG — Per Workflow

### 2.1 `ci.yml` — PR Gate

**Goal:** Fast feedback on every PR. Must complete in under 10 minutes. No infrastructure access — all external calls mocked.

```
restore ─→ build ─→ unit-tests ──→ coverage-gate
                 └─→ lint
                 └─→ security-scan
```

| Job | `needs:` | Description | Fail Behavior |
|-----|---------|-------------|---------------|
| `restore` | — | `dotnet restore` with NuGet cache. Restores all projects in solution. | Fail PR |
| `build` | `restore` | `dotnet build -c Release --no-restore --warnaserror`. Zero compiler warnings policy. | Fail PR |
| `unit-tests` | `build` | `dotnet test` on `SixToFix.Domain.Tests` + `SixToFix.Blazor.Tests`. Runs with Coverlet coverage collection. Uploads raw coverage XML as artifact. | Fail PR |
| `coverage-gate` | `unit-tests` | Downloads coverage XML. Runs ReportGenerator. Asserts ≥ 80% line coverage on `SixToFix.Domain` + `SixToFix.Application`. Posts summary to PR as step summary. | Fail PR |
| `lint` | `build` | `dotnet format --verify-no-changes`. Fails if any file is not formatted. | Fail PR |
| `security-scan` | `build` | `dotnet list package --vulnerable --include-transitive`. Fails on any HIGH or CRITICAL vulnerability. | Fail PR |

**`ci.yml` approximate YAML structure:**

```yaml
jobs:
  restore:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: nuget-
      - run: dotnet restore

  build:
    needs: restore
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ hashFiles('**/*.csproj') }}
      - run: dotnet restore
      - run: dotnet build -c Release --no-restore --warnaserror
      - uses: actions/upload-artifact@v4
        with:
          name: build-output
          path: '**/bin/Release/**'
          retention-days: 1

  unit-tests:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ hashFiles('**/*.csproj') }}
      - run: dotnet restore
      - run: |
          dotnet test tests/SixToFix.Domain.Tests \
            --no-restore \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults
          dotnet test tests/SixToFix.Blazor.Tests \
            --no-restore \
            --results-directory ./TestResults
      - uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: TestResults/**/coverage.cobertura.xml

  coverage-gate:
    needs: unit-tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: coverage-report
          path: ./TestResults
      - uses: danielpalme/ReportGenerator-GitHub-Action@5
        with:
          reports: './TestResults/**/coverage.cobertura.xml'
          targetdir: './CoverageReport'
          reporttypes: 'MarkdownSummaryGithub;TextSummary'
      - run: |
          cat ./CoverageReport/Summary.txt
          # Assert threshold — ReportGenerator exits non-zero if below threshold
          reportgenerator \
            -reports:"./TestResults/**/coverage.cobertura.xml" \
            -targetdir:"./CoverageReport" \
            -reporttypes:TextSummary \
            -assemblyfilters:"+SixToFix.Domain;+SixToFix.Application" \
            -minimumcoverageThresholds:lineCoverage=80

  lint:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ hashFiles('**/*.csproj') }}
      - run: dotnet restore
      - run: dotnet format --verify-no-changes

  security-scan:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ hashFiles('**/*.csproj') }}
      - run: dotnet restore
      - run: dotnet list package --vulnerable --include-transitive 2>&1 | tee vuln-report.txt
      - run: |
          if grep -q "High\|Critical" vuln-report.txt; then
            echo "::error::High or Critical vulnerabilities found. See vuln-report.txt."
            exit 1
          fi
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: vulnerability-report
          path: vuln-report.txt
```

---

### 2.2 `integration.yml` — Integration Tests

**Goal:** Run real PostgreSQL integration tests and API tests on every push to main. Testcontainers spins up PostgreSQL 16 — no shared test database.

```
restore ─→ build ─→ integration-tests ─→ coverage-report
```

| Job | `needs:` | Description | Fail Behavior |
|-----|---------|-------------|---------------|
| `restore` | — | `dotnet restore` with NuGet cache | Fail workflow |
| `build` | `restore` | `dotnet build -c Release --no-restore --warnaserror` | Fail workflow |
| `integration-tests` | `build` | `dotnet test` on `SixToFix.Infrastructure.Tests` + `SixToFix.Api.Tests`. Testcontainers pulls `postgres:16-alpine` and manages the container lifecycle. Docker daemon must be available on runner. | Fail workflow |
| `coverage-report` | `integration-tests` | Downloads coverage XML + previous CI coverage XML. Generates combined HTML report. Uploads as artifact. | Warn only (non-blocking) |

**Key `integration-tests` job notes:**
- Docker is available on `ubuntu-latest` runners — no extra setup needed for Testcontainers.
- `TESTCONTAINERS_RYUK_DISABLED=true` is set to prevent Ryuk reaper issues on CI.
- Parallel test execution is limited (`-p 1` per project, or `[CollectionDefinition]` controls parallelism within xUnit).
- Each `[Collection("PostgreSQL")]` fixture starts one container shared across all tests in that collection.

```yaml
integration-tests:
  needs: build
  runs-on: ubuntu-latest
  env:
    TESTCONTAINERS_RYUK_DISABLED: 'true'
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.x'
    - uses: actions/cache@v4
      with:
        path: ~/.nuget/packages
        key: nuget-${{ hashFiles('**/*.csproj') }}
    - run: dotnet restore
    - run: |
        dotnet test tests/SixToFix.Infrastructure.Tests \
          --no-restore \
          --collect:"XPlat Code Coverage" \
          --results-directory ./TestResults \
          --logger "trx;LogFileName=infrastructure.trx"
        dotnet test tests/SixToFix.Api.Tests \
          --no-restore \
          --collect:"XPlat Code Coverage" \
          --results-directory ./TestResults \
          --logger "trx;LogFileName=api.trx"
    - uses: actions/upload-artifact@v4
      if: always()
      with:
        name: integration-test-results
        path: |
          TestResults/**/*.trx
          TestResults/**/coverage.cobertura.xml
```

---

### 2.3 `e2e.yml` — Playwright E2E

**Goal:** Full end-to-end user journey validation against the deployed dev environment. Runs only after integration tests pass on main.

```
[guard: check upstream success]
       ↓
deploy-staging ─→ smoke-test ─→ playwright-e2e ─→ notify
```

| Job | `needs:` | Description | Fail Behavior |
|-----|---------|-------------|---------------|
| `guard` | — | Fails workflow immediately if `github.event.workflow_run.conclusion != 'success'`. Prevents E2E running after a failed integration build. | Abort workflow |
| `deploy-staging` | `guard` | Deploys current `main` build to `app-strategicglue-dev` (dev environment used as staging). Uses Azure OIDC auth. | Fail workflow |
| `smoke-test` | `deploy-staging` | `curl https://app-strategicglue-dev.azurewebsites.net/health` — expects HTTP 200 within 60s. Retries 3x with 10s sleep. | Fail workflow |
| `playwright-e2e` | `smoke-test` | Installs Playwright browsers (`npx playwright install --with-deps chromium`). Runs full test suite in `SixToFix.E2E`. Uploads Playwright HTML report + traces as artifacts. | Fail workflow |
| `notify` | `playwright-e2e` | Posts result summary to GitHub Step Summary. On failure, creates a GitHub issue tagged `e2e-failure`. | Always runs (`if: always()`) |

```yaml
jobs:
  guard:
    runs-on: ubuntu-latest
    steps:
      - name: Check upstream workflow conclusion
        if: github.event.workflow_run.conclusion != 'success'
        run: |
          echo "Upstream integration workflow did not succeed. Aborting E2E."
          exit 1

  playwright-e2e:
    needs: smoke-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - name: Install Playwright browsers
        run: |
          cd tests/SixToFix.E2E
          dotnet build
          pwsh bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
      - name: Run Playwright tests
        env:
          PLAYWRIGHT_BASE_URL: https://app-strategicglue-dev.azurewebsites.net
        run: dotnet test tests/SixToFix.E2E --no-build
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: playwright-report
          path: tests/SixToFix.E2E/playwright-report/
          retention-days: 30
```

---

### 2.4 `deploy.yml` — Infrastructure + Application Deploy

**Goal:** Deploy Bicep infrastructure changes and the application to production. Requires human approval via GitHub Environment protection rules.

```
[guard: check upstream success]
       ↓
validate-bicep ─→ deploy-infra ─→ deploy-app ─→ health-check ─→ notify
```

| Job | `needs:` | Environment | Description | Fail Behavior |
|-----|---------|-------------|-------------|---------------|
| `guard` | — | — | Fails if upstream `e2e.yml` did not succeed. | Abort workflow |
| `validate-bicep` | `guard` | — | `az deployment group what-if` against `rg-StrategicGlue-CommandCenter`. Uploads what-if diff as artifact. | Fail workflow |
| `deploy-infra` | `validate-bicep` | `production` ← **requires approval** | `az deployment group create` with `main.prod.bicepparam`. Captures outputs (App Service name, Key Vault URI). | Fail workflow |
| `deploy-app` | `deploy-infra` | `production` | `dotnet publish -c Release`, zip deploy via `az webapp deploy`. Runs pending DB migrations before swap. | Fail workflow |
| `health-check` | `deploy-app` | — | `curl https://app-strategicglue-prod.azurewebsites.net/health` — expects `{"status":"healthy"}` within 90s (3 retries × 30s sleep). | Fail workflow (triggers rollback investigation) |
| `notify` | `health-check` | — | Posts deployment summary (version, timestamp, who approved) to GitHub Step Summary + Slack webhook. | Always runs |

```yaml
jobs:
  deploy-infra:
    needs: validate-bicep
    runs-on: ubuntu-latest
    environment: production   # ← Requires approval from Chris (see §3)
    permissions:
      id-token: write         # Required for OIDC token
      contents: read
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - run: |
          az deployment group create \
            --resource-group ${{ vars.AZURE_RESOURCE_GROUP }} \
            --template-file infrastructure/main.bicep \
            --parameters infrastructure/main.prod.bicepparam \
            --name "deploy-${{ github.run_number }}"
```

---

## 3. Environment Protection Rules

The GitHub **`production`** Environment must be configured with:

| Setting | Value |
|---------|-------|
| Required reviewers | Chris (StrategicGlue owner) |
| Prevent self-review | Yes |
| Wait timer | 0 minutes (no artificial delay) |
| Deployment branches | `main` only |
| Allow admins to bypass | No |

> The `deploy-infra` and `deploy-app` jobs both target the `production` environment. The approval gate fires once per run — if Chris approves, both jobs proceed. GitHub does not re-prompt for each job.

---

## 4. GitHub Actions Secrets and Variables

### 4.1 Secrets (Repository-level, encrypted)

| Secret Name | Purpose | Source |
|-------------|---------|--------|
| `AZURE_CLIENT_ID` | App Registration client ID for OIDC federated auth | Azure App Registration (see §5) |
| `AZURE_TENANT_ID` | Azure AD tenant ID | Azure Portal → Azure Active Directory |
| `AZURE_SUBSCRIPTION_ID` | Target Azure subscription ID | Azure Portal → Subscriptions |

> ⚠️ **No `AZURE_CLIENT_SECRET` is stored.** GitHub Actions authenticates to Azure via OIDC (workload identity federation) — no service principal password is used or stored. The `AZURE_CLIENT_ID` is the App Registration application (client) ID, **not** a secret.

### 4.2 Variables (Repository-level, plaintext — not secrets)

| Variable Name | Example Value | Purpose |
|---------------|---------------|---------|
| `AZURE_RESOURCE_GROUP` | `rg-StrategicGlue-CommandCenter` | Target resource group for all deployments |
| `AZURE_APP_SERVICE_NAME_DEV` | `app-strategicglue-dev` | Dev App Service name for E2E deploy |
| `AZURE_APP_SERVICE_NAME_PROD` | `app-strategicglue-prod` | Prod App Service name for production deploy |
| `AZURE_KEYVAULT_NAME_DEV` | `kv-strategicglue-dev` | Dev Key Vault name |
| `AZURE_KEYVAULT_NAME_PROD` | `kv-strategicglue-prod` | Prod Key Vault name |

### 4.3 Workflow Permissions

All workflows that interact with Azure must declare:

```yaml
permissions:
  id-token: write   # Required to request OIDC JWT from GitHub
  contents: read    # Required to checkout code
```

For workflows that post PR comments or create issues:

```yaml
permissions:
  id-token: write
  contents: read
  pull-requests: write   # Post coverage comments, etc.
  issues: write          # Create e2e-failure issues
```

---

## 5. Azure OIDC Setup (Workload Identity Federation)

GitHub Actions authenticates to Azure without a client secret using OpenID Connect (OIDC). This requires a one-time setup on an Azure App Registration.

### 5.1 Azure App Registration

Create one App Registration for CI/CD purposes:

| Property | Value |
|----------|-------|
| Display Name | `sp-github-actions-strategicglue` |
| Account Type | Single tenant |
| Client Secret | **None** — OIDC only |
| API Permissions | None (uses RBAC, not Graph API) |

### 5.2 Federated Credentials

Add the following federated credentials to the App Registration:

| Credential Name | Issuer | Subject | Audience | Used By |
|-----------------|--------|---------|----------|---------|
| `github-main-push` | `https://token.actions.githubusercontent.com` | `repo:cdaly33/six-to-fix-7:ref:refs/heads/main` | `api://AzureADTokenExchange` | `integration.yml`, `e2e.yml`, `deploy.yml` |
| `github-main-environment-production` | `https://token.actions.githubusercontent.com` | `repo:cdaly33/six-to-fix-7:environment:production` | `api://AzureADTokenExchange` | `deploy.yml` production environment jobs |
| `github-pr` | `https://token.actions.githubusercontent.com` | `repo:cdaly33/six-to-fix-7:pull_request` | `api://AzureADTokenExchange` | `ci.yml` (if any Azure access is needed in PR gate) |

> **Subject format reference:**
> - Push to branch: `repo:{owner}/{repo}:ref:refs/heads/{branch}`
> - Pull request: `repo:{owner}/{repo}:pull_request`
> - Environment: `repo:{owner}/{repo}:environment:{environment-name}`

### 5.3 RBAC Grants for the App Registration

The App Registration's service principal (not the App Service managed identity) needs the following RBAC grants to deploy:

| Role | Scope | Purpose |
|------|-------|---------|
| `Contributor` | `rg-StrategicGlue-CommandCenter` (resource group) | Deploy Bicep templates, manage App Service |
| `User Access Administrator` | `rg-StrategicGlue-CommandCenter` | Assign RBAC roles in Bicep (role assignments for managed identity) |

> `User Access Administrator` is required because Bicep creates `Microsoft.Authorization/roleAssignments` resources. Without it, `az deployment group create` fails with `AuthorizationFailed`.

### 5.4 GitHub Actions Login Step

```yaml
- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

No password or certificate. The OIDC token is exchanged automatically by `azure/login@v2`.

---

## 6. Caching Strategy

### 6.1 NuGet Package Cache

All workflows cache NuGet packages to avoid re-downloading on every run.

```yaml
- uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', '**/Directory.Packages.props') }}
    restore-keys: |
      nuget-${{ runner.os }}-
```

**Cache key:** Hash of all `*.csproj` files + `Directory.Packages.props` (if central package management is used). When any project file changes (new dependency added), the cache is busted and a fresh restore runs. The `restore-keys` fallback allows partial cache hits when only one package changed.

### 6.2 Playwright Browser Cache

```yaml
- uses: actions/cache@v4
  with:
    path: ~/.cache/ms-playwright
    key: playwright-${{ runner.os }}-${{ hashFiles('tests/SixToFix.E2E/**/*.csproj') }}
    restore-keys: |
      playwright-${{ runner.os }}-
```

### 6.3 Docker Layer Cache (Testcontainers)

GitHub-hosted runners do not persist Docker layers between runs. Testcontainers will pull `postgres:16-alpine` on each `integration.yml` run (~60 MB). This is acceptable — do not attempt to cache Docker images on `ubuntu-latest`.

---

## 7. Artifact Passing Between Jobs

| Artifact Name | Produced By | Consumed By | Retention |
|---------------|-------------|-------------|-----------|
| `build-output` | `ci.yml / build` | `ci.yml / unit-tests`, `ci.yml / lint`, `ci.yml / security-scan` | 1 day |
| `coverage-report` | `ci.yml / unit-tests` | `ci.yml / coverage-gate` | 1 day |
| `vulnerability-report` | `ci.yml / security-scan` | Human review (uploaded, not consumed by downstream job) | 7 days |
| `integration-test-results` | `integration.yml / integration-tests` | Human review + `integration.yml / coverage-report` | 7 days |
| `playwright-report` | `e2e.yml / playwright-e2e` | Human review | 30 days |
| `bicep-whatif-diff` | `deploy.yml / validate-bicep` | Human review (approval step) | 7 days |

**Artifact download pattern:**

```yaml
- uses: actions/download-artifact@v4
  with:
    name: coverage-report
    path: ./TestResults
```

---

## 8. Branch Protection Rules (`main`)

The `main` branch must have the following branch protection rules configured in GitHub repository settings:

| Rule | Value |
|------|-------|
| Require status checks before merging | ✅ Enabled |
| Required status checks | `ci.yml / build`, `ci.yml / unit-tests`, `ci.yml / coverage-gate`, `ci.yml / lint`, `ci.yml / security-scan` |
| Require branches to be up to date before merging | ✅ Enabled |
| Require a pull request before merging | ✅ Enabled |
| Required approving reviews | **1** |
| Dismiss stale pull request approvals when new commits are pushed | ✅ Enabled |
| Require review from code owners | ✅ Enabled (if `CODEOWNERS` file exists) |
| Restrict who can push to matching branches | ✅ Enabled — no direct pushes |
| Allow force pushes | ❌ Disabled |
| Allow deletions | ❌ Disabled |
| Require linear history | ✅ Enabled (squash merge required) |

> No developer — including repo owners — may push directly to `main`. All changes go through a PR with `ci.yml` passing + 1 approving review.

---

## ⚠️ Open Questions

1. **Staging environment vs dev environment for E2E:** `e2e.yml` currently deploys to `app-strategicglue-dev`. If the dev environment is actively used by developers between PRs, E2E against dev may be disruptive. Consider an App Service deployment slot (`staging`) or a dedicated E2E environment.
2. **Database migration in `deploy.yml`:** The `deploy-app` job must run EF Core migrations before deploying the new app version. Confirm migration runner approach: (a) `dotnet ef database update` from CI runner (requires direct DB access — complex with VNet), or (b) app runs migrations on startup via `MigrateAsync()` in `Program.cs`. Option (b) is simpler but means migrations run with `sf_app` credentials (no DDL). Recommend option (b) with `sf_admin` credentials available in a GitHub Actions secret for migration runs.
3. **`ci.yml` Azure access:** If `ci.yml` only runs unit tests with mocks, no Azure access is needed. The `github-pr` federated credential is listed speculatively — remove if not needed.
4. **Slack webhook for `notify` job:** Confirm whether a Slack webhook secret should be added (`SLACK_WEBHOOK_URL`) or if GitHub Step Summary notifications are sufficient.
5. **`User Access Administrator` scope:** Granting `User Access Administrator` at the resource group scope is a broad permission. If security policy requires narrower scope, RBAC assignments can be moved out of Bicep and into a separate privileged pipeline step. Confirm with Chris.
