# v1 Audit Orchestration Spec (Copilot-Ready)

Version: 1.0  
Date: 2026-04-30  
Owner: Strategic Glue  
Status: Draft for implementation planning

---

## 1) Objective

Design an orchestration flow for Tier 1 Audit scoring across six categories using independent AI workers, a policy layer, AI Council tie-break, and one human reviewer before delivery.

This spec is intended to be pasted into GitHub Copilot Chat or used as a planning prompt for implementation tasks.

---

## 2) Scope

In scope:
- Six independent category scoring processes:
  - brand
  - customer
  - offering
  - communications
  - sales
  - management
- Category-only re-run behavior
- Confidence-based warnings
- Benchmark-driven scoring with evidence controls
- AI Council tie-break workflow
- Single human reviewer approval gate
- Versioned persistence design (target: Azure PostgreSQL Flex)

Out of scope (v1):
- Fully autonomous final delivery (no human review)
- Multi-reviewer workflow
- Hard-blocking tier derivation on low confidence

---

## 3) Product decisions already locked

1. One human reviewer is sufficient.
2. Confidence threshold for warning: `< 0.70`.
3. Benchmark-driven score deltas `> 1 point` are allowed only with strong evidence.
4. Reviewer edits should feed the calibration/training loop.
5. Low confidence should warn only (not block tier recommendation derivation).

---

## 4) Canonical output model

Use existing scorecard schema semantics for each area, plus governance extensions.

### 4.1 Required category payload (logical)

```json
{
  "category": "brand",
  "activity_score": 0,
  "documented_strategy": "none|partial|current",
  "documented_strategy_elements": [
    {
      "name": "Positioning statement",
      "status": "current|stale|missing",
      "evidence": "string"
    }
  ],
  "top_gap": "string",
  "gap_narrative": "string",
  "evidence": ["string"],
  "confidence": 0.0,
  "benchmark_context": {
    "industry_band_used": "string",
    "estimated_peer_position": "below_median|at_median|above_median",
    "evidence_strength": "weak|moderate|strong",
    "notes": "string"
  }
}
```

Notes:
- `activity_score` range: `0..10`
- `confidence` range: `0..1`
- `documented_strategy_elements[]` must exist for explainability

---

## 5) Input model

Each run includes:
- Shared audit context (interviews, materials index, asset crawl refs, competitive scan refs, martech refs)
- Category-specific JSON input for each of the six areas
- Policy settings

### 5.1 Example run input envelope

```json
{
  "audit_id": "uuid",
  "client": {
    "name": "Acme HVAC Services",
    "industry": "HVAC services",
    "revenue_band": "$3M-$5M"
  },
  "shared_context": {
    "interview_refs": ["..."],
    "materials_refs": ["..."],
    "asset_inventory_refs": ["..."],
    "competitive_scan_refs": ["..."],
    "martech_inventory_refs": ["..."]
  },
  "category_inputs": {
    "brand": {"ref": "..."},
    "customer": {"ref": "..."},
    "offering": {"ref": "..."},
    "communications": {"ref": "..."},
    "sales": {"ref": "..."},
    "management": {"ref": "..."}
  },
  "policies": {
    "confidence_warning_threshold": 0.7,
    "enforce_strong_evidence_for_gt1_benchmark_shift": true
  }
}
```

---

## 6) Orchestration flow

1. Validate run input envelope.
2. Start six category workers in parallel.
3. Validate each worker output (schema + bounds).
4. Execute policy checks.
5. Trigger AI Council for flagged conflicts.
6. Present results to single human reviewer.
7. Apply reviewer actions (approve/edit/rerun/escalate).
8. Recompute derived rollups and tier recommendation.
9. Persist immutable versions + current approved pointers.
10. Publish dashboard payload (with warnings if present).

---

## 7) Policy engine rules

### Rule P1 — Confidence warning
If `confidence < 0.70`, append warning:
- `LOW_CONFIDENCE`

### Rule P2 — Benchmark delta control
If score delta vs last approved is `> 1` and benchmark reasoning is material:
- Require strong evidence standard (see P2.a)
- Else route to AI Council or reviewer explicit override

#### P2.a Strong evidence standard (2 of 3 required)
1. Recent direct artifact evidence exists
2. Cross-source corroboration (>=2 source types)
3. Benchmark method trace has medium/high confidence and stated limits

### Rule P3 — Evidence completeness
If major claims have no evidence entry:
- Append warning `INSUFFICIENT_EVIDENCE`
- Require reviewer acknowledgment before final approval

