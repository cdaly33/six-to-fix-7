# StrategicGlue Six-to-Fix — Product Overview

## 1. Executive Summary

**StrategicGlue Six-to-Fix** is a multi-tenant SaaS platform that automates marketing maturity audits for professional service agencies. Marketing agencies use the platform to rapidly assess their clients' marketing effectiveness across six critical business functions, receive AI-powered analysis backed by evidence, facilitate structured human review, and produce a scored maturity report with a tier recommendation.

The platform solves a critical time-to-value problem for agencies: traditional marketing audits require weeks of manual analysis by senior consultants. Six-to-Fix reduces that to hours—capturing client data through a structured intake process, running six parallel AI-assisted assessments, surfacing high-confidence insights to reviewers, escalating uncertain findings to an AI Council for tie-breaking, and publishing a production-ready audit report. The scored tier recommendation helps agencies and their clients prioritize where to invest marketing resources next.

Agencies subscribed to Six-to-Fix can run unlimited audits, each producing a scored report that feeds directly into engagement planning. The platform is built on a transparent scoring model so agencies can explain every score to their clients and rerun audits to track maturity improvements over time.

---

## 2. User Personas & Roles

### Super Admin
**Who they are:** Platform operator; typically an employee of Strategic Glue (the company) or a delegated partner.

**What they need:**
- Full visibility and control over all tenants, their subscription status, and usage metrics
- Ability to create new tenant accounts and manage their subscription tier
- System configuration: AI model parameters, policy thresholds, audit parameters
- Access to platform monitoring dashboards and audit run telemetry

**Primary workflow:**
1. Onboard a new agency as a tenant
2. Configure AI/policy settings for the platform
3. Monitor aggregate metrics: total audits run, average tier distribution, system health
4. Troubleshoot platform-wide issues or escalations

**Constraints:**
- Cannot access audit data belonging to individual tenants unless explicitly escalated for support
- Cannot modify individual audit results; only platform configuration

---

### Tenant Admin
**Who they are:** Account administrator at a marketing agency; typically the office manager, partner, or principal who manages team access and billing.

**What they need:**
- Control over which team members can access Six-to-Fix and in what role
- Visibility into subscription status, usage, and billing
- Ability to invite new auditors and reviewers
- Audit history and analytics for their agency's work

**Primary workflow:**
1. Invite team members (auditors and reviewers) to the tenant
2. Manage permissions and team structure
3. View usage reports (audits run this month, tier distribution across clients)
4. Manage billing and subscription settings

**Constraints:**
- Cannot create or run audits themselves
- Cannot access raw scores or AI deliberations; can only view published reports
- Limited to their own tenant data only

---

### Auditor
**Who they are:** Marketing consultant or analyst at the agency who gathers client data and initiates audits.

**What they need:**
- Interface to create a new client record with basic information (company name, industry, revenue band)
- Simple document upload interface to attach intake materials (brand guides, interview transcripts, sales data, MarTech inventory)
- Ability to initiate the audit run once documents are ready
- View of the audit run status and progress as it processes

**Primary workflow:**
1. Create a client entry in the platform (company name, industry, revenue band)
2. Upload reference documents for that client
3. Initiate an audit run
4. Monitor progress as the AI processes the six categories
5. Notify reviewers when the run is ready for review

**Constraints:**
- Cannot approve, edit, or reject audit outputs
- Cannot modify client data once the audit has started
- Cannot access other tenants' data

---

### Reviewer
**Who they are:** Senior consultant or practice lead responsible for validating audit findings and ensuring quality before client delivery.

**What they need:**
- Dedicated review queue showing all flagged categories awaiting human decision
- For each category: AI score, confidence level, supporting evidence summary, and any policy flags or AI Council deliberations
- Ability to approve, edit the score, request an AI rerun, or escalate to the AI Council
- View of calibration metrics (how often and by how much they override AI scores)
- Lock-out protection: clear feedback when they've exceeded rerun limits for a category

