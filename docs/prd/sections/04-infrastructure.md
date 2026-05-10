# Section 4 — Infrastructure & Deployment

> **Status:** Specification  
> **Author:** Smithers (Cloud DevOps)  
> **Date:** 2026-05-10  
> **Audience:** New DevOps engineer provisioning a blank Azure subscription

---

## 4.1 Hosting Architecture Overview

StrategicGlue Six-to-Fix is hosted entirely on Azure. The application tier is a single Azure App Service Web App running a unified ASP.NET Core (.NET 10) process that serves both the API and the Blazor Server frontend. There is no separate CDN layer for the initial launch; the App Service handles all HTTP/WebSocket traffic directly.

### Runtime Connection Flow

```
Browser (HTTPS)
      │
      ▼
Azure App Service (app-strategicglue-{env})
      │
      ├──► Azure PostgreSQL Flexible Server   (psql-strategicglue-{env}:6432 via pgBouncer)
      ├──► Azure Key Vault                    (kv-strategicglue-{env} — managed identity)
      ├──► Azure Blob Storage                 (ststrategicglue{env} — managed identity)
      └──► Azure AI Search                    (srch-strategicglue-{env} — managed identity)
```

### App Service Plan Tier Recommendations

| Environment | SKU | vCores | RAM | Rationale |
|-------------|-----|--------|-----|-----------|
| dev | B2 (Basic) | 2 | 3.5 GB | Cheapest tier with always-on and custom domains; no SLA |
| prod | P2v3 (Premium v3) | 2 | 8 GB | Zone-redundant capable, VNet integration, 99.95% SLA |

**Do not use the Free or Shared tiers** — they do not support always-on, which causes Blazor Server's SignalR connections to drop when the worker idles.

### Blazor Server and Sticky Sessions (ARR Affinity)

Blazor Server maintains a stateful WebSocket (SignalR) circuit between the browser and the server. Each circuit is pinned to a specific App Service instance. Without sticky sessions, the load balancer may route a reconnect request to a different instance, causing circuit disconnection and UI breakage.

**Required configuration:** Set `ARR_AFFINITY_ENABLED = true` in the App Service settings (this is the default for Azure App Service, but it must be explicitly verified and not disabled). For the prod P2v3 plan running multiple instances, this ensures each browser session returns to the same worker.

---

## 4.2 Authentication Architecture Summary

The system uses **Duende IdentityServer** as its OIDC/JWT authentication server, self-hosted within the same App Service (as a separate project path) or as a companion App Service (see `auth-spec.md` for the full recommendation and rationale).

### ASP.NET Core Middleware Integration

Authentication is configured in `Program.cs` using the standard ASP.NET Core middleware pipeline:

```
app.UseAuthentication()   // validates JWT Bearer tokens
app.UseAuthorization()    // enforces [Authorize] attributes and policies
```

Blazor Server does not pass tokens from the browser — the auth state is held server-side in the SignalR circuit. The `AuthenticationStateProvider` is cascaded to all Blazor components via `<CascadingAuthenticationState>`.

### Tenant Context Flow

Every authenticated request carries a JWT with a `tenant_id` claim. On each Blazor Server circuit:

1. The `AuthenticationStateProvider` reads the JWT claims from the server-side session.
2. A scoped `ITenantContext` service extracts `tenant_id` and `tenant_slug` from claims.
3. All database queries and Blob Storage paths are filtered/prefixed by `tenant_id`.
4. The `tenant_id` is never derived from the URL or a query parameter — it comes exclusively from the validated JWT.

### Token Storage in Blazor Server

Because Blazor Server renders on the server, JWT tokens are **never exposed to the browser**. Tokens are stored in the server-side SignalR session state. This eliminates XSS token theft risks that affect SPA architectures.

Full details in [`../auth-spec.md`](../auth-spec.md).

---

## 4.3 Azure Services Configuration

### 4.3.1 App Service — `app-strategicglue-{env}`

| Property | Dev | Prod |
|----------|-----|------|
| Runtime | .NET 10 | .NET 10 |
| Always On | Enabled | Enabled |
| ARR Affinity | Enabled | Enabled |
| HTTPS Only | Enforced | Enforced |
| HTTP/2 | Enabled | Enabled |
| Managed Identity | System-assigned | System-assigned |
| VNet Integration | No | Yes (subnet: snet-app-strategicglue-prod) |

