# Decision: Polly Resilience Pipeline Configuration — Locked Values

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Locked — Architectural Commitment  
**Scope:** All Azure OpenAI API calls within the Six-to-Fix platform  

---

## Summary

Every Azure OpenAI Service call in this system passes through a single Polly resilience pipeline composed of three policies in order: Timeout → Retry → Circuit Breaker. These values are final architectural commitments, not estimates. Changing them requires a team decision and a new entry in this file.

---

## Pipeline Composition Order

```
Request
  │
  ▼
┌─────────────────────────────────────┐
│  1. Timeout Policy (60s pessimistic) │  ← outermost: enforces total call time limit
└──────────────────┬──────────────────┘
                   │
                   ▼
┌─────────────────────────────────────┐
│  2. Retry Policy (3 total attempts) │  ← handles transient 429/5xx failures
└──────────────────┬──────────────────┘
                   │
                   ▼
┌─────────────────────────────────────┐
│  3. Circuit Breaker (50% / 60s)     │  ← protects against sustained failures
└──────────────────┬──────────────────┘
                   │
                   ▼
            Azure OpenAI API
```

The timeout wraps the retry which wraps the circuit breaker. This means the 60-second timeout is the absolute wall-clock limit for the entire retry sequence — not per individual attempt.

---

## Policy 1: Timeout

| Parameter | Value |
|---|---|
| Strategy | `TimeoutStrategy.Pessimistic` |
| Duration | **60 seconds** |
| Scope | Wraps the full retry sequence (all attempts combined must complete within 60s) |

**On timeout:** `TimeoutRejectedException` is thrown.

**Mapping:**
- `TimeoutRejectedException` → `SkillRun.failure_reason = 'AI_TIMEOUT'`
- `SkillRun.status = 'failed'`
- Timeout counts as a failure toward the circuit breaker's failure ratio
- Timeout is NOT retried — the retry policy does not catch `TimeoutRejectedException`

**Rationale for Pessimistic strategy:** Pessimistic timeout cancels the underlying task immediately rather than waiting for it to cooperatively check a cancellation token. This is required because `HttpClient`-based calls may not honor cooperative cancellation if the Azure OpenAI SDK does not propagate it.

---

## Policy 2: Retry

| Parameter | Value |
|---|---|
| Total Attempts | **3** (initial attempt + 2 retries) |
| Backoff Strategy | Exponential with jitter |
| Backoff Delays | Attempt 1 (initial): immediate. Attempt 2: ~2 seconds. Attempt 3: ~4 seconds. |
| Base Delay | 2 seconds |
| Multiplier | 2× |
| Jitter | ±20% of computed delay to avoid thundering herd |
| Retry On | `HttpRequestException`, HTTP 429 (Too Many Requests), HTTP 5xx (500, 502, 503, 504) |

**Clarification on attempt count:** 3 total attempts means the initial call plus 2 additional retry attempts. The initial attempt is attempt 1. If it fails with a retriable status, retry attempt 1 fires (attempt 2 overall). If that fails, retry attempt 2 fires (attempt 3 overall). A 4th call is never made.

**Do NOT retry:**
- `HTTP 400 Bad Request` — malformed prompt; retrying will produce the same result
- `HTTP 422 Unprocessable Entity` — this is the Azure OpenAI response when the request is structurally valid but cannot be processed (treated equivalent to schema validation failure path — do not retry)
- `TimeoutRejectedException` — already handled at the timeout layer
- `BrokenCircuitException` — circuit is open; do not retry
- Schema validation failure (application-level, post-response) — the response was received but invalid; retrying the same prompt will likely fail again

**On max retries exceeded:** `MaxRetryAttemptsExceededException` is thrown.

**Mapping:**
- `MaxRetryAttemptsExceededException` → `SkillRun.failure_reason = 'MAX_RETRIES_EXCEEDED'`
- `SkillRun.status = 'failed'`

---

## Policy 3: Circuit Breaker

