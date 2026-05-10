# Oracle Phase 2 AI Services — Decisions

**Date:** 2026-05-10  
**Author:** Oracle (AI & Integration Dev)

## D1: IRealtimeNotifier abstraction instead of IHubContext<AuditRunHub>

**Decision:** Added `IRealtimeNotifier` interface in `SixToFix.Application.Services` as the SignalR abstraction used by SkillRunner and CouncilRunner.

**Reason:** Infrastructure cannot reference SixToFix.Web (circular dependency). `IHubContext<AuditRunHub>` requires the hub type from the Web project. The concrete implementation of `IRealtimeNotifier` using `IHubContext<AuditRunHub>` must be registered in the Web project's DI setup (Program.cs).

**Action required:** Trinity or Neo must create `AuditRunHubNotifier : IRealtimeNotifier` in the Web project and register `services.AddScoped<IRealtimeNotifier, AuditRunHubNotifier>()` in Program.cs.

## D2: HubSpotEvent stub in Application.Models

**Decision:** Created `HubSpotEvent` record in `SixToFix.Application.Models` as a stub.

**Reason:** `HubSpotWorker` reads from `Channel<HubSpotEvent>`. Neo's `Publisher` writes to this channel. Since Neo's branch isn't merged, the stub allows Oracle's branch to compile independently. The record: `(Guid AuditRunId, string ClientSlug, string Tier, decimal CompositeScore)`.

**Action required:** Neo should verify the `HubSpotEvent` record matches their Publisher's write contract on merge.

## D3: Channel<HubSpotEvent> registered in AiServiceExtensions

**Decision:** `Channel<HubSpotEvent>` is registered as Singleton in `AiServiceExtensions.AddAiServices()`.

**Reason:** The channel must be a singleton shared between the writer (Publisher) and reader (HubSpotWorker). Since both AiServiceExtensions and BusinessServiceExtensions will be called from Program.cs, there is a risk of double registration.

**Action required:** Neo must NOT register `Channel<HubSpotEvent>` in BusinessServiceExtensions — Oracle owns this registration.

## D4: Skill definitions are hard-coded stubs

**Decision:** Skill definitions (name, system prompt, output schema) are hard-coded in `SkillRunner` as a static dictionary.

**Reason:** `docs/skills/` YAML files don't exist in Phase 2. Phase 3 should replace this with a YAML file loader using the `output_schema_pointer` frontmatter field.

## D5: SAS URI generation limitation

**Decision:** `AzureBlobStorageClient.GetSasUriAsync` throws `InvalidOperationException` when DefaultAzureCredential is used (token-based auth).

**Reason:** SAS generation with `GenerateSasUri` requires a `StorageSharedKeyCredential` or user delegation key. With DefaultAzureCredential, the `CanGenerateSasUri` property is false.

**Options:** (a) Use user delegation SAS via `GetUserDelegationKeyAsync` + `GenerateSasUri`, (b) Store a storage account key in Key Vault as a fallback for SAS operations only. Recommend option (a) for Phase 3.
