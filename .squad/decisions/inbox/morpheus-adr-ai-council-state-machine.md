# ADR: AI Council State Machine & Skill Chain Interaction Pattern

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  
**Supersedes:** —  

---

## Context

The StrategicGlue Six-to-Fix audit pipeline has three automated subsystems that interact in sequence:

1. **Skill Chain** — five sequential AI skills, executed by `SkillRunner` under `AuditOrchestrator`'s coordination
2. **Policy Engine** — five stateless rules that evaluate skill outputs and produce flags
3. **AI Council** — three AI personas that deliberate on triggered categories

The interaction pattern between these three must be fully explicit before any code is written. Ambiguities in sequencing, state transitions, and failure modes cause bugs that are extremely hard to debug in a live multi-tenant system.

The key questions to resolve:
- When does PolicyEngine run? (per-skill or post-chain?)
- Is Council deliberation synchronous or asynchronous within the audit run?
- What aborts the chain vs. what continues it?
- What is the exact state sequence of an `AuditRun`?

---

## Decision

### 1. Skill Sequencing via AuditOrchestrator + SkillRunner

`AuditOrchestrator` is the single coordinator for an audit run. It calls `SkillRunner.ExecuteAsync(skill, auditRunId)` for each of the five skills **sequentially** — the output of skill N is available as input context for skill N+1. Parallel execution is not permitted: skills declare `depends_on` in their YAML frontmatter, and the current chain is strictly sequential.

```
AuditOrchestrator.RunAsync(auditRunId)
  ├─ SkillRunner.ExecuteAsync("6tofix-scorecard-rubric", auditRunId)        → SkillRunResult
  ├─ SkillRunner.ExecuteAsync("systems-maturity-scoring", auditRunId)       → SkillRunResult
  ├─ SkillRunner.ExecuteAsync("gap-analysis-template", auditRunId)          → SkillRunResult
  ├─ SkillRunner.ExecuteAsync("value-driver-rating", auditRunId)            → SkillRunResult
  └─ SkillRunner.ExecuteAsync("derive-tier", auditRunId)                    → SkillRunResult
```

`SkillRunner.ExecuteAsync` returns a `SkillRunResult` (sealed record):

```csharp
public sealed record SkillRunResult(
    Guid SkillRunId,
    string SkillId,
    SkillRunStatus Status,          // Succeeded | Failed
    string? OutputJson,             // null if failed
    string? FailureReason,          // null if succeeded
    SkillFailureKind? FailureKind,  // SchemaValidation | Timeout | CircuitOpen | ApiError
    TimeSpan Duration
);
```

### 2. PolicyEngine Evaluation: Per-Skill, After Each Skill Completes

PolicyEngine evaluates **after each individual skill completes**, not post-chain. This is the correct sequencing because:
- Trigger flags on a skill's output must be known before the next skill runs (future skill variants may branch based on prior council adjudication)
- Warnings visible during run provide real-time context to the SignalR-watching auditor
- Post-chain evaluation would delay council escalation until all five skills complete

```
SkillRunner.ExecuteAsync("6tofix-scorecard-rubric") → SkillRunResult(Succeeded)
  └─ PolicyEngine.Evaluate(categoryPayloads[]) → IReadOnlyList<PolicyFlag>
       └─ Flags persisted to policy_flags table
       └─ If any Trigger flags → CouncilRunner.RunAsync(triggeredCategories)
            └─ CouncilDecision persisted
            └─ CategoryPayload updated with council-adjusted scores

SkillRunner.ExecuteAsync("systems-maturity-scoring") → next...
```

### 3. Council Escalation: Synchronous Within the Audit Run

When PolicyEngine produces one or more `Trigger`-severity flags, `AuditOrchestrator` calls `CouncilRunner.RunAsync(...)` **synchronously within the same audit run execution** — it does not queue the council deliberation for later. The audit run does not transition to `completed` until all triggered categories have received a `CouncilDecision`.

