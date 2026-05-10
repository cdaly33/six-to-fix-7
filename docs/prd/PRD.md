# StrategicGlue Six-to-Fix — Product Requirements Document

> **Version:** 1.0  
> **Status:** Greenfield Specification  
> **Audience:** Engineering team, product stakeholders, and technical reviewers  
> **Framing:** This document describes the system to be built from scratch. It contains no references to prior implementations or migration steps. It is handed to a new team with a blank repository.

---

## Table of Contents

1. [Product Overview](#1-product-overview)
2. [UX & Screens](#2-ux--screens)
3. [Backend & Data Architecture](#3-backend--data-architecture)
4. [Infrastructure & Deployment](#4-infrastructure--deployment)
5. [Acceptance Criteria & Test Strategy](#5-acceptance-criteria--test-strategy)

**Reference Documents**
- [Design System](./design-system.md) — color tokens, typography, spacing, component specs
- [Data Dictionary](./data-dictionary.md) — all PostgreSQL tables and column definitions
- [API Spec](./api-spec.md) — REST endpoints and SignalR hub contracts
- [Infrastructure Spec](./infra-spec.md) — Azure resource inventory, Bicep layout, cost estimate
- [Auth Spec](./auth-spec.md) — auth provider recommendation, JWT claims, RBAC matrix

---

## 1. Product Overview

*Full detail: [sections/01-product-overview.md](./sections/01-product-overview.md)*

### What It Is

**StrategicGlue Six-to-Fix** is a multi-tenant SaaS platform that automates marketing maturity audits for professional service agencies. Agencies use it to assess their clients across six marketing domains, generate AI-powered analysis backed by evidence, facilitate structured human review, and produce a scored maturity report with a tier recommendation.

The platform compresses weeks of senior consultant effort into hours. It captures client reference materials, runs six parallel AI-assisted assessments, surfaces high-confidence insights to human reviewers, escalates uncertain findings to an AI Council for adjudication, and publishes a production-ready audit report.

### The Six Marketing Areas

Every audit scores a client across exactly these six areas, in this order:

| # | Area | What It Assesses |
|---|------|-----------------|
| 1 | **Brand** | Brand clarity, consistency, differentiation, and documentation |
| 2 | **Customer** | ICP definition, segmentation, persona documentation, and data quality |
| 3 | **Offering** | Product/service clarity, packaging, positioning, and value articulation |
| 4 | **Communications** | Content strategy, channel mix, messaging consistency, and campaign cadence |
| 5 | **Sales** | Sales process maturity, enablement, pipeline management, and conversion tracking |
| 6 | **Management** | Marketing leadership, planning discipline, budget ownership, and reporting cadence |

### Scoring Model

Each area receives two ratings:

- **Activity Score (0–10):** How actively and effectively the client operates in this area based on rubric-defined criteria.
- **Documented Strategy (current / partial / none):** Whether the client's practices are formally documented and maintained.

From these six area scores, three composite metrics are derived:

| Metric | Formula | Range |
|--------|---------|-------|
| **Composite Maturity Score** | Sum of all 6 Activity Scores | 0–60 |
| **Systems Maturity Score** | Rated across 4 dimensions (documentation, repeatability, measurability, owner-independence) | 0–20 |
| **AI Readiness Score** | Rated readiness for AI augmentation | 0–100% |

**Tier Recommendation** maps composite score to a tier:

| Tier | Meaning | Typical Range |
|------|---------|---------------|
| **tier_1** | High marketing maturity — optimize and scale | Upper composite band |
| **tier_2** | Developing maturity — systematize and document | Middle band |
| **tier_3** | Early stage — foundational work required | Lower band |

### User Roles

| Role | Primary Responsibility | Key Capability |
|------|----------------------|----------------|
| **Super Admin** | Platform operator | Manage all tenants, system config, global telemetry |
| **Tenant Admin** | Agency administrator | Manage agency users, clients, subscription settings |
| **Auditor** | Agency practitioner | Create clients, upload documents, run skill chain |
| **Reviewer** | Quality gatekeeper | Approve, edit, rerun, or escalate AI-generated outputs |

### Multi-Tenant Model

The platform is **multi-tenant SaaS**: each subscribing agency is a **Tenant**. All tenant data is strictly isolated at the database level via `tenant_id` foreign key on every tenant-scoped table.

```
Platform
  └── Tenant (agency)
        ├── Users (Tenant Admin, Auditor, Reviewer)
        ├── Clients (companies being audited)
        │     └── Documents (reference files)
        └── Audits
              └── AuditRuns → SkillRuns → CategoryPayloads
```

### The Audit Workflow

```
1. Create Client ──────────────────────────────────── Auditor
2. Upload Reference Documents ─────────────────────── Auditor
3. Create Audit Run ────────────────────────────────── Auditor
4. Execute Skill Chain (sequential):
     a. 6tofix-scorecard-rubric        → scores all 6 areas
     b. systems-maturity-scoring       → rates 4 maturity dimensions
     c. gap-analysis-template          → narrative gap per area
     d. value-driver-rating            → rates 6 value drivers
     e. derive-tier                    → synthesizes tier recommendation
5. Policy Engine evaluates each output ─────────────── (automated)
6. AI Council deliberates on flagged outputs ────────── (automated, when triggered)
7. Reviewer Queue: approve / edit / rerun / escalate ── Reviewer
8. Reviewer Lockout enforced (3 rejections / 24h) ───── (automated)
9. Publish: immutable final audit artifact ─────────── Reviewer / Auditor
10. CalibrationDelta logged for every score edit ──────── (automated)
```

### HubSpot Integration

Bidirectional CRM sync with HubSpot:
- Client created in platform → upsert HubSpot Company
- Audit published → push tier + composite score to HubSpot Company properties
- Inbound HubSpot webhooks → create/link platform clients (HMAC-verified)

---

## 2. UX & Screens

*Full detail: [sections/02-ux-and-screens.md](./sections/02-ux-and-screens.md) | Design tokens: [design-system.md](./design-system.md)*

### Visual Design Language

The platform uses a **warm cream/tan palette** with hand-crafted CSS design tokens. No external UI framework. Key tokens:

| Token | Value | Usage |
|-------|-------|-------|
| `--bg-primary` | `#F5F0E8` | Page background |
| `--bg-dark` | `#1A1A2E` | Top navigation bar |
| `--accent` | `#2563EB` | Primary action buttons, active states |
| `--border` | `#E5E0D5` | Card and input borders |
| `--text-primary` | `#1A1A2E` | Body text |

Cards use `border-radius: 8px`, `box-shadow: 0 1px 3px rgba(0,0,0,0.08)`, and lift on hover. Status is communicated through semantic color (green/blue/amber/red for tier and review status). Typography uses the system font stack at a 14px base.

### Screen Inventory

| Screen | Route | Primary Roles |
|--------|-------|---------------|
| Login | `/login` | All |
| Tenant Onboarding | `/onboard` | New Tenant Admin |
| Audit List | `/audits` | All authenticated |
| Audit Detail Dashboard | `/audits/{slug}` | Auditor, Reviewer, Tenant Admin |
| Skill Chain Runner | `/audits/{slug}/run/{runId}` | Auditor |
| Reviewer Queue | `/review/{runId}` | Reviewer |
| Category Review Drawer | (slide panel on Reviewer Queue) | Reviewer |
| Calibration Dashboard | `/calibration` | Tenant Admin, Super Admin |
| Telemetry Dashboard | `/telemetry` | Tenant Admin, Super Admin |
| Client Management | `/clients` | Auditor, Tenant Admin |
| Document Management | `/clients/{id}/documents` | Auditor |
| Tenant Admin Panel | `/admin` | Tenant Admin |
| Super Admin Panel | `/platform` | Super Admin |

See [sections/02-ux-and-screens.md](./sections/02-ux-and-screens.md) for full wireframes, interaction specs, and state documentation for each screen.

---

## 3. Backend & Data Architecture

*Full detail: [sections/03-backend-and-data.md](./sections/03-backend-and-data.md) | API contracts: [api-spec.md](./api-spec.md) | Tables: [data-dictionary.md](./data-dictionary.md)*

### Application Architecture

A single **ASP.NET Core (.NET 10)** application hosts both the Blazor Server frontend and all backend services. Blazor components call services via direct C# method calls over the SignalR circuit — no REST round-trip for UI data access. REST endpoints are exposed only for external integrations.

**Service Boundaries:**

| Service | Responsibility |
|---------|---------------|
| `AuditOrchestrator` | Coordinates full audit run lifecycle |
| `SkillRunner` | Executes individual AI skills with resilience pipeline |
| `PolicyEngine` | Evaluates outputs against 5 quality rules |
| `CouncilRunner` | Runs 3-persona AI council deliberation |
| `ReviewerWorkflow` | Enforces reviewer actions and lockout rule |
| `Publisher` | Assembles and persists immutable published audit |
| `CalibrationTracker` | Logs score overrides for model improvement |
| `TelemetryCollector` | Records daily run metrics |

### AI Layer

**Recommendation: Azure OpenAI Service** (over Azure AI Inference SDK) for structured output support, .NET SDK maturity, and JSON Schema enforcement on model responses.

**Skill System:** Each skill is a markdown file with YAML frontmatter defining `name`, `version`, `model`, `output_schema_pointer`, and `depends_on`. Skills are loaded at startup and executed sequentially by `SkillRunner`.

**Resilience Pipeline (Polly):** Every AI call passes through:
1. Timeout: 60 seconds
2. Retry: 3 attempts with exponential backoff (on 429 / 5xx)
3. Circuit Breaker: opens at 50% failure rate, 60-second break

**Schema Validation:** All AI outputs validated against JSON Schema before acceptance. Failure = HTTP 502, `SkillRun` marked `failed`, no retry.

### Policy Engine

Five rules evaluated against every `CategoryPayload`:

| Rule | Condition | Level | Effect |
|------|-----------|-------|--------|
| `LOW_CONFIDENCE` | `confidence < 0.6` | Warning + Trigger | Auto-escalate to council |
| `MISSING_EVIDENCE` | Evidence list empty | Warning | Informational flag |
| `BENCHMARK_OUTLIER` | Score deviates > 2 from industry median | Trigger | Escalate to council |
| `INSUFFICIENT_EVIDENCE` | Evidence list < 2 items | Warning | Informational flag |
| `SCORE_STRATEGY_MISMATCH` | `activity_score > 7` AND `documented_strategy = "none"` | Trigger | Escalate to council |

Warnings are informational and do not block publishing. Triggers route the category to the Reviewer Queue.

### AI Council

When a category is triggered, three AI personas deliberate:
- **Advocate** — optimistic, argues for the higher score
- **Skeptic** — challenges assumptions, argues for caution
- **Method Judge** — evaluates rigor of evidence, produces final adjudication

Output: `CouncilDecision` with `decision_type` (confirmed / adjusted), final scores, and rationale. Council-adjusted scores replace the original payload before the reviewer sees the category.

### Reviewer Workflow

| Action | Effect | Constraints |
|--------|--------|-------------|
| **Approve** | Category marked approved, removed from queue | None |
| **Edit** | Score + strategy updated, `CalibrationDelta` created | Override reason code + non-empty notes required |
| **Rerun** | New `SkillRun` triggered, category back to pending | Marks downstream skills stale |
| **Escalate** | Sends to AI Council regardless of policy flags | Available even without a trigger flag |

**Reviewer Lockout:** 3 rejections of the same category within 24 hours → HTTP 409 `REVIEWER_REJECTION_LOCKOUT`. The locked reviewer cannot act; a different reviewer can.

### Database

**Azure PostgreSQL Flexible Server** with:
- Multi-tenant isolation: `tenant_id` FK on all tenant-scoped tables
- Append-only ledger for category history (`category_result_versions`)
- Database roles: `sf_admin` (DDL+DML, migrations), `sf_app` (DML only, runtime)
- pgBouncer connection pooling on port 6432

15 tables total — see [data-dictionary.md](./data-dictionary.md) for full schema.

---

## 4. Infrastructure & Deployment

*Full detail: [sections/04-infrastructure.md](./sections/04-infrastructure.md) | Resource inventory: [infra-spec.md](./infra-spec.md) | Auth: [auth-spec.md](./auth-spec.md)*

### Azure Resource Topology

```
rg-StrategicGlue-CommandCenter
  ├── app-strategicglue-{env}        Azure App Service (Blazor + API)
  ├── asp-strategicglue-{env}        App Service Plan (B2 dev / P2v3 prod)
  ├── psql-strategicglue-{env}       PostgreSQL Flexible Server (v16)
  ├── kv-strategicglue-{env}         Key Vault (all secrets)
  ├── ststrategicglue{env}           Blob Storage (documents)
  ├── srch-strategicglue-{env}       Azure AI Search
  ├── appi-strategicglue-{env}       Application Insights
  ├── log-strategicglue-{env}        Log Analytics Workspace
  ├── vnet-strategicglue-{env}       Virtual Network (prod)
  └── nsg-strategicglue-{env}        Network Security Group (prod)
```

All Azure connections use **system-assigned managed identity** — no connection string secrets in app settings except those stored in Key Vault (referenced via Key Vault reference syntax).

### Auth: Duende IdentityServer

**Recommendation: Duende IdentityServer** (co-hosted in the ASP.NET Core app) — selected over ASP.NET Core Identity+JWT (no OIDC server), Azure AD B2C (policy complexity), and Keycloak (JVM runtime).

Rationale:
- Native .NET, runs in the same process as the app
- Full OIDC server (supports future tenant SSO federation)
- Blazor Server-optimized (server-side token management, no browser token exposure)
- Strong multi-tenancy support via custom claims (`tenant_id`, `tenant_slug`)

**JWT Claims Required:**

```json
{
  "sub": "user-uuid",
  "email": "user@example.com",
  "name": "Full Name",
  "tenant_id": "tenant-uuid",
  "tenant_slug": "agency-slug",
  "roles": ["Reviewer"],
  "iss": "https://auth.strategicglue.com"
}
```

### CI/CD

Four GitHub Actions workflows:

| Workflow | Trigger | Action |
|----------|---------|--------|
| `deploy-infra.yml` | Push to `main` touching `infrastructure/` | Bicep deployment via OIDC |
| `deploy-app.yml` | Push to `main` (app code) | Zip deploy to App Service |
| `validate-skills.yml` | PR touching `skills/` | Skill schema validation |
| `test.yml` | Every PR | .NET tests + Playwright E2E |

**Environments:** `dev` auto-deploys on push to `main`. `prod` requires manual approval gate, deployed on release tag.

---

## 5. Acceptance Criteria & Test Strategy

*Full detail: [sections/05-acceptance-criteria.md](./sections/05-acceptance-criteria.md)*

### Summary: 44 Acceptance Criteria Across 13 Feature Areas

| Area | AC Count | Key Threshold |
|------|----------|---------------|
| Multi-Tenancy & Isolation | 4 | `tenant_id` isolation on every query |
| Authentication & Authorization | 5 | 5 failures → 15-min lockout |
| Client & Document Management | 5 | 10MB limit, indexed within 30s |
| Audit Creation & Skill Chain | 6 | Schema fail → 502, no retry |
| Policy Engine | 4 | All 5 rules, combined flags |
| AI Council | 3 | Triggers only, not warnings |
| Reviewer Queue & Actions | 5 | 3 rejections/24h → 409 lockout |
| Publishing | 4 | All 6 approved, immutable |
| Calibration & Telemetry | 3 | Every edit creates delta |
| HubSpot Integration | 3 | HMAC verified, failure non-blocking |
| Document Search | 2 | Scoped to tenant, 30s indexing |
| Resilience & Error Handling | 4 | No PII in logs, 503 with Retry-After |
| Performance Targets | 4 | Dashboard < 2s, AI call < 60s |

### Test Strategy

**Test Pyramid:**

| Layer | Tool | When |
|-------|------|------|
| Unit | xUnit (pure functions, policy rules, score computation) | Every PR |
| Integration | xUnit + Testcontainers (real PostgreSQL) | Every PR |
| Component | bUnit (Blazor components with interactive state) | Every PR |
| API/Contract | xUnit + `WebApplicationFactory` | Every PR |
| E2E | Playwright (full audit workflow) | Merge to `main` |

**Coverage target:** 80% of domain logic (unit + integration).  
**AI calls:** Mocked at every test layer — no real AI calls in tests.  
**Quality gate:** All tests pass + no new compiler warnings required to merge.

---

## Key Product Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Frontend | Blazor Server (.NET 10) | Server-side, no SPA complexity, .NET native |
| AI Service | Azure OpenAI Service | Structured output, JSON Schema enforcement, mature SDK |
| Auth Provider | Duende IdentityServer | Native .NET, OIDC server, Blazor-optimized |
| Database | Azure PostgreSQL Flexible Server | Managed, scalable, pgBouncer support |
| Tenancy Model | Multi-tenant SaaS | `tenant_id` isolation on all tables |
| Reviewer Lockout | 3 rejections / 24h → 409 | Anti-gaming protection |
| Publish Semantics | Immutable, versioned | Audit integrity and replay |
| Policy Escalation | Triggers → council, Warnings → informational | Proportionate automation |

---

## Document Change History

| Version | Date | Author | Notes |
|---------|------|--------|-------|
| 1.0 | 2026-05-10 | Squad (Lisa, Bart, Frink, Smithers, Milhouse) | Initial greenfield specification |
