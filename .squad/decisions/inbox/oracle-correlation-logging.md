# Decision: Correlation ID & Logging Strategy

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Locked — Architectural Commitment  
**Scope:** All structured logging across the Six-to-Fix platform  

---

## Summary

This document defines the cross-cutting correlation ID propagation strategy and structured logging conventions for the Six-to-Fix platform. These rules apply to all components — Blazor services, SkillRunner, CouncilRunner, PolicyEngine, HubSpot client, and all background workers.

**Non-negotiable constraint:** No PII in any log payload, ever. PII includes user names, email addresses, company names, client data content, or any AI-generated narrative text. Use IDs only.

---

## 1. Correlation ID Propagation

### Generation

- **Per HTTP request:** If the incoming request includes an `X-Correlation-ID` header with a valid GUID, propagate that value as the correlation ID for all downstream operations in that request.
- **If header is absent or invalid:** Generate a new `Guid.NewGuid()` in the middleware and set it as the correlation ID.
- **Response header:** Every HTTP response must include `X-Correlation-ID` with the (propagated or generated) correlation ID.

### Middleware Implementation

A dedicated `CorrelationIdMiddleware` is registered early in the ASP.NET Core pipeline (before routing, before auth). It:
1. Reads `X-Correlation-ID` from the request header
2. Generates a GUID if absent
3. Stores the correlation ID in `IHttpContextAccessor.HttpContext.Items["CorrelationId"]`
4. Sets it on the response header `X-Correlation-ID`
5. Begins an `ILogger` scope: `using (logger.BeginScope(new { CorrelationId = correlationId }))` — this causes the correlation ID to be included in all log entries produced within the scope

### Flow Through Services

```
HTTP Request (X-Correlation-ID: abc-123)
  │
  ▼
CorrelationIdMiddleware → stores "abc-123" in HttpContext.Items
  │
  ▼
AuditOrchestrator → receives CorrelationId via IHttpContextAccessor
  │
  ▼
SkillRunner → passes CorrelationId + AuditRunId to every AI call
  │
  ▼
IAIClient → logs CorrelationId + AuditRunId + SkillName on every request/response
  │
  ▼
All log entries in this request carry: { CorrelationId: "abc-123", AuditRunId: "uuid", SkillName: "..." }
```

### Background Workers

Background workers (HubSpot Channel worker, any `IHostedService`) do not have an HTTP context. They generate their own correlation ID per dequeued event:
- `CorrelationId = Guid.NewGuid().ToString()`
- Set via `ILogger.BeginScope` at the start of each event processing loop
- The generated correlation ID is included in all logs for that event's processing lifetime

---

## 2. Log Levels

| Level | When to Use | Examples |
|---|---|---|
| `Trace` | Highly verbose, development only | Raw prompt fragments (NEVER in production) |
| `Debug` | Diagnostic details not needed in production | Polly retry attempt number, token counts |
| `Information` | Normal operational events | Skill started, skill completed, council completed |
| `Warning` | Non-fatal anomalies requiring attention | Policy flag raised, circuit breaker half-open, HubSpot sync delayed |
| `Error` | Failures requiring investigation | Schema validation failure, circuit breaker open, HubSpot sync failed, DB error |
| `Critical` | System-wide failures | Application startup failure, unhandled exception in background worker |

### Specific Log Events

| Event | Level | Message Template |
|---|---|---|
| Skill execution started | `Information` | `"Skill {SkillName} started for {AuditRunId}"` |
| Skill execution completed | `Information` | `"Skill {SkillName} completed for {AuditRunId} in {LatencyMs}ms ({TokensUsed} tokens)"` |
| Skill execution failed | `Error` | `"Skill {SkillName} failed for {AuditRunId}: {FailureReason}"` |
| Policy flag raised | `Warning` | `"Policy flag {PolicyRule} ({Severity}) raised for category {Category} in {AuditRunId}"` |
| Council deliberation started | `Information` | `"Council deliberation started for {AuditRunId} ({CategoryCount} categories)"` |
| Council persona started | `Information` | `"Council {Persona} deliberation started for {Category} in {AuditRunId}"` |
| Council persona completed | `Information` | `"Council {Persona} deliberation completed for {Category} in {AuditRunId} ({LatencyMs}ms)"` |
| Council persona schema failed | `Error` | `"Council {Persona} schema validation failed for {Category} in {AuditRunId}"` |
| Council deliberation completed | `Information` | `"Council deliberation completed for {AuditRunId}: {DecisionType}"` |
| Circuit breaker opened | `Warning` | `"Polly circuit breaker opened. Break duration: {BreakDurationSeconds}s"` |
| Circuit breaker half-open | `Warning` | `"Polly circuit breaker half-open. Probe call in flight."` |
| Circuit breaker closed | `Information` | `"Polly circuit breaker closed. Normal operation resumed."` |
| HubSpot upsert succeeded | `Information` | `"HubSpot company upsert succeeded for {ClientId} (tenant: {TenantId})"` |
| HubSpot upsert failed | `Error` | `"HubSpot company upsert failed for {ClientId} (tenant: {TenantId}): HTTP {StatusCode}"` |
| HubSpot webhook signature failed | `Warning` | `"HubSpot webhook signature validation failed (tenant: {TenantId})"` |
| Schema validation failure | `Error` | `"Schema validation failure for skill {SkillName} in {AuditRunId}: {ValidationErrors}"` |
| Document indexed | `Information` | `"Document {DocumentId} indexed for tenant {TenantId} in {LatencyMs}ms"` |
| Document index failed | `Error` | `"Document {DocumentId} index failed for tenant {TenantId}: {ErrorDetail}"` |

