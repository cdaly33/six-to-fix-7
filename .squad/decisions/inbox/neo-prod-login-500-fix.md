# Neo — Prod Login 500 Fix

**Author:** Neo (Backend/Auth)  
**Date:** 2026-05-18  
**Status:** Resolved (login confirmed working)

## Root Cause — Three-Layer Cascade

### Layer 1: `sf_app` PostgreSQL role did not exist
The runtime connection string (`ConnectionStrings__DefaultConnection`) uses login role `sf_app` on pgBouncer port 6432. When the Azure PostgreSQL Flexible Server was provisioned (PR #38, burstable tier change), the `sf_app` role was never created. pgBouncer returned `FATAL: no such user (SqlState 08P01)` on every database call. This caused an unhandled `NpgsqlException` in `ExceptionHandlerMiddlewareImpl` → HTTP 500 on all endpoints hitting the DB, including login.

**Fix (manual, non-driftable):** `CREATE ROLE sf_app WITH LOGIN PASSWORD '<KV DefaultConnection password>'` executed via psql as sfadmin.

### Layer 2: EF Core migrations had never run against prod
The database was empty — no `AspNetUsers`, `AspNetRoles`, or any application tables. Migration `20260516042353_InitialCreate` had never been applied to the Azure Flexible Server (neither manually nor on startup).

**Fix (manual + codified):**  
1. `dotnet ef database update` with `DESIGN_TIME_CONNECTION_STRING=<AdminConnection>` — applied InitialCreate to prod.  
2. Granted DML permissions: `GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sf_app;` plus `REVOKE UPDATE, DELETE ON TABLE category_result_versions FROM sf_app;` (append-only enforcement per schema spec).

### Layer 3: `SeedAdmin--Password` did not meet Identity password policy
The original KV secret `SeedAdmin--Password = GYyE3jnmvGJuMyjtNQAk` lacked a digit and a non-alphanumeric character. `UserManager.CreateAsync` returned `PasswordRequiresNonAlphanumeric` and `PasswordRequiresDigit` identity errors. The seeder correctly caught and logged the error without crashing the host, but the bootstrap user was never created. The SuperAdmin role was created (before the user creation step) — the seeder's partial state was handled correctly by idempotency on the next run.

**Fix (KV update):** Updated `SeedAdmin--Password` in `kv-sixtofix-prod` to `GYyE3jnmvGJuMyjtNQAk1!`. Restarted app; seeder ran and created `chris@christopherdaly.com` with role `SuperAdmin`.

## Code Fixes (PR #41 — `fix/prod-login-500`)

### 1. Startup migration runner in `Program.cs`
Added a startup block that creates a temporary `SixToFixDbContext` using `ConnectionStrings__AdminConnection` (sfadmin, port 5432, DDL perms) and calls `Database.MigrateAsync()`. This runs before the middleware pipeline and before `BackgroundService` tasks.

**Effect:** Every app deploy automatically applies pending migrations. No more `dotnet ef database update` by hand. Failures are logged as errors (non-fatal — app continues) so a migration bug doesn't hard-crash the host.

**Why AdminConnection and not DefaultConnection:** `sf_app` has only DML permissions. DDL (CREATE TABLE, ALTER TABLE, etc.) requires the `sfadmin` role. Using DefaultConnection for migrations would fail with `permission denied` as soon as there is a schema change.

### 2. Migration `20260519033146_GrantAppRolePermissions`
Codifies the GRANT/REVOKE SQL in source control so that:
- A new environment deployment (dev, staging) automatically grants the right permissions after `dotnet ef database update` (or after the startup runner applies it).
- `category_result_versions` append-only enforcement is enforced at DB layer for `sf_app`.
- Includes a fail-fast DO block: if `sf_app` doesn't exist when the migration runs, it raises an exception with a clear message rather than silently granting nothing.

## Standing Rules (New)

1. **When provisioning a new PostgreSQL Flexible Server for any environment:** create the `sf_app` and `sf_admin` login roles (if separate from sfadmin) before the first app deploy. The startup migration runner will fail-fast if the role is missing.

2. **`SeedAdmin--Password` in KV must meet Identity policy:** ≥12 chars, at least one uppercase, one digit, one non-alphanumeric character. Validate before storing in KV.

3. **Never use `DefaultConnection` for schema changes.** DefaultConnection = sf_app = DML-only on pgBouncer port 6432. Migrations always use AdminConnection = sfadmin on port 5432.

4. **Drift prevention:** The KV secret update (`SeedAdmin--Password`) is a runtime secret, not an infra resource — no Bicep PR needed for this change. Tank's Bicep already has the KV reference correctly wired (PR #39). No Bicep drift introduced by this incident.

## Verification

```
POST https://app-sixtofix-prod.azurewebsites.net/api/auth/login
{"email":"chris@christopherdaly.com","password":"GYyE3jnmvGJuMyjtNQAk1!"}

HTTP 200 OK
{
  "email": "chris@christopherdaly.com",
  "userId": "55d11c4a-d353-4683-a8b4-2ae1e23ca983",
  "roles": ["SuperAdmin"],
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

Bearer token accepted on protected endpoints (404 on unimplemented route, not 401/403).
