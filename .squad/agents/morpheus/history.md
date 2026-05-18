# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform automating marketing maturity audits across 6 domains, with AI skill chain execution, Policy Engine, AI Council deliberation, and Reviewer workflow
- **Stack:** .NET 10 LTS, Blazor Server (SignalR circuit, no SPA), ASP.NET Core, ASP.NET Core Identity + JWT (custom tenant claims), EF Core, Azure PostgreSQL Flexible Server (pgBouncer on 6432), Azure OpenAI Service, Azure Blob Storage, Azure AI Search, Azure App Service (B2/P2v3), Azure Key Vault (managed identity), Azure Bicep, GitHub Actions
- **Auth decision:** ASP.NET Core Identity + JWT — app issues its own tokens with \	enant_id\, \	enant_slug\, \oles\ claims. No OIDC server (Duende/OpenIddict not used).
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-17 — Phase Auth: Dual Cookie+JWT Scheme (Proper Fix for Tank's prod 401)

**Task:** Replace Tank's `JwtBearerEvents.OnChallenge` redirect shim with a real
dual-auth pipeline. Branch: `dev/phase-auth-cookie-scheme`.

**Architecture decision (full ADR draft: `.squad/decisions/inbox/morpheus-dual-auth-scheme.md`):**

- **Cookie scheme = default** for `DefaultScheme`, `DefaultSignInScheme`,
  `DefaultChallengeScheme`. Browser channel (Blazor Server SSR + login flow)
  uses the cookie because HTTP nav requests carry cookies natively; they do
  NOT carry JS-attached `Authorization` headers.
- **JwtBearer scheme = explicitly pinned per `/api/*` endpoint** via a small
  `BearerPolicy(policyName?)` helper that produces
  `AuthorizeAttribute { AuthenticationSchemes = Bearer, Policy = … }`.
  Touching the existing `.RequireAuthorization(...)` call sites was preferable
  to introducing a route group — keeps minimal-API path strings literal.
- **Named policies** (`SuperAdmin/TenantAdmin/Reviewer/Viewer`) accept BOTH
  schemes so the same role logic works regardless of which scheme
  authenticated. The API endpoints then narrow to Bearer-only via the
  attribute override.
- **Cookie events** route `/api/*` failures to raw `401/403`; everything else
  to `/login` (replaces Tank's manual `Accept`/`Sec-Fetch-Mode` sniffing).

**Claim parity gotcha:** `LoginResult` originally lacked `TenantSlug`. Without
it, cookie sign-in would have produced a principal missing the `tenant_slug`
claim that `TenantContextMiddleware` requires. Added `TenantSlug` to
`LoginResult` and passed `user.TenantSlug` through `AuthService.BuildResultAsync`.

**Package gotcha:** `JwtBearerDefaults` lives in the
`Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package, NOT in the
`Microsoft.AspNetCore.App` shared framework. The `SixToFix.Api` project did
not reference it (only `Microsoft.AspNetCore.App` framework ref). Added the
package to `SixToFix.Api.csproj`. (Web only compiled because it inherits the
ref transitively through `Infrastructure`.)

**Cookie + JWT both issued on `/api/auth/login`:** The endpoint now calls
`HttpContext.SignInAsync(CookieScheme, principal)` *and* returns the JWT in
the response body. Same-origin `fetch` from `Login.razor` honours
`Set-Cookie` automatically — so Trinity's `Login.razor` does not strictly
need to change for the redirect loop to close. Her component-side work can
focus on UX (e.g., dropping the `localStorage` JWT for first-party flows).

**Logout:** Added `GET /logout` → `SignOutAsync(Cookies)` → redirect `/login`.
Matches the existing `TopNav.razor` "Sign out" anchor href.

**Verification:** Build clean. Tests: 120 passed
(Domain 34, Infrastructure 54, Web 18, Api 14), 0 failed.

---

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
