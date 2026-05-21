namespace SixToFix.Web.Tests.Pages;

using System.Security.Claims;

public sealed class ClientsPageTests
{
    private static TestContext BuildContext(string role)
    {
        var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("client-user");
        auth.SetClaims(new Claim("tenant_id", Guid.NewGuid().ToString()));
        auth.SetRoles(role);

        var clientService = new Mock<IClientService>();
        clientService.Setup(s => s.GetAllForTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Client>)[]);
        ctx.Services.AddSingleton(clientService.Object);

        return ctx;
    }

    [Fact]
    public void Clients_WhenEmpty_ShowsActionableChecklist()
    {
        using var ctx = BuildContext("TenantAdmin");
        var cut = ctx.RenderComponent<Clients>();

        cut.Markup.Should().Contain("No clients are active yet");
        cut.Markup.Should().Contain("Add your first client");
        cut.Markup.Should().Contain("Add First Client");
    }

    [Fact]
    public void Clients_WhenEmpty_ForClientRole_ShowsAdminHint()
    {
        using var ctx = BuildContext("Client");
        var cut = ctx.RenderComponent<Clients>();

        cut.Markup.Should().Contain("Ask a tenant admin to add clients");
        cut.Markup.Should().NotContain("Add First Client");
    }
}