**Primary workflow:**
1. Navigate to the review queue for a specific audit run
2. Review each flagged category one at a time
3. For each category:
   - Read the AI score and confidence level
   - Review supporting evidence snippets from uploaded documents
   - Either approve the score, edit the score, request an AI rerun, or escalate to council
4. Once all six categories are approved, publish the final audit report
5. Periodically review calibration data to understand override patterns

**Constraints:**
- Can only review their own tenant's audits
- Cannot edit or delete any category after it is approved and locked
- Cannot run audits themselves
- Subject to the 3-rerun lockout rule (cannot request more than 3 reruns per category within 24 hours)

---

## 3. Multi-Tenancy Model

### Tenant Definition
A **tenant** represents a subscription unit—typically one marketing agency or organization. Each tenant is a completely isolated instance of the platform with its own:
- Client records and audit history
- Users (auditors, reviewers, admins)
- Billing and subscription status
- AI and policy configuration (inherited from platform defaults, may be customized per tenant in future versions)

### Data Isolation Guarantees
- No tenant can view, access, or modify another tenant's data
- All audit records, client information, and uploaded documents are encrypted and stored in tenant-specific partitions
- API authentication ensures that a user's authentication token grants access only to their own tenant
- Database row-level security enforces isolation at the query level as a defense-in-depth safeguard

### Onboarding Flow
1. Super Admin creates a new tenant account via the admin console (provides agency name, contact email, subscription tier)
2. Tenant Admin (usually the contact person) receives an account setup email with a login link
3. Tenant Admin logs in and completes tenant setup: adds team members, configures team roles
4. Auditor or Tenant Admin creates the first client and uploads documents
5. First audit run begins

### Subscription Model (Logical)
The platform operates on a **per-tenant subscription model**:
- **Tier options:** Starter (limited audits/month), Professional (unlimited audits, standard support), Enterprise (unlimited audits, priority support, custom AI config)
- **Billing cycle:** Monthly or annual
- **Usage-based limits:** Starter tier caps at N audits/month; Professional and Enterprise have no per-audit caps
- **Seat-based access:** Unlimited users per tenant (all users share the same audit and client data; differentiation is by role)

### Tenant Admin Authority
Tenant Admins manage:
- User invitations and role assignment (auditor, reviewer, admin)
- Team member offboarding (revoke access)
- Subscription upgrades/downgrades
- Billing contact and payment method
- Audit history for their agency (read-only; cannot modify)

---

## 4. The Six-to-Fix Audit Domain Model

### The Six Marketing Areas

The audit assesses marketing maturity across six distinct business functions, always evaluated in this order:

1. **Brand** — consistency and clarity of brand identity, positioning, messaging, and visual standards
2. **Customer** — depth of customer understanding, data quality, segmentation, and personalization strategy
3. **Offering** — product/service positioning, differentiation, pricing strategy, and market fit
4. **Communications** — omnichannel campaign design, content strategy, creative execution, and measurement rigor
5. **Sales** — sales process definition, enablement, pipeline management, and revenue alignment
6. **Management** — organizational structure, cross-functional alignment, performance tracking, and strategic planning

### Activity Scores (0–10 Scale)

Each of the six areas receives an **Activity Score** between 0 and 10, reflecting the maturity level of activities and practices in that domain:

- **0–2 (Ad Hoc):** Minimal documented process; activities are sporadic and inconsistently applied
- **3–4 (Reactive):** Some documented processes exist; activities are triggered by immediate needs rather than strategy
- **5–6 (Planned):** Clear documented processes and ownership; activities are strategic and planned quarterly
- **7–8 (Optimized):** Well-defined processes with regular measurement and continuous improvement; activities are data-driven
- **9–10 (Autonomous):** Fully automated, self-improving systems; activities require minimal human intervention and adapt in real-time

Each score is justified by specific evidence from the audit (direct quotes or findings from uploaded documents) and confidence level (high, medium, or low).