Rationale:
- The final `AuditRun` state must reflect adjudicated scores, not raw AI scores
- Asynchronous council introduces a "partially adjudicated" intermediate state that complicates the reviewer queue significantly
- Council calls are AI calls subject to the same Polly resilience pipeline — latency is acceptable within the overall audit run duration

`Warning`-severity flags do NOT trigger council escalation. They are persisted to `policy_flags` and surfaced in the reviewer queue as informational context.

### 4. CouncilRunner Input Contract

```csharp
public sealed record CouncilInput(
    Guid AuditRunId,
    Guid TenantId,
    string CategoryId,           // e.g., "brand", "customer"
    CategoryPayload Payload,     // The skill's output for this category
    IReadOnlyList<PolicyFlag> TriggerFlags  // The Trigger-severity flags that caused escalation
);
```

`CouncilRunner.RunAsync(IReadOnlyList<CouncilInput> inputs)` — accepts all triggered categories from a single skill evaluation in one call. Runs the three-persona deliberation for each category in sequence.

### 5. CouncilDecision Structure

```csharp
public sealed record CouncilDecision(
    Guid CouncilDecisionId,
    Guid AuditRunId,
    Guid TenantId,
    string CategoryId,
    CouncilDecisionType DecisionType,   // Confirmed | Adjusted
    decimal? AdjustedActivityScore,     // null if Confirmed
    string? AdjustedDocumentedStrategy, // null if Confirmed
    string AdvocateRationale,
    string SkepticRationale,
    string MethodJudgeRationale,
    string FinalRationale,
    TimeSpan Duration,
    DateTimeOffset CreatedAt
);
```

When `DecisionType = Adjusted`, `AuditOrchestrator` updates the `CategoryPayload` with the adjusted scores before passing control to the next skill. The original scores are preserved in the `category_result_versions` append-only table.

### 6. Skill Failure Abort Logic

| Failure Kind | Chain Behavior |
|---|---|
| `SchemaValidation` | **Immediate abort.** `AuditRun` transitions to `failed`. `run-failed` SignalR event fired. No retry. |
| `Timeout` (post-Polly exhaustion) | **Immediate abort.** Same as above. |
| `CircuitOpen` | **Immediate abort.** Same as above. |
| `ApiError` (post-Polly exhaustion) | **Immediate abort.** Same as above. |

There is no partial-success continuation. If skill 3 fails, skills 4 and 5 do not run. The `AuditRun` is marked `failed` with the `failed_skill_id` recorded.

### 7. State Machine: AuditRun States

```
                      ┌─────────────────────────────────────────────────────────────┐
                      │                  AuditRun State Machine                     │
                      └─────────────────────────────────────────────────────────────┘

  [created]
     │
     │ AuditOrchestrator.RunAsync() called
     ▼
  [running]
     │
     │ for each skill (1..5):
     │   ├─ SkillRunner executes skill
     │   │     ├─ [skill-started SignalR event]
     │   │     │
     │   │     ├── SUCCESS ──────────────────────────────────────────────┐
     │   │     │   PolicyEngine.Evaluate()                              │
     │   │     │   [skill-completed SignalR event]                       │
     │   │     │   if Trigger flags:                                     │
     │   │     │     [council-started SignalR event]                     │
     │   │     │     CouncilRunner.RunAsync()                            │
     │   │     │     [council-completed SignalR event]                   │
     │   │     │     CategoryPayload updated                             │
     │   │     │   continue to next skill ──────────────────────────────┘
     │   │     │
     │   │     └── FAILURE ─────────────────────────────────────────────┐
     │   │         [skill-failed SignalR event]                         │
     │   │         [run-failed SignalR event]                           │
     │   │         AuditRun.Status → failed                            │
     │   │         ABORT                                                │
     │   │                                                              ▼
     │   │                                                          [failed] ◄──────────┐
     │   │                                                                              │
     │   └─ all 5 skills succeeded                                                     │
     │                                                                                 │
     │ AuditRun.Status → completed                                                     │
     │ [run-completed SignalR event]                                                   │
     ▼                                                                                 │
  [completed]                                                                          │
     │                                                                                 │
     │ Reviewer Queue: approve / edit / rerun / escalate                               │
     │   if rerun: a new AuditRun is created → [created] ─────────────────────────────┘
     │   if 3 rejections in 24h: HTTP 409 REVIEWER_REJECTION_LOCKOUT
     │
     │ All 6 category results approved
     ▼
  [publishing]
     │
     │ Publisher.PublishAsync()
     │   category_result_versions append
     │   audit marked published
     │
     ▼
  [published]
```

