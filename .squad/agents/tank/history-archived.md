# Tank — History Archive (Summarized from 25KB+ original)

**Purpose:** Compress Tank's full history into summary form to keep active history.md under 15KB.

**Archived Date:** 2026-05-20T04:30:00Z

## Summary of Learnings

### Phase 0 — Planning Artifacts (2026-05-10)
Produced 4 locked infrastructure + CI/CD planning artifacts:
- `environment-contract.md` — App Settings schema, Key Vault secrets, managed identity RBAC, pgBouncer (port 6432)
- `test-layer-matrix.md` — 5-layer test responsibility matrix (Unit/bUnit/Integration/API/E2E)
- `managed-identity-wiring.md` — DefaultAzureCredential chain, per-service wiring, Bicep roleAssignments
- `github-actions-dag.md` — 4-workflow inventory with full job DAGs

### Bicep & Infrastructure Issues Fixed
- **2026-05-17:** Application Insights `dependencies/failed` metric only accepts `timeAggregation: 'Count'` (not 'Total')
- **2026-05-17:** PostgreSQL region must be explicit param (centralus), not resource group location
- **2026-05-17:** pgBouncer: use `'True'` / `'False'` (capital T/F, string), not `'on'` or `'true'`
- **2026-05-17:** `linuxFxVersion` must be `'DOTNETCORE|10.0'` (not `DOTNET|10.0`); validate with `/health` probe
- **2026-05-17:** **CRITICAL:** Always run `az deployment group validate` (not just `az bicep build`) to catch ARM runtime errors

### Search Index Cleanup (2026-05-15)
Removed `six-to-fix-skill-outputs` and `six-to-fix-calibration` indexes. Only `six-to-fix-evidence` (Standard tier + 2 replicas). Updated 5 files (Bicep, PowerShell provisioner, C# client, tests, docs).

### Deployment Docs Sync (2026-05-15)
Always verify deployment docs against code behavior. Removed stale SearchIndex references, updated SignalR references (now PeriodicTimer polling), verified script output examples.

### Secret Handling (2026-05-15)
- DefaultConnection must use `sf_app` role, never `sfadmin`
- PSReadLine history captures plaintext secrets — use `Read-Host -AsSecureString` in docs
- Bicep secret names must match Program.cs config keys exactly
- Empty KV slots safer than placeholder secrets

### Prod 401 Bug & Fix (2026-05-17)
JwtBearer challenge override needed for Blazor SSR pages. Added `JwtBearerEvents.OnChallenge` to 302 to `/login` for HTML/browser navigations (detect via `Accept` header + `Sec-Fetch-Mode`), keep 401 for API/XHR.

### Bicep Drift Prevention (2026-05-18)
**Standing rule:** Any manual Azure change not in Bicep is a time-bomb. Tank must proactively open PR to codify it. Added SeedAdmin KV references (`SeedAdmin--Email`, `SeedAdmin--Password`) to `appservice.bicep` (PR #39).

### CSP & Security Headers (2026-05-18)
Created `SecurityHeadersMiddleware.cs` (registered step 2 in Program.cs). CSP directives: `script-src 'unsafe-inline'` (Blazor circuit), `style-src 'unsafe-inline'` (scoped CSS), `font-src` + `connect-src` for Google Fonts + WebSocket. Also: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`. CI audit: deploy-infra.yml had stale RG name (fixed `rg-StrategicGlue-CommandCenter` → `rg-sixtofix-prod`).

### SeedAdmin Security Fix (2026-05-19, PR #56)
Added bool param `seedAdminEnabled` to appservice.bicep (defaults false). Previously, Prod always re-seeded admin on restart. Changed to explicit opt-in. Fixed smoke test: `GET /` now expects 200 (Phase 2 public homepage), not 302.

### Calibration/Telemetry Cleanup
No action needed. Code completely removed; only EF Core Designer snapshots remain (auto-generated, never manually edit).

## Standing Rules Established by Tank

1. **Proactive Bicep PR for all manual Azure changes** (drift prevention)
2. **Always run `az deployment group validate`** (catch ARM errors, not just syntax)
3. **Explicit bool params for security-sensitive on/off** (not `isProd` ternary)
4. **Smoke test post-deploy** (GET /, GET /health, 401 error path)
5. **Secret handling:** PSReadLine history, least-privilege roles, KV secret → code config parity
6. **pgBouncer value:** `'True'` (capital), not lowercase
7. **Deployment docs always current** (audit against actual code)

## Key Files Changed
- `infra/modules/appservice.bicep` — SeedAdmin KV refs, seedAdminEnabled param
- `infra/modules/postgres.bicep` — pgBouncer config
- `src/SixToFix.Web/Middleware/SecurityHeadersMiddleware.cs` — CSP
- `src/SixToFix.Web/Program.cs` — middleware registration, JwtBearer challenge override
- `.github/workflows/deploy-app.yml` — smoke test corrections, RG name fix
- `docs/deployment/` — multiple docs sync updates
- `docs/architecture/` — 4 planning artifacts

## Test Results
- **Phase 0 seal:** 15 inbox files merged, Phase 1 gate CLEAR
- **Final:** 98 unit tests passing, 0 errors, Bicep validated clean

---

**Next steps for Tank:** Monitor drift; continue proactive Bicep PR culture; validate all future schema changes with ARM.
