# Decision: Key Vault Secret Naming — `--` vs `__` Convention

**Author:** Tank  
**Date:** 2026-05-18  
**Status:** Proposed  

## Context

Azure Key Vault secret names do not allow `__` (double underscore). ASP.NET Core's configuration system uses `__` as the hierarchy separator for environment variables and App Settings. This creates a naming mismatch that must be handled consistently.

## Decision

| Layer | Separator | Example |
|-------|-----------|---------|
| KV secret name | `--` (double dash) | `Jwt--SigningKey`, `SeedAdmin--Email` |
| App Setting name (KV reference pointer) | `__` (double underscore) | `Jwt__SigningKey`, `SeedAdmin__Email` |
| App code / `IConfiguration` key | `:` | `Jwt:SigningKey`, `SeedAdmin:Email` |

The App Setting named `Jwt__SigningKey` contains the value `@Microsoft.KeyVault(VaultName=kv-sixtofix-prod;SecretName=Jwt--SigningKey)`. The `__` in the App Setting key is what .NET reads as the config hierarchy separator; the `--` in the KV secret name is the only naming Azure allows for nested keys.

## Rationale

- Azure Key Vault rejects `__` in secret names (invalid character for the API)
- ASP.NET Core reads `__` in env vars / App Settings as `:` (nested config binding)
- The `--` convention is the standard Azure workaround documented by Microsoft

## Implications for Bicep / GitHub Actions

- When adding new secrets, always create two things: (1) a KV secret with `--` separators, (2) an App Setting in `infra/modules/appservice.bicep` using `__` separators pointing to the `--` secret name
- If a KV reference App Setting is added manually (Portal or `az` CLI), it must also be added to `appservice.bicep` — otherwise the next `deploy-infra` run will DELETE it (Bicep is declarative and replaces the app settings block)
- `SeedAdmin__Email` and `SeedAdmin__Password` are currently missing from `appservice.bicep` — they were added manually and will be lost on next infra deploy

## Affected Secrets (as of 2026-05-18)

| KV Secret Name | App Setting Name | Status |
|----------------|-----------------|--------|
| `Jwt--SigningKey` | `Jwt__SigningKey` | ❌ KV secret MISSING — create it |
| `AzureOpenAI--ApiKey` | `AzureOpenAI__ApiKey` | ❌ KV secret MISSING |
| `HubSpot--PrivateAppToken` | `HubSpot__PrivateAppToken` | ❌ KV secret MISSING |
| `ConnectionStrings--DefaultConnection` | `ConnectionStrings__DefaultConnection` | ✅ OK |
| `ConnectionStrings--AdminConnection` | `ConnectionStrings__AdminConnection` | ✅ OK |
| `SeedAdmin--Email` | `SeedAdmin__Email` | ⚠️ KV secret OK; App Setting missing in Portal AND Bicep |
| `SeedAdmin--Password` | `SeedAdmin__Password` | ⚠️ KV secret OK; App Setting missing in Portal AND Bicep |
