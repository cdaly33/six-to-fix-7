using System.Security.Claims;

namespace SixToFix.Web.Tests.Pages;

public sealed class LoggedInRouteSmokeTests
{
    [Fact]
    public void ClientsRoute_RendersPageShell()
    {
        using var ctx = BuildContext("TenantAdmin");

        var clientService = new Mock<IClientService>();
        clientService.Setup(s => s.GetAllForTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Client>());
        ctx.Services.AddSingleton(clientService.Object);

        var cut = ctx.RenderComponent<Clients>();
        cut.Markup.Should().Contain("Clients");
        cut.Markup.Should().Contain("No clients yet.");
    }

    [Fact]
    public void TemplatesRoute_RendersEmptyState()
    {
        using var ctx = BuildContext("Client");

        var templateService = new Mock<IPlaybookTemplateService>();
        templateService.Setup(s => s.GetPublishedAsync(It.IsAny<Guid>(), It.IsAny<Pillar?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlaybookTemplate>());
        ctx.Services.AddSingleton(templateService.Object);
        ctx.Services.AddSingleton(Mock.Of<IPillarContentService>());

        var cut = ctx.RenderComponent<Templates>();
        cut.Markup.Should().Contain("Template Library");
        cut.Markup.Should().Contain("No templates yet.");
    }

    [Fact]
    public void TenantAdminRoute_RendersTenantSettings()
    {
        using var ctx = BuildContext("TenantAdmin", out var tenantId);
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(s => s.GetCurrentTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantDto(tenantId, "Demo Tenant", "demo-tenant", DateTimeOffset.UtcNow));
        tenantService.Setup(s => s.GetTenantUsersAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TenantUserDto(Guid.NewGuid(), "owner@example.com", "Owner", "TenantAdmin", true, null, DateTimeOffset.UtcNow) });
        ctx.Services.AddSingleton(tenantService.Object);
        ctx.Services.AddSingleton(Mock.Of<IClientService>());

        var cut = ctx.RenderComponent<TenantAdminPanel>();
        cut.Markup.Should().Contain("Tenant Administration");
        cut.Markup.Should().Contain("Tenant Settings");
    }

    [Fact]
    public void TemplatesAdminRoute_RendersManageTemplatesHeader()
    {
        using var ctx = BuildContext("TenantAdmin");

        var templateService = new Mock<IPlaybookTemplateService>();
        templateService.Setup(s => s.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlaybookTemplate>());
        ctx.Services.AddSingleton(templateService.Object);

        var cut = ctx.RenderComponent<TemplatesAdmin>();
        cut.Markup.Should().Contain("Manage Templates");
        cut.Markup.Should().Contain("No templates yet.");
    }

    private static TestContext BuildContext(string role) => BuildContext(role, out _);

    private static TestContext BuildContext(string role, out Guid tenantId)
    {
        var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();

        tenantId = Guid.NewGuid();
        auth.SetAuthorized("test-user");
        auth.SetRoles(role);
        auth.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("name", "Test User"));

        return ctx;
    }
}
