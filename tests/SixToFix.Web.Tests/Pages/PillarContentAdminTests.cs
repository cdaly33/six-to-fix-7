using System.Security.Claims;
using SixToFix.Domain.Entities;

namespace SixToFix.Web.Tests.Pages;

public class PillarContentAdminTests : TestContext
{
    private static readonly Guid TestTenantId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestUserId   = new("22222222-2222-2222-2222-222222222222");

    private Mock<IPillarContentService> SetupService(PillarContent? content = null)
    {
        var svc = new Mock<IPillarContentService>();
        svc.Setup(s => s.GetForTenantAsync(It.IsAny<Guid>(), It.IsAny<Pillar>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(content);
        svc.Setup(s => s.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Pillar>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((Guid tid, Pillar p, string body, Guid uid, CancellationToken _) =>
               new PillarContent { TenantId = tid, Pillar = p, BodyJson = body, UpdatedByUserId = uid });
        Services.AddSingleton(svc.Object);
        return svc;
    }

    private void SetTenantAdminClaims(TestAuthorizationContext auth)
    {
        auth.SetAuthorized("test-user");
        auth.SetRoles("TenantAdmin");
        auth.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
            new Claim("tenant_id", TestTenantId.ToString()));
    }

    [Fact]
    public void Unauthenticated_ShowsNoEditorContent()
    {
        SetupService();
        this.AddTestAuthorization();

        var cut = RenderComponent<PillarContentAdmin>();
        cut.Markup.Should().NotContain("field-strategy");
    }

    [Fact]
    public void ClientRole_DoesNotShowEditor()
    {
        SetupService();
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("client-user");
        auth.SetRoles("Client");
        auth.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
            new Claim("tenant_id", TestTenantId.ToString()));

        var cut = RenderComponent<PillarContentAdmin>();
        cut.Markup.Should().NotContain("field-strategy");
    }

    [Fact]
    public void TenantAdmin_ShowsEditor()
    {
        SetupService();
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var cut = RenderComponent<PillarContentAdmin>();
        cut.Markup.Should().Contain("field-strategy");
    }

    [Fact]
    public void SuperAdmin_ShowsEditor()
    {
        SetupService();
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("super");
        auth.SetRoles("SuperAdmin");
        auth.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
            new Claim("tenant_id", TestTenantId.ToString()));

        var cut = RenderComponent<PillarContentAdmin>();
        cut.Markup.Should().Contain("field-strategy");
    }

    [Fact]
    public void ShowsSixPillarButtons()
    {
        SetupService();
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var cut = RenderComponent<PillarContentAdmin>();
        cut.FindAll(".sh-content-admin__pillar-btn").Count.Should().Be(6);
    }

    [Fact]
    public void ShowsFiveTextareaFields()
    {
        SetupService();
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var cut = RenderComponent<PillarContentAdmin>();
        cut.FindAll(".sh-content-admin__textarea").Count.Should().Be(5);
    }

    [Fact]
    public void WithExistingContent_PopulatesFields()
    {
        var body = """{"strategy":"Test strategy","execution":["Step 1","Step 2"],"templates":[],"examples":[],"metrics":[]}""";
        var content = new PillarContent
        {
            TenantId = TestTenantId,
            Pillar   = Pillar.Brand,
            BodyJson = body
        };
        SetupService(content);
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var cut = RenderComponent<PillarContentAdmin>();
        // bUnit renders @bind on textarea as value attribute or inner text; check both
        var element = cut.Find("#field-strategy");
        var value = element.GetAttribute("value") ?? element.InnerHtml;
        value.Should().Contain("Test strategy");
    }

    [Fact]
    public void ShowsLastUpdatedMeta()
    {
        var updatedAt = new DateTime(2025, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var content = new PillarContent
        {
            TenantId        = TestTenantId,
            Pillar          = Pillar.Brand,
            BodyJson        = "{}",
            UpdatedByUserId = TestUserId,
            UpdatedAt       = updatedAt
        };
        SetupService(content);
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var cut = RenderComponent<PillarContentAdmin>();
        cut.Markup.Should().Contain("Last updated by");
        cut.Markup.Should().Contain("22222222");
    }

    [Fact]
    public void NullContent_ShowsEmptyFields()
    {
        SetupService(null);
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var cut = RenderComponent<PillarContentAdmin>();
        var element = cut.Find("#field-strategy");
        var value = element.GetAttribute("value") ?? element.InnerHtml;
        value.Trim().Should().BeEmpty();
    }

    [Fact]
    public void ShowsSaveButton()
    {
        SetupService();
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var cut = RenderComponent<PillarContentAdmin>();
        cut.Find(".sh-content-admin__save-btn").TextContent.Should().Be("Save");
    }

    [Fact]
    public void PreSelectsPillarFromQueryString()
    {
        SetupService();
        var auth = this.AddTestAuthorization();
        SetTenantAdminClaims(auth);

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/admin/content?pillar=customer");

        var cut = RenderComponent<PillarContentAdmin>();
        var activeBtn = cut.Find(".sh-content-admin__pillar-btn--active");
        activeBtn.TextContent.Should().Contain("Customer");
    }
}