**Key App Settings (non-secret):**

| Setting Key | Value / Notes |
|-------------|---------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `APPLICATIONINSIGHTS__CONNECTIONSTRING` | Direct value (not secret) |
| `AZURE__STORAGEACCOUNTNAME` | `ststrategicgluedev` or `ststrategicglueprod` |
| `AZURE__SEARCHENDPOINT` | `https://srch-strategicglue-{env}.search.windows.net` |

**Secret-backed App Settings (Key Vault references):**

All secrets reference Key Vault using the format `@Microsoft.KeyVault(SecretUri=https://kv-strategicglue-{env}.vault.azure.net/secrets/{SECRET-NAME}/)`. The App Service managed identity must have the **Key Vault Secrets User** role.

---

### 4.3.2 App Service Plan — `asp-strategicglue-{env}`

| Property | Dev | Prod |
|----------|-----|------|
| SKU | B2 | P2v3 |
| OS | Linux | Linux |
| Zone Redundancy | No | Yes |
| Instance Count | 1 | 2 (min) |
| Scale Out Trigger | N/A | CPU > 70% for 5 min |

Use Linux hosting. The .NET 10 runtime is fully supported on Linux App Service and costs less than Windows.

---

### 4.3.3 PostgreSQL Flexible Server — `psql-strategicglue-{env}`

| Property | Dev | Prod |
|----------|-----|------|
| Version | 16 | 16 |
| SKU | Standard_B2ms | Standard_D4s_v3 |
| Storage | 32 GB | 128 GB |
| pgBouncer | Enabled (port 6432) | Enabled (port 6432) |
| High Availability | Disabled | Zone-redundant standby |
| Backup Retention | 7 days | 35 days |
| Geo-redundant backup | No | Yes |
| Public access | Allowed (dev only) | Denied — private endpoint only |
| SSL enforcement | Required | Required |

**Connection:** The App Service connects via the connection string stored in Key Vault under `DATABASE-URL`. The connection string targets pgBouncer port 6432 using the `sf_app` role (DML only). The `sf_admin` role is reserved for migrations and is never used by the application at runtime.

**Networking:** In prod, a private endpoint (`pep-psql-strategicglue-prod`) binds the PostgreSQL server to the VNet. The App Service accesses it via VNet Integration. Public access is completely disabled in prod.

---

### 4.3.4 Key Vault — `kv-strategicglue-{env}`

| Property | Dev | Prod |
|----------|-----|------|
| SKU | Standard | Premium |
| Soft delete | Enabled (90 days) | Enabled (90 days) |
| Purge protection | Disabled | Enabled |
| Access model | RBAC | RBAC |
| Public network access | Allowed | Disabled (private endpoint) |

The App Service managed identity is assigned the **Key Vault Secrets User** role scoped to this vault. No person should have standing Key Vault access in prod — use Privileged Identity Management (PIM) or break-glass procedures for one-off access.

**Private endpoint (prod):** `pep-kv-strategicglue-prod` — bound to the VNet.

---

### 4.3.5 Azure Blob Storage — `ststrategicglue{env}`

| Property | Dev | Prod |
|----------|-----|------|
| Replication | LRS | ZRS |
| Access tier | Hot | Hot |
| Public blob access | Disabled | Disabled |
| Secure transfer | Required | Required |
| VNet restriction | No | Yes (service endpoint from App Service subnet) |

**Containers:**

| Container | Purpose |
|-----------|---------|
| `intake-documents` | Raw uploaded client documents |
| `audit-exports` | Generated audit PDFs and reports |
| `skill-artifacts` | Intermediate skill run outputs |

The App Service managed identity is assigned **Storage Blob Data Contributor** on this account.

---

### 4.3.6 Azure AI Search — `srch-strategicglue-{env}`

| Property | Dev | Prod |
|----------|-----|------|
| SKU | Basic | Standard S1 |
| Replicas | 1 | 2 |
| Partitions | 1 | 1 |
| Semantic ranking | Disabled | Enabled |

**Connection:** The App Service connects using the search endpoint URL (app setting) and API key (Key Vault secret). Managed identity is preferred for the indexer connection; the API key is a fallback for query operations where managed identity role propagation may lag.

