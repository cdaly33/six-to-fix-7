# 🛑 Chris's Pickup Guide — Three Remaining Steps

**Written:** 2026-05-10 (evening) | **Updated:** 2026-05-16 (morning)
**Project:** StrategicGlue Six-to-Fix — Multi-tenant SaaS marketing maturity audit platform
**Status:** All 6 development phases complete. 84 tests passing. Building clean. PR #20 already merged to main ✅. Aspire local dev integration on `feature/aspire-integration` branch (pending merge). Three infrastructure items blocked on Azure/PostgreSQL access.

---

## Quick Summary

The entire codebase is done. PR #20 is already merged — you're on the latest main. You just need to wire up three real Azure services and the app can deploy. Optionally, merge the Aspire branch first for easier local dev:

| # | Task | Prerequisite | Est. time |
|---|------|-------------|-----------|
| — | _(done)_ ~~Merge PR #20~~ | ✅ Already on main | — |
| — | **Merge `feature/aspire-integration`** (optional) | GitHub access | ~2 min |
| 1 | **EF Core database migration** | PostgreSQL hostname + sf_admin password | ~10 min |
| 2 | **Key Vault secrets population** | `az login` + 8 values from Azure/HubSpot portals | ~30 min |
| 3 | **Azure AI Search index provisioning** | `az login` (same login as step 2) + Search service name | ~5 min |

Steps 1–3 are independent of the Aspire merge. Steps 2 and 3 share the same `az login`.

---

## Before You Start — Get Your Bearings

Open PowerShell and navigate to the repo:
```powershell
cd C:\GitHub\six-to-fix-7        # or wherever you cloned it on the new machine
git pull                          # make sure you're on latest main
git log --oneline -3              # top commit should be the PR #20 squash merge
dotnet build SixToFix.slnx        # should say "Build succeeded, 0 Error(s)"
```

If the build fails, stop and check `git status` before continuing.

---

## Local Dev with Aspire (Optional but Recommended)

.NET Aspire is a local orchestration tool that spins up your entire dev environment — PostgreSQL container, the web app, and the Aspire dashboard — with a single command. No manual connection strings needed.

**This is on the `feature/aspire-integration` branch. Merge it first:**
```powershell
gh pr create --title "feat: add .NET Aspire for local dev orchestration" \
  --head feature/aspire-integration --base main \
  --body "Adds AppHost and ServiceDefaults. See docs for details."
# Or just merge directly:
gh pr merge feature/aspire-integration --squash --delete-branch
git checkout main && git pull
```

**Prerequisite:** Docker Desktop must be running (Aspire uses it for the PostgreSQL container).

**How to run it:**
```powershell
cd C:\GitHub\six-to-fix-7
dotnet run --project src/SixToFix.AppHost
```

**What you'll see:**
- Aspire dashboard at a localhost HTTPS URL (printed in the console, typically `https://localhost:15888` or similar)
- The web app running against a real PostgreSQL database container
- All services visible in the dashboard with logs, traces, and metrics

**Notes:**
- In local dev, Aspire manages the PostgreSQL container automatically — no manual connection string needed
- The database starts fresh each run (add a named volume in AppHost if you want persistence — see Aspire docs)
- Aspire is for **LOCAL DEV ONLY**. Production still deploys to Azure App Service + Azure PostgreSQL via GitHub Actions — nothing changes there



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

⚠️ **New prerequisite — GitHub Secret:** Before deploying, add `SF_APP_PASSWORD` to your GitHub repository secrets. This is the password for the `sf_app` PostgreSQL user (the lower-privilege app account). Without it, the `deploy-infra.yml` workflow will fail.

To add it:
1. Go to https://github.com/cdaly33/six-to-fix-7/settings/secrets/actions
2. Click **New repository secret**
3. Name: `SF_APP_PASSWORD`
4. Value: (the password you choose for the sf_app PostgreSQL user)

You'll also use this password in the `ConnectionStrings--DefaultConnection` Key Vault secret below.

---

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

**5. Set all secrets in Key Vault** (run each block separately, replacing placeholders):

> ⚠️ **These prompts keep credentials out of your PowerShell command history.** PowerShell's PSReadLine records every command you type to disk. Using `Read-Host -AsSecureString` means the history entry contains only the prompt text — never the actual secret value.

```powershell
# PostgreSQL runtime connection string (port 6432 = pgBouncer, sf_app least-privilege user)
$secret = Read-Host -Prompt "Enter value for ConnectionStrings--DefaultConnection" -AsSecureString
$plain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "ConnectionStrings--DefaultConnection" `
  --value $plain
$plain = $null   # clear from memory
```

```powershell
# JWT signing key (use the value you generated in step 3)
$secret = Read-Host -Prompt "Enter value for Jwt--SigningKey" -AsSecureString
$plain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "Jwt--SigningKey" `
  --value $plain
$plain = $null
```

```powershell
# HubSpot private app token
$secret = Read-Host -Prompt "Enter value for HubSpot--PrivateAppToken" -AsSecureString
$plain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "HubSpot--PrivateAppToken" `
  --value $plain
$plain = $null
```

```powershell
# HubSpot webhook secret
$secret = Read-Host -Prompt "Enter value for HubSpot--WebhookSecret" -AsSecureString
$plain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "HubSpot--WebhookSecret" `
  --value $plain
$plain = $null
```

```powershell
# Azure OpenAI endpoint URL (not a credential — inline is fine)
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "AzureOpenAI--Endpoint" `
  --value "https://<your-openai-resource>.openai.azure.com/"

# Azure OpenAI deployment name (not a credential — inline is fine)
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "AzureOpenAI--DeploymentName" `
  --value "gpt-4o"

# Azure AI Search endpoint (not a credential — inline is fine)
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "Search--Endpoint" `
  --value "https://<your-search-resource>.search.windows.net"

# Blob Storage endpoint (not a credential — inline is fine)
az keyvault secret set --vault-name kv-sixtofix-dev `
  --name "Storage--BlobEndpoint" `
  --value "https://<your-storage-account>.blob.core.windows.net"
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

The app uses 1 Azure AI Search index:
- `six-to-fix-evidence` — stores chunked client document content for evidence retrieval before audits (uses semantic and vector search)

Other data (skill outputs, council decisions, calibration) lives in PostgreSQL and does not require separate indexes. This index must exist in Azure before the app can run. The team already wrote the provisioning script — you just run it.

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
Endpoint       : https://six-to-fix-search-dev.search.windows.net
API version    : 2024-07-01

Checking index 'six-to-fix-evidence'... creating... done.

Index provisioned successfully.
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

## Project State Snapshot (as of 2026-05-16 — morning)

- **Branch:** `main` — all 6 phases merged, PR #20 infrastructure fixes merged ✅
- **Aspire branch:** `feature/aspire-integration` — pending merge (optional, adds local dev orchestration)
- **Tests:** 84 passing, 0 failing
- **Build:** Clean (`TreatWarningsAsErrors=true` — zero warnings allowed)
- **Architecture:** .NET 10, Blazor Server, Azure PostgreSQL (Flexible with separate `sf_admin` and `sf_app` users), Azure OpenAI (GPT-4o), Azure AI Search (1 index: `six-to-fix-evidence`), Azure Blob Storage, HubSpot integration, PeriodicTimer polling for real-time audit progress updates
- **Auth:** JWT Bearer, four roles: `SuperAdmin`, `TenantAdmin`, `Reviewer`, `Viewer`
- **Pending:** Three infrastructure steps above (EF migration, Key Vault, AI Search) — no code changes needed

Good luck, Chris. The hard part is done. 🎉
