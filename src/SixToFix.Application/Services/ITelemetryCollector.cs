using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface ITelemetryCollector
{
    Task InitializeTelemetryAsync(Guid auditRunId, CancellationToken ct = default);
    Task IncrementSkillRunCountAsync(Guid auditRunId, int tokensUsed, int latencyMs, CancellationToken ct = default);
    Task IncrementPolicyTriggerCountAsync(Guid auditRunId, CancellationToken ct = default);
    Task IncrementCouncilRunCountAsync(Guid auditRunId, CancellationToken ct = default);
    Task IncrementReviewerActionCountAsync(Guid auditRunId, CancellationToken ct = default);
    Task FinalizeTelemetryAsync(Guid auditRunId, CancellationToken ct = default);
    Task<IReadOnlyList<TelemetryEventModel>> GetDailyMetricsAsync(DateOnly date, CancellationToken ct = default);
}
