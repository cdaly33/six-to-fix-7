using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public record CouncilResult(string DecisionType, IReadOnlyDictionary<string, int> AdjustedScores);

public interface ICouncilRunner
{
    Task<CouncilResult> RunCouncilAsync(Guid auditRunId, string category, CancellationToken ct = default);
    Task<CouncilDecisionModel> RunCouncilAsync(Guid auditRunId, Guid categoryId, IReadOnlyList<PolicyFlagModel> triggeringFlags, CancellationToken ct = default);
    Task<CouncilDecisionModel?> GetCouncilDecisionAsync(Guid auditRunId, Guid categoryId, CancellationToken ct = default);
}
