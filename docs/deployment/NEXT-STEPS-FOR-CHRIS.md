# 🛑 Chris's Pickup Guide — Three Remaining Deployment Steps

**Written:** 2026-05-10 (evening)
**Project:** StrategicGlue Six-to-Fix — Multi-tenant SaaS marketing maturity audit platform
**Status:** All 6 development phases complete. 84 tests passing. Building clean. Three infrastructure items blocked on Azure/PostgreSQL access.

---

## Quick Summary

The entire codebase is done and merged to `main`. Nothing left to code. You just need to wire up three real Azure services and the app can deploy:

| # | Task | Prerequisite | Est. time |
|---|------|-------------|-----------|
| 1 | **EF Core database migration** | PostgreSQL hostname + sf_admin password | ~10 min |
| 2 | **Key Vault secrets population** | `az login` + 8 values from Azure/HubSpot portals | ~30 min |
| 3 | **Azure AI Search index provisioning** | `az login` (same login as step 2) + Search service name | ~5 min |

Do them in order. Step 1 is independent. Steps 2 and 3 share the same `az login`.

---

## Before You Start — Get Your Bearings

Open PowerShell and navigate to the repo:
```powershell
cd C:\GitHub\six-to-fix-7        # or wherever you cloned it on the new machine
git pull                          # make sure you're on latest main
git log --oneline -5              # should show recent commits from the team
dotnet build SixToFix.slnx        # should say "Build succeeded, 0 Error(s)"
```

If the build fails, stop and check `git status` before continuing.

---

## Step 1 — EF Core Initial Migration

### What this is

The app uses Entity Framework Core to talk to PostgreSQL. Before the app can run, EF needs to create all the database tables. It does this by running a "migration." The migration is a C# description of your schema that EF translates into SQL `CREATE TABLE` statements.

You have to generate the migration file once (it gets committed to the repo), and then apply it to the actual PostgreSQL database.

### What you need first

- Your Azure PostgreSQL Flexible Server hostname (looks like: `six-to-fix-dev.postgres.database.azure.com`)
- The `sf_admin` username and password you set when provisioning the server
- The database must already exist and be named `sixtofix`

**Where to find the hostname:** Azure Portal → PostgreSQL Flexible Server → your server resource → left menu: **Overview** → "Server name" field

### Step-by-step

**1. Install the EF CLI tool** (skip if already installed — one-time setup):
```powershell
dotnet tool install --global dotnet-ef
```
If you already have it but it may be outdated:
```powershell
dotnet tool update --global dotnet-ef
dotnet ef --version    # should show 9.x or 10.x
```

**2. Set the connection string as an environment variable.** Replace every `<...>` with real values:
```powershell
$env:DESIGN_TIME_CONNECTION_STRING = "Host=<your-server>.postgres.database.azure.com;Port=5432;Database=sixtofix;Username=sfadmin;Password=<your-password>;Ssl Mode=Require"
```

⚠️ **Port MUST be 5432** — not 6432. Port 6432 is pgBouncer (the connection pool) and breaks migrations.

Example with fake values so you can see the format:
```
Host=six-to-fix-dev.postgres.database.azure.com;Port=5432;Database=sixtofix;Username=sfadmin;Password=Str0ng!Pass123;Ssl Mode=Require
```

