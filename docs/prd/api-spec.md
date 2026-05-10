# API Specification Reference

**Product:** StrategicGlue Six-to-Fix  
**Author:** Frink (Backend Dev)  
**Date:** 2026-05-10  
**Status:** Specification — For Engineering Team

---

## Overview

The StrategicGlue Six-to-Fix system uses **Blazor Server** for all primary UI interactions. Blazor components communicate with services via direct C# method calls over the SignalR circuit — there are no REST round-trips for UI data access.

The system **does** expose REST endpoints for:
- Operational health and metrics
- Inbound webhooks (HubSpot)
- Super-admin tenant management
- External consumers of published audit data

All REST endpoints are implemented as ASP.NET Core Minimal API endpoints unless otherwise noted.

---

## Authentication and Authorization

| Role | Description |
|---|---|
| `SuperAdmin` | Platform-level administrator; manages tenants |
| `TenantAdmin` | Tenant-level administrator; manages users and clients |
| `Auditor` | Creates and runs audits |
| `Reviewer` | Reviews and approves category payloads |
| `OpsViewer` | Read-only access to operational metrics |

All authenticated endpoints require a valid JWT bearer token (Azure AD B2C). Role claims are extracted from the token and enforced by ASP.NET Core authorization policies.

Unauthenticated endpoints: `/health`, `/webhooks/hubspot` (HMAC-validated separately).

---

## Base URL

| Environment | Base URL |
|---|---|
| Production | `https://app.strategicglue.com` |
| Staging | `https://staging.strategicglue.com` |
| Development | `https://localhost:5001` |

---

## Standard Response Envelope

All REST responses use `application/json`. Errors use `application/problem+json` (RFC 7807).

**Success response:** Raw resource object or array. No wrapper envelope.

**Error response:**
```json
{
  "type": "https://strategicglue.com/errors/{error-code}",
  "title": "Human-readable error title",
  "status": 409,
  "detail": "Detailed description of what went wrong",
  "code": "MACHINE_READABLE_CODE",
  "correlationId": "uuid-from-X-Correlation-ID-header",
  "traceId": "aspnet-activity-trace-id"
}
```

---

## Health and Ops Endpoints

### `GET /health`

Returns the current health status of the system and its dependencies.

**Authentication:** None required.

**Response: `200 OK`**
```json
{
  "status": "healthy",
  "checks": {
    "db": "ok",
    "storage": "ok",
    "search": "ok",
    "openai": "ok",
    "keyVault": "ok"
  },
  "timestamp": "2026-05-10T14:20:11Z"
}
```

**Response: `503 Service Unavailable`** — one or more checks failed. Same shape, `status` = `"degraded"` or `"unhealthy"`, failed check value = `"failed"`.

---

### `GET /api/ops/metrics/daily`

Returns a daily telemetry snapshot for all completed audit runs on the specified date.

**Authentication:** Required — `SuperAdmin` or `OpsViewer` role.

**Query Parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `date` | `string (YYYY-MM-DD)` | Yes | The calendar date to retrieve metrics for |

**Response: `200 OK`**
```json
{
  "date": "2026-05-10",
  "samples": [
    {
      "auditRunId": "uuid",
      "tenantId": "uuid",
      "clientSlug": "acme-corp",
      "totalTokensUsed": 42800,
      "totalLatencyMs": 18400,
      "skillRunCount": 5,
      "policyTriggerCount": 2,
      "councilRunCount": 1,
      "reviewerActionCount": 6,
      "completedAt": "2026-05-10T09:14:33Z"
    }
  ],
  "totalSamples": 1
}
```

**Error Codes:**

| Status | Code | Condition |
|---|---|---|
| `400` | `INVALID_DATE_FORMAT` | `date` param not parseable as YYYY-MM-DD |
| `401` | — | Missing or invalid JWT |
| `403` | `INSUFFICIENT_ROLE` | Caller lacks `SuperAdmin` or `OpsViewer` role |

---

## Webhook Endpoints

### `POST /webhooks/hubspot`

Receives inbound events from HubSpot (contact created, company updated, etc.).

**Authentication:** HMAC-SHA256 signature validation using the configured HubSpot private app secret. The system validates the `X-HubSpot-Signature` header before processing any payload.

