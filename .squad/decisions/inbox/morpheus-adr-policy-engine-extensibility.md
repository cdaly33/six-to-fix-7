# ADR: Policy Engine Extensibility Contract

**Status:** Accepted  
**Date:** 2026-05-10  
**Author:** Morpheus (Lead & Architect)  
**Supersedes:** —  

---

## Context

The Policy Engine currently has five rules. The domain will evolve: new rules will be added as calibration data accumulates and new quality signals are identified. The engine must be designed to accommodate new rules without modifying existing code (Open/Closed Principle). The five current rules and their conditions must also be explicitly specified so implementors do not interpret them from prose.

---

## Decision

### IPolicyRule Interface Contract

```csharp
/// <summary>
/// A single, stateless quality rule evaluated against a category payload.
/// Implementations must be thread-safe. PolicyEngine is Singleton.
/// </summary>
public interface IPolicyRule
{
    /// <summary>
    /// The machine-readable name of this rule.
    /// Must be UPPER_SNAKE_CASE and unique across all rules.
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// Evaluates the rule against the given category payload.
    /// Returns null if the rule does not fire for this payload.
    /// </summary>
    PolicyFlag? Evaluate(CategoryPayload payload, AuditContext context);
}
```

**Input — `CategoryPayload`:** The structured output of a skill run for a single marketing category. Contains:
- `CategoryId` (string: "brand", "customer", "offering", "communications", "sales", "management")
- `ActivityScore` (decimal 0–10)
- `DocumentedStrategy` (string: "current", "partial", "none")
- `ConfidenceScore` (decimal 0–1)
- `Evidence` (IReadOnlyList<string>)
- `Narrative` (string)

**Input — `AuditContext`:** Cross-category context needed by rules like `BENCHMARK_OUTLIER`:
- `TenantId` (Guid)
- `AuditRunId` (Guid)
- `CategoryMedianScores` (IReadOnlyDictionary<string, decimal>) — pre-loaded industry/tenant benchmark medians, passed in from `AuditOrchestrator`
- `CategoryStdDevs` (IReadOnlyDictionary<string, decimal>) — standard deviations for outlier detection

**Output — `PolicyFlag`:**

```csharp
public sealed record PolicyFlag(
    string RuleName,
    PolicyFlagSeverity Severity,      // Warning | Trigger
    string CategoryId,
    string Detail,                    // Human-readable explanation for the reviewer
    DateTimeOffset EvaluatedAt
);

public enum PolicyFlagSeverity { Warning, Trigger }
```

### Rule Discovery and Registration

Rules are registered **explicitly in DI** — not via reflection scanning. This ensures:
- The registered rule set is deterministic and auditable in `Program.cs`
- No accidental rule activation from a test or dev stub class
- New rules require an explicit code change at the registration site (making the addition intentional and reviewable)

```csharp
// In AddDomainServices() extension
services.AddSingleton<IPolicyRule, LowConfidenceRule>();
services.AddSingleton<IPolicyRule, MissingEvidenceRule>();
services.AddSingleton<IPolicyRule, BenchmarkOutlierRule>();
services.AddSingleton<IPolicyRule, InsufficientEvidenceRule>();
services.AddSingleton<IPolicyRule, ScoreStrategyMismatchRule>();

// PolicyEngine receives IEnumerable<IPolicyRule> via constructor injection
services.AddSingleton<IPolicyEngine, PolicyEngine>();
```

**Adding a new rule** requires:
1. Implement `IPolicyRule` in `Domain/PolicyEngine/Rules/`
2. Add `services.AddSingleton<IPolicyRule, NewRule>()` in `AddDomainServices()`
3. No changes to `PolicyEngine` itself

### Rule Execution: Parallel with Isolated Error Handling

`PolicyEngine` runs all rules **in parallel** (via `Task.WhenAll` or `Parallel.ForEach` over the Singleton instances). All rules are stateless and thread-safe. Parallel execution is safe and faster than sequential for potentially growing rule counts.

```csharp
public IReadOnlyList<PolicyFlag> Evaluate(CategoryPayload payload, AuditContext context)
{
    var flags = new ConcurrentBag<PolicyFlag>();

    Parallel.ForEach(_rules, rule =>
    {
        try
        {
            var flag = rule.Evaluate(payload, context);
            if (flag is not null) flags.Add(flag);
        }
        catch (Exception ex)
        {
            // Rule execution failure is non-fatal.
            // Log at ERROR level with rule name and category.
            // Do NOT propagate — a broken rule must not abort a valid audit run.
            _logger.LogError(ex,
                "PolicyRule {RuleName} threw for category {CategoryId} on run {AuditRunId}",
                rule.RuleName, payload.CategoryId, context.AuditRunId);
        }
    });

    return [.. flags];
}
```

