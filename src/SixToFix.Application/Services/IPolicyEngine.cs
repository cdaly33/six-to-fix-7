using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public record PolicyEvaluationResult(bool RequiresEscalation, IReadOnlyList<string> TriggeredPolicyIds);

public interface IPolicyEngine
{
    Task<PolicyEvaluationResult> EvaluateCategoryAsync(Guid auditRunId, string category, CancellationToken ct = default);
    IReadOnlyList<PolicyFlagModel> EvaluateCategory(CategoryResultPayload payload, PolicyEvaluationContext context);
    bool RequiresCouncilEscalation(IReadOnlyList<PolicyFlagModel> flags);
    PolicyEvaluationSummary SummarizeFlags(IReadOnlyList<PolicyFlagModel> flags);
}
