# Neo Decision Note — Admin Bootstrap Seeder

## Context

Production had no users in ASP.NET Core Identity, so Chris needed a safe bootstrap path to create the first SuperAdmin without direct database writes.

## Decision

Add an environment-gated startup hosted service that uses `UserManager<ApplicationUser>` and `RoleManager<IdentityRole<Guid>>` to create exactly one bootstrap SuperAdmin when no SuperAdmin user exists. The service is registered only when `SeedAdmin:Enabled=true`, reads `SeedAdmin:Email` and `SeedAdmin:Password` from configuration, confirms email immediately, and assigns the canonical `SuperAdmin` role.

## Safety properties

- Idempotent: any existing SuperAdmin user makes startup a no-op.
- Non-fatal: missing config or Identity failures are logged and do not crash the host.
- No raw Identity table writes: all user/role changes flow through ASP.NET Core Identity managers.
- Prod wiring uses Key Vault flat secrets `SeedAdmin--Email` and `SeedAdmin--Password`, plus App Service env var `SeedAdmin__Enabled`.
