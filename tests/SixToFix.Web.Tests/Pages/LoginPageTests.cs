namespace SixToFix.Web.Tests.Pages;

public sealed class LoginPageTests
{
    [Fact]
    public void Login_ShowsValidationMessagesForMissingFields()
    {
        using var ctx = new TestContext();
        ctx.AddTestAuthorization();
        ctx.JSInterop.Setup<string>("SixToFix.login", _ => true);
        ctx.Services.AddSingleton(Mock.Of<ILoginNavigator>());

        var cut = ctx.RenderComponent<Login>();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("The Email field is required.");
            cut.Markup.Should().Contain("The Password field is required.");
        });
    }

    [Fact]
    public void Login_ShowsErrorForInvalidCredentials()
    {
        using var ctx = new TestContext();
        ctx.AddTestAuthorization();
        ctx.JSInterop.Setup<string>("SixToFix.login", _ => true).SetResult("unauthorized");
        ctx.Services.AddSingleton(Mock.Of<ILoginNavigator>());

        var cut = ctx.RenderComponent<Login>();

        cut.Find("#email").Change("reviewer@strategicglue.com");
        cut.Find("#password").Change("Password123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Invalid email or password."));
    }

    [Fact]
    public void Login_RedirectsToDashboardWhenApiReturnsToken()
    {
        using var ctx = new TestContext();
        ctx.AddTestAuthorization();
        ctx.JSInterop.Setup<string>("SixToFix.login", _ => true).SetResult("ok");
        var navigator = new Mock<ILoginNavigator>();
        ctx.Services.AddSingleton(navigator.Object);

        var cut = ctx.RenderComponent<Login>();

        cut.Find("#email").Change("reviewer@strategicglue.com");
        cut.Find("#password").Change("Password123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => navigator.Verify(service => service.NavigateTo(null), Times.Once));
    }
}