---

## 3. Structured Logging Requirements

### Format: Structured Parameters Only

All log calls **must** use structured logging message templates. String interpolation is **prohibited** for log messages.

**Correct:**
```csharp
logger.LogInformation("Skill {SkillName} completed for {AuditRunId} in {LatencyMs}ms",
    skillName, auditRunId, latencyMs);
```

**Prohibited:**
```csharp
// DO NOT DO THIS
logger.LogInformation($"Skill {skillName} completed for {auditRunId}");
logger.LogInformation("Skill " + skillName + " completed.");
```

Structured parameters enable Application Insights to index values as searchable dimensions. String interpolation destroys this capability.

### Required Context in Every AI Call Log

All log entries produced during an AI call (within `IAIClient`) must include at a minimum:
- `CorrelationId` — from the active logger scope
- `AuditRunId` — passed to every AI call
- `SkillName` — the skill being executed
- `TenantId` — for multi-tenant log filtering

These are set via `ILogger.BeginScope` at the start of each AI call, not repeated on every individual log call.

### Prohibited Content in Log Payloads

The following are **never logged**, at any log level, in any log sink (Application Insights, Log Analytics):

| Prohibited | Reason |
|---|---|
| User display names | PII |
| Email addresses | PII |
| Company names | Client-confidential |
| AI-generated narrative text | Too large + potential PII |
| Raw AI prompt content | Too large + potential client data |
| Raw AI response content | Too large + potential PII |
| Document content/excerpts | Client-confidential |
| HubSpot webhook request body | May contain PII |

**Use IDs only.** Refer to users by `user_id` (UUID), clients by `client_id`, tenants by `tenant_id`, documents by `document_id`.

Exception: `SkillRun.raw_ai_response` is stored in the database (not in logs) for debugging. The `prompt_used` and `raw_ai_response` columns exist in the `skill_runs` table with the expectation that they are access-controlled at the database level and not streamed to log aggregation.

---

## 4. Log Sinks and Configuration

| Sink | Environment | Level Filter |
|---|---|---|
| Application Insights | Production + Staging | `Warning` and above by default; `Information` for audit-critical events (skill-started, skill-completed, council-*) |
| Console (structured JSON) | Development | `Debug` and above |
| Log Analytics Workspace | Production | All `Error` and above, forwarded from Application Insights |

### `appsettings.json` Baseline

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Polly": "Warning"
    }
  },
  "ApplicationInsights": {
    "LogLevel": {
      "Default": "Warning",
      "StrategicGlue": "Information"
    }
  }
}
```

The `StrategicGlue` namespace prefix covers all application-owned classes, ensuring operational `Information` events reach Application Insights while framework noise is suppressed.

---

## 5. Correlation ID in Azure Application Insights

ASP.NET Core's built-in `Activity`-based distributed tracing integrates with Application Insights. The `X-Trace-ID` response header is set to the `Activity.Current?.Id` value, which Application Insights uses to correlate telemetry.

**Dual correlation:**
- `CorrelationId` — application-level, human-readable, propagated via `X-Correlation-ID` header
- `TraceId` — W3C Trace Context / Application Insights operation ID, propagated via `traceparent` header

Both are logged as structured properties on every request. Both appear in the `X-Correlation-ID` and `X-Trace-ID` response headers respectively (per api-spec.md requirements).
