namespace SixToFix.Application.Models;

public record TelemetryEventModel(
    Guid AuditRunId,
    int SkillRunCount,
    int PolicyTriggerCount,
    int CouncilRunCount,
    int ReviewerActionCount,
    int TotalTokensUsed,
    int TotalLatencyMs,
    DateTimeOffset InitializedAt,
    DateTimeOffset? CompletedAt);
