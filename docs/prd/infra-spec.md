# Infrastructure Spec Reference

> **Status:** Specification  
> **Author:** Smithers (Cloud DevOps)  
> **Date:** 2026-05-10  
> **Audience:** DevOps engineer provisioning a blank Azure subscription  
> **Related:** [Section 04 — Infrastructure](prd/sections/04-infrastructure.md) | [Auth Spec](auth-spec.md)

---

## 1. Complete Azure Resource Inventory

| Resource Name (dev) | Resource Name (prod) | Type | SKU (dev) | SKU (prod) | Purpose | Depends On |
|---------------------|----------------------|------|-----------|-----------|---------|-----------|
| `rg-StrategicGlue-CommandCenter` | `rg-StrategicGlue-CommandCenter` | Resource Group | — | — | Container for all resources | Azure subscription |
| `log-strategicglue-dev` | `log-strategicglue-prod` | Log Analytics Workspace | PerGB2018 | PerGB2018 | Centralized log aggregation | Resource group |
| `appi-strategicglue-dev` | `appi-strategicglue-prod` | Application Insights | Standard | Standard | APM telemetry | Log Analytics workspace |
| `kv-strategicglue-dev` | `kv-strategicglue-prod` | Key Vault | Standard | Premium | Secret storage | Resource group |
| `ststrategicgluedev` | `ststrategicglueprod` | Storage Account | LRS / StorageV2 | ZRS / StorageV2 | Blob file storage | Resource group |
| `psql-strategicglue-dev` | `psql-strategicglue-prod` | PostgreSQL Flexible Server | Standard_B2ms | Standard_D4s_v3 | Primary database | Resource group (+ VNet for prod) |
| `srch-strategicglue-dev` | `srch-strategicglue-prod` | Azure AI Search | Basic | Standard S1 | Document and audit search | Resource group |
| `asp-strategicglue-dev` | `asp-strategicglue-prod` | App Service Plan | B2 (Linux) | P2v3 (Linux) | Compute host | Resource group |
| `app-strategicglue-dev` | `app-strategicglue-prod` | App Service (Web App) | — (uses plan) | — (uses plan) | Application host | App Service Plan, all services |
| _(not used)_ | `vnet-strategicglue-prod` | Virtual Network | — | — | Network isolation | Resource group |
| _(not used)_ | `nsg-strategicglue-prod` | Network Security Group | — | — | Subnet traffic rules | VNet |
| _(not used)_ | `pep-psql-strategicglue-prod` | Private Endpoint | — | — | Private PostgreSQL access | VNet, PostgreSQL |
| _(not used)_ | `pep-kv-strategicglue-prod` | Private Endpoint | — | — | Private Key Vault access | VNet, Key Vault |

---

## 2. Bicep Module Structure

The infrastructure-as-code lives in `infrastructure/` at the repository root. The module layout below reflects a clean Bicep project structure suitable for both dev and prod parameterization.

```
infrastructure/
├── main.bicep                    # Orchestrates all modules; accepts env parameter
├── main.dev.bicepparam           # Dev environment parameter values
├── main.prod.bicepparam          # Prod environment parameter values
├── modules/
│   ├── logging.bicep             # Log Analytics + Application Insights
│   ├── keyvault.bicep            # Key Vault + RBAC assignments
│   ├── storage.bicep             # Storage Account + containers + RBAC
│   ├── postgresql.bicep          # PostgreSQL Flexible Server + database + pgBouncer
│   ├── search.bicep              # Azure AI Search + RBAC
│   ├── appservice.bicep          # App Service Plan + Web App + managed identity
│   ├── network.bicep             # VNet + NSG + subnets (prod only)
│   └── privateendpoints.bicep    # Private endpoints for prod (conditional)
└── scripts/
    ├── init-db.sh                # First-time DB role creation (run once per env)
    └── seed-keyvault.sh          # Populates required secrets interactively
```

**`main.bicep` parameter contract:**

```bicep
param environment string          // 'dev' | 'prod'
param location string = 'eastus'
param resourceGroupName string = 'rg-StrategicGlue-CommandCenter'
param enableVnet bool             // false for dev, true for prod
param enablePrivateEndpoints bool // false for dev, true for prod
param enableZoneRedundancy bool   // false for dev, true for prod
```

