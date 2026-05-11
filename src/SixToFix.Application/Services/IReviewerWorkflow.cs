using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface IReviewerWorkflow
{
    Task ApproveAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default);
    Task RejectAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, string? reason, CancellationToken ct = default);
    Task<CategoryResult> EditAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, decimal newActivityScore, string? newDocumentedStrategy, string overrideReasonCode, string notes, CancellationToken ct = default);
    Task RerunAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default);
    Task<CouncilDecisionModel> EscalateAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default);
    Task<ReviewerLockoutStatus> GetLockoutStatusAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, CancellationToken ct = default);
}
