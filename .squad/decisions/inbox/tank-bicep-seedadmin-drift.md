# Tank — Bicep Drift Prevention: SeedAdmin App Settings

**Author:** Tank (DevOps & Infrastructure)  
**Date:** 2026-05-18  
**Status:** Accepted (PR #39 opened on dev/phase-bicep-seedadmin-settings)

## Context

Chris manually wired three App Settings on `app-sixtofix-prod` to wire the bootstrap seeder:

- `SeedAdmin__Enabled = "true"`
- `SeedAdmin__Email = @Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=SeedAdmin--Email)`
- `SeedAdmin__Password = @Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=SeedAdmin--Password)`

`infra/modules/appservice.bicep` was missing the two KV references. The next `deploy-infra` run would have overwritten the full `appsettings` block via ARM replace semantics, wiping both references and re-breaking the seeder (no SuperAdmin seeded → no way to log in to prod).

## Decision

Added `SeedAdmin__Email` and `SeedAdmin__Password` as Key Vault references to `appservice.bicep`, following the identical pattern used for `ConnectionStrings--DefaultConnection`, `Jwt--SigningKey`, etc. `SeedAdmin__Enabled` was already present as `isProd ? 'true' : 'false'` and was left unchanged.

`SeedAdmin__Enabled` was kept as a conditional expression (not hardcoded `'true'`) so the seeder only activates in prod — this is intentional and correct.

## Standing Rule (New)

**When any manual Azure change is made (Portal, az CLI, etc.) that is not yet reflected in Bicep, Tank must proactively open a Bicep PR to codify it — do NOT wait for Chris to ask.** Manual changes that are not in Bicep are a time-bomb: the next infrastructure deploy will silently wipe them.

This rule applies to all resource types: App Service settings, Key Vault access policies, role assignments, CORS rules, etc.

## Validation

`az bicep build --file infra/main.bicep` — ✅ exit 0, no errors.

## PR

[#39 — infra(bicep): add SeedAdmin app settings to prevent drift](https://github.com/cdaly33/six-to-fix-7/pull/39)
