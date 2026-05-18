namespace SixToFix.Api.Tests;

public sealed class AuthEndpointTests
{
    [Fact]
    public async Task Login_ReturnsToken_WhenCredentialsAreValid()
    {
        using var factory = new CustomWebApplicationFactory();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        factory.AuthService
            .Setup(service => service.LoginAsync("reviewer@strategicglue.com", "Password123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult("jwt-token", "reviewer@strategicglue.com", userId, tenantId, "strategic-glue", ["Reviewer"]));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("reviewer@strategicglue.com", "Password123!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().Be("jwt-token");
        payload.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.AuthService
            .Setup(service => service.LoginAsync("reviewer@strategicglue.com", "Password123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginResult?)null);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("reviewer@strategicglue.com", "Password123!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        using var factory = new CustomWebApplicationFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = string.Empty, password = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
