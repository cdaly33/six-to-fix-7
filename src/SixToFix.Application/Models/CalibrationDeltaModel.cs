namespace SixToFix.Application.Models;

public record CalibrationDeltaModel(
    Guid Id,
    Guid AuditRunId,
    string CategoryId,
    Guid ReviewerId,
    decimal OriginalActivityScore,
    decimal AdjustedActivityScore,
    string? OriginalDocumentedStrategy,
    string? AdjustedDocumentedStrategy,
    string OverrideReasonCode,
    string Notes,
    DateTimeOffset CreatedAt);
