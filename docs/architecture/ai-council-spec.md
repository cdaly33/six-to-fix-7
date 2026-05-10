# AI Council Deliberation Specification

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Locked — Architectural Commitment  

---

## Overview

The AI Council is a structured multi-persona deliberation system invoked when the Policy Engine flags a category with `Trigger`-severity policy rules. Three AI personas (Advocate, Skeptic, Method Judge) deliberate sequentially on each flagged category and produce a `CouncilDecision` that either confirms or adjusts the original AI-generated scores.

---

## 1. Trigger Conditions

### Which PolicyEngine flags trigger AI Council escalation?

Only `Trigger`-severity flags escalate to the AI Council. `Warning`-severity flags are informational only and never trigger Council escalation.

| Policy Rule | Severity | Escalates to Council? |
|---|---|---|
| `LOW_CONFIDENCE` | Trigger | ✅ Yes — confidence < 0.6 |
| `MISSING_EVIDENCE` | Warning | ❌ No |
| `BENCHMARK_OUTLIER` | Trigger | ✅ Yes — score > 2σ from tenant median |
| `INSUFFICIENT_EVIDENCE` | Warning | ❌ No |
| `SCORE_STRATEGY_MISMATCH` | Trigger | ✅ Yes — score > 7 AND documented_strategy = "none" |

**Important:** A reviewer may also manually escalate any category via the `escalate` action in the Reviewer Queue, regardless of whether a Trigger flag is present. Manual escalations bypass the policy evaluation and go directly to `CouncilRunner`.

### Can multiple categories be escalated in a single Council session?

**Yes.** A single `CouncilRunner` invocation processes all flagged categories in sequence. If categories A and B are both flagged, the Council deliberates on A completely (all 3 personas), then deliberates on B completely. The result is a single `CouncilDecision` record with deliberations for all processed categories.

### Who calls CouncilRunner?

`AuditOrchestrator` calls `CouncilRunner` after `PolicyEngine` evaluation completes for the full skill run. The sequence is:

```
AuditOrchestrator
  → SkillRunner.ExecuteChain()          [skills 1–5]
  → PolicyEngine.Evaluate()             [per category result]
  → CouncilRunner.Deliberate()          [if any Trigger flags exist]
  → ReviewerWorkflow.EnqueueForReview() [all categories, council-adjusted first]
```

---

## 2. Deliberation Protocol