### Documented Strategy Rating

For each of the six areas, the audit also rates the **Documented Strategy** as one of three values:

- **Current** — A documented strategy exists, is current (updated within the last 12 months), and is actively followed
- **Partial** — A documented strategy exists but is incomplete, outdated, or partially followed
- **None** — No documented strategy exists for this area

This dimension measures the organization's rigor around strategic planning and documentation, independent of execution quality.

### Composite Marketing Maturity Score

The **Composite Marketing Maturity Score** is the sum of all six Activity Scores, yielding a total between 0 and 60.

```
Composite Score = Brand + Customer + Offering + Communications + Sales + Management
Range: 0–60
```

This aggregate score represents the overall health of the organization's marketing maturity and is used alongside Systems Maturity and AI Readiness to derive the final tier recommendation.

### Systems Maturity Score (0–20)

The **Systems Maturity Score** rates the organization's operational and technical infrastructure across four dimensions, each scored 0–5:

1. **Documentation** — Processes are documented, accessible, and current (0: none; 5: comprehensive, version-controlled)
2. **Repeatability** — Processes are standardized and repeatable, not dependent on individuals (0: no; 5: fully standardized)
3. **Measurability** — Outcomes are tracked with KPIs and dashboards (0: no metrics; 5: real-time dashboards, predictive models)
4. **Independence** — Processes can be executed by anyone with the right training, not just key individuals (0: key-person dependent; 5: fully independent)

**Total range: 0–20**

A high Systems Maturity Score indicates that the organization has built scalable, sustainable marketing operations that do not depend on individual experts or heroic efforts.

### AI Readiness Score (0–100%)

The **AI Readiness Score** is a percentage (0–100%) that rates the organization's readiness to benefit from AI augmentation across marketing operations. It considers:

- Quality and accessibility of first-party customer data
- Depth of historical marketing performance data
- Clarity of defined business outcomes and KPIs
- Technical infrastructure capable of ingesting and acting on AI outputs
- Organizational alignment on AI governance and ethical guidelines

A score of 70%+ indicates the organization is well-positioned to pilot AI marketing tools. A score below 50% suggests foundational data and process work is needed first.

### Value Drivers

The audit identifies and scores six **Value Drivers**—business areas where the organization could realize significant enterprise value by improving marketing maturity. Each driver is scored as:

- **High Impact** — Could drive 10%+ incremental revenue or 15%+ cost reduction
- **Medium Impact** — Could drive 3–10% revenue or 5–15% cost reduction
- **Low Impact** — Could drive <3% revenue or <5% cost reduction

The six Value Drivers are:

1. **Revenue Growth** — Increased sales velocity, expanded customer lifetime value, or new market penetration
2. **Cost Efficiency** — Reduced customer acquisition cost, optimized ad spend, or streamlined operations
3. **Customer Retention** — Improved customer satisfaction, repeat purchase rate, or NPS
4. **Competitive Differentiation** — Stronger market positioning, brand awareness, or defensibility
5. **Operational Agility** — Faster time-to-market, reduced campaign cycle time, or increased experimentation velocity
6. **Data & Insights** — Improved decision-making through customer data platforms, analytics maturity, or predictive capabilities

---

## 5. Tier Recommendation Logic

Based on the Composite Score, Systems Maturity Score, and supplementary factors, the audit produces a **Tier Recommendation** with three possible values:

### Tier 1 (Highest Maturity — Score 48+)
- Marketing operations are sophisticated, data-driven, and strategic
- Organization has documented processes, clear KPIs, and cross-functional alignment
- Ready for advanced initiatives (AI augmentation, predictive marketing, demand generation at scale)
- Recommendation: Focus on competitive differentiation, operational optimization, and revenue growth

### Tier 2 (Mid-Market Maturity — Score 30–47)
- Marketing operations are planned but have gaps in execution, data quality, or cross-functional alignment
- Some processes are documented and measured, but inconsistencies remain
- Systems Maturity or a critical gap in a high-value area may be limiting factor
- Recommendation: Build out fundamentals (data infrastructure, process documentation, team alignment)

