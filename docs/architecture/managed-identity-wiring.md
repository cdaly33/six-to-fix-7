# Managed Identity Wiring

> **Status: LOCKED — Phase 0 Planning Artifact**
>
> Owner: Tank (DevOps & QA) | Phase: 0 | Last updated: 2026-05-10

This document is the authoritative plan for how the Six-to-Fix application authenticates to every Azure service using managed identity. No connection strings, API keys, or SAS tokens appear in app settings (except as Key Vault references where unavoidable).

---

## 1. Why Managed Identity

| Problem (Alternatives) | Solution (Managed Identity) |
|------------------------|------------------------------|
| Connection strings rotated manually | No rotation — identity is the credential |
| Secrets committed to source control by mistake | Impossible — there is no secret to commit |
| SAS token expiry causes production outages | Token refresh is handled by the Azure SDK automatically |
| Service principal client secrets expire after 1–2 years | System-assigned identity is tied to the App Service lifecycle |
| Secret sprawl across multiple vaults and services | Single identity, RBAC grants per resource |

**Summary:** The App Service system-assigned managed identity is granted RBAC roles on each Azure resource. The `DefaultAzureCredential` in the Azure SDK handles token acquisition transparently. Developers authenticate locally via Azure CLI (`az login`). No secrets are passed, stored, or rotated.

---

## 2. DefaultAzureCredential Chain

`DefaultAzureCredential` tries credential sources in order until one succeeds. The chain differs between local development and production.

### 2.1 Local Development

Order tried in a developer workstation (Visual Studio / VS Code / terminal):

```
1. EnvironmentCredential        — AZURE_CLIENT_ID + AZURE_CLIENT_SECRET/CERTIFICATE (service principal, rarely used)
2. WorkloadIdentityCredential   — Kubernetes workload identity (not applicable here)
3. ManagedIdentityCredential    — Only succeeds inside Azure; skipped locally
4. SharedTokenCacheCredential   — Visual Studio cached credentials (Windows only)
5. VisualStudioCredential       — Visual Studio signed-in account (Windows)
6. VisualStudioCodeCredential   — VS Code Azure Account extension
7. AzureCliCredential           ✅ USED LOCALLY — az login token
8. AzurePowerShellCredential    — fallback
9. InteractiveBrowserCredential — last resort
```

**For local dev: run `az login` and select the correct subscription.** The `AzureCliCredential` will be picked up automatically. No env vars required for most developers.

### 2.2 Production (Azure App Service)

Order tried on App Service with system-assigned managed identity:

```
1. EnvironmentCredential        — skipped (no env vars set)
2. WorkloadIdentityCredential   — skipped
3. ManagedIdentityCredential    ✅ USED IN PROD — IMDS endpoint on App Service
   └─ System-assigned identity (no AZURE_CLIENT_ID needed)
4. (remaining sources never reached)
```

### 2.3 C# Registration Pattern

```csharp
// Program.cs — register once, used everywhere
var credential = new DefaultAzureCredential();
builder.Services.AddSingleton<TokenCredential>(credential);

// Each Azure SDK client is then registered using this shared credential
// (see §3 for per-service registration)
```

> **Do not** create a `new DefaultAzureCredential()` inside each service registration. Register it once as a singleton and inject `TokenCredential` where needed. This ensures a single token cache is shared across all clients, reducing IMDS token acquisition calls.

---

## 3. Service-by-Service Wiring

### 3.1 Key Vault

| Property | Value |
|----------|-------|
| SDK Client Class | `SecretClient` (Azure.Security.KeyVault.Secrets) |
| Registration Pattern | `AddAzureKeyVault` on `IConfigurationBuilder` (see §4) |
| Required RBAC Role | `Key Vault Secrets User` (ID: `4633458b-17de-408a-b874-0445c86b69e6`) |
| Scope | Resource (`/subscriptions/.../resourceGroups/.../providers/Microsoft.KeyVault/vaults/kv-strategicglue-{env}`) |
| Dev Setup | `az login` — `AzureCliCredential` is used automatically |

