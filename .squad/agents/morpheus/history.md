# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT (custom tenant claims), EF Core, Azure PostgreSQL Flexible Server (pgBouncer on 6432), Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service (B2/P2v3), Azure Key Vault (managed identity), Azure Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — app issues its own tokens with `tenant_id`, `tenant_slug`, `roles` claims. No OIDC server (Duende/OpenIddict not used).
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
