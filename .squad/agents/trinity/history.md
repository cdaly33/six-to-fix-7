# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT, EF Core, Azure PostgreSQL Flexible Server, Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service, Azure Key Vault, Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — no OIDC server. App issues JWTs with `tenant_id`, `tenant_slug`, `roles` claims.
- **CSS approach:** Custom CSS design tokens only — no Tailwind, no Bootstrap, no MudBlazor. Warm cream/tan palette. Base tokens in `wwwroot/css/`.
- **Key design tokens:** `--bg-primary: #F5F0E8`, `--bg-dark: #1A1A2E`, `--accent: #2563EB`, `--border: #E5E0D5`, `--text-primary: #1A1A2E`. Cards: `border-radius: 8px`, `box-shadow: 0 1px 3px rgba(0,0,0,0.08)`, lift on hover.
- **Screen count:** 13 screens across 4 user roles (Super Admin, Tenant Admin, Auditor, Reviewer)
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
