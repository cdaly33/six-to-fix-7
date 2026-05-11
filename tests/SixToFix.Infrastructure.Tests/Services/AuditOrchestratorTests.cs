using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Hubs;
using SixToFix.Application.Models;
using SixToFix.Application.Multitenancy;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Hubs;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for AuditOrchestrator using a real PostgreSQL database.
/// External AI dependencies (ISkillRunner, IPolicyEngine, ICouncilRunner, ITelemetryCollector,
/// SignalR hub) are fully mocked with NSubstitute.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuditOrchestratorTests : IntegrationTestBase
{
    private readonly AuditOrchestrator _sut;
    private readonly ISkillRunner _skillRunner;
    private readonly IPolicyEngine _policyEngine;
    private readonly ICouncilRunner _councilRunner;
    private readonly ITelemetryCollector _telemetryCollector;
    private readonly IHubContext<AuditRunHub, IAuditRunHubClient> _hubContext;

    private readonly Guid _tenantId = Guid.NewGuid();

    public AuditOrchestratorTests(PostgresContainerFixture fixture) : base(fixture)
    {
        _skillRunner = Substitute.For<ISkillRunner>();
        _policyEngine = Substitute.For<IPolicyEngine>();
        _councilRunner = Substitute.For<ICouncilRunner>();
        _telemetryCollector = Substitute.For<ITelemetryCollector>();

        // Mock the SignalR hub context — ReceiveEvent is fire-and-forget
        _hubContext = Substitute.For<IHubContext<AuditRunHub, IAuditRunHubClient>>();
        var hubClients = Substitute.For<IHubClients<IAuditRunHubClient>>();
        var hubClient = Substitute.For<IAuditRunHubClient>();
        _hubContext.Clients.Returns(hubClients);
        hubClients.Group(Arg.Any<string>()).Returns(hubClient);
        hubClient.ReceiveEvent(Arg.Any<string>(), Arg.Any<object>()).Returns(Task.CompletedTask);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(_tenantId);
        tenant.IsResolved.Returns(false);

        // Default: skills return a no-op result
        var fakeSkillRun = new SkillRun
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuditRunId = Guid.Empty,
            SkillName = "fake",
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var fakeSkillResult = new SkillRunResult(fakeSkillRun, JsonDocument.Parse("{}"), true, 100, 50);
        _skillRunner
            .ExecuteSkillAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<JsonDocument>(), Arg.Any<CancellationToken>())
            .Returns(fakeSkillResult);

        // Default: policy engine returns no flags
        _policyEngine
            .EvaluateCategory(Arg.Any<CategoryResultPayload>(), Arg.Any<PolicyEvaluationContext>())
            .Returns(Array.Empty<PolicyFlagModel>());
        _policyEngine.RequiresCouncilEscalation(Arg.Any<IReadOnlyList<PolicyFlagModel>>()).Returns(false);

        _sut = new AuditOrchestrator(
            _skillRunner,
            _policyEngine,
            _councilRunner,
            _telemetryCollector,
            _hubContext,
            NullLogger<AuditOrchestrator>.Instance,
            DbContext,
            tenant);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Seed the test Tenant once per test instance so all helpers can reference _tenantId
        DbContext.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = $"OrchestratorTenant-{_tenantId:N}",
            Slug = $"orchestrator-{_tenantId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // CreateAuditRunAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAuditRunAsync_ValidClient_CreatesAuditRun()
    {
        var (clientId, _) = await SeedClientWithAuditAsync();

        var auditRun = await _sut.CreateAuditRunAsync(clientId, Guid.NewGuid());

        auditRun.Should().NotBeNull();
        auditRun.Status.Should().Be("pending");
        auditRun.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task CreateAuditRunAsync_ClientNotFound_ThrowsClientNotFoundException()
    {
        var action = async () => await _sut.CreateAuditRunAsync(Guid.NewGuid(), Guid.NewGuid());

        await action.Should().ThrowAsync<ClientNotFoundException>();
    }

    [Fact]
    public async Task CreateAuditRunAsync_ActiveRunAlreadyExists_ThrowsAuditRunConflictException()
    {
        var (clientId, auditId) = await SeedClientWithAuditAsync();
        // Seed an active run
        DbContext.AuditRuns.Add(new AuditRun
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuditId = auditId,
            Status = "running",
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        var action = async () => await _sut.CreateAuditRunAsync(clientId, Guid.NewGuid());

        await action.Should().ThrowAsync<AuditRunConflictException>()
            .Where(ex => ex.ClientId == clientId);
    }

    // ──────────────────────────────────────────────────────────────
    // GetAuditRunAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditRunAsync_ExistingRun_ReturnsRun()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = Guid.NewGuid();
        DbContext.AuditRuns.Add(new AuditRun
        {
            Id = auditRunId,
            TenantId = _tenantId,
            AuditId = auditId,
            Status = "pending",
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        var result = await _sut.GetAuditRunAsync(auditRunId);

        result.Id.Should().Be(auditRunId);
    }

    [Fact]
    public async Task GetAuditRunAsync_MissingRun_ThrowsAuditRunNotFoundException()
    {
        var action = async () => await _sut.GetAuditRunAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<AuditRunNotFoundException>();
    }

    // ──────────────────────────────────────────────────────────────
    // StartAuditRunAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAuditRunAsync_PendingRun_TransitionsToAwaitingReview()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = await SeedPendingAuditRunAsync(auditId);

        await _sut.StartAuditRunAsync(auditRunId);

        DbContext.ChangeTracker.Clear();
        var auditRun = await DbContext.AuditRuns.FindAsync(auditRunId);
        auditRun!.Status.Should().Be("awaiting_review");
    }

    [Fact]
    public async Task StartAuditRunAsync_PendingRun_InitializesTelemetry()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = await SeedPendingAuditRunAsync(auditId);

        await _sut.StartAuditRunAsync(auditRunId);

        await _telemetryCollector.Received(1).InitializeTelemetryAsync(auditRunId, Arg.Any<CancellationToken>());
        await _telemetryCollector.Received(1).FinalizeTelemetryAsync(auditRunId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAuditRunAsync_PendingRun_ExecutesFiveSkills()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = await SeedPendingAuditRunAsync(auditId);

        await _sut.StartAuditRunAsync(auditRunId);

        await _skillRunner.Received(5).ExecuteSkillAsync(
            auditRunId,
            Arg.Any<string>(),
            Arg.Any<JsonDocument>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAuditRunAsync_PendingRun_SendsRunStartedAndCompletedHubEvents()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = await SeedPendingAuditRunAsync(auditId);

        await _sut.StartAuditRunAsync(auditRunId);

        var groupKey = auditRunId.ToString("N");
        var hubClient = _hubContext.Clients.Group(groupKey);
        await hubClient.Received().ReceiveEvent("run-started", Arg.Any<object>());
        await hubClient.Received().ReceiveEvent("run-completed", Arg.Any<object>());
    }

    [Fact]
    public async Task StartAuditRunAsync_RunNotPending_ThrowsInvalidAuditRunStateException()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = Guid.NewGuid();
        DbContext.AuditRuns.Add(new AuditRun
        {
            Id = auditRunId,
            TenantId = _tenantId,
            AuditId = auditId,
            Status = "running", // not pending
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        var action = async () => await _sut.StartAuditRunAsync(auditRunId);

        await action.Should().ThrowAsync<InvalidAuditRunStateException>()
            .Where(ex => ex.CurrentState == "running");
    }

    [Fact]
    public async Task StartAuditRunAsync_WithTriggerFlags_RunsCouncil()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = await SeedPendingAuditRunAsync(auditId);

        // Seed a CategoryResult so the policy loop has something to evaluate
        DbContext.CategoryResults.Add(new CategoryResult
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuditRunId = auditRunId,
            Category = "brand",
            ActivityScore = 8,
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Policy engine returns a Trigger flag → council should run
        var triggerFlag = new PolicyFlagModel("LOW_CONFIDENCE", "Trigger", null);
        _policyEngine
            .EvaluateCategory(Arg.Any<CategoryResultPayload>(), Arg.Any<PolicyEvaluationContext>())
            .Returns(new[] { triggerFlag });
        _policyEngine.RequiresCouncilEscalation(Arg.Any<IReadOnlyList<PolicyFlagModel>>()).Returns(true);

        var fakeDecision = new CouncilDecisionModel(
            Guid.NewGuid(), "uphold",
            new Dictionary<string, int>(),
            0.9m, "Council upheld the score", DateTimeOffset.UtcNow);
        _councilRunner
            .RunCouncilAsync(auditRunId, Arg.Any<Guid>(), Arg.Any<IReadOnlyList<PolicyFlagModel>>(), Arg.Any<CancellationToken>())
            .Returns(fakeDecision);

        await _sut.StartAuditRunAsync(auditRunId);

        await _councilRunner.Received(1).RunCouncilAsync(
            auditRunId,
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<PolicyFlagModel>>(),
            Arg.Any<CancellationToken>());
        await _telemetryCollector.Received(1).IncrementPolicyTriggerCountAsync(auditRunId, Arg.Any<CancellationToken>());
        await _telemetryCollector.Received(1).IncrementCouncilRunCountAsync(auditRunId, Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // MarkAuditRunFailedAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAuditRunFailedAsync_ExistingRun_SetsFailedStatus()
    {
        var (_, auditId) = await SeedClientWithAuditAsync();
        var auditRunId = await SeedPendingAuditRunAsync(auditId);

        await _sut.MarkAuditRunFailedAsync(auditRunId, "Skill timeout");

        DbContext.ChangeTracker.Clear();
        var auditRun = await DbContext.AuditRuns.FindAsync(auditRunId);
        auditRun!.Status.Should().Be("failed");
        auditRun.ErrorMessage.Should().Be("Skill timeout");
        auditRun.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAuditRunFailedAsync_NonExistentRun_DoesNotThrow()
    {
        // Silent no-op for non-existent runs (logged as warning)
        var action = async () => await _sut.MarkAuditRunFailedAsync(Guid.NewGuid(), "reason");

        await action.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // GetAuditRunsForClientAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditRunsForClientAsync_MultipleRuns_ReturnsAllDescending()
    {
        var (clientId, auditId) = await SeedClientWithAuditAsync();

        var run1Id = Guid.NewGuid();
        var run2Id = Guid.NewGuid();
        DbContext.AuditRuns.Add(new AuditRun { Id = run1Id, TenantId = _tenantId, AuditId = auditId, Status = "completed", StartedAt = DateTimeOffset.UtcNow.AddDays(-2), CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) });
        DbContext.AuditRuns.Add(new AuditRun { Id = run2Id, TenantId = _tenantId, AuditId = auditId, Status = "pending", StartedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow });
        await DbContext.SaveChangesAsync();

        var runs = await _sut.GetAuditRunsForClientAsync(clientId);

        runs.Should().HaveCount(2);
        runs[0].Id.Should().Be(run2Id); // most recent first
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private async Task<(Guid clientId, Guid auditId)> SeedClientWithAuditAsync()
    {
        var clientId = Guid.NewGuid();
        DbContext.Clients.Add(new Client
        {
            Id = clientId,
            TenantId = _tenantId,
            Name = "Acme Corp",
            Slug = $"acme-{clientId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var auditId = Guid.NewGuid();
        DbContext.Audits.Add(new Audit
        {
            Id = auditId,
            TenantId = _tenantId,
            ClientId = clientId,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await DbContext.SaveChangesAsync();
        return (clientId, auditId);
    }

    private async Task<Guid> SeedPendingAuditRunAsync(Guid auditId)
    {
        var auditRunId = Guid.NewGuid();
        DbContext.AuditRuns.Add(new AuditRun
        {
            Id = auditRunId,
            TenantId = _tenantId,
            AuditId = auditId,
            Status = "pending",
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();
        return auditRunId;
    }
}
