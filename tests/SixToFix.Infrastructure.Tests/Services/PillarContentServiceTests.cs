using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Domain.Enums;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for PillarContentService using a real PostgreSQL database.
/// Tests cover: basic CRUD, lazy placeholder seeding, upsert semantics,
/// and multi-tenant isolation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PillarContentServiceTests : IntegrationTestBase
{
    private PillarContentService _sut = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PillarContentServiceTests(PostgresContainerFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _sut = new PillarContentService(DbContext, NullLogger<PillarContentService>.Instance);

        // Seed the two tenants referenced by tests
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
    // GetForTenantAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForTenantAsync_NoRows_ReturnsNull()
    {
        var result = await _sut.GetForTenantAsync(_tenantId, Pillar.Brand);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetForTenantAsync_ExistingRow_ReturnsThatRow()
    {
        await _sut.UpsertAsync(_tenantId, Pillar.Customer, """{"hello":"world"}""", _userId);

        var result = await _sut.GetForTenantAsync(_tenantId, Pillar.Customer);

        result.Should().NotBeNull();
        result!.Pillar.Should().Be(Pillar.Customer);
        result.TenantId.Should().Be(_tenantId);
    }

    // ──────────────────────────────────────────────────────────────
    // GetAllForTenantAsync — lazy seeding
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllForTenantAsync_NoExistingRows_SeedsAllSixPillars()
    {
        var result = await _sut.GetAllForTenantAsync(_tenantId);

        result.Should().HaveCount(6);
        result.Select(r => r.Pillar).Should().BeEquivalentTo(Enum.GetValues<Pillar>());
    }

    [Fact]
    public async Task GetAllForTenantAsync_AllSeededWithCorrectTenantId()
    {
        var result = await _sut.GetAllForTenantAsync(_tenantId);

        result.Should().OnlyContain(r => r.TenantId == _tenantId);
    }

    [Fact]
    public async Task GetAllForTenantAsync_PlaceholderBodyJson_IsSet()
    {
        var result = await _sut.GetAllForTenantAsync(_tenantId);

        result.Should().OnlyContain(r => r.BodyJson == """{"placeholder":true}""");
    }

    [Fact]
    public async Task GetAllForTenantAsync_CalledTwice_DoesNotDuplicateRows()
    {
        await _sut.GetAllForTenantAsync(_tenantId);
        DbContext.ChangeTracker.Clear();
        var result = await _sut.GetAllForTenantAsync(_tenantId);

        result.Should().HaveCount(6);
    }

    [Fact]
    public async Task GetAllForTenantAsync_PartiallySeeded_OnlySeedsMissing()
    {
        // Pre-seed 3 pillars
        await _sut.UpsertAsync(_tenantId, Pillar.Brand, """{"seeded":true}""", _userId);
        await _sut.UpsertAsync(_tenantId, Pillar.Customer, """{"seeded":true}""", _userId);
        await _sut.UpsertAsync(_tenantId, Pillar.Sales, """{"seeded":true}""", _userId);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetAllForTenantAsync(_tenantId);

        result.Should().HaveCount(6);
        result.Single(r => r.Pillar == Pillar.Brand).BodyJson.Should().Be("""{"seeded":true}""");
        result.Single(r => r.Pillar == Pillar.Offering).BodyJson.Should().Be("""{"placeholder":true}""");
    }

    // ──────────────────────────────────────────────────────────────
    // UpsertAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_NewPillar_InsertsRow()
    {
        var result = await _sut.UpsertAsync(
            _tenantId, Pillar.Management, """{"foo":"bar"}""", _userId);

        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.TenantId.Should().Be(_tenantId);
        result.Pillar.Should().Be(Pillar.Management);
        result.BodyJson.Should().Be("""{"foo":"bar"}""");
        result.UpdatedByUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task UpsertAsync_ExistingPillar_UpdatesBodyJson()
    {
        await _sut.UpsertAsync(_tenantId, Pillar.Sales, """{"v":1}""", _userId);
        DbContext.ChangeTracker.Clear();

        var updated = await _sut.UpsertAsync(_tenantId, Pillar.Sales, """{"v":2}""", _userId);

        updated.BodyJson.Should().Be("""{"v":2}""");
    }

    [Fact]
    public async Task UpsertAsync_ExistingPillar_UpdatesUpdatedAt()
    {
        var first = await _sut.UpsertAsync(_tenantId, Pillar.Offering, """{"v":1}""", _userId);
        DbContext.ChangeTracker.Clear();
        await Task.Delay(10); // ensure time advances

        var second = await _sut.UpsertAsync(_tenantId, Pillar.Offering, """{"v":2}""", _userId);

        second.UpdatedAt.Should().BeOnOrAfter(first.UpdatedAt);
    }

    // ──────────────────────────────────────────────────────────────
    // Multi-tenant isolation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForTenantAsync_TenantACannotSeeTenantBContent()
    {
        await _sut.UpsertAsync(_tenantBId, Pillar.Communication, """{"secret":"b"}""", _userId);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetForTenantAsync(_tenantId, Pillar.Communication);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllForTenantAsync_TenantIsolation_ReturnsOnlyOwnRows()
    {
        await _sut.UpsertAsync(_tenantBId, Pillar.Brand, """{"tenant":"B"}""", _userId);
        DbContext.ChangeTracker.Clear();

        var resultA = await _sut.GetAllForTenantAsync(_tenantId);

        resultA.Should().OnlyContain(r => r.TenantId == _tenantId);
        resultA.Should().NotContain(r => r.BodyJson == """{"tenant":"B"}""");
    }
}
