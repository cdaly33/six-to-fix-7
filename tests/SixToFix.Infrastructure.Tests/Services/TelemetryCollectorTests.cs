using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for TelemetryCollector against a real PostgreSQL database.
/// Each test is isolated via IntegrationTestBase transaction rollback.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TelemetryCollectorTests : IntegrationTestBase
{
    private readonly TelemetryCollector _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _auditRunId = Guid.NewGuid();

    public TelemetryCollectorTests(PostgresContainerFixture fixture) : base(fixture)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(_tenantId);
        tenant.IsResolved.Returns(false);

        _sut = new TelemetryCollector(DbContext, tenant, NullLogger<TelemetryCollector>.Instance);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedPrerequisitesAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // InitializeTelemetryAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeTelemetryAsync_NewAuditRun_PersistsTelemetryEvent()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);

        var stored = await DbContext.TelemetryEvents
            .FirstOrDefaultAsync(e => e.AuditRunId == _auditRunId);

        stored.Should().NotBeNull();
        stored!.AuditRunId.Should().Be(_auditRunId);
        stored.TenantId.Should().Be(_tenantId);
        stored.SkillRunCount.Should().Be(0);
        stored.CompletedAt.Should().BeNull();
        stored.InitializedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InitializeTelemetryAsync_CalledTwice_ThrowsTelemetryAlreadyInitializedException()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);

        var action = async () => await _sut.InitializeTelemetryAsync(_auditRunId);

        await action.Should().ThrowAsync<TelemetryAlreadyInitializedException>()
            .Where(ex => ex.AuditRunId == _auditRunId);
    }

    // ──────────────────────────────────────────────────────────────
    // IncrementSkillRunCountAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IncrementSkillRunCountAsync_UpdatesCountersCorrectly()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);

        await _sut.IncrementSkillRunCountAsync(_auditRunId, tokensUsed: 500, latencyMs: 120);
        await _sut.IncrementSkillRunCountAsync(_auditRunId, tokensUsed: 300, latencyMs: 80);

        // Reload from DB to bypass EF tracking cache
        DbContext.ChangeTracker.Clear();
        var stored = await DbContext.TelemetryEvents
            .FirstAsync(e => e.AuditRunId == _auditRunId);

        stored.SkillRunCount.Should().Be(2);
        stored.TotalTokensUsed.Should().Be(800);
        stored.TotalLatencyMs.Should().Be(200);
    }

    [Fact]
    public async Task IncrementSkillRunCountAsync_ZeroValues_IncrementsCountOnly()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);
        await _sut.IncrementSkillRunCountAsync(_auditRunId, tokensUsed: 0, latencyMs: 0);

        DbContext.ChangeTracker.Clear();
        var stored = await DbContext.TelemetryEvents
            .FirstAsync(e => e.AuditRunId == _auditRunId);

        stored.SkillRunCount.Should().Be(1);
        stored.TotalTokensUsed.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────────
    // IncrementPolicyTriggerCountAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IncrementPolicyTriggerCountAsync_IncrementsCount()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);
        await _sut.IncrementPolicyTriggerCountAsync(_auditRunId);
        await _sut.IncrementPolicyTriggerCountAsync(_auditRunId);

        DbContext.ChangeTracker.Clear();
        var stored = await DbContext.TelemetryEvents
            .FirstAsync(e => e.AuditRunId == _auditRunId);

        stored.PolicyTriggerCount.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────
    // IncrementCouncilRunCountAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IncrementCouncilRunCountAsync_IncrementsCount()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);
        await _sut.IncrementCouncilRunCountAsync(_auditRunId);

        DbContext.ChangeTracker.Clear();
        var stored = await DbContext.TelemetryEvents
            .FirstAsync(e => e.AuditRunId == _auditRunId);

        stored.CouncilRunCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────
    // FinalizeTelemetryAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FinalizeTelemetryAsync_SetsCompletedAt()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);
        await _sut.FinalizeTelemetryAsync(_auditRunId);

        DbContext.ChangeTracker.Clear();
        var stored = await DbContext.TelemetryEvents
            .FirstAsync(e => e.AuditRunId == _auditRunId);

        stored.CompletedAt.Should().NotBeNull();
        stored.CompletedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FinalizeTelemetryAsync_CalledTwice_DoesNotOverwriteCompletedAt()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);
        await _sut.FinalizeTelemetryAsync(_auditRunId);

        DbContext.ChangeTracker.Clear();
        var first = (await DbContext.TelemetryEvents
            .FirstAsync(e => e.AuditRunId == _auditRunId)).CompletedAt;

        // Wait briefly, then finalize again — the WHERE ... AND completed_at IS NULL prevents overwrite
        await Task.Delay(50);
        await _sut.FinalizeTelemetryAsync(_auditRunId);

        DbContext.ChangeTracker.Clear();
        var second = (await DbContext.TelemetryEvents
            .FirstAsync(e => e.AuditRunId == _auditRunId)).CompletedAt;

        second.Should().Be(first);
    }

    // ──────────────────────────────────────────────────────────────
    // Full lifecycle: init → increment → finalize
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullLifecycle_InitIncrementFinalize_RecordsAllCounters()
    {
        await _sut.InitializeTelemetryAsync(_auditRunId);
        await _sut.IncrementSkillRunCountAsync(_auditRunId, 1000, 200);
        await _sut.IncrementSkillRunCountAsync(_auditRunId, 800, 150);
        await _sut.IncrementSkillRunCountAsync(_auditRunId, 600, 100);
        await _sut.IncrementPolicyTriggerCountAsync(_auditRunId);
        await _sut.IncrementCouncilRunCountAsync(_auditRunId);
        await _sut.FinalizeTelemetryAsync(_auditRunId);

        DbContext.ChangeTracker.Clear();
        var stored = await DbContext.TelemetryEvents.FirstAsync(e => e.AuditRunId == _auditRunId);

        stored.SkillRunCount.Should().Be(3);
        stored.TotalTokensUsed.Should().Be(2400);
        stored.TotalLatencyMs.Should().Be(450);
        stored.PolicyTriggerCount.Should().Be(1);
        stored.CouncilRunCount.Should().Be(1);
        stored.CompletedAt.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private async Task SeedPrerequisitesAsync()
    {
        var tenant = new Tenant
        {
            Id = _tenantId,
            Name = "TelemetryTenant",
            Slug = $"telemetry-{_tenantId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Tenants.Add(tenant);

        var clientId = Guid.NewGuid();
        DbContext.Clients.Add(new Client { Id = clientId, TenantId = _tenantId, Name = "TelemetryClient", Slug = $"client-{clientId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

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

        DbContext.AuditRuns.Add(new AuditRun
        {
            Id = _auditRunId,
            TenantId = _tenantId,
            AuditId = auditId,
            Status = "running",
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await DbContext.SaveChangesAsync();
    }
}
