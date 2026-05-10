# HubSpot Integration — Field Mapping Specification

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Locked — Architectural Commitment  

---

## Overview

Six-to-Fix integrates bidirectionally with HubSpot via the HubSpot CRM API v3 (Companies object) and HubSpot Webhooks API v3 (inbound). All HubSpot credentials (private app tokens) are stored in Azure Key Vault, per-tenant, and accessed via managed identity. HubSpot operations are tenant-scoped: each tenant has their own HubSpot portal and token.

**HubSpot client interface:** `IHubSpotClient` — wraps all outbound API calls with correlation ID logging and failure handling.

---

## Outbound — Client Create (HubSpot Company Upsert)

### Trigger

Fires when a new `Client` record is created in the platform. Event: `Client.created`.

**Timing:** Synchronous with the client creation transaction, but HubSpot call is made asynchronously via a background task to prevent HubSpot latency from blocking the client creation response. If the HubSpot call fails, `clients.hubspot_sync_status` is set to `'failed'` and the client creation still succeeds.

### HubSpot Object Type

`Company`

### Operation

**Upsert** — idempotent create-or-update.

**Deduplication key:** `clients.company_domain` → HubSpot `domain` property. If `company_domain` is null (domain is optional), fall back to matching on the custom `strategicglue_client_id` property.

**Idempotency:** On retry, the upsert uses the `strategicglue_client_id` custom property as the definitive lookup key. HubSpot's `POST /crm/v3/objects/companies/search` is called first to check if a company with that `strategicglue_client_id` already exists. If found, the existing company's properties are patched (PATCH). If not found, a new company is created (POST). This prevents duplicate HubSpot companies on retry.

### Field Mapping

| Six-to-Fix Field | HubSpot Property | Type | Notes |
|---|---|---|---|
| `clients.name` | `name` | `string` | HubSpot standard property. Company display name. |
| `clients.company_domain` | `domain` | `string` | HubSpot standard property. Used as deduplication key. May be null — do not send if null. |
| `tenants.id` | `strategicglue_tenant_id` | `string` | Custom property. Enables reverse lookup from HubSpot → tenant. |
| `clients.id` | `strategicglue_client_id` | `string` | Custom property. Idempotency key for upserts and retries. |
| `clients.industry` | `industry` | `string` | HubSpot standard property. Send if non-null. |
| `clients.employee_count_range` | `numberofemployees` | `string` | HubSpot standard property. Sent as a string band (e.g., `"10-50"`). |
| `clients.annual_revenue_range` | `annualrevenue` | `string` | HubSpot standard property. Sent as string band. |
| `tenants.slug` | `strategicglue_tenant_slug` | `string` | Custom property. Human-readable tenant reference for HubSpot users. |
| `clients.created_at` | `strategicglue_client_created_at` | `datetime` | Custom property. ISO 8601 UTC. |

### Custom Properties to Register in HubSpot

The following custom properties must be created in HubSpot before integration is active. This is a one-time setup per HubSpot portal:

| Property Name | Label | Type | Object |
|---|---|---|---|
| `strategicglue_tenant_id` | StrategicGlue Tenant ID | `string` | Company |
| `strategicglue_tenant_slug` | StrategicGlue Tenant Slug | `string` | Company |
| `strategicglue_client_id` | StrategicGlue Client ID | `string` | Company |
| `strategicglue_tier` | StrategicGlue Tier | `enumeration` (`tier_1`, `tier_2`, `tier_3`) | Company |
| `strategicglue_composite_score` | StrategicGlue Composite Score | `number` | Company |
| `strategicglue_brand_score` | StrategicGlue Brand Score | `number` | Company |
| `strategicglue_customer_score` | StrategicGlue Customer Score | `number` | Company |
| `strategicglue_offering_score` | StrategicGlue Offering Score | `number` | Company |
| `strategicglue_communications_score` | StrategicGlue Communications Score | `number` | Company |
| `strategicglue_sales_score` | StrategicGlue Sales Score | `number` | Company |
| `strategicglue_management_score` | StrategicGlue Management Score | `number` | Company |
| `strategicglue_systems_maturity_score` | StrategicGlue Systems Maturity Score | `number` | Company |
| `strategicglue_ai_readiness` | StrategicGlue AI Readiness % | `number` | Company |
| `strategicglue_last_audit_date` | StrategicGlue Last Audit Date | `date` | Company |
| `strategicglue_client_created_at` | StrategicGlue Client Created At | `datetime` | Company |

