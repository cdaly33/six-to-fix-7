# Neo Phase 2 Service Implementation Decisions

**Date:** 2026-05-10
**Author:** Neo (Backend Dev)

## Decisions Made

### D1: Oracle-owned interface stubs
ISkillRunner, IPolicyEngine, ICouncilRunner stubs created in Application/Services/ as thin interfaces.
Oracle MUST replace these with real implementations before integration. The stubs include SkillResult, PolicyEvaluationResult, CouncilResult record types.

### D2: CategoryId stored as string in CalibrationDelta and ReviewerAction
Matches the existing pattern in CategoryResult.Category (string). Using Guid.ToString() for the Guid-typed categoryId params.

### D3: ReviewerWorkflow lockout implementation
Uses serializable isolation + pg_advisory_xact_lock({hash}). Advisory lock key = categoryId.GetHashCode() * 31L + reviewerId.GetHashCode(). This matches the spec exactly.

### D4: AuditOrchestrator does NOT take IPublisher in constructor
Per the spec, Publisher is a separate service. Orchestrator only takes ISkillRunner, IPolicyEngine, ICouncilRunner, ITelemetryCollector, IHubContext<AuditRunHub>, SixToFixDbContext, ILogger, ITenantContext.

### D5: Oracle exception types detected by type name string
AuditOrchestrator detects SkillSchemaValidationException and SkillCircuitOpenException by ex.GetType().Name to avoid coupling to Oracle's assembly at compile time.

### D6: SignalR hub in Infrastructure.Hubs
AuditRunHub lives in SixToFix.Infrastructure.Hubs. Interface IAuditRunHubClient in SixToFix.Application.Hubs. These are stubs — Oracle may flesh out the hub with real client methods.

### D7: Dapper added and ASP.NET Core framework reference used for SignalR
Dapper was added to Infrastructure.csproj. Microsoft.AspNetCore.SignalR.Core 10.* was unavailable on NuGet, so Infrastructure uses FrameworkReference Include="Microsoft.AspNetCore.App" for SignalR server types.

### D8: BusinessServiceExtensions does NOT modify InfrastructureServiceExtensions
Per charter — DO NOT modify InfrastructureServiceExtensions. Stitching is a separate step for Morpheus.