### Tier 3 (Developing Maturity — Score <30)
- Marketing operations are largely reactive and ad-hoc
- Limited process documentation, measurement rigor, or cross-functional alignment
- Foundational work needed (audience definition, competitive positioning, campaign measurement)
- Recommendation: Invest in foundational process design and team enablement

The tier derivation accounts for:
- The Composite Score (primary factor)
- Systems Maturity Score (modifier—a gap in systems can lower a high composite score)
- Confidence levels of the individual scores (low-confidence findings are noted but do not block publication)
- Any cross-component warnings (e.g., misalignment between sales and customer understanding)

---

## 6. The AI-Assisted Audit Workflow

### Workflow Overview

The audit workflow is a coordinated sequence of AI skill execution, policy-driven triage, optional AI Council deliberation, human review, approval locking, and publication:

```
1. Auditor uploads documents and initiates audit run
   ↓
2. Six category workers run in parallel (Foundry skills)
   ↓
3. Policy Engine evaluates each category for flags
   ↓
4. Flagged categories → AI Council (optional)
   ↓
5. All categories enter Reviewer Queue
   ↓
6. Reviewer approves, edits, reruns, or escalates each category
   ↓
7. All 6 categories approved → Publish final audit payload
   ↓
8. Calibration recorded; audit complete
```

### The Skill Chain — Six Parallel Workers

When an auditor initiates an audit run, the platform launches six independent **category workers**, one for each marketing area. All six run in parallel; execution is not serialized.

Each worker:
1. Loads the uploaded documents and audit context
2. Invokes the corresponding Foundry skill (`brand-audit`, `customer-audit`, `offering-audit`, `communications-audit`, `sales-audit`, `management-audit`)
3. The skill:
   - Analyzes the documents for evidence and insights
   - Scores the Activity Score (0–10) and Documented Strategy rating (current/partial/none)
   - Generates a narrative gap analysis (what's working, what's not, why)
   - Provides confidence level (high, medium, low) for the score
   - Extracts supporting evidence snippets from the documents
4. The worker persists the result with a timestamp and version number

**Expected output per skill:** Activity Score, Documented Strategy rating, narrative gap analysis, confidence level, evidence snippets, warning codes (if any).

### Policy Engine — Quality Gate & Triage

Once all six category workers complete (or as each completes), the **Policy Engine** evaluates whether the result is ready for human review or requires escalation to the AI Council.

**Policy Rules:**

- **P1 (Low Confidence):** If confidence level is "low", flag for AI Council review
- **P2 (High Impact + Low Confidence):** If a category is flagged as a high-value business driver AND confidence is "medium" or lower, flag for AI Council review
- **P3 (Warning Trigger):** If the result contains any warning codes (e.g., `LOW_CONFIDENCE`, `MISSING_EVIDENCE`, `CONFLICTING_SOURCES`), flag for AI Council review

**Policy Decision:** Each category receives one of three dispositions:
- **Auto-Approved** — Pass all policies; go directly to publishing
- **AI Council Review** — Flag triggered; must be deliberated by AI Council before human review
- **Human Review Only** — No flags; bypass AI Council and go directly to reviewer queue

### AI Council — Three-Model Tie-Break

If a category is flagged by the Policy Engine, it enters the **AI Council**—a deterministic deliberation among three AI personas:

1. **Advocate** — Defends the AI's original score; explains why the confidence should be higher
2. **Skeptic** — Challenges the AI's original score; looks for gaps in evidence and alternative explanations
3. **Method Judge** — Focuses on methodology and rigor; asks whether the evaluation process is sound

Each persona reviews the same documents and AI output and produces a written assessment. The platform then aggregates the three assessments using a deterministic voting mechanism:

- If 2+ personas agree with the AI's score → approve the original score
- If all 3 personas diverge significantly → recommend a score adjustment (midpoint of the three) and escalate to human reviewer with **"AI Council Adjustment"** flag
- If exactly 1 persona dissents → keep original score but add dissenting note for human reviewer

**AI Council output:** Revised score (if adjusted), decision justification, and dissenting notes (if any). All AI Council deliberations are logged and visible to the human reviewer.

### Reviewer Queue — Human Approval Gate

Once all categories are either Auto-Approved or AI Council-approved, they enter the **Reviewer Queue**. A human reviewer (the assigned Reviewer role) sees:

- Six categories, each with:
  - AI score and confidence level
  - Documented Strategy rating
  - Narrative gap analysis (first 200 words)
  - Supporting evidence snippets
  - Any policy flags or AI Council notes
  - Category status (pending approval, locked, approved)

### Four Reviewer Actions

For each category, the reviewer can take one of four actions:

1. **Approve** — Accept the AI score and Documented Strategy rating as-is. The category is locked (cannot be re-edited).

2. **Edit Score** — Change the Activity Score (e.g., from 6 to 7) or Documented Strategy rating (e.g., from "partial" to "current"). The reviewer provides a brief justification. The category is locked after edit.

3. **Request Rerun** — Ask the AI to re-evaluate the category with additional instructions or clarifications. The category re-enters the queue as "awaiting rerun", the skill runs again, and the reviewer sees the new result.

4. **Escalate to AI Council** — If unsure or if the AI and reviewer views diverge significantly, escalate for AI Council re-deliberation. The Council re-engages and produces a new recommendation.

All actions are timestamped and logged for calibration and audit trail purposes.

### Reviewer Lockout Rule

To prevent gaming (e.g., repeatedly rejecting the AI to stall a review or cause unnecessary cost), the platform enforces a **Lockout Rule**:

- **If a reviewer requests more than 3 reruns for the same category within 24 hours, the category is locked for further reruns.**
- Subsequent rerun requests for that category return **HTTP 409 (Conflict)** with error code `LOCKOUT_VIOLATION` and a message: *"Rerun limit reached for {category}. Approve, edit, or escalate instead."*
- The lockout persists for 24 hours from the first rerun request; after 24 hours, the counter resets.
- The Super Admin or Tenant Admin can manually override the lockout in cases of genuine escalation (audit trail entry required).

**Rationale:** Three reruns in 24 hours is sufficient to address legitimate concerns. Beyond that, the reviewer should make a decision (approve, edit, escalate) rather than endlessly rerun the AI.

### Publish Step — Final Aggregation

Once the reviewer has approved (or edited and approved) all six categories:

1. The platform computes the **Composite Marketing Maturity Score** (sum of all six Activity Scores)
2. The **Systems Maturity Score** is derived from the six category outputs (if not already scored by a dedicated skill)
3. The **AI Readiness Score** is computed
4. The **Tier Recommendation** is derived using the scoring formula
5. All six categories, metadata, scores, and tier recommendation are packaged into a final **Audit Payload**
6. The payload is versioned (version = previous version + 1) and stored as a locked, read-only record
7. The audit status changes to **Published**
8. Auditor and Tenant Admin receive a notification that the audit is ready for client delivery

### Calibration Feedback Loop

Every time a reviewer edits an AI score, the delta (AI score vs. reviewer score) is recorded to a **Calibration Store**:

```json
{
  "run_id": "audit-run-abc-123",
  "category": "brand",
  "ai_score": 7.0,
  "reviewer_score": 5.0,
  "delta_absolute": 2.0,
  "source": "reviewer_edit",
  "timestamp": "2026-05-10T14:32:00Z"
}
```

Over time, patterns in calibration deltas inform:
- Skill model refinement (if a category is consistently overscored by the AI, retrain with corrected feedback)
- Policy threshold adjustments (if many high-confidence results are being overridden, lower the confidence threshold for AI Council escalation)
- Reviewer feedback (if one reviewer's deltas are larger than others', provide coaching on scoring rubric consistency)