Each module receives only the parameters it needs. The `main.bicep` assembles outputs from each module (e.g., Key Vault URI, storage account name) and passes them as inputs to dependent modules. No module references another module's resources directly by name — all cross-module wiring goes through `main.bicep` output variables.

---

## 3. GitHub Actions Workflow Specs

### 3.1 `deploy-infra.yml` — Infrastructure Deployment

**Trigger:** Push to `main` where files under `infrastructure/**` changed.  
**Environment:** Runs against dev automatically; prod requires manual approval.

```
Steps:
1. Checkout repository
2. Azure login via OIDC (az login --federated-token)
3. Run Bicep what-if preview (az deployment group what-if)
4. [Manual approval gate for prod]
5. Deploy Bicep (az deployment group create --parameters main.{env}.bicepparam)
6. Capture outputs (Key Vault URI, App Service name, etc.) as step outputs
7. Post deployment summary to GitHub Step Summary
```

### 3.2 `deploy-app.yml` — Application Deployment

**Trigger:** Push to `main` where files under `api/**` or `web/**` changed.  
**Environment:** dev auto-deploys; prod requires release tag `v*.*.*` and approval.

```
Steps:
1. Checkout repository
2. Setup .NET 10 SDK
3. Restore NuGet packages (dotnet restore)
4. Build in Release mode (dotnet build -c Release --no-restore)
5. Run unit tests (dotnet test --no-build) — fail fast
6. Publish application (dotnet publish -c Release -o ./publish)
7. Azure login via OIDC
8. Run pending DB migrations (psql using sf_admin credentials from Key Vault)
9. Zip publish output
10. Deploy to App Service (az webapp deploy --type zip)
11. Health check: curl app URL, assert HTTP 200 within 60s
12. Post deployment URL and status to GitHub Step Summary
```

### 3.3 `validate-skills.yml` — Skill Schema Validation

**Trigger:** PR where files under `skills/**` changed.

```
Steps:
1. Checkout repository
2. Setup .NET 10 SDK
3. Run skill validation tool (dotnet run --project tools/SkillValidator)
   - Discovers all skills/*/SKILL.md files
   - Validates YAML frontmatter against skill schema
   - Validates referenced JSON schemas exist
4. Fail PR check if any skill fails validation
5. Post validation report as PR comment
```

### 3.4 `test.yml` — Test Suite

**Trigger:** Any PR.

```
Steps:
1. Checkout repository
2. Setup .NET 10 SDK
3. Start PostgreSQL service container (postgres:16-alpine)
4. Run DB migrations against test database (sf_admin)
5. Restore and build solution
6. Run xUnit tests (dotnet test) — all projects
7. Setup Playwright browsers (npx playwright install)
8. Start app in test mode (dotnet run --environment=Testing &)
9. Wait for app health endpoint to return 200
10. Run Playwright E2E tests (npx playwright test)
11. Upload test results and Playwright trace artifacts
12. Report test pass/fail status on PR
```

---

## 4. Network Topology Diagram

### Development Environment (No VNet)

```
┌────────────────────────────────────────────────────────────┐
│                        INTERNET                            │
└──────────────────────────┬─────────────────────────────────┘
                           │ HTTPS
                           ▼
              ┌────────────────────────┐
              │  App Service           │
              │  app-strategicglue-dev │
              │  (.NET 10 / Blazor)    │
              └─────┬──────┬──────┬───┘
                    │      │      │
          Managed   │      │      │  Key Vault
          Identity  │      │      │  Reference
                    │      │      │
        ┌───────────▼──┐ ┌─▼────┐ ┌▼──────────────────┐
        │ PostgreSQL   │ │ Blob │ │ Key Vault          │
        │ Flexible     │ │Store │ │ kv-strategicglue-  │
        │ Server (dev) │ │ (dev)│ │ dev                │
        │ port 6432    │ │      │ │                    │
        └──────────────┘ └──────┘ └────────────────────┘
                    │
        ┌───────────▼──────────────────┐
        │ Azure AI Search              │
        │ srch-strategicglue-dev       │
        └──────────────────────────────┘
```