### Rule P4 — Non-blocking warnings
Warnings do not block tier recommendation derivation.

---

## 8) AI Council tie-break spec

### 8.1 Trigger conditions
- Cross-component contradiction
- Confidence below threshold on high-impact area
- Benchmark-driven delta >1 lacking strong evidence
- Manual reviewer escalation

### 8.2 Council roles
- Advocate Agent (defend current result)
- Skeptic Agent (challenge assumptions)
- Method Judge Agent (enforce rubric/schema)

### 8.3 Council output
```json
{
  "category": "sales",
  "decision": {
    "final_activity_score": 4,
    "final_documented_strategy": "partial",
    "decision_type": "affirmed|adjusted|insufficient_evidence",
    "rationale": "string",
    "required_followups": ["string"],
    "confidence_after_council": 0.74
  }
}
```

---

## 9) Human review spec (single reviewer)

### 9.1 Allowed actions
- `approve`
- `edit`
- `rerun_category`
- `escalate_to_council`

### 9.2 Required review metadata
- `reviewer_id`
- `action`
- `timestamp_utc`
- `reason_code`
- `note` (optional)

### 9.3 Approval gate
No client-facing final snapshot unless all six categories have reviewer-approved state.

---

## 10) Category-only rerun behavior

When rerunning one category:
1. Only selected category worker runs.
2. Previous approved result remains active until replacement is approved.
3. Dependent aggregates marked stale/pending.
4. After approval, recompute affected rollups only.

---

## 11) Persistence design (Azure PostgreSQL Flex target)

Suggested tables:
- `audits`
- `audit_runs`
- `category_runs`
- `category_result_versions`
- `category_results_current`
- `warnings`
- `council_runs`
- `council_decisions`
- `review_actions`
- `derived_rollups`

Design principles:
- Immutable version history for every AI and reviewer decision
- Explicit pointer to current approved category result
- Full audit trail from score -> evidence -> council/reviewer edits

---

## 12) Warning taxonomy

- `LOW_CONFIDENCE`
- `INSUFFICIENT_EVIDENCE`
- `BENCHMARK_HIGH_DELTA`
- `CROSS_COMPONENT_CONFLICT`
- `STALE_SOURCE_MATERIAL`

All warnings are visible in reviewer UX and final dashboard metadata.

---

## 13) Calibration loop (reviewer edits as learning)

Capture per category:
- `ai_initial_score`
- `reviewer_final_score`
- `delta`
- `reason_code`
- `initial_confidence`

Periodic calibration report:
- Mean absolute delta by category
- Delta by confidence band
- Frequent reason codes
- Prompt/rubric update recommendations

---

## 14) Acceptance criteria (v1)

1. Six category workers execute independently and in parallel.
2. Each output passes schema and score bounds.
3. Confidence warnings appear for all `<0.70` outputs.
4. >1 benchmark-related deltas are evidence-gated.
5. AI Council triggers on configured conditions.
6. Single reviewer can approve/edit/rerun/escalate each category.
7. Category-only rerun works without full pipeline rerun.
8. Tier derivation proceeds with warnings (no hard block).
9. Versioned persistence preserves full decision lineage.

---

## 15) Copilot task prompts (ready to paste)

### Prompt A — Create orchestrator state machine
"Implement a state machine for audit orchestration with states: queued, running_workers, policy_check, council_review, human_review, approved, published, failed. Include category-level rerun transitions and stale aggregate handling."

### Prompt B — Define TypeScript/C# contracts
"Generate strongly-typed models for audit run input, category worker output, warnings, council decision, reviewer action, and approved snapshot. Enforce score/confidence bounds and enums."

### Prompt C — Policy engine scaffolding
"Create a policy evaluator that emits warnings for confidence<0.70, evidence completeness gaps, and benchmark delta >1 without strong evidence. Return machine-readable flags for council routing."

### Prompt D — Reviewer workflow
"Build reviewer action handlers (approve/edit/rerun/escalate) with immutable version writes and a current-approved pointer update strategy."

### Prompt E — Persistence migration plan
"Propose SQL migrations for tables: audits, audit_runs, category_runs, category_result_versions, category_results_current, warnings, council_runs, council_decisions, review_actions, derived_rollups, with indexes for latest-approved lookups."

---

## 16) Open questions for v1.1

1. Should confidence be calibrated per category (different thresholds by area)?
2. Should specific warnings require mandatory reviewer notes before approval?
3. Should benchmark evidence capture include external citation URLs in v1?
4. Do we need SLA targets for worker completion and council turnaround?
5. Should we support partial publish when one category is under rerun?

