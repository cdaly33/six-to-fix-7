# Decisions

## Phase 2 — Production Stability & Auth Flow

### Tank — Prod 401 fix and prevention proposal
**Author:** Tank (DevOps & QA)  
**Date:** 2026-05-17  
**Status:** Accepted (PR #28 merged, deploy run 26011803996, verified live)

After deploying `.NET 10` with `linuxFxVersion: 'DOTNETCORE|10.0'`, the site began returning `HTTP 401 Unauthorized` with `WWW-Authenticate: Bearer` on `/` and all Blazor `[Authorize]` pages. The app was actually healthy (`/health` 200, `/login` 200, Easy Auth confirmed off). The 401 was the app's own JwtBearer challenge firing because:
- `JwtBearer` is the only auth scheme registered in `Program.cs`.
- Blazor pages with `[Authorize]` are mapped via `MapRazorComponents`, which invokes endpoint authorization middleware and calls `ChallengeAsync` against the default scheme BEFORE Blazor SSR renders the `<NotAuthorized>` component.
- The `<RedirectToLogin />` component in `Routes.razor` never runs for unauthenticated browsers. They receive raw 401 + Bearer challenge (appears "unavailable").

**Decision (PR #28 merged):** Override `JwtBearerEvents.OnChallenge` in `Program.cs` to redirect HTML/browser navigations to `/login?returnUrl=…` (302). API and XHR callers (path under `/api` AND no `text/html` accept AND no `Sec-Fetch-Mode: navigate`) continue to receive 401 unchanged.

**Recommendations for team:**
1. **Post-deploy smoke step in deploy-app.yml:** After App Service deploy, run:
   - `GET /health` must be 200.
   - `GET /` with `Accept: text/html` must be 200 or 302 (never 401, 403, or nginx server header).
   - `GET /api/…` with no bearer token must be 401.
   - Response `Server` header must be `Kestrel`.

2. **Bicep guardrail:** Add validation in deploy-infra.yml to assert `linuxFxVersion` starts with `DOTNETCORE|`.

3. **WebApplicationFactory test:** Unauthenticated `GET /` with `Accept: text/html` → 302 to `/login`. Same test with `/api/*` → 401.

4. **Longer-term client bearer wiring (flagged for Trinity/Morpheus):** Login.razor stores JWT in `localStorage` but no client code attaches it as a Bearer header on Blazor SSR navigations — so logging in does not actually authenticate subsequent navigations. Requires either cookie auth for browser flows (JwtBearer reserved for `/api`) or SPA model overhaul.

**Resolved by:** PR #28 (merged 2026-05-17 03:24 UTC), verified live with `curl` against prod.


### Tank — Prod Login Recovery — Password Typo Diagnosis
**Author:** Tank (DevOps & QA)  
**Date:** 2026-05-20  
**Status:** Resolved — User Error

Chris reported being locked out of prod (https://app-sixtofix-prod.azurewebsites.net/) with credentials `chris@christopherdaly.com` / `GYyE3jnmvGJuMyjtNQAk!`, claiming "last night this exact password worked." PRs #56 (Tank: SeedAdmin defaults to false), #57 (Neo: TenantService), and #58 (Trinity: default pillar content seeding) had shipped between "last night" and this morning.

**Investigation findings:**
- KV secret created 2026-05-18 10:36 PM: `GYyE3jnmvGJuMyjtNQAk1!` (with `1` before `!`)
- Chris's notes had typo: missing the `1`
- Database user `55d11c4a-d353-4683-a8b4-2ae1e23ca983` created 2026-05-19 03:37 AM, never updated, not locked (`lockout_end=NULL`, `access_failed_count=0`)
- AdminBootstrapHostedService correctly skips re-seeding when SuperAdmin exists
- PR #56 changed `SeedAdmin__Enabled` to default `false` AFTER admin creation — no re-hashing occurred
- Login test with correct password: `curl -X POST https://app-sixtofix-prod.azurewebsites.net/api/auth/login` returned HTTP 200 with JWT

**Root cause:** Password typo in Chris's notes. Correct credentials: `chris@christopherdaly.com` / `GYyE3jnmvGJuMyjtNQAk1!`

**Lesson:** Always verify KV secrets match user notes; Key Vault is the source of truth.