### Post-Upsert State Update

On successful upsert:
- `clients.hubspot_company_id` ← HubSpot Company `hs_object_id`
- `clients.hubspot_sync_status` ← `'synced'`

On failure:
- `clients.hubspot_sync_status` ← `'failed'`
- Error logged at `Error` level with `tenant_id`, `client_id`, HTTP status from HubSpot
- No retry by the application — failure is surfaced in the Tenant Admin Panel for manual resolution

---

## Outbound — Audit Publish (Score/Tier Push)

### Trigger

Fires when `audit_runs.status` transitions to `'published'`. Event: `AuditRun.published`.

**Timing:** Asynchronous — the audit publish action completes immediately; the HubSpot push is a background task. Failure is non-blocking and does not revert the published status.

### HubSpot Object Type

`Company` (matched via `strategicglue_client_id` custom property)

### Operation

`PATCH /crm/v3/objects/companies/{hubspot_company_id}` — direct property update using the stored `clients.hubspot_company_id`. If `hubspot_company_id` is null (company not yet synced), the push is deferred until `hubspot_sync_status = 'synced'`. A warning is logged.

### Field Mapping

| Six-to-Fix Field | HubSpot Property | Type | Notes |
|---|---|---|---|
| `derive_tier_output.tier` | `strategicglue_tier` | `enumeration` | One of: `tier_1`, `tier_2`, `tier_3` |
| `scorecard_output.composite_score` | `strategicglue_composite_score` | `number` | Integer 0–60 sent as number |
| `scorecard_output.area_scores.brand` | `strategicglue_brand_score` | `number` | Integer 0–10 |
| `scorecard_output.area_scores.customer` | `strategicglue_customer_score` | `number` | Integer 0–10 |
| `scorecard_output.area_scores.offering` | `strategicglue_offering_score` | `number` | Integer 0–10 |
| `scorecard_output.area_scores.communications` | `strategicglue_communications_score` | `number` | Integer 0–10 |
| `scorecard_output.area_scores.sales` | `strategicglue_sales_score` | `number` | Integer 0–10 |
| `scorecard_output.area_scores.management` | `strategicglue_management_score` | `number` | Integer 0–10 |
| `systems_maturity_output.systems_maturity_score` | `strategicglue_systems_maturity_score` | `number` | Integer 0–20 |
| `derive_tier_output.ai_readiness` | `strategicglue_ai_readiness` | `number` | Integer 0–100 |
| `audit_runs.completed_at` (publish timestamp) | `strategicglue_last_audit_date` | `date` | ISO 8601 date only (no time component per HubSpot date type) |

**Note on score source:** Area scores in HubSpot always reflect the final published scores — i.e., after council adjustments and reviewer edits have been applied. The values come from `category_results_current` joined to `category_result_versions.activity_score`, not directly from Skill 1 raw output.

### Idempotency

HubSpot PATCH is idempotent by nature — sending the same property values multiple times produces the same result. No additional deduplication logic is required for the audit publish push.

---

## Inbound — Webhook Processing

### Trigger

HubSpot sends a POST to `/webhooks/hubspot` when subscribed company properties change or when specific CRM events occur in the tenant's HubSpot portal.

### Authentication: HMAC-SHA256

Every inbound webhook request from HubSpot includes an `X-HubSpot-Signature` header containing the HMAC-SHA256 of the raw request body, computed using the tenant's HubSpot private app client secret.

**Validation algorithm:**
```
hmac = HMAC-SHA256(key=hubspot_client_secret, message=raw_request_body_bytes)
expected_signature = hex(hmac)
received_signature = request.headers["X-HubSpot-Signature"]
valid = ConstantTimeEquals(expected_signature, received_signature)
```

**On validation failure:**
- Return `HTTP 401` with body `{ "error": "INVALID_SIGNATURE" }`
- Log at `Warning` level: `"HubSpot webhook signature validation failed"` — log `tenant_id` (extracted from path or query param) and request timestamp only. Do not log request body.
- Do not process payload
- Do not enqueue event

**On validation success:**
- Return `HTTP 200 OK` immediately (before processing the event body)
- Enqueue to background Channel worker

**Signature version:** HubSpot Webhooks API v3 uses `X-HubSpot-Signature-v3` with the timestamp-signed variant. The system supports both `v1` (body only) and `v3` (method+url+body+timestamp). Default: enforce `v3` if `X-HubSpot-Signature-Version: v3` header is present, otherwise fall back to `v1`.

### Background Processing — Channel Worker