Calibration data is visible to Super Admin and Tenant Admin as a table showing per-category mean deltas, trends over time, and comparison to platform baseline.

---

## 7. HubSpot CRM Integration

### Sync Scope

The platform syncs audit data with HubSpot to enable seamless engagement workflows. The following data entities sync bidirectionally:

| Entity | Direction | Trigger | Mapping |
|--------|-----------|---------|---------|
| **Company** | ← → | Audit client created / updated | Company name, industry, revenue band sync to HubSpot company properties |
| **Contact** | ← | HubSpot contact created / updated | New/updated HubSpot contact can auto-create a platform client (if Tenant Admin enables) |
| **Deal** | → | Audit published | Audit tier recommendation and composite score written to deal custom properties |
| **Contact Property** | → | Audit published | Tier recommendation, score, and AI Readiness % written to contact properties for engagement tracking |

### Sync Direction & Triggers

**Outbound (Platform → HubSpot):**
- When an audit is **published**, the tier recommendation, Composite Score, Systems Maturity Score, and AI Readiness Score are pushed to the associated HubSpot deal (if deal ID was provided during audit setup)
- Contact properties are updated with tier, score, and readiness data so sales/success teams can see maturity at a glance

**Inbound (HubSpot → Platform):**
- If enabled by Tenant Admin, new HubSpot contacts/companies can trigger auto-creation of platform clients
- Platform admins can manually initiate a sync to pull existing HubSpot companies as client records

### Authentication & Rate Limiting

- Tenant Admin provides HubSpot OAuth credentials once during setup
- Platform uses OAuth tokens to call HubSpot API with Tenant-scoped access (does not access other HubSpot workspaces)
- Sync respects HubSpot API rate limits (25K calls/day); sync queue manages timing to stay compliant
- Sync failures are logged and retried with exponential backoff (max 3 retries over 24 hours)

### Sync Conflicts & Error Handling

- If a platform client and HubSpot company have conflicting data (e.g., different industry classification), **platform data wins** (platform is the source of truth for audit attributes)
- If sync fails, a notification is sent to Tenant Admin; manual retry is available from the audit UI
- No audit is blocked or delayed due to HubSpot sync failures (async, best-effort)

---

## 8. Scope & Constraints

### What Is IN Scope for V1

- **Core audit platform:** Multi-tenant SaaS with role-based access (Super Admin, Tenant Admin, Auditor, Reviewer)
- **The six-area audit:** Full workflow from client intake to published tier recommendation
- **AI-assisted scoring:** Six parallel Foundry skills, policy engine, AI Council tie-break, human reviewer queue
- **Reviewer workflow:** Approve, edit, rerun, and escalate actions with lockout enforcement
- **Audit persistence:** Versioned, immutable audit payloads; calibration tracking
- **HubSpot integration:** Outbound sync of tier and scores to deals and contacts; inbound contact sync (if enabled)
- **Multi-tenancy:** Complete data isolation, per-tenant configuration, subscription model
- **Role-based access control:** Four roles with distinct permissions and workflows
- **API:** RESTful API for audit operations (create client, upload documents, initiate run, reviewer actions, publish)
- **Web UI:** Blazor Server frontend for audit intake, reviewer queue, calibration dashboard, and tenant admin
- **Documentation:** Product specification, API reference, Reviewer Guide
- **Observability:** Structured logging for audit workflow events, metrics for runs, reviewer actions, and tier distribution

### What Is OUT of Scope for V1

