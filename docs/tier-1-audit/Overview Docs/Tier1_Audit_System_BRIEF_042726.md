STRATEGIC GLUE

**Tier 1 Audit**

System architecture, data model, and build brief

To: Ben (audit operator) · Chris (technical build)

From: Terry

Date: April 27, 2026

| **Quick read**  Ben — sections 2, 3, 4, 9, 10 cover the methodology and your role as audit operator.  Chris — sections 5, 6, 7, 8, 10 cover the data model, dashboard, build approach, and tech decisions.  Both — section 11 lists the open decisions we need to resolve to start Phase 1. |
| --- |

# **1. Purpose of this brief**

Strategic Glue is packaging the Tier 1 Audit as a productized service: $3,500 fixed fee, two-week delivery, designed as the entry tier in a three-tier ladder (Tier 2 Playbook, Tier 3 fCMO + AI). This brief captures the full architecture: what the audit delivers, how those deliverables share a single underlying data model, how that data model drives both the PDF bundle and a client-facing dashboard, and what we need to build to ship the first version.

The point of the architecture is leverage. With the Skills Library and the JSON data model in place, every audit produces consistent output against the same rubric, the same scoring discipline, and the same set of deliverables. That is the productized integrity that lets us eventually run audits with Ben as the operator and Terry on review — and that turns a 25-hour bespoke consulting engagement into a 10-hour, 60–70% conversion-driving wedge into the Tier 2 and Tier 3 economics.

Two audiences for this document. Ben will run the audit methodology — intake, scoring discipline, the readout. Chris will build the technical infrastructure — the JSON-driven dashboard, the Skill execution layer, the PDF generator. The brief is structured so each of you can read the sections you need without reading the whole thing.

# **2. Tier 1 Audit at a glance**

|  |  |
| --- | --- |
| Position | Entry tier in three-tier ladder (Tier 1 Audit → Tier 2 Playbook → Tier 3 fCMO + AI) |
| Pricing | $3,500 fixed fee, paid up front |
| Delivery | Two weeks from kickoff |
| Target buyer | $2M–$10M revenue, owner-operated, growth/succession/exit motivated |
| Conversion target | 60–70% of audits progress to Tier 2 within 45 days of readout |
| What it isn't | Not a sales meeting (it's a paid deliverable). Not a generic marketing audit (scored against a defined rubric). Not Tier 2 (no documented strategy is produced). |

The audit does four jobs at once: it generates revenue, it credentials the methodology, it filters tire-kickers, and it pre-sells the next tier. The economic argument for Tier 2 and Tier 3 is constructed during the audit and delivered at the readout. The conversion logic isn't a sales pitch tacked onto the end — it's the consequence of accurately diagnosing what we find.

# **3. The seven deliverables**

After the most recent round of design work, the audit produces seven deliverables — six artifacts and one live readout. The split between the 6 To Fix Scorecard and the Systems Maturity Score is intentional: they measure different things and confusing them muddies both.

| **#** | **Deliverable** | **What it is** |
| --- | --- | --- |
| 1 | 6 To Fix Scorecard | Two-page scored PDF. Numerical Activity Score (0–10) per area + Documented Strategy state (None/Partial/Current) per area. Total Marketing Maturity Score out of 60. Documented Strategy Coverage out of 6. |
| 2 | Systems Maturity Score | Single-page scored PDF. Independent four-dimension score (Documented / Repeatable / Measurable / Owner-Independent), 0–5 each, total 0–20. Distinct from Documented Strategy — measures execution, not thinking. |
| 3 | Gap Analysis Report | 8–12 page narrative PDF. For each area: current state, gaps, impact, framed as activity-vs-system and strategy-vs-documentation. |
| 4 | Value Driver Assessment | Single-page dashboard PDF. Scores against the 6 Value Drivers Marketing Owns, with peer benchmarks. Bridges marketing maturity to enterprise value. |
| 5 | 90-Day Roadmap | PDF + tracker. Top 6–10 priorities ranked by value and difficulty; each tagged human-vs-AI and activity-vs-system. |
| 6 | AI-Readiness Assessment | Multi-section PDF. Capability matrix, automation opportunity %, labor savings projection, three-way economic comparison, AI implementation roadmap. The economic case for Tier 3. |
| 7 | 90-Minute Readout | Live presentation; recording delivered. Walks all six artifacts through one narrative arc and closes with a tier recommendation. |

