using System.Security.Claims;
using SixToFix.Web.Components.Nav;

namespace SixToFix.Web.Tests.Components;

public sealed class SectionSidebarTests
{
    [Fact]
    public void SectionSidebar_ShowsLoggedInCoreRoutes()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("test-user");
        var deploymentInfoService = new Mock<IDeploymentInfoService>();
        deploymentInfoService.Setup(x => x.GetDeploymentInfo()).Returns(new DeploymentInfo(null, null, null));
        ctx.Services.AddSingleton(deploymentInfoService.Object);
        auth.SetClaims(
            new Claim("name", "Test User"),
            new Claim("tenant_id", Guid.NewGuid().ToString()));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/dashboard");

        var cut = ctx.RenderComponent<SectionSidebar>();
        var markup = cut.Markup;

        markup.Should().Contain("href=\"/dashboard\"");
        markup.Should().Contain("href=\"/brand\"");
        markup.Should().Contain("href=\"/customer\"");
        markup.Should().Contain("href=\"/offering\"");
        markup.Should().Contain("href=\"/communication\"");
        markup.Should().Contain("href=\"/sales\"");
        markup.Should().Contain("href=\"/management\"");
        markup.Should().Contain("href=\"/templates\"");
        markup.Should().NotContain("href=\"/admin/content\"");
        markup.Should().NotContain("href=\"/admin/templates\"");
        markup.Should().NotContain("href=\"/clients\"");
    }

    [Fact]
    public void SectionSidebar_ShowsAdminRoutes_ForTenantAdmin()
    {
        using var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("admin-user");
        var deploymentInfoService = new Mock<IDeploymentInfoService>();
        deploymentInfoService.Setup(x => x.GetDeploymentInfo()).Returns(new DeploymentInfo(null, null, null));
        ctx.Services.AddSingleton(deploymentInfoService.Object);
        auth.SetRoles("TenantAdmin");
        auth.SetClaims(
            new Claim("name", "Tenant Admin"),
            new Claim("tenant_id", Guid.NewGuid().ToString()));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/dashboard");

        var cut = ctx.RenderComponent<SectionSidebar>();
        var markup = cut.Markup;

        markup.Should().Contain("href=\"/clients\"");
        markup.Should().Contain("href=\"/admin/content\"");
        markup.Should().Contain("href=\"/admin/templates\"");
    }
}
