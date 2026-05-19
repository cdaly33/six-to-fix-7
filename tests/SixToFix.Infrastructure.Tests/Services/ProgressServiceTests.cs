using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Application.Multitenancy;
using SixToFix.Domain.Enums;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for ProgressService using a real PostgreSQL database.
/// Tests cover: GetForUser, GetForUserPillar, SetPercent (insert + update),
/// percent clamping, average calculation, and multi-tenant isolation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProgressServiceTests : IntegrationTestBase
{
    private ProgressService _sut = null!;
    private ITenantContext _tenantContext = null!;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _userBId = Guid.NewGuid();

    public ProgressServiceTests(PostgresContainerFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.IsResolved.Returns(false); // bypass global query filter

        _sut = new ProgressService(DbContext, _tenantContext, NullLogger<ProgressService>.Instance);

        DbContext.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = $"Tenant-A-{_tenantId:N}",
            Slug = $"tenant-a-{_tenantId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        DbContext.Tenants.Add(new Tenant
        {
            Id = _tenantBId,
            Name = $"Tenant-B-{_tenantBId:N}",
            Slug = $"tenant-b-{_tenantBId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // GetForUserAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForUserAsync_NoRows_ReturnsEmpty()
    {
        var result = await _sut.GetForUserAsync(_userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForUserAsync_AfterSetPercent_ReturnsThatRow()
    {
        await _sut.SetPercentAsync(_userId, Pillar.Brand, 50);

        var result = await _sut.GetForUserAsync(_userId);

        result.Should().HaveCount(1);
        result[0].Pillar.Should().Be(Pillar.Brand);
        result[0].PercentComplete.Should().Be(50);
    }

    // ──────────────────────────────────────────────────────────────
    // GetForUserPillarAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForUserPillarAsync_NoRow_ReturnsNull()
    {
        var result = await _sut.GetForUserPillarAsync(_userId, Pillar.Customer);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetForUserPillarAsync_ExistingRow_ReturnsIt()
    {
        await _sut.SetPercentAsync(_userId, Pillar.Customer, 75);

        var result = await _sut.GetForUserPillarAsync(_userId, Pillar.Customer);

        result.Should().NotBeNull();
        result!.PercentComplete.Should().Be(75);
    }

    // ──────────────────────────────────────────────────────────────
    // SetPercentAsync — insert vs update
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetPercentAsync_NewRow_CreatesWithCorrectTenantId()
    {
        var result = await _sut.SetPercentAsync(_userId, Pillar.Offering, 30);

        result.TenantId.Should().Be(_tenantId);
        result.UserId.Should().Be(_userId);
        result.Pillar.Should().Be(Pillar.Offering);
        result.PercentComplete.Should().Be(30);
    }

    [Fact]
    public async Task SetPercentAsync_ExistingRow_UpdatesPercent()
    {
        await _sut.SetPercentAsync(_userId, Pillar.Sales, 20);
        DbContext.ChangeTracker.Clear();

        var updated = await _sut.SetPercentAsync(_userId, Pillar.Sales, 60);

        updated.PercentComplete.Should().Be(60);
    }

    [Fact]
    public async Task SetPercentAsync_ExistingRow_UpdatesLastActivityAt()
    {
        var first = await _sut.SetPercentAsync(_userId, Pillar.Management, 10);
        DbContext.ChangeTracker.Clear();
        await Task.Delay(10);

        var second = await _sut.SetPercentAsync(_userId, Pillar.Management, 20);

        second.LastActivityAt.Should().BeOnOrAfter(first.LastActivityAt);
    }

    [Fact]
    public async Task SetPercentAsync_DoesNotCreateDuplicates()
    {
        await _sut.SetPercentAsync(_userId, Pillar.Communication, 10);
        DbContext.ChangeTracker.Clear();
        await _sut.SetPercentAsync(_userId, Pillar.Communication, 20);
        DbContext.ChangeTracker.Clear();

        var rows = await _sut.GetForUserAsync(_userId);

        rows.Where(r => r.Pillar == Pillar.Communication).Should().HaveCount(1);
    }

    // ──────────────────────────────────────────────────────────────
    // Percent clamping
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetPercentAsync_AboveOneHundred_ClampsToOneHundred()
    {
        var result = await _sut.SetPercentAsync(_userId, Pillar.Brand, 150);

        result.PercentComplete.Should().Be(100);
    }

    [Fact]
    public async Task SetPercentAsync_BelowZero_ClampsToZero()
    {
        var result = await _sut.SetPercentAsync(_userId, Pillar.Brand, -5);

        result.PercentComplete.Should().Be(0);
    }

    [Fact]
    public async Task SetPercentAsync_ExactlyZero_Accepted()
    {
        var result = await _sut.SetPercentAsync(_userId, Pillar.Brand, 0);

        result.PercentComplete.Should().Be(0);
    }

    [Fact]
    public async Task SetPercentAsync_ExactlyOneHundred_Accepted()
    {
        var result = await _sut.SetPercentAsync(_userId, Pillar.Brand, 100);

        result.PercentComplete.Should().Be(100);
    }

    // ──────────────────────────────────────────────────────────────
    // GetAverageForUserAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAverageForUserAsync_NoRows_ReturnsZero()
    {
        var result = await _sut.GetAverageForUserAsync(_userId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetAverageForUserAsync_AllSixPillarsAtHundred_ReturnsHundred()
    {
        foreach (var pillar in Enum.GetValues<Pillar>())
            await _sut.SetPercentAsync(_userId, pillar, 100);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetAverageForUserAsync(_userId);

        result.Should().Be(100);
    }

    [Fact]
    public async Task GetAverageForUserAsync_TwoPillarsAt60_AveragesOverSix()
    {
        // 60 + 60 + 0 + 0 + 0 + 0 = 120 / 6 = 20
        await _sut.SetPercentAsync(_userId, Pillar.Brand, 60);
        await _sut.SetPercentAsync(_userId, Pillar.Customer, 60);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetAverageForUserAsync(_userId);

        result.Should().Be(20);
    }

    // ──────────────────────────────────────────────────────────────
    // Multi-tenant isolation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForUserAsync_UserBelongsToTenantB_DoesNotLeakToTenantA()
    {
        // Seed a row for tenant B's user directly
        DbContext.UserPillarProgresses.Add(new UserPillarProgress
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantBId,
            UserId = _userBId,
            Pillar = Pillar.Brand,
            PercentComplete = 99,
            LastActivityAt = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Query for tenant A's user
        var result = await _sut.GetForUserAsync(_userId);

        result.Should().BeEmpty();
        result.Should().NotContain(r => r.UserId == _userBId);
    }
}
