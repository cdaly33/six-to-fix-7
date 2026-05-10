namespace SixToFix.Application.Models;

public record PublishResult(
    decimal CompositeScore,
    decimal SystemsMaturityScore,
    decimal AiReadinessPct,
    string Tier,
    DateTimeOffset PublishedAt);
