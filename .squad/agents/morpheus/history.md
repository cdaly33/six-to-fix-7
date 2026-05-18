# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT (custom tenant claims), EF Core, Azure PostgreSQL Flexible Server (pgBouncer on 6432), Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service (B2/P2v3), Azure Key Vault (managed identity), Azure Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — app issues its own tokens with \	enant_id\, \	enant_slug\, \oles\ claims. No OIDC server (Duende/OpenIddict not used).
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### ⚠️ 2026-05-17 — FOLLOW-UP: Client Bearer Token Wiring (Tank flagged)

Tank's prod 401 fix (PR #28) identified a critical gap: `Login.razor` stores JWT in `localStorage` but no client code wires it to HTTP requests for Blazor SSR navigations. Consequence: After login, subsequent page navigations still send no bearer token, so `[Authorize]` pages receive JwtBearer challenge → 401 → redirect loop potential.

**Scope for Morpheus:**
- Current: JwtBearer challenge override redirects browser to `/login?returnUrl=…` (302), preventing 401 exposure.
- Next: Add bearer token attachment to all `HttpClient` requests from Blazor (both SSR and API). Determine: client-side automatic wiring vs cookie-based auth redesign for browser flows.
- Decision required: If cookie auth for browser, JwtBearer reserved for `/api` only. Otherwise, implement `HttpMessageHandler` to inject bearer token from `localStorage`.

**Documented in:** decisions.md Phase 2, recommendation #4. Tank session log: 2026-05-17T22:19:46Z.

---

### Phases 0–6 Summary (Detailed learnings archived to history-archived.md)

**2026-05-10 to 2026-05-14:** Completed comprehensive architecture reviews across 6 phases. Key findings: (1) Service lifetime model (PolicyEngine Singleton, others Scoped). (2) Tenant isolation via EF Core global filters. (3) 5 HubSpot integration gaps identified and fixed. (4) Cross-layer architecture enforced. (5) Reviewer lockout race conditions solved with \pg_advisory_xact_lock\. All PRs reviewed and merged; 84 tests passing.

**See:** \history-archived.md\ for full Phase 0–6 details.

---

### 2026-05-15 — Security Review: Secret Handling & Deployment Guide

**Task:** Full trace of secrets flow through design-time, runtime, and deployment guide.

**Architecture Verdict:** Correct in principle. Runtime secrets in Key Vault only. \AddAzureKeyVault\ chain sound. GitHub Actions uses OIDC — no stored credentials.

**Three Real Problems (HIGH/MEDIUM severity):**

1. **PSReadLine History Exposure** — \z keyvault secret set --value "<secret>"\ records plaintext in \PSReadLine/ConsoleHost_history.txt\. **Fix:** Use \Read-Host -AsSecureString\ in deployment docs.

2. **Bicep Writes Admin to Runtime Secret** — \ootstrapSecrets\ uses \sfadmin\ for \DefaultConnection\ instead of least-privilege \sf_app\. **Fix:** Remove from bootstrap or mark as placeholder; Chris sets manually.

3. **Secret Name Mismatch** — Docs tell Chris to set \Jwt--SigningKey\; Bicep/appservice read \Jwt--Key\. **Fix:** Canonical names — \Jwt--SigningKey\, \HubSpot--PrivateAppToken\, \AzureOpenAI--ApiKey\ — applied consistently.

**Deliverable:** ✅ Merged to \decisions.md\ Phase 1. Awaiting implementation.

---

### Phase 1 — Stack Simplification & Security Fixes (2026-05-15, Ongoing)

- **Neo (SignalR→PeriodicTimer):** Replaced hub with polling (3s intervals). New endpoint: \GET /api/audit-runs/{id}/status\ (Bearer JWT, tenant-scoped).
- **Tank (Search Index Cleanup):** Removed unused indexes; only \six-to-fix-evidence\ remains.
- **Tank (Docs Sync):** Updated deployment guide for stack changes.
