namespace SixToFix.Application.Models;

public record HubSpotEvent(
    Guid AuditRunId,
    string ClientSlug,
    string Tier,
    decimal CompositeScore)
{
    /// <summary>The HubSpot company hs_object_id. Populated on audit-publish events; null for inbound webhook echo events.</summary>
    public string? HubSpotCompanyId { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Full scored data for the HubSpot audit-result push. Null for inbound webhook echo events.</summary>
    public AuditPublishScores? Scores { get; init; }

    public HubSpotEvent(
        Guid auditRunId,
        string clientSlug,
        string tier,
        decimal compositeScore,
        DateTimeOffset publishedAt)
        : this(auditRunId, clientSlug, tier, compositeScore)
    {
        PublishedAt = publishedAt;
    }
}
