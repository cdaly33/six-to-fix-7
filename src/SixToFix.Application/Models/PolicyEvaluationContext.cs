namespace SixToFix.Application.Models;

public record PolicyEvaluationContext(
    decimal TenantMedianScore,
    decimal TenantStdDev,
    Guid AuditRunId);