**Request Headers:**

| Header | Description |
|---|---|
| `X-HubSpot-Signature` | HMAC-SHA256 of request body using app secret |
| `Content-Type` | `application/json` |

**Request Body:** HubSpot event array (per HubSpot Webhooks API v3 schema).

**Response: `200 OK`** — always returned immediately if HMAC validation passes, before processing the event body. This prevents HubSpot from retrying due to processing latency.

**Response: `401 Unauthorized`** — HMAC validation failed. Body: `{ "error": "INVALID_SIGNATURE" }`.

**Business Rules:**
- Events are deduplicated by `portalId + subscriptionId + occurredAt` composite key stored in `hubspot_sync_log`.
- Duplicate events (same composite key) are accepted (200) and silently discarded.
- Processing occurs asynchronously in a background `Channel<HubSpotEvent>` worker. HTTP response precedes processing.

---

## Admin API (Super Admin)

All endpoints in this section require `SuperAdmin` role.

### `GET /api/admin/tenants`

Lists all tenants registered in the platform.

**Authentication:** `SuperAdmin` required.

**Query Parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `page` | `integer` | No | Page number (1-based, default: 1) |
| `pageSize` | `integer` | No | Items per page (default: 20, max: 100) |
| `search` | `string` | No | Filter by tenant name (case-insensitive substring) |

**Response: `200 OK`**
```json
{
  "items": [
    {
      "id": "uuid",
      "name": "Acme Agency",
      "slug": "acme-agency",
      "plan": "professional",
      "isActive": true,
      "createdAt": "2026-01-15T08:00:00Z"
    }
  ],
  "total": 42,
  "page": 1,
  "pageSize": 20
}
```

---

### `POST /api/admin/tenants`

Creates a new tenant.

**Authentication:** `SuperAdmin` required.

**Request Body:**
```json
{
  "name": "Acme Agency",
  "slug": "acme-agency",
  "plan": "professional",
  "adminEmail": "admin@acme-agency.com"
}
```

| Field | Type | Required | Constraints |
|---|---|---|---|
| `name` | `string` | Yes | 2–100 characters |
| `slug` | `string` | Yes | 3–50 chars, lowercase alphanumeric + hyphens, globally unique |
| `plan` | `string` | Yes | One of: `starter`, `professional`, `enterprise` |
| `adminEmail` | `string` | Yes | Valid email; becomes the first TenantAdmin user |

**Response: `201 Created`** — full tenant object. `Location` header set to `/api/admin/tenants/{id}`.

**Error Codes:**

| Status | Code | Condition |
|---|---|---|
| `400` | `VALIDATION_ERROR` | Missing or invalid fields |
| `409` | `SLUG_CONFLICT` | Slug already in use |

---

### `GET /api/admin/tenants/{id}`

Returns full detail for a single tenant.

**Authentication:** `SuperAdmin` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `UUID` | Tenant ID |

**Response: `200 OK`** — full tenant object including user count and client count.

**Error Codes:**

| Status | Code | Condition |
|---|---|---|
| `404` | `TENANT_NOT_FOUND` | No tenant with given ID |

---

### `PUT /api/admin/tenants/{id}`

Updates tenant metadata.

**Authentication:** `SuperAdmin` required.

**Path Parameters:** `id` (UUID) — tenant to update.

**Request Body:**
```json
{
  "name": "Updated Agency Name",
  "plan": "enterprise",
  "isActive": true
}
```

All fields are optional; only provided fields are updated (PATCH semantics on a PUT endpoint, applied server-side).

**Response: `200 OK`** — updated tenant object.

**Error Codes:**

| Status | Code | Condition |
|---|---|---|
| `404` | `TENANT_NOT_FOUND` | No tenant with given ID |
| `409` | `SLUG_CONFLICT` | If slug update was attempted and conflicts |

---

## Audit Data API (External Consumers)

These endpoints expose published audit data for potential external consumers (partner systems, reporting tools, future public API). They are tenant-scoped via the authenticated JWT.

### `GET /api/audits/{clientSlug}`

Returns the latest published audit for the specified client, within the authenticated tenant.

**Authentication:** Required — any authenticated tenant role (`TenantAdmin`, `Auditor`, `Reviewer`). Caller's tenant must own the client.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `clientSlug` | `string` | The client's URL slug |

