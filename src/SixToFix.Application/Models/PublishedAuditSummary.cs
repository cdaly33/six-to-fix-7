namespace SixToFix.Application.Models;

public record PublishedAuditSummary(
    string ClientSlug,
    string Tier,
    decimal CompositeScore,
    decimal SystemsMaturityScore,
    decimal AiReadinessPct,
    DateTimeOffset PublishedAt,
    IReadOnlyDictionary<string, decimal> CategoryScores);
