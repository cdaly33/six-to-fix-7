using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface IHubSpotClient
{
    Task<HubSpotCompanyResult> UpsertCompanyAsync(string hubSpotPortalId, string companyName, IDictionary<string, string> properties, CancellationToken ct = default);
    Task UpdateAuditResultAsync(string hubSpotCompanyId, string tier, decimal compositeScore, CancellationToken ct = default);
    Task<bool> ValidateWebhookSignatureAsync(string signature, string requestBody, CancellationToken ct = default);
}