**Notes:**
- Key Vault is wired into `IConfiguration` via `AddAzureKeyVault` at the `ConfigurationBuilder` level, before the DI container is built. This means Key Vault secrets are transparent — code reads them with `config["ConnectionStrings:DefaultConnection"]`, not `secretClient.GetSecretAsync(...)`.
- The `SecretClient` does **not** need to be registered in DI separately (unless you need dynamic secret reads at runtime beyond startup).

### 3.2 Blob Storage

| Property | Value |
|----------|-------|
| SDK Client Class | `BlobServiceClient` (Azure.Storage.Blobs) |
| Registration Pattern | `AddSingleton<BlobServiceClient>` with managed identity |
| Required RBAC Role | `Storage Blob Data Contributor` (ID: `ba92f5b4-2d11-453d-a403-e96b0029c9fe`) |
| Scope | Resource (`/subscriptions/.../providers/Microsoft.Storage/storageAccounts/ststrategicglue{env}`) |
| Dev Setup | `az login` — local identity must have the same RBAC role on the dev storage account |

```csharp
// Program.cs
builder.Services.AddSingleton(sp =>
{
    var accountName = builder.Configuration["Azure:StorageAccountName"]!;
    var credential = sp.GetRequiredService<TokenCredential>();
    return new BlobServiceClient(
        new Uri($"https://{accountName}.blob.core.windows.net"),
        credential);
});
```

### 3.3 Azure OpenAI

| Property | Value |
|----------|-------|
| SDK Client Class | `AzureOpenAIClient` (Azure.AI.OpenAI) |
| Registration Pattern | `AddSingleton<AzureOpenAIClient>` with managed identity |
| Required RBAC Role | `Cognitive Services OpenAI User` (ID: `5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) |
| Scope | Resource (the Azure OpenAI account resource) |
| Dev Setup | `az login` — local identity needs `Cognitive Services OpenAI User` on the dev OpenAI resource |

```csharp
// Program.cs
builder.Services.AddSingleton(sp =>
{
    var endpoint = new Uri(builder.Configuration["Azure:OpenAiEndpoint"]!);
    var credential = sp.GetRequiredService<TokenCredential>();
    return new AzureOpenAIClient(endpoint, credential);
});
```

> The `ISkillRunner` implementation wraps `AzureOpenAIClient` and applies the Polly pipeline (60s timeout → 3 retries exponential backoff on 429/5xx → circuit breaker at 50% failure rate / 60s break duration). In tests, `ISkillRunner` is mocked — `AzureOpenAIClient` is never instantiated.

### 3.4 Azure AI Search

| Property | Value |
|----------|-------|
| SDK Client Class | `SearchClient` (Azure.Search.Documents) |
| Registration Pattern | `AddSingleton<SearchClient>` with managed identity |
| Required RBAC Role | `Search Index Data Reader` (ID: `1407120a-92aa-4202-b7e9-c0e197c71c8f`) |
| Scope | Resource (`/subscriptions/.../providers/Microsoft.Search/searchServices/srch-strategicglue-{env}`) |
| Dev Setup | `az login` — local identity needs the role on the dev Search resource |

```csharp
// Program.cs
builder.Services.AddSingleton(sp =>
{
    var endpoint = new Uri(builder.Configuration["Azure:SearchEndpoint"]!);
    var indexName = builder.Configuration["Azure:SearchIndexName"]!;
    var credential = sp.GetRequiredService<TokenCredential>();
    return new SearchClient(endpoint, indexName, credential);
});
```

---

## 4. Key Vault Reference Pattern (IConfiguration Integration)

Key Vault is added to the `IConfigurationBuilder` pipeline. After this, all secrets are accessible through standard `IConfiguration` — no special Key Vault code in services.

```csharp
// Program.cs — MUST be done before builder.Build()
var keyVaultUri = new Uri(builder.Configuration["Azure:KeyVaultUri"]
    ?? throw new InvalidOperationException("Azure:KeyVaultUri is required"));

var credential = new DefaultAzureCredential();

builder.Configuration.AddAzureKeyVault(keyVaultUri, credential);

// ← From this point, all Key Vault secrets are available as IConfiguration keys.
// Secret named "sf-pgbouncer-connstr" becomes config["sf-pgbouncer-connstr"].
// Key Vault reference in App Settings maps it to:
// config["ConnectionStrings:DefaultConnection"] → sf-pgbouncer-connstr value
```

**Secret naming convention — Key Vault to `IConfiguration` mapping:**

Azure Key Vault secret names use kebab-case. `AddAzureKeyVault` maps double-dashes (`--`) to the `IConfiguration` section separator (`:`). Single dashes are preserved as-is.

| Key Vault Secret Name | `IConfiguration` Key | Notes |
|-----------------------|---------------------|-------|
| `sf-pgbouncer-connstr` | `sf-pgbouncer-connstr` | Resolved via App Service Key Vault reference → `ConnectionStrings:DefaultConnection` |
| `sf-jwt-signing-key` | `sf-jwt-signing-key` | Mapped via `Jwt:SigningKey` binding in `JwtOptions` |
| `sf-hubspot-client-secret` | `sf-hubspot-client-secret` | Mapped via `HubSpot:ClientSecret` binding |
| `sf-hubspot-webhook-secret` | `sf-hubspot-webhook-secret` | Mapped via `HubSpot:WebhookSecret` binding |

> **Important:** The App Service Key Vault reference syntax (`@Microsoft.KeyVault(...)`) resolves the secret into the App Setting value *before* ASP.NET Core reads it. `AddAzureKeyVault` is an additional layer that makes Key Vault secrets available as direct `IConfiguration` keys for any secrets not already surfaced via App Settings. Both mechanisms can coexist.

---

## 5. Startup Validation — Managed Identity Connectivity

The app must fail fast if managed identity cannot authenticate. Use a startup health check that probes Key Vault before the app starts accepting traffic.

```csharp
// In Program.cs, after building the app:
var app = builder.Build();

// Validate Key Vault connectivity synchronously before starting
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var kvUri = new Uri(config["Azure:KeyVaultUri"]!);
        var client = new SecretClient(kvUri, new DefaultAzureCredential());
        // Probe: list one secret to verify auth works
        await client.GetSecretAsync("sf-pgbouncer-connstr");
        logger.LogInformation("Key Vault connectivity verified.");
    }
    catch (AuthenticationFailedException ex)
    {
        logger.LogCritical(ex, "Managed identity cannot authenticate to Key Vault. Aborting startup.");
        throw; // Prevents app from serving traffic with broken config
    }
    catch (RequestFailedException ex) when (ex.Status == 403)
    {
        logger.LogCritical(ex, "Managed identity is authenticated but lacks Key Vault Secrets User role. Aborting startup.");
        throw;
    }
}
```

> In local development, if `az login` has not been run, `AuthenticationFailedException` is thrown with a clear message pointing to the `AzureCliCredential` failure.

---

## 6. Bicep RBAC Assignments

The following `Microsoft.Authorization/roleAssignments` resources must exist in Bicep. They are provisioned in the respective modules.

### 6.1 Key Vault — `modules/keyvault.bicep`

```bicep
// Key Vault Secrets User — for App Service managed identity
resource kvSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appServicePrincipalId, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'  // Key Vault Secrets User
    )
    principalId: appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}