A rule that throws produces no flag for that evaluation. The audit run continues. The error is logged at `ERROR` level and visible in Application Insights. A broken rule does not propagate to the caller.

### PolicyFlag Persistence

**All `PolicyFlag` evaluations are recorded to the `policy_flags` table**, including `Warning`-severity flags. Rationale:
- Warnings surfaced in the reviewer queue need a DB-backed source of truth
- CalibrationTracker uses historical policy flag patterns to improve models
- Audit trail requires that every evaluation be recordable

```
policy_flags table columns:
  policy_flag_id   UUID (PK)
  audit_run_id     UUID (FK → audit_runs)
  tenant_id        UUID (FK → tenants)
  skill_run_id     UUID (FK → skill_runs)
  category_id      VARCHAR(50)
  rule_name        VARCHAR(100)
  severity         VARCHAR(20)   -- 'warning' | 'trigger'
  detail           TEXT
  evaluated_at     TIMESTAMPTZ
```

`AuditOrchestrator` persists all returned `PolicyFlag` records to `policy_flags` immediately after `PolicyEngine.Evaluate()` returns, within the same `DbContext` unit of work as the `SkillRun` completion update.

### The Five Current Rules: Exact Conditions

| Rule | Interface Implementation | Input Evaluated | Condition | Severity |
|---|---|---|---|---|
| `LOW_CONFIDENCE` | `LowConfidenceRule` | `CategoryPayload.ConfidenceScore` | `ConfidenceScore < 0.6` | **Trigger** |
| `MISSING_EVIDENCE` | `MissingEvidenceRule` | `CategoryPayload.Evidence` | `Evidence.Count == 0` | **Warning** |
| `BENCHMARK_OUTLIER` | `BenchmarkOutlierRule` | `CategoryPayload.ActivityScore`, `AuditContext.CategoryMedianScores`, `AuditContext.CategoryStdDevs` | `Abs(ActivityScore - median) > (2 * stdDev)` | **Trigger** |
| `INSUFFICIENT_EVIDENCE` | `InsufficientEvidenceRule` | `CategoryPayload.Evidence` | `Evidence.Count > 0 && Evidence.Count < 2` | **Warning** |
| `SCORE_STRATEGY_MISMATCH` | `ScoreStrategyMismatchRule` | `CategoryPayload.ActivityScore`, `CategoryPayload.DocumentedStrategy` | `ActivityScore > 7.0m && DocumentedStrategy == "none"` | **Trigger** |

**Notes on specific rules:**

- `LOW_CONFIDENCE`: The PRD describes this rule as both Warning AND Trigger. The decision is **Trigger only** — it goes directly to council escalation. The informational warning is conveyed by the `Detail` field on the `PolicyFlag` surfaced to the reviewer.
- `BENCHMARK_OUTLIER`: The `AuditContext.CategoryMedianScores` and `AuditContext.CategoryStdDevs` are pre-loaded from the `calibration_deltas` aggregate view or a static benchmark seed. This is loaded by `AuditOrchestrator` before the first skill runs, not by PolicyEngine itself (PolicyEngine is pure functional, no DB access).
- `MISSING_EVIDENCE` vs `INSUFFICIENT_EVIDENCE`: These are separate rules. A payload with 0 items fires `MISSING_EVIDENCE`. A payload with exactly 1 item fires `INSUFFICIENT_EVIDENCE`. A payload with 2+ items fires neither.

---

## Consequences

### Adding New Rules

New rules are added by:
1. Creating a class implementing `IPolicyRule` in `Domain/PolicyEngine/Rules/`
2. Registering it as `AddSingleton<IPolicyRule, NewRule>()` in `AddDomainServices()`

No other code changes required. `PolicyEngine` receives `IEnumerable<IPolicyRule>` and runs whatever is registered.

### Testing Rules

Each `IPolicyRule` implementation is unit-testable in complete isolation:

```csharp
var rule = new LowConfidenceRule();
var flag = rule.Evaluate(payload with { ConfidenceScore = 0.55m }, context);
Assert.Equal("LOW_CONFIDENCE", flag!.RuleName);
Assert.Equal(PolicyFlagSeverity.Trigger, flag.Severity);
```

No DI container, no mocking, no `DbContext` required.

### Tenant-Configurable Rules

Rules are not currently tenant-configurable — all tenants run all five rules with identical thresholds. If tenant-specific thresholds are needed in the future (e.g., a tenant that calibrates `LOW_CONFIDENCE` threshold to 0.7), the `AuditContext` is the correct vehicle for passing tenant-specific configuration to rules. The `IPolicyRule` interface does not need to change.

### Rule Ordering

Because rules run in parallel, no ordering guarantees exist. Rules must not depend on each other's output. If a future rule needs to reference a prior rule's flag, that design is rejected — it introduces coupling. Each rule evaluates independently from `CategoryPayload` and `AuditContext` only.

---
