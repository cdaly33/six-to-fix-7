# Neo Phase 3 — Minimal API Endpoints Decisions

**Author:** Neo | **Date:** 2026-05-10 | **Phase:** 3

---

## ADR-NEO-P3-001: IAuthService Interface Isolates UserManager from SixToFix.Api

**Decision:** Login and token re-issue functionality is exposed through `IAuthService` (Application layer) rather than resolving `UserManager<ApplicationUser>` directly in endpoint handlers.

**Rationale:** `SixToFix.Api` references only `SixToFix.Application`. `UserManager<ApplicationUser>` is in `SixToFix.Infrastructure.Auth`. Introducing a direct reference would create a layer violation and circular dependency risk. `IAuthService` is the boundary.

**Implementation:** `IAuthService.LoginAsync(email, password)` → `AuthService` resolves `UserManager<ApplicationUser>`, verifies password, gets roles, calls `ITokenService.GenerateAccessToken`. `IAuthService.ReissueTokenAsync(userId)` is the "refresh" — no refresh token table; re-issues from the current principal's userId claim (caller must have valid token to hit the endpoint).

---

## ADR-NEO-P3-002: Reviewer Reject = Serializable TX + Advisory Lock + Counter Update Atomically

**Decision:** `ReviewerWorkflow.RejectAsync` opens a serializable transaction, acquires `pg_advisory_xact_lock` keyed on `(categoryId, reviewerId)`, reads `ReviewerLockout`, checks count, and either throws `ReviewerLockoutException` or inserts the rejection and increments the counter — all in one transaction.

**Rationale:** This mirrors the existing `CheckLockoutAsync` pattern. Combining the check and the counter update in the same serializable tx prevents a race where two concurrent rejects both pass the lockout check before either has written the increment. The advisory lock serializes concurrent rejects for the same (category, reviewer) pair.

**Counter behavior:** First rejection creates a new `ReviewerLockout` row with `RejectionCount=1`, `IsLocked=false`. Subsequent rejections increment. At count ≥ 3, `IsLocked=true`. The window check (`WindowStartedAt > now() - 24h`) guards whether an existing row is still active.

---

## ADR-NEO-P3-003: IPublisher.GetPublishedAuditByRunIdAsync(Guid) vs GetPublishedAuditAsync(string clientSlug)

**Decision:** Added a new method `GetPublishedAuditByRunIdAsync(Guid auditRunId)` rather than overloading the existing `GetPublishedAuditAsync`. Both coexist on the interface.

**Rationale:** The existing method is consumed by the public-facing client portal (slug-based). The new method serves the authenticated reviewer/admin flow (`GET /api/published/{auditRunId}`). Different access patterns; different query strategies. Slug-based queries traverse Client → Audit → AuditRun. ID-based queries go direct to AuditRun. Keeping them separate avoids overloading ambiguity.

**404 behavior:** `GetPublishedAuditByRunIdAsync` throws `AuditRunNotFoundException` if the run does not exist OR if it exists but is not yet published (status != "published"). The filter is in the EF query: `r.Status == "published"`.

---

## ADR-NEO-P3-004: HubSpot Webhook Body Buffering and HMAC Validation

**Decision:** The `/api/webhooks/hubspot` endpoint calls `Request.EnableBuffering()`, reads the full body as a string using `StreamReader`, resets the stream position, then calls `IHubSpotClient.ValidateWebhookSignatureAsync(signature, body)` before deserializing the payload.

**Rationale:** HMAC validation requires the raw body bytes/string. Minimal API routes that bind from JSON body consume the stream. `EnableBuffering()` writes body to a memory/disk buffer allowing multiple reads. Stream is reset to position 0 after reading so it remains available to the DI container if needed.

**Signature header:** `X-HubSpot-Signature`. Returns 401 on invalid signature, 400 on deserialization failure, 202 Accepted on success.

---

## ADR-NEO-P3-005: Authorization Policy Hierarchy Applied at Endpoint Level

**Decision:** Endpoints use the named policies defined in `Program.cs` (`"TenantAdmin"`, `"Reviewer"`, `"Viewer"`) rather than `[Authorize(Roles="...")]` strings or inline role lists.

**Rationale:** Policies are hierarchical (TenantAdmin policy accepts SuperAdmin+TenantAdmin roles; Reviewer policy accepts those plus Reviewer). This prevents role drift — if a new role is added above TenantAdmin in the hierarchy, only `Program.cs` changes, not every endpoint. The `VerifyTenantOwnership` helper in `ApiEndpointExtensions` additionally checks that the caller's `tenant_id` claim matches the resource's tenant where needed.

---

## ADR-NEO-P3-006: ICalibrationTracker.GetCalibrationHistoryAsync Joins Through Audit Graph

**Decision:** `GetCalibrationHistoryAsync(Guid clientId)` fetches all `CalibrationDelta` rows for a client by first resolving `Audits.Where(a.ClientId == clientId)`, then `AuditRuns.Where(r.AuditId in auditIds)`, then `CalibrationDeltas.Where(d.AuditRunId in auditRunIds)`.

**Rationale:** There is no direct FK from `CalibrationDelta` to `Client`. The join goes through the audit graph. This is three sequential EF queries to avoid unbounded Cartesian product joins. Results are ordered by `CreatedAt DESC` for the history view.

**EF Core global query filter note:** `SixToFixDbContext` tenant filter ensures only the current tenant's audits and deltas are visible. The service layer does not add additional `.Where(tenant_id == ...)` (per ADR-002).