- **Billing and payment processing:** Billing system integration deferred to V2 (platform assumes manual subscription provisioning)
- **Advanced reporting and analytics:** Custom dashboards, drill-down analytics deferred to V2; V1 provides basic calibration view only
- **Competitive benchmarking:** Scoring relative to industry norms deferred to V2
- **Audit scheduling and automation:** V1 requires manual audit initiation; recurring/scheduled audits deferred to V2
- **Client-facing portal:** Clients cannot directly view results or interact with the platform; all client communication is agency-mediated in V1
- **Integration beyond HubSpot:** Salesforce, Pipedrive, and other CRMs deferred to V2
- **Mobile app:** V1 is web-only
- **Multi-language support:** V1 is English-only
- **Advanced AI customization:** V1 uses platform-default AI models and policies; per-tenant model fine-tuning deferred to V2
- **Audit template customization:** V1 uses the fixed six-area model; custom audit frameworks deferred to future versions
- **Offline mode:** V1 requires internet connectivity; offline draft/capture deferred to V2

### Known Constraints

**Technical Constraints:**
- **Tech stack is fixed:** ASP.NET Core (.NET 10), Blazor Server, Azure PostgreSQL, Azure Blob Storage, Azure AI Search, Azure App Service
- **AI model selection TBD:** Azure OpenAI Service or Azure AI Inference SDK (team to recommend; not in scope of this PRD)
- **Authentication approach TBD:** ASP.NET Core Identity + JWT, Azure AD B2C, or Duende IdentityServer (team to recommend)
- **Maximum document upload per audit:** 100 MB (soft limit, configurable per tenant in future)
- **Audit run timeout:** 5 minutes for all six skills to complete (soft limit; configurable)

**Operational Constraints:**
- **Super Admin role requires Azure access:** Super Admin portal runs on Azure Portal integration or dedicated admin dashboard (TBD by team)
- **No offline audit creation:** Auditor must have internet connectivity; all audit operations require API/web connectivity
- **No partial saves:** Audit must be fully initiated before skill execution begins (no in-progress draft saves)
- **Reviewer cannot approve while AI Council is deliberating:** If escalated, the category is locked pending Council decision

**Business Constraints:**
- **Single audit per client:** V1 does not prevent creating multiple audits for the same client, but the platform does not merge or compare audit history automatically; comparison is manual
- **Tier recommendation is final on publish:** No rollback or unpublish; audit history is immutable once published
- **No audit bulk operations:** V1 does not support bulk importing, bulk review, or bulk publishing; all operations are per-audit

### Performance Targets (Indicative)

- **Audit run completion:** All six skills complete and are ready for review within 5 minutes (p50), 10 minutes (p95)
- **Reviewer queue load:** Reviewer queue UI loads in <3 seconds for audits with <500 KB of evidence
- **API response time:** 99% of API responses return within 500 ms
- **Concurrent audits:** Platform should support 100+ concurrent audits running simultaneously without degradation

### Compliance & Security Considerations

- **Data encryption:** All data at rest encrypted using Azure-managed keys; data in transit uses TLS 1.2+
- **GDPR compliance:** Tenant data can be exported or deleted per GDPR requirements (data subject access requests handled by Tenant Admin)
- **SOC 2 readiness:** Platform logs all audit actions for SOC 2 Type II compliance; audit trail is immutable
- **No PII in logs:** Structured logs do not capture client names, company names, or user emails; only anonymized IDs
- **Secret management:** API keys, connection strings, and credentials stored in Azure Key Vault; never committed to source code

---

## 9. Success Metrics (Non-Binding Guidance)

The product is considered successful at V1 launch if:

- **Adoption:** At least 5 pilot agencies sign up and run 10+ audits each within the first 30 days
- **Reviewer efficiency:** Average time to review and approve an audit is <15 minutes (benchmarked against manual audits: 2–4 hours)
- **AI accuracy:** Reviewer override rate (% of AI scores edited by reviewers) is <15% on average
- **System reliability:** Audit runs complete successfully 99.5% of the time; no data loss or corruption incidents
- **User satisfaction:** Pilot agencies rate the platform 4.0+ out of 5.0 on ease of use (NPS >40)

---

## 10. Document Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-05-10 | Lisa (Lead Planner) | Initial greenfield PRD Section 1 |