**Response: `200 OK`**
```json
{
  "clientSlug": "rolling-rock-stone",
  "clientName": "Rolling Rock Stone",
  "publishedAt": "2026-05-10T12:00:00Z",
  "version": 3,
  "tier": "Tier 2",
  "overallScore": 6.4,
  "categories": {
    "brand": { "activityScore": 7.0, "documentedStrategy": "partial", "tier": "Tier 2" },
    "customer": { "activityScore": 5.5, "documentedStrategy": "none", "tier": "Tier 3" },
    "offering": { "activityScore": 8.0, "documentedStrategy": "full", "tier": "Tier 1" },
    "communications": { "activityScore": 6.0, "documentedStrategy": "partial", "tier": "Tier 2" },
    "sales": { "activityScore": 5.0, "documentedStrategy": "none", "tier": "Tier 3" },
    "management": { "activityScore": 7.5, "documentedStrategy": "partial", "tier": "Tier 2" }
  }
}
```

**Error Codes:**

| Status | Code | Condition |
|---|---|---|
| `404` | `CLIENT_NOT_FOUND` | No client with that slug in the tenant |
| `404` | `NO_PUBLISHED_AUDIT` | Client exists but has no published audit |
| `403` | `TENANT_MISMATCH` | Authenticated tenant does not own this client |

---

### `GET /api/audits/{clientSlug}/versions`

Lists all published audit versions for a client.

**Authentication:** Required — any authenticated tenant role.

**Path Parameters:** `clientSlug` (string).

**Response: `200 OK`**
```json
{
  "clientSlug": "rolling-rock-stone",
  "versions": [
    { "version": 3, "publishedAt": "2026-05-10T12:00:00Z", "publishedBy": "uuid" },
    { "version": 2, "publishedAt": "2026-04-01T10:00:00Z", "publishedBy": "uuid" },
    { "version": 1, "publishedAt": "2026-03-01T09:00:00Z", "publishedBy": "uuid" }
  ]
}
```

**Error Codes:** Same as `GET /api/audits/{clientSlug}`.

---

## SignalR Hub: Real-Time Audit Run Progress

The system shall expose a SignalR hub for real-time skill run progress updates to connected Blazor clients.

**Hub Endpoint:** `/hubs/audit-run`

**Authentication:** Required — same JWT bearer token as REST endpoints, passed as query string `?access_token={token}` for SignalR WebSocket connections.

### Client Methods (Hub → Client)

These are events the server pushes to connected clients:

| Event | Payload | Description |
|---|---|---|
| `skill-started` | `{ auditRunId, skillId, skillIndex, totalSkills, startedAt }` | Fired when a skill begins execution |
| `skill-completed` | `{ auditRunId, skillId, skillIndex, durationMs, policyFlags, requiresCouncil }` | Fired when a skill run completes successfully |
| `skill-failed` | `{ auditRunId, skillId, skillIndex, failureReason, error }` | Fired when a skill run fails (schema validation, circuit open, timeout) |
| `council-started` | `{ auditRunId, category, triggeredBy }` | Fired when AI Council deliberation begins for a category |
| `council-completed` | `{ auditRunId, category, adjustedScore, durationMs }` | Fired when AI Council produces a decision |
| `run-completed` | `{ auditRunId, totalDurationMs, categoryCount, requiresReviewCount }` | Fired when all skills complete and the run transitions to `completed` |
| `run-failed` | `{ auditRunId, failedSkillId, reason }` | Fired when the run is halted due to an unrecoverable skill failure |

### Hub Groups

Clients join a group keyed by `auditRunId` on connection. The server sends events to the group, not to individual connections. This ensures all browser tabs watching the same run receive updates.

**Server method (Client → Hub):** `JoinRun(string auditRunId)` — subscribes the connection to the run's group. The hub validates that the calling tenant owns the specified `auditRunId`.

---

## Correlation and Tracing

Every HTTP response shall include:
- `X-Correlation-ID` header — the correlation ID assigned or forwarded for the request.
- `X-Trace-ID` header — the ASP.NET Core activity trace ID (links to Application Insights).

Callers should include `X-Correlation-ID` on inbound requests to propagate their own correlation context.