| **Why split Systems Maturity from the Scorecard**  Activity asks what they do. Systems Maturity asks how they do it. A client can score 8/10 on Brand activity and 4/20 on Systems Maturity — fundamentally different problems with different remedies. Embedding systems-maturity inside the Scorecard hid this distinction. Pulling it out makes both scores legible. |
| --- |

# **4. The 6 To Fix Scorecard — dual-criterion structure**

This is the most important methodological change in the current spec, and the one Ben needs to internalize. The Scorecard now scores each of the six areas on two independent axes:

* **Activity Score (0–10)** — quality and maturity of the marketing work happening in this area
* **Documented Strategy** — whether the strategic thinking behind the work is written down (None / Partial / Current)

These are independent. A client can score high on activity and zero on documented strategy — that's a different problem than low activity. The Scorecard surfaces the difference because the remedy is different.

## **Distinguishing Documented Strategy from Systems Maturity → Documented**

These two look similar. They are not the same thing, and both Ben (in scoring) and Chris (in modeling) need to keep them apart:

* **Documented Strategy** asks whether the *strategic thinking* is written down. "Why are we doing this?" Examples: positioning statement, ICP definition, channel rationale.
* **Systems Maturity → Documented** asks whether the *execution* is documented. "How do we do this?" Examples: SOPs, checklists, campaign playbooks.

A client can have one without the other. Most owner-operated $2M–$10M businesses have neither. Score them independently.

## **What 'Current' means per area**

The rubric defines required elements per area. An element counts as Current only if it (a) exists in writing, (b) was updated within the last 12 months, and (c) is referenced or used in actual marketing decisions. Articulated-by-the-owner-but-not-written-down counts as Missing.

| **Area** | **Required elements for Current** |
| --- | --- |
| Brand | Positioning · target audience · voice & tone · differentiation (3 of 4 current) |
| Customer | ICP · persona · acquisition strategy · retention strategy · journey map (ICP + persona + acquisition + retention current) |
| Offering | Value prop · USP · primary/secondary offerings · pricing · competitive positioning (value prop + USP + competitive positioning current) |
| Communications | Channel strategy · messaging framework · content cadence · campaign architecture (channel + messaging current) |
| Sales | Pipeline stages · sales process · objection handling · marketing handoff (pipeline + qualification + handoff current) |
| Management | KPI framework · reporting cadence · marketing budget · vendor/team structure (KPI + reporting current) |

## **Documented Strategy Coverage as conversion lever**

The new top-line metric — Documented Strategy Coverage, the count of areas where Documented Strategy is Current (0–6) — drives the tier recommendation directly. The mapping is the cleanest version of the conversion logic we've ever had:

| **Coverage** | **Recommendation** | **Why** |
| --- | --- | --- |
| 0–2 of 6 | Tier 2 | Tier 2 is the literal answer: it produces documented strategy across all six areas in 30 days. |
| 3–4 of 6 | Targeted Tier 2 or self-execute | Fill specific gaps with Tier 2, or self-execute with the roadmap if the owner has discipline and bandwidth. |
| 5–6 of 6 | Skip Tier 2; consider Tier 3 | Strategy is documented; the question is execution capacity. Recommend refresh-and-execute or Tier 3 directly. |

This shifts the readout from a soft judgment-based recommendation to a number-driven one. Ben doesn't have to win an argument — the data has already made the case.

# **5. System architecture: one data model, two presentations**

The architectural principle that everything else depends on: every audit produces a single structured JSON output as its real artifact. That JSON drives both the dashboard and the PDF bundle. Same numbers, same evidence, same gap statements — different containers.

* **Dashboard** is the living version: client logs in, drills into any area, sees roadmap progress, eventually re-scores. Re-scores create new JSON snapshots; deltas become visualizable automatically.
* **PDF bundle** is the archived snapshot: timestamped, signed, shareable with advisors, lender, board, prospective acquirer. The audit becomes a document the owner can hand to outside parties.

Three reasons this separation matters:

* **Consistency** — eliminates the risk of dashboard and PDF drifting apart as we iterate.
* **Versioning** — each re-score creates a new JSON; the dashboard can show baseline vs. current at the field level without rebuilding the renderer.
* **Build economics** — we build the data model once and render it twice, instead of maintaining two parallel pipelines.

| **Build sequence implication**  Lock the JSON schema before either renderer is built. The schema is the contract; everything else is a consumer of it. Both Ben (scoring rubric output) and Chris (rendering input) need to agree on the schema before either side ships code. |
| --- |

## **Data flow**

The full pipeline from intake to delivery, with each stage's input and output:

