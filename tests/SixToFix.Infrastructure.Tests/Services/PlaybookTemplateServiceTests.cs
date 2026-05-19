using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SixToFix.Domain.Enums;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for PlaybookTemplateService using a real PostgreSQL database.
/// Tests cover: GetPublished (with/without pillar filter), GetById, Create (Draft forced),
/// Update, Publish, Archive, and multi-tenant isolation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlaybookTemplateServiceTests : IntegrationTestBase
{
    private PlaybookTemplateService _sut = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();

    public PlaybookTemplateServiceTests(PostgresContainerFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _sut = new PlaybookTemplateService(DbContext, NullLogger<PlaybookTemplateService>.Instance);

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
    // CreateAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SetsIdAndTenantId()
    {
        var template = BuildTemplate(_tenantId, Pillar.Brand);

        var result = await _sut.CreateAsync(_tenantId, template);

        result.Id.Should().NotBe(Guid.Empty);
        result.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task CreateAsync_AlwaysForcesStatusToDraft()
    {
        var template = BuildTemplate(_tenantId, Pillar.Brand, status: PlaybookTemplateStatus.Published);

        var result = await _sut.CreateAsync(_tenantId, template);

        result.Status.Should().Be(PlaybookTemplateStatus.Draft);
    }

    [Fact]
    public async Task CreateAsync_SetsLastUpdatedAt()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var result = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Customer));

        result.LastUpdatedAt.Should().BeAfter(before);
    }

    // ──────────────────────────────────────────────────────────────
    // GetByIdAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingTemplate_ReturnsIt()
    {
        var created = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Sales));

        var result = await _sut.GetByIdAsync(_tenantId, created.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(_tenantId, Guid.NewGuid());

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // UpdateAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangesName()
    {
        var created = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Brand, name: "Old Name"));
        created.Name = "New Name";

        var updated = await _sut.UpdateAsync(_tenantId, created);

        updated.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeStatus()
    {
        var created = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Brand));
        await _sut.PublishAsync(_tenantId, created.Id);
        DbContext.ChangeTracker.Clear();

        var fetched = (await _sut.GetByIdAsync(_tenantId, created.Id))!;
        fetched.Name = "Updated";
        await _sut.UpdateAsync(_tenantId, fetched);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetByIdAsync(_tenantId, created.Id);
        result!.Status.Should().Be(PlaybookTemplateStatus.Published);
    }

    // ──────────────────────────────────────────────────────────────
    // PublishAsync / ArchiveAsync — status transitions
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_DraftTemplate_TransitionsToPublished()
    {
        var created = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Offering));

        var published = await _sut.PublishAsync(_tenantId, created.Id);

        published.Status.Should().Be(PlaybookTemplateStatus.Published);
    }

    [Fact]
    public async Task ArchiveAsync_PublishedTemplate_TransitionsToArchived()
    {
        var created = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Communication));
        await _sut.PublishAsync(_tenantId, created.Id);
        DbContext.ChangeTracker.Clear();

        var archived = await _sut.ArchiveAsync(_tenantId, created.Id);

        archived.Status.Should().Be(PlaybookTemplateStatus.Archived);
    }

    [Fact]
    public async Task ArchiveAsync_DraftTemplate_TransitionsToArchived()
    {
        var created = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Management));

        var archived = await _sut.ArchiveAsync(_tenantId, created.Id);

        archived.Status.Should().Be(PlaybookTemplateStatus.Archived);
    }

    [Fact]
    public async Task PublishAsync_NotFound_ThrowsInvalidOperationException()
    {
        var action = async () => await _sut.PublishAsync(_tenantId, Guid.NewGuid());

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ArchiveAsync_NotFound_ThrowsInvalidOperationException()
    {
        var action = async () => await _sut.ArchiveAsync(_tenantId, Guid.NewGuid());

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────────
    // GetPublishedAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublishedAsync_NoPillarFilter_ReturnsOnlyPublished()
    {
        var draft = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Brand, name: "Draft One"));
        var published = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Customer, name: "Published One"));
        await _sut.PublishAsync(_tenantId, published.Id);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetPublishedAsync(_tenantId, null);

        result.Should().NotContain(r => r.Id == draft.Id);
        result.Should().Contain(r => r.Id == published.Id);
    }

    [Fact]
    public async Task GetPublishedAsync_WithPillarFilter_ReturnsMatchingAndNullPillar()
    {
        var brandTemplate = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Brand, name: "Brand"));
        var crossCutting = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, null, name: "Cross-cutting"));
        var salesTemplate = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Sales, name: "Sales"));

        await _sut.PublishAsync(_tenantId, brandTemplate.Id);
        await _sut.PublishAsync(_tenantId, crossCutting.Id);
        await _sut.PublishAsync(_tenantId, salesTemplate.Id);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetPublishedAsync(_tenantId, Pillar.Brand);

        result.Should().Contain(r => r.Id == brandTemplate.Id);
        result.Should().Contain(r => r.Id == crossCutting.Id);   // null-pillar included
        result.Should().NotContain(r => r.Id == salesTemplate.Id); // different pillar excluded
    }

    [Fact]
    public async Task GetPublishedAsync_ArchivedTemplate_NotIncluded()
    {
        var t = await _sut.CreateAsync(_tenantId, BuildTemplate(_tenantId, Pillar.Offering));
        await _sut.PublishAsync(_tenantId, t.Id);
        await _sut.ArchiveAsync(_tenantId, t.Id);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetPublishedAsync(_tenantId, null);

        result.Should().NotContain(r => r.Id == t.Id);
    }

    // ──────────────────────────────────────────────────────────────
    // Multi-tenant isolation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_TenantACannotSeeTenantBTemplate()
    {
        var tenantBTemplate = await _sut.CreateAsync(_tenantBId, BuildTemplate(_tenantBId, Pillar.Sales));
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetByIdAsync(_tenantId, tenantBTemplate.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPublishedAsync_TenantIsolation_DoesNotReturnOtherTenantTemplates()
    {
        var tenantBTemplate = await _sut.CreateAsync(_tenantBId, BuildTemplate(_tenantBId, Pillar.Brand, name: "Tenant B Template"));
        await _sut.PublishAsync(_tenantBId, tenantBTemplate.Id);
        DbContext.ChangeTracker.Clear();

        var result = await _sut.GetPublishedAsync(_tenantId, null);

        result.Should().NotContain(r => r.TenantId == _tenantBId);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static PlaybookTemplate BuildTemplate(
        Guid tenantId,
        Pillar? pillar,
        string name = "Test Template",
        PlaybookTemplateStatus status = PlaybookTemplateStatus.Draft) => new()
    {
        Id = Guid.Empty, // overwritten by CreateAsync
        TenantId = tenantId,
        Pillar = pillar,
        Name = name,
        Format = "doc",
        Status = status,
        Popularity = 0,
        LastUpdatedAt = DateTimeOffset.UtcNow,
        Notes = null
    };
}
