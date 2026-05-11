using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace SixToFix.Web.Tests.Pages;

public sealed class LoginPageTests
{
    [Fact]
    public void Login_ShowsValidationMessagesForMissingFields()
    {
        using var ctx = new TestContext();
        ctx.AddTestAuthorization();
        ctx.Services.AddSingleton(CreateHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));

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
        ctx.Services.AddSingleton(CreateHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));

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
        ctx.Services.AddSingleton(CreateHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accessToken = "jwt-token" })
        }));

        var cut = ctx.RenderComponent<Login>();

        cut.Find("#email").Change("reviewer@strategicglue.com");
        cut.Find("#password").Change("Password123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            ctx.JSInterop.VerifyInvoke("localStorage.setItem");
            ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/dashboard");
        });
    }

    private static IHttpClientFactory CreateHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var mock = new Mock<IHttpClientFactory>();
        var client = new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://localhost")
        };

        mock.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(client);
        return mock.Object;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
