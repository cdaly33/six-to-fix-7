# Azure Deployment Guide — Six-to-Fix

## Overview

This guide shows two supported manual deployment paths for Six-to-Fix in Azure:

1. **Path A: Azure Portal (Step-by-Step Web UI)** — the primary path, written for a first-time Azure portal user.
2. **Path B: Bicep CLI (Automated)** — the faster alternative once you are comfortable with `az` commands.

This guide intentionally does **not** use GitHub Actions for Azure deployment. Infrastructure should be deployed manually through the Azure Portal or with `az deployment group create`, and the app should be deployed manually to Azure App Service via ZIP deploy.

> **GitHub Actions Deployment (Optional)**: If you prefer automated deployments from GitHub Actions, see **[OIDC-SETUP.md](./OIDC-SETUP.md)** to configure Azure OIDC (Workload Identity Federation). This allows the workflow to deploy without storing long-lived secrets in the repository.

The examples below use the **prod** environment and these expected names:

- Resource group: `rg-sixtofix-prod`
- App Service Plan: `asp-sixtofix-prod`
- Web App: `app-sixtofix-prod`
- PostgreSQL Flexible Server: `psql-sixtofix-prod`
- Key Vault: `kv-sixtofix-prod`
- Azure AI Search: `srch-sixtofix-prod`
- Application Insights: `appi-sixtofix-prod`
- Log Analytics workspace: `law-sixtofix-prod`
- Storage account: choose a globally unique lowercase name such as `stsixtofixprod1234`

> **Important:** Pick one Azure region up front (recommended: `centralus`) and use that same region for every resource unless Azure requires otherwise. Note: `eastus2` has quota restrictions for PostgreSQL Flexible Server on some subscriptions — use `centralus` to avoid this.

## Before You Start — Checklist

Before you click anything, make sure you have all of the following:

- An Azure subscription where you can create resources.
- Permission to create resource groups, App Service, PostgreSQL, Key Vault, Storage, AI Search, Application Insights, and role assignments.
- The Azure region you want to use. Recommended: **Central US** (`centralus`).
- Azure CLI installed if you want the Bicep or ZIP deploy path:
  - https://learn.microsoft.com/cli/azure/install-azure-cli-windows
- .NET SDK installed locally so you can publish the app.
- PowerShell and `psql` available locally, or a plan to use Azure Cloud Shell.
- The Azure/Entra tenant ID:
  - Azure Portal -> **Microsoft Entra ID** -> **Overview** -> **Tenant ID**
- A strong PostgreSQL admin password for `sfadmin`.
- A second strong PostgreSQL password for the runtime `sf_app` user.
- HubSpot values, if applicable:
  - `HubSpot--PrivateAppToken`
  - `HubSpot--WebhookSecret`
- Azure OpenAI API key, if applicable:
  - `AzureOpenAI--ApiKey`
- A random JWT signing key that is at least 64 characters.

Recommended local prep:

```powershell
cd C:\GitHub\six-to-fix-7
dotnet build SixToFix.slnx
```

## Path A: Azure Portal (Step-by-Step Web UI)

This path assumes you want to create everything manually in the Azure Portal with detailed click-by-click guidance.

### Phase 1: Create the Resource Group

1. Open your browser and go to **https://portal.azure.com**.
2. Sign in.
3. In the top search bar, type **Resource groups**.
4. Click **Resource groups** in the search results.
5. Click **+ Create**.
6. On the **Basics** tab, fill in:
   - **Subscription**: select your subscription.
   - **Resource group**: `rg-sixtofix-prod`
   - **Region**: **Central US** (or your preferred region)
7. Click **Review + create**.
8. Wait for validation to finish.
9. Click **Create**.
10. Wait until Azure shows **Your deployment is complete**.

> **Important:** Write down the region you used. Use the same region again for PostgreSQL, Key Vault, App Service, Storage, Search, and Monitoring.

### Phase 2: Create PostgreSQL Flexible Server