```

### 6.2 Blob Storage — `modules/storage.bicep`

```bicep
// Storage Blob Data Contributor — for App Service managed identity
resource blobContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, appServicePrincipalId, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'ba92f5b4-2d11-453d-a403-e96b0029c9fe'  // Storage Blob Data Contributor
    )
    principalId: appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}
```

### 6.3 Azure OpenAI — `modules/openai.bicep` (or `appservice.bicep`)

```bicep
// Cognitive Services OpenAI User — for App Service managed identity
resource openAiUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAiAccount.id, appServicePrincipalId, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: openAiAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'  // Cognitive Services OpenAI User
    )
    principalId: appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}
```

### 6.4 Azure AI Search — `modules/search.bicep`

```bicep
// Search Index Data Reader — for App Service managed identity
resource searchReaderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, appServicePrincipalId, '1407120a-92aa-4202-b7e9-c0e197c71c8f')
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '1407120a-92aa-4202-b7e9-c0e197c71c8f'  // Search Index Data Reader
    )
    principalId: appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}
```

### 6.5 Role Definition ID Reference Table

| Role | ID | Resource |
|------|----|----------|
| Key Vault Secrets User | `4633458b-17de-408a-b874-0445c86b69e6` | Key Vault |
| Storage Blob Data Contributor | `ba92f5b4-2d11-453d-a403-e96b0029c9fe` | Storage Account |
| Cognitive Services OpenAI User | `5e0bd9bd-7b93-4f28-af87-19fc36ad61bd` | Azure OpenAI |
| Search Index Data Reader | `1407120a-92aa-4202-b7e9-c0e197c71c8f` | AI Search |

> All role definition IDs are well-known Azure built-in roles and do not change between subscriptions.

---

## 7. Local Development Setup

A developer must complete these steps **once** to enable local managed-identity-equivalent auth via Azure CLI.

### 7.1 Steps

```bash
# Step 1: Install Azure CLI (if not installed)
# Windows: winget install Microsoft.AzureCLI
# macOS: brew install azure-cli