### Input to CouncilRunner

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "CouncilRunnerInput",
  "type": "object",
  "required": ["audit_run_id", "flagged_categories"],
  "properties": {
    "audit_run_id": {
      "type": "string",
      "format": "uuid",
      "description": "The audit run that produced the flagged categories."
    },
    "flagged_categories": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["category_id", "policy_flags", "current_score", "confidence", "evidence"],
        "properties": {
          "category_id": {
            "type": "string",
            "enum": ["brand", "customer", "offering", "communications", "sales", "management"]
          },
          "policy_flags": {
            "type": "array",
            "items": {
              "type": "object",
              "required": ["rule", "severity"],
              "properties": {
                "rule":     { "type": "string", "enum": ["LOW_CONFIDENCE", "BENCHMARK_OUTLIER", "SCORE_STRATEGY_MISMATCH"] },
                "severity": { "type": "string", "enum": ["Trigger"] },
                "detail":   { "type": "string" }
              }
            }
          },
          "current_score": {
            "type": "integer",
            "minimum": 0,
            "maximum": 10,
            "description": "The activity score from Skill 1 for this category."
          },
          "confidence": {
            "type": "number",
            "minimum": 0.0,
            "maximum": 1.0
          },
          "evidence": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Evidence items used to produce the current score."
          },
          "documented_strategy": {
            "type": "string",
            "enum": ["none", "partial", "full"]
          },
          "skill_run_id": {
            "type": "string",
            "format": "uuid",
            "description": "The SkillRun that produced the flagged output."
          }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
```

### 3-Persona Deliberation — Sequential, Cascading Context

Personas are invoked **sequentially** for each flagged category in the following order:

```
Category N:
  1. Advocate    (sees: category data only — no prior persona output)
  2. Skeptic     (sees: category data + Advocate's position)
  3. Method Judge(sees: category data + Advocate's position + Skeptic's position)
```

**Context visibility rules:**
- **Advocate:** Blind to other persona outputs. Receives only the category's current score, policy flags, confidence, and evidence.
- **Skeptic:** Receives Advocate's position in full before generating its response. This allows the Skeptic to directly engage with the Advocate's reasoning.
- **Method Judge:** Receives both Advocate and Skeptic positions before arbitrating. Produces the definitive recommended score and final rationale.

This is intentional: each persona builds on (and can challenge) the prior persona's reasoning, producing a genuine deliberation rather than three independent assessments.

### Persona Position Schemas

#### Advocate Position Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "AdvocatePosition",
  "type": "object",
  "required": ["category_id", "recommended_score", "rationale", "supporting_evidence"],
  "properties": {
    "category_id": {
      "type": "string",
      "enum": ["brand", "customer", "offering", "communications", "sales", "management"]
    },
    "recommended_score": {
      "type": "integer",
      "minimum": 0,
      "maximum": 10,
      "description": "Advocate's recommended activity score for the category."
    },
    "rationale": {
      "type": "string",
      "minLength": 20,
      "maxLength": 1000,
      "description": "Reasoning for the recommended score, emphasizing client strengths."
    },
    "supporting_evidence": {
      "type": "array",
      "items": { "type": "string", "minLength": 5 },
      "minItems": 1,
      "description": "Evidence items the Advocate cites as strongest support."
    },
    "confidence_in_position": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0
    }
  },
  "additionalProperties": false
}
```

#### Skeptic Position Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SkepticPosition",
  "type": "object",
  "required": ["category_id", "concerns", "recommended_score", "risk_assessment"],
  "properties": {
    "category_id": {
      "type": "string",
      "enum": ["brand", "customer", "offering", "communications", "sales", "management"]
    },
    "concerns": {
      "type": "array",
      "items": { "type": "string", "minLength": 10, "maxLength": 500 },
      "minItems": 1,
      "description": "Specific challenges to the Advocate's position or the original score."
    },
    "recommended_score": {
      "type": "integer",
      "minimum": 0,
      "maximum": 10,
      "description": "Skeptic's recommended activity score — may be lower than Advocate's."
    },
    "risk_assessment": {
      "type": "string",
      "minLength": 20,
      "maxLength": 1000,
      "description": "Assessment of the risks of accepting the Advocate's higher score (e.g., overestimating client capability)."
    },
    "advocate_rebuttal": {
      "type": "string",
      "maxLength": 500,
      "description": "Direct response to specific claims made by the Advocate."
    }
  },
  "additionalProperties": false
}
```

#### Method Judge Position Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "MethodJudgePosition",
  "type": "object",
  "required": ["category_id", "process_verdict", "final_recommended_score", "arbitration_notes"],
  "properties": {
    "category_id": {
      "type": "string",
      "enum": ["brand", "customer", "offering", "communications", "sales", "management"]
    },
    "process_verdict": {
      "type": "string",
      "enum": ["compliant", "non_compliant"],
      "description": "Whether the scoring methodology was correctly applied. non_compliant implies the original score must be adjusted."
    },
    "final_recommended_score": {
      "type": "integer",
      "minimum": 0,
      "maximum": 10,
      "description": "The Method Judge's authoritative final score recommendation. This is the score that flows into CouncilDecision.adjusted_scores if decision_type = 'adjusted'."
    },
    "arbitration_notes": {
      "type": "string",
      "minLength": 20,
      "maxLength": 1500,
      "description": "Full explanation of why the Method Judge sided with Advocate, Skeptic, or neither. Must reference both prior positions."
    },
    "methodology_issues": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Specific methodology or rubric application issues identified, if any."
    }
  },
  "additionalProperties": false
}
```

### OpenAI Calls Per Council Session

For a Council session processing **N** flagged categories:
- **Total OpenAI calls = 3 × N** (one per persona per category)
- Each call is individually wrapped by the Polly resilience pipeline
- Sequential per category: Advocate call completes and output is validated → Skeptic call executes → Skeptic output validated → Method Judge call executes → Method Judge output validated
- If Advocate's output fails schema validation: that category's deliberation is aborted; Skeptic and Method Judge do not run for that category; the category falls back to the original score with an error flag
- If Skeptic's output fails schema validation: Method Judge does not run; same fallback applies
- A schema validation failure within Council does NOT trigger HTTP 502 for the overall request — the audit run continues with the original (unflagged) score for that category, and the failure is logged at Error level

---

## 3. Council Decision Output

### CouncilDecision Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "CouncilDecision",
  "type": "object",
  "required": [
    "audit_run_id",
    "decision_type",
    "deliberations",
    "adjusted_scores",
    "overall_confidence",
    "rationale",
    "decided_at"
  ],
  "properties": {
    "audit_run_id": {
      "type": "string",
      "format": "uuid"
    },
    "decision_type": {
      "type": "string",
      "enum": ["confirmed", "adjusted"],
      "description": "'confirmed' = Method Judge agreed with original scores across all flagged categories. 'adjusted' = at least one score was changed."
    },
    "deliberations": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["category_id", "advocate", "skeptic", "method_judge"],
        "properties": {
          "category_id": { "type": "string" },
          "advocate":    { "$ref": "#/definitions/AdvocatePosition" },
          "skeptic":     { "$ref": "#/definitions/SkepticPosition" },
          "method_judge":{ "$ref": "#/definitions/MethodJudgePosition" },
          "deliberation_status": {
            "type": "string",
            "enum": ["completed", "advocate_failed", "skeptic_failed", "judge_failed"],
            "description": "completed = all 3 personas succeeded. *_failed = that persona's schema validation failed; original score retained."
          }
        },
        "additionalProperties": false
      }
    },
    "adjusted_scores": {
      "type": "object",
      "description": "Map of category_id → adjusted score. Empty object if decision_type = 'confirmed'. Only categories where score changed are included.",
      "additionalProperties": {
        "type": "integer",
        "minimum": 0,
        "maximum": 10
      }
    },
    "overall_confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0,
      "description": "Average of Method Judge confidence across all successfully deliberated categories."
    },
    "rationale": {
      "type": "string",
      "minLength": 20,
      "maxLength": 3000,
      "description": "Aggregate rationale explaining the overall Council decision across all categories."
    },
    "decided_at": {
      "type": "string",
      "format": "date-time",
      "description": "ISO 8601 timestamp when the CouncilDecision was finalized. Set by the server, not the AI."
    }
  },
  "additionalProperties": false
}
```

### Database Persistence

The `CouncilDecision` is persisted to the `council_decisions` table (see data-dictionary). All three persona positions are stored in a JSONB `deliberations` column.

---

## 4. Post-Decision Flow

### If `decision_type = "adjusted"`

1. **`category_result_versions`**: A new row is inserted for each adjusted category with `source = 'council_adjustment'` and `council_decision_id` set. The `activity_score` is updated to the Method Judge's `final_recommended_score`. The `version_number` increments monotonically within `(audit_id, category)`.
2. **`category_results_current`**: The `current_version_id` pointer is updated to the new `category_result_versions.id` for each adjusted category.
3. The original Skill 1 score row in `category_result_versions` is **not modified** — append-only semantics are preserved. The full audit trail is maintained.
4. The updated composite score is recomputed from the six current activity scores.

### If `decision_type = "confirmed"`

No score rows are inserted. `category_results_current` is not updated. The original Skill 1 outputs remain current.

### SignalR Events

| Event | Fires When | Payload |
|---|---|---|
| `council-started` | `CouncilRunner.Deliberate()` is called, before first OpenAI persona call | `{ auditRunId, categories: [categoryId], triggeredBy: "policy_engine" \| "reviewer" }` |
| `council-completed` | `CouncilDecision` is persisted to the database | `{ auditRunId, decisionType: "confirmed"\|"adjusted", adjustedCategories: [categoryId], durationMs }` |

Both events are broadcast to the SignalR group keyed by `auditRunId`.

### Fallback: All 5 Skills Failed for a Category

If a category has no Skill 1 output (e.g., the scorecard rubric skill failed before producing scores), the Council **cannot deliberate** on that category. There is no score to confirm or adjust.

**Behavior:**
- The category is excluded from the `CouncilRunnerInput.flagged_categories` array
- It is flagged in the reviewer queue with `failure_reason = 'NO_SKILL_OUTPUT'`
- The reviewer must manually enter a score via the `edit` action with `override_reason_code = 'DATA_QUALITY_ISSUE'`
- The Council is never called for a category with no skill output — attempting to deliberate with no evidence would produce an unreliable AI output with no grounding

---

## 5. Azure OpenAI Calls Within Council

### Call Architecture Per Flagged Category

```
Category: "brand" (flagged: LOW_CONFIDENCE, SCORE_STRATEGY_MISMATCH)