### 8. SignalR Event Sequence — Full Happy-Path Audit Run

```
1. Client joins group: JoinRun(auditRunId)
2. → skill-started        { auditRunId, skillId:"6tofix-scorecard-rubric",   skillIndex:1, totalSkills:5 }
3. → skill-completed      { auditRunId, skillId:"6tofix-scorecard-rubric",   skillIndex:1, durationMs, policyFlags:[], requiresCouncil:false }
4. → skill-started        { auditRunId, skillId:"systems-maturity-scoring",  skillIndex:2, totalSkills:5 }
5. → skill-completed      { auditRunId, skillId:"systems-maturity-scoring",  skillIndex:2, durationMs, policyFlags:["BENCHMARK_OUTLIER"], requiresCouncil:true }
6. → council-started      { auditRunId, category:"brand", triggeredBy:["BENCHMARK_OUTLIER"] }
7. → council-completed    { auditRunId, category:"brand", adjustedScore:6.5, durationMs }
8. → skill-started        { auditRunId, skillId:"gap-analysis-template",     skillIndex:3, totalSkills:5 }
9. → skill-completed      { auditRunId, skillId:"gap-analysis-template",     skillIndex:3, durationMs, policyFlags:[], requiresCouncil:false }
10. → skill-started       { auditRunId, skillId:"value-driver-rating",       skillIndex:4, totalSkills:5 }
11. → skill-completed     { auditRunId, skillId:"value-driver-rating",       skillIndex:4, durationMs, policyFlags:[], requiresCouncil:false }
12. → skill-started       { auditRunId, skillId:"derive-tier",               skillIndex:5, totalSkills:5 }
13. → skill-completed     { auditRunId, skillId:"derive-tier",               skillIndex:5, durationMs, policyFlags:[], requiresCouncil:false }
14. → run-completed       { auditRunId, totalDurationMs, categoryCount:6, requiresReviewCount:6 }
```

---

## Consequences

### AuditOrchestrator Design

`AuditOrchestrator` is the orchestration controller — it owns the top-level `try/catch` for the audit run. On any caught exception from `SkillRunner` or `CouncilRunner`, it transitions the `AuditRun` to `failed`, fires `run-failed` via `IHubContext<AuditRunHub>`, and rethrows to let the caller observe the failure. It does NOT swallow exceptions silently.

### SkillRunner Return Types

`SkillRunner` never throws for expected AI failures (timeout, schema validation, circuit open). It catches these and returns `SkillRunResult` with `Status = Failed`. This means `AuditOrchestrator` inspects the result, not a catch block, to decide whether to abort.

Unexpected infrastructure exceptions (e.g., `DbContext` failure, `OutOfMemoryException`) propagate as normal exceptions and are caught by `AuditOrchestrator`'s top-level handler.

### CouncilRunner Input/Output Contracts

`CouncilRunner` accepts a batch of triggered categories (may be from one skill's evaluation). It runs each through the three-persona sequence. The output is `IReadOnlyList<CouncilDecision>`. `AuditOrchestrator` applies decisions to `CategoryPayload` records via the `DbContext` before proceeding to the next skill.

### Policy Engine Statelessness

Because PolicyEngine is Singleton and pure-functional, it can be called from `AuditOrchestrator` (Scoped) without lifetime concerns. The Scoped service can safely consume the Singleton.

---
