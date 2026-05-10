# ADR: SignalR Event Contract & Concurrent Audit Ordering

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  
**Supersedes:** —  

---

## Context

The Skill Chain Runner screen (`/audits/{slug}/run/{runId}`) must show real-time progress as each AI skill executes. Multiple audits may run concurrently across different tenants and potentially within the same tenant. SignalR events must be:

- **Ordered within a run:** A client watching a specific audit run must see events in the sequence they fired.
- **Isolated between runs:** Events from audit run A must not appear to clients watching audit run B.
- **Tenant-safe:** A client must not receive events for an audit run belonging to a different tenant.
- **Resilient to Blazor Server reconnects:** The Blazor Server circuit reconnects via SignalR on transient disconnections.

---

## Decision

### Hub Endpoint

```
/hubs/audit-run
```

Registered in `Program.cs`:
```csharp
app.MapHub<AuditRunHub>("/hubs/audit-run");
```

### Authentication

The hub requires authentication. JWT bearer token is passed as query string `access_token` for WebSocket connections (SignalR's standard pattern for WS):

```
wss://app.strategicglue.com/hubs/audit-run?access_token={jwt}
```

The hub is decorated with `[Authorize]`. The JWT bearer handler is configured to read the token from the query string for hub connections:

```csharp
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/audit-run"))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });
```

### Group Key: auditRunId

Each connected client joins a SignalR group keyed by `auditRunId` (UUID string). Server events are sent to the group, not to individual connections. All browser tabs or connections watching the same audit run receive the same events.

**Client → Hub method:**

```csharp
// Client calls this after connecting
public async Task JoinRun(string auditRunId)
```

**Hub validates tenant ownership before joining:**

```csharp
public async Task JoinRun(string auditRunId)
{
    var tenantId = Context.User!.FindFirst("tenant_id")!.Value;
    var runTenantId = await _auditRunRepository.GetTenantIdAsync(Guid.Parse(auditRunId));

    if (runTenantId == null || runTenantId.ToString() != tenantId)
    {
        // Reject silently — do not reveal whether the run exists
        return;
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, auditRunId);
}
```

The hub never sends a "join failed" event — it simply does not add the connection to the group. The client times out naturally.

### Full Event Contract

All events are sent to the `auditRunId` group from `IHubContext<AuditRunHub>` (injected into `AuditOrchestrator` and `CouncilRunner`).

---

#### Event 1: `skill-started`

**Fired by:** `AuditOrchestrator`, immediately before calling `SkillRunner.ExecuteAsync`  
**Fired when:** A skill is about to begin execution

```json
{
  "auditRunId": "uuid",
  "skillId": "6tofix-scorecard-rubric",
  "skillName": "Scorecard Rubric",
  "skillIndex": 1,
  "totalSkills": 5,
  "startedAt": "2026-05-10T15:00:00.000Z"
}
```

---

#### Event 2: `skill-completed`

**Fired by:** `AuditOrchestrator`, after `SkillRunner.ExecuteAsync` returns `Status = Succeeded` AND PolicyEngine evaluation is complete  
**Fired when:** A skill run succeeds and policy flags are resolved

```json
{
  "auditRunId": "uuid",
  "skillId": "6tofix-scorecard-rubric",
  "skillIndex": 1,
  "durationMs": 4200,
  "policyFlags": [
    { "ruleName": "LOW_CONFIDENCE", "severity": "Warning", "categoryId": "brand" }
  ],
  "requiresCouncil": false
}
```

`requiresCouncil` is `true` if any `policyFlags` have `severity = "Trigger"`.

---

#### Event 3: `skill-failed`

**Fired by:** `AuditOrchestrator`, after `SkillRunner.ExecuteAsync` returns `Status = Failed`  
**Fired when:** A skill run fails for any reason

```json
{
  "auditRunId": "uuid",
  "skillId": "6tofix-scorecard-rubric",
  "skillIndex": 1,
  "failureReason": "Schema validation failed: missing required field 'activityScore'",
  "error": "SchemaValidation"
}
```

`error` is one of: `"SchemaValidation"`, `"Timeout"`, `"CircuitOpen"`, `"ApiError"`.

---

#### Event 4: `council-started`

**Fired by:** `AuditOrchestrator`, immediately before calling `CouncilRunner.RunAsync` for a category  
**Fired when:** A category is being escalated to the AI Council

```json
{
  "auditRunId": "uuid",
  "category": "brand",
  "categoryDisplayName": "Brand",
  "triggeredBy": ["BENCHMARK_OUTLIER", "LOW_CONFIDENCE"]
}
```

---

#### Event 5: `council-completed`

**Fired by:** `AuditOrchestrator`, after `CouncilRunner.RunAsync` returns a `CouncilDecision` for a category  
**Fired when:** The AI Council has produced a decision for the category

```json
{
  "auditRunId": "uuid",
  "category": "brand",
  "decisionType": "adjusted",
  "adjustedScore": 6.5,
  "durationMs": 7800
}
```

`decisionType` is `"confirmed"` or `"adjusted"`. `adjustedScore` is `null` if `"confirmed"`.

---

#### Event 6: `run-completed`

**Fired by:** `AuditOrchestrator`, after all 5 skills have succeeded and the `AuditRun` status transitions to `completed`  
**Fired when:** The entire audit run completes successfully

```json
{
  "auditRunId": "uuid",
  "totalDurationMs": 38400,
  "categoryCount": 6,
  "requiresReviewCount": 6,
  "completedAt": "2026-05-10T15:06:24.000Z"
}
```

---

#### Event 7: `run-failed`

**Fired by:** `AuditOrchestrator`, after a skill failure causes the run to abort  
**Fired when:** The audit run is halted due to an unrecoverable failure

```json
{
  "auditRunId": "uuid",
  "failedSkillId": "gap-analysis-template",
  "failedSkillIndex": 3,
  "reason": "SchemaValidation",
  "failedAt": "2026-05-10T15:03:11.000Z"
}
```

---

### Concurrent Audit Isolation

Each audit run has its own unique `auditRunId` (UUID). Events are sent to `Groups.SendAsync(auditRunId, ...)`. Clients join only the group for the run they are watching. SignalR's group implementation ensures complete isolation between runs.

Multiple simultaneous audits — even from the same tenant — are isolated by their distinct `auditRunId` values. No additional coordination is required.

### Message Ordering Guarantee

SignalR over WebSocket delivers messages in order on a single connection. Because all events for a given audit run are sent sequentially from `AuditOrchestrator` (which is inherently sequential — one skill at a time), and are sent from a single server node (Azure App Service with sticky sessions), message ordering is guaranteed.

**No out-of-order delivery scenario exists** in the happy path. However:

- If a client reconnects mid-run, it may have missed events. See reconnect semantics below.
- If the server has multiple nodes (scale-out), SignalR backplane (Azure SignalR Service) is required. In that case, ordering within a single publisher (the `AuditOrchestrator` instance) is preserved because the publisher writes to the backplane sequentially. **Azure App Service sticky sessions are required for Blazor Server circuits** — this ensures a given circuit always hits the same server node, preserving circuit state.

### Replay Semantics on Reconnect

Blazor Server reconnects the SignalR circuit automatically on transient disconnection. On reconnect:

- The circuit is restored on the same server node (sticky sessions).
- The client must call `JoinRun(auditRunId)` again via a Blazor `OnAfterRenderAsync` or `OnInitializedAsync` handler that is aware of reconnection state.
- **Missed events are NOT replayed** — SignalR does not buffer events for disconnected clients. The client must refetch current state via the REST API (`GET /api/audits/{clientSlug}`) or a dedicated `GetRunStatus(auditRunId)` hub method to resync.

**Recovery pattern (client-side):**

```csharp
// On reconnect, fetch current run state and re-render
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender || _circuit.IsReconnected)
    {
        var state = await AuditOrchestrator.GetRunStateAsync(auditRunId);
        await hubConnection.InvokeAsync("JoinRun", auditRunId);
        RenderFromState(state);
    }
}
```

The `GetRunStateAsync` returns the persisted `AuditRun` state from the DB, which always reflects the latest true state.

### Tenant Validation in Hub

As described above, `JoinRun` validates that the calling user's `tenant_id` claim matches the `tenant_id` of the specified `auditRunId`. If validation fails, the connection is not added to the group and no error is sent.

Additionally, `AuditRunHub` is decorated with `[Authorize(Policy = "TenantUser")]` to ensure only authenticated tenant users can connect.

---

## Consequences

### Hub Registration in Program.cs

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

app.MapHub<AuditRunHub>("/hubs/audit-run");
```

### Authorization Policy on Hub

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantUser", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("tenant_id"));
});
```

### Client Reconnect Strategy

Blazor Server applications must implement `OnCircuitClosed` and `OnCircuitOpened` lifecycle hooks to detect reconnection and re-join the appropriate run group. The `HubConnection` in the Blazor component (if using a client-side hub for non-circuit communication) must implement exponential backoff reconnect.

### Scale-Out Consideration

If the application scales beyond one instance, Azure SignalR Service must replace the in-process SignalR hub. This is a configuration change (`builder.Services.AddSignalR().AddAzureSignalR(connectionString)`) and does not change the event contract or the client code.

---
