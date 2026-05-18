# Decision: PostgreSQL 16 pgBouncer Configuration Fix

**Author:** Tank  
**Date:** 2026-05-17  
**Status:** Proposed — for team review / ADR promotion

---

## Context

Azure deployment failed with:

- `ParameterNotExists`
- `Server parameter 'connection_pooling' is invalid or isn't supported in PostgreSQL version '16'`

The active PostgreSQL module is `infra/modules/postgres.bicep`, referenced by `infra/main.bicep`. That module created a `Microsoft.DBforPostgreSQL/flexibleServers/configurations` resource named `connection_pooling` with value `PgBouncer`.

This repo’s runtime Key Vault bootstrap secret builds `ConnectionStrings--DefaultConnection` with `Port=6432`, which means the deployment expects Azure Flexible Server’s built-in pgBouncer to be enabled for app traffic.

---

## Decisions

### 1. Do not use `connection_pooling` on PostgreSQL Flexible Server 16

`connection_pooling` is not a valid PostgreSQL Flexible Server v16 server parameter and must not be deployed from Bicep.

### 2. Enable pgBouncer with `pgbouncer.enabled = 'on'`

For this project, the correct `Microsoft.DBforPostgreSQL/flexibleServers/configurations` resource name is:

- `pgbouncer.enabled`

And the value must be:

- `'on'`

This matches the application’s expected runtime topology because the app connection string targets port `6432`.

### 3. Keep admin connections on port `5432`

Only the runtime app connection should use pgBouncer (`6432`). Administrative and migration connections should continue to use direct PostgreSQL on port `5432`.

---

## Files Changed

| File | Change |
|------|--------|
| `infra/modules/postgres.bicep` | Replaced invalid configuration name `connection_pooling` with `pgbouncer.enabled` and changed value from `PgBouncer` to `'on'` |

---

## Validation

Validated with:

- `az bicep build --file infra/main.bicep`

---

## Recommendation

Promote to a new ADR covering Azure PostgreSQL Flexible Server pgBouncer enablement rules, including the requirement that any runtime connection string using port `6432` must keep `pgbouncer.enabled` enabled in infrastructure code.