| **Stage** | **Input** | **Output** | **Owned by** |
| --- | --- | --- | --- |
| Intake | Owner + stakeholder interviews, materials packet, MarTech access | Project knowledge populated | Ben (interviews); automation (transcription, asset crawl, competitive scan) |
| Scoring | Project knowledge | Validated JSON conforming to tier1\_audit\_schema.json | Skills Library (Claude); Ben on review |
| Rendering | Validated JSON | PDF bundle (6 artifacts) + dashboard payload | PDF generator (Chris); dashboard renderer (Chris) |
| Delivery | PDF bundle, dashboard URL, recording | Audit complete; client onboarded to portal | Ben (readout); automation (delivery) |

# **6. The JSON data model**

Full schema is provided as a separate file: tier1\_audit\_schema.json. A populated example for a fictional client is provided as tier1\_audit\_sample.json. This section walks the structure at the conceptual level.

## **Top-level structure**

| {  "schema\_version": "1.0.0",  "engagement": { /\* client metadata, tier recommendation \*/ },  "scorecard": { /\* the dual-criterion 6 To Fix Scorecard \*/ },  "systems\_maturity":{ /\* 4-dimension systems score \*/ },  "value\_drivers": { /\* 6 value drivers, 0-5 each \*/ },  "ai\_readiness": { /\* automation %, labor savings $, capability matrix \*/ },  "roadmap": { /\* 6-10 prioritized items, tagged \*/ },  "documents": { /\* URLs to PDF artifacts and readout recording \*/ }  } |
| --- |

## **scorecard.areas — the dual-criterion shape**

Each of the six areas carries both criteria, plus the per-element checklist that drives the dashboard drill-down panel:

