# Morpheus History Archive (Phases 0–6)

## Summary

Morpheus completed comprehensive architecture review across 6 phases (2026-05-10 to 2026-05-15):

- **Phase 0 Seal:** Consolidated 12 Chris architecture decisions. Verified Service Lifetimes (PolicyEngine Singleton, others Scoped), Tenant Isolation (EF Core global filters), SignalR contract, Policy Engine extension points, AI Council state machine, Reviewer Lockout semantics (24h rolling window with pgBouncer-compatible advisory locks).

- **Phase 3 Cross-Agent Gap Review:** Identified 5 functional gaps in HubSpot integration (missing fields, wrong score mapping, incorrect company ID, missing ai_readiness capture, missing confidence_scores rubric). Fixed by Neo and Oracle in subsequent PRs.

- **Phase 4+5 Infrastructure & UI Review (PRs #15, #16):** Tank's Bicep: removed `@secure()` parameter defaults. Trinity's UI: merged after resolving test path fix, Moq dependency, and CSS law compliance verification. Final state: 79 passed tests, 0 errors.

- **Phase 6 Service Layer Architecture (PRs #17, #18):** Neo: moved `IClientService` from Infrastructure to Application layer (corrected dependency direction), fixed tenant assignment to use `_tenant.TenantId` not parameter. Oracle: YAML loader implementation verified clean. Final: 84 passed tests.

- **Phase 1 (2026-05-15): Security Review** → See main history section below.

## Key Lasting Learnings

1. **Interface Convention:** All service interfaces belong in `Application/Services/`. Dependency direction is always Infrastructure → Application.
2. **Tenant Assignment:** Always use `_tenant.TenantId` (same authoritative source as EF Core global filter), never method parameters.
3. **Advisory Locks:** `pg_advisory_xact_lock` with PostgreSQL `ISOLATION LEVEL SERIALIZABLE` handles race conditions on `reviewer_rejections` counts. Compatible with pgBouncer transaction pooling.
4. **Service Lifetimes:** PolicyEngine is Singleton (stateless, no DB). All others Scoped. HTTP clients (ISkillRunner, IHubSpotClient, etc.) are Transient via IHttpClientFactory with Polly pipelines.
5. **Bicep Security:** `@secure()` parameters must have no defaults — forces callers to supply values via params or secrets.
