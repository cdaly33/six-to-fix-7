using System.Text;

namespace SixToFix.Api.Tests;

public sealed class WebhookEndpointTests
{
    [Fact]
    public async Task HubSpotWebhook_AcceptsValidSignatureAndQueuesEvent()
    {
        using var factory = new CustomWebApplicationFactory();
        var payload = "{\"auditRunId\":\"3f5074d4-0a3c-4fc0-9d4f-43b07497ab7b\",\"clientSlug\":\"acme\",\"tier\":\"tier_2\",\"compositeScore\":38}";

        factory.HubSpotClient
            .Setup(client => client.ValidateWebhookSignatureAsync("valid-signature", payload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/hubspot")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-HubSpot-Signature", "valid-signature");

        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var queued = await factory.WebhookEvents.Reader.ReadAsync(CancellationToken.None);
        queued.ClientSlug.Should().Be("acme");
        queued.Tier.Should().Be("tier_2");
        queued.CompositeScore.Should().Be(38);
    }

    [Fact]
    public async Task HubSpotWebhook_ReturnsUnauthorized_WhenSignatureIsInvalid()
    {
        using var factory = new CustomWebApplicationFactory();
        const string payload = "{}";

        factory.HubSpotClient
            .Setup(client => client.ValidateWebhookSignatureAsync("bad-signature", payload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/hubspot")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-HubSpot-Signature", "bad-signature");

        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.WebhookEvents.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task HubSpotWebhook_ReturnsBadRequest_WhenPayloadIsInvalidJson()
    {
        using var factory = new CustomWebApplicationFactory();
        const string payload = "{ not-json }";

        factory.HubSpotClient
            .Setup(client => client.ValidateWebhookSignatureAsync("valid-signature", payload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/hubspot")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-HubSpot-Signature", "valid-signature");

        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
