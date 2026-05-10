using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface ICalibrationTracker
{
    Task<CalibrationDeltaModel> RecordDeltaAsync(Guid auditRunId, Guid categoryId, Guid reviewerId, decimal originalActivityScore, decimal adjustedActivityScore, string? originalDocumentedStrategy, string? adjustedDocumentedStrategy, string overrideReasonCode, string notes, CancellationToken ct = default);
    Task<IReadOnlyList<CalibrationDeltaModel>> GetDeltasForAuditRunAsync(Guid auditRunId, CancellationToken ct = default);
    Task<CalibrationSummary> GetCalibrationSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