# Step 2: Login to Azure
az login
# Follow browser prompt. Use your corporate/Microsoft account that has access to the dev subscription.

# Step 3: Set the correct subscription
az account set --subscription "<subscription-id-or-name>"
# Confirm: az account show

# Step 4: Verify you have the required RBAC roles on dev resources
# Check Key Vault:
az role assignment list --assignee $(az ad signed-in-user show --query id -o tsv) \
  --scope $(az keyvault show -n kv-strategicglue-dev --query id -o tsv)
# Expected role: Key Vault Secrets User

# If roles are missing, ask the DevOps lead to grant them via Bicep or portal.

# Step 5: Set the Key Vault URI in local user secrets (NOT in appsettings.json)
dotnet user-secrets set "Azure:KeyVaultUri" "https://kv-strategicglue-dev.vault.azure.net/"
# Run from the SixToFix.Api project directory

# Step 6: Verify local auth works
az keyvault secret show --vault-name kv-strategicglue-dev --name sf-pgbouncer-connstr
# If this returns the secret value, DefaultAzureCredential will work in the app too.
```

### 7.2 `appsettings.Development.json` Pattern

Non-secret dev values (endpoint URLs, account names) live in `appsettings.Development.json`. Secrets are **never** in this file — they come from Key Vault via `AzureCliCredential`.

```json
{
  "Azure": {
    "KeyVaultUri": "https://kv-strategicglue-dev.vault.azure.net/",
    "StorageAccountName": "ststrategicgluedev",
    "SearchEndpoint": "https://srch-strategicglue-dev.search.windows.net",
    "SearchIndexName": "audit-index",
    "OpenAiEndpoint": "https://oai-strategicglue-dev.openai.azure.com/",
    "OpenAiDeploymentName": "gpt-4o"
  },
  "Jwt": {
    "Issuer": "https://localhost:7001",
    "Audience": "six-to-fix",
    "TokenExpiryMinutes": 120
  },
  "HubSpot": {
    "PortalId": "<your-dev-portal-id>",
    "BaseUrl": "https://api.hubapi.com"
  }
}
```

### 7.3 `AZURE_CLIENT_ID` (Optional — Service Principal Override)

If a developer does not have direct Azure CLI access (e.g., contractor with limited subscription access), a service principal can be used:

```bash
export AZURE_CLIENT_ID="<sp-client-id>"
export AZURE_TENANT_ID="<tenant-id>"
export AZURE_CLIENT_SECRET="<sp-secret>"  # Store in shell profile, NOT in code
```

`EnvironmentCredential` (step 1 in the chain) will pick this up before `AzureCliCredential`. This is the exception, not the norm.

---

## ⚠️ Open Questions

1. **Azure OpenAI subscription placement:** If the Azure OpenAI resource is in a different subscription than the App Service, `DefaultAzureCredential` with system-assigned managed identity will not work across subscriptions. Confirm the OpenAI resource is in `rg-StrategicGlue-CommandCenter`.
2. **AI Search — Data Reader vs Contributor:** The task specifies `Search Index Data Reader` (read-only). If the app needs to push documents to the index (e.g., indexing audit results), `Search Index Data Contributor` is required. Confirm the app's search write requirements.
3. **GitHub Actions OIDC identity:** The federated credential for GitHub Actions (used in `deploy.yml`) needs its own RBAC grants (Contributor on resource group). This is separate from the App Service managed identity and documented in the GitHub Actions DAG artifact.
