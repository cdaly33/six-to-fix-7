# Tank — DevOps/Infrastructure/Security Decisions

## Dispatch #8: Infrastructure Gap Analysis (Advisory)

**Date:** 2026-05-19  
**Status:** Findings delivered (advisory, no PR)

### Gap Analysis Scope
Analyzed infrastructure provisioning, Bicep drift, Kubernetes/container deployment patterns, and CDN/scaling needs.

---

## Background — CSP + Pipeline Checkup (PR #42)

**Date:** 2026-05-18  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/42  
**Status:** Completed

### CSP Policy Added
**New file:** `src/SixToFix.Web/Middleware/SecurityHeadersMiddleware.cs`  
**Registered in:** `Program.cs` as middleware step 2 (after CorrelationId, before HttpsRedirection)

```
default-src 'self';
script-src 'self' 'unsafe-inline';
style-src 'self' https://fonts.googleapis.com 'unsafe-inline';
font-src 'self' https://fonts.gstatic.com data:;
connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com;
img-src 'self' data: blob:;
frame-ancestors 'none';
base-uri 'self';
form-action 'self'
```

#### Rationale for Directives
- **`'unsafe-inline'` on `script-src`:** Blazor Server emits inline script for SignalR circuit bootstrapper; removing would break app
- **`'unsafe-inline'` on `style-src`:** Blazor's scoped-CSS isolation injects `<style>` blocks at render time
- **`wss:` in `connect-src`:** SignalR uses WebSocket for Blazor Server circuit
- **`data:` in `font-src`:** Covers base64-encoded font faces potentially inlined by CSS build pipeline

#### Additional Security Headers
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`

### CI Workflow Audit

| Workflow | SixToFix.slnx | Test filter | .NET 10 | Issues |
|----------|---------------|------------|---------|---------|
| test.yml | ✅ | ✅ `Category!=Integration&Category!=E2E` | ✅ | None |
| deploy-app.yml | ✅ | n/a | ✅ | None |
| deploy-infra.yml | n/a | n/a | n/a | **Fixed: stale RG name** |

**Fix:** `rg-StrategicGlue-CommandCenter` → `rg-sixtofix-prod` in deploy-infra.yml

### Bicep Drift Status

| Resource | Expected | Bicep | Status |
|----------|----------|-------|--------|
| Postgres SKU (prod) | Burstable B2ms | `Standard_B2ms / Burstable` | ✅ In sync |
| Postgres HA | Disabled | `mode: 'Disabled'` | ✅ In sync |
| App Service affinity | Enabled | `clientAffinityEnabled: true` | ✅ In sync |

No unreflected drift found.

---

## Background — Bicep Drift Prevention: SeedAdmin App Settings (PR #39)

**Date:** 2026-05-18  
**Branch:** `dev/phase-bicep-seedadmin-settings`  
**PR:** https://github.com/cdaly33/six-to-fix-7/pull/39  
**Status:** Accepted ✅

### Context
Chris manually wired three App Settings on `app-sixtofix-prod` to support bootstrap seeder:
- `SeedAdmin__Enabled = "true"`
- `SeedAdmin__Email = @Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=SeedAdmin--Email)`
- `SeedAdmin__Password = @Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=SeedAdmin--Password)`

`infra/modules/appservice.bicep` was missing the KV references. Next `deploy-infra` run would have overwritten via ARM replace semantics, wiping both references and re-breaking seeder (no way to log in).

### Decision
Added `SeedAdmin__Email` and `SeedAdmin__Password` as Key Vault references to `appservice.bicep`, following identical pattern used for `ConnectionStrings--DefaultConnection`, `Jwt--SigningKey`, etc. `SeedAdmin__Enabled` was already present as conditional expression (`isProd ? 'true' : 'false'`) and left unchanged.

### Standing Rule (New)

**When any manual Azure change is made (Portal, az CLI, etc.) that is not yet reflected in Bicep, Tank must proactively open a Bicep PR to codify it — do NOT wait for Chris to ask.** Manual changes not in Bicep are a time-bomb: next infra deploy will silently wipe them.

This rule applies to all resource types: App Service settings, Key Vault access policies, role assignments, CORS rules, etc.

### Validation
`az bicep build --file infra/main.bicep` — ✅ exit 0, no errors

---