1. In the Azure Portal top search bar, type **Azure Database for PostgreSQL flexible servers**.
2. Click **Azure Database for PostgreSQL flexible servers**.
3. Click **+ Create**.
4. Choose **Flexible server**.
5. On the **Basics** tab, fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Server name**: `psql-sixtofix-prod`
   - **Region**: the same region as the resource group
   - **PostgreSQL version**: **16**
   - **Workload type**: **Production**
   - **Compute + storage**: choose **Configure server** or **Configure** and set:
     - **SKU / Compute tier**: **Standard_D4s_v3**
     - **vCores**: **4**
     - **Storage**: **128 GB**
   - **Availability zone / High availability**: if Azure offers **Zone-redundant HA**, that matches the Bicep intent for prod. If cost is a concern for the first deploy, you can skip HA initially.
   - **Admin username**: `sfadmin`
   - **Password**: choose a strong password
   - **Confirm password**: enter the same password
6. **Write the `sfadmin` password down somewhere safe.** You will need it later for migrations.
7. Go to the **Networking** tab.
8. Set networking so the server uses **Public access**.
9. Turn on the checkbox that says something like **Allow public access from any Azure service within Azure to this server**.
10. If you want to connect from your own machine with `psql`, add a firewall rule for your client IP:
    - Click **+ Add current client IP address** if Azure shows that button, or manually add your IP.
11. Click **Review + create**.
12. After validation passes, click **Create**.
13. When the server finishes deploying, open the new PostgreSQL resource.
14. In the left menu, click **Server parameters**.
15. Search for `require_secure_transport`.
16. Set it to **ON**.
17. Click **Save** if the portal requires it.
18. In **Server parameters**, search for either:
    - `pgbouncer.enabled`, or
    - `connection_pooling`
19. Turn PgBouncer / connection pooling on:
    - If you see `connection_pooling`, set it to **PgBouncer**.
    - If you see `pgbouncer.enabled`, set it to **true**.
20. Save the change.
21. Go back to **Overview**.
22. Copy the **Server name** / **Fully qualified domain name**. It will look like:

```text
psql-sixtofix-prod.postgres.database.azure.com
```

23. In the left menu, click **Databases**.
24. Click **+ Add**.
25. Fill in:
   - **Database name**: `sixtofix`
   - **Charset**: `UTF8`
   - **Collation**: `en_US.utf8`
26. Click **Save** or **Create**.

### Phase 3: Create Key Vault

1. In the portal search bar, type **Key vaults**.
2. Click **Key vaults**.
3. Click **+ Create**.
4. On **Basics**, fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Key vault name**: `kv-sixtofix-prod`
   - **Region**: the same region
   - **Pricing tier**: **Standard**
5. Go to the settings area where Azure asks about deletion retention.
6. Set:
   - **Days to retain deleted vaults**: **90**
   - **Purge protection**: **Enabled**
7. Go to the **Access configuration** tab.
8. For **Permission model**, choose **Azure role-based access control**.
9. Double-check that you did **not** leave it on **Vault access policy**.
10. Click **Review + create**.
11. Click **Create**.

### Phase 4: Create App Service Plan + Web App

#### Sub-section A: App Service Plan

1. In the portal search bar, type **App Service plans**.
2. Click **App Service plans**.
3. Click **+ Create**.
4. Fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Name**: `asp-sixtofix-prod`
   - **Operating System**: **Linux**
   - **Region**: same region
5. For **Pricing plan**, choose **Premium V3 P2v3**.
6. Click **Review + create**.
7. Click **Create**.

#### Sub-section B: Web App

1. In the portal search bar, type **App Services**.
2. Click **App Services**.
3. Click **+ Create**.
4. Choose **Web App**.
5. On the **Basics** tab, fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Name**: `app-sixtofix-prod`
   - **Publish**: **Code**
   - **Runtime stack**: **.NET 10 (LTS)**
   - **Operating System**: **Linux**
   - **Region**: same region
   - **Linux Plan**: select `asp-sixtofix-prod`
6. Open the **Identity** tab during creation if the wizard shows it.
7. Set **System assigned** to **On**.
8. Click **Review + create**.
9. Click **Create**.
10. After deployment finishes, open the `app-sixtofix-prod` Web App.
11. In the left menu, open **Settings** -> **Configuration**.
12. On **General settings**, confirm or set:
   - **Always On**: **On**
   - **ARR Affinity / Client affinity**: **On**
   - **HTTPS Only**: **On**
   - **Minimum TLS Version**: **1.2**
   - **FTP State**: **Disabled**
   - **HTTP version**: **2.0**
