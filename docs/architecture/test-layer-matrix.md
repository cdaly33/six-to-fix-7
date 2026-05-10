# Test Layer Matrix

> **Status: LOCKED — Phase 0 Planning Artifact**
>
> Owner: Tank (DevOps & QA) | Phase: 0 | Last updated: 2026-05-10

This document defines the complete test responsibility matrix for the Six-to-Fix platform. Every test written must conform to this contract. Violations (e.g., mocking PostgreSQL in an integration test, calling real Azure OpenAI in CI) are treated as defects.

---

## 1. Layer Responsibility Table

| Layer | What It Tests | Mocked | Real | Project | Coverage Scope | Runs In |
|-------|--------------|--------|------|---------|----------------|---------|
| **Unit** | Domain services, scoring algorithms, policy engine, reviewer lockout state machine, JWT claim parsing, Polly pipeline configuration, HubSpot HMAC validation logic | Azure OpenAI (`ISkillRunner`), HubSpot API (`IHubSpotClient`), Blob Storage (`IBlobService`), `IRepository<T>` abstractions, `ISkillRunner`, all external HTTP | Domain objects, business rules, value objects, data annotations | `SixToFix.Domain.Tests` | `SixToFix.Domain`, `SixToFix.Application` | `ci.yml` (PR gate) |
| **Component / bUnit** | Blazor components: rendering, event handling, parameter binding, SignalR hub interaction (mocked), authorization attributes on components | All services injected into components, `IHubContext`, SignalR hub | Component render tree, parameter/event contracts | `SixToFix.Blazor.Tests` | Blazor component public surface | `ci.yml` (PR gate) |
| **Integration / Testcontainers** | EF Core repositories, `DbContext` queries, tenant isolation enforcement, migration correctness, `category_result_versions` append-only behavior | Azure OpenAI, HubSpot API, Blob Storage | **Real PostgreSQL 16** via Testcontainer, EF Core migrations, `sf_app` + `sf_admin` roles, all 15 DB tables | `SixToFix.Infrastructure.Tests` | `SixToFix.Infrastructure` (repositories, EF Core) | `integration.yml` (push to main) |
| **API Integration / WebApplicationFactory** | All REST endpoints: HTTP status codes, request/response shapes, JWT auth enforcement, multi-tenant scoping via `tenant_id` claim, 409 REVIEWER_REJECTION_LOCKOUT, 502 AI schema failure | Azure OpenAI (`ISkillRunner` stub returning canned responses), HubSpot API, Blob Storage (`IBlobService` stub), real external HTTP calls | **Real PostgreSQL 16** via Testcontainer, EF Core, JWT token generation, ASP.NET Core middleware pipeline, SignalR hub negotiation | `SixToFix.Api.Tests` | All API controllers, middleware, auth policies | `integration.yml` (push to main) |
| **E2E / Playwright** | Full user journeys: login, audit creation, AI skill chain execution, reviewer workflow, lockout scenario, HubSpot sync status display | Nothing — runs against fully deployed staging environment | Deployed app, real (dev) database, real Azure OpenAI, real HubSpot sandbox | `SixToFix.E2E` | Critical user paths (happy path + key error paths) | `e2e.yml` (merge to main only) |

---

## 2. Mock Boundary Rules

### 2.1 ALWAYS Mocked

The following are **never real** in unit or component tests. They are always replaced by test doubles (interfaces + in-memory fakes or Moq/NSubstitute mocks).

| Boundary | Interface | Reason |
|----------|-----------|--------|
| Azure OpenAI AI skill chain | `ISkillRunner` | Non-deterministic, costly, slow, has rate limits |
| HubSpot API (outbound) | `IHubSpotClient` | External dependency; requires sandbox credentials |
| Azure Blob Storage | `IBlobService` | External dependency; not relevant to domain logic |
| Azure AI Search | `ISearchService` | External; search relevance is not unit-testable |
| Email / notification sending | `INotificationService` | External SMTP/webhook |
| `System.DateTime` / `DateTimeOffset` | `ISystemClock` | Must be deterministic in tests (lockout window logic depends on this) |
| Background Channel workers (HubSpot sync) | _(use `Channel<T>` with in-memory queue)_ | Full pipeline tested in integration layer |