### Production Environment (VNet Isolated)

```
┌────────────────────────────────────────────────────────────┐
│                        INTERNET                            │
└──────────────────────────┬─────────────────────────────────┘
                           │ HTTPS
                           ▼
              ┌────────────────────────┐
              │  App Service           │
              │  app-strategicglue-    │
              │  prod (2+ instances)   │
              └──────────┬─────────────┘
                         │ VNet Integration
                         │ (snet-app-strategicglue-prod)
      ┌──────────────────┴──────────────────────────────────┐
      │             vnet-strategicglue-prod (10.0.0.0/16)   │
      │                                                      │
      │  snet-app (10.0.1.0/24)   [App Service egress]      │
      │  snet-db  (10.0.2.0/24)   [PostgreSQL private EP]   │
      │  snet-svc (10.0.3.0/24)   [KV, Search private EPs]  │
      │                                                      │
      │  ┌───────────────┐   ┌────────────┐   ┌──────────┐  │
      │  │ pep-psql-     │   │ pep-kv-    │   │ pep-     │  │
      │  │ prod          │   │ prod       │   │ srch-    │  │
      │  │ (10.0.2.x)    │   │ (10.0.3.x) │   │ prod     │  │
      │  └───────┬───────┘   └─────┬──────┘   └────┬─────┘  │
      └──────────┼─────────────────┼───────────────┼────────┘
                 │                 │               │
                 ▼                 ▼               ▼
        PostgreSQL Prod      Key Vault Prod    AI Search Prod
        (zone-redundant)     (Premium, purge   (Standard S1,
                              protected)        2 replicas)

        Storage Account (ZRS) — accessed via service endpoint
        from snet-app subnet (no private endpoint needed for
        ZRS storage with service endpoint)
```

---

## 5. Environment Variable Reference

All configuration enters the App Service as **App Settings**. Secrets use Key Vault references. Non-secret values are set directly.

| App Setting Key | Source | Example Value (dev) | Purpose |
|-----------------|--------|---------------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Direct | `Development` | ASP.NET Core env selection |
| `APPLICATIONINSIGHTS__CONNECTIONSTRING` | Direct | `InstrumentationKey=...` | Application Insights telemetry |
| `AZURE__STORAGEACCOUNTNAME` | Direct | `ststrategicgluedev` | Blob SDK account name |
| `AZURE__SEARCHENDPOINT` | Direct | `https://srch-strategicglue-dev.search.windows.net` | AI Search endpoint |
| `IDENTITYSERVER__ISSUER` | Direct | `https://app-strategicglue-dev.azurewebsites.net` | JWT issuer URI |
| `DATABASE-URL` | Key Vault ref | `@Microsoft.KeyVault(...)` | PostgreSQL connection string |
| `FOUNDRY-API-KEY` | Key Vault ref | `@Microsoft.KeyVault(...)` | Azure OpenAI / Foundry API key |
| `HUBSPOT-PRIVATE-APP-TOKEN` | Key Vault ref | `@Microsoft.KeyVault(...)` | HubSpot CRM sync token |
| `JWT-SIGNING-KEY` | Key Vault ref | `@Microsoft.KeyVault(...)` | IdentityServer signing key material |
| `AZURE-SEARCH-API-KEY` | Key Vault ref | `@Microsoft.KeyVault(...)` | AI Search query API key |

**Key Vault reference format:**
```
@Microsoft.KeyVault(SecretUri=https://kv-strategicglue-dev.vault.azure.net/secrets/DATABASE-URL/)
```

---

## 6. Database Schema Initialization — Fresh Environment Order of Operations

Run these steps in strict order for a brand-new environment. Skipping or reordering steps causes failures.