13. Click **Save**.
14. Still under **Configuration**, go to **Application settings**.
15. Add these initial settings:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `WEBSITE_RUN_FROM_PACKAGE` = `1`
16. Click **Save**.
17. In the left menu, click **Identity**.
18. Confirm **System assigned** is **On**. If it is off, turn it on and click **Save** now.

### Phase 5: Create Storage Account

1. In the portal search bar, type **Storage accounts**.
2. Click **Storage accounts**.
3. Click **+ Create**.
4. Fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Storage account name**: choose a globally unique name such as `stsixtofixprod1234`
     - Must be all lowercase
     - Must contain no spaces
     - Must contain no hyphens
     - Must be 3-24 characters
   - **Region**: same region
   - **Performance**: **Standard**
   - **Redundancy**: **GRS** for prod, or **LRS** if you want the cheaper dev-style option
5. Click **Review + create**.
6. Click **Create**.
7. After deployment, open the storage account.
8. In the left menu, open **Endpoints**.
9. Copy the **Blob service** URL. You will need it later for `Storage__BlobEndpoint`.

### Phase 6: Create Azure AI Search

1. In the portal search bar, type **Search services**.
2. Click **Search services**.
3. Click **+ Create**.
4. Fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Service name**: `srch-sixtofix-prod`
   - **Location**: same region
   - **Pricing tier**: **Standard**
5. If the portal exposes scale settings during creation or immediately after creation, set:
   - **Replicas**: **2**
   - **Partitions**: **1**
6. Click **Review + create**.
7. Click **Create**.
8. After deployment, open the search service.
9. On **Overview**, copy the endpoint URL. It will look like:

```text
https://srch-sixtofix-prod.search.windows.net
```

### Phase 7: Create Application Insights + Log Analytics

#### First: Log Analytics Workspace

1. In the portal search bar, type **Log Analytics workspaces**.
2. Click **Log Analytics workspaces**.
3. Click **+ Create**.
4. Fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Name**: `law-sixtofix-prod`
   - **Region**: same region
5. Click **Review + create**.
6. Click **Create**.

#### Then: Application Insights

1. In the portal search bar, type **Application Insights**.
2. Click **Application Insights**.
3. Click **+ Create**.
4. Fill in:
   - **Subscription**: your subscription
   - **Resource group**: `rg-sixtofix-prod`
   - **Name**: `appi-sixtofix-prod`
   - **Region**: same region
   - **Resource Mode**: **Workspace-based**
   - **Log Analytics Workspace**: select `law-sixtofix-prod`
5. Click **Review + create**.
6. Click **Create**.
7. After deployment, open `appi-sixtofix-prod`.
8. On **Overview**, copy the **Connection String**. It usually starts with `InstrumentationKey=`.

### Phase 8: Wire Managed Identity Permissions

This phase gives the Web App permission to read secrets and talk to other Azure resources without storing credentials.

#### a) Get the Web App's managed identity principal ID

1. Go to **App Services**.
2. Open **app-sixtofix-prod**.
3. In the left menu, click **Identity**.
4. Under the **System assigned** section, make sure it says **Status: On**.
5. Copy the **Object (principal) ID** somewhere temporary.

#### b) Key Vault — grant secret read access

1. Go to **Key vaults**.
2. Open **kv-sixtofix-prod**.
3. In the left menu, click **Access control (IAM)**.
4. Click **+ Add**.
5. Click **Add role assignment**.
6. In the role search box, search for **Key Vault Secrets User**.
7. Select **Key Vault Secrets User**.
8. Click **Next**.
9. For **Assign access to**, choose **Managed identity**.
10. Click **+ Select members**.
11. In the selector:
    - For the managed identity type, choose **App Service** if Azure asks.
    - Find and select `app-sixtofix-prod`.
12. Click **Select**.
13. Click **Review + assign**.

#### c) Storage Account — grant blob access

1. Go to **Storage accounts**.
2. Open your storage account.
3. Click **Access control (IAM)**.
4. Click **+ Add** -> **Add role assignment**.
5. Search for and select **Storage Blob Data Contributor**.
6. Click **Next**.
7. Choose **Managed identity**.
8. Click **+ Select members**.
9. Select `app-sixtofix-prod`.
10. Click **Select**.
11. Click **Review + assign**.

