namespace SixToFix.Application.Models;

public record PublishedAuditVersion(
    DateTimeOffset PublishedAt,
    string Tier,
    decimal CompositeScore);