**Index naming convention:** `idx-{tenant-slug}-{index-type}` — e.g., `idx-acme-corp-audits`.

---

### 4.3.7 Application Insights — `appi-strategicglue-{env}`

| Property | Dev | Prod |
|----------|-----|------|
| Workspace | log-strategicglue-{env} | log-strategicglue-{env} |
| Sampling | 100% | Adaptive (target 5 req/s) |
| Retention | 90 days | 90 days |
| Live Metrics | Enabled | Enabled |

The connection string is set as a direct App Service app setting (`APPLICATIONINSIGHTS__CONNECTIONSTRING`). The .NET SDK auto-discovers this and initializes telemetry without additional code. Blazor Server lifecycle events and SignalR messages are captured automatically.

---

### 4.3.8 Log Analytics Workspace — `log-strategicglue-{env}`

| Property | Dev | Prod |
|----------|-----|------|
| Retention | 30 days | 90 days |
| Daily cap | 1 GB | 5 GB |

All Azure diagnostic logs (App Service, PostgreSQL, Key Vault audit, Azure AI Search) are routed to this workspace in addition to Application Insights telemetry. This provides a single query surface for cross-service correlation.

---

### 4.3.9 Virtual Network — `vnet-strategicglue-prod` (prod only)

| Property | Value |
|----------|-------|
| Address space | 10.0.0.0/16 |
| App Service subnet | snet-app-strategicglue-prod (10.0.1.0/24) |
| Database subnet | snet-db-strategicglue-prod (10.0.2.0/24) |
| Services subnet | snet-svc-strategicglue-prod (10.0.3.0/24) |

VNet Integration is enabled on the prod App Service plan, routing all outbound traffic through the App subnet. PostgreSQL and Key Vault are reachable only via private endpoints on their respective subnets.

**Dev environment:** No VNet. Services use firewall rules to allow the App Service outbound IP addresses.

---

### 4.3.10 Network Security Group — `nsg-strategicglue-prod` (prod only)

Applied to the App Service integration subnet. Key rules:

| Priority | Direction | Name | Protocol | Destination | Action |
|----------|-----------|------|----------|-------------|--------|
| 100 | Outbound | Allow-PostgreSQL | TCP | snet-db (port 6432) | Allow |
| 110 | Outbound | Allow-KeyVault | TCP | Private endpoint (443) | Allow |
| 120 | Outbound | Allow-Blob | TCP | Storage service endpoint | Allow |
| 130 | Outbound | Allow-Search | TCP | Private endpoint (443) | Allow |
| 4096 | Outbound | Deny-All | Any | Any | Deny |

---

## 4.4 Managed Identity & RBAC

The App Service has a **system-assigned managed identity** enabled. No credentials are stored in code or environment variables for Azure service connections.

### Required RBAC Assignments

| Identity | Role | Scope |
|----------|------|-------|
| `app-strategicglue-dev` (managed identity) | Storage Blob Data Contributor | `ststrategicgluedev` |
| `app-strategicglue-dev` (managed identity) | Key Vault Secrets User | `kv-strategicglue-dev` |
| `app-strategicglue-dev` (managed identity) | Search Index Data Contributor | `srch-strategicglue-dev` |
| `app-strategicglue-prod` (managed identity) | Storage Blob Data Contributor | `ststrategicglueprod` |
| `app-strategicglue-prod` (managed identity) | Key Vault Secrets User | `kv-strategicglue-prod` |
| `app-strategicglue-prod` (managed identity) | Search Index Data Contributor | `srch-strategicglue-prod` |
| GitHub Actions OIDC identity | Contributor | `rg-StrategicGlue-CommandCenter` |
| GitHub Actions OIDC identity | Key Vault Secrets Officer | Both vaults (for secret rotation in CI) |

**PostgreSQL:** Managed identity authentication to PostgreSQL Flexible Server requires the `azure_ad_admin` role and Entra-based login. For simplicity at launch, the `sf_app` database role credentials are stored as a secret in Key Vault and loaded via `DATABASE-URL`. Managed identity for PostgreSQL should be evaluated for a future hardening pass.

### Why Managed Identity Over Connection Strings