Call 1: Advocate
  - System prompt: Advocate persona instructions
  - User message: category data (score=4, confidence=0.48, evidence=[...], flags=[...])
  - Response schema: AdvocatePosition
  - Polly: [Timeout 60s → Retry 3x → Circuit Breaker] wraps this call

Call 2: Skeptic
  - System prompt: Skeptic persona instructions  
  - User message: category data + Advocate position output
  - Response schema: SkepticPosition
  - Polly: [Timeout 60s → Retry 3x → Circuit Breaker] wraps this call independently

Call 3: Method Judge
  - System prompt: Method Judge persona instructions
  - User message: category data + Advocate position + Skeptic position
  - Response schema: MethodJudgePosition
  - Polly: [Timeout 60s → Retry 3x → Circuit Breaker] wraps this call independently
```

### Schema Validation Per Persona Call

Each persona's output is schema-validated **before** the next persona's call is made. This is by design:
- The Skeptic's prompt includes the Advocate's verbatim validated output — feeding invalid JSON would corrupt the Skeptic's deliberation
- Schema validation failure of a persona output is logged at `Error` level
- The category's deliberation is marked `deliberation_status = 'advocate_failed'` (or `skeptic_failed` / `judge_failed`)
- The original score is retained for that category
- The `CouncilDecision` is still finalized — it records the failed deliberation

### Polly Pipeline Behavior Within Council

Each of the 3N calls is an independent Polly execution. The shared singleton `ResiliencePipeline<HttpResponseMessage>` is used for each. This means:

- A timeout on Call 1 (Advocate) counts toward the circuit breaker's failure rate
- If the circuit breaker opens mid-Council (e.g., after the 2nd persona call hits the threshold), subsequent calls immediately throw `BrokenCircuitException`
- `BrokenCircuitException` is NOT retried — the affected category's deliberation is aborted with `judge_failed` status
- Council deliberation failures are non-fatal to the audit run: the run proceeds to the Reviewer Queue with original scores

### Logging Per Council Call

Every persona OpenAI call logs:
- `Information`: `"{Persona} deliberation started for {Category} in {AuditRunId}"`
- `Information`: `"{Persona} deliberation completed for {Category} in {AuditRunId} ({LatencyMs}ms)"`
- `Error`: `"{Persona} schema validation failed for {Category} in {AuditRunId}: {ErrorCode}"`

No AI response content is logged. No category name or client data is included in structured log values — only IDs.
