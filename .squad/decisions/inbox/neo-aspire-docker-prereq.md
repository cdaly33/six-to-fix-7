# Neo Decision — Aspire AppHost prerequisites

- **Date:** 2026-05-16
- **Status:** Proposed
- **Owner:** Neo

## Context

Running `dotnet run --project src\SixToFix.AppHost` failed with Aspire dashboard configuration errors for missing `ASPNETCORE_URLS` and OTLP endpoint environment variables. The AppHost project was also missing its checked-in `Properties\launchSettings.json`, and this machine does not have a working Docker CLI available.

## Decision

Check in `src\SixToFix.AppHost\Properties\launchSettings.json` so the default AppHost launch profile always provides the Aspire dashboard URLs required at startup. Document Docker Desktop as an explicit prerequisite for local AppHost runs because the PostgreSQL dependency is containerized.

## Consequences

Developers can launch the AppHost with the standard `dotnet run --project src\SixToFix.AppHost` command without hitting the dashboard environment-variable failure. They must still have Docker Desktop installed and running for the PostgreSQL container to start successfully.
