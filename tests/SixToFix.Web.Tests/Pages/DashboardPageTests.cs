namespace SixToFix.Web.Tests.Pages;
public sealed class DashboardPageTests
{
    private static TestContext BuildContext(bool authenticated = true, string? role = null) { var ctx = new TestContext(); var auth = ctx.AddTestAuthorization(); if (authenticated) { auth.SetAuthorized("testuser"); if (role is not null) auth.SetRoles(role); } return ctx; }
    [Fact] public void Dashboard_ShowsStrategyHubWelcome_WhenAuthenticated() { using var ctx = BuildContext(); var cut = ctx.RenderComponent<Dashboard>(); cut.Markup.Should().Contain("Welcome to StrategyHub"); cut.Markup.Should().Contain("six key pillars"); }
    [Fact] public void Dashboard_ShowsPillarsAndTemplatesLinks_WhenAuthenticated() { using var ctx = BuildContext(); var cut = ctx.RenderComponent<Dashboard>(); cut.Markup.Should().Contain("Pillars"); cut.Markup.Should().Contain("href=\"/pillars\""); cut.Markup.Should().Contain("Templates"); cut.Markup.Should().Contain("href=\"/templates\""); }
    [Fact] public void Dashboard_ShowsTenantAdminCard_ForTenantAdmin() { using var ctx = BuildContext(role: "TenantAdmin"); var cut = ctx.RenderComponent<Dashboard>(); cut.Markup.Should().Contain("Tenant Admin"); cut.Markup.Should().Contain("href=\"/admin/tenant\""); }
    [Fact] public void Dashboard_ShowsSuperAdminCard_ForSuperAdmin() { using var ctx = BuildContext(role: "SuperAdmin"); var cut = ctx.RenderComponent<Dashboard>(); cut.Markup.Should().Contain("Super Admin"); cut.Markup.Should().Contain("href=\"/admin/super\""); }
}
