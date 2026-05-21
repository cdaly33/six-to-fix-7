namespace SixToFix.Web.Tests.Pages;

using System.Security.Claims;

public sealed class TemplatesPageTests
{
    private static TestContext BuildContext(bool isAdmin)
    {
        var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("template-user");
        auth.SetClaims(new Claim("tenant_id", Guid.NewGuid().ToString()));
        auth.SetRoles(isAdmin ? "TenantAdmin" : "Client");

        var templateService = new Mock<IPlaybookTemplateService>();
        templateService.Setup(s => s.GetPublishedAsync(It.IsAny<Guid>(), It.IsAny<Pillar?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PlaybookTemplate>)[]);
        ctx.Services.AddSingleton(templateService.Object);

        return ctx;
    }

    [Fact]
    public void Templates_WhenEmpty_ShowsStarterTracks()
    {
        using var ctx = BuildContext(isAdmin: false);
        var cut = ctx.RenderComponent<Templates>();

        cut.Markup.Should().Contain("Start from a proven template track");
        cut.Markup.Should().Contain("Pillar kickoff");
        cut.Markup.Should().Contain("Review Pillar Guidance");
    }

    [Fact]
    public void Templates_WhenEmpty_HidesAdminActionForClientRole()
    {
        using var ctx = BuildContext(isAdmin: false);
        var cut = ctx.RenderComponent<Templates>();

        cut.Markup.Should().NotContain("Open Template Admin");
        cut.Markup.Should().Contain("Ask your tenant admin to publish templates");
    }
}
