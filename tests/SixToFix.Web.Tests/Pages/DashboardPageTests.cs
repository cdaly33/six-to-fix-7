using SixToFix.Domain.Enums;

namespace SixToFix.Web.Tests.Pages;

public sealed class DashboardPageTests
{
    private static TestContext BuildContext(bool authenticated = true, string? role = null)
    {
        var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();

        if (authenticated)
        {
            auth.SetAuthorized("testuser");
            if (role is not null) auth.SetRoles(role);
        }

        // Mock required services
        var progressMock = new Mock<IProgressService>();
        progressMock.Setup(s => s.GetForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserPillarProgress>());
        progressMock.Setup(s => s.GetAverageForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var pillarMock = new Mock<IPillarContentService>();
        pillarMock.Setup(s => s.GetAllForTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PillarContent>());

        ctx.Services.AddSingleton(progressMock.Object);
        ctx.Services.AddSingleton(pillarMock.Object);

        return ctx;
    }

    [Fact]
    public void Dashboard_ShowsWelcomeHeader_WhenAuthenticated()
    {
        using var ctx = BuildContext(authenticated: true);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Welcome back");
    }

    [Fact]
    public void Dashboard_ShowsSixPillarCards_WhenAuthenticated()
    {
        using var ctx = BuildContext(authenticated: true);
        var cut = ctx.RenderComponent<Dashboard>();
        // Six pillar names should appear
        cut.Markup.Should().Contain("Brand");
        cut.Markup.Should().Contain("Customer");
        cut.Markup.Should().Contain("Offering");
        cut.Markup.Should().Contain("Communication");
        cut.Markup.Should().Contain("Sales");
        cut.Markup.Should().Contain("Management");
    }

    [Fact]
    public void Dashboard_ShowsResumeButton_WhenAuthenticated()
    {
        using var ctx = BuildContext(authenticated: true);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Resume");
        cut.Markup.Should().Contain("href=\"/brand\"");
    }

    [Fact]
    public void Dashboard_ShowsYourProgressSection_WhenAuthenticated()
    {
        using var ctx = BuildContext(authenticated: true);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Your Progress");
        cut.Markup.Should().Contain("Across all six pillars");
    }

    [Fact]
    public void Dashboard_ShowsRecentlyUpdatedSection_WhenAuthenticated()
    {
        using var ctx = BuildContext(authenticated: true);
        var cut = ctx.RenderComponent<Dashboard>();
        cut.Markup.Should().Contain("Recently Updated");
    }
}