| Parameter | Value |
|---|---|
| Implementation | `AdvancedCircuitBreaker` (ratio-based, not count-based) |
| Failure Ratio Threshold | **0.5 (50%)** — circuit opens when ≥50% of calls in the sampling window fail |
| Sampling Duration | **60 seconds** — the rolling window over which the failure ratio is calculated |
| Minimum Throughput | **3 calls** — the circuit cannot open unless at least 3 calls have been made in the sampling window |
| Break Duration | **60 seconds** — circuit remains open (fast-failing all calls) for 60 seconds before transitioning to half-open |
| Half-Open Probe Count | **1** — one test call is allowed through in the half-open state to assess recovery |

**State machine:**
```
Closed (normal)
  │ failure ratio ≥ 50% AND throughput ≥ 3 in 60s window
  ▼
Open (fast-failing) ←── 60 second break duration ──► Half-Open (probe)
                                                          │
                                           probe succeeds ▼  probe fails
                                                        Closed         Open (reset break)
```

**On circuit open:** `BrokenCircuitException` is thrown immediately without making an HTTP call.

**Mapping:**
- `BrokenCircuitException` → `SkillRun.failure_reason = 'CIRCUIT_OPEN'`
- `SkillRun.status = 'failed'`
- SignalR: No dedicated `circuit-open` event. The `skill-failed` event fires with `failureReason = 'CIRCUIT_OPEN'`.

**Logging on state transitions:**
- Circuit Opens: `Warning` — `"Polly circuit breaker opened for Azure OpenAI. Break duration: 60s."`
- Circuit Half-Opens: `Warning` — `"Polly circuit breaker half-open. Probe call in flight."`
- Circuit Closes: `Information` — `"Polly circuit breaker closed. Normal operation resumed."`

**Scope:** The circuit breaker state is **application-scoped**. The `ResiliencePipeline<HttpResponseMessage>` is registered as a singleton in the DI container. All skill calls across all concurrent audit runs share the same circuit breaker state. If Azure OpenAI is down for one run, it fast-fails for all runs until the break duration expires.

---

## DI Registration

```csharp
// Registered as singleton — shared across all AI calls
builder.Services.AddResiliencePipeline<string, HttpResponseMessage>(
    "azure-openai-pipeline",
    pipeline =>
    {
        pipeline
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(60),
                Strategy = TimeoutStrategy.Pessimistic
            })
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 2, // 2 retries = 3 total attempts
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                    .HandleResult(r => (int)r.StatusCode >= 500)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(60)
            });
    });
```

---

## Schema Validation Failure Handling

Schema validation failure is an **application-layer concern** that occurs after a successful HTTP response from Azure OpenAI. The Polly pipeline has already completed successfully when the schema validation check runs.

**Sequence:**
1. Azure OpenAI returns `HTTP 200` with a JSON response body
2. Polly pipeline: success (no retry, no circuit event)
3. `SkillRunner` deserializes the response and validates against the skill's output JSON Schema
4. Validation fails: the response does not conform to the schema

**Outcomes:**
- `SkillRun.status = 'failed'`
- `SkillRun.failure_reason = 'SCHEMA_VALIDATION_FAILURE'`
- `SkillRun.error_code = 'SCHEMA_VALIDATION_FAILURE'`
- `SkillRun.raw_ai_response` = the raw (non-conforming) response, stored for debugging, PII-scrubbed
- `HTTP 502 Bad Gateway` returned to the caller
- The skill chain is aborted: no subsequent skills execute
- **No retry** — schema failures are deterministic. The same prompt + same schema will fail again.
- Schema validation failures do NOT count toward the Polly circuit breaker failure ratio (they are not HTTP failures — the HTTP call succeeded)

---

## Failure Code Summary

| Failure Code | Polly Policy | HTTP Response | Retried? | Chain Continues? |
|---|---|---|---|---|
| `AI_TIMEOUT` | Timeout (60s) | `HTTP 502` | No | No |
| `MAX_RETRIES_EXCEEDED` | Retry (3 attempts) | `HTTP 502` | N/A | No |
| `CIRCUIT_OPEN` | Circuit Breaker | `HTTP 503` + `Retry-After: 60` | No | No |
| `SCHEMA_VALIDATION_FAILURE` | None (app-layer) | `HTTP 502` | No | No |
