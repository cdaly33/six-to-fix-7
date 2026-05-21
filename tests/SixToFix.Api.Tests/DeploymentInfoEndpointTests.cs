using Microsoft.Extensions.Configuration;
using SixToFix.Infrastructure.Services;

namespace SixToFix.Api.Tests;

public sealed class DeploymentInfoEndpointTests
{
    [Fact]
    public async Task GetDeploymentInfo_ReturnsOk_WhenValuesAreConfigured()
    {
        using var factory = new CustomWebApplicationFactory(config =>
        {
            config["Deploy:BuildTimestamp"] = "2026-05-20T20:00:00+00:00";
            config["Deploy:Timestamp"] = "2026-05-20T21:00:00+00:00";
            config["Deploy:CommitSha"] = "abc1234def";
        });

        var response = await factory.CreateClient().GetAsync("/api/deployment-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DeploymentInfoResponse>();
        payload.Should().NotBeNull();
        payload!.BuildTimestamp.Should().Be(DateTimeOffset.Parse("2026-05-20T20:00:00+00:00"));
        payload.DeployedAt.Should().Be(DateTimeOffset.Parse("2026-05-20T21:00:00+00:00"));
        payload.CommitSha.Should().Be("abc1234");
    }

    [Fact]
    public async Task GetDeploymentInfo_ReturnsOk_WithNulls_WhenValuesAreMissing()
    {
        using var factory = new CustomWebApplicationFactory();

        var response = await factory.CreateClient().GetAsync("/api/deployment-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DeploymentInfoResponse>();
        payload.Should().NotBeNull();
        payload!.BuildTimestamp.Should().BeNull();
        payload.DeployedAt.Should().BeNull();
        payload.CommitSha.Should().BeNull();
    }

    [Fact]
    public async Task GetDeploymentInfo_IsAccessibleWithoutAuthentication()
    {
        using var factory = new CustomWebApplicationFactory();

        // No Bearer token or cookie — should still get 200
        var response = await factory.CreateClient().GetAsync("/api/deployment-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDeploymentInfo_TruncatesCommitShaToSevenChars()
    {
        using var factory = new CustomWebApplicationFactory(config =>
        {
            config["Deploy:CommitSha"] = "deadbeefcafe1234";
        });

        var response = await factory.CreateClient().GetAsync("/api/deployment-info");
        var payload = await response.Content.ReadFromJsonAsync<DeploymentInfoResponse>();

        payload!.CommitSha.Should().Be("deadbee");
    }

    [Fact]
    public void DeploymentInfoService_ReturnsNulls_WhenTimestampIsInvalid()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Deploy:BuildTimestamp"] = "not-a-date",
                ["Deploy:Timestamp"] = "not-a-date",
                ["Deploy:CommitSha"] = "abc1234"
            })
            .Build();

        var svc = new DeploymentInfoService(config);
        var info = svc.GetDeploymentInfo();

        info.BuildTimestamp.Should().BeNull("invalid build timestamp must fall back to null");
        info.DeployedAt.Should().BeNull("invalid deploy timestamp must fall back to null");
        info.CommitSha.Should().Be("abc1234");
    }
}
