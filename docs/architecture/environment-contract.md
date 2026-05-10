# Environment Contract

> **Status: LOCKED — Phase 0 Planning Artifact**
>
> Owner: Tank (DevOps & QA) | Phase: 0 | Last updated: 2026-05-10

This document is the authoritative contract for all configuration, secrets, managed identity bindings, and environment-specific values. Nothing ships until this contract is satisfied in both dev and prod.

---

## 1. App Settings Schema

All configuration enters the App Service as **App Settings** (environment variables surfaced by ASP.NET Core). Secrets use Key Vault references. Non-secret values are set directly in the App Service configuration blade (or Bicep `appSettings` array).

### 1.1 Grouped Configuration Keys

#### ConnectionStrings

| Key | Source | Purpose |
|-----|--------|---------|
| `ConnectionStrings:DefaultConnection` | `KV:sf-pgbouncer-connstr` | Primary PostgreSQL connection string via pgBouncer (port 6432) |

#### Azure

| Key | Source | Example Value (dev) | Purpose |
|-----|--------|---------------------|---------|
| `Azure:KeyVaultUri` | `AppSettings` | `https://kv-strategicglue-dev.vault.azure.net/` | Key Vault base URI for `AddAzureKeyVault` bootstrap |
| `Azure:StorageAccountName` | `AppSettings` | `ststrategicgluedev` | Blob Storage SDK — account name only; auth via managed identity |
| `Azure:StorageBlobContainerName` | `AppSettings` | `audit-artifacts` | Blob container name for audit report artifacts |
| `Azure:SearchEndpoint` | `AppSettings` | `https://srch-strategicglue-dev.search.windows.net` | AI Search endpoint; auth via managed identity |
| `Azure:SearchIndexName` | `AppSettings` | `audit-index` | Target search index name |
| `Azure:OpenAiEndpoint` | `AppSettings` | `https://oai-strategicglue-dev.openai.azure.com/` | Azure OpenAI endpoint; auth via managed identity |
| `Azure:OpenAiDeploymentName` | `AppSettings` | `gpt-4o` | Azure OpenAI deployment name |

#### Jwt

| Key | Source | Example Value | Purpose |
|-----|--------|---------------|---------|
| `Jwt:SigningKey` | `KV:sf-jwt-signing-key` | _(256-bit random hex)_ | HMAC-SHA256 signing key for JWT tokens |
| `Jwt:Issuer` | `AppSettings` | `https://app-strategicglue-dev.azurewebsites.net` | JWT issuer claim (`iss`) |
| `Jwt:Audience` | `AppSettings` | `six-to-fix` | JWT audience claim (`aud`) |
| `Jwt:TokenExpiryMinutes` | `AppSettings` | `60` | Access token lifetime in minutes |

#### HubSpot

| Key | Source | Purpose |
|-----|--------|---------|
| `HubSpot:PrivateAppToken` | `KV:sf-hubspot-private-app-token` | HubSpot Private App bearer token for outbound API calls |
| `HubSpot:WebhookSecret` | `KV:sf-hubspot-webhook-secret` | HMAC-SHA256 secret for inbound webhook signature validation |
| `HubSpot:PortalId` | `AppSettings` | HubSpot portal/account ID (non-secret) |
| `HubSpot:BaseUrl` | `AppSettings` | `https://api.hubapi.com` | HubSpot API base URL |

#### ApplicationInsights

| Key | Source | Purpose |
|-----|--------|---------|
| `ApplicationInsights:ConnectionString` | `AppSettings` | Application Insights connection string (contains instrumentation key only — no secret) |

#### SignalR / Blazor

| Key | Source | Example Value | Purpose |
|-----|--------|---------------|---------|
| `SignalR:HubPath` | `AppSettings` | `/hubs/audit-run` | Audit run progress hub path |

#### ASPNETCORE

| Key | Source | Example Value | Purpose |
|-----|--------|---------------|---------|
| `ASPNETCORE_ENVIRONMENT` | `AppSettings` | `Development` or `Production` | ASP.NET Core environment selector |

### 1.2 Key Vault Reference Syntax (App Service)

When an App Setting value is sourced from Key Vault, use this format in Bicep:

```
@Microsoft.KeyVault(SecretUri=https://kv-strategicglue-{env}.vault.azure.net/secrets/{secret-name}/)
```

Example:
```
@Microsoft.KeyVault(SecretUri=https://kv-strategicglue-dev.vault.azure.net/secrets/sf-pgbouncer-connstr/)
```

The App Service managed identity must have `Key Vault Secrets User` RBAC on the Key Vault (or Get/List on the secret) for Key Vault references to resolve at startup. If the reference cannot resolve, the App Service **will not start**.

---

## 2. Key Vault Secrets — Exhaustive List

All secrets below must exist in Key Vault before the application can start. The name column uses the canonical kebab-case name used in Key Vault.