```
Step 1: Provision Resource Group
   az group create --name rg-StrategicGlue-CommandCenter --location eastus

Step 2: Deploy Bicep (logging + keyvault + storage + postgresql + search + appservice)
   az deployment group create \
     --resource-group rg-StrategicGlue-CommandCenter \
     --template-file infrastructure/main.bicep \
     --parameters infrastructure/main.dev.bicepparam

Step 3: Seed Key Vault secrets (manual — never automated with plaintext in CI)
   Run: infrastructure/scripts/seed-keyvault.sh
   (Interactively prompts for each secret value; writes to Key Vault)

Step 4: Initialize database roles (run once per environment)
   Run: infrastructure/scripts/init-db.sh
   (Connects as postgres admin; creates sf_admin and sf_app roles)

Step 5: Run all migrations (in order, via sf_admin)
   psql $ADMIN_DATABASE_URL -f db/migrations/0001_init.sql
   psql $ADMIN_DATABASE_URL -f db/migrations/0002_....sql
   ... (all migrations in numeric order)

Step 6: Verify database health
   psql $APP_DATABASE_URL -c "SELECT current_user, current_database();"
   (Expected: sf_app | strategicglue)

Step 7: Deploy application
   Trigger deploy-app.yml workflow (or run deployment manually)

Step 8: Smoke test
   curl https://app-strategicglue-dev.azurewebsites.net/health
   (Expected: HTTP 200 with {"status":"healthy"})
```

---

## 7. Rollback Procedures

### 7.1 Application Rollback (No DB Migration Involved)

If the new app version breaks and migrations were **not** part of the deployment:

```
1. In Azure Portal → App Service → Deployment Center → Deployment History
2. Select the previous successful deployment
3. Click "Redeploy"
   OR
4. Trigger deploy-app.yml manually with the previous release tag:
   gh workflow run deploy-app.yml -f ref=v{previous-version}
```

Expected recovery time: 3–5 minutes.

### 7.2 Application Rollback (With DB Migration)

If a migration ran and the app is broken:

```
1. Immediately: redeploy the previous app version (see 7.1) — this stops new damage
2. Assess whether the migration is backward-compatible:
   - If additive only (new table, new nullable column): old app version likely works; migration stays
   - If breaking (renamed column, dropped column, changed constraint): must run rollback script
3. Run the corresponding rollback script:
   psql $ADMIN_DATABASE_URL -f db/migrations/rollback/{NNNN}_{description}_down.sql
4. Verify application health with previous version
5. Create a new migration fix and go through the standard deployment process
```

⚠️ **Do not** run rollback scripts in prod without verifying them in dev first. Data loss is possible.

### 7.3 Infrastructure Rollback

Bicep deployments are idempotent. To roll back an infrastructure change:

```
1. Revert the Bicep file change in git (git revert)
2. Push to main — the deploy-infra.yml workflow re-applies the previous Bicep state
3. For irreversible changes (e.g., deleted resources): restore from backup or re-provision
```

---

## 8. Cost Estimate — Dev Environment (Monthly)

All estimates use Azure East US public pricing. Round numbers used for planning.

| Resource | SKU | Estimated Monthly Cost |
|----------|-----|----------------------|
| App Service Plan (B2, Linux) | B2 | ~$75 |
| PostgreSQL Flexible Server (Standard_B2ms, 32 GB) | Standard_B2ms | ~$55 |
| Azure AI Search (Basic, 1 replica) | Basic | ~$75 |
| Key Vault (Standard, ~1K operations/day) | Standard | ~$5 |
| Storage Account (LRS, ~50 GB + transactions) | StorageV2 LRS | ~$5 |
| Application Insights + Log Analytics (~2 GB/mo) | PerGB2018 | ~$5 |
| Bandwidth (egress, ~10 GB/mo) | — | ~$1 |
| **Total (dev)** | | **~$221/month** |

**Prod estimate (rough):** ~$600–800/month depending on P2v3 instance count, PostgreSQL Standard_D4s_v3 with HA, AI Search Standard S1 with semantic ranking, and ZRS storage. Engage Azure Pricing Calculator for a precise figure before committing.

**Cost governance:** Set a budget alert at $300/month for dev (80% alert at $240). If dev costs exceed $300, investigate App Service plan uptime (B2 charges 24/7 regardless of traffic — consider stopping the dev app service plan during overnight/weekend hours using Azure Automation).
