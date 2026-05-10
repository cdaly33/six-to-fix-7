namespace SixToFix.Application.Models;

// Stub: Neo's Publisher will write to this channel. Converge on merge.
public record HubSpotEvent(
    Guid AuditRunId,
    string ClientSlug,
    string Tier,
    decimal CompositeScore)
{
    public DateTimeOffset? PublishedAt { get; init; }

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