#### d) Azure AI Search — grant index access

1. Go to **Search services**.
2. Open **srch-sixtofix-prod**.
3. Click **Access control (IAM)**.
4. Click **+ Add** -> **Add role assignment**.
5. Search for and select **Search Index Data Contributor**.
6. Click **Next**.
7. Choose **Managed identity**.
8. Click **+ Select members**.
9. Select `app-sixtofix-prod`.
10. Click **Select**.
11. Click **Review + assign**.

#### e) Optional: Azure OpenAI

If you already have an Azure OpenAI resource and want the app's managed identity to use it:

1. Open your Azure OpenAI resource.
2. Click **Access control (IAM)**.
3. Click **+ Add** -> **Add role assignment**.
4. Search for and select **Cognitive Services OpenAI User**.
5. Click **Next**.
6. Choose **Managed identity**.
7. Click **+ Select members**.
8. Select `app-sixtofix-prod`.
9. Click **Select**.
10. Click **Review + assign**.

### Phase 9: Add Secrets to Key Vault

#### First, give yourself permission to add secrets

1. Go to **Key Vaults** -> **kv-sixtofix-prod**.
2. Click **Access control (IAM)**.
3. Click **+ Add** -> **Add role assignment**.
4. Search for **Key Vault Secrets Officer**.
5. Select **Key Vault Secrets Officer**.
6. Click **Next**.
7. For **Assign access to**, choose **User, group, or service principal**.
8. Click **+ Select members**.
9. Search for your own user account and select it.
10. Click **Select**.
11. Click **Review + assign**.

#### Then add the secrets

1. In the Key Vault left menu, click **Secrets**.
2. Click **+ Generate/Import** for each secret below.
3. For each one, set:
   - **Upload options / Upload method**: **Manual**
   - **Name**: exact value shown below
   - **Value**: exact secret value shown below
4. Click **Create** after each secret.

Add these secrets exactly:

**Secret 1**
- **Name**: `ConnectionStrings--DefaultConnection`
- **Value**:

```text
Host=psql-sixtofix-prod.postgres.database.azure.com;Port=6432;Database=sixtofix;Username=sf_app;Password=YOUR_SF_APP_PASSWORD;No Reset On Close=true;Ssl Mode=Require
```

**Secret 2**
- **Name**: `ConnectionStrings--AdminConnection`
- **Value**:

```text
Host=psql-sixtofix-prod.postgres.database.azure.com;Port=5432;Database=sixtofix;Username=sfadmin;Password=YOUR_SFADMIN_PASSWORD;Ssl Mode=Require;Trust Server Certificate=false
```

**Secret 3**
- **Name**: `Jwt--SigningKey`
- **Value**: generate a random 64+ character string.

PowerShell example:

```powershell
[System.Convert]::ToBase64String((New-Object byte[] 64 | ForEach-Object { Get-Random -Maximum 256 } ))
```

**Secret 4**
- **Name**: `HubSpot--PrivateAppToken`
- **Value**: from HubSpot -> **Settings** -> **Integrations** -> **Private Apps** -> your app -> copy the token

**Secret 5**
- **Name**: `HubSpot--WebhookSecret`
- **Value**: from HubSpot -> **Settings** -> **Integrations** -> **Webhooks** -> your webhook -> copy the secret

**Secret 6**
- **Name**: `AzureOpenAI--ApiKey`
- **Value**: from Azure Portal -> your Azure OpenAI resource -> **Keys and Endpoint** -> copy **Key 1**

#### Now wire the Web App to read the Key Vault values

1. Go to **App Services** -> **app-sixtofix-prod**.
2. Click **Configuration**.
3. Stay on **Application settings**.
4. Add the following settings exactly.
5. Click **Save** when done.

| Setting Name | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | `@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=ConnectionStrings--DefaultConnection)` |
| `ConnectionStrings__AdminConnection` | `@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=ConnectionStrings--AdminConnection)` |
| `Jwt__SigningKey` | `@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=Jwt--SigningKey)` |
| `HubSpot__PrivateAppToken` | `@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=HubSpot--PrivateAppToken)` |
| `AzureOpenAI__ApiKey` | `@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=AzureOpenAI--ApiKey)` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | paste the Application Insights connection string from Phase 7 |
| `KeyVault__Uri` | `https://kv-sixtofix-prod.vault.azure.net/` |
| `Search__Endpoint` | `https://srch-sixtofix-prod.search.windows.net` |
| `Storage__BlobEndpoint` | paste the Blob service endpoint from your storage account |

