namespace SixToFix.Api.Tests;

/// <summary>
/// Verifies the authentication contract that was broken in prod:
///   - Unauthenticated browser navigations to public pages → 200 OK
///   - Unauthenticated browser navigations to protected pages → 302 to /login (NOT 401)
///   - Unauthenticated calls to /api/* → 401 (JwtBearer gate intact)
///
/// These tests catch regressions in the OnChallenge redirect logic in Program.cs
/// and confirm the JwtBearer scheme is still protecting API routes.
/// </summary>
[Trait("Category", "Contract")]
public sealed class AuthContractTests
{
    [Fact]
    public async Task Get_Root_Unauthenticated_BrowserNav_ReturnsOk()
    {
        // / is now a public homepage — anonymous visitors must see it, not get bounced to /login
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "/ is a public homepage — unauthenticated browser navigation must succeed, not redirect");
    }

    [Fact]
    public async Task Get_Dashboard_Unauthenticated_BrowserNav_RedirectsToLogin_Not401()
    {
        // /dashboard is a protected page — unauthenticated visitors must be redirected to /login
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/dashboard");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            because: "unauthenticated browser navigation to a protected page must redirect to /login, not challenge with 401");
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("/login",
            because: "the redirect destination must be the login page");
    }

    [Fact]
    public async Task Post_AuthRefresh_Unauthenticated_Returns401()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "JwtBearer must gate all /api/* routes and return 401 for anonymous callers");
    }
}

