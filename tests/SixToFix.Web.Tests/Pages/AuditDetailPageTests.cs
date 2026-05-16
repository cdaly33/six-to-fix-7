using System.Security.Claims;

namespace SixToFix.Web.Tests.Pages;

public sealed class AuditDetailPageTests
{
    [Fact]
    public void AuditDetail_ShowsLiveIndicatorWhenRunIsActive()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("tenant-admin");
        auth.SetRoles("TenantAdmin");

        var auditRunId = Guid.NewGuid();
        var orchestrator = new Mock<IAuditOrchestrator>();
        orchestrator
            .Setup(service => service.GetAuditRunAsync(auditRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAuditRun(auditRunId, "running"));

        RegisterServices(ctx, orchestrator.Object);

        var cut = ctx.RenderComponent<AuditDetail>(parameters => parameters.Add(p => p.AuditRunId, auditRunId));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("running");
        });
    }

    [Fact]
    public void AuditDetail_ShowsPublishButtonForCompletedRunWithAdminRole()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("tenant-admin");
        auth.SetRoles("TenantAdmin");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var auditRunId = Guid.NewGuid();
        var orchestrator = new Mock<IAuditOrchestrator>();
        orchestrator
            .Setup(service => service.GetAuditRunAsync(auditRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAuditRun(auditRunId, "completed"));

        RegisterServices(ctx, orchestrator.Object);

        var cut = ctx.RenderComponent<AuditDetail>(parameters => parameters.Add(p => p.AuditRunId, auditRunId));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Publish Results");
        });
    }

    [Fact]
    public void AuditDetail_HidesPublishButtonForViewerRole()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("viewer");
        auth.SetRoles("Viewer");

        var auditRunId = Guid.NewGuid();
        var orchestrator = new Mock<IAuditOrchestrator>();
        orchestrator
            .Setup(service => service.GetAuditRunAsync(auditRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAuditRun(auditRunId, "completed"));

        RegisterServices(ctx, orchestrator.Object);

        var cut = ctx.RenderComponent<AuditDetail>(parameters => parameters.Add(p => p.AuditRunId, auditRunId));

        cut.Markup.Should().NotContain("Publish Results");
    }

    private static void RegisterServices(TestContext ctx, IAuditOrchestrator orchestrator)
    {
        ctx.Services.AddSingleton(orchestrator);
        ctx.Services.AddSingleton(Mock.Of<IReviewerWorkflow>());
        ctx.Services.AddSingleton(Mock.Of<IPublisher>());
        ctx.Services.AddSingleton<ILogger<AuditDetail>>(_ => NullLogger<AuditDetail>.Instance);
    }

    private static AuditRun BuildAuditRun(Guid id, string status) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        AuditId = Guid.NewGuid(),
        Status = status,
        StartedAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow,
        Tier = status == "completed" ? "tier_2" : null,
        CompositeScore = status == "completed" ? 38 : null
    };
}
