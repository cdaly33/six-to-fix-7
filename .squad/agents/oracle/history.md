# Project Context

- **Owner:** Scribe (Copilot CLI)
- **Project:** StrategicGlue Six-to-Fix — multi-tenant SaaS platform for marketing maturity audits. AI skill chain is the core product feature.
- **Stack:** .NET 10 LTS, Azure OpenAI Service (Azure.AI.OpenAI SDK, structured output + JSON Schema enforcement), Polly resilience (timeout 60s, retry 3x exponential, circuit breaker 50%/60s), Azure AI Search, Azure Blob Storage, HubSpot Webhooks API v3 (HMAC-SHA256), Azure Key Vault (managed identity)
- **Skill system:** 5 sequential skills with YAML frontmatter (name, version, model, output_schema_pointer, depends_on). Loaded at startup. Executed by SkillRunner.
  - 6tofix-scorecard-rubric → systems-maturity-scoring → gap-analysis-template → value-driver-rating → derive-tier
- **AI failure semantics:** Schema validation failure = HTTP 502, SkillRun.status = "failed", NO retry. This is intentional.
- **AI Council:** 3 personas (Advocate, Skeptic, Method Judge). Triggered only by policy "Trigger" rules (not Warnings). Produces CouncilDecision with decision_type (confirmed/adjusted).
- **HubSpot:** Outbound upsert on client create and audit publish. Inbound webhook HMAC validated before processing. Processing is async (background Channel worker). Failure is non-blocking.
- **Azure AI Search:** Documents indexed within 30s SLA. Search scoped to tenant.
- **Created:** 2026-05-10

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
