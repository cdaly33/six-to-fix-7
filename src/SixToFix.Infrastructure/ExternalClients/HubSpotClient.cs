using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using SixToFix.Application.Models;
using SixToFix.Application.Services;

namespace SixToFix.Infrastructure.ExternalClients;

public sealed class HubSpotClient : IHubSpotClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _webhookSecret;

    public HubSpotClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _webhookSecret = configuration["HubSpot:WebhookSecret"];
    }

    public async Task<HubSpotCompanyResult> UpsertCompanyAsync(
        string hubSpotPortalId,
        string companyName,
        IDictionary<string, string> properties,
        CancellationToken ct = default)
    {
        var allProperties = new Dictionary<string, string>(properties)
        {
            ["name"] = companyName
        };

        var payload = new
        {
            inputs = new[]
            {
                new
                {
                    idProperty = "strategicglue_client_id",
                    id = hubSpotPortalId,
                    properties = allProperties
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("crm/v3/objects/companies/batch/upsert", payload, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<HubSpotBatchResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("HubSpot returned an empty upsert response.");
        var company = result.Results.FirstOrDefault()
            ?? throw new InvalidOperationException("HubSpot upsert response did not contain a company result.");

        var resolvedName = company.Properties is not null &&
            company.Properties.TryGetValue("name", out var nameValue) &&
            !string.IsNullOrWhiteSpace(nameValue)
            ? nameValue
            : companyName;

        return new HubSpotCompanyResult(company.Id, resolvedName);
    }

    public async Task UpdateAuditResultAsync(
        string hubSpotCompanyId,
        string tier,
        decimal compositeScore,
        AuditPublishScores? scores = null,
        CancellationToken ct = default)
    {
        var properties = new Dictionary<string, object>
        {
            ["strategicglue_tier"] = tier,
            ["strategicglue_composite_score"] = compositeScore
        };

        if (scores is not null)
        {
            properties["strategicglue_brand_score"] = scores.BrandScore;
            properties["strategicglue_customer_score"] = scores.CustomerScore;
            properties["strategicglue_offering_score"] = scores.OfferingScore;
            properties["strategicglue_communications_score"] = scores.CommunicationsScore;
            properties["strategicglue_sales_score"] = scores.SalesScore;
            properties["strategicglue_management_score"] = scores.ManagementScore;
            properties["strategicglue_systems_maturity_score"] = scores.SystemsMaturityScore;
            properties["strategicglue_ai_readiness"] = scores.AiReadiness;
            // HubSpot date type requires date-only (no time component)
            properties["strategicglue_last_audit_date"] = scores.PublishedAt.ToString("yyyy-MM-dd");
        }

        var payload = new { properties };
        using var response = await _httpClient.PatchAsJsonAsync($"crm/v3/objects/companies/{hubSpotCompanyId}", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<bool> ValidateWebhookSignatureAsync(string signature, string requestBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret) || string.IsNullOrWhiteSpace(signature))
        {
            return Task.FromResult(false);
        }

        var secretBytes = Encoding.UTF8.GetBytes(_webhookSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
        using var hmac = new HMACSHA256(secretBytes);
        var computed = hmac.ComputeHash(bodyBytes);
        var expected = Convert.ToHexString(computed).ToLowerInvariant();
        var provided = signature.Trim().ToLowerInvariant();

        return Task.FromResult(FixedTimeEquals(expected, provided));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private sealed record HubSpotBatchResponse(IReadOnlyList<HubSpotCompanyRecord> Results);

    private sealed record HubSpotCompanyRecord(string Id, IDictionary<string, string>? Properties);
}