> **Important naming note:**
>
> - App Service settings use `__` (double underscore).
> - Key Vault secret names use `--` (double dash).
> - In .NET configuration, both map to the same logical key.

> **Why is `HubSpot--WebhookSecret` not in the App Service settings table above?**
>
> The app loads the full Key Vault when `KeyVault__Uri` is set and the managed identity has Key Vault access, so `HubSpot--WebhookSecret` can be read directly from Key Vault without a separate `HubSpot__WebhookSecret` application setting.

After you click **Save**, the Web App will restart.

### Phase 10: Create the sf_app PostgreSQL User

The app should run as a lower-privilege PostgreSQL user named `sf_app`. The Bicep and portal resource creation do **not** create that database user for you.

You have two ways to connect:

- **Option A:** Add your machine IP to PostgreSQL firewall rules and use local `psql`
- **Option B:** Use Azure Portal -> **Cloud Shell** -> **Bash**

Using `psql`:

```bash
psql "host=psql-sixtofix-prod.postgres.database.azure.com port=5432 dbname=sixtofix user=sfadmin sslmode=require"
# Enter sfadmin password when prompted

# Once connected, run:
CREATE USER sf_app WITH PASSWORD 'YOUR_SF_APP_PASSWORD';
GRANT CONNECT ON DATABASE sixtofix TO sf_app;
\c sixtofix
GRANT USAGE ON SCHEMA public TO sf_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sf_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sf_app;
```

The password in `CREATE USER sf_app WITH PASSWORD 'YOUR_SF_APP_PASSWORD';` must match the password you used in the `ConnectionStrings--DefaultConnection` Key Vault secret.

### Phase 11: Run EF Core Migrations

Run migrations from your local machine.

> **Important repo-specific note:** the runtime app setting is named `ConnectionStrings__AdminConnection`, but EF tooling in this repo currently reads the `DESIGN_TIME_CONNECTION_STRING` environment variable when you run `dotnet ef`.

```powershell
cd C:\GitHub\six-to-fix-7

# Set the admin connection string. Use port 5432 for migrations.
$env:DESIGN_TIME_CONNECTION_STRING = "Host=psql-sixtofix-prod.postgres.database.azure.com;Port=5432;Database=sixtofix;Username=sfadmin;Password=YOUR_SFADMIN_PASSWORD;Ssl Mode=Require;Trust Server Certificate=false"

# Apply migrations
dotnet ef database update --project src\SixToFix.Infrastructure --startup-project src\SixToFix.Web

# Verify the tables exist
psql "host=psql-sixtofix-prod.postgres.database.azure.com port=5432 dbname=sixtofix user=sfadmin sslmode=require" -c "\dt"
```

> **Important:** Migrations use the admin connection on port **5432**, not the runtime PgBouncer port **6432**.

### Phase 12: Build and Deploy the App

#### Option A: ZIP deploy with Azure CLI

```powershell
cd C:\GitHub\six-to-fix-7

# Build and publish the web app
dotnet publish src\SixToFix.Web\SixToFix.Web.csproj -c Release -o .\publish

# Zip the published output
Compress-Archive -Path .\publish\* -DestinationPath .\deploy.zip -Force

# Sign in if needed
az login

# Deploy to App Service
az webapp deploy `
  --resource-group rg-sixtofix-prod `
  --name app-sixtofix-prod `
  --src-path .\deploy.zip `
  --type zip

# Clean up
Remove-Item .\deploy.zip -Force
Remove-Item .\publish -Recurse -Force
```

#### Option B: ZIP deploy in the Azure Portal

You can also deploy the ZIP package through the portal:

1. Build and zip the app locally first using the same `dotnet publish` and `Compress-Archive` steps above.
2. Go to **App Services** -> **app-sixtofix-prod**.
3. Look for either:
   - **Advanced Tools (Kudu)** -> **Zip Push Deploy**, or
   - **Deployment Center** -> **Manual deployment** -> **ZIP** / **Deploy from package**
