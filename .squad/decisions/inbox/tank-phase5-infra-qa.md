# Tank Phase 5 — Infra / QA Decisions

## Summary

- Canonical infrastructure entrypoint is now `infra/main.bicep` with seven focused modules under `infra/modules/` and environment params under `infra/params/`.
- ARR affinity remains enabled for the App Service because Blazor Server SignalR requires sticky sessions.
- GitHub Actions workflow inventory is now `test.yml`, `validate-skills.yml`, `deploy-infra.yml`, and `deploy-app.yml`; the older CI/deploy workflow files were removed to prevent duplicate pipelines.
- API contract tests use `WebApplicationFactory<Program>` with mocked application services instead of Docker-backed infrastructure so they stay PR-safe.
- Infrastructure integration tests are explicitly tagged with `Category=Integration`; Playwright scaffolding is tagged with `Category=E2E`.
- `AzureSearchClient` now composes tenant filters with optional additional filters and bootstraps the three required Azure AI Search indexes on first use.

## Follow-up considerations

- `postgresAdminPassword` in `infra/main.bicep` still uses a bootstrap default for template usability; production deployments should override it immediately with a secure value.
- Local test execution on this Windows host is limited by Application Control policy blocking xUnit test assemblies after build, so CI remains the authoritative runtime verification path for new tests.