### 2.2 NEVER Mocked

The following are **always real** when tested at the appropriate layer. Using a mock here defeats the purpose of the test.

| Boundary | Layer Where Real | Reason |
|----------|-----------------|--------|
| PostgreSQL database | Integration, API Integration | SQLite cannot replicate PostgreSQL-specific behavior (row-level security, advisory locks, `uuid_generate_v4()`, `jsonb`, `timestamptz`) |
| `tenant_id` isolation logic | Integration, API Integration | The entire security guarantee — must be tested with real queries against real data |
| JWT claim parsing | Unit (real `System.IdentityModel` code) | Parsing correctness is not environment-dependent; mock would bypass the actual parser |
| EF Core query translation | Integration | ORM-to-SQL translation has bugs that only appear with a real DB |
| ASP.NET Core middleware pipeline | API Integration | Authorization, routing, model binding must be exercised end-to-end |
| Polly retry/circuit-breaker policies | Unit (real Polly policies, mocked HTTP handler) | Policy configuration correctness must be verified with real Polly objects |
| Reviewer lockout state machine | Unit | Core domain logic — must not be mocked |
| HubSpot HMAC-SHA256 validation | Unit | Cryptographic correctness — must run the real algorithm |

---

## 3. Project Structure

```
tests/
├── SixToFix.Domain.Tests/           # xUnit — Unit tests for domain + application layer
│   ├── Scoring/                     # Scoring algorithm tests
│   ├── PolicyEngine/                # Policy flag evaluation tests
│   ├── ReviewerLockout/             # 3-rejection / 24h lockout state machine
│   ├── SkillChain/                  # ISkillRunner mock-based tests, Polly pipeline
│   ├── HubSpot/                     # HMAC-SHA256 webhook signature validation
│   └── Jwt/                         # JWT claim parsing and generation
│
├── SixToFix.Infrastructure.Tests/   # xUnit + Testcontainers — DB integration tests
│   ├── Fixtures/
│   │   └── PostgresContainerFixture.cs   # Shared Testcontainer lifecycle
│   ├── Repositories/                # EF Core repository tests (all 15 tables)
│   ├── TenantIsolation/             # Cross-tenant data access tests (REQUIRED)
│   ├── Migrations/                  # Migration idempotency tests
│   └── AppendOnly/                  # category_result_versions immutability tests
│
├── SixToFix.Api.Tests/              # xUnit + WebApplicationFactory — API integration tests
│   ├── Fixtures/
│   │   └── ApiTestFixture.cs        # WebApplicationFactory + Testcontainer wiring
│   ├── Endpoints/                   # One file per controller: audits, runs, policies, etc.
│   ├── Auth/                        # JWT enforcement, tenant claim scoping
│   ├── SignalR/                     # Hub negotiation endpoint tests
│   └── Webhooks/                    # HubSpot inbound webhook (HMAC validation)
│
├── SixToFix.Blazor.Tests/           # bUnit — Blazor component tests
│   ├── Components/                  # Per-component render + event tests
│   ├── Auth/                        # AuthorizeView rendering tests
│   └── SignalR/                     # AuditRunProgressHub interaction (mocked IHubContext)
│
└── SixToFix.E2E/                    # Playwright — End-to-end tests
    ├── Fixtures/
    │   └── PlaywrightFixture.cs     # Browser setup, base URL from env
    ├── Journeys/                    # Full user journeys
    │   ├── LoginJourney.cs
    │   ├── AuditCreationJourney.cs
    │   ├── AuditRunJourney.cs       # Includes SignalR progress updates
    │   ├── ReviewerWorkflowJourney.cs
    │   ├── ReviewerLockoutJourney.cs
    │   └── HubSpotSyncJourney.cs
    └── playwright.config.ts
```

### 3.1 Test Project Dependencies