| "brand": {  "activity\_score": 5,  "documented\_strategy": "partial", // none | partial | current  "documented\_strategy\_elements": [  { "name": "Positioning statement", "status": "stale", "evidence": "..." },  { "name": "Target audience definition", "status": "stale", "evidence": "..." },  { "name": "Brand voice and tone guide", "status": "missing", "evidence": "..." },  { "name": "Differentiation statement", "status": "missing", "evidence": "..." }  ],  "top\_gap": "No documented voice or differentiation statement",  "gap\_narrative": "Brand activity is reasonable: a clean website...",  "evidence": ["Intake: ...", "Materials review: ..."]  } |
| --- |

Three things to notice:

* documented\_strategy\_elements is what makes the Tier 2 sales conversation pre-built into the UI. The owner sees exactly which elements are missing — that's the literal scope of the Tier 2 engagement.
* top\_gap renders on the area card; gap\_narrative renders in the drill-down panel. The Skills Library produces both.
* evidence citations are required. Every score must be defensible from at least two citations in the Project knowledge. Without this discipline, the methodology is unfalsifiable.

## **Composite numbers are derived**

Two top-line numbers in the scorecard are computed from the six area scores, not stored independently:

* total\_marketing\_maturity = sum of the six activity\_score values (0–60)
* documented\_strategy\_coverage = count of areas where documented\_strategy === "current" (0–6)

The dashboard and PDF should compute these on render rather than trusting the stored values. This protects against drift if the per-area scores are edited.

## **ai\_readiness — the conversion economics**

Four headline numbers drive the Tier 3 economic case:

| **Field** | **What it is** |
| --- | --- |
| automation\_opportunity\_pct | % of marketing work that could be AI-executed under Tier 3. Typical for ICP: 55–75%. |
| labor\_savings\_annual | Annual cost in USD to staff equivalent in-house team (loaded). Typical for $3M–$8M businesses: $180K–$320K/yr. |
| tier3\_annual\_cost | Annual Strategic Glue Tier 3 retainer in USD. |
| net\_annual\_benefit | labor\_savings − tier3\_cost. Renders as the highlighted line on the dashboard. The single most persuasive number in the audit. |

## **roadmap.items — the action layer**

Each roadmap item is fully tagged so the dashboard can filter and the readout can sequence:

| {  "id": "r1",  "priority": 1,  "title": "Document positioning, audience, voice, and differentiation",  "area": "brand",  "value\_impact": "high", // high | medium | low  "execution\_difficulty": "low", // high | medium | low  "human\_or\_ai": "ai\_assisted", // human\_only | ai\_assisted | ai\_native  "activity\_or\_system": "system", // activity | system  "estimated\_days": 5,  "status": "not\_started" // not\_started | in\_progress | complete | deferred  } |
| --- |

The activity\_or\_system tag is the most consequential. Strategy and systems-building items always tag as system. Items that drive volume (more posts, more emails) tag as activity. Item-level tagging is what lets the dashboard filter the roadmap into the lens that matters at any given moment.

# **7. The dashboard**

Standalone HTML wireframe is provided as tier1\_dashboard.html — Chris can open it in any browser and inspect the JSON-to-UI binding directly. The wireframe reads an embedded JSON literal at the bottom of the file; in production this becomes a fetch from the backend.

## **Information architecture**

Five zones, in priority order of what the owner sees first:

| **Zone** | **Content** | **Purpose** |
| --- | --- | --- |
| 1. Headline strip | Client name, audit date, next re-score date, tier recommendation pill | Resolves the 'what should I do' question before scrolling |
| 2. Metric row | Marketing Maturity (X/60), Documented Strategy (X/6), Systems Maturity (X/20), AI-Readiness (%) | At-a-glance summary numbers |
| 3. 6 To Fix Scorecard | Six area cards in a 3×2 grid; click any card to expand | The diagnostic centerpiece |
| 4. Drill-down panel | Activity sub-rubric, documented-strategy element checklist, gap narrative | Where the Tier 2 conversation lives — owner sees exactly what's missing |
| 5. Supporting widgets | Value drivers, Systems Maturity dimensions, AI-Readiness numbers | Three smaller cards in a row |
| 6. Roadmap + docs | Filterable roadmap, links to PDF artifacts, link to readout recording | The action layer + archive |

## **Per-area card format**

The card renders three things at a glance, in order of visual hierarchy:

* Area name (top-left) and Documented Strategy pill (top-right). The pill is the most semantically loaded element and gets the only color: green = current, amber = partial, red = none.
* Activity Score as the visual centerpiece — large number with a slim horizontal bar fill underneath.
* Top gap callout in muted text at the bottom — one line.

The eye reads name → strategy state → score → gap, which is the order an owner naturally processes the information. The card is fully clickable; click expands into the drill-down panel.

## **Construction approach**

Three phases, deferring complexity as long as possible:

| **Phase** | **Scope** | **Effort** | **Goal** |
| --- | --- | --- | --- |
| 1. MVP | Single-tenant per audit. Hardcoded routing per client. JSON file in simple backend (Supabase/Airtable). Magic-link auth. React + Tailwind. | 2–3 weeks of focused build | Ship to first client; validate the JSON-to-UI binding |
| 2. Multi-client + history | Real auth, multi-tenant data, audit versioning, deltas between baseline and re-score, interactive roadmap (status updates, notes). | 1–2 months | Dashboard becomes the central UI of every audit engagement |
| 3. Subscription product UI | Expand beyond Tier 1: Playbook content lives here for Tier 2 clients, monthly sprint calendars, content calendars, KPI dashboards, AI execution layer status. | 3–6 months | The Tier 1 dashboard is the seed; this is what it grows into |

| **Construction principles to lock in now**  Schema first, before any UI code. Both renderers consume it; lock the schema before either is built.  Component library, not bespoke screens. The same area card component appears six times; build once, render with data.  Print-first thinking. The PDF is generated from the same JSON; if the data structure can't render to a clean PDF, the data structure is wrong.  No native dashboard charts library yet. Recharts is plenty for Phase 1.  Authentication is not the interesting problem. Use Clerk, Supabase Auth, or Auth.js. Three weeks on auth is the most common Phase 1 mistake. |
| --- |

# **8. The Skills Library**

Anthropic Claude Skills are reusable methodology modules — each one is a SKILL.md plus supporting reference files (rubrics, templates, calibration examples) that Claude loads when the task matches. The Skills Library is the productized methodology. Without it, Tier 1 audits are bespoke consulting; with it, every audit scores against the same rubric and produces JSON conforming to the same schema.

## **Skills vs. Projects vs. code**

Three layers, often confused:

* **Skills** — reusable IP. Methodology that applies across every client. Built once, version-controlled, improved over time. Live in a shared repo.
* **Projects** — per-client knowledge. Interview transcripts, materials packet, asset inventory, in-progress deliverables. One Claude Project per audit client.
* **Code/scripts** — operational utilities. The asset-inventory crawler, the competitive scan harness. Live in a shared repo, called by Claude or by n8n.

## **The eight skills**

| **#** | **Skill** | **What it does** |
| --- | --- | --- |
| 1 | intake-interview-guide | Drives the 90-min owner conversation + 2–3 stakeholder interviews. Same signal collected every audit. |
| 2 | 6tofix-scorecard-rubric | Scores each of the six areas (Activity 0–10 + Documented Strategy state). Full SKILL.md provided as separate file. |
| 3 | systems-maturity-scoring | Independent 4-dimension score: Documented / Repeatable / Measurable / Owner-Independent. |
| 4 | gap-analysis-template | Produces the 8–12 page narrative Gap Analysis Report from scorecard + Project. |
| 5 | value-driver-rating | Scores against the 6 Value Drivers Marketing Owns; pulls peer benchmarks. |
| 6 | ai-readiness-projection | Produces capability matrix, automation %, labor savings, three-way economic comparison. |
| 7 | roadmap-prioritization | Drafts the 6–10 prioritized roadmap items with full tagging. |
| 8 | competitive-intel-scan | Structured competitive intelligence pull (Perplexity / Claude with web search). |

## **Build sequence**

| **Phase** | **Skills** | **Effort** | **When** |
| --- | --- | --- | --- |
| 1. Foundation | 6tofix-scorecard-rubric, systems-maturity-scoring, gap-analysis-template | 12–15 hrs | Build first; everything else depends on these |
| 2. Differentiation | value-driver-rating, ai-readiness-projection | 8–10 hrs | The deliverables that distinguish our audit from a generic marketing audit |
| 3. Efficiency | intake-interview-guide, roadmap-prioritization, competitive-intel-scan | 6–8 hrs | Save time on every audit but not blockers for delivery |

Total one-time effort: 26–33 hours. Recouped on roughly the third audit through time savings. The real return is consistency and defensibility — every audit scores against the same rubric, which is the productized integrity that lets us eventually run audits with Ben as the operator and Terry on review.

# **9. How an audit runs end-to-end**

This section is for Ben. Walks the audit week-by-week from kickoff to readout, identifying what's automated, what's Ben's, and what flows through the data model.

## **Week 1 — Intake**

| **Day** | **Activity** | **Owner** | **Output to Project** |
| --- | --- | --- | --- |
| Mon | Kickoff call (60 min). Confirm scope, schedule interviews, share materials request list. | Ben | Kickoff transcript |
| Tue | Asset crawl runs (automated). Competitive scan runs (automated). | Automation | Asset inventory + competitive scan |
| Tue–Wed | 90-min owner interview using intake-interview-guide skill. | Ben | Interview transcript (auto-transcribed) |
| Wed–Thu | 2–3 stakeholder interviews, 30–45 min each. | Ben | Stakeholder transcripts |
| Thu | Materials review. Ben reviews everything in the Project for completeness. | Ben | Materials review notes |
| Fri | Project knowledge complete. Scoring can begin. | Ben checkpoint | n/a |

## **Week 2 — Scoring, deliverables, readout**

| **Day** | **Activity** | **Owner** | **Output** |
| --- | --- | --- | --- |
| Mon | Run Skills: 6tofix-scorecard-rubric, systems-maturity-scoring, value-driver-rating. Validate JSON against schema. | Ben + Skills (Claude) | Validated JSON v1 |
| Tue | Run Skills: gap-analysis-template, ai-readiness-projection, roadmap-prioritization. Ben reviews and edits. | Ben + Skills | Updated JSON; draft narratives |
| Wed | PDF generation (automated from JSON). Dashboard build (automated from JSON). Ben reviews PDFs and dashboard. | Automation + Ben | PDF bundle, dashboard URL |
| Thu | Readout prep. Ben builds slides from the deliverables, anticipates objections, drafts the tier recommendation talk-track. | Ben | Readout slides |
| Fri | 90-minute readout. Recording delivered. Documents archived. Engagement transitions to either Tier 2 sales conversation or close. | Ben + Terry on first 3 audits | Recording, follow-up actions |

| **Where Ben's judgment is irreplaceable**  The 90-minute owner interview. Most of the real signal comes from the conversation; AI transcribes and structures, but the conversation has to be human.  Final scoring calibration. Skills draft scores; Ben sanity-checks against the calibration set and against what he heard in the interview.  The tier recommendation. Documented Strategy Coverage gives the rule, but Ben reads the politics of what the owner can actually push through.  The readout itself. The audit is a paid sales meeting and the readout is the close. Automating it would gut the conversion logic of the entire offer. |
| --- |

# **10. Build sequence and ownership**

This section is the joint plan. Three phases, with explicit ownership per stream.

## **Phase 1 — MVP (target: ship by 8 weeks from kickoff)**

| **Stream** | **Owner** | **Deliverable** | **Effort** |
| --- | --- | --- | --- |
| Schema lock | Terry | tier1\_audit\_schema.json finalized; sample JSON validated against it | 1 wk |
| Skills Library — Phase 1 skills | Terry + Ben | 6tofix-scorecard-rubric, systems-maturity-scoring, gap-analysis-template (12–15 hrs) | 2 wks |
| Dashboard MVP | Chris | React + Tailwind dashboard reading JSON; magic-link auth; one client | 2–3 wks |
| PDF generator | Chris | Generates 6 PDFs from JSON. HTML-to-PDF (Playwright) or docx-based. | 1–2 wks |
| Upstream automation | Chris | Asset crawl, competitive scan, transcription pipeline. n8n orchestration. | 1–2 wks |
| First-audit dogfood | Ben (operator) + Terry (review) | Run the entire pipeline on a friendly first client. Capture every break. | 2 wks (parallel) |

## **Phase 2 — Multi-client + audit history (1–2 months after Phase 1)**

* Real auth (Clerk or Supabase Auth) replacing magic-link.
* Multi-tenant backend; one client's audit invisible to another.
* Audit versioning: re-scores create new JSON snapshots; dashboard shows baseline vs. current.
* Roadmap interactivity: client can mark items complete, add notes, attach evidence.
* Phase 2–3 skills built (value-driver-rating, ai-readiness-projection, intake-interview-guide, roadmap-prioritization, competitive-intel-scan).

## **Phase 3 — Subscription product UI (3–6 months after Phase 2)**

* Dashboard expands beyond Tier 1. Becomes the platform for Tier 2 and Tier 3 engagements.
* Tier 2 Playbook content lives here, structured by area.
* Monthly sprint calendars and content calendars for Tier 3.
* KPI dashboards for ongoing engagements.
* AI execution layer status (which workflows running, which paused, last-run results).

# **11. Open decisions**

Decisions Chris needs us to lock before Phase 1 build starts. Most have a recommended default; flagging the alternatives so we make them deliberately.

| **Decision** | **Recommendation** | **Alternatives to weigh** |
| --- | --- | --- |
| Hosting/build approach | Custom React + Tailwind, deployed to Vercel. Full control, exact JSON binding. | Lovable.ai (faster build, less control). Retool (faster build for internal tools but worse for client-facing). |
| Auth provider | Clerk. Best DX, magic links built in, multi-tenant ready. | Supabase Auth (cheaper, more bundled). Auth.js (free, more setup). |
| Backend storage | Supabase. Postgres + auth + storage in one; easy to evolve. | Airtable (faster Phase 1, harder to scale). GitHub-as-CMS (cheap and version-controlled but rough UX). |
| PDF generator | Playwright headless rendering of HTML templates. Same component library as dashboard. | docx-based (uses our existing patterns). Server-side React-PDF (steeper curve but production-grade). |
| Where Skills run | Anthropic API direct, called from n8n with the Project knowledge attached. Best schema fidelity. | Claude Code (interactive, harder to automate). Claude API + Code Execution (more setup for marginal gain). |
| Schema validation | Ajv (JSON Schema validator) on every JSON write. Block writes that fail validation. | Manual review only (faster Phase 1, fragile). |
| Deployment URL pattern | portal.strategicglue.com/c/{client\_slug} | Subdomain per client (overkill for Phase 1). |

| **First decision to resolve**  Schema lock. Until tier1\_audit\_schema.json is final, neither side can build. Terry to circulate v1.0.0 by end of next week, Chris and Ben to review by following Friday. |
| --- |

# **12. Reference materials attached**

| **File** | **Purpose** | **Audience** |
| --- | --- | --- |
| Tier1\_Audit\_System\_Brief.docx | This document | Both |
| tier1\_audit\_schema.json | JSON Schema (formal contract for audit output) | Chris primarily; Ben for understanding what scoring produces |
| tier1\_audit\_sample.json | Fully populated JSON for a fictional HVAC client; shows the data model in practice | Both |
| tier1\_dashboard.html | Standalone wireframe; reads embedded JSON; runs in any browser | Chris primarily; Ben for understanding what the client sees |
| 6tofix-scorecard-rubric.SKILL.md | The central scoring skill, with the new dual-criterion rubric | Ben primarily; Chris for understanding what produces the JSON |

*Questions or pushback to Terry. Next checkpoint: schema lock review.*
