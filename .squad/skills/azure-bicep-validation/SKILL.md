# Skill: Azure Bicep Validation — Full Audit Checklist

**Author:** Tank (DevOps/Infrastructure)  
**Captured:** 2026-05-17  
**Trigger:** Reactive bug-fix chain on this project; three sequential deployment errors that ARM validation would have caught upfront.

---

## The Core Lesson

> **Always run `az deployment group validate`, not just `az bicep build`.**

| Command | What it catches |
|---------|-----------------|
| `az bicep build` | Syntax errors, type errors, missing required params (Bicep compiler only) |
| `az deployment group validate` | Everything above **plus**: invalid enum values, incompatible resource property combinations, SKU/feature availability, ARM-level constraint violations |

`az bicep build` is not enough. ARM validation is the minimum bar before any PR is merged that touches `infra/`.

---

## How to Run ARM Validation

```powershell
# Set secrets as env vars (never put in param files)
$env:POSTGRES_ADMIN_PASSWORD = "PLACEHOLDER_NOT_REAL"
$env:SF_APP_PASSWORD = "PLACEHOLDER_NOT_REAL"

# 1. Syntax only
az bicep build --file infra/main.bicep

# 2. ARM-level (catches runtime param errors)
az deployment group validate `
  --resource-group rg-sixtofix-prod `
  --template-file infra/main.bicep `
  --parameters infra/params/prod.bicepparam
