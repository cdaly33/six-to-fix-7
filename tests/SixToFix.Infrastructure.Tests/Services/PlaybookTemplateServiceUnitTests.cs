using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using SixToFix.Domain.Enums;
using SixToFix.Infrastructure.Data;
using SixToFix.Infrastructure.Services;

namespace SixToFix.Infrastructure.Tests.Services;

public sealed class PlaybookTemplateServiceUnitTests
{
    [Fact]
    public async Task CreateAsync_HappyPath_ScopesTemplateToTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var sut = new PlaybookTemplateService(db, NullLogger<PlaybookTemplateService>.Instance);

        var created = await sut.CreateAsync(tenantId, new PlaybookTemplate
        {
            Name = "Launch Plan",
            Pillar = Pillar.Brand,
            Format = "markdown",
            Notes = "Brand rollout",
            ContentMarkdown = "# Launch"
        });

        created.Id.Should().NotBe(Guid.Empty);
        created.TenantId.Should().Be(tenantId);
        created.Status.Should().Be(PlaybookTemplateStatus.Draft);
        created.ContentMarkdown.Should().Be("# Launch");

        (await sut.GetByIdAsync(tenantId, created.Id)).Should().NotBeNull();
        (await sut.GetByIdAsync(otherTenantId, created.Id)).Should().BeNull();
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
