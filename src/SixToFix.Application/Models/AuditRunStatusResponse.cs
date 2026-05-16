namespace SixToFix.Application.Models;

public sealed record AuditRunStatusResponse(
    string Status,
    int CompletedSkillCount,
    int TotalSkillCount,
    string? CurrentSkillName,
    string? FailureReason,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);