Inbound events are handed off to a `Channel<HubSpotEvent>` (bounded, capacity 500). A singleton background `IHostedService` worker dequeues and processes events sequentially.

**Deduplication:** Events are deduplicated by composite key `(portalId + subscriptionId + occurredAt)` checked against `hubspot_sync_log`. If the key already exists, the event is silently discarded.

### HubSpotEvent Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "HubSpotEvent",
  "type": "object",
  "required": ["event_type", "object_type", "object_id", "occurred_at"],
  "properties": {
    "event_type": {
      "type": "string",
      "description": "HubSpot event type (e.g., 'company.propertyChange', 'company.creation')"
    },
    "object_type": {
      "type": "string",
      "enum": ["company", "contact", "deal"],
      "description": "HubSpot CRM object type. Only 'company' events are processed in v1."
    },
    "object_id": {
      "type": "string",
      "description": "HubSpot CRM object ID (hs_object_id). Maps to clients.hubspot_company_id."
    },
    "portal_id": {
      "type": "string",
      "description": "HubSpot portal ID. Used to identify the tenant."
    },
    "subscription_id": {
      "type": "string",
      "description": "HubSpot subscription ID. Used for deduplication."
    },
    "changed_properties": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["property_name", "new_value"],
        "properties": {
          "property_name": { "type": "string" },
          "new_value":     { "type": "string" },
          "old_value":     { "type": "string" }
        }
      },
      "description": "List of changed HubSpot properties and their new/old values."
    },
    "occurred_at": {
      "type": "string",
      "format": "date-time",
      "description": "ISO 8601 timestamp from HubSpot when the event occurred."
    },
    "received_at": {
      "type": "string",
      "format": "date-time",
      "description": "ISO 8601 timestamp set by our system when the webhook was received."
    }
  },
  "additionalProperties": true
}
```

### Which Incoming Changes We Process

In v1, only `company` object events from HubSpot are processed. Within company events, we sync back the following property changes:

| HubSpot Property Changed | Action in Six-to-Fix |
|---|---|
| `name` | Update `clients.name` and `clients.updated_at` where `clients.hubspot_company_id = object_id` |
| `domain` | Update `clients.company_domain`. Log if domain changed (may affect deduplication). |
| `hs_is_deleted = true` (company deleted) | Set `clients.hubspot_sync_status = 'failed'`, log warning. Do NOT delete client record — data is preserved. |

Properties we **do not** sync back (platform is authoritative for these):
- `strategicglue_*` properties — these are set by us; changes from HubSpot are ignored
- `industry`, `numberofemployees`, `annualrevenue` — platform data is authoritative

### Retry Strategy

HubSpot webhooks are managed by HubSpot's delivery system:
- HubSpot retries delivery up to **10 times** with exponential backoff (over ~24 hours) if we do not return `HTTP 200`
- **Our strategy:** Return `HTTP 200` immediately after HMAC validation passes — always. Processing failures (e.g., database errors during event processing) are our responsibility, not HubSpot's.
- If the background Channel worker fails to process an event (e.g., DB is down), the event is logged at `Error` level with the full `HubSpotEvent` payload for manual replay. There is no automatic retry within our application — the assumption is that HubSpot's delivery guarantee covers the network layer and our idempotency key covers accidental duplicate delivery.

### Failure Semantics

HubSpot webhook processing is non-blocking and non-fatal:
- A failure to process an inbound webhook does not affect audit runs, scoring, or any core platform function
- Inbound webhook failures are surfaced in the Tenant Admin Panel under the HubSpot integration status section
- `hubspot_sync_log` records all processed events and their outcome for audit trail purposes

---

## HubSpot Sync Log — `hubspot_sync_log`

The `hubspot_sync_log` table (see data-dictionary) records all inbound and outbound HubSpot events for deduplication and audit trail:

| Column | Description |
|---|---|
| `portal_id` | HubSpot portal ID |
| `subscription_id` | HubSpot subscription ID (inbound) or `null` (outbound) |
| `occurred_at` | Event timestamp from HubSpot |
| `direction` | `'inbound'` or `'outbound'` |
| `event_type` | Event type string |
| `object_id` | HubSpot object ID |
| `outcome` | `'processed'`, `'duplicate'`, `'validation_failed'`, `'processing_failed'` |
| `error_detail` | Error message if outcome is a failure |
| `created_at` | Row insertion timestamp |

**Deduplication key:** `(portal_id, subscription_id, occurred_at)` — unique constraint enforced at database level.