- No secret rotation burden — Azure manages the credential lifecycle automatically.
- Eliminates the risk of secrets leaking through logs, config dumps, or container image layers.
- Access is auditable via Azure Monitor (every managed identity call is logged).
- Revocation is instant — disable the identity rather than hunting down all secret copies.

---

## 4.5 Secret Management

### Key Vault Secret Inventory

| Secret Name | Contents | Rotation Cadence |
|-------------|----------|-----------------|
| `DATABASE-URL` | PostgreSQL connection string with `sf_app` role (includes `?sslmode=require&port=6432`) | 90 days or on compromise |
| `FOUNDRY-API-KEY` | Azure OpenAI API key or Foundry service key | 90 days |
| `HUBSPOT-PRIVATE-APP-TOKEN` | HubSpot private app token for CRM sync | On revocation |
| `JWT-SIGNING-KEY` | IdentityServer signing key (RSA or ECDSA) | Annual, with key rollover overlap |
| `AZURE-SEARCH-API-KEY` | Azure AI Search query/admin API key | 180 days |

### Key Vault Reference Pattern

App Service app settings reference Key Vault using the notation:

```
@Microsoft.KeyVault(SecretUri=https://kv-strategicglue-dev.vault.azure.net/secrets/DATABASE-URL/)
```

The trailing `/` ensures Azure uses the latest version automatically. Pinning to a specific version requires updating the app setting on rotation.

### Secret Rotation Guidance

1. Create the new secret version in Key Vault (do not delete the old version).
2. Wait 5 minutes for the App Service to pick up the new version via the Key Vault reference (or restart the app).
3. Verify the application connects successfully using the new secret.
4. Mark the old version as **disabled** in Key Vault (do not delete — retention for audit trail).

For database password rotation: the `sf_app` PostgreSQL role password must be changed first in the database, then the new `DATABASE-URL` secret created in Key Vault before the old one is disabled.

---

## 4.6 CI/CD Pipeline

### GitHub Actions Workflow Summary

| Workflow File | Trigger | Purpose |
|---------------|---------|---------|
| `deploy-infra.yml` | Push to `main` touching `infrastructure/**` | Bicep deployment — provision/update Azure resources |
| `deploy-app.yml` | Push to `main` touching `api/**` or `web/**` | Build and zip-deploy the .NET app to App Service |
| `validate-skills.yml` | PR touching `skills/**` | Validate skill YAML frontmatter against schema |
| `test.yml` | PR (any) | Run .NET xUnit tests + Playwright E2E tests |

### OIDC Federated Identity (No Long-Lived Secrets)

GitHub Actions uses OIDC federation to authenticate to Azure. No `AZURE_CLIENT_SECRET` is stored in GitHub. The federated credential subjects must cover:

- `repo:cdaly33/six-to-fix-5:environment:dev`
- `repo:cdaly33/six-to-fix-5:environment:prod`
- `repo:cdaly33/six-to-fix-5:ref:refs/heads/main`

Required GitHub repository variables:

| Variable | Description |
|----------|-------------|
| `AZURE_CLIENT_ID` | Client ID of the user-assigned managed identity for GitHub Actions |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Target Azure subscription ID |

### Environment-Gated Deployments

- **dev:** Auto-deploys on every push to `main`. No approval required.
- **prod:** Requires a GitHub Environment approval gate. At least one designated reviewer must approve before the deployment job runs. Deploy is triggered by a release tag matching `v*.*.*`.

### Deployment Strategy

App Service deployment uses **zip deploy** via the `azure/webapps-deploy@v3` action. The .NET app is published with `dotnet publish -c Release` and the output folder is zipped. Slot swapping is not configured at launch; a future prod hardening pass should introduce a staging slot with swap-based deployment for zero-downtime releases.

---

## 4.7 Environments & Branching Strategy

### Environment Mapping

| Environment | Azure Resources | Deploy Trigger | Approval |
|-------------|-----------------|----------------|---------|
| dev | `-dev` suffix resources | Push to `main` | None |
| prod | `-prod` suffix resources | Release tag `v*.*.*` | Manual (GitHub Env gate) |

### Branch → Environment Conventions

```
main          → auto-deploy to dev
feature/*     → PR only (tests + skill validation)
v1.0.0 tag    → triggers prod deployment (after approval)
```

### Environment-Specific Configuration

