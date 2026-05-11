# EF Core Migrations

## Runtime vs design-time connections

- Runtime application traffic goes through **pgBouncer on port 6432**.
- EF Core migrations must use the **admin connection on port 5432**.
- Set `DESIGN_TIME_CONNECTION_STRING` to a real PostgreSQL admin connection before running any migration command.

## Apply migrations

```bash
DESIGN_TIME_CONNECTION_STRING=<admin-connection> dotnet ef database update --project src/SixToFix.Infrastructure --startup-project src/SixToFix.Web
```

## Create the initial migration

```bash
dotnet ef migrations add InitialCreate --project src/SixToFix.Infrastructure --startup-project src/SixToFix.Web
```

## Important notes

- CI does **not** scaffold migrations automatically because it does not have access to a real PostgreSQL admin endpoint.
- Use the App Service / Key Vault admin connection secret (`ConnectionStrings--AdminConnection`) or another secure admin credential source when exporting `DESIGN_TIME_CONNECTION_STRING`.
- After scaffolding a migration locally, commit the generated files under `src/SixToFix.Infrastructure/Migrations/`.
- The placeholder file in `src/SixToFix.Infrastructure/Migrations/` exists only to document the expected workflow until the first real migration is created.