```

Placeholder passwords are fine for validation — ARM validates structure, not secret values.

---

## Full Audit Checklist

### A. Parameter Value Correctness

- [ ] Boolean-style params: check whether Azure expects `'True'`/`'False'` strings vs `true`/`false` booleans vs `'on'`/`'off'` strings
  - Example: `pgbouncer.enabled` → must be `'True'` or `'False'` (capital T/F, string)
  - Example: `require_secure_transport` (PostgreSQL GUC) → must be `'on'` or `'off'` (lowercase)
- [ ] All enum values match Azure's allowed strings exactly (case-sensitive)
- [ ] SKU / capacity values available in target region

### B. PostgreSQL Flexible Server (PG16)

- [ ] `configurations` sub-resources: every `name` must be a valid PG16 GUC parameter name
  - ❌ `connection_pooling` — not a valid PG16 GUC; use `pgbouncer.enabled`
  - ✅ `pgbouncer.enabled` with value `'True'` or `'False'`
  - ✅ `require_secure_transport` with value `'on'` or `'off'` (lowercase)
- [ ] `highAvailability.mode` — one of: `'Disabled'`, `'SameZone'`, `'ZoneRedundant'`
  - `ZoneRedundant` requires GeneralPurpose or MemoryOptimized tier (NOT Burstable)
  - `ZoneRedundant` requires the region to support availability zones
- [ ] `backup.backupRetentionDays` — must be 7–35 (inclusive)
- [ ] `storage.autoGrow` (if set) — `'Enabled'` or `'Disabled'` (capital E/D)
- [ ] `geoRedundantBackup` — `'Enabled'` or `'Disabled'` (capital E/D)

### C. Azure AI Search

- [ ] SKU name — one of: `'free'`, `'basic'`, `'standard'`, `'standard2'`, `'standard3'`, `'storage_optimized_l1'`, `'storage_optimized_l2'`
- [ ] `replicaCount` — must be compatible with SKU (basic: 1–3, standard: 1–12)
- [ ] `partitionCount` — must be compatible with SKU (free/basic: 1 only)
- [ ] Semantic search requires Standard tier or higher

### D. App Service / Web App

- [ ] `clientAffinityEnabled` — boolean (`true`/`false`), not string
- [ ] `httpsOnly` — boolean (`true`/`false`), not string
- [ ] `linuxFxVersion` format: `DOTNET|10.0` (not `DOTNETCORE|10.0`, not `dotnet|10.0`)
  - `DOTNETCORE` prefix: .NET Core 1.x–3.1 and early .NET 5 only
  - `DOTNET` prefix: .NET 6 and later (including .NET 10)
  - All uppercase
- [ ] `alwaysOn: true` — only valid on Basic tier or higher; invalid on Free/Shared (causes silent deploy failure)
- [ ] `kind: 'app,linux'` for Linux plans — must match between plan and site

### E. MetricAlerts

- [ ] `timeAggregation` — must match the metric's supported aggregations:
  - `Microsoft.Web/sites` metrics (e.g., `Http5xx`): `'Total'`, `'Average'`, `'Count'`
  - `microsoft.insights/components` metrics (e.g., `dependencies/failed`): `'Count'`
  - **`'Total'` is NOT valid for Application Insights component metrics**
- [ ] `operator` — one of: `'GreaterThan'`, `'LessThan'`, `'GreaterThanOrEqual'`, `'LessThanOrEqual'`, `'Equals'`
- [ ] `criterionType` — `'StaticThresholdCriterion'` or `'DynamicThresholdCriterion'`

### F. Key Vault

- [ ] `softDeleteRetentionInDays` — 7–90 (inclusive)
- [ ] SKU name — `'standard'` or `'premium'` (lowercase)
- [ ] Secret names — alphanumeric + hyphens only (`^[0-9a-zA-Z-]+$`), max 127 chars
  - .NET config key separator `--` (double hyphen) is valid in KV secret names
- [ ] `enablePurgeProtection: true` is a one-way door — once set, cannot be disabled

### G. Log Analytics / Application Insights

- [ ] `retentionInDays` — must be one of: `30, 60, 90, 120, 180, 270, 365, 550, 730`
  - Same allowed values apply to both `Microsoft.OperationalInsights/workspaces` and `Microsoft.Insights/components`
- [ ] AppInsights `kind: 'web'` — lowercase; `'Web'` (capitalised) is not valid
- [ ] AppInsights `Application_Type: 'web'` — lowercase

### H. General

- [ ] No hardcoded region strings — all `location` properties should reference the `location` parameter
- [ ] API versions — flag anything older than 2 years (may be deprecated or lack newer features)
  - Note: `Microsoft.Insights/metricAlerts@2018-03-01` is the latest stable version for that type — exception to the rule
- [ ] Implicit `dependsOn` — Bicep handles via `parent` and output references; explicit `dependsOn` only needed for non-parent cross-resource ordering
- [ ] Conditional resources (`if (condition)`) — ensure all property references on conditionally-deployed resources have the same condition (or the compiler will error)

---

## Known Azure Gotchas Encountered in This Project

| Gotcha | Details | Fix |
|--------|---------|-----|
| `dependencies/failed` timeAggregation | AppInsights metric only accepts `'Count'`, not `'Total'` | Change to `'Count'` |
| `connection_pooling` not a PG16 GUC | Parameter was removed; use `pgbouncer.enabled` instead | Replace config resource |
| `pgbouncer.enabled` value | Must be `'True'`/`'False'` string, not `'on'`/`'off'` | Change to `'True'` |
| `require_secure_transport` value case | Must be lowercase `'on'`/`'off'` | Change from `'ON'` to `'on'` |
| `linuxFxVersion` prefix | .NET 10 uses `DOTNET|10.0`, not `DOTNETCORE|10.0` | Change prefix |
| eastus2 capacity | PostgreSQL Flexible Server and AI Search can be capacity-restricted in eastus2 | Deploy to `centralus` instead |
| `resourceGroup().location` as default | Don't derive resource location from RG — pass explicit `location` param | Add `param location` to all modules |

---

## Validation Gate Recommendation

Add to `deploy-infra.yml` workflow:

```yaml
- name: ARM validation
  run: |
    az deployment group validate \
      --resource-group ${{ vars.AZURE_RG }} \
      --template-file infra/main.bicep \
      --parameters infra/params/prod.bicepparam \
      --parameters postgresAdminPassword="${{ secrets.POSTGRES_ADMIN_PASSWORD }}" \
      --parameters sfAppPassword="${{ secrets.SF_APP_PASSWORD }}" \
      --parameters openAiAccountName=""
```

This should run **before** any deployment step and gate the pipeline.
