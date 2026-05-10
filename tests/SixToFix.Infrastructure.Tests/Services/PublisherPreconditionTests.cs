using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Data;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for Publisher.PublishAuditAsync precondition guards.
/// Each test uses IntegrationTestBase's per-test transaction rollback.
/// </summary>
public sealed class PublisherPreconditionTests : IntegrationTestBase
{
    private static readonly string[] AllCategories =
        ["brand", "customer", "offering", "communications", "sales", "management"];

    private readonly ITenantContext _tenant;
    private readonly Channel<HubSpotEvent> _hubSpotChannel;
    private readonly Publisher _sut;

    public PublisherPreconditionTests(PostgresContainerFixture fixture) : base(fixture)
    {
        _tenant = Substitute.For<ITenantContext>();
        _tenant.TenantId.Returns(Guid.NewGuid());
        _tenant.IsResolved.Returns(false); // bypass global query filters

        _hubSpotChannel = Channel.CreateUnbounded<HubSpotEvent>();
        _sut = new Publisher(DbContext, _hubSpotChannel, _tenant, NullLogger<Publisher>.Instance);
    }

    // ──────────────────────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAuditAsync_AllApproved_AwaitingReviewStatus_ReturnsResult()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync("awaiting_review", allApproved: true);

        var result = await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        result.Should().NotBeNull();
        result.Tier.Should().BeOneOf("tier_1", "tier_2", "tier_3");
        result.CompositeScore.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PublishAuditAsync_AllApproved_CompletedStatus_ReturnsResult()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync("completed", allApproved: true);

        var result = await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        result.Should().NotBeNull();
        result.PublishedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PublishAuditAsync_CompositeScoreAbove45_ReturnsTier1()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync(
            "awaiting_review", allApproved: true, scorePerCategory: 8);

        var result = await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        // 6 categories × 8 = 48 → tier_1
        result.Tier.Should().Be("tier_1");
    }

    [Fact]
    public async Task PublishAuditAsync_CompositeScoreBelow25_ReturnsTier3()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync(
            "awaiting_review", allApproved: true, scorePerCategory: 3);

        var result = await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        // 6 × 3 = 18 → tier_3
        result.Tier.Should().Be("tier_3");
    }

    // ──────────────────────────────────────────────────────────────
    // Guard: AuditRun not found
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAuditAsync_UnknownAuditRunId_ThrowsAuditRunNotFoundException()
    {
        var action = async () => await _sut.PublishAuditAsync(Guid.NewGuid(), Guid.NewGuid());

        await action.Should().ThrowAsync<AuditRunNotFoundException>();
    }

    // ──────────────────────────────────────────────────────────────
    // Guard: already published
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAuditAsync_AlreadyPublished_ThrowsAuditAlreadyPublishedException()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync("published", allApproved: true);

        var action = async () => await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        await action.Should().ThrowAsync<AuditAlreadyPublishedException>()
            .Where(ex => ex.AuditRunId == auditRunId);
    }

    // ──────────────────────────────────────────────────────────────
    // Guard: wrong status (not awaiting_review / completed)
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("pending")]
    [InlineData("running")]
    [InlineData("failed")]
    public async Task PublishAuditAsync_WrongStatus_ThrowsInvalidAuditRunStateException(string status)
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync(status, allApproved: true);

        var action = async () => await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        await action.Should().ThrowAsync<InvalidAuditRunStateException>()
            .Where(ex => ex.CurrentState == status);
    }

    // ──────────────────────────────────────────────────────────────
    // Guard: not all categories approved
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAuditAsync_FiveOfSixCategoriesApproved_ThrowsNotAllCategoriesApprovedException()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync(
            "awaiting_review", approvedCount: 5);

        var action = async () => await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        await action.Should().ThrowAsync<NotAllCategoriesApprovedException>()
            .Where(ex => ex.ApprovedCount == 5 && ex.RequiredCount == 6);
    }

    [Fact]
    public async Task PublishAuditAsync_ZeroCategoriesApproved_ThrowsNotAllCategoriesApprovedException()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync(
            "awaiting_review", approvedCount: 0);

        var action = async () => await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        await action.Should().ThrowAsync<NotAllCategoriesApprovedException>();
    }

    [Fact]
    public async Task PublishAuditAsync_PublishesHubSpotEvent()
    {
        var (auditRunId, _) = await SeedAuditRunWithCategoriesAsync("awaiting_review", allApproved: true);

        await _sut.PublishAuditAsync(auditRunId, Guid.NewGuid());

        _hubSpotChannel.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.AuditRunId.Should().Be(auditRunId);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private async Task<(Guid auditRunId, List<CategoryResult> categories)> SeedAuditRunWithCategoriesAsync(
        string status,
        bool allApproved = false,
        int? approvedCount = null,
        int scorePerCategory = 7)
    {
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = $"Test-{tenantId:N}", Slug = $"test-{tenantId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        DbContext.Tenants.Add(tenant);

        var clientId = Guid.NewGuid();
        DbContext.Clients.Add(new Client { Id = clientId, TenantId = tenantId, Name = "TestClient", Slug = $"client-{clientId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        var auditId = Guid.NewGuid();
        var audit = new Audit { Id = auditId, TenantId = tenantId, ClientId = clientId, Status = "active", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        DbContext.Audits.Add(audit);

        var auditRunId = Guid.NewGuid();
        var auditRun = new AuditRun
        {
            Id = auditRunId,
            TenantId = tenantId,
            AuditId = auditId,
            Status = status,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.AuditRuns.Add(auditRun);

        var effective = approvedCount ?? (allApproved ? 6 : 0);
        var categories = new List<CategoryResult>();
        for (var i = 0; i < AllCategories.Length; i++)
        {
            var catStatus = i < effective ? "approved" : "pending";
            var cr = new CategoryResult
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AuditRunId = auditRunId,
                Category = AllCategories[i],
                ActivityScore = scorePerCategory,
                Status = catStatus,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            categories.Add(cr);
            DbContext.CategoryResults.Add(cr);
        }

        await DbContext.SaveChangesAsync();
        return (auditRunId, categories);
    }
}