4. Upload `deploy.zip`.
5. Wait for the deployment to finish.

> **Important:** This app runs on App Service with `WEBSITE_RUN_FROM_PACKAGE=1`, so ZIP deploy is the correct deployment model.

### Phase 13: Verify the Deployment

1. Open the site in your browser:

```text
https://app-sixtofix-prod.azurewebsites.net
```

2. If the app loads, that is your first success check.
3. In Azure Portal, go to **Application Insights** -> **appi-sixtofix-prod** -> **Live Metrics** and confirm you see live traffic and no repeated startup failures.
4. Go to **App Services** -> **app-sixtofix-prod** -> **Log stream** and watch startup logs.
5. If you see **Key Vault reference** errors in App Service configuration, go back to **Phase 8** and re-check the managed identity role assignment.
6. If the home page loads but features fail, re-check the Key Vault secret values and PostgreSQL user setup.

## Path B: Bicep CLI (Automated)

This is the faster, more repeatable path. It uses the Bicep that already exists in `infra\`.

### Prerequisites

Before you run the Bicep deployment:

1. Install Azure CLI if needed.
2. Open PowerShell.
3. Sign in:

```powershell
az login
```

4. If you have more than one subscription, set the correct one:

```powershell
az account set --subscription "YOUR_SUBSCRIPTION_ID"
```

5. Make sure Bicep is available. Azure CLI can install it for you:

```powershell
az bicep install
```

6. Create the resource group first. The Bicep deploys **into an existing resource group**:

```powershell
az group create --name rg-sixtofix-prod --location centralus
```

### Step 1: Fill in prod.bicepparam

Open this file:

```text
infra/params/prod.bicepparam
```

Replace it with this content if needed:

```bicep
using '../main.bicep'
param environment = 'prod'
param appName = 'six-to-fix' // Leave as-is — this gets normalized to 'sixtofix' in resource names.
param tenantId = 'REPLACE_WITH_YOUR_TENANT_ID' // Get this from Azure Portal -> Microsoft Entra ID -> Overview -> Tenant ID
```

What each parameter means:

- `environment`: leave as `'prod'`
- `appName`: leave as `'six-to-fix'`; the Bicep converts it to `sixtofix` for names like `app-sixtofix-prod`
- `tenantId`: your Microsoft Entra tenant GUID from the Azure Portal

Do **not** put passwords in this file.

### Step 2: Run the Bicep Deployment

From the repo root:

```powershell
cd C:\GitHub\six-to-fix-7

# Set secrets as environment variables — never put them in files
$env:POSTGRES_ADMIN_PASSWORD = "STRONG_PASSWORD_HERE"
$env:SF_APP_PASSWORD         = "ANOTHER_STRONG_PASSWORD_HERE"

az deployment group create `
  --resource-group rg-sixtofix-prod `
  --template-file infra/main.bicep `
  --parameters infra/params/prod.bicepparam
```

> **Why environment variables?**  `.bicepparam` files require ALL parameters declared inside them — you can't supplement them with `--parameters` on the command line the way JSON params files allow. Using `readEnvironmentVariable()` in the params file keeps secrets out of source control and off your command-line history.

What each secure parameter means:

- `POSTGRES_ADMIN_PASSWORD` → `postgresAdminPassword`
  - This becomes the password for the PostgreSQL admin login `sfadmin`.
  - Use a strong password: 20+ characters, upper/lowercase, numbers, symbols.
  - Save it somewhere secure because you need it later for migrations.
- `SF_APP_PASSWORD` → `sfAppPassword`
  - This is the runtime password for the lower-privilege PostgreSQL user `sf_app`.
  - It must be different from the admin password.
  - Save it too, because you need it again when validating the runtime connection.
- `openAiAccountName`
  - If you already have an Azure OpenAI resource and want Bicep to assign the managed identity role, put the Azure OpenAI resource name here.
  - If you do **not** have Azure OpenAI yet, leave it as an empty string: `""`

What Bicep creates for you:

