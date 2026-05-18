# Decision: pgbouncer.enabled Correct Value Is 'True' Not 'on'

**Date:** 2026-05-17  
**Author:** Tank (DevOps/Infrastructure)  
**Branch:** fix/bicep-deployment-errors  
**Commit:** 7cf1e7c  

## Context

The previous fix to `infra/modules/postgres.bicep` replaced the invalid `connection_pooling` parameter with `pgbouncer.enabled`. However the value was set to `'on'`, which Azure also rejects.

**Azure error:**
```
"code":"ServerParameterToCMSUnAllowedParameterValue",
"message":"Value 'on' is invalid for server parameter 'pgbouncer.enabled'. Allowed values are 'True,False'."
```

## Decision

Set `pgbouncer.enabled` value to `'True'` (capital T, string literal) in `infra/modules/postgres.bicep`.

## Change

**File:** `infra/modules/postgres.bicep`  
**Resource:** `pgBouncerConfig` (`Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01`)

```bicep
// Before
value: 'on'

// After
value: 'True'
```

## Rationale

Azure PostgreSQL Flexible Server strictly validates configuration parameter values against an allowlist. For `pgbouncer.enabled`, the only accepted values are `'True'` and `'False'`. Neither `'on'` nor `'true'` (lowercase) are accepted.

## Validation

`az bicep build --file infra/main.bicep` compiled with zero errors (only a version upgrade warning).

## Rule Going Forward

When setting boolean-style PostgreSQL Flexible Server configuration parameters via Bicep, always use `'True'` / `'False'` (capital first letter, string). Do not use `'on'`/`'off'` or `true`/`false` (boolean).
