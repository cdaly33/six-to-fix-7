using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using SixToFix.Domain.Enums;
using SixToFix.Infrastructure.Data;
using SixToFix.Infrastructure.Services;

namespace SixToFix.Infrastructure.Tests.Services;

public sealed class PillarContentServiceUnitTests
{
    [Fact]
    public async Task GetForTenantAsync_MissingRows_SeedsAndReturnsRequestedPillar()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var sut = new PillarContentService(db, NullLogger<PillarContentService>.Instance);

        var result = await sut.GetForTenantAsync(tenantId, Pillar.Brand);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantId);
        result.Pillar.Should().Be(Pillar.Brand);
        result.Title.Should().Be("Brand Strategy");
        result.BodyJson.Should().Contain("strategy");
        result.BodyJson.Should().NotContain("placeholder");
    }

    [Fact]
    public async Task GetAllForTenantAsync_MissingRows_SeedsAllSixPillars()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var sut = new PillarContentService(db, NullLogger<PillarContentService>.Instance);

        var result = await sut.GetAllForTenantAsync(tenantId);

        result.Should().HaveCount(6);
        result.Should().OnlyContain(e => e.TenantId == tenantId);
        result.Select(e => e.Pillar).Should().BeEquivalentTo(Enum.GetValues<Pillar>());
        result.Should().OnlyContain(e => e.BodyJson.Contains("strategy"));
    }

    [Fact]
    public async Task GetAllForTenantAsync_CalledTwice_DoesNotDuplicateRows()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var sut = new PillarContentService(db, NullLogger<PillarContentService>.Instance);

        await sut.GetAllForTenantAsync(tenantId);
        db.ChangeTracker.Clear();
        var second = await sut.GetAllForTenantAsync(tenantId);

        second.Should().HaveCount(6);
    }

    private static SixToFixDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<SixToFixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .Options;

        return new SixToFixDbContext(options, new TestTenantContext(tenantId));
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; }
        public string TenantSlug => "test-tenant";
        public bool IsResolved => true;
    }
}
