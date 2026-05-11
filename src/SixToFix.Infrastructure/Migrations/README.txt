No EF Core migration has been scaffolded yet.

When a real PostgreSQL admin connection is available, create the first migration with:
  dotnet ef migrations add InitialCreate --project src/SixToFix.Infrastructure --startup-project src/SixToFix.Web

Apply migrations with:
  DESIGN_TIME_CONNECTION_STRING=<admin-connection> dotnet ef database update --project src/SixToFix.Infrastructure --startup-project src/SixToFix.Web

Use port 5432 for migrations and port 6432 only for runtime traffic through pgBouncer.