- App Service Plan: `asp-sixtofix-prod`
- Web App: `app-sixtofix-prod`
- PostgreSQL Flexible Server: `psql-sixtofix-prod`
- Key Vault: `kv-sixtofix-prod`
- Storage account with a generated unique name
- Azure AI Search: `srch-sixtofix-prod`
- Application Insights: `appi-sixtofix-prod`
- Log Analytics workspace: `law-sixtofix-prod`
- Managed identity role assignments for Key Vault, Storage, Search, and optionally Azure OpenAI
- Two bootstrap Key Vault secrets:
  - `ConnectionStrings--DefaultConnection`
  - `ConnectionStrings--AdminConnection`

### Step 3: Post-Bicep Manual Steps (same as Portal Phase 9-13 above)

After the Bicep deployment succeeds, you still need to do the manual post-deploy work.

Do these steps from **Path A**:

1. **Phase 9** — add the missing Key Vault secrets manually:
   - `Jwt--SigningKey`
   - `HubSpot--PrivateAppToken`
   - `HubSpot--WebhookSecret`
   - `AzureOpenAI--ApiKey`
2. **Phase 10** — create the `sf_app` PostgreSQL user.
3. **Phase 11** — run EF Core migrations.
4. **Phase 12** — publish, zip, and deploy the app.
5. **Phase 13** — verify the deployment in the browser, App Service logs, and Application Insights.

## Troubleshooting

### "Key Vault reference" error in App Service configuration

Most likely cause: the Web App's system-assigned managed identity does not have the **Key Vault Secrets User** role on `kv-sixtofix-prod`.

Fix:

1. Go to the Web App -> **Identity** and confirm system-assigned identity is **On**.
2. Go to the Key Vault -> **Access control (IAM)**.
3. Re-add the **Key Vault Secrets User** role assignment for `app-sixtofix-prod`.
4. Wait a minute, then restart the Web App.

### App crashes immediately on startup

Most likely cause: a missing or incorrect secret.

Check:

1. App Service -> **Log stream**
2. Application Insights -> **Failures** / **Live Metrics**
3. Key Vault -> **Secrets**
4. App Service -> **Configuration**

Common missing values:

- `Jwt--SigningKey`
- `ConnectionStrings--DefaultConnection`
- `ConnectionStrings--AdminConnection`
- `Search__Endpoint`
- `Storage__BlobEndpoint`

### SSL error when connecting to PostgreSQL

Add `Ssl Mode=Require` to the connection string.

Use:

```text
Host=...;Port=5432;Database=sixtofix;Username=sfadmin;Password=...;Ssl Mode=Require
```

or:

```text
Host=...;Port=6432;Database=sixtofix;Username=sf_app;Password=...;No Reset On Close=true;Ssl Mode=Require
```

### "authentication failed for user sf_app"

Most likely cause: the `sf_app` user does not exist yet, or the password in PostgreSQL does not match the password in `ConnectionStrings--DefaultConnection`.

Fix:

1. Repeat **Phase 10**.
2. Re-run `CREATE USER sf_app WITH PASSWORD '...'` with the intended runtime password.
3. Update the Key Vault secret if needed.
4. Restart the Web App.

### 503 errors from the Web App

Check whether the app is actually running:

```powershell
az webapp show --resource-group rg-sixtofix-prod --name app-sixtofix-prod
```

Also check:

- App Service -> **Overview**
- App Service -> **Log stream**
- App Service -> **Configuration**
- Application Insights -> **Live Metrics**

### Bicep deployment fails with "already exists"

That usually means the resources are already present in the resource group.

Notes:

- `az deployment group create` uses **Incremental** mode by default.
- Incremental mode updates matching resources instead of deleting unrelated ones.
- If you are re-running the same deployment, verify you are pointing at the correct resource group and that the existing resources have the expected names.

## Updating the App (Re-deploying Without Infra Changes)

If infrastructure is already in place and you only changed application code, you do **not** need to redeploy Bicep. Just rebuild, zip, and redeploy the Web App package.

```powershell
cd C:\GitHub\six-to-fix-7

dotnet publish src\SixToFix.Web\SixToFix.Web.csproj -c Release -o .\publish
Compress-Archive -Path .\publish\* -DestinationPath .\deploy.zip -Force
az webapp deploy --resource-group rg-sixtofix-prod --name app-sixtofix-prod --src-path .\deploy.zip --type zip
Remove-Item .\deploy.zip -Force
Remove-Item .\publish -Recurse -Force
```

This is the preferred update path when infrastructure settings have not changed.
