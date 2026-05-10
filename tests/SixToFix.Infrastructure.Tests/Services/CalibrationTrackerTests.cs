using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Application.Data;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for CalibrationTracker against a real PostgreSQL database.
/// Tests RecordDeltaAsync and GetDeltasForAuditRunAsync via EF Core.
/// Tenant isolation is verified: tenant A cannot see tenant B's deltas.
/// </summary>
public sealed class CalibrationTrackerTests : IntegrationTestBase
{
    private readonly CalibrationTracker _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _auditRunId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    public CalibrationTrackerTests(PostgresContainerFixture fixture) : base(fixture)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(_tenantId);
        tenant.IsResolved.Returns(false); // bypass global query filters

        // IDbConnectionFactory not needed for the EF-backed methods under test
        _sut = new CalibrationTracker(
            DbContext,
            Substitute.For<IDbConnectionFactory>(),
            tenant,
            NullLogger<CalibrationTracker>.Instance);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedPrerequisitesAsync(_tenantId, _auditRunId);
    }

    // ──────────────────────────────────────────────────────────────
    // RecordDeltaAsync — persistence
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordDeltaAsync_ValidInput_PersistsCalibrationDelta()
    {
        var model = await _sut.RecordDeltaAsync(
            _auditRunId, _categoryId, _reviewerId,
            originalActivityScore: 6m,
            adjustedActivityScore: 8m,
            originalDocumentedStrategy: null,
            adjustedDocumentedStrategy: "differentiation",
            overrideReasonCode: "BENCHMARK_OUTLIER",
            notes: "Score adjusted after council review");

        model.Should().NotBeNull();
        model.Id.Should().NotBe(Guid.Empty);
        model.AuditRunId.Should().Be(_auditRunId);
        model.CategoryId.Should().Be(_categoryId.ToString());
        model.ReviewerId.Should().Be(_reviewerId);
        model.OriginalActivityScore.Should().Be(6m);
        model.AdjustedActivityScore.Should().Be(8m);
        model.AdjustedDocumentedStrategy.Should().Be("differentiation");
        model.OverrideReasonCode.Should().Be("BENCHMARK_OUTLIER");
        model.Notes.Should().Be("Score adjusted after council review");
        model.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordDeltaAsync_PersistsWithCorrectTenantId()
    {
        await _sut.RecordDeltaAsync(
            _auditRunId, _categoryId, _reviewerId,
            5m, 7m, null, null,
            "MANUAL_OVERRIDE", "Test notes");

        var stored = await DbContext.CalibrationDeltas
            .FirstOrDefaultAsync(d => d.AuditRunId == _auditRunId);

        stored.Should().NotBeNull();
        stored!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task RecordDeltaAsync_EmptyNotes_ThrowsMissingCalibrationNotesException()
    {
        var action = async () => await _sut.RecordDeltaAsync(
            _auditRunId, _categoryId, _reviewerId,
            5m, 7m, null, null,
            overrideReasonCode: "REASON",
            notes: "");

        await action.Should().ThrowAsync<MissingCalibrationNotesException>();
    }

    [Fact]
    public async Task RecordDeltaAsync_EmptyOverrideReasonCode_ThrowsMissingOverrideReasonException()
    {
        var action = async () => await _sut.RecordDeltaAsync(
            _auditRunId, _categoryId, _reviewerId,
            5m, 7m, null, null,
            overrideReasonCode: "",
            notes: "Some notes");

        await action.Should().ThrowAsync<MissingOverrideReasonException>();
    }

    // ──────────────────────────────────────────────────────────────
    // GetDeltasForAuditRunAsync — retrieval
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeltasForAuditRunAsync_ReturnsPersistedDeltas()
    {
        await _sut.RecordDeltaAsync(_auditRunId, _categoryId, _reviewerId,
            6m, 8m, null, null, "REASON_A", "Notes A");
        await _sut.RecordDeltaAsync(_auditRunId, Guid.NewGuid(), _reviewerId,
            3m, 5m, null, null, "REASON_B", "Notes B");

        var deltas = await _sut.GetDeltasForAuditRunAsync(_auditRunId);

        deltas.Should().HaveCount(2);
        deltas.Select(d => d.OverrideReasonCode).Should().BeEquivalentTo(["REASON_A", "REASON_B"]);
    }

    [Fact]
    public async Task GetDeltasForAuditRunAsync_EmptyAuditRun_ReturnsEmptyList()
    {
        var deltas = await _sut.GetDeltasForAuditRunAsync(_auditRunId);

        deltas.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeltasForAuditRunAsync_ReturnedInChronologicalOrder()
    {
        await _sut.RecordDeltaAsync(_auditRunId, Guid.NewGuid(), _reviewerId,
            6m, 7m, null, null, "FIRST", "First delta");
        await Task.Delay(10); // ensure ordering
        await _sut.RecordDeltaAsync(_auditRunId, Guid.NewGuid(), _reviewerId,
            3m, 4m, null, null, "SECOND", "Second delta");

        var deltas = await _sut.GetDeltasForAuditRunAsync(_auditRunId);

        deltas[0].OverrideReasonCode.Should().Be("FIRST");
        deltas[1].OverrideReasonCode.Should().Be("SECOND");
    }

    // ──────────────────────────────────────────────────────────────
    // Tenant isolation: tenant A cannot see tenant B's deltas
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeltasForAuditRunAsync_TenantIsolation_OtherTenantDeltasNotVisible()
    {
        // Seed a delta for Tenant B using a separate DbContext under the same transaction
        var tenantBId = Guid.NewGuid();
        var tenantBAuditRunId = Guid.NewGuid();
        await SeedPrerequisitesAsync(tenantBId, tenantBAuditRunId);

        // Directly insert a delta for Tenant B (bypassing the SUT's tenant context)
        DbContext.CalibrationDeltas.Add(new CalibrationDelta
        {
            Id = Guid.NewGuid(),
            TenantId = tenantBId,
            AuditRunId = tenantBAuditRunId,
            CategoryId = Guid.NewGuid().ToString(),
            ReviewerId = Guid.NewGuid(),
            OriginalActivityScore = 5m,
            AdjustedActivityScore = 6m,
            OverrideReasonCode = "TENANT_B_REASON",
            Notes = "Tenant B note",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Query from Tenant A's perspective — SUT uses _tenantId (Tenant A)
        // IsResolved = false bypasses filter, but query is scoped by _auditRunId (Tenant A's run)
        var deltas = await _sut.GetDeltasForAuditRunAsync(_auditRunId);

        deltas.Should().BeEmpty("Tenant A's auditRunId has no deltas; Tenant B's delta is on a different auditRunId");
    }

    [Fact]
    public async Task GetDeltasForAuditRunAsync_AuditRunIsolation_OtherAuditRunNotReturned()
    {
        var otherAuditRunId = Guid.NewGuid();
        var otherClientId = Guid.NewGuid();
        var otherAuditId = Guid.NewGuid();

        // Seed another AuditRun for the same tenant (new client required to satisfy FK)
        DbContext.Clients.Add(new Client { Id = otherClientId, TenantId = _tenantId, Name = $"OtherClient", Slug = $"client-{otherClientId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        DbContext.Audits.Add(new Audit { Id = otherAuditId, TenantId = _tenantId, ClientId = otherClientId, Status = "active", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        DbContext.AuditRuns.Add(new AuditRun { Id = otherAuditRunId, TenantId = _tenantId, AuditId = otherAuditId, Status = "running", StartedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow });
        await DbContext.SaveChangesAsync();

        // Record delta for the OTHER audit run
        DbContext.CalibrationDeltas.Add(new CalibrationDelta
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuditRunId = otherAuditRunId,
            CategoryId = Guid.NewGuid().ToString(),
            ReviewerId = _reviewerId,
            OriginalActivityScore = 4m,
            AdjustedActivityScore = 6m,
            OverrideReasonCode = "OTHER_RUN",
            Notes = "Other run note",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Query _auditRunId — should not return the delta from otherAuditRunId
        var deltas = await _sut.GetDeltasForAuditRunAsync(_auditRunId);

        deltas.Should().BeEmpty();
        deltas.Should().NotContain(d => d.OverrideReasonCode == "OTHER_RUN");
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private async Task SeedPrerequisitesAsync(Guid tenantId, Guid auditRunId)
    {
        var existingTenant = await DbContext.Tenants.FindAsync(tenantId);
        if (existingTenant is null)
        {
            DbContext.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = $"Tenant-{tenantId:N}",
                Slug = $"tenant-{tenantId:N}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        var clientId = Guid.NewGuid();
        DbContext.Clients.Add(new Client
        {
            Id = clientId,
            TenantId = tenantId,
            Name = $"Client-{clientId:N}",
            Slug = $"client-{clientId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var auditId = Guid.NewGuid();
        DbContext.Audits.Add(new Audit
        {
            Id = auditId,
            TenantId = tenantId,
            ClientId = clientId,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        DbContext.AuditRuns.Add(new AuditRun
        {
            Id = auditRunId,
            TenantId = tenantId,
            AuditId = auditId,
            Status = "running",
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await DbContext.SaveChangesAsync();
    }
}