```
SixToFix.Domain.Tests         → SixToFix.Domain, SixToFix.Application
SixToFix.Infrastructure.Tests → SixToFix.Infrastructure, SixToFix.Domain
SixToFix.Api.Tests            → SixToFix.Api (full app), SixToFix.Infrastructure
SixToFix.Blazor.Tests         → SixToFix.Blazor (component library)
SixToFix.E2E                  → No project references — tests the deployed app via HTTP
```

---

## 4. Coverage Gate

### 4.1 Gate Definition

- **Target:** 80% line coverage on `SixToFix.Domain` and `SixToFix.Application` assemblies.
- **Enforcement:** `ci.yml` coverage-gate job uses `dotnet-coverage` + ReportGenerator. If coverage drops below 80%, the PR check fails.
- **Format:** Cobertura XML → uploaded as artifact + posted as PR comment.

### 4.2 Coverage Tool Configuration

```xml
<!-- Directory.Build.props (test projects) -->
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <CoverletOutput>./TestResults/coverage.xml</CoverletOutput>
  <Include>[SixToFix.Domain]*,[SixToFix.Application]*</Include>
  <Exclude>[*.Tests]*,[SixToFix.E2E]*</Exclude>
  <Threshold>80</Threshold>
  <ThresholdType>line</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

### 4.3 Included / Excluded Projects

| Assembly | Coverage Measured | Reason |
|----------|------------------|--------|
| `SixToFix.Domain` | ✅ Yes | Core business rules — primary coverage target |
| `SixToFix.Application` | ✅ Yes | Application services — orchestration logic |
| `SixToFix.Infrastructure` | ❌ No | Repository implementations — tested via integration tests, not line coverage |
| `SixToFix.Api` | ❌ No | Controllers — tested via API integration tests |
| `SixToFix.Blazor` | ❌ No | UI components — tested via bUnit, not line coverage gate |
| `*.Tests` | ❌ No | Test projects excluded from measurement |
| `SixToFix.E2E` | ❌ No | E2E — no coverage instrumentation |

---

## 5. Testcontainers Pattern

### 5.1 Base Class for Integration Tests

All tests in `SixToFix.Infrastructure.Tests` and `SixToFix.Api.Tests` that need a real PostgreSQL database must inherit from or use the shared fixture below.

```csharp
// tests/SixToFix.Infrastructure.Tests/Fixtures/PostgresContainerFixture.cs
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("strategicglue_test")
        .WithUsername("sf_admin")       // Admin role for migrations
        .WithPassword("test_admin_pw")
        .WithPortBinding(5432, true)    // Random host port — no conflicts between parallel runs
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public string AppConnectionString =>
        _container.GetConnectionString()
            .Replace("Username=sf_admin", "Username=sf_app")
            .Replace("Password=test_admin_pw", "Password=test_app_pw");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplyMigrationsAsync();
        await CreateAppRoleAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        // Run EF Core migrations using sf_admin credentials
        var optionsBuilder = new DbContextOptionsBuilder<SixToFixDbContext>();
        optionsBuilder.UseNpgsql(AdminConnectionString);
        await using var context = new SixToFixDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync();
    }

    private async Task CreateAppRoleAsync()
    {
        // Create sf_app role with DML-only permissions (mirrors prod init-db.sh)
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'sf_app') THEN
                    CREATE ROLE sf_app WITH LOGIN PASSWORD 'test_app_pw';
                END IF;
            END $$;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sf_app;
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO sf_app;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
```

### 5.2 Collection Fixture Registration

```csharp
[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgresContainerFixture>;

// Usage in test class:
[Collection("PostgreSQL")]
public sealed class AuditRepositoryTests(PostgresContainerFixture db)
{
    // db.AppConnectionString for runtime queries
    // db.AdminConnectionString for test setup only
}
```

### 5.3 DB Role Rules in Tests

| Operation | Role Used | Reason |
|-----------|-----------|--------|
| `MigrateAsync()` in fixture setup | `sf_admin` | DDL requires admin privileges |
| `DbContext` in test body (runtime queries) | `sf_app` | Mirrors production — only DML allowed |
| Direct test data seeding (INSERT) | `sf_app` | Runtime inserts use the app role |
| Schema assertions in migration tests | `sf_admin` | Reading `pg_catalog` requires admin |

---

## 6. Tenant Isolation Test Rule

**This is a mandatory test pattern.** Every repository integration test file that reads data must include at least one cross-tenant assertion.

### 6.1 Required Pattern

```csharp
[Fact]
public async Task GetAuditsForTenant_CannotReturnDataBelongingToAnotherTenant()
{
    // Arrange — two separate tenants with separate data
    var tenantA = Guid.NewGuid();
    var tenantB = Guid.NewGuid();

    await SeedTenantAsync(tenantA, auditCount: 3);
    await SeedTenantAsync(tenantB, auditCount: 2);

    var repo = new AuditRepository(BuildContextForTenant(tenantA));

    // Act — query as tenant A
    var results = await repo.GetAllAsync(tenantA, CancellationToken.None);

    // Assert — tenant A sees only its own audits; never tenant B's
    Assert.Equal(3, results.Count);
    Assert.All(results, a => Assert.Equal(tenantA, a.TenantId));
    Assert.DoesNotContain(results, a => a.TenantId == tenantB);
}
```

### 6.2 Rule Enforcement

- Every `IRepository<T>` implementation must have a corresponding cross-tenant test.
- The `ci.yml` coverage gate indirectly enforces this: uncovered `tenant_id` filter paths will drop coverage below 80%.
- Code reviews (Tank) will reject repository PRs that lack cross-tenant assertions.

---

## 7. Multi-Workflow Trigger Table

| Test Type | `ci.yml` (PR gate) | `integration.yml` (push to main) | `e2e.yml` (merge to main) | `deploy.yml` (merge to main) |
|-----------|-------------------|----------------------------------|--------------------------|------------------------------|
| Unit tests (`SixToFix.Domain.Tests`) | ✅ Runs | ❌ | ❌ | ❌ |
| bUnit component tests (`SixToFix.Blazor.Tests`) | ✅ Runs | ❌ | ❌ | ❌ |
| Coverage gate (80% on Domain + Application) | ✅ Enforced | ❌ | ❌ | ❌ |
| Lint (`dotnet format --verify-no-changes`) | ✅ Runs | ❌ | ❌ | ❌ |
| Security scan (dependency audit) | ✅ Runs | ❌ | ❌ | ❌ |
| Integration tests — repositories (`SixToFix.Infrastructure.Tests`) | ❌ | ✅ Runs | ❌ | ❌ |
| API integration tests (`SixToFix.Api.Tests`) | ❌ | ✅ Runs | ❌ | ❌ |
| Coverage report upload | ❌ | ✅ Uploaded | ❌ | ❌ |
| Playwright E2E (`SixToFix.E2E`) | ❌ | ❌ | ✅ Runs | ❌ |
| Health check (post-deploy) | ❌ | ❌ | ❌ | ✅ Runs |

---

## ⚠️ Open Questions

1. **bUnit + SignalR:** Confirm that `IHubContext<AuditRunHub>` can be injected as a Moq mock in bUnit without triggering real WebSocket negotiation. Expect yes — bUnit uses a fake JSInterop runtime.
2. **API integration test database isolation:** Each `WebApplicationFactory` test class will spin up its own Testcontainer (or share a collection fixture). Confirm preferred approach: shared container with per-test transaction rollback, or separate container per class. **Recommended:** shared container + `IDbContextTransaction` rollback per test to reduce container startup overhead.
3. **Playwright staging URL:** `e2e.yml` needs a deployed staging URL. Confirm whether E2E runs against dev environment (`app-strategicglue-dev`) or a dedicated staging slot.
4. **Coverage on `SixToFix.Application`:** If the Application layer is thin (just orchestration calling Domain services), 80% may be trivially met. Confirm the Application layer contains enough logic to warrant the gate (e.g., command handlers, validators).
