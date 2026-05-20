namespace SixToFix.Web.Tests.Pages;

using System.Security.Claims;

public sealed class DashboardPageTests
{
    private static TestContext BuildContext(bool authenticated = true, string? role = null, int averageProgress = 50, int clientCount = 1)
    {
        var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        
        if (authenticated)
        {
            auth.SetAuthorized("testuser");
            auth.SetClaims(
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("tenant_id", Guid.NewGuid().ToString())
            );
            if (role is not null) auth.SetRoles(role);
        }

        var mockProgressService = new Mock<IProgressService>();
        mockProgressService.Setup(x => x.GetAverageForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(averageProgress);

        var mockClientService = new Mock<IClientService>();
        mockClientService.Setup(x => x.GetAllForTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Client>)
                Enumerable.Range(0, clientCount).Select(_ => new Client { Id = Guid.NewGuid(), Name = "Test Client" }).ToList()
            );

        ctx.Services.AddSingleton(mockProgressService.Object);
        ctx.Services.AddSingleton(mockClientService.Object);

        return ctx;
    }

    [Fact]
    public void Dashboard_ShowsStrategyHubWelcome_WhenAuthenticated()
    {
        using var ctx = BuildContext();
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Welcome to StrategyHub");
        cut.Markup.Should().Contain("six key pillars");
    }

    [Fact]
    public void Dashboard_ShowsPillarsLink_WhenAuthenticated()
    {
        using var ctx = BuildContext();
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Pillars");
        cut.Markup.Should().Contain("href=\"/brand\"");
    }

    [Fact]
    public void Dashboard_ShowsTemplatesLink_WhenAuthenticated()
    {
        using var ctx = BuildContext();
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Templates");
        cut.Markup.Should().Contain("href=\"/templates\"");
    }

    [Fact]
    public void Dashboard_ShowsTenantAdminCard_ForTenantAdmin()
    {
        using var ctx = BuildContext(role: "TenantAdmin");
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Tenant Admin");
        cut.Markup.Should().Contain("href=\"/admin/tenant\"");
    }

    [Fact]
    public void Dashboard_ShowsSuperAdminCard_ForSuperAdmin()
    {
        using var ctx = BuildContext(role: "SuperAdmin");
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Super Admin");
        cut.Markup.Should().Contain("href=\"/admin/super\"");
    }

    [Fact]
    public void Dashboard_ShowsGettingStarted_WhenNoProgressAndNoClients()
    {
        using var ctx = BuildContext(averageProgress: 0, clientCount: 0);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Welcome to Six-to-Fix");
        cut.Markup.Should().Contain("Add a Client");
        cut.Markup.Should().Contain("Review the 6 Pillars");
        cut.Markup.Should().Contain("Create Your First Playbook Template");
    }

    [Fact]
    public void Dashboard_ShowsNormalDashboard_WhenHasProgress()
    {
        using var ctx = BuildContext(averageProgress: 25, clientCount: 0);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Welcome to StrategyHub");
        cut.Markup.Should().NotContain("Welcome to Six-to-Fix");
    }

    [Fact]
    public void Dashboard_ShowsNormalDashboard_WhenHasClients()
    {
        using var ctx = BuildContext(averageProgress: 0, clientCount: 1);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Welcome to StrategyHub");
        cut.Markup.Should().NotContain("Welcome to Six-to-Fix");
    }

    [Fact]
    public void Dashboard_ShowsProgressPercentage_WhenHasProgress()
    {
        using var ctx = BuildContext(averageProgress: 45, clientCount: 1);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Your Progress");
        cut.Markup.Should().Contain("45%");
    }
}
