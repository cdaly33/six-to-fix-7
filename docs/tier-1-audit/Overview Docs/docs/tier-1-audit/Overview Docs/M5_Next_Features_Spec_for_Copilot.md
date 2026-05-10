# M5 Next Features Spec (Copilot-Ready)

Version: 1.0  
Date: 2026-04-30  
Owner: Strategic Glue  
Status: Planning draft for squad implementation

---

## 1) Purpose

Define the next implementation wave after M4 stabilization. This document focuses on operationalizing audit delivery with human approval, category-level reruns, evidence-safe outputs, council adjudication, benchmark governance, and frontend test hardening.

This spec is intended for direct use in GitHub Copilot and sprint planning.

---

## 2) Scope and priorities

### In scope (M5)
1. Reviewer workflow + approval ledger
2. Category-only rerun orchestration
3. Evidence-safe error handling + PII-safe response behavior
4. Benchmark provenance and >1 point delta guardrails
5. AI Council v1 adjudication flow
6. Frontend test coverage for Tier 1 audit screens
7. Operational telemetry dashboard foundation

### Out of scope (M5)
- Multi-reviewer approval workflow
- Fully autonomous no-human delivery
- Replacing AI scoring with deterministic-only rules

---

## 3) Epics and acceptance criteria

## Epic A — Reviewer workflow + approval ledger

### Objective
Introduce a mandatory one-human-review gate with immutable review history.

### Requirements
- Support actions per category:
  - `approve`
  - `edit`
  - `rerun_category`
  - `escalate_to_council`
- Require audit metadata:
  - `reviewer_id`
  - `timestamp_utc`
  - `reason_code`
  - `notes` (optional)
- Do not publish final snapshot unless all six categories are approved.

### Acceptance criteria
1. Reviewer actions are persisted immutably.
2. A current approved pointer exists per category.
3. Dashboard shows draft/pending/approved states.
4. Attempt to publish without six approvals is rejected with explicit validation message.

---

## Epic B — Category-only rerun orchestration

### Objective
Enable rerunning one category without forcing full-pipeline reruns.

### Requirements
- Rerun command targets exactly one category.
- Previous approved state remains active until replacement is approved.
- Dependent rollups marked stale during rerun.
- On approval, recompute only affected aggregates.

### Acceptance criteria
1. Rerunning one category does not enqueue other category workers.
2. State transitions are captured in run timeline.
3. Rollups update after approval, not before.
4. Historical category versions remain queryable.

---

## Epic C — Evidence-safe errors and PII-safe client responses

### Objective
Prevent prompt/content leakage in client-visible error payloads while retaining internal diagnostics.

### Requirements
- Client-facing schema errors return safe path-oriented details only.
- Raw model output previews remain server-side only (internal logs/restricted diagnostics).
- Introduce warning when evidence claims are missing.

### Acceptance criteria
1. PII tokens never appear in API error bodies for schema violations.
2. Error payload includes safe fields (`error_code`, `message`, optional `violations[]`).
3. Tests prove leakage does not occur on failing skill runs.

---

## Epic D — Benchmark provenance + delta guardrails

### Objective
Allow AI-estimated peer comparisons while preventing ungrounded score swings.

### Requirements
- Persist benchmark context with each category result:
  - `industry_band_used`
  - `estimated_peer_position`
  - `evidence_strength` (`weak|moderate|strong`)
  - `notes`
- Enforce policy: score delta >1 point requires strong evidence.
- Strong evidence = 2 of 3:
  1. Recent direct artifact evidence
  2. Cross-source corroboration (>=2 source types)
  3. Benchmark method trace with confidence + limitations

### Acceptance criteria
1. >1 benchmark-driven score shifts are blocked, routed to council, or require reviewer override reason.
2. Every benchmark-influenced score contains provenance metadata.
3. UI exposes evidence strength badge.

---

## Epic E — AI Council v1 adjudication

### Objective
Provide deterministic tie-break workflow for conflicting outputs.

### Requirements
- Trigger conditions:
  - low confidence (`<0.70`) in high-impact contexts
  - cross-component conflict
  - >1 point delta without strong evidence
  - manual reviewer escalation
- Council roles:
  - Advocate
  - Skeptic
  - Method Judge
- Output:
  - final recommendation
  - rationale
  - required follow-ups
  - confidence after council

### Acceptance criteria
1. Council run is recorded with full decision lineage.
2. Reviewer can accept or override council output with reason.
3. Final category state references council run id when used.

---

## Epic F — Frontend test coverage (Tier 1)

### Objective
Add focused UI test coverage for critical audit paths after lint hardening.

### Requirements
- Component tests for:
  - ScorecardArea
  - SkillChain
  - AIReadinessSection
  - Review action controls
  - Warning badges and confidence displays
- Page tests for:
  - AuditDashboardPage data loading + unmount cleanup
  - SkillReviewPage action flows
