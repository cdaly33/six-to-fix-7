# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Solution architecture, DI setup, project structure | Morpheus | Namespace layout, service lifetimes, interface contracts, `Program.cs` config |
| Code review, architectural decisions | Morpheus | PR review, cross-cutting concerns, integration surface design |
| Blazor Server components, Razor pages | Trinity | All `.razor` files, component state, cascading parameters |
| CSS design system, UI styling | Trinity | Design tokens, layout, all 13 screen implementations |
| SignalR real-time UI | Trinity | Skill chain runner progress, connecting to `/hubs/audit-run` |
| Service layer implementation | Neo | AuditOrchestrator, SkillRunner, PolicyEngine, CouncilRunner, ReviewerWorkflow, Publisher, CalibrationTracker, TelemetryCollector |
| EF Core, database schema, migrations | Neo | Entity models, DbContext, 15-table schema, multi-tenant queries |
| ASP.NET Core Identity + JWT | Neo | User store, JWT issuance, tenant claims, refresh tokens |
| REST API endpoints | Neo | Minimal API endpoints for /api/*, /health, /webhooks |
| Background workers | Neo | HubSpotEvent Channel worker |
| Azure OpenAI integration, skill execution | Oracle | IAIClient implementation, Polly resilience pipeline, JSON Schema validation |
| AI Council deliberation | Oracle | 3-persona council (Advocate/Skeptic/Method Judge), CouncilDecision |
| HubSpot CRM sync, webhook HMAC | Oracle | Inbound webhook validation, outbound company/audit sync |
| Azure AI Search, document indexing | Oracle | Search client implementation, tenant-scoped queries |
| Azure Bicep, infrastructure provisioning | Tank | All resources in rg-StrategicGlue-CommandCenter |
| GitHub Actions CI/CD | Tank | deploy-infra.yml, deploy-app.yml, validate-skills.yml, test.yml |
| App Service configuration | Tank | ARR affinity, always-on, Key Vault references, managed identity grants |
| Unit tests, component tests | Tank | xUnit, bUnit, test project setup |
| Integration tests, E2E tests | Tank | Testcontainers (PostgreSQL), Playwright |
| Test coverage enforcement | Tank | 80% gate, compiler warnings gate |
| Session logging | Scribe | Automatic — never needs routing |
| Work queue monitoring | Ralph | Automatic when activated |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