Environment-specific configuration is managed through App Service app settings, not `appsettings.{env}.json` files committed to the repository. The `ASPNETCORE_ENVIRONMENT` app setting drives the ASP.NET Core environment selection, which controls logging verbosity, developer error pages, and environment-specific service registrations.

Do not commit environment-specific secrets or connection strings to the repository. Everything environment-specific either comes from Key Vault (secrets) or App Service app settings (non-secret config).

---

## 4.8 Monitoring & Alerting

### Application Insights Capabilities

Application Insights automatically captures:
- HTTP request/response traces with duration and status codes
- Dependency calls (PostgreSQL queries via ADO.NET, HTTP calls to external APIs)
- Unhandled exceptions with full stack traces
- Blazor Server SignalR circuit events (connection, disconnection)
- Custom events and metrics via `TelemetryClient`

Structured log events from the .NET `ILogger` pipeline are forwarded to Application Insights and correlated by `operation_id`.

### Alert Rules (to configure in Azure Monitor)

| Alert | Condition | Severity | Action |
|-------|-----------|----------|--------|
| High API error rate | `requests/failed` > 1% over 5 min | Sev 2 | Email + Teams webhook |
| PostgreSQL connection failures | `dependencies/failed` where `type = SQL` > 5/min | Sev 1 | Page on-call |
| Key Vault access denied | KV audit log `ResultType = Forbidden` > 0 in 5 min | Sev 1 | Page on-call |
| Slow responses | `requests/duration` p95 > 5000ms over 10 min | Sev 3 | Email |
| App Service CPU | CPU > 85% for 10 min | Sev 2 | Email |

### Cost Governance

Configure an Azure Budget alert at the subscription level:
- **Threshold:** 80% of the monthly estimate
- **Action:** Email to Chris and the engineering lead
- Second alert at 100% of budget for immediate escalation.

---

## 4.9 Database Operations

### PostgreSQL Flexible Server Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| PostgreSQL version | 16 | Minimum; do not use 14 or 15 |
| pgBouncer | Enabled | App uses port 6432; admin uses 5432 |
| pgBouncer pool mode | Transaction | Appropriate for .NET connection pooling |
| SSL mode | `require` | Mandatory for all connections |
| `max_connections` | 100 (dev) / 400 (prod) | Set server parameter |
| `shared_buffers` | Default | Managed service handles this |

### Database Roles

| Role | Privileges | Used By |
|------|-----------|---------|
| `sf_admin` | CREATEDB, DDL, DML on all tables | CI migrations only |
| `sf_app` | INSERT, UPDATE, SELECT, DELETE on app tables (no DDL) | Application runtime |

Create roles in this order during environment setup:
```sql
CREATE ROLE sf_admin WITH LOGIN PASSWORD '…' CREATEDB;
CREATE ROLE sf_app WITH LOGIN PASSWORD '…';
GRANT CONNECT ON DATABASE strategicglue TO sf_app;
GRANT USAGE ON SCHEMA public TO sf_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sf_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sf_app;
```

### Migration Strategy

Migrations are numbered SQL files in `db/migrations/` using the naming convention `{NNNN}_{description}.sql`. They are run in ascending numeric order by the CI pipeline using the `sf_admin` role **before** the new app version is deployed.

Migration run order in CI (`deploy-app.yml`):
1. Run pending migrations via `psql` with `sf_admin` credentials (loaded from Key Vault).
2. Verify migration table row count matches expected.
3. Deploy new app version.

**Rollback:** There are no automatic rollback scripts. Each migration ships with a corresponding `down` script in `db/migrations/rollback/` that a human operator runs manually if a rollback is required. See `infra-spec.md §7` for rollback procedure.

### Backup Configuration

| Property | Dev | Prod |
|----------|-----|------|
| Automated backups | Azure-managed | Azure-managed |
| Retention | 7 days | 35 days |
| Geo-redundant backup | No | Yes |
| Point-in-time restore | Yes (within retention window) | Yes (within retention window) |

### High Availability

- **Dev:** Single server, no standby. Downtime acceptable during Azure maintenance windows.
- **Prod:** Zone-redundant standby enabled. Failover is automatic (Azure-managed), typically within 60–120 seconds. The application must handle connection retry with exponential backoff (configured via Polly in the .NET client).