- Contract tests for handling invalid/partial API payloads.

### Acceptance criteria
1. New tests cover review/rerun/warning critical path.
2. CI gate fails on broken review actions.
3. No regressions in existing build/lint checks.

---

## Epic G — Operational telemetry foundation

### Objective
Expose cost/reliability indicators for operational decisions.

### Requirements
- Capture and aggregate per run:
  - latency (p50/p95)
  - prompt/completion/total tokens
  - council escalation rate
  - reviewer edit delta rate
- Alerts for:
  - auth failures
  - retry spikes
  - schema violation spikes

### Acceptance criteria
1. Daily metrics aggregation job runs successfully.
2. Dashboard endpoint returns summarized operational metrics.
3. Alert thresholds configurable via environment.

---

## 4) Data model additions (conceptual)

Suggested new/extended tables:
- `review_actions`
- `category_results_current`
- `category_result_versions`
- `council_runs`
- `council_decisions`
- `warnings`
- `run_metrics_daily`

Core principles:
- Immutable event history
- Current-pointer pattern for fast reads
- Full lineage from model output to reviewer-approved result

---

## 5) API surface (conceptual)

### Reviewer endpoints
- `POST /api/audits/{slug}/categories/{category}/approve`
- `POST /api/audits/{slug}/categories/{category}/edit`
- `POST /api/audits/{slug}/categories/{category}/rerun`
- `POST /api/audits/{slug}/categories/{category}/escalate`

### Council endpoints
- `POST /api/audits/{slug}/categories/{category}/council/run`
- `GET /api/audits/{slug}/categories/{category}/council/{runId}`

### Operational metrics
- `GET /api/ops/audits/metrics/daily`

---

## 6) State machine update (high-level)

Global states:
- `queued`
- `running_workers`
- `policy_check`
- `council_review` (optional)
- `human_review`
- `approved`
- `published`
- `failed`

Category states:
- `saved`
- `pending_review`
- `approved`
- `rerun_in_progress`
- `council_pending`

---

## 7) Definition of done checklist

For each epic, done means:
1. Functional behavior implemented.
2. Unit/integration tests cover critical paths.
3. Documentation updated.
4. Observability hooks added where applicable.
5. Error cases include safe and actionable responses.

---

## 8) Suggested sprint sequence

### Sprint M5-A
- Epic A (Reviewer workflow)
- Epic B (Category-only reruns)
- Epic C (PII-safe error responses)

### Sprint M5-B
- Epic D (Benchmark provenance/guardrails)
- Epic E (AI Council v1)
- Epic F (Frontend tests)

### Sprint M5-C
- Epic G (Ops telemetry)
- Performance and reliability tuning

---

## 9) Risk register

1. **Risk:** Reviewer flow causes UX bottlenecks  
   **Mitigation:** Save drafts + async notifications + quick approve shortcuts.

2. **Risk:** Council overuse increases latency  
   **Mitigation:** Tight trigger conditions and confidence thresholding.

3. **Risk:** Benchmark quality inconsistency  
   **Mitigation:** Mandatory provenance + evidence-strength labels.

4. **Risk:** Schema-safe errors lose debug detail  
   **Mitigation:** Preserve full detail in restricted internal logs.

---

## 10) Copilot prompts (ready to paste)

### Prompt 1 — Reviewer workflow
"Implement reviewer action endpoints and immutable review event persistence for approve/edit/rerun/escalate actions per category. Enforce six-category approval before publish."

### Prompt 2 — Category rerun engine
"Add category-only rerun orchestration that preserves prior approved output, marks dependent rollups stale, and selectively recomputes aggregates after approval."

### Prompt 3 — Safe error contract
"Refactor schema violation API responses to return safe error codes and violation paths only; keep raw model payload server-side. Add tests proving no PII leaks in client responses."

### Prompt 4 — Benchmark guardrails
"Implement policy checks for benchmark-driven score deltas >1 requiring strong evidence. Add provenance metadata to category outputs and return machine-readable warnings."

### Prompt 5 — AI Council
"Implement council orchestration with trigger matrix, three-role adjudication output contract, decision persistence, and reviewer escalation hooks."

### Prompt 6 — Frontend tests
"Add React tests for Tier 1 critical paths: review actions, rerun behavior, warning badges, confidence display, and API partial/error payload handling."

### Prompt 7 — Ops telemetry
"Implement daily aggregation of skill-run latency and token metrics with council/reviewer deltas. Expose API endpoint for operational dashboard consumption."

---

## 11) Open questions for M5.1

1. Do warnings require mandatory reviewer notes for certain codes?
2. Should council decisions auto-apply or require explicit reviewer acceptance always?
3. Should publish permit partial categories in exceptional cases?
4. What SLA targets should be enforced per category run and full audit run?
5. Should benchmark references require external citation URLs in persisted metadata?