**3. Generate the migration file** (this does NOT touch the database — it only creates C# files):
```powershell
dotnet ef migrations add InitialCreate --project src\SixToFix.Infrastructure --startup-project src\SixToFix.Web --solution SixToFix.slnx
```

You should see output ending in: `Done. To undo this action, use 'ef migrations remove'`

This creates files in `src\SixToFix.Infrastructure\Migrations\`. Do not edit them.

**4. Apply the migration to the database** (this runs SQL against PostgreSQL — creates all the tables):
```powershell
dotnet ef database update --project src\SixToFix.Infrastructure --startup-project src\SixToFix.Web --solution SixToFix.slnx
```

You should see a series of log lines ending in: `Done.`

**5. Commit the generated migration files:**
```powershell
git checkout -b dev/initial-migration
git add src\SixToFix.Infrastructure\Migrations\
git commit -m "feat: add InitialCreate EF Core migration

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push -u origin dev/initial-migration
gh pr create --title "feat: add InitialCreate EF Core migration" --base main --body "Adds the initial EF Core migration generated against the dev PostgreSQL server."
gh pr merge --squash --auto --delete-branch
```

### If something goes wrong

- **"Host not found" or connection timeout:** The server name is wrong, or your machine can't reach Azure. Check the hostname carefully. Try pinging it: `Test-NetConnection <hostname> -Port 5432`
- **"role sf_admin does not exist":** Use whatever admin login username you chose when creating the PostgreSQL server (check Azure Portal → PostgreSQL → server → Configuration)
- **"database sixtofix does not exist":** Create it first: `CREATE DATABASE sixtofix;` using psql or Azure Data Studio
- **The env var disappears:** PowerShell environment variables are session-scoped. If you close and reopen the terminal, re-run the `$env:DESIGN_TIME_CONNECTION_STRING = ...` command before running EF commands again

---

## Step 2 — Key Vault Secrets Population

### What this is

The app does NOT store any secrets in config files or environment variables (those would be visible in the repo or App Service logs). Instead, it reads all sensitive values from Azure Key Vault at startup. You need to put 8 values into Key Vault before the app can start.

**Key Vault name:** `kv-sixtofix-dev` (for the dev environment)

### What you need first

- Azure CLI installed. Test: `az --version`. If missing: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows
- Your Azure account must have **Key Vault Secrets Officer** or **Owner** role on the Key Vault
- The following values (collected from various portals):

| Secret name (exact) | What it is | Where to find it |
|---------------------|------------|-----------------|
| `ConnectionStrings--DefaultConnection` | PostgreSQL runtime connection string (port 6432 — pgBouncer) | Build it yourself using the PostgreSQL server details |
| `Jwt--SigningKey` | Random 32+ character string used to sign JWT auth tokens | Generate it (see below) |
| `AzureOpenAI--Endpoint` | Your Azure OpenAI resource's endpoint URL | Azure Portal → OpenAI resource → **Endpoint** |
| `AzureOpenAI--DeploymentName` | The name of your GPT-4o deployment | Azure Portal → OpenAI resource → **Model deployments** |
| `Search--Endpoint` | Your Azure AI Search resource's URL | Azure Portal → Search resource → **Url** |
| `Storage--BlobEndpoint` | Your Blob Storage endpoint URL | Azure Portal → Storage Account → **Endpoints** → Blob service |
| `HubSpot--PrivateAppToken` | HubSpot private app token | HubSpot portal → Settings → Integrations → Private Apps |
| `HubSpot--WebhookSecret` | HubSpot webhook signature secret | HubSpot webhook subscription settings |

### Step-by-step

**1. Log in to Azure:**
```powershell
az login
```
A browser window will open. Sign in with your StrategicGlue Azure account. When complete, the terminal shows a list of your subscriptions.

**2. Set your active subscription** (replace with your actual subscription ID — find it in Azure Portal → Subscriptions):
```powershell
az account set --subscription "your-subscription-id-here"
```

**3. Generate a JWT signing key** (run this, copy the output — it's random and secure):
```powershell
[System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```
Save this value somewhere safe. You'll need it in the next step.

**4. Build the runtime connection string** (port 6432, NOT 5432 — this is for the app, not migrations):
```
Host=<your-server>.postgres.database.azure.com;Port=6432;Database=sixtofix;Username=sf_app;Password=<sf_app_password>;No Reset On Close=true;Ssl Mode=Require
```
Note: `sf_app` is a lower-privilege app user — different from `sf_admin` used in Step 1.

**5. Set all secrets in Key Vault** (run each line separately, replace placeholders):

```powershell
# PostgreSQL runtime connection string (port 6432 = pgBouncer)
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "ConnectionStrings--DefaultConnection" `
  --value "Host=<server>.postgres.database.azure.com;Port=6432;Database=sixtofix;Username=sf_app;Password=<password>;No Reset On Close=true;Ssl Mode=Require"

# JWT signing key (use the value you generated in step 3)
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "Jwt--SigningKey" `
  --value "<paste-the-base64-string-you-generated>"

# Azure OpenAI endpoint URL
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "AzureOpenAI--Endpoint" `
  --value "https://<your-openai-resource>.openai.azure.com/"

# Azure OpenAI deployment name (the model deployment name, e.g. "gpt-4o")
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "AzureOpenAI--DeploymentName" `
  --value "gpt-4o"

# Azure AI Search endpoint
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "Search--Endpoint" `
  --value "https://<your-search-resource>.search.windows.net"

# Blob Storage endpoint
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "Storage--BlobEndpoint" `
  --value "https://<your-storage-account>.blob.core.windows.net"

# HubSpot private app token
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "HubSpot--PrivateAppToken" `
  --value "<your-hubspot-private-app-token>"

# HubSpot webhook secret
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "HubSpot--WebhookSecret" `
  --value "<your-hubspot-webhook-secret>"
```

**6. Verify all 8 secrets are present:**
```powershell
az keyvault secret list --vault-name kv-sixtofix-dev --query "[].name" -o table
```
You should see all 8 names listed.

**7. Tell the App Service where to find Key Vault** (replace `<your-app-service-name>` with the actual App Service name from Azure Portal → App Services):
```powershell
az webapp config appsettings set `
  --resource-group rg-StrategicGlue-CommandCenter `
  --name <your-app-service-name> `
  --settings KeyVault__Uri="https://kv-sixtofix-dev.vault.azure.net/"
```

**8. Ensure the App Service's managed identity can read secrets.** Find the managed identity's Object ID first:
```powershell
az webapp identity show --name <your-app-service-name> --resource-group rg-StrategicGlue-CommandCenter --query principalId -o tsv
```
Then grant access:
```powershell
az role assignment create `
  --assignee <principal-id-from-above> `
  --role "Key Vault Secrets User" `
  --scope "/subscriptions/<sub-id>/resourceGroups/rg-StrategicGlue-CommandCenter/providers/Microsoft.KeyVault/vaults/kv-sixtofix-dev"
```

### If something goes wrong

- **"Vault not found":** Double-check the vault name is `kv-sixtofix-dev`. Confirm in Azure Portal → Key Vault resources.
- **"Forbidden" or "Access denied":** Your account doesn't have the right role. Ask someone with Owner access to add you as **Key Vault Secrets Officer** on the vault resource.
- **"az: command not found":** Azure CLI isn't installed. Download from https://aka.ms/installazurecliwindows

---

## Step 3 — Azure AI Search Index Provisioning

### What this is

The app uses 3 Azure AI Search indexes for different purposes:
- `six-to-fix-evidence` — stores chunked client document content for evidence retrieval before audits
- `six-to-fix-skill-outputs` — indexes skill run outputs and council decisions for the audit trail
- `six-to-fix-calibration` — indexes reviewer score overrides for calibration analysis

These indexes must exist in Azure before the app can run. The team already wrote the provisioning script — you just run it.

**Script location:** `infra\search-indexes\provision-indexes.ps1`

### What you need first

- Already logged in to Azure from Step 2 (`az login` — same session is fine)
- Your Azure AI Search service name (find it: Azure Portal → Azure AI Search → your resource → **Name**)
- **Key Vault secrets from Step 2 must already be done** (the app needs `Search--Endpoint` to talk to Search)

### Step-by-step

**1. Verify you're still logged into Azure:**
```powershell
az account show --query name -o tsv
```
If it errors, run `az login` again.

**2. Find your Search service name.** Go to Azure Portal → search for "AI Search" → click your search resource → the **Name** field (looks like `six-to-fix-search-dev` or similar).

**3. Run the provisioning script:**
```powershell
.\infra\search-indexes\provision-indexes.ps1 -SearchServiceName <your-search-service-name>
```
Example:
```powershell
.\infra\search-indexes\provision-indexes.ps1 -SearchServiceName six-to-fix-search-dev
```

The script will print what it's doing:
```
Search service : six-to-fix-search-dev
Checking index 'six-to-fix-evidence'... creating... done.
Checking index 'six-to-fix-skill-outputs'... creating... done.
Checking index 'six-to-fix-calibration'... creating... done.

All indexes provisioned successfully.
```

If you see "already exists, skipping" — that's fine, the index was already there.

**4. Grant the App Service managed identity access to Search.** The script prints the exact command at the end. Here's the pattern:

First, get the managed identity principal ID (if you don't already have it from Step 2):
```powershell
az webapp identity show `
  --name <your-app-service-name> `
  --resource-group rg-StrategicGlue-CommandCenter `
  --query principalId -o tsv
```

Then grant both roles:
```powershell
# For indexing documents (writing to the index)
az role assignment create `
  --assignee <principal-id> `
  --role "Search Index Data Contributor" `
  --scope "/subscriptions/<sub-id>/resourceGroups/rg-StrategicGlue-CommandCenter/providers/Microsoft.Search/searchServices/<search-service-name>"

# For reading/searching the index
az role assignment create `
  --assignee <principal-id> `
  --role "Search Index Data Reader" `
  --scope "/subscriptions/<sub-id>/resourceGroups/rg-StrategicGlue-CommandCenter/providers/Microsoft.Search/searchServices/<search-service-name>"
```

### If something goes wrong

- **"Unauthorized" or 403:** Your account doesn't have Contributor access on the Search resource. Grant yourself Contributor in Azure Portal → Search resource → Access control (IAM).
- **"Search service not found":** The service name is wrong. Check the exact name in Azure Portal — it's case-sensitive.
- **Script won't run due to execution policy:** Run `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` first, then re-run the script.

---

## After All Three Steps — Trigger Deployment

Once all three steps are done, trigger a deployment via GitHub Actions:

```powershell
# Trigger the infra deployment workflow (if Bicep isn't deployed yet)
gh workflow run deploy-infra.yml --field environment=dev

# Trigger the app deployment
gh workflow run deploy-app.yml
```

You can monitor progress at: https://github.com/cdaly33/six-to-fix-7/actions

---

## Reference — Key Files

| File | What it is |
|------|------------|
| `docs/deployment/secrets.md` | GitHub Actions secrets needed for CI/CD (OIDC, webapp names, Playwright) |
| `docs/deployment/migrations.md` | Migration runbook (brief version of Step 1 above) |
| `docs/architecture/search-index-schema.md` | Full technical schema for all 3 Search indexes |
| `infra/search-indexes/provision-indexes.ps1` | Search index provisioning script (Step 3) |
| `infra/main.bicep` | Top-level Bicep template — provisions all Azure resources |
| `src/SixToFix.Infrastructure/Migrations/` | Where EF migration files go after Step 1 |
| `.squad/decisions.md` | All 22+ architectural decisions made during development |

---

## Project State Snapshot (as of 2026-05-10)

- **Branch:** `main` — all 6 phases merged
- **Tests:** 84 passing, 0 failing
- **Build:** Clean (`TreatWarningsAsErrors=true` — zero warnings allowed)
- **Architecture:** .NET 10, Blazor Server, Azure PostgreSQL (Flexible), Azure OpenAI (GPT-4o), Azure AI Search, Azure Blob Storage, HubSpot integration, SignalR for real-time audit progress
- **Auth:** JWT Bearer, four roles: `SuperAdmin`, `TenantAdmin`, `Reviewer`, `Viewer`
- **Pending:** ONLY the three infrastructure steps above — no code changes needed

Good luck tomorrow, Chris. The hard part is done. 🎉
