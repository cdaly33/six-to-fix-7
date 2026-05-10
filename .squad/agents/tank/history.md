# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform for marketing maturity audits. CI/CD gates and test coverage are product requirements.
- **Stack:** .NET 10 LTS, Azure Bicep, GitHub Actions (OIDC for Bicep deploy, zip deploy for app), Azure App Service (B2 dev / P2v3 prod), Azure PostgreSQL Flexible Server v16, Azure Key Vault (managed identity), Azure Blob Storage, Azure AI Search, Application Insights, Log Analytics; xUnit, bUnit, Testcontainers, Playwright
- **Azure resource group:** rg-StrategicGlue-CommandCenter
- **Resource naming pattern:** `{type}-strategicglue-{env}` (e.g., `psql-strategicglue-dev`, `kv-strategicglue-prod`)
- **Environments:** dev (auto-deploy on main push), prod (manual approval gate, release tag)
- **ARR Affinity:** Must be enabled — Blazor Server SignalR circuits require sticky sessions
- **pgBouncer:** PostgreSQL connections via port 6432, not default 5432
- **Test strategy:** xUnit (unit), bUnit (Blazor components), xUnit + Testcontainers (integration with real PostgreSQL), WebApplicationFactory (API/contract), Playwright (E2E — merge to main only). Coverage target: 80% domain logic. AI calls mocked at all layers.
- **Quality gate:** All tests pass + no new compiler warnings required to merge
- **4 workflows:** deploy-infra.yml, deploy-app.yml, validate-skills.yml, test.yml
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
