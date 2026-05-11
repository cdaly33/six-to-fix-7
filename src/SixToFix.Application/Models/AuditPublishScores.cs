namespace SixToFix.Application.Models;

/// <summary>
/// Carries the full set of scored values to be pushed to HubSpot on audit publish.
/// Matches the locked HubSpot field mapping specification (docs/architecture/hubspot-field-mapping.md).
/// </summary>
public record AuditPublishScores(
    int BrandScore,
    int CustomerScore,
    int OfferingScore,
    int CommunicationsScore,
    int SalesScore,
    int ManagementScore,
    decimal SystemsMaturityScore,
    int AiReadiness,
    DateTimeOffset PublishedAt);
