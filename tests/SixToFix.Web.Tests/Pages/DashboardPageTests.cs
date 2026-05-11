namespace SixToFix.Web.Tests.Pages;

public sealed class DashboardPageTests
{
    [Fact]
    public void Dashboard_RedirectsUnauthenticatedUsersToLogin()
    {
        using var ctx = new TestContext();
        ctx.AddTestAuthorization();

        ctx.RenderComponent<Dashboard>();

        ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/login");
    }

    [Fact]
    public void Dashboard_ShowsReviewerNavigationForReviewerRole()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("reviewer");
        auth.SetRoles("Reviewer");

        var cut = ctx.RenderComponent<Dashboard>();

        cut.Markup.Should().Contain("Review Queue");
        cut.Markup.Should().NotContain("Tenant Admin");
        cut.Markup.Should().NotContain("Super Admin");
    }

    [Fact]
    public void Dashboard_ShowsAdminCardsForSuperAdmin()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("SuperAdmin");

        var cut = ctx.RenderComponent<Dashboard>();

        cut.Markup.Should().Contain("+ New Audit Run");
        cut.Markup.Should().Contain("Tenant Admin");
        cut.Markup.Should().Contain("Super Admin");
    }
}
