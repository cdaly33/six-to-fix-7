using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Application.Multitenancy;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Services;
using SixToFix.Infrastructure.Tests.Fixtures;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for ReviewerWorkflow lockout decision logic via GetLockoutStatusAsync.
/// The public GetLockoutStatusAsync method reads ReviewerLockout records and applies the
/// 24-hour / 3-rejection window rules — fully testable without triggering CheckLockoutAsync's
/// serializable transaction (which cannot nest inside the test's outer transaction).
/// </summary>
public sealed class ReviewerWorkflowLockoutTests : IntegrationTestBase
{
    private const int LockoutWindowHours = 24;
    private const int LockoutThreshold = 3;

    private readonly ReviewerWorkflow _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _auditRunId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();

    public ReviewerWorkflowLockoutTests(PostgresContainerFixture fixture) : base(fixture)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(_tenantId);
        tenant.IsResolved.Returns(false);

        _sut = new ReviewerWorkflow(
            DbContext,
            Substitute.For<ICalibrationTracker>(),
            Substitute.For<ICouncilRunner>(),
            Substitute.For<ISkillRunner>(),
            tenant,
            NullLogger<ReviewerWorkflow>.Instance);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedPrerequisitesAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // No lockout record → not locked
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLockoutStatus_NoRecord_ReturnsNotLocked()
    {
        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeFalse();
        status.RejectionCount.Should().Be(0);
        status.LockoutExpiresAt.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // 1st rejection: count = 1, IsLocked = false
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLockoutStatus_OneRejection_NotLocked()
    {
        await SeedLockoutAsync(rejectionCount: 1, isLocked: false, windowStartedAt: DateTimeOffset.UtcNow);

        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeFalse();
        status.RejectionCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────
    // 2nd rejection: count = 2, IsLocked = false
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLockoutStatus_TwoRejections_NotLocked()
    {
        await SeedLockoutAsync(rejectionCount: 2, isLocked: false, windowStartedAt: DateTimeOffset.UtcNow);

        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeFalse();
        status.RejectionCount.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────
    // 3rd rejection within 24h: IsLocked = true → returns locked
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLockoutStatus_ThreeRejectionsWithin24h_ReturnsLocked()
    {
        var windowStart = DateTimeOffset.UtcNow.AddHours(-1); // 1h ago, inside window
        await SeedLockoutAsync(rejectionCount: 3, isLocked: true, windowStartedAt: windowStart);

        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeTrue();
        status.RejectionCount.Should().Be(LockoutThreshold);
        status.LockoutExpiresAt.Should().BeCloseTo(
            windowStart.AddHours(LockoutWindowHours),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetLockoutStatus_FiveRejectionsWithin24h_ReturnsLocked()
    {
        var windowStart = DateTimeOffset.UtcNow.AddHours(-2);
        await SeedLockoutAsync(rejectionCount: 5, isLocked: true, windowStartedAt: windowStart);

        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────
    // Lockout resets after 24h: WindowStartedAt is > 24h ago
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLockoutStatus_ThreeRejectionsOlderThan24h_ReturnsNotLocked()
    {
        // Window started 25 hours ago — outside the 24h rolling window
        var windowStart = DateTimeOffset.UtcNow.AddHours(-(LockoutWindowHours + 1));
        await SeedLockoutAsync(rejectionCount: 3, isLocked: true, windowStartedAt: windowStart);

        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeFalse();
        status.LockoutExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task GetLockoutStatus_ExactlyAt24hBoundary_ReturnsNotLocked()
    {
        // Window started exactly 24h ago: UtcNow.AddHours(-24) is NOT > windowStart (equal)
        // so isLocked check evaluates as false (stale window)
        var windowStart = DateTimeOffset.UtcNow.AddHours(-LockoutWindowHours);
        await SeedLockoutAsync(rejectionCount: 3, isLocked: true, windowStartedAt: windowStart);

        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // Tenant isolation: different reviewer → no lockout visible
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLockoutStatus_DifferentReviewer_NotLocked()
    {
        // Seed lockout for a DIFFERENT reviewer
        await SeedLockoutAsync(rejectionCount: 3, isLocked: true,
            windowStartedAt: DateTimeOffset.UtcNow.AddHours(-1),
            reviewerIdOverride: Guid.NewGuid());

        // Query for our reviewer — should see no lockout
        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task GetLockoutStatus_DifferentAuditRun_NotLocked()
    {
        await SeedLockoutAsync(rejectionCount: 3, isLocked: true,
            windowStartedAt: DateTimeOffset.UtcNow.AddHours(-1),
            auditRunIdOverride: Guid.NewGuid());

        var status = await _sut.GetLockoutStatusAsync(_auditRunId, _categoryId, _reviewerId);

        status.IsLockedOut.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private async Task SeedPrerequisitesAsync()
    {
        var tenant = new Tenant
        {
            Id = _tenantId,
            Name = "LockoutTest",
            Slug = $"lockout-{_tenantId:N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Tenants.Add(tenant);

        var clientId = Guid.NewGuid();
        DbContext.Clients.Add(new Client { Id = clientId, TenantId = _tenantId, Name = "LockoutClient", Slug = $"client-{clientId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        var auditId = Guid.NewGuid();
        var audit = new Audit
        {
            Id = auditId,
            TenantId = _tenantId,
            ClientId = clientId,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Audits.Add(audit);

        var auditRun = new AuditRun
        {
            Id = _auditRunId,
            TenantId = _tenantId,
            AuditId = auditId,
            Status = "awaiting_review",
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.AuditRuns.Add(auditRun);
        await DbContext.SaveChangesAsync();
    }

    private async Task SeedLockoutAsync(
        int rejectionCount,
        bool isLocked,
        DateTimeOffset windowStartedAt,
        Guid? reviewerIdOverride = null,
        Guid? auditRunIdOverride = null)
    {
        var lockout = new ReviewerLockout
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuditRunId = auditRunIdOverride ?? _auditRunId,
            Category = _categoryId.ToString(),
            ReviewerUserId = reviewerIdOverride ?? _reviewerId,
            RejectionCount = rejectionCount,
            IsLocked = isLocked,
            WindowStartedAt = windowStartedAt,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.ReviewerLockouts.Add(lockout);
        await DbContext.SaveChangesAsync();
    }
}
