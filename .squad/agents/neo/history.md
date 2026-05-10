# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains (Brand, Customer, Offering, Communications, Sales, Management)
- **Stack:** .NET 10 LTS, ASP.NET Core, EF Core, Azure PostgreSQL Flexible Server (v16, pgBouncer on port 6432), ASP.NET Core Identity + JWT, Azure OpenAI Service (via IAIClient interface), Azure Blob Storage, Azure AI Search, Azure App Service, Azure Key Vault (managed identity)
- **Auth decision:** ASP.NET Core Identity + JWT — app issues JWTs with `tenant_id`, `tenant_slug`, `roles` claims. No OIDC server.
- **Database:** 15 tables, strict `tenant_id` FK isolation on all tenant-scoped tables. Append-only `category_result_versions` ledger. Two DB roles: `sf_admin` (DDL+DML, migrations), `sf_app` (DML only, runtime). pgBouncer on 6432.
- **Service layer (8 services):** AuditOrchestrator, SkillRunner, PolicyEngine (Singleton), CouncilRunner, ReviewerWorkflow, Publisher, CalibrationTracker, TelemetryCollector
- **Reviewer lockout:** 3 rejections of same category within 24h → HTTP 409 REVIEWER_REJECTION_LOCKOUT
- **Publishing:** Immutable, versioned. All 6 categories must be approved before publishing.
- **CalibrationDelta:** Created on every reviewer score override. Never skipped.
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
