using System.Security.Claims;

namespace SixToFix.Web.Tests.Pages;

public sealed class AuditDetailPageTests
{
    [Fact]
    public async Task AuditDetail_ConnectsToHubAndRendersIncomingEvents()
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

        var hubClient = new FakeAuditRunHubClient();
        RegisterServices(ctx, orchestrator.Object, hubClient);

        var cut = ctx.RenderComponent<AuditDetail>(parameters => parameters.Add(p => p.AuditRunId, auditRunId));
        await hubClient.TriggerAsync("skill-completed", "Brand analysis finished");

        cut.WaitForAssertion(() =>
        {
            hubClient.Started.Should().BeTrue();
            hubClient.JoinedAuditRunId.Should().Be(auditRunId.ToString("N"));
            cut.Markup.Should().Contain("skill-completed");
            cut.Markup.Should().Contain("Brand analysis finished");
        });
    }

    [Fact]
    public async Task AuditDetail_ReloadsAuditRunAfterTerminalSignalREvent()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("tenant-admin");
        auth.SetRoles("TenantAdmin");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var auditRunId = Guid.NewGuid();
        var orchestrator = new Mock<IAuditOrchestrator>();
        orchestrator
            .SetupSequence(service => service.GetAuditRunAsync(auditRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAuditRun(auditRunId, "running"))
            .ReturnsAsync(BuildAuditRun(auditRunId, "completed"));

        var hubClient = new FakeAuditRunHubClient();
        RegisterServices(ctx, orchestrator.Object, hubClient);

        var cut = ctx.RenderComponent<AuditDetail>(parameters => parameters.Add(p => p.AuditRunId, auditRunId));
        await hubClient.TriggerAsync("run-completed", "Done");

        cut.WaitForAssertion(() =>
        {
            orchestrator.Verify(service => service.GetAuditRunAsync(auditRunId, It.IsAny<CancellationToken>()), Times.Exactly(2));
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

        RegisterServices(ctx, orchestrator.Object, new FakeAuditRunHubClient());

        var cut = ctx.RenderComponent<AuditDetail>(parameters => parameters.Add(p => p.AuditRunId, auditRunId));

        cut.Markup.Should().NotContain("Publish Results");
    }

    private static void RegisterServices(TestContext ctx, IAuditOrchestrator orchestrator, IAuditRunHubClientFactory hubClientFactory)
    {
        ctx.Services.AddSingleton(orchestrator);
        ctx.Services.AddSingleton(Mock.Of<IReviewerWorkflow>());
        ctx.Services.AddSingleton(Mock.Of<IPublisher>());
        ctx.Services.AddSingleton(hubClientFactory);
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