| Secret Name | Content | Rotation Policy | Notes |
|-------------|---------|-----------------|-------|
| `sf-pgbouncer-connstr` | Full ADO.NET connection string to pgBouncer on port 6432 | On DB password change | Format: `Host=psql-strategicglue-{env}.postgres.database.azure.com;Port=6432;Database=strategicglue;Username=sf_app;Password=...;SSL Mode=Require;Trust Server Certificate=true` |
| `sf-jwt-signing-key` | 256-bit (32-byte) random hex string | Every 90 days in prod | Used for HMAC-SHA256 JWT signing. Rotation invalidates all active tokens |
| `sf-openai-api-key` | Azure OpenAI API key | On compromise or quarterly | Used only as fallback if managed identity auth fails; prefer managed identity |
| `sf-hubspot-private-app-token` | HubSpot Private App bearer token | Manual rotation in HubSpot portal | Required for outbound HubSpot API authentication |
| `sf-hubspot-webhook-secret` | HMAC-SHA256 shared secret for HubSpot webhook | On compromise | Used to validate `X-HubSpot-Signature-v3` on inbound webhooks |
| `sf-blob-storage-connstr` | Azure Blob Storage connection string | Fallback only — prefer managed identity | Exists in both dev and prod Key Vaults; primarily used for local-dev override / fallback scenarios |

> ✅ Confirmed: `sf-blob-storage-connstr` exists in the prod Key Vault as well as dev.

### 2.1 Secrets Seeding

Secrets must be seeded using the interactive script `infrastructure/scripts/seed-keyvault.sh` before first deployment. **Never** put secret values in source control, GitHub Actions variables, or plain-text environment variables.

---

## 3. Managed Identity Bindings

The App Service (`app-strategicglue-{env}`) uses a **system-assigned managed identity**. The following RBAC assignments must exist before the app starts.

| Azure Resource | Role | Scope | Purpose |
|----------------|------|-------|---------|
| `kv-strategicglue-{env}` (Key Vault) | `Key Vault Secrets User` | Resource | Get/List secrets; required for Key Vault references and `AddAzureKeyVault` |
| `ststrategicglue{env}` (Storage Account) | `Storage Blob Data Contributor` | Resource | Read/write audit artifact blobs; no SAS tokens needed |
| Azure OpenAI resource | `Cognitive Services OpenAI User` | Resource | Authenticate AI skill chain API calls without API key |
| `srch-strategicglue-{env}` (AI Search) | `Search Index Data Contributor` | Resource | Read/write the audit search index |

> **Note:** All RBAC assignments are provisioned by Bicep (`modules/keyvault.bicep`, `modules/storage.bicep`, `modules/search.bicep`). No manual portal assignments.

---

## 4. Environment-Specific Values

| Setting | Dev | Prod | Notes |
|---------|-----|------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Production` | Controls middleware, logging verbosity |
| App Service Plan SKU | B2 (Linux) | P2v3 (Linux) | P2v3 required for auto-scale + perf |
| App Service Name | `app-strategicglue-dev` | `app-strategicglue-prod` | |
| App Service Plan Name | `asp-strategicglue-dev` | `asp-strategicglue-prod` | |
| PostgreSQL Server Name | `psql-strategicglue-dev` | `psql-strategicglue-prod` | |
| PostgreSQL SKU | `Standard_B2ms` | `Standard_D4s_v3` | Prod: HA + zone-redundant |
| PostgreSQL HA | Disabled | `SameZone` | |
| Key Vault Name | `kv-strategicglue-dev` | `kv-strategicglue-prod` | |
| Key Vault SKU | Standard | Premium | Premium required for HSM-backed keys in prod |
| Key Vault Purge Protection | Disabled | Enabled | |
| Storage Account Name | `ststrategicgluedev` | `ststrategicglueprod` | Storage account names must be globally unique |
| Storage Redundancy | LRS | ZRS | ZRS for prod resilience |
| AI Search SKU | Basic | Standard S1 | S1 for semantic ranking + replicas |
| AI Search Replicas | 1 | 2 | |
| VNet Integration | No | Yes (`vnet-strategicglue-prod`) | |
| Private Endpoints | No | Yes (PostgreSQL + Key Vault) | |
| Application Insights | `appi-strategicglue-dev` | `appi-strategicglue-prod` | |
| Azure OpenAI Endpoint | `https://oai-strategicglue-dev.openai.azure.com/` | `https://oai-strategicglue-prod.openai.azure.com/` | |
| `Jwt:Issuer` | `https://app-strategicglue-dev.azurewebsites.net` | `https://app.strategicglue.com` | Custom domain in prod; SSL via App Service Managed Certificate |
| `Jwt:TokenExpiryMinutes` | `120` | `60` | Shorter expiry in prod |
| ARR Affinity | Enabled | Enabled | Required for SignalR (see §6) |
| Always On | Enabled | Enabled | Prevents cold starts |
| Min TLS Version | 1.2 | 1.3 | |
| HTTPS Only | Enabled | Enabled | |
| Auto-scale | No | Yes (1–4 instances) | CPU > 70% → scale out |

