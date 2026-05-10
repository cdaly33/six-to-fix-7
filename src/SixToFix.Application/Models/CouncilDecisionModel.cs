namespace SixToFix.Application.Models;

public record CouncilDecisionModel(
    Guid AuditRunId,
    string DecisionType,
    IReadOnlyDictionary<string, int> AdjustedScores,
    decimal OverallConfidence,
    string Rationale,
    DateTimeOffset DecidedAt)
{
    public CouncilDecisionModel(string decisionType, IReadOnlyDictionary<string, int> adjustedScores)
        : this(Guid.Empty, decisionType, adjustedScores, 0m, string.Empty, DateTimeOffset.UtcNow)
    {
    }
}