> ✅ Confirmed: Prod custom domain is `app.strategicglue.com`, and SSL is provided by an App Service Managed Certificate.

---

## 5. pgBouncer Connection Note

Azure PostgreSQL Flexible Server ships with a built-in pgBouncer instance. All application connections **must** use pgBouncer, not the direct PostgreSQL port.

| Property | Value |
|----------|-------|
| pgBouncer Port | **6432** (not 5432) |
| Direct PostgreSQL Port | 5432 (migrations/admin only, via `sf_admin` role) |
| pgBouncer Mode | **Transaction pooling** — connections are returned to the pool after each transaction |
| Max Client Connections | 500 (Azure Flexible Server default; tuned per SKU) |
| Pool Size per Database | 25 (default; increase if connection wait timeouts occur) |

**Connection string format (application — `sf_app` role via pgBouncer):**
```
Host=psql-strategicglue-dev.postgres.database.azure.com;Port=6432;Database=strategicglue;Username=sf_app;Password=<from KV>;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20;Connection Idle Lifetime=300
```

**Connection string format (migrations — `sf_admin` role, direct port 5432):**
```
Host=psql-strategicglue-dev.postgres.database.azure.com;Port=5432;Database=strategicglue;Username=sf_admin;Password=<from KV>;SSL Mode=Require;Trust Server Certificate=true
```

> ⚠️ **EF Core caveat:** Transaction pooling mode does NOT support `SET` commands, advisory locks, or prepared statements across connection boundaries. Disable `Npgsql` prepared statement caching when using pgBouncer: `No Reset On Close=true` and disable `Use Perf Counters` in Npgsql options.

---

## 6. ARR Affinity (SignalR Sticky Sessions)

Blazor Server uses SignalR circuits. Each browser tab maintains a persistent WebSocket/SSE connection to a specific server instance. Without sticky sessions, a reconnect may land on a different instance, losing circuit state.

**App Service setting:**

| Setting Name | Value | How to Set |
|-------------|-------|-----------|
| `ARR Affinity` | **Enabled** | Bicep: `stickySettings` + `WEBSITE_ARR_AFFINITY` = `1` |

**Bicep configuration:**
```bicep
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  properties: {
    clientAffinityEnabled: true  // sets ARR Affinity cookie
  }
}
```

> ⚠️ With `clientAffinityEnabled: true`, scale-out distributes *new* users across instances but existing circuits stay pinned. This is the correct behavior for Blazor Server.

---

## 7. Startup Validation

The application must validate all critical configuration before accepting traffic. Use `IOptions<T>` with data annotations + `ValidateDataAnnotations()` + `ValidateOnStart()` to fail fast.

### 7.1 Required Validation at Startup

| Config Section | Class | Key Fields Validated |
|----------------|-------|---------------------|
| `ConnectionStrings:DefaultConnection` | _(direct EF Core check)_ | Non-null, non-empty; test DB connection |
| `Azure` | `AzureOptions` | `KeyVaultUri` (valid URI), `StorageAccountName` (non-empty), `SearchEndpoint` (valid URI), `OpenAiEndpoint` (valid URI) |
| `Jwt` | `JwtOptions` | `SigningKey` (min 32 chars), `Issuer` (valid URI), `Audience` (non-empty), `TokenExpiryMinutes` (1–1440) |
| `HubSpot` | `HubSpotOptions` | `PrivateAppToken` (non-empty), `WebhookSecret` (non-empty), `PortalId` (non-empty) |

### 7.2 Registration Pattern

```csharp
// Program.cs
builder.Services
    .AddOptions<AzureOptions>()
    .Bind(builder.Configuration.GetSection("Azure"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<HubSpotOptions>()
    .Bind(builder.Configuration.GetSection("HubSpot"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

```csharp
// AzureOptions.cs
public class AzureOptions
{
    [Required, Url]
    public string KeyVaultUri { get; set; } = default!;

    [Required]
    public string StorageAccountName { get; set; } = default!;

    [Required, Url]
    public string SearchEndpoint { get; set; } = default!;

    [Required, Url]
    public string OpenAiEndpoint { get; set; } = default!;

    [Required]
    public string OpenAiDeploymentName { get; set; } = default!;
}
```

### 7.3 Database Connectivity Check

On startup (after DI container is built), verify Key Vault connectivity and database reachability before the application begins serving requests:

```csharp
// HealthChecks registration
builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy)
    .AddAzureKeyVault(
        new Uri(builder.Configuration["Azure:KeyVaultUri"]!),
        new DefaultAzureCredential(),
        opts => opts.AddSecret("sf-pgbouncer-connstr"),
        name: "keyvault",
        failureStatus: HealthStatus.Unhealthy);
```

Map at `/health` — this endpoint is polled by the deployment pipeline's health-check step before traffic is routed.

---

## ⚠️ Open Questions

1. **Azure OpenAI resource name:** The task specifies `sf-openai-api-key` as a KV secret but managed identity auth is preferred. Confirm whether the OpenAI resource will be in the same subscription (managed identity works) or a shared subscription (may need API key fallback).
